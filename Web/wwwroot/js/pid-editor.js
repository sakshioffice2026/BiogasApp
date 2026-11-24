(function () {
    'use strict';

    var root = document.getElementById('pidEditor');
    if (!root || !window.PidSymbols) return;

    var PS = window.PidSymbols;
    var NS = 'http://www.w3.org/2000/svg';
    var GRID = 10;
    var TOL = 1;

    var URLS = {
        data: root.getAttribute('data-data-url'),
        save: root.getAttribute('data-save-url'),
        status: root.getAttribute('data-status-url'),
        revision: root.getAttribute('data-revision-url')
    };
    var DIAGRAM_ID = root.getAttribute('data-diagram-id');
    var READONLY = root.getAttribute('data-readonly') === 'true';
    var tokenInput = root.querySelector('input[name="__RequestVerificationToken"]');

    function $(id) { return document.getElementById(id); }

    var el = {
        canvas: $('pidCanvas'),
        message: $('pidMessage'),
        props: $('pidProps'),
        legend: $('pidLegend'),
        dirty: $('pidDirty'),
        save: $('btnSave'),
        addValve: $('btnAddValve'),
        addInstrument: $('btnAddInstrument'),
        deleteItem: $('btnDeleteItem'),
        lockNode: $('btnLockNode'),
        rotate: $('btnRotate'),
        zoomIn: $('btnZoomIn'),
        zoomOut: $('btnZoomOut'),
        fit: $('btnFit'),
        valveType: $('selValveType'),
        instrumentType: $('selInstrumentType'),
        submitReview: $('btnSubmitReview'),
        approve: $('btnApprove'),
        reject: $('btnReject'),
        newRevision: $('btnNewRevision')
    };

    var diagram = null;
    var selected = null;
    var dirty = false;
    var saving = false;
    var nodeMap = {};
    var lineMap = {};
    var view = { x: 0, y: 0, w: 1200, h: 800 };
    var svg = null;
    var scene = null;
    var drag = null;
    var renderQueued = false;
    var messageTimer = null;

    /* ---------- helpers ---------- */

    function esc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;')
            .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function num(v, d) {
        var n = Number(v);
        return isNaN(n) ? (d || 0) : n;
    }

    function r2(v) { return Math.round(v * 100) / 100; }
    function r3(v) { return Math.round(v * 1000) / 1000; }
    function snap(v) { return Math.round(v / GRID) * GRID; }

    function uid() {
        if (window.crypto && window.crypto.randomUUID) return window.crypto.randomUUID();
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0;
            var v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    function en(v, map) {
        return typeof v === 'number' ? (map[v] || v) : v;
    }

    function setDisabled(elm, flag) {
        if (elm) elm.disabled = !!flag;
    }

    function msg(text, type) {
        if (!el.message) return;
        el.message.className = 'alert alert-' + (type || 'info') + ' py-2 mb-2';
        el.message.textContent = text;
        if (messageTimer) clearTimeout(messageTimer);
        if (type === 'success') {
            messageTimer = setTimeout(function () {
                el.message.className = 'd-none';
                el.message.textContent = '';
            }, 3000);
        }
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

    function markDirty() {
        dirty = true;
        if (el.dirty) el.dirty.textContent = 'Unsaved changes';
        if (el.save) el.save.classList.add('btn-warning');
    }

    function clearDirty() {
        dirty = false;
        if (el.dirty) el.dirty.textContent = '';
        if (el.save) el.save.classList.remove('btn-warning');
    }

    /* ---------- data ---------- */

    function normalize(d) {
        d.status = en(d.status, PS.statusNames);
        d.nodes = (d.nodes || []).map(function (n) {
            n.symbolType = en(n.symbolType, PS.symbolNames);
            n.x = num(n.x); n.y = num(n.y);
            n.rotationDeg = num(n.rotationDeg);
            n.isLocked = !!n.isLocked;
            return n;
        });
        d.lines = (d.lines || []).map(function (l) {
            l.medium = en(l.medium, PS.mediumNames);
            l.nominalSizeMm = num(l.nominalSizeMm);
            l.isInsulated = !!l.isInsulated;
            l.points = (l.points || []).map(function (p) { return { x: num(p.x), y: num(p.y) }; });
            return l;
        });
        d.valves = (d.valves || []).map(function (v) {
            v.valveType = en(v.valveType, PS.valveNames);
            v.nominalSizeMm = num(v.nominalSizeMm);
            v.positionOnLine = num(v.positionOnLine, 0.5);
            v.x = num(v.x); v.y = num(v.y);
            v.rotationDeg = num(v.rotationDeg);
            v.isNormallyOpen = v.isNormallyOpen !== false;
            return v;
        });
        d.instruments = (d.instruments || []).map(function (i) {
            i.instrumentType = en(i.instrumentType, PS.instrumentNames);
            i.x = num(i.x); i.y = num(i.y);
            i.rangeMin = (i.rangeMin === null || i.rangeMin === undefined) ? null : num(i.rangeMin);
            i.rangeMax = (i.rangeMax === null || i.rangeMax === undefined) ? null : num(i.rangeMax);
            return i;
        });
        return d;
    }

    function reindex() {
        nodeMap = {};
        lineMap = {};
        diagram.nodes.forEach(function (n) { nodeMap[n.id] = n; });
        diagram.lines.forEach(function (l) { lineMap[l.id] = l; });
    }

    function getSelected() {
        if (!selected || !diagram) return null;
        var list = {
            node: diagram.nodes,
            line: diagram.lines,
            valve: diagram.valves,
            instrument: diagram.instruments
        }[selected.kind] || [];
        for (var i = 0; i < list.length; i++) {
            if (list[i].id === selected.id) return list[i];
        }
        return null;
    }

    function nextTag(prefix, existing) {
        var max = 100;
        existing.forEach(function (t) {
            if (!t) return;
            var s = String(t).trim();
            var i = s.lastIndexOf('-');
            if (i <= 0) return;
            if (s.substring(0, i).toLowerCase() !== prefix.toLowerCase()) return;
            var n = parseInt(s.substring(i + 1), 10);
            if (!isNaN(n) && n > max) max = n;
        });
        return prefix + '-' + (max + 1);
    }

    /* ---------- geometry ---------- */

    function segLen(a, b) {
        return Math.abs(b.x - a.x) + Math.abs(b.y - a.y);
    }

    function pointAt(pts, f) {
        if (!pts || !pts.length) return { x: 0, y: 0, h: true };
        if (pts.length === 1) return { x: pts[0].x, y: pts[0].y, h: true };
        var total = 0, i;
        for (i = 1; i < pts.length; i++) total += segLen(pts[i - 1], pts[i]);
        var target = total * Math.min(1, Math.max(0, f));
        var run = 0;
        for (i = 1; i < pts.length; i++) {
            var s = segLen(pts[i - 1], pts[i]);
            var h = Math.abs(pts[i].y - pts[i - 1].y) < 0.01;
            if (s > 0 && run + s >= target) {
                var t = (target - run) / s;
                return {
                    x: pts[i - 1].x + (pts[i].x - pts[i - 1].x) * t,
                    y: pts[i - 1].y + (pts[i].y - pts[i - 1].y) * t,
                    h: h
                };
            }
            run += s;
        }
        var last = pts[pts.length - 1];
        return { x: last.x, y: last.y, h: true };
    }

    function nearest(pts, px, py) {
        var total = 0, i;
        for (i = 1; i < pts.length; i++) total += segLen(pts[i - 1], pts[i]);
        if (pts.length < 2 || total <= 0) {
            return { x: pts.length ? pts[0].x : px, y: pts.length ? pts[0].y : py, f: 0.5 };
        }
        var best = 1e18, bestAt = 0, bx = pts[0].x, by = pts[0].y, run = 0;
        for (i = 1; i < pts.length; i++) {
            var a = pts[i - 1], b = pts[i];
            var dx = b.x - a.x, dy = b.y - a.y;
            var l2 = dx * dx + dy * dy;
            var t = l2 > 0 ? ((px - a.x) * dx + (py - a.y) * dy) / l2 : 0;
            t = Math.max(0, Math.min(1, t));
            var qx = a.x + dx * t, qy = a.y + dy * t;
            var d = (px - qx) * (px - qx) + (py - qy) * (py - qy);
            var s = segLen(a, b);
            if (d < best) { best = d; bestAt = run + s * t; bx = qx; by = qy; }
            run += s;
        }
        return { x: bx, y: by, f: Math.min(1, Math.max(0, bestAt / total)) };
    }

    function cleanRoute(pts) {
        var i;
        for (i = pts.length - 1; i > 0; i--) {
            if (pts[i][0] === pts[i - 1][0] && pts[i][1] === pts[i - 1][1]) pts.splice(i, 1);
        }
        for (i = pts.length - 2; i > 0; i--) {
            var p0 = pts[i - 1], p1 = pts[i], p2 = pts[i + 1];
            if ((p0[0] === p1[0] && p1[0] === p2[0]) || (p0[1] === p1[1] && p1[1] === p2[1])) pts.splice(i, 1);
        }
    }

    function computeRoute(line) {
        var a = nodeMap[line.fromNodeId];
        var b = nodeMap[line.toNodeId];
        if (!a || !b) return null;

        var sa = PS.size(a.symbolType), sb = PS.size(b.symbolType);
        var wa = sa[0], ha = sa[1], wb = sb[0], hb = sb[1];
        var dx = b.x - a.x;
        var pts = [];
        var sx = a.x + wa / 2, ex = b.x - wb / 2;

        if (dx > TOL && ex > sx + 10) {
            if (Math.abs(a.y - b.y) <= TOL) {
                pts.push([sx, a.y]);
                pts.push([ex, a.y]);
            } else {
                var mid = sx + (ex - sx) / 2;
                pts.push([sx, a.y]);
                pts.push([mid, a.y]);
                pts.push([mid, b.y]);
                pts.push([ex, b.y]);
            }
        } else if (Math.abs(dx) <= TOL) {
            var down = b.y >= a.y;
            pts.push([a.x, down ? a.y + ha / 2 : a.y - ha / 2]);
            pts.push([a.x, down ? b.y - hb / 2 : b.y + hb / 2]);
        } else {
            var yMid = Math.max(a.y + ha / 2, b.y + hb / 2) + 60;
            pts.push([sx, a.y]);
            pts.push([sx + 25, a.y]);
            pts.push([sx + 25, yMid]);
            pts.push([ex - 25, yMid]);
            pts.push([ex - 25, b.y]);
            pts.push([ex, b.y]);
        }

        cleanRoute(pts);
        return pts;
    }

    function placeValve(v) {
        var line = lineMap[v.pidLineId];
        if (!line || !line.points || line.points.length < 2) return;
        var p = pointAt(line.points, v.positionOnLine);
        v.x = r2(p.x);
        v.y = r2(p.y);
        v.rotationDeg = p.h ? 0 : 90;
    }

    function rerouteLine(line) {
        var oldMid = pointAt(line.points, 0.5);
        var pts = computeRoute(line);
        if (!pts) return;
        line.points = pts.map(function (p) { return { x: r2(p[0]), y: r2(p[1]) }; });
        var newMid = pointAt(line.points, 0.5);

        diagram.valves.forEach(function (v) {
            if (v.pidLineId === line.id) placeValve(v);
        });
        diagram.instruments.forEach(function (i) {
            if (i.pidLineId === line.id && !i.pidEquipmentNodeId) {
                i.x = r2(i.x + newMid.x - oldMid.x);
                i.y = r2(i.y + newMid.y - oldMid.y);
            }
        });
    }

    function rerouteNodeLines(nodeId) {
        diagram.lines.forEach(function (l) {
            if (l.fromNodeId === nodeId || l.toNodeId === nodeId) rerouteLine(l);
        });
    }

    /* ---------- rendering ---------- */

    function arrow(pts, color) {
        var n = pts.length;
        if (n < 2) return '';
        var a = pts[n - 2], b = pts[n - 1];
        var dx = b.x - a.x, dy = b.y - a.y;
        var len = Math.sqrt(dx * dx + dy * dy);
        if (!len) return '';
        var ux = dx / len, uy = dy / len, px = -uy, py = ux;
        var bx = b.x - ux * 9, by = b.y - uy * 9;
        return '<polygon points="' + b.x + ',' + b.y + ' ' +
            (bx + px * 4) + ',' + (by + py * 4) + ' ' +
            (bx - px * 4) + ',' + (by - py * 4) +
            '" fill="' + color + '" pointer-events="none"/>';
    }

    function lineLabel(l) {
        var pts = l.points;
        var best = 0, bi = 1, i;
        for (i = 1; i < pts.length; i++) {
            var len = segLen(pts[i - 1], pts[i]);
            if (len > best) { best = len; bi = i; }
        }
        var a = pts[bi - 1], b = pts[bi];
        var mx = (a.x + b.x) / 2, my = (a.y + b.y) / 2;
        var horiz = Math.abs(a.y - b.y) < 0.01;
        var txt = esc(l.lineNo);
        if (horiz) {
            return '<text x="' + mx + '" y="' + (my - 7) + '" text-anchor="middle" font-size="9" font-family="Arial, sans-serif" fill="#343a40" pointer-events="none">' + txt + '</text>';
        }
        return '<text x="' + (mx + 7) + '" y="' + my + '" font-size="9" font-family="Arial, sans-serif" fill="#343a40" pointer-events="none">' + txt + '</text>';
    }

    function isSel(kind, id) {
        return selected && selected.kind === kind && selected.id === id;
    }

    function render() {
        if (!diagram || !scene) return;
        var h = [];

        diagram.lines.forEach(function (l) {
            var pts = l.points || [];
            if (pts.length < 2) return;
            var st = PS.mediumStyle(l.medium);
            var d = pts.map(function (p) { return p.x + ',' + p.y; }).join(' ');
            var sel = isSel('line', l.id);

            h.push('<g data-kind="line" data-id="' + l.id + '">');
            if (l.isInsulated) {
                h.push('<polyline points="' + d + '" fill="none" stroke="' + st.color + '" stroke-opacity="0.25" stroke-width="8" stroke-linejoin="round"/>');
            }
            h.push('<polyline points="' + d + '" fill="none" stroke="transparent" stroke-width="14" style="cursor:pointer"/>');
            h.push('<polyline points="' + d + '" fill="none" stroke="' + st.color + '" stroke-width="' + (sel ? 3.8 : 2.2) + '"' +
                (st.dash ? ' stroke-dasharray="' + st.dash + '"' : '') + ' stroke-linejoin="round" pointer-events="none"/>');
            h.push(arrow(pts, st.color));
            h.push(lineLabel(l));
            h.push('</g>');
        });

        diagram.nodes.forEach(function (n) {
            var sz = PS.size(n.symbolType);
            var sel = isSel('node', n.id);
            var cur = (READONLY || n.isLocked) ? 'pointer' : 'move';
            h.push('<g data-kind="node" data-id="' + n.id + '" transform="translate(' + n.x + ',' + n.y + ')" style="cursor:' + cur + '">');
            h.push('<g transform="rotate(' + (n.rotationDeg || 0) + ')">' + PS.nodeMarkup(n.symbolType) + '</g>');
            h.push('<rect x="' + (-sz[0] / 2) + '" y="' + (-sz[1] / 2) + '" width="' + sz[0] + '" height="' + sz[1] +
                '" fill="transparent" stroke="' + (sel ? '#e8590c' : 'none') + '" stroke-width="1.6" stroke-dasharray="5 3"/>');
            h.push('<text x="0" y="' + PS.tagOffset(n.symbolType) + '" text-anchor="middle" font-size="12" font-weight="bold" font-family="Arial, sans-serif" fill="#1f2933" pointer-events="none">' + esc(n.tag) + '</text>');
            if (n.name) {
                h.push('<text x="0" y="' + (PS.tagOffset(n.symbolType) + 11) + '" text-anchor="middle" font-size="8.5" font-family="Arial, sans-serif" fill="#6c757d" pointer-events="none">' + esc(n.name) + '</text>');
            }
            if (n.isLocked) {
                h.push('<text x="' + (sz[0] / 2 - 4) + '" y="' + (-sz[1] / 2 + 8) + '" font-size="10" pointer-events="none">\uD83D\uDD12</text>');
            }
            h.push('</g>');
        });

        diagram.instruments.forEach(function (i) {
            var ax = null, ay = null;
            if (i.pidEquipmentNodeId && nodeMap[i.pidEquipmentNodeId]) {
                var nd = nodeMap[i.pidEquipmentNodeId];
                ax = nd.x; ay = nd.y - PS.size(nd.symbolType)[1] / 2;
            } else if (i.pidLineId && lineMap[i.pidLineId] && lineMap[i.pidLineId].points.length > 1) {
                var q = nearest(lineMap[i.pidLineId].points, i.x, i.y);
                ax = q.x; ay = q.y;
            }
            if (ax !== null) {
                h.push('<line x1="' + i.x + '" y1="' + i.y + '" x2="' + ax + '" y2="' + ay + '" stroke="#495057" stroke-width="1" stroke-dasharray="3 2" pointer-events="none"/>');
            }
            var sel = isSel('instrument', i.id);
            h.push('<g data-kind="instrument" data-id="' + i.id + '" transform="translate(' + i.x + ',' + i.y + ')" style="cursor:' + (READONLY ? 'pointer' : 'move') + '">');
            h.push(PS.instrumentMarkup(i.instrumentType));
            h.push('<rect x="-17" y="-17" width="34" height="34" fill="transparent" stroke="' + (sel ? '#e8590c' : 'none') + '" stroke-width="1.6" stroke-dasharray="4 2"/>');
            h.push('<text x="0" y="9" text-anchor="middle" font-size="7.5" font-family="Arial, sans-serif" fill="#1f2933" pointer-events="none">' + esc((i.tag || '').replace(/^[A-Za-z]+-/, '')) + '</text>');
            h.push('<text x="0" y="28" text-anchor="middle" font-size="9" font-family="Arial, sans-serif" fill="#1f2933" pointer-events="none">' + esc(i.tag) + '</text>');
            h.push('</g>');
        });

        diagram.valves.forEach(function (v) {
            var sel = isSel('valve', v.id);
            var vert = Math.abs(((v.rotationDeg || 0) % 180)) === 90;
            h.push('<g data-kind="valve" data-id="' + v.id + '" transform="translate(' + v.x + ',' + v.y + ')" style="cursor:' + (READONLY ? 'pointer' : 'move') + '">');
            h.push('<g transform="rotate(' + (v.rotationDeg || 0) + ')">' + PS.valveMarkup(v.valveType) + '</g>');
            h.push('<rect x="-12" y="-12" width="24" height="24" fill="transparent" stroke="' + (sel ? '#e8590c' : 'none') + '" stroke-width="1.6" stroke-dasharray="4 2"/>');
            h.push('<text x="' + (vert ? 16 : 0) + '" y="' + (vert ? 3 : 24) + '" text-anchor="' + (vert ? 'start' : 'middle') +
                '" font-size="9" font-family="Arial, sans-serif" fill="#1f2933" pointer-events="none">' + esc(v.tag) + (v.isNormallyOpen ? '' : ' (NC)') + '</text>');
            h.push('</g>');
        });

        scene.innerHTML = h.join('');
    }

    function queueRender() {
        if (renderQueued) return;
        renderQueued = true;
        window.requestAnimationFrame(function () {
            renderQueued = false;
            render();
        });
    }

    function applyView() {
        var cw = el.canvas.clientWidth || 1000;
        var ch = el.canvas.clientHeight || 700;
        view.h = view.w * ch / cw;
        svg.setAttribute('viewBox', view.x + ' ' + view.y + ' ' + view.w + ' ' + view.h);
    }

    function fitView() {
        if (!diagram) return;
        var minX = 1e9, minY = 1e9, maxX = -1e9, maxY = -1e9;
        function add(x, y) {
            if (x < minX) minX = x; if (y < minY) minY = y;
            if (x > maxX) maxX = x; if (y > maxY) maxY = y;
        }
        diagram.nodes.forEach(function (n) {
            var s = PS.size(n.symbolType);
            add(n.x - s[0] / 2, n.y - s[1] / 2 - 10);
            add(n.x + s[0] / 2, n.y + s[1] / 2 + 40);
        });
        diagram.lines.forEach(function (l) { l.points.forEach(function (p) { add(p.x, p.y); }); });
        diagram.valves.forEach(function (v) { add(v.x - 15, v.y - 15); add(v.x + 15, v.y + 30); });
        diagram.instruments.forEach(function (i) { add(i.x - 20, i.y - 20); add(i.x + 20, i.y + 35); });
        if (minX > maxX) { minX = 0; minY = 0; maxX = 1200; maxY = 800; }

        var pad = 60;
        var bw = maxX - minX + pad * 2;
        var bh = maxY - minY + pad * 2;
        var cw = el.canvas.clientWidth || 1000;
        var ch = el.canvas.clientHeight || 700;
        var w = Math.max(bw, bh * cw / ch);
        view.w = w;
        view.h = w * ch / cw;
        view.x = (minX + maxX) / 2 - view.w / 2;
        view.y = (minY + maxY) / 2 - view.h / 2;
        applyView();
    }

    function zoomAt(cx, cy, factor) {
        var nw = Math.min(30000, Math.max(120, view.w * factor));
        var f = nw / view.w;
        view.x = cx - (cx - view.x) * f;
        view.y = cy - (cy - view.y) * f;
        view.w = nw;
        applyView();
    }

    function toSvg(clientX, clientY) {
        var pt = svg.createSVGPoint();
        pt.x = clientX;
        pt.y = clientY;
        var ctm = svg.getScreenCTM();
        if (!ctm) return { x: 0, y: 0 };
        var p = pt.matrixTransform(ctm.inverse());
        return { x: p.x, y: p.y };
    }

    /* ---------- selection and properties ---------- */

    function select(kind, id) {
        selected = kind ? { kind: kind, id: id } : null;
        renderProps();
        updateButtons();
        render();
    }

    function updateButtons() {
        var k = selected ? selected.kind : null;
        setDisabled(el.addValve, READONLY || !(k === 'line' || k === 'valve'));
        setDisabled(el.addInstrument, READONLY || !(k === 'node' || k === 'line' || k === 'valve'));
        setDisabled(el.deleteItem, READONLY || !(k === 'valve' || k === 'instrument'));
        setDisabled(el.lockNode, READONLY || k !== 'node');
        setDisabled(el.rotate, READONLY || k !== 'node');
        setDisabled(el.save, READONLY);
    }

    function fieldWrap(label, control) {
        var wrap = document.createElement('div');
        wrap.className = 'mb-2';
        var lb = document.createElement('label');
        lb.className = 'form-label small mb-1';
        lb.textContent = label;
        wrap.appendChild(lb);
        wrap.appendChild(control);
        return wrap;
    }

    function textField(box, label, obj, key, opts) {
        opts = opts || {};
        var input = document.createElement('input');
        input.type = 'text';
        input.className = 'form-control form-control-sm';
        input.value = obj[key] == null ? '' : obj[key];
        input.disabled = READONLY || !!opts.readonly;
        input.addEventListener('input', function () {
            obj[key] = input.value === '' && opts.nullable ? null : input.value;
            markDirty();
            if (opts.after) opts.after();
            queueRender();
        });
        box.appendChild(fieldWrap(label, input));
    }

    function numField(box, label, obj, key, opts) {
        opts = opts || {};
        var input = document.createElement('input');
        input.type = 'number';
        input.className = 'form-control form-control-sm';
        input.step = opts.step || 'any';
        if (opts.min !== undefined) input.min = opts.min;
        if (opts.max !== undefined) input.max = opts.max;
        input.value = obj[key] == null ? '' : obj[key];
        input.disabled = READONLY;
        input.addEventListener('input', function () {
            if (input.value === '') {
                if (opts.nullable) { obj[key] = null; markDirty(); queueRender(); }
                return;
            }
            var n = parseFloat(input.value);
            if (isNaN(n)) return;
            if (opts.min !== undefined && n < opts.min) n = opts.min;
            if (opts.max !== undefined && n > opts.max) n = opts.max;
            obj[key] = n;
            markDirty();
            if (opts.after) opts.after();
            queueRender();
        });
        box.appendChild(fieldWrap(label, input));
    }

    function selectField(box, label, obj, key, options, opts) {
        opts = opts || {};
        var sel = document.createElement('select');
        sel.className = 'form-select form-select-sm';
        sel.disabled = READONLY;
        options.forEach(function (o) {
            var op = document.createElement('option');
            op.value = typeof o === 'object' ? o.value : o;
            op.textContent = typeof o === 'object' ? o.text : o;
            sel.appendChild(op);
        });
        sel.value = String(obj[key]);
        sel.addEventListener('change', function () {
            obj[key] = opts.number ? parseFloat(sel.value) : sel.value;
            markDirty();
            if (opts.after) opts.after();
            queueRender();
        });
        box.appendChild(fieldWrap(label, sel));
    }

    function checkField(box, label, obj, key, opts) {
        opts = opts || {};
        var wrap = document.createElement('div');
        wrap.className = 'form-check mb-2';
        var input = document.createElement('input');
        input.type = 'checkbox';
        input.className = 'form-check-input';
        input.checked = !!obj[key];
        input.disabled = READONLY;
        var lb = document.createElement('label');
        lb.className = 'form-check-label small';
        lb.textContent = label;
        input.addEventListener('change', function () {
            obj[key] = input.checked;
            markDirty();
            if (opts.after) opts.after();
            queueRender();
        });
        wrap.appendChild(input);
        wrap.appendChild(lb);
        box.appendChild(wrap);
    }

    function readonlyField(box, label, text) {
        var input = document.createElement('input');
        input.type = 'text';
        input.className = 'form-control form-control-sm';
        input.value = text == null ? '' : text;
        input.disabled = true;
        box.appendChild(fieldWrap(label, input));
    }

    function heading(box, text) {
        var h = document.createElement('div');
        h.className = 'fw-semibold mb-2';
        h.textContent = text;
        box.appendChild(h);
    }

    function renderProps() {
        var box = el.props;
        if (!box) return;
        box.innerHTML = '';

        var item = getSelected();
        if (!item) {
            var p = document.createElement('div');
            p.className = 'text-muted small';
            p.textContent = 'Select an item on the drawing.';
            box.appendChild(p);
            return;
        }

        if (selected.kind === 'node') {
            heading(box, 'Equipment');
            textField(box, 'Tag', item, 'tag');
            textField(box, 'Name', item, 'name');
            readonlyField(box, 'Symbol', item.symbolType);
            readonlyField(box, 'Stage', item.stageNo);
            selectField(box, 'Rotation', item, 'rotationDeg', [
                { value: '0', text: '0' }, { value: '90', text: '90' },
                { value: '180', text: '180' }, { value: '270', text: '270' }
            ], { number: true });
            checkField(box, 'Locked (excluded from auto layout)', item, 'isLocked', { after: function () { updateButtons(); } });
        } else if (selected.kind === 'line') {
            var from = nodeMap[item.fromNodeId], to = nodeMap[item.toNodeId];
            heading(box, 'Line');
            textField(box, 'Line No', item, 'lineNo');
            readonlyField(box, 'From', from ? from.tag : '');
            readonlyField(box, 'To', to ? to.tag : '');
            selectField(box, 'Medium', item, 'medium', PS.mediums);
            numField(box, 'Nominal size (mm)', item, 'nominalSizeMm', { min: 0 });
            textField(box, 'Material', item, 'material', { nullable: true });
            checkField(box, 'Insulated', item, 'isInsulated');
            textField(box, 'Notes', item, 'notes', { nullable: true });
        } else if (selected.kind === 'valve') {
            var vl = lineMap[item.pidLineId];
            heading(box, 'Valve');
            textField(box, 'Tag', item, 'tag');
            readonlyField(box, 'Line', vl ? vl.lineNo : '');
            selectField(box, 'Type', item, 'valveType', PS.valveTypes);
            numField(box, 'Nominal size (mm)', item, 'nominalSizeMm', { min: 0 });
            numField(box, 'Position on line (0.05 - 0.95)', item, 'positionOnLine', {
                min: 0.05, max: 0.95, step: 0.05,
                after: function () { placeValve(item); }
            });
            checkField(box, 'Normally open', item, 'isNormallyOpen');
            textField(box, 'Notes', item, 'notes', { nullable: true });
        } else if (selected.kind === 'instrument') {
            heading(box, 'Instrument');
            textField(box, 'Tag', item, 'tag');
            selectField(box, 'Type', item, 'instrumentType', PS.instrumentTypes);
            textField(box, 'Service', item, 'service', { nullable: true });
            numField(box, 'Range min', item, 'rangeMin', { nullable: true });
            numField(box, 'Range max', item, 'rangeMax', { nullable: true });
            textField(box, 'Unit', item, 'unit', { nullable: true });
            textField(box, 'Notes', item, 'notes', { nullable: true });
        }
    }

    /* ---------- actions ---------- */

    function lineForSelection() {
        var item = getSelected();
        if (!item) return null;
        if (selected.kind === 'line') return item;
        if (selected.kind === 'valve') return lineMap[item.pidLineId] || null;
        return null;
    }

    function addValve() {
        if (READONLY) return;
        var line = lineForSelection();
        if (!line) { msg('Select a line first.', 'warning'); return; }

        var type = el.valveType && el.valveType.value ? el.valveType.value : 'Gate';
        var tries = [0.5, 0.35, 0.65, 0.25, 0.75, 0.15, 0.85];
        var pos = 0.5;
        for (var t = 0; t < tries.length; t++) {
            var free = true;
            for (var k = 0; k < diagram.valves.length; k++) {
                var ov = diagram.valves[k];
                if (ov.pidLineId === line.id && Math.abs(ov.positionOnLine - tries[t]) < 0.08) { free = false; break; }
            }
            if (free) { pos = tries[t]; break; }
        }

        var v = {
            id: uid(),
            pidLineId: line.id,
            tag: nextTag('V', diagram.valves.map(function (x) { return x.tag; })),
            valveType: type,
            nominalSizeMm: line.nominalSizeMm,
            positionOnLine: pos,
            x: 0, y: 0, rotationDeg: 0,
            isNormallyOpen: true,
            notes: null
        };
        diagram.valves.push(v);
        placeValve(v);
        markDirty();
        select('valve', v.id);
    }

    function addInstrument() {
        if (READONLY) return;
        var item = getSelected();
        if (!item) { msg('Select equipment or a line first.', 'warning'); return; }

        var type = el.instrumentType && el.instrumentType.value ? el.instrumentType.value : 'PT';
        var defaults = {
            PT: { service: 'Pressure', unit: 'bar' },
            TT: { service: 'Temperature', unit: '\u00b0C' },
            FT: { service: 'Flow', unit: 'm\u00b3/h' },
            LT: { service: 'Level', unit: '%' }
        }[type] || { service: null, unit: null };

        var ins = {
            id: uid(),
            pidLineId: null,
            pidEquipmentNodeId: null,
            tag: nextTag(type, diagram.instruments.map(function (x) { return x.tag; })),
            instrumentType: type,
            service: defaults.service,
            rangeMin: null,
            rangeMax: null,
            unit: defaults.unit,
            x: 0, y: 0,
            notes: null
        };

        var count, x, y;
        if (selected.kind === 'node') {
            ins.pidEquipmentNodeId = item.id;
            count = diagram.instruments.filter(function (i) { return i.pidEquipmentNodeId === item.id; }).length;
            x = item.x + count * 40 - 20;
            y = item.y - PS.size(item.symbolType)[1] / 2 - 45;
        } else {
            var line = lineForSelection();
            if (!line || line.points.length < 2) { msg('Select equipment or a line first.', 'warning'); return; }
            ins.pidLineId = line.id;
            count = diagram.instruments.filter(function (i) { return i.pidLineId === line.id; }).length;
            var mp = pointAt(line.points, 0.5);
            x = mp.h ? mp.x + count * 40 : mp.x + 35;
            y = mp.h ? mp.y - 35 : mp.y + count * 40;
        }
        ins.x = r2(x);
        ins.y = r2(y);

        diagram.instruments.push(ins);
        markDirty();
        select('instrument', ins.id);
    }

    function deleteSelected() {
        if (READONLY || !selected) return;
        if (selected.kind === 'valve') {
            diagram.valves = diagram.valves.filter(function (v) { return v.id !== selected.id; });
        } else if (selected.kind === 'instrument') {
            diagram.instruments = diagram.instruments.filter(function (i) { return i.id !== selected.id; });
        } else {
            msg('Only valves and instruments can be deleted.', 'warning');
            return;
        }
        markDirty();
        select(null, null);
    }

    function toggleLock() {
        var n = selected && selected.kind === 'node' ? getSelected() : null;
        if (READONLY || !n) return;
        n.isLocked = !n.isLocked;
        markDirty();
        renderProps();
        render();
    }

    function rotateNode() {
        var n = selected && selected.kind === 'node' ? getSelected() : null;
        if (READONLY || !n) return;
        n.rotationDeg = ((n.rotationDeg || 0) + 90) % 360;
        markDirty();
        renderProps();
        render();
    }

    function save() {
        if (READONLY || saving || !diagram) return;
        saving = true;
        setDisabled(el.save, true);
        post(URLS.save, diagram).then(function (res) {
            saving = false;
            setDisabled(el.save, READONLY);
            if (res.ok && res.data) {
                diagram = normalize(res.data);
                reindex();
                if (selected && !getSelected()) selected = null;
                clearDirty();
                renderProps();
                updateButtons();
                render();
                msg('Saved.', 'success');
            } else {
                msg(res.text || ('Save failed (' + res.status + ').'), 'danger');
            }
        }).catch(function () {
            saving = false;
            setDisabled(el.save, READONLY);
            msg('Save failed. Check the connection.', 'danger');
        });
    }

    function changeStatus(status, askRemarks, confirmText) {
        if (dirty) { msg('Save changes first.', 'warning'); return; }
        if (confirmText && !window.confirm(confirmText)) return;
        var remarks = null;
        if (askRemarks) {
            remarks = window.prompt('Remarks (optional):', '');
            if (remarks === null) return;
        }
        post(URLS.status, { id: DIAGRAM_ID, status: status, remarks: remarks }).then(function (res) {
            if (res.ok) { window.location.reload(); }
            else { msg(res.text || ('Status change failed (' + res.status + ').'), 'danger'); }
        });
    }

    function newRevision() {
        if (dirty) { msg('Save changes first.', 'warning'); return; }
        var remarks = window.prompt('Revision remarks (optional):', '');
        if (remarks === null) return;
        post(URLS.revision, { id: DIAGRAM_ID, remarks: remarks }).then(function (res) {
            if (res.ok) { window.location.reload(); }
            else { msg(res.text || ('New revision failed (' + res.status + ').'), 'danger'); }
        });
    }

    /* ---------- pointer interaction ---------- */

    function onPointerDown(e) {
        if (e.button !== 0) return;
        var t = e.target.closest ? e.target.closest('[data-kind]') : null;

        if (!t) {
            drag = {
                mode: 'pan', cx: e.clientX, cy: e.clientY,
                sx: view.x, sy: view.y,
                scale: view.w / (el.canvas.clientWidth || 1000),
                moved: false
            };
        } else {
            var kind = t.getAttribute('data-kind');
            var id = t.getAttribute('data-id');
            select(kind, id);
            var item = getSelected();
            if (!READONLY && item && kind !== 'line' && !(kind === 'node' && item.isLocked)) {
                var p = toSvg(e.clientX, e.clientY);
                drag = {
                    mode: kind, id: id, cx: e.clientX, cy: e.clientY, moved: false,
                    startP: p, ox: item.x, oy: item.y,
                    attached: kind === 'node'
                        ? diagram.instruments.filter(function (i) { return i.pidEquipmentNodeId === id; })
                            .map(function (i) { return { o: i, x: i.x, y: i.y }; })
                        : []
                };
            }
        }

        if (drag) {
            document.addEventListener('pointermove', onPointerMove);
            document.addEventListener('pointerup', onPointerUp);
            e.preventDefault();
        }
    }

    function onPointerMove(e) {
        if (!drag) return;
        if (!drag.moved && Math.abs(e.clientX - drag.cx) + Math.abs(e.clientY - drag.cy) < 3) return;
        drag.moved = true;

        if (drag.mode === 'pan') {
            view.x = drag.sx - (e.clientX - drag.cx) * drag.scale;
            view.y = drag.sy - (e.clientY - drag.cy) * drag.scale;
            applyView();
            return;
        }

        var p = toSvg(e.clientX, e.clientY);
        var item = getSelected();
        if (!item) return;
        var nx = snap(drag.ox + p.x - drag.startP.x);
        var ny = snap(drag.oy + p.y - drag.startP.y);

        if (drag.mode === 'node') {
            item.x = nx;
            item.y = ny;
            drag.attached.forEach(function (a) {
                a.o.x = r2(a.x + nx - drag.ox);
                a.o.y = r2(a.y + ny - drag.oy);
            });
            rerouteNodeLines(item.id);
        } else if (drag.mode === 'valve') {
            var line = lineMap[item.pidLineId];
            if (line && line.points.length > 1) {
                var q = nearest(line.points, drag.ox + p.x - drag.startP.x, drag.oy + p.y - drag.startP.y);
                item.positionOnLine = r3(Math.min(0.95, Math.max(0.05, q.f)));
                placeValve(item);
            }
        } else if (drag.mode === 'instrument') {
            item.x = nx;
            item.y = ny;
        }
        queueRender();
    }

    function onPointerUp() {
        document.removeEventListener('pointermove', onPointerMove);
        document.removeEventListener('pointerup', onPointerUp);
        var d = drag;
        drag = null;
        if (!d) return;

        if (d.mode === 'pan') {
            if (!d.moved) select(null, null);
            return;
        }
        if (d.moved) {
            markDirty();
            renderProps();
            render();
        }
    }

    function onWheel(e) {
        e.preventDefault();
        var p = toSvg(e.clientX, e.clientY);
        zoomAt(p.x, p.y, e.deltaY < 0 ? 0.9 : 1.1);
    }

    function onKeyDown(e) {
        var tag = e.target && e.target.tagName ? e.target.tagName.toLowerCase() : '';
        if ((e.ctrlKey || e.metaKey) && (e.key === 's' || e.key === 'S')) {
            e.preventDefault();
            save();
            return;
        }
        if (tag === 'input' || tag === 'textarea' || tag === 'select') return;
        if (e.key === 'Delete' || e.key === 'Backspace') {
            if (selected && (selected.kind === 'valve' || selected.kind === 'instrument')) {
                e.preventDefault();
                deleteSelected();
            }
        } else if (e.key === 'Escape') {
            select(null, null);
        }
    }

    /* ---------- legend and init ---------- */

    function renderLegend() {
        if (!el.legend) return;
        var h = [];
        PS.legendItems().forEach(function (it) {
            h.push('<div class="d-flex align-items-center small mb-1">' +
                '<svg width="40" height="8" class="me-2"><line x1="0" y1="4" x2="40" y2="4" stroke="' + it.color + '" stroke-width="2.5"' +
                (it.dash ? ' stroke-dasharray="' + it.dash + '"' : '') + '/></svg>' +
                '<span>' + esc(it.label) + '</span></div>');
        });
        el.legend.innerHTML = h.join('');
    }

    function buildSvg() {
        svg = document.createElementNS(NS, 'svg');
        svg.setAttribute('width', '100%');
        svg.setAttribute('height', '100%');
        svg.style.display = 'block';
        svg.style.background = '#ffffff';
        svg.style.touchAction = 'none';
        svg.style.userSelect = 'none';
        svg.innerHTML =
            '<defs><pattern id="pidGrid" width="50" height="50" patternUnits="userSpaceOnUse">' +
            '<path d="M50 0 L0 0 0 50" fill="none" stroke="#eef0f2" stroke-width="1"/></pattern></defs>' +
            '<rect x="-20000" y="-20000" width="40000" height="40000" fill="url(#pidGrid)" pointer-events="none"/>' +
            '<g id="pidScene"></g>';
        scene = svg.querySelector('#pidScene');
        el.canvas.innerHTML = '';
        el.canvas.appendChild(svg);

        svg.addEventListener('pointerdown', onPointerDown);
        svg.addEventListener('wheel', onWheel, { passive: false });
    }

    function bind(elm, fn) {
        if (elm) elm.addEventListener('click', fn);
    }

    function init() {
        if (!el.canvas) return;
        buildSvg();
        renderLegend();

        bind(el.save, save);
        bind(el.addValve, addValve);
        bind(el.addInstrument, addInstrument);
        bind(el.deleteItem, deleteSelected);
        bind(el.lockNode, toggleLock);
        bind(el.rotate, rotateNode);
        bind(el.fit, fitView);
        bind(el.zoomIn, function () { zoomAt(view.x + view.w / 2, view.y + view.h / 2, 0.8); });
        bind(el.zoomOut, function () { zoomAt(view.x + view.w / 2, view.y + view.h / 2, 1.25); });
        bind(el.submitReview, function () { changeStatus('InReview', false, null); });
        bind(el.approve, function () { changeStatus('Approved', true, 'Approve this P&ID? It will become read-only.'); });
        bind(el.reject, function () { changeStatus('Rejected', true, null); });
        bind(el.newRevision, newRevision);

        document.addEventListener('keydown', onKeyDown);
        window.addEventListener('resize', applyView);
        window.addEventListener('beforeunload', function (e) {
            if (dirty) { e.preventDefault(); e.returnValue = ''; }
        });

        updateButtons();
        renderProps();

        request(URLS.data).then(function (res) {
            if (!res.ok || !res.data) {
                msg('Could not load the diagram (' + res.status + ').', 'danger');
                return;
            }
            diagram = normalize(res.data);
            reindex();
            render();
            fitView();
        }).catch(function () {
            msg('Could not load the diagram.', 'danger');
        });
    }

    init();
})();
