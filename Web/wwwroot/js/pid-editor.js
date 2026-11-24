(function () {
    'use strict';

    var root = document.getElementById('pidEditor');
    if (!root || typeof PidSymbols === 'undefined') return;

    var URLS = {
        data: root.getAttribute('data-data-url'),
        save: root.getAttribute('data-save-url'),
        status: root.getAttribute('data-status-url'),
        revision: root.getAttribute('data-revision-url')
    };
    var DIAGRAM_ID = root.getAttribute('data-diagram-id');
    var READ_ONLY = root.getAttribute('data-readonly') === 'true';
    var tokenInput = root.querySelector('input[name="__RequestVerificationToken"]');

    var DRAG_THRESHOLD_PX = 3;
    var GRID = 10;
    var TOL = 1;

    var EQ_PREFIX = { Tank: 'T', Pump: 'P', Blower: 'BL', Flare: 'FL', Digester: 'D', GasHolder: 'GH', Vessel: 'VS' };
    var EQ_NAME = { Tank: 'Tank', Pump: 'Pump', Blower: 'Blower', Flare: 'Flare', Digester: 'Digester', GasHolder: 'Gas holder', Vessel: 'Vessel' };
    var MEDIUM_CODE = { Slurry: 'SL', Biogas: 'BG', Cbg: 'CBG', Water: 'W', Digestate: 'DG', Steam: 'ST', Air: 'AIR', Condensate: 'CD', Other: 'OT' };
    var INSTRUMENT_DEFAULTS = {
        PT: { unit: 'mbar', min: 0, max: 50, service: 'Pressure' },
        TT: { unit: 'degC', min: 0, max: 80, service: 'Temperature' },
        FT: { unit: 'm3/h', min: 0, max: 100, service: 'Flow' },
        LT: { unit: '%', min: 0, max: 100, service: 'Level' }
    };

    function $(id) { return document.getElementById(id); }

    var el = {
        canvas: $('pidCanvas'),
        message: $('pidMessage'),
        save: $('btnSave'),
        undo: $('btnUndo'),
        redo: $('btnRedo'),
        zoomIn: $('btnZoomIn'),
        zoomOut: $('btnZoomOut'),
        zoomFit: $('btnZoomFit'),
        connect: $('btnConnect'),
        del: $('btnDelete'),
        title: $('diagramTitle'),
        props: $('pidProps'),
        legend: $('pidLegend'),
        statusSelect: $('statusSelect'),
        statusRemarks: $('statusRemarks'),
        setStatus: $('btnSetStatus'),
        newRevision: $('btnNewRevision')
    };

    var diagram = null;
    var selected = null;
    var svg = null;
    var baseView = null;
    var view = null;
    var drag = null;
    var pan = null;
    var hist = null;
    var savedSnapshot = '';
    var connectMode = false;
    var connectFrom = null;
    var rafPending = false;
    var busy = false;

    /* ---------- helpers ---------- */

    function esc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function r2(v) { return Math.round(v * 100) / 100; }

    function snap(v) { return Math.round(v / GRID) * GRID; }

    function uuid() {
        if (window.crypto && typeof window.crypto.randomUUID === 'function') return window.crypto.randomUUID();
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0;
            var v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
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
                return { ok: r.ok, status: r.status, data: data, text: t };
            });
        });
    }

    function post(url, body) {
        var headers = { 'Content-Type': 'application/json' };
        if (tokenInput) headers.RequestVerificationToken = tokenInput.value;
        return request(url, { method: 'POST', headers: headers, body: JSON.stringify(body || {}) });
    }

    function failText(res, fallback) {
        if (res && res.text && res.text.length < 300 && !res.data) return res.text;
        return fallback + ' (' + (res ? res.status : 'no response') + ').';
    }

    function findIn(list, id) {
        for (var i = 0; i < list.length; i++) {
            if (list[i].id === id) return list[i];
        }
        return null;
    }

    function normalize(d) {
        d.nodes = d.nodes || [];
        d.lines = d.lines || [];
        d.valves = d.valves || [];
        d.instruments = d.instruments || [];
        d.lines.forEach(function (l) { l.points = l.points || []; });
        return d;
    }

    function isDirty() {
        return !!diagram && JSON.stringify(diagram) !== savedSnapshot;
    }

    function commit() {
        if (hist) hist.commit();
        updateButtons();
    }

    function nextTag(prefix, tags) {
        var max = 100;
        tags.forEach(function (t) {
            var m = /^(.*)-(\d+)$/.exec(String(t || ''));
            if (m && m[1].toLowerCase() === prefix.toLowerCase()) {
                var n = parseInt(m[2], 10);
                if (n > max) max = n;
            }
        });
        return prefix + '-' + (max + 1);
    }

    function nextLineNo(medium, sizeMm) {
        var max = 100;
        diagram.lines.forEach(function (l) {
            var m = /-(\d+)$/.exec(String(l.lineNo || ''));
            if (m) {
                var n = parseInt(m[1], 10);
                if (n > max) max = n;
            }
        });
        return Math.round(sizeMm) + '-' + (MEDIUM_CODE[medium] || 'OT') + '-' + (max + 1);
    }

    function nodeTags() { return diagram.nodes.map(function (n) { return n.tag; }); }

    /* ---------- routing (same rules as the server auto layout) ---------- */

    function clean(pts) {
        var i;
        for (i = pts.length - 1; i > 0; i--) {
            if (pts[i].x === pts[i - 1].x && pts[i].y === pts[i - 1].y) pts.splice(i, 1);
        }
        for (i = pts.length - 2; i > 0; i--) {
            var p0 = pts[i - 1];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            if ((p0.x === p1.x && p1.x === p2.x) || (p0.y === p1.y && p1.y === p2.y)) pts.splice(i, 1);
        }
    }

    function routeAll() {
        var nodes = {};
        diagram.nodes.forEach(function (n) { nodes[n.id] = n; });

        var oldMid = {};
        diagram.lines.forEach(function (l) {
            oldMid[l.id] = l.points && l.points.length > 1 ? PidSymbols.pointAt(l.points, 0.5) : null;
        });

        var gapCount = {};

        diagram.lines.forEach(function (line) {
            var a = nodes[line.fromNodeId];
            var b = nodes[line.toNodeId];
            if (!a || !b) { line.points = []; return; }

            var sa = PidSymbols.size(a.symbolType);
            var sb = PidSymbols.size(b.symbolType);
            var pts = [];
            var dx = b.x - a.x;
            var sx;
            var ex;

            if (dx > TOL) {
                sx = a.x + sa.w / 2;
                ex = b.x - sb.w / 2;

                if (Math.abs(a.y - b.y) <= TOL) {
                    pts.push({ x: sx, y: a.y }, { x: ex, y: a.y });
                } else {
                    var k = gapCount[a.stageNo] || 0;
                    gapCount[a.stageNo] = k + 1;
                    var mid = sx + (ex - sx) / 2 + ((k % 5) - 2) * 12;
                    if (mid <= sx + 5 || mid >= ex - 5) mid = sx + (ex - sx) / 2;
                    pts.push({ x: sx, y: a.y }, { x: mid, y: a.y }, { x: mid, y: b.y }, { x: ex, y: b.y });
                }
            } else if (dx >= -TOL) {
                var down = b.y >= a.y;
                pts.push(
                    { x: a.x, y: down ? a.y + sa.h / 2 : a.y - sa.h / 2 },
                    { x: a.x, y: down ? b.y - sb.h / 2 : b.y + sb.h / 2 });
            } else {
                sx = a.x + sa.w / 2;
                ex = b.x - sb.w / 2;
                var yMid = Math.max(a.y + sa.h / 2, b.y + sb.h / 2) + 60;
                pts.push(
                    { x: sx, y: a.y }, { x: sx + 25, y: a.y }, { x: sx + 25, y: yMid },
                    { x: ex - 25, y: yMid }, { x: ex - 25, y: b.y }, { x: ex, y: b.y });
            }

            clean(pts);
            line.points = pts.map(function (p) { return { x: r2(p.x), y: r2(p.y) }; });
        });

        diagram.instruments.forEach(function (ins) {
            if (!ins.pidLineId) return;
            var line = findIn(diagram.lines, ins.pidLineId);
            var before = oldMid[ins.pidLineId];
            if (!line || !before || line.points.length < 2) return;
            var after = PidSymbols.pointAt(line.points, 0.5);
            ins.x = r2(ins.x + (after.x - before.x));
            ins.y = r2(ins.y + (after.y - before.y));
        });

        placeValves();
    }

    function placeValves() {
        diagram.valves.forEach(function (v) {
            var line = findIn(diagram.lines, v.pidLineId);
            if (!line || line.points.length < 2) return;
            var p = PidSymbols.pointAt(line.points, v.positionOnLine);
            v.x = r2(p.x);
            v.y = r2(p.y);
            v.rotationDeg = p.horizontal ? 0 : 90;
        });
    }

    function nearestFraction(points, x, y) {
        var total = 0;
        var i;
        for (i = 1; i < points.length; i++) {
            total += Math.abs(points[i].x - points[i - 1].x) + Math.abs(points[i].y - points[i - 1].y);
        }
        if (total <= 0) return 0.5;

        var best = { d: Infinity, at: 0 };
        var run = 0;

        for (i = 1; i < points.length; i++) {
            var ax = points[i - 1].x;
            var ay = points[i - 1].y;
            var bx = points[i].x;
            var by = points[i].y;
            var seg = Math.abs(bx - ax) + Math.abs(by - ay);
            if (seg > 0) {
                var vx = bx - ax;
                var vy = by - ay;
                var t = ((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy);
                t = Math.max(0, Math.min(1, t));
                var cx = ax + vx * t;
                var cy = ay + vy * t;
                var d = Math.sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d < best.d) best = { d: d, at: run + seg * t };
            }
            run += seg;
        }

        return Math.max(0.05, Math.min(0.95, best.at / total));
    }

    /* ---------- view (zoom and pan) ---------- */

    function contentBounds() {
        var minX = Infinity;
        var minY = Infinity;
        var maxX = -Infinity;
        var maxY = -Infinity;

        function grow(x1, y1, x2, y2) {
            if (x1 < minX) minX = x1;
            if (y1 < minY) minY = y1;
            if (x2 > maxX) maxX = x2;
            if (y2 > maxY) maxY = y2;
        }

        diagram.nodes.forEach(function (n) {
            var s = PidSymbols.size(n.symbolType);
            grow(n.x - s.w / 2, n.y - s.h / 2, n.x + s.w / 2, n.y + s.h / 2 + 32);
        });
        diagram.valves.forEach(function (v) { grow(v.x - 20, v.y - 20, v.x + 20, v.y + 36); });
        diagram.instruments.forEach(function (i) { grow(i.x - 20, i.y - 20, i.x + 20, i.y + 32); });
        diagram.lines.forEach(function (l) {
            l.points.forEach(function (p) { grow(p.x, p.y, p.x, p.y); });
        });

        if (!isFinite(minX)) return { x: 0, y: 0, w: 900, h: 600 };

        var pad = 80;
        return { x: minX - pad, y: minY - pad, w: (maxX - minX) + pad * 2, h: (maxY - minY) + pad * 2 };
    }

    function applyView() {
        if (!svg || !view) return;
        svg.setAttribute('viewBox', view.x + ' ' + view.y + ' ' + view.w + ' ' + view.h);
    }

    function fitView() {
        if (!diagram) return;
        baseView = contentBounds();
        view = { x: baseView.x, y: baseView.y, w: baseView.w, h: baseView.h };
        applyView();
    }

    function zoomAt(px, py, factor) {
        if (!view || !baseView) return;
        var w = view.w * factor;
        if (w < baseView.w * 0.05 || w > baseView.w * 6) return;
        view = {
            x: px - (px - view.x) * factor,
            y: py - (py - view.y) * factor,
            w: w,
            h: view.h * factor
        };
        applyView();
    }

    function zoomCenter(factor) {
        if (!view) return;
        zoomAt(view.x + view.w / 2, view.y + view.h / 2, factor);
    }

    function toSvg(e) {
        var pt = svg.createSVGPoint();
        pt.x = e.clientX;
        pt.y = e.clientY;
        var m = svg.getScreenCTM();
        return m ? pt.matrixTransform(m.inverse()) : { x: 0, y: 0 };
    }

    /* ---------- render ---------- */

    function selectionOverlay() {
        if (!selected) return '';
        var obj = selectedObject();
        if (!obj) return '';

        var stroke = 'stroke="#0d6efd" fill="none" stroke-width="1.5" stroke-dasharray="5 3" pointer-events="none"';

        if (selected.kind === 'node') {
            var s = PidSymbols.size(obj.symbolType);
            return '<rect x="' + r2(obj.x - s.w / 2 - 6) + '" y="' + r2(obj.y - s.h / 2 - 6) + '" width="' + (s.w + 12) +
                '" height="' + (s.h + 12) + '" rx="4" ' + stroke + '/>';
        }
        if (selected.kind === 'valve') {
            return '<circle cx="' + r2(obj.x) + '" cy="' + r2(obj.y) + '" r="17" ' + stroke + '/>';
        }
        if (selected.kind === 'instrument') {
            return '<circle cx="' + r2(obj.x) + '" cy="' + r2(obj.y) + '" r="19" ' + stroke + '/>';
        }
        if (selected.kind === 'line') {
            var path = obj.points.map(function (p) { return p.x + ',' + p.y; }).join(' ');
            return '<polyline points="' + path + '" fill="none" stroke="#0d6efd" stroke-opacity="0.35" stroke-width="9" ' +
                'stroke-linejoin="round" pointer-events="none"/>';
        }
        return '';
    }

    function renderCanvas() {
        if (!diagram) return;

        var parts = [];
        diagram.lines.forEach(function (l) { parts.push(PidSymbols.lineSvg(l)); });
        diagram.nodes.forEach(function (n) { parts.push(PidSymbols.nodeSvg(n)); });
        diagram.valves.forEach(function (v) { parts.push(PidSymbols.valveSvg(v)); });
        diagram.instruments.forEach(function (i) { parts.push(PidSymbols.instrumentSvg(i)); });
        parts.push(selectionOverlay());

        el.canvas.innerHTML =
            '<svg xmlns="http://www.w3.org/2000/svg" style="width:100%;height:100%;display:block;color:var(--bs-body-color,#212529);touch-action:none">' +
            parts.join('') + '</svg>';

        svg = el.canvas.querySelector('svg');
        if (!view) fitView();
        applyView();
    }

    function scheduleRender() {
        if (rafPending) return;
        rafPending = true;
        window.requestAnimationFrame(function () {
            rafPending = false;
            renderCanvas();
        });
    }

    function statusClass(s) {
        if (s === 'Approved') return 'text-bg-success';
        if (s === 'InReview') return 'text-bg-warning';
        if (s === 'Rejected') return 'text-bg-danger';
        if (s === 'Superseded') return 'text-bg-dark';
        return 'text-bg-secondary';
    }

    function renderHeader() {
        el.title.value = diagram.title || '';
        el.title.disabled = READ_ONLY;
        if (el.statusSelect) el.statusSelect.value = diagram.status;

        var badge = $('statusBadge');
        if (badge) {
            badge.className = 'badge ' + statusClass(diagram.status);
            badge.textContent = diagram.status;
        }
        var rev = $('revisionText');
        if (rev) rev.textContent = 'Rev ' + diagram.revisionNo;
        var no = $('drawingNoText');
        if (no) no.textContent = diagram.drawingNo;
    }

    function updateButtons() {
        var dirty = isDirty();
        el.save.disabled = busy || READ_ONLY || !dirty;
        el.del.disabled = busy || READ_ONLY || !selected;
        el.connect.disabled = busy || READ_ONLY;
        el.connect.classList.toggle('active', connectMode);
        if (el.setStatus) el.setStatus.disabled = busy;
        if (el.newRevision) {
            var st = diagram ? diagram.status : '';
            el.newRevision.disabled = busy || !(st === 'Approved' || st === 'Rejected' || st === 'Superseded');
        }
        if (el.undo) el.undo.disabled = busy || READ_ONLY || !(hist && hist.canUndo());
        if (el.redo) el.redo.disabled = busy || READ_ONLY || !(hist && hist.canRedo());
    }

    function renderAll() {
        renderHeader();
        renderCanvas();
        renderProps();
        updateButtons();
    }

    /* ---------- selection and properties ---------- */

    function selectedObject() {
        if (!selected || !diagram) return null;
        var list = selected.kind === 'node' ? diagram.nodes
            : selected.kind === 'line' ? diagram.lines
                : selected.kind === 'valve' ? diagram.valves
                    : diagram.instruments;
        return findIn(list, selected.id);
    }

    function select(kind, id) {
        selected = kind ? { kind: kind, id: id } : null;
        renderCanvas();
        renderProps();
        updateButtons();
    }

    function dis() { return READ_ONLY ? ' disabled' : ''; }

    function fText(label, field, value) {
        return '<div class="mb-2"><label class="form-label small text-muted mb-1">' + esc(label) + '</label>' +
            '<input class="form-control form-control-sm" data-field="' + field + '" value="' + esc(value) + '"' + dis() + ' /></div>';
    }

    function fNum(label, field, value, attrs, nullable) {
        return '<div class="mb-2"><label class="form-label small text-muted mb-1">' + esc(label) + '</label>' +
            '<input type="number" class="form-control form-control-sm" data-field="' + field + '" data-type="number"' +
            (nullable ? ' data-nullable="1"' : '') + ' ' + (attrs || '') +
            ' value="' + (value == null ? '' : esc(value)) + '"' + dis() + ' /></div>';
    }

    function fSelect(label, field, options, value) {
        return '<div class="mb-2"><label class="form-label small text-muted mb-1">' + esc(label) + '</label>' +
            '<select class="form-select form-select-sm" data-field="' + field + '"' + dis() + '>' +
            options.map(function (o) {
                return '<option value="' + esc(o) + '"' + (o === value ? ' selected' : '') + '>' + esc(o) + '</option>';
            }).join('') + '</select></div>';
    }

    function fCheck(label, field, checked) {
        return '<div class="form-check mb-2"><input type="checkbox" class="form-check-input" id="f_' + field + '" data-field="' + field +
            '" data-type="bool"' + (checked ? ' checked' : '') + dis() + ' />' +
            '<label class="form-check-label small" for="f_' + field + '">' + esc(label) + '</label></div>';
    }

    function renderProps() {
        var obj = selectedObject();

        if (!obj) {
            el.props.innerHTML = '<div class="small text-muted p-1">Click an item on the drawing to edit it.</div>';
            return;
        }

        var html = '';

        if (selected.kind === 'node') {
            html += '<div class="fw-semibold mb-2">Equipment ' + esc(obj.tag) + '</div>';
            html += fText('Tag', 'tag', obj.tag);
            html += fText('Name', 'name', obj.name);
            html += fSelect('Symbol', 'symbolType', PidSymbols.symbolTypes, obj.symbolType);
            html += fCheck('Locked in place', 'isLocked', obj.isLocked);
        } else if (selected.kind === 'line') {
            var from = findIn(diagram.nodes, obj.fromNodeId);
            var to = findIn(diagram.nodes, obj.toNodeId);
            html += '<div class="fw-semibold mb-1">Line ' + esc(obj.lineNo) + '</div>';
            html += '<div class="small text-muted mb-2">' + esc(from ? from.tag : '?') + ' &rarr; ' + esc(to ? to.tag : '?') + '</div>';
            html += fText('Line no', 'lineNo', obj.lineNo);
            html += fSelect('Medium', 'medium', Object.keys(PidSymbols.mediumColors), obj.medium);
            html += fNum('Size DN (mm)', 'nominalSizeMm', obj.nominalSizeMm, 'min="10" step="5"', false);
            html += fText('Material', 'material', obj.material || '');
            html += fCheck('Insulated', 'isInsulated', obj.isInsulated);
            html += fText('Notes', 'notes', obj.notes || '');
        } else if (selected.kind === 'valve') {
            html += '<div class="fw-semibold mb-2">Valve ' + esc(obj.tag) + '</div>';
            html += fText('Tag', 'tag', obj.tag);
            html += fSelect('Type', 'valveType', PidSymbols.valveTypes, obj.valveType);
            html += fNum('Size DN (mm)', 'nominalSizeMm', obj.nominalSizeMm, 'min="10" step="5"', false);
            html += fNum('Position on line (0.05 to 0.95)', 'positionOnLine', obj.positionOnLine, 'min="0.05" max="0.95" step="0.05"', false);
            html += fCheck('Normally open', 'isNormallyOpen', obj.isNormallyOpen);
            html += fText('Notes', 'notes', obj.notes || '');
        } else {
            html += '<div class="fw-semibold mb-2">Instrument ' + esc(obj.tag) + '</div>';
            html += fText('Tag', 'tag', obj.tag);
            html += fSelect('Type', 'instrumentType', PidSymbols.instrumentTypes, obj.instrumentType);
            html += fText('Service', 'service', obj.service || '');
            html += fNum('Range min', 'rangeMin', obj.rangeMin, 'step="any"', true);
            html += fNum('Range max', 'rangeMax', obj.rangeMax, 'step="any"', true);
            html += fText('Unit', 'unit', obj.unit || '');
            html += fText('Notes', 'notes', obj.notes || '');
        }

        el.props.innerHTML = html;
    }

    function applyField(target, isChange) {
        if (READ_ONLY) return;
        var field = target.getAttribute('data-field');
        var obj = selectedObject();
        if (!field || !obj) return;

        var type = target.getAttribute('data-type');
        var value;

        if (type === 'bool') {
            value = target.checked;
        } else if (type === 'number') {
            var n = parseFloat(target.value);
            if (!isFinite(n)) {
                if (target.getAttribute('data-nullable') === '1') value = null;
                else return;
            } else {
                value = n;
            }
        } else {
            value = target.value;
        }

        obj[field] = value;

        if (field === 'positionOnLine') placeValves();
        if (field === 'symbolType') routeAll();

        renderCanvas();
        if (isChange) {
            if (field === 'symbolType') renderProps();
            commit();
        }
    }

    el.props.addEventListener('input', function (e) {
        if (e.target.getAttribute('data-type') === 'bool' || e.target.tagName === 'SELECT') return;
        applyField(e.target, false);
    });

    el.props.addEventListener('change', function (e) {
        applyField(e.target, true);
    });

    el.title.addEventListener('input', function () {
        if (READ_ONLY || !diagram) return;
        diagram.title = el.title.value;
        updateButtons();
    });

    el.title.addEventListener('change', function () {
        if (READ_ONLY || !diagram) return;
        diagram.title = el.title.value;
        commit();
    });

    /* ---------- add and delete ---------- */

    function viewCentre() {
        if (!view) return { x: 400, y: 300 };
        return { x: snap(view.x + view.w / 2), y: snap(view.y + view.h / 2) };
    }

    function addEquipment(type) {
        var c = viewCentre();
        var node = {
            id: uuid(),
            equipmentItemId: null,
            sizedEquipmentId: null,
            tag: nextTag(EQ_PREFIX[type] || 'X', nodeTags()),
            name: EQ_NAME[type] || type,
            symbolType: type,
            stageNo: 0,
            instanceNo: 1,
            x: c.x,
            y: c.y,
            rotationDeg: 0,
            isLocked: false
        };
        diagram.nodes.push(node);
        selected = { kind: 'node', id: node.id };
        renderCanvas();
        renderProps();
        commit();
        showMessage('Equipment added. Drag it into place.', 'info');
    }

    function addValve(type) {
        var obj = selectedObject();
        if (!selected || selected.kind !== 'line' || !obj) {
            showMessage('Select a line first, then click the valve symbol.', 'warning');
            return;
        }
        var valve = {
            id: uuid(),
            pidLineId: obj.id,
            tag: nextTag('V', diagram.valves.map(function (v) { return v.tag; })),
            valveType: type,
            nominalSizeMm: obj.nominalSizeMm,
            positionOnLine: 0.5,
            x: 0,
            y: 0,
            rotationDeg: 0,
            isNormallyOpen: true,
            notes: null
        };
        diagram.valves.push(valve);
        placeValves();
        selected = { kind: 'valve', id: valve.id };
        renderCanvas();
        renderProps();
        commit();
    }

    function addInstrument(type) {
        var obj = selectedObject();
        if (!selected || (selected.kind !== 'node' && selected.kind !== 'line') || !obj) {
            showMessage('Select equipment or a line first, then click the instrument symbol.', 'warning');
            return;
        }

        var d = INSTRUMENT_DEFAULTS[type] || { unit: '', min: 0, max: 100, service: '' };
        var ins = {
            id: uuid(),
            pidLineId: null,
            pidEquipmentNodeId: null,
            tag: nextTag(type, diagram.instruments.map(function (i) { return i.tag; })),
            instrumentType: type,
            service: d.service,
            rangeMin: d.min,
            rangeMax: d.max,
            unit: d.unit,
            x: 0,
            y: 0,
            notes: null
        };

        if (selected.kind === 'node') {
            var s = PidSymbols.size(obj.symbolType);
            var count = diagram.instruments.filter(function (i) { return i.pidEquipmentNodeId === obj.id; }).length;
            ins.pidEquipmentNodeId = obj.id;
            ins.x = r2(obj.x + count * 40 - 20);
            ins.y = r2(obj.y - s.h / 2 - 45);
        } else {
            var p = PidSymbols.pointAt(obj.points, 0.5);
            ins.pidLineId = obj.id;
            ins.x = r2(p.horizontal ? p.x : p.x + 35);
            ins.y = r2(p.horizontal ? p.y - 35 : p.y);
        }

        diagram.instruments.push(ins);
        selected = { kind: 'instrument', id: ins.id };
        renderCanvas();
        renderProps();
        commit();
    }

    function connectNodes(fromId, toId) {
        var line = {
            id: uuid(),
            lineNo: nextLineNo('Slurry', 100),
            fromNodeId: fromId,
            toNodeId: toId,
            medium: 'Slurry',
            nominalSizeMm: 100,
            material: 'CS',
            isInsulated: false,
            points: [],
            notes: null
        };
        diagram.lines.push(line);
        routeAll();
        selected = { kind: 'line', id: line.id };
        renderCanvas();
        renderProps();
        commit();
        showMessage('Line added. Set its medium and size on the right.', 'info');
    }

    function removeLines(lineIds) {
        diagram.valves = diagram.valves.filter(function (v) { return lineIds.indexOf(v.pidLineId) < 0; });
        diagram.instruments = diagram.instruments.filter(function (i) { return !i.pidLineId || lineIds.indexOf(i.pidLineId) < 0; });
        diagram.lines = diagram.lines.filter(function (l) { return lineIds.indexOf(l.id) < 0; });
    }

    function deleteSelected() {
        if (READ_ONLY || !selected) return;
        var obj = selectedObject();
        if (!obj) return;

        if (selected.kind === 'node') {
            var attached = diagram.lines.filter(function (l) {
                return l.fromNodeId === obj.id || l.toNodeId === obj.id;
            }).map(function (l) { return l.id; });

            if (attached.length && !window.confirm('Delete ' + obj.tag + ' and its ' + attached.length + ' connected line(s)?')) return;

            removeLines(attached);
            diagram.instruments = diagram.instruments.filter(function (i) { return i.pidEquipmentNodeId !== obj.id; });
            diagram.nodes = diagram.nodes.filter(function (n) { return n.id !== obj.id; });
        } else if (selected.kind === 'line') {
            removeLines([obj.id]);
        } else if (selected.kind === 'valve') {
            diagram.valves = diagram.valves.filter(function (v) { return v.id !== obj.id; });
        } else {
            diagram.instruments = diagram.instruments.filter(function (i) { return i.id !== obj.id; });
        }

        selected = null;
        renderCanvas();
        renderProps();
        commit();
    }

    function setConnectMode(on) {
        connectMode = on;
        connectFrom = null;
        el.connect.classList.toggle('active', on);
        if (on) showMessage('Click the source equipment, then the target equipment.', 'info');
        else clearMessage();
    }

    el.connect.addEventListener('click', function () {
        if (READ_ONLY) return;
        setConnectMode(!connectMode);
    });

    el.del.addEventListener('click', deleteSelected);

    /* ---------- legend (click to add) ---------- */

    function renderLegend() {
        var items = PidSymbols.legend();
        var groups = { equipment: 'Equipment', valve: 'Valves', instrument: 'Instruments' };
        var html = '';

        Object.keys(groups).forEach(function (kind) {
            html += '<div class="small text-muted mt-2 mb-1">' + groups[kind] + '</div><div class="d-flex flex-wrap gap-1">';
            items.filter(function (i) { return i.kind === kind; }).forEach(function (i) {
                html += '<button type="button" class="btn btn-outline-secondary btn-sm p-1 pid-legend-item" data-kind="' + i.kind +
                    '" data-type="' + i.type + '" title="' + esc(i.label) + '"' + dis() + '>' +
                    PidSymbols.previewSvg(i.kind, i.type, 44) + '</button>';
            });
            html += '</div>';
        });

        var media = Object.keys(PidSymbols.mediumColors).map(function (m) {
            return '<div class="d-flex align-items-center gap-2 small"><span style="display:inline-block;width:22px;height:3px;background:' +
                PidSymbols.mediumColors[m] + '"></span>' + esc(m) + '</div>';
        }).join('');

        html += '<div class="small text-muted mt-3 mb-1">Line media</div>' + media;
        el.legend.innerHTML = html;
    }

    el.legend.addEventListener('click', function (e) {
        if (READ_ONLY) return;
        var btn = e.target.closest('.pid-legend-item');
        if (!btn) return;
        var kind = btn.getAttribute('data-kind');
        var type = btn.getAttribute('data-type');
        clearMessage();
        if (kind === 'equipment') addEquipment(type);
        else if (kind === 'valve') addValve(type);
        else addInstrument(type);
    });

    /* ---------- canvas pointer handling ---------- */

    function targetOf(e) {
        var t = e.target.closest ? e.target.closest('.pid-node, .pid-valve, .pid-instrument, .pid-line') : null;
        if (!t) return null;
        var kind = t.classList.contains('pid-node') ? 'node'
            : t.classList.contains('pid-valve') ? 'valve'
                : t.classList.contains('pid-instrument') ? 'instrument' : 'line';
        return { kind: kind, id: t.getAttribute('data-id') };
    }

    el.canvas.addEventListener('wheel', function (e) {
        if (!svg) return;
        e.preventDefault();
        var p = toSvg(e);
        zoomAt(p.x, p.y, e.deltaY < 0 ? 0.85 : 1 / 0.85);
    }, { passive: false });

    el.canvas.addEventListener('pointerdown', function (e) {
        if (!svg || busy || e.button !== 0) return;

        var t = targetOf(e);

        if (connectMode && !READ_ONLY) {
            if (t && t.kind === 'node') {
                if (!connectFrom) {
                    connectFrom = t.id;
                    select('node', t.id);
                    showMessage('Now click the target equipment.', 'info');
                } else if (connectFrom !== t.id) {
                    var from = connectFrom;
                    setConnectMode(false);
                    connectNodes(from, t.id);
                }
            }
            e.preventDefault();
            return;
        }

        if (t) {
            select(t.kind, t.id);

            if (!READ_ONLY && t.kind !== 'line') {
                var obj = selectedObject();
                if (obj && t.kind === 'node' && obj.isLocked) {
                    showMessage('This equipment is locked. Untick Locked to move it.', 'warning');
                } else if (obj) {
                    var attached = {};
                    if (t.kind === 'node') {
                        diagram.instruments.forEach(function (i) {
                            if (i.pidEquipmentNodeId === obj.id) attached[i.id] = { x: i.x, y: i.y };
                        });
                    }
                    drag = {
                        kind: t.kind,
                        id: t.id,
                        start: toSvg(e),
                        clientX: e.clientX,
                        clientY: e.clientY,
                        orig: { x: obj.x, y: obj.y },
                        attached: attached,
                        moved: false
                    };
                }
            }
        } else {
            var m = svg.getScreenCTM();
            pan = {
                clientX: e.clientX,
                clientY: e.clientY,
                view: { x: view.x, y: view.y, w: view.w, h: view.h },
                scale: m ? m.a : 1,
                moved: false
            };
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
            drag.moved = true;

            var cur = toSvg(e);
            var dx = cur.x - drag.start.x;
            var dy = cur.y - drag.start.y;

            if (drag.kind === 'node') {
                var node = findIn(diagram.nodes, drag.id);
                if (!node) return;
                node.x = snap(drag.orig.x + dx);
                node.y = snap(drag.orig.y + dy);
                var appliedX = node.x - drag.orig.x;
                var appliedY = node.y - drag.orig.y;
                Object.keys(drag.attached).forEach(function (id) {
                    var ins = findIn(diagram.instruments, id);
                    if (ins) {
                        ins.x = r2(drag.attached[id].x + appliedX);
                        ins.y = r2(drag.attached[id].y + appliedY);
                    }
                });
                routeAll();
            } else if (drag.kind === 'valve') {
                var valve = findIn(diagram.valves, drag.id);
                var line = valve ? findIn(diagram.lines, valve.pidLineId) : null;
                if (!valve || !line) return;
                valve.positionOnLine = r2(nearestFraction(line.points, cur.x, cur.y));
                placeValves();
            } else if (drag.kind === 'instrument') {
                var inst = findIn(diagram.instruments, drag.id);
                if (!inst) return;
                inst.x = r2(drag.orig.x + dx);
                inst.y = r2(drag.orig.y + dy);
            }

            scheduleRender();
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
            if (d.moved) {
                renderCanvas();
                renderProps();
                commit();
            }
        } else if (pan) {
            var p = pan;
            pan = null;
            if (!p.moved) select(null);
        }
    }

    document.addEventListener('keydown', function (e) {
        var t = e.target;
        var tag = t && t.tagName ? t.tagName.toUpperCase() : '';
        var typing = tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || (t && t.isContentEditable);

        if (e.key === 'Escape') {
            if (connectMode) setConnectMode(false);
            else if (!typing) select(null);
            return;
        }
        if (typing || READ_ONLY) return;
        if (e.key === 'Delete' || e.key === 'Backspace') {
            e.preventDefault();
            deleteSelected();
        }
    });

    el.zoomIn.addEventListener('click', function () { zoomCenter(0.8); });
    el.zoomOut.addEventListener('click', function () { zoomCenter(1.25); });
    el.zoomFit.addEventListener('click', fitView);

    /* ---------- save, status, revision ---------- */

    function setBusy(b) {
        busy = b;
        root.classList.toggle('is-busy', b);
        updateButtons();
    }

    function adopt(dto) {
        diagram = normalize(dto);
        if (selected && !selectedObject()) selected = null;
        savedSnapshot = JSON.stringify(diagram);
        renderAll();
    }

    function save() {
        if (READ_ONLY || !diagram || !isDirty()) return;
        clearMessage();
        setBusy(true);
        showMessage('Saving...', 'info');

        post(URLS.save, diagram).then(function (res) {
            setBusy(false);
            if (!res.ok || !res.data) {
                showMessage(failText(res, 'Could not save'), 'danger');
                return;
            }
            adopt(res.data);
            if (hist) hist.reset();
            updateButtons();
            showMessage('Saved.', 'success');
        }).catch(function () {
            setBusy(false);
            showMessage('Could not reach the server. Try again.', 'danger');
        });
    }

    el.save.addEventListener('click', save);

    if (el.setStatus) {
        el.setStatus.addEventListener('click', function () {
            if (isDirty()) {
                showMessage('Save your changes before changing the status.', 'warning');
                return;
            }
            setBusy(true);
            post(URLS.status, {
                id: DIAGRAM_ID,
                status: el.statusSelect.value,
                remarks: el.statusRemarks.value.trim() || null
            }).then(function (res) {
                if (!res.ok) {
                    setBusy(false);
                    showMessage(failText(res, 'Could not change the status'), 'danger');
                    return;
                }
                window.location.reload();
            }).catch(function () {
                setBusy(false);
                showMessage('Could not reach the server. Try again.', 'danger');
            });
        });
    }

    if (el.newRevision) {
        el.newRevision.addEventListener('click', function () {
            if (isDirty()) {
                showMessage('Save your changes first.', 'warning');
                return;
            }
            if (!window.confirm('Start a new revision? The drawing becomes Draft and editable.')) return;
            setBusy(true);
            post(URLS.revision, {
                id: DIAGRAM_ID,
                remarks: el.statusRemarks.value.trim() || null
            }).then(function (res) {
                if (!res.ok) {
                    setBusy(false);
                    showMessage(failText(res, 'Could not start a new revision'), 'danger');
                    return;
                }
                window.location.reload();
            }).catch(function () {
                setBusy(false);
                showMessage('Could not reach the server. Try again.', 'danger');
            });
        });
    }

    window.addEventListener('beforeunload', function (e) {
        if (isDirty()) {
            e.preventDefault();
            e.returnValue = '';
        }
    });

    /* ---------- undo and redo ---------- */

    function restoreDiagram(state) {
        diagram = normalize(state);
        if (selected && !selectedObject()) selected = null;
        renderAll();
    }

    /* ---------- start ---------- */

    renderLegend();
    el.props.innerHTML = '<div class="small text-muted p-1">Loading...</div>';

    request(URLS.data + '?id=' + encodeURIComponent(DIAGRAM_ID), { method: 'GET' }).then(function (res) {
        if (!res.ok || !res.data) {
            showMessage(failText(res, 'Could not load the drawing'), 'danger');
            return;
        }

        diagram = normalize(res.data);
        savedSnapshot = JSON.stringify(diagram);
        fitView();
        renderAll();

        hist = typeof UndoRedo === 'function' ? UndoRedo({
            getState: function () { return diagram; },
            setState: restoreDiagram,
            onChange: updateButtons,
            isActive: function () { return !READ_ONLY && !busy; },
            limit: 100
        }) : null;

        if (el.undo) el.undo.addEventListener('click', function () { if (hist && !busy && !READ_ONLY) hist.undo(); });
        if (el.redo) el.redo.addEventListener('click', function () { if (hist && !busy && !READ_ONLY) hist.redo(); });
        updateButtons();
    }).catch(function () {
        showMessage('Could not reach the server. Try again.', 'danger');
    });
})();
