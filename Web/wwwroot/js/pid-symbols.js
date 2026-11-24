(function (global) {
    'use strict';

    var SIZES = {
        Tank: [80, 100],
        Digester: [120, 140],
        Pump: [40, 40],
        Valve: [20, 20],
        Blower: [50, 50],
        Flare: [40, 80],
        GasHolder: [110, 110],
        Vessel: [100, 70],
        Instrument: [30, 30]
    };

    var MEDIUM_COLORS = {
        Slurry: '#8d6e63',
        Biogas: '#f9a825',
        Cbg: '#2e7d32',
        Water: '#1976d2',
        Digestate: '#6d4c41',
        Steam: '#78909c',
        Air: '#00acc1',
        Condensate: '#5c6bc0',
        Other: '#757575'
    };

    var SYMBOL_TYPES = ['Tank', 'Pump', 'Blower', 'Flare', 'Digester', 'GasHolder', 'Vessel'];
    var VALVE_TYPES = ['Gate', 'Ball', 'Butterfly', 'Check', 'Globe', 'Relief', 'Control', 'Solenoid'];
    var INSTRUMENT_TYPES = ['PT', 'TT', 'FT', 'LT'];

    var STROKE = 'stroke="currentColor" stroke-width="1.5" fill="none" stroke-linejoin="round" stroke-linecap="round"';

    function esc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function r2(v) {
        return Math.round(v * 100) / 100;
    }

    function size(type) {
        var s = SIZES[type] || [80, 80];
        return { w: s[0], h: s[1] };
    }

    function shorten(text, max) {
        text = String(text == null ? '' : text);
        return text.length > max ? text.slice(0, max - 1) + '\u2026' : text;
    }

    function textEl(text, x, y, fontSize, anchor, muted) {
        return '<text x="' + r2(x) + '" y="' + r2(y) + '" font-size="' + fontSize + '" text-anchor="' + (anchor || 'middle') +
            '" fill="currentColor" stroke="none"' + (muted ? ' opacity="0.65"' : '') +
            ' font-family="Arial, Helvetica, sans-serif">' + esc(text) + '</text>';
    }

    function hitRect(w, h) {
        return '<rect class="pid-hit" x="' + r2(-w / 2) + '" y="' + r2(-h / 2) + '" width="' + w + '" height="' + h +
            '" fill="transparent" stroke="none" pointer-events="all"/>';
    }

    /* ---------- equipment symbols (centred on 0,0) ---------- */

    function symbolShape(type, w, h) {
        var hw = w / 2;
        var hh = h / 2;
        var r = Math.min(hw, hh) * 0.9;

        switch (type) {
            case 'Tank':
                return '<rect x="' + (-hw) + '" y="' + (-hh) + '" width="' + w + '" height="' + h + '" rx="8"/>' +
                    '<line x1="' + (-hw) + '" y1="' + (-hh + 14) + '" x2="' + hw + '" y2="' + (-hh + 14) + '"/>';

            case 'Digester':
                return '<path d="M ' + (-hw) + ' ' + hh + ' L ' + (-hw) + ' ' + (-hh / 3) +
                    ' Q 0 ' + (-hh * 1.25) + ' ' + hw + ' ' + (-hh / 3) + ' L ' + hw + ' ' + hh + ' Z"/>' +
                    '<line x1="0" y1="' + (-hh * 0.55) + '" x2="0" y2="' + (hh * 0.7) + '"/>' +
                    '<line x1="-14" y1="' + (hh * 0.7) + '" x2="14" y2="' + (hh * 0.7) + '"/>' +
                    '<line x1="-10" y1="' + (hh * 0.25) + '" x2="10" y2="' + (hh * 0.25) + '"/>';

            case 'Pump':
                return '<circle cx="0" cy="0" r="' + r + '"/>' +
                    '<path d="M ' + (-r * 0.4) + ' ' + (-r * 0.6) + ' L ' + (r * 0.75) + ' 0 L ' + (-r * 0.4) + ' ' + (r * 0.6) + ' Z"/>';

            case 'Blower':
                return '<circle cx="0" cy="0" r="' + r + '"/>' +
                    '<line x1="0" y1="0" x2="0" y2="' + (-r * 0.75) + '"/>' +
                    '<line x1="0" y1="0" x2="' + r2(r * 0.65) + '" y2="' + r2(r * 0.375) + '"/>' +
                    '<line x1="0" y1="0" x2="' + r2(-r * 0.65) + '" y2="' + r2(r * 0.375) + '"/>';

            case 'Flare':
                return '<rect x="-4" y="' + (-hh + 24) + '" width="8" height="' + (h - 24) + '"/>' +
                    '<path d="M 0 ' + (-hh) + ' C 12 ' + (-hh + 10) + ' 10 ' + (-hh + 20) + ' 0 ' + (-hh + 24) +
                    ' C -10 ' + (-hh + 20) + ' -12 ' + (-hh + 10) + ' 0 ' + (-hh) + ' Z"/>' +
                    '<line x1="-14" y1="' + hh + '" x2="14" y2="' + hh + '"/>';

            case 'GasHolder':
                return '<path d="M ' + (-hw) + ' ' + hh + ' L ' + (-hw) + ' 0 A ' + hw + ' ' + hw + ' 0 0 1 ' + hw + ' 0 L ' + hw + ' ' + hh + ' Z"/>' +
                    '<path d="M ' + (-hw * 0.7) + ' 0 A ' + (hw * 0.7) + ' ' + (hw * 0.7) + ' 0 0 1 ' + (hw * 0.7) + ' 0"/>';

            case 'Vessel':
                return '<rect x="' + (-hw) + '" y="' + (-hh) + '" width="' + w + '" height="' + h + '" rx="' + r2(h / 3) + '"/>' +
                    '<line x1="' + (-hw * 0.4) + '" y1="' + (-hh) + '" x2="' + (-hw * 0.4) + '" y2="' + hh + '"/>';

            default:
                return '<rect x="' + (-hw) + '" y="' + (-hh) + '" width="' + w + '" height="' + h + '"/>';
        }
    }

    function nodeSvg(node) {
        var s = size(node.symbolType);
        var rot = node.rotationDeg || 0;

        return '<g class="pid-node' + (node.isLocked ? ' is-locked' : '') + '" data-id="' + esc(node.id) +
            '" transform="translate(' + r2(node.x) + ' ' + r2(node.y) + ')">' +
            '<g transform="rotate(' + rot + ')"><g ' + STROKE + '>' + symbolShape(node.symbolType, s.w, s.h) + '</g></g>' +
            hitRect(s.w, s.h) +
            textEl(node.tag, 0, s.h / 2 + 14, 12, 'middle', false) +
            textEl(shorten(node.name, 24), 0, s.h / 2 + 27, 10, 'middle', true) +
            '</g>';
    }

    /* ---------- valves ---------- */

    function valveShape(type) {
        var bow = '<path d="M -10 -7 L 10 7 L 10 -7 L -10 7 Z"/>';

        switch (type) {
            case 'Ball':
                return bow + '<circle cx="0" cy="0" r="3" fill="currentColor"/>';
            case 'Butterfly':
                return bow + '<line x1="0" y1="-9" x2="0" y2="9"/>';
            case 'Globe':
                return bow + '<circle cx="0" cy="0" r="3"/>';
            case 'Check':
                return '<path d="M -8 -7 L 8 0 L -8 7 Z"/><line x1="8" y1="-8" x2="8" y2="8"/>';
            case 'Relief':
                return '<path d="M -8 8 L 0 -8 L 8 8 Z"/><path d="M -4 -10 L 4 -12 L -4 -14 L 4 -16"/>';
            case 'Control':
                return bow + '<line x1="0" y1="0" x2="0" y2="-10"/><path d="M -7 -10 A 7 7 0 0 1 7 -10 Z"/>';
            case 'Solenoid':
                return bow + '<line x1="0" y1="0" x2="0" y2="-8"/><rect x="-5" y="-15" width="10" height="7"/>';
            default:
                return bow;
        }
    }

    function valveSvg(valve) {
        var rot = valve.rotationDeg || 0;
        var open = valve.isNormallyOpen === false;

        return '<g class="pid-valve" data-id="' + esc(valve.id) + '" transform="translate(' + r2(valve.x) + ' ' + r2(valve.y) + ')">' +
            '<g transform="rotate(' + rot + ')"><g ' + STROKE + '>' + valveShape(valve.valveType) + '</g></g>' +
            hitRect(24, 24) +
            textEl(valve.tag, 0, 24, 10, 'middle', false) +
            (open ? textEl('NC', 0, 35, 8, 'middle', true) : '') +
            '</g>';
    }

    /* ---------- instruments ---------- */

    function instrumentSvg(ins) {
        return '<g class="pid-instrument" data-id="' + esc(ins.id) + '" transform="translate(' + r2(ins.x) + ' ' + r2(ins.y) + ')">' +
            '<g ' + STROKE + '><circle cx="0" cy="0" r="13"/><line x1="-13" y1="0" x2="13" y2="0"/></g>' +
            textEl(ins.instrumentType, 0, -2, 9, 'middle', false) +
            textEl(String(ins.tag || '').replace(/^[A-Z]+-/, ''), 0, 9, 8, 'middle', false) +
            hitRect(30, 30) +
            textEl(ins.tag, 0, 27, 10, 'middle', false) +
            '</g>';
    }

    /* ---------- lines ---------- */

    function pointAt(points, fraction) {
        if (!points || !points.length) return { x: 0, y: 0, horizontal: true };
        if (points.length === 1) return { x: points[0].x, y: points[0].y, horizontal: true };

        var total = 0;
        var i;
        for (i = 1; i < points.length; i++) {
            total += Math.abs(points[i].x - points[i - 1].x) + Math.abs(points[i].y - points[i - 1].y);
        }

        var target = total * Math.max(0, Math.min(1, fraction));
        var run = 0;

        for (i = 1; i < points.length; i++) {
            var seg = Math.abs(points[i].x - points[i - 1].x) + Math.abs(points[i].y - points[i - 1].y);
            if (seg > 0 && run + seg >= target) {
                var t = (target - run) / seg;
                return {
                    x: points[i - 1].x + (points[i].x - points[i - 1].x) * t,
                    y: points[i - 1].y + (points[i].y - points[i - 1].y) * t,
                    horizontal: points[i].y === points[i - 1].y
                };
            }
            run += seg;
        }

        var last = points[points.length - 1];
        return { x: last.x, y: last.y, horizontal: true };
    }

    function lineSvg(line) {
        var pts = line.points || [];
        var color = MEDIUM_COLORS[line.medium] || MEDIUM_COLORS.Other;
        var path = pts.map(function (p) { return r2(p.x) + ',' + r2(p.y); }).join(' ');
        var mid = pointAt(pts, 0.5);
        var label = (line.lineNo || '');

        return '<g class="pid-line" data-id="' + esc(line.id) + '">' +
            '<polyline points="' + path + '" fill="none" stroke="transparent" stroke-width="14" pointer-events="stroke"/>' +
            '<polyline points="' + path + '" fill="none" stroke="' + color + '" stroke-width="2" stroke-linejoin="round"' +
            (line.isInsulated ? ' stroke-dasharray="10 3"' : '') + '/>' +
            (pts.length >= 2 ? textEl(label, mid.x, mid.horizontal ? mid.y - 8 : mid.y, 9, mid.horizontal ? 'middle' : 'start', false) : '') +
            '</g>';
    }

    /* ---------- palette and legend ---------- */

    function legend() {
        var items = [];

        SYMBOL_TYPES.forEach(function (t) {
            items.push({ kind: 'equipment', type: t, label: t === 'GasHolder' ? 'Gas holder' : t });
        });
        VALVE_TYPES.forEach(function (t) {
            items.push({ kind: 'valve', type: t, label: t + ' valve' });
        });
        INSTRUMENT_TYPES.forEach(function (t) {
            var names = { PT: 'Pressure transmitter', TT: 'Temperature transmitter', FT: 'Flow transmitter', LT: 'Level transmitter' };
            items.push({ kind: 'instrument', type: t, label: names[t] });
        });

        return items;
    }

    function previewSvg(kind, type, boxSize) {
        var box = boxSize || 56;
        var inner;
        var w;
        var h;

        if (kind === 'valve') {
            inner = '<g ' + STROKE + '>' + valveShape(type) + '</g>';
            w = 36;
            h = 36;
        } else if (kind === 'instrument') {
            inner = '<g ' + STROKE + '><circle cx="0" cy="0" r="13"/><line x1="-13" y1="0" x2="13" y2="0"/></g>' +
                textEl(type, 0, -2, 9, 'middle', false);
            w = 36;
            h = 36;
        } else {
            var s = size(type);
            inner = '<g ' + STROKE + '>' + symbolShape(type, s.w, s.h) + '</g>';
            w = s.w + 8;
            h = s.h + 8;
        }

        var scale = Math.min(box / w, box / h);
        return '<svg width="' + box + '" height="' + box + '" viewBox="' + (-box / 2) + ' ' + (-box / 2) + ' ' + box + ' ' + box + '">' +
            '<g transform="scale(' + r2(scale) + ')">' + inner + '</g></svg>';
    }

    global.PidSymbols = {
        size: size,
        mediumColors: MEDIUM_COLORS,
        symbolTypes: SYMBOL_TYPES,
        valveTypes: VALVE_TYPES,
        instrumentTypes: INSTRUMENT_TYPES,
        nodeSvg: nodeSvg,
        valveSvg: valveSvg,
        instrumentSvg: instrumentSvg,
        lineSvg: lineSvg,
        pointAt: pointAt,
        legend: legend,
        previewSvg: previewSvg
    };
})(window);
