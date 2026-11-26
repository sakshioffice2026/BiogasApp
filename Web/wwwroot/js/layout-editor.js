(function () {
    'use strict';

    var root = document.getElementById('layoutEditor');
    if (!root) return;

    var URLS = {
        generate: root.getAttribute('data-generate-url'),
        evaluate: root.getAttribute('data-evaluate-url'),
        save: root.getAttribute('data-save-url'),
        version: root.getAttribute('data-version-url'),
        deleteVersion: root.getAttribute('data-delete-version-url'),
        saveEquipment: root.getAttribute('data-save-equipment-url'),
        deleteEquipment: root.getAttribute('data-delete-equipment-url')
    };
    var PROJECT_ID = root.getAttribute('data-project-id');
    var tokenInput = root.querySelector('input[name="__RequestVerificationToken"]');
    var DRAG_THRESHOLD_PX = 3;

    function $(id) { return document.getElementById(id); }

    var el = {
        canvas: $('layoutCanvas'),
        message: $('layoutMessage'),
        generate: $('btnGenerate'),
        saveVersion: $('btnSaveVersion'),
        lock: $('btnLock'),
        rotate: $('btnRotate'),
        zoomIn: $('btnZoomIn'),
        zoomOut: $('btnZoomOut'),
        zoomFit: $('btnZoomFit'),
        versionSelect: $('versionSelect'),
        deleteVersion: $('btnDeleteVersion'),
        downloadDxf: $('btnDownloadDxf'),
        addCustom: $('btnAddCustom'),
        equipmentList: $('equipmentList'),
        selectedInfo: $('selectedInfo'),
        warningSummary: $('warningSummary'),
        warningList: $('warningList'),
        modal: $('customModal'),
        ceId: $('ceId'),
        ceTag: $('ceTag'),
        ceName: $('ceName'),
        ceZone: $('ceZone'),
        ceHazard: $('ceHazard'),
        ceShape: $('ceShape'),
        ceWidth: $('ceWidth'),
        ceLength: $('ceLength'),
        ceDiameter: $('ceDiameter'),
        ceHeight: $('ceHeight'),
        ceQty: $('ceQty'),
        ceRoad: $('ceRoad'),
        ceRotate: $('ceRotate'),
        ceError: $('ceError'),
        ceSave: $('ceSave'),
        ceDelete: $('ceDelete'),
        undo: $('btnUndo'),
        redo: $('btnRedo'),
        view2d: $('btnView2d'),
        view3d: $('btnView3d'),
        zoomGroup: $('zoomGroup'),
        layout3d: $('layout3d')
    };

    function readJson(id) {
        var node = $(id);
        if (!node) return null;
        try { return JSON.parse(node.textContent); } catch (e) { return null; }
    }

    var layout = readJson('layoutData');
    var equipment = readJson('equipmentData') || [];
    var run = readJson('runData') || {};
    var siteData = readJson('siteData') || {};

    var unsavedLayout = null;
    var selected = null;
    var svg = null;
    var baseView = null;
    var view = null;
    var drag = null;
    var pan = null;
    var busy = false;
    var hist = null;
    var viewer3d = null;
    var mode3d = false;
    var clearWarnings = [];

    /* ---------- helpers ---------- */

    function esc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function fmt(v, d) {
        return (typeof v === 'number' && isFinite(v)) ? v.toFixed(d === undefined ? 1 : d) : '-';
    }

    function showMessage(text, type) {
        el.message.className = 'alert alert-' + (type || 'info') + ' py-2 small';
        el.message.textContent = text;
    }

    function clearMessage() {
        el.message.className = 'd-none';
        el.message.textContent = '';
    }

    function request(url, opts) {
        return fetch(url, opts).then(function (r) {
            return r.text().then(function (t) {
                var data = null;
                try { data = t ? JSON.parse(t) : null; } catch (e) { data = null; }
                return { ok: r.ok, status: r.status, data: data };
            });
        });
    }

    function post(url, body) {
        var headers = { 'Content-Type': 'application/json' };
        if (tokenInput) headers.RequestVerificationToken = tokenInput.value;
        return request(url, { method: 'POST', headers: headers, body: JSON.stringify(body || {}) });
    }

    function failText(res, fallback) {
        if (res && res.data && res.data.message) return res.data.message;
        return fallback + ' (' + (res ? res.status : 'no response') + ').';
    }

    function layoutForServer() {
        var copy = JSON.parse(JSON.stringify(layout));
        copy.svgMarkup = null;
        return copy;
    }

    function setBusy(b) {
        busy = b;
        root.classList.toggle('is-busy', b);
        updateButtons();
    }

    function findEquipment(id) {
        for (var i = 0; i < equipment.length; i++) {
            if (equipment[i].id === id) return equipment[i];
        }
        return null;
    }

    function findItem(eq, inst) {
        if (!layout) return null;
        for (var i = 0; i < layout.items.length; i++) {
            var it = layout.items[i];
            if (it.equipmentItemId === eq && it.instanceNo === inst) return it;
        }
        return null;
    }

    function selectedItem() {
        return selected ? findItem(selected.eq, selected.inst) : null;
    }

    function markUnsaved() {
        layout.id = null;
        unsavedLayout = layout;
        el.versionSelect.value = '';
    }

    /* ---------- buttons ---------- */

    function updateButtons() {
        var item = selectedItem();
        el.generate.disabled = busy || !run.plotReady;
        el.saveVersion.disabled = busy || !layout || !!layout.id;
        el.lock.disabled = busy || !item;
        el.rotate.disabled = busy || !item;
        el.lock.textContent = item && item.isLocked ? 'Unlock' : 'Lock';
        el.deleteVersion.disabled = busy || !(layout && layout.id);
        el.downloadDxf.disabled = busy || !(layout && layout.id);
        if (el.undo) el.undo.disabled = busy || !(hist && hist.canUndo());
        if (el.redo) el.redo.disabled = busy || !(hist && hist.canRedo());
    }

    /* ---------- view (zoom and pan) ---------- */

    function parseViewBox(s) {
        var p = String(s || '').trim().split(/[\s,]+/).map(parseFloat);
        if (p.length !== 4 || p.some(function (v) { return !isFinite(v); })) return null;
        return { x: p[0], y: p[1], w: p[2], h: p[3] };
    }

    function applyView() {
        if (!svg || !view) return;
        svg.setAttribute('viewBox', view.x + ' ' + view.y + ' ' + view.w + ' ' + view.h);
    }

    function zoomAt(px, py, factor) {
        if (!view || !baseView) return;
        var w = view.w * factor;
        if (w < baseView.w * 0.03 || w > baseView.w * 4) return;
        var h = view.h * factor;
        view = {
            x: px - (px - view.x) * factor,
            y: py - (py - view.y) * factor,
            w: w,
            h: h
        };
        applyView();
    }

    function zoomCenter(factor) {
        if (!view) return;
        zoomAt(view.x + view.w / 2, view.y + view.h / 2, factor);
    }

    function fitView() {
        if (!baseView) return;
        view = { x: baseView.x, y: baseView.y, w: baseView.w, h: baseView.h };
        applyView();
    }

    function toSvg(e) {
        var pt = svg.createSVGPoint();
        pt.x = e.clientX;
        pt.y = e.clientY;
        var m = svg.getScreenCTM();
        return m ? pt.matrixTransform(m.inverse()) : { x: 0, y: 0 };
    }

    /* ---------- render ---------- */

    function renderLayout() {
        if (!layout || !layout.svgMarkup) {
            el.canvas.innerHTML = '<div class="layout-empty text-muted" id="layoutEmpty">Generate a layout to see it here.</div>';
            svg = null;
            baseView = null;
            view = null;
        } else {
            el.canvas.innerHTML = layout.svgMarkup;
            svg = el.canvas.querySelector('svg');
            baseView = svg ? parseViewBox(svg.getAttribute('viewBox')) : null;
            if (!view && baseView) view = { x: baseView.x, y: baseView.y, w: baseView.w, h: baseView.h };
            applyView();
            applySelection();
        }

        renderWarnings();
        renderSelected();
        renderEquipmentList();
        sync3d();
        updateButtons();
    }

    function applySelection() {
        if (!svg) return;
        Array.prototype.forEach.call(svg.querySelectorAll('g.item'), function (g) {
            var on = selected &&
                g.getAttribute('data-eq') === selected.eq &&
                parseInt(g.getAttribute('data-inst'), 10) === selected.inst;
            g.classList.toggle('selected', !!on);
        });
    }

    function select(eq, inst) {
        selected = eq ? { eq: eq, inst: inst || 1 } : null;
        applySelection();
        renderSelected();
        renderWarnings();
        renderEquipmentList();
        sync3d();
        updateButtons();
    }

    function itemWarnings(item) {
        if (!layout || !item) return [];
        return layout.warnings.filter(function (w) {
            return w.equipmentItemId === item.equipmentItemId && (w.instanceNo || 1) === item.instanceNo;
        });
    }

    function renderSelected() {
        var it = selectedItem();
        if (!it) {
            el.selectedInfo.textContent = 'Click an item on the plan.';
            return;
        }

        var size = it.shape === 'Circle'
            ? '&Oslash; ' + fmt(it.widthM) + ' m'
            : fmt(it.widthM) + ' &times; ' + fmt(it.lengthM) + ' m';
        var tag = it.instanceNo > 1 ? it.tag + '-' + it.instanceNo : it.tag;

        el.selectedInfo.innerHTML =
            '<div><strong>' + esc(tag) + '</strong> ' + esc(it.name) + '</div>' +
            '<div>Zone: ' + esc(it.zone) + '</div>' +
            '<div>Size: ' + size + '</div>' +
            '<div>Centre: ' + fmt(it.x) + ', ' + fmt(it.y) + ' m</div>' +
            '<div>Rotation: ' + fmt(it.rotationDeg, 0) + '&deg;</div>' +
            '<div>Locked: ' + (it.isLocked ? 'yes' : 'no') + '</div>' +
            '<div>Warnings: ' + itemWarnings(it).length + '</div>';
    }

    function statusClass(status) {
        if (status === 'Official') return 'rule-official';
        if (status === 'Reference') return 'rule-reference';
        if (status === 'Assumed') return 'rule-assumed';
        return 'rule-none';
    }

    function renderWarnings() {
        if (!layout) {
            el.warningSummary.textContent = '';
            el.warningList.innerHTML = '<div class="p-3 small text-muted">No layout yet.</div>';
            return;
        }

        var list = allWarnings();
        if (!list.length) {
            el.warningSummary.textContent = '0';
            el.warningList.innerHTML = '<div class="p-3 small text-muted">No warnings. This is still a draft for engineer review.</div>';
            return;
        }

        var o = 0, r = 0, a = 0;
        list.forEach(function (w) {
            if (w.ruleStatus === 'Official') o++;
            else if (w.ruleStatus === 'Reference') r++;
            else if (w.ruleStatus === 'Assumed') a++;
        });
        el.warningSummary.textContent = list.length + ' (' + o + ' Official, ' + r + ' Reference, ' + a + ' Assumed)';

        el.warningList.innerHTML = list.map(function (w, i) {
            var on = selected && w.equipmentItemId === selected.eq && (w.instanceNo || 1) === selected.inst;
            var dist = '';
            if (w.actualDistanceM != null || w.requiredDistanceM != null) {
                dist = '<div class="text-muted">Actual ' + fmt(w.actualDistanceM) + ' m, required ' + fmt(w.requiredDistanceM) + ' m</div>';
            }
            var src = w.sourceNote ? '<div class="text-muted">Source: ' + esc(w.sourceNote) + '</div>' : '';
            return '<div class="warning-item' + (on ? ' is-selected' : '') + '" data-idx="' + i + '">' +
                '<span class="badge ' + statusClass(w.ruleStatus) + '">' + esc(w.ruleStatus || 'Check') + '</span> ' +
                esc(w.message) + dist + src + '</div>';
        }).join('');
    }

    function sizeText(e) {
        var plan = e.shape === 'Circle'
            ? 'Ø' + fmt(e.diameterM) + ' m'
            : fmt(e.widthM) + ' × ' + fmt(e.lengthM) + ' m';
        return e.heightM != null ? plan + ', H ' + fmt(e.heightM) + ' m' : plan;
    }

    function renderEquipmentList() {
        if (!equipment.length) {
            el.equipmentList.innerHTML = '<div class="p-3 small text-muted">No equipment. Run a simulation or add an item.</div>';
            return;
        }

        el.equipmentList.innerHTML = equipment.map(function (e) {
            var on = selected && selected.eq === e.id;
            return '<div class="list-group-item' + (e.isEnabled ? '' : ' is-disabled') + (on ? ' is-selected' : '') + '" data-id="' + esc(e.id) + '">' +
                '<div class="d-flex justify-content-between align-items-start gap-1">' +
                '<div><span class="eq-tag">' + esc(e.tag) + '</span> ' + esc(e.name) +
                (e.isCustom ? ' <span class="badge text-bg-secondary">Custom</span>' : '') +
                '<div class="eq-size">' + esc(sizeText(e)) + ' &times; ' + (e.quantity || 1) + '</div>' +
                '<div class="input-group input-group-sm mt-1" style="max-width:150px">' +
                '<span class="input-group-text">H (m)</span>' +
                '<input type="number" step="any" min="0" class="form-control eq-height" value="' + (e.heightM != null ? esc(String(e.heightM)) : '') + '" /></div></div>' +
                '<div class="d-flex flex-column align-items-end gap-1">' +
                '<input type="checkbox" class="form-check-input eq-enable" title="Include in layout"' + (e.isEnabled ? ' checked' : '') + ' />' +
                (e.isCustom ? '<button type="button" class="btn btn-sm btn-outline-secondary py-0 eq-edit">Edit</button>' : '') +
                '</div></div></div>';
        }).join('');
    }

    /* ---------- server actions ---------- */

    function adoptLayout(dto, isSaved) {
        layout = dto;
        layout.warnings = layout.warnings || [];
        layout.items = layout.items || [];
        layout.roads = layout.roads || [];
        if (!isSaved) {
            layout.id = null;
            unsavedLayout = layout;
            el.versionSelect.value = '';
        }
        if (selected && !findItem(selected.eq, selected.inst)) selected = null;
        renderLayout();
    }

    function evaluate() {
        setBusy(true);
        post(URLS.evaluate, layoutForServer()).then(function (res) {
            setBusy(false);
            if (!res.ok || !res.data) {
                showMessage(failText(res, 'Could not check the layout'), 'danger');
                renderLayout();
                return;
            }
            adoptLayout(res.data, false);
            if (hist) hist.commit();
            showMessage('Layout updated. ' + res.data.warnings.length + ' warning(s). Warnings never block the layout.', res.data.warnings.length ? 'warning' : 'success');
        }).catch(function () {
            setBusy(false);
            showMessage('Could not reach the server. Try again.', 'danger');
            renderLayout();
        });
    }

    el.generate.addEventListener('click', function () {
        clearMessage();
        var locked = layout ? layout.items.filter(function (i) { return i.isLocked; }) : [];
        setBusy(true);
        showMessage('Generating layout...', 'info');

        post(URLS.generate, { projectId: PROJECT_ID, lockedItems: locked }).then(function (res) {
            setBusy(false);
            if (!res.ok || !res.data) {
                showMessage(failText(res, 'Could not generate the layout'), 'danger');
                return;
            }
            adoptLayout(res.data, false);
            if (hist) hist.commit();
            showMessage('Layout generated. ' + res.data.warnings.length + ' warning(s). Locked items were kept in place.',
                res.data.warnings.length ? 'warning' : 'success');
        }).catch(function () {
            setBusy(false);
            showMessage('Could not reach the server. Try again.', 'danger');
        });
    });

    function versionLabel(v) {
        return 'v' + v.versionNo + ' - ' + v.origin + ' - rules v' + (v.ruleSetVersion || '-');
    }

    function upsertVersionOption(v) {
        var opt = null;
        Array.prototype.forEach.call(el.versionSelect.options, function (o) {
            if (o.value === v.id) opt = o;
        });
        if (!opt) {
            opt = document.createElement('option');
            opt.value = v.id;
            el.versionSelect.appendChild(opt);
        }
        opt.textContent = versionLabel(v);
        el.versionSelect.value = v.id;
    }

    el.saveVersion.addEventListener('click', function () {
        if (!layout) return;
        clearMessage();
        setBusy(true);

        post(URLS.save, layoutForServer()).then(function (res) {
            setBusy(false);
            if (!res.ok || !res.data) {
                showMessage(failText(res, 'Could not save the version'), 'danger');
                return;
            }
            unsavedLayout = null;
            adoptLayout(res.data, true);
            upsertVersionOption(res.data);
            updateButtons();
            if (hist) hist.reset();
            showMessage('Saved as version ' + res.data.versionNo + ' (rule set v' + (res.data.ruleSetVersion || '-') + ').', 'success');
        }).catch(function () {
            setBusy(false);
            showMessage('Could not reach the server. Try again.', 'danger');
        });
    });

    el.versionSelect.addEventListener('change', function () {
        var id = el.versionSelect.value;
        clearMessage();

        if (!id) {
            if (unsavedLayout) {
                layout = unsavedLayout;
                renderLayout();
                if (hist) hist.reset();
            } else {
                showMessage('There is no unsaved layout to show.', 'info');
                el.versionSelect.value = layout && layout.id ? layout.id : '';
            }
            return;
        }

        setBusy(true);
        request(URLS.version + '?id=' + encodeURIComponent(id), { method: 'GET' }).then(function (res) {
            setBusy(false);
            if (!res.ok || !res.data) {
                showMessage(failText(res, 'Could not load the version'), 'danger');
                el.versionSelect.value = layout && layout.id ? layout.id : '';
                return;
            }
            adoptLayout(res.data, true);
            if (hist) hist.reset();
        }).catch(function () {
            setBusy(false);
            showMessage('Could not reach the server. Try again.', 'danger');
        });
    });

    el.deleteVersion.addEventListener('click', function () {
        if (!layout || !layout.id) return;
        if (!window.confirm('Delete this saved version?')) return;

        var id = layout.id;
        setBusy(true);
        post(URLS.deleteVersion + '?id=' + encodeURIComponent(id), {}).then(function (res) {
            setBusy(false);
            if (!res.ok) {
                showMessage(failText(res, 'Could not delete the version'), 'danger');
                return;
            }
            Array.prototype.forEach.call(el.versionSelect.options, function (o) {
                if (o.value === id) o.parentNode.removeChild(o);
            });
            layout = unsavedLayout;
            selected = null;
            el.versionSelect.value = '';
            renderLayout();
            if (hist) hist.reset();
            showMessage('Version deleted.', 'success');
        }).catch(function () {
            setBusy(false);
            showMessage('Could not reach the server. Try again.', 'danger');
        });
    });

    el.downloadDxf.addEventListener('click', function () {
        if (!layout || !layout.id) return;

        setBusy(true);
        post(URLS.deleteVersion.replace(/DeleteVersion/i, 'DownloadDxf') + '?id=' + encodeURIComponent(layout.id), {}).then(function (res) {
            setBusy(false);
            if (!res.ok || !res.data || !res.data.success) {
                showMessage((res.data && res.data.message) || failText(res, 'Could not generate the DXF'), 'danger');
                return;
            }
            window.location.href = res.data.downloadUrl;
            showMessage('DXF generated: ' + res.data.fileName, 'success');
        }).catch(function () {
            setBusy(false);
            showMessage('Could not reach the server. Try again.', 'danger');
        });
    });

    /* ---------- lock and rotate ---------- */

    el.lock.addEventListener('click', function () {
        var it = selectedItem();
        if (!it) return;
        it.isLocked = !it.isLocked;
        markUnsaved();

        if (svg) {
            var g = svg.querySelector('g.item[data-eq="' + it.equipmentItemId + '"][data-inst="' + it.instanceNo + '"]');
            if (g) {
                g.classList.toggle('locked', it.isLocked);
                g.setAttribute('data-locked', it.isLocked ? '1' : '0');
            }
        }
        renderSelected();
        updateButtons();
        if (hist) hist.commit();
    });

    el.rotate.addEventListener('click', function () {
        var it = selectedItem();
        if (!it) return;

        if (it.isLocked) {
            showMessage('This item is locked. Unlock it to rotate it.', 'warning');
            return;
        }
        if (it.shape === 'Circle') {
            showMessage('A round item does not need rotation.', 'info');
            return;
        }
        var eq = findEquipment(it.equipmentItemId);
        if (eq && eq.allowRotation === false) {
            showMessage('This item is set to not rotate.', 'warning');
            return;
        }

        it.rotationDeg = ((it.rotationDeg || 0) + 90) % 360;
        evaluate();
    });

    /* ---------- zoom buttons ---------- */

    el.zoomIn.addEventListener('click', function () { zoomCenter(0.8); });
    el.zoomOut.addEventListener('click', function () { zoomCenter(1.25); });
    el.zoomFit.addEventListener('click', fitView);

    /* ---------- canvas pointer handling ---------- */

    el.canvas.addEventListener('wheel', function (e) {
        if (!svg) return;
        e.preventDefault();
        var p = toSvg(e);
        zoomAt(p.x, p.y, e.deltaY < 0 ? 0.85 : 1 / 0.85);
    }, { passive: false });

    el.canvas.addEventListener('pointerdown', function (e) {
        if (!svg || busy || e.button !== 0) return;

        var g = e.target.closest ? e.target.closest('g.item') : null;

        if (g) {
            var eq = g.getAttribute('data-eq');
            var inst = parseInt(g.getAttribute('data-inst'), 10) || 1;
            select(eq, inst);

            drag = {
                g: g,
                item: findItem(eq, inst),
                start: toSvg(e),
                clientX: e.clientX,
                clientY: e.clientY,
                dx: 0,
                dy: 0,
                moved: false,
                warnedLocked: false
            };
        } else {
            var m = svg.getScreenCTM();
            pan = {
                clientX: e.clientX,
                clientY: e.clientY,
                view: { x: view.x, y: view.y, w: view.w, h: view.h },
                scale: m ? m.a : 1,
                moved: false
            };
            el.canvas.classList.add('is-panning');
        }

        document.addEventListener('pointermove', onPointerMove);
        document.addEventListener('pointerup', onPointerUp);
        document.addEventListener('pointercancel', onPointerUp);
        e.preventDefault();
    });

    function onPointerMove(e) {
        if (drag) {
            var px = Math.abs(e.clientX - drag.clientX) + Math.abs(e.clientY - drag.clientY);
            if (!drag.moved && px < DRAG_THRESHOLD_PX) return;

            if (drag.item && drag.item.isLocked) {
                if (!drag.warnedLocked) {
                    drag.warnedLocked = true;
                    showMessage('This item is locked. Unlock it to move it.', 'warning');
                }
                return;
            }

            drag.moved = true;
            var cur = toSvg(e);
            drag.dx = cur.x - drag.start.x;
            drag.dy = cur.y - drag.start.y;
            drag.g.classList.add('is-dragging');
            drag.g.setAttribute('transform', 'translate(' + drag.dx + ' ' + drag.dy + ')');
        } else if (pan) {
            var cx = e.clientX - pan.clientX;
            var cy = e.clientY - pan.clientY;
            if (Math.abs(cx) + Math.abs(cy) >= DRAG_THRESHOLD_PX) pan.moved = true;
            view = {
                x: pan.view.x - cx / pan.scale,
                y: pan.view.y - cy / pan.scale,
                w: pan.view.w,
                h: pan.view.h
            };
            applyView();
        }
    }

    function onPointerUp() {
        document.removeEventListener('pointermove', onPointerMove);
        document.removeEventListener('pointerup', onPointerUp);
        document.removeEventListener('pointercancel', onPointerUp);

        if (drag) {
            var d = drag;
            drag = null;
            d.g.classList.remove('is-dragging');

            if (d.moved && d.item) {
                // Plan Y points up while SVG Y points down
                d.item.x = d.item.x + d.dx;
                d.item.y = d.item.y - d.dy;
                evaluate();
            } else {
                d.g.removeAttribute('transform');
            }
        } else if (pan) {
            var p = pan;
            pan = null;
            el.canvas.classList.remove('is-panning');
            if (!p.moved) select(null);
        }
    }

    /* ---------- warning list and equipment list clicks ---------- */

    el.warningList.addEventListener('click', function (e) {
        var row = e.target.closest('.warning-item');
        if (!row || !layout) return;
        var w = allWarnings()[parseInt(row.getAttribute('data-idx'), 10)];
        if (w && w.equipmentItemId) select(w.equipmentItemId, w.instanceNo || 1);
    });

    function saveEquipmentDto(dto) {
        return post(URLS.saveEquipment, dto);
    }

    el.equipmentList.addEventListener('click', function (e) {
        var row = e.target.closest('.list-group-item');
        if (!row) return;
        var id = row.getAttribute('data-id');
        var eq = findEquipment(id);
        if (!eq) return;

        if (e.target.classList.contains('eq-edit')) {
            openModal(eq);
            return;
        }
        if (e.target.classList.contains('eq-enable')) return;
        if (e.target.classList.contains('eq-height')) return;

        if (layout) {
            for (var i = 0; i < layout.items.length; i++) {
                if (layout.items[i].equipmentItemId === id) {
                    select(id, layout.items[i].instanceNo);
                    return;
                }
            }
        }
    });

    el.equipmentList.addEventListener('change', function (e) {
        if (!e.target.classList.contains('eq-enable')) return;
        var row = e.target.closest('.list-group-item');
        var eq = row ? findEquipment(row.getAttribute('data-id')) : null;
        if (!eq) return;

        var previous = eq.isEnabled;
        eq.isEnabled = e.target.checked;
        setBusy(true);

        saveEquipmentDto(eq).then(function (res) {
            setBusy(false);
            if (!res.ok || !res.data || !res.data.success) {
                eq.isEnabled = previous;
                renderEquipmentList();
                showMessage(failText(res, 'Could not update the item'), 'danger');
                return;
            }
            renderEquipmentList();
            showMessage('Item updated. Generate the layout again to apply it.', 'info');
        }).catch(function () {
            setBusy(false);
            eq.isEnabled = previous;
            renderEquipmentList();
            showMessage('Could not reach the server. Try again.', 'danger');
        });
    });

    el.equipmentList.addEventListener('change', function (e) {
        if (!e.target.classList.contains('eq-height')) return;
        var row = e.target.closest('.list-group-item');
        var eq = row ? findEquipment(row.getAttribute('data-id')) : null;
        if (!eq) return;

        var previous = eq.heightM;
        var v = parseFloat(e.target.value);
        eq.heightM = isFinite(v) && v >= 0 ? v : null;
        sync3d();
        setBusy(true);

        saveEquipmentDto(eq).then(function (res) {
            setBusy(false);
            if (!res.ok || !res.data || !res.data.success) {
                eq.heightM = previous;
                renderEquipmentList();
                sync3d();
                showMessage(failText(res, 'Could not update the height'), 'danger');
                return;
            }
            renderEquipmentList();
            sync3d();
            showMessage('Height updated.', 'info');
        }).catch(function () {
            setBusy(false);
            eq.heightM = previous;
            renderEquipmentList();
            showMessage('Could not reach the server. Try again.', 'danger');
        });
    });

    /* ---------- custom equipment modal ---------- */

    function modalInstance() {
        return typeof bootstrap !== 'undefined' ? bootstrap.Modal.getOrCreateInstance(el.modal) : null;
    }

    function toggleShapeFields() {
        var circle = el.ceShape.value === 'Circle';
        Array.prototype.forEach.call(el.modal.querySelectorAll('.ce-rect'), function (n) { n.classList.toggle('d-none', circle); });
        Array.prototype.forEach.call(el.modal.querySelectorAll('.ce-circle'), function (n) { n.classList.toggle('d-none', !circle); });
    }

    function numOrNull(input) {
        var v = parseFloat(input.value);
        return isFinite(v) ? v : null;
    }

    function openModal(eq) {
        el.ceError.classList.add('d-none');
        el.ceError.textContent = '';

        el.ceId.value = eq ? eq.id : '';
        el.ceTag.value = eq ? eq.tag : '';
        el.ceName.value = eq ? eq.name : '';
        el.ceZone.value = eq ? eq.zone : 'Process';
        el.ceHazard.value = eq ? eq.hazardClass : 'Other';
        el.ceShape.value = eq ? eq.shape : 'Rectangle';
        el.ceWidth.value = eq && eq.widthM != null ? eq.widthM : '';
        el.ceLength.value = eq && eq.lengthM != null ? eq.lengthM : '';
        el.ceDiameter.value = eq && eq.diameterM != null ? eq.diameterM : '';
        el.ceHeight.value = eq && eq.heightM != null ? eq.heightM : '';
        el.ceQty.value = eq ? eq.quantity : 1;
        el.ceRoad.checked = eq ? eq.needsRoadAccess !== false : true;
        el.ceRotate.checked = eq ? eq.allowRotation !== false : true;
        el.ceDelete.classList.toggle('d-none', !eq);
        toggleShapeFields();

        var m = modalInstance();
        if (m) m.show();
    }

    function modalError(text) {
        el.ceError.textContent = text;
        el.ceError.classList.remove('d-none');
    }

    el.addCustom.addEventListener('click', function () { openModal(null); });
    el.ceShape.addEventListener('change', toggleShapeFields);

    el.ceSave.addEventListener('click', function () {
        var circle = el.ceShape.value === 'Circle';
        var dto = {
            id: el.ceId.value || null,
            projectId: PROJECT_ID,
            tag: el.ceTag.value.trim(),
            name: el.ceName.value.trim(),
            hazardClass: el.ceHazard.value,
            zone: el.ceZone.value,
            shape: el.ceShape.value,
            widthM: circle ? null : numOrNull(el.ceWidth),
            lengthM: circle ? null : numOrNull(el.ceLength),
            diameterM: circle ? numOrNull(el.ceDiameter) : null,
            heightM: numOrNull(el.ceHeight),
            sizeSource: 'UserOverride',
            quantity: Math.max(1, parseInt(el.ceQty.value, 10) || 1),
            isEnabled: true,
            isCustom: true,
            allowRotation: el.ceRotate.checked,
            needsRoadAccess: el.ceRoad.checked
        };

        if (!dto.tag || !dto.name) { modalError('Enter a tag and a name.'); return; }
        if (circle ? !(dto.diameterM > 0) : !(dto.widthM > 0 && dto.lengthM > 0)) {
            modalError('Enter the size of the item.');
            return;
        }

        el.ceSave.disabled = true;
        saveEquipmentDto(dto).then(function (res) {
            el.ceSave.disabled = false;
            if (!res.ok || !res.data || !res.data.success) {
                modalError(failText(res, 'Could not save the item'));
                return;
            }

            var saved = res.data.item;
            var found = false;
            for (var i = 0; i < equipment.length; i++) {
                if (equipment[i].id === saved.id) { equipment[i] = saved; found = true; }
            }
            if (!found) equipment.push(saved);

            renderEquipmentList();
            var m = modalInstance();
            if (m) m.hide();
            showMessage('Item saved. Generate the layout again to place it.', 'info');
        }).catch(function () {
            el.ceSave.disabled = false;
            modalError('Could not reach the server. Try again.');
        });
    });

    el.ceDelete.addEventListener('click', function () {
        var id = el.ceId.value;
        if (!id || !window.confirm('Delete this item?')) return;

        post(URLS.deleteEquipment + '?id=' + encodeURIComponent(id), {}).then(function (res) {
            if (!res.ok) {
                modalError(failText(res, 'Could not delete the item'));
                return;
            }
            equipment = equipment.filter(function (e) { return e.id !== id; });
            renderEquipmentList();
            var m = modalInstance();
            if (m) m.hide();
            showMessage('Item deleted. Generate the layout again.', 'info');
        }).catch(function () {
            modalError('Could not reach the server. Try again.');
        });
    });

    /* ---------- 3D view ---------- */

    function allWarnings() {
        var base = (layout && layout.warnings) || [];
        return base.concat(clearWarnings);
    }

    function sync3d() {
        if (!mode3d || !viewer3d) return;
        viewer3d.update({
            site: siteData,
            layout: layout,
            equipment: equipment,
            selected: selected
        });
    }

    function ensureViewer(cb) {
        if (viewer3d) { cb(); return; }
        var url = root.getAttribute('data-viewer3d-url');
        import(url).then(function (mod) {
            if (!mode3d) return;
            viewer3d = mod.createLayout3DViewer({
                container: el.layout3d,
                onSelect: function (eq, inst) { select(eq, inst); },
                onClearance: function (res) {
                    clearWarnings = (res && res.warnings) || [];
                    renderWarnings();
                }
            });
            cb();
        }).catch(function (err) {
            setViewMode(false);
            showMessage('Could not load the 3D viewer: ' + (err && err.message ? err.message : err), 'danger');
            console.error(err);
        });
    }

    function setViewMode(is3d) {
        mode3d = is3d;
        el.view2d.classList.toggle('active', !is3d);
        el.view3d.classList.toggle('active', is3d);
        el.canvas.classList.toggle('d-none', is3d);
        el.layout3d.classList.toggle('d-none', !is3d);
        el.zoomGroup.classList.toggle('d-none', is3d);

        var row = el.layout3d.closest('.row');
        var cols = row ? row.children : [];
        if (cols.length === 3) {
            cols[0].classList.toggle('d-none', is3d);
            cols[2].classList.toggle('d-none', is3d);
            cols[1].classList.toggle('col-lg-7', !is3d);
            cols[1].classList.toggle('col-lg-12', is3d);
            var foot = cols[1].querySelector('.card-footer');
            if (foot) foot.classList.toggle('d-none', is3d);
        }
        toggleMaxButton(is3d);

        if (is3d) {
            ensureViewer(sync3d);
        } else {
            if (document.fullscreenElement) document.exitFullscreen();
            if (viewer3d) { viewer3d.dispose(); viewer3d = null; }
            clearWarnings = [];
            renderWarnings();
        }
    }

    var btnMax = null;

    function toggleMaxButton(show) {
        if (!btnMax) {
            btnMax = document.createElement('button');
            btnMax.type = 'button';
            btnMax.className = 'btn btn-sm btn-dark';
            btnMax.textContent = '⛶ Maximize';
            btnMax.style.cssText = 'position:absolute;top:8px;right:8px;z-index:20';
            btnMax.addEventListener('click', function () {
                if (document.fullscreenElement) {
                    document.exitFullscreen();
                } else {
                    el.layout3d.requestFullscreen();
                }
            });
            el.layout3d.style.position = 'relative';
            el.layout3d.appendChild(btnMax);
            document.addEventListener('fullscreenchange', function () {
                var full = document.fullscreenElement === el.layout3d;
                el.layout3d.style.height = full ? '100vh' : '70vh';
                btnMax.textContent = full ? '✕ Exit' : '⛶ Maximize';
                window.dispatchEvent(new Event('resize'));
            });
        }
        btnMax.classList.toggle('d-none', !show);
        if (show && btnMax.parentNode !== el.layout3d) el.layout3d.appendChild(btnMax);
    }

    el.view2d.addEventListener('click', function () { if (mode3d) setViewMode(false); });
    el.view3d.addEventListener('click', function () { if (!mode3d) setViewMode(true); });

    /* ---------- start ---------- */

    if (layout) {
        layout.warnings = layout.warnings || [];
        layout.items = layout.items || [];
        layout.roads = layout.roads || [];
    }

    renderLayout();

    /* ---------- undo and redo ---------- */

    function restoreLayout(state) {
        layout = state;
        if (layout) {
            layout.warnings = layout.warnings || [];
            layout.items = layout.items || [];
            layout.roads = layout.roads || [];
            if (layout.id) {
                el.versionSelect.value = layout.id;
            } else {
                unsavedLayout = layout;
                el.versionSelect.value = '';
            }
        }
        if (selected && !findItem(selected.eq, selected.inst)) selected = null;
        renderLayout();
    }

    hist = typeof UndoRedo === 'function' ? UndoRedo({
        getState: function () { return layout; },
        setState: restoreLayout,
        onChange: updateButtons,
        isActive: function () { return !busy; },
        limit: 50
    }) : null;

    if (el.undo) el.undo.addEventListener('click', function () { if (hist && !busy) hist.undo(); });
    if (el.redo) el.redo.addEventListener('click', function () { if (hist && !busy) hist.redo(); });
    updateButtons();
})();