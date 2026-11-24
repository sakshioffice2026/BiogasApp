(function (global) {
    'use strict';

    var STROKE = '#1f2933';
    var FILL = '#ffffff';
    var SW = 1.6;

    var SIZES = {
        Tank: [80, 100],
        Pump: [40, 40],
        Valve: [20, 20],
        Blower: [50, 50],
        Flare: [40, 80],
        Digester: [120, 140],
        GasHolder: [110, 110],
        Vessel: [100, 70],
        Instrument: [30, 30]
    };

    var NUM_TO_SYMBOL = { 1: 'Tank', 2: 'Pump', 3: 'Valve', 4: 'Blower', 5: 'Flare', 6: 'Digester', 7: 'GasHolder', 8: 'Vessel', 9: 'Instrument' };
    var NUM_TO_MEDIUM = { 1: 'Slurry', 2: 'Biogas', 3: 'Cbg', 4: 'Water', 5: 'Digestate', 6: 'Steam', 7: 'Air', 8: 'Condensate', 9: 'Other' };
    var NUM_TO_VALVE = { 1: 'Gate', 2: 'Ball', 3: 'Butterfly', 4: 'Check', 5: 'Globe', 6: 'Relief', 7: 'Control', 8: 'Solenoid' };
    var NUM_TO_INSTR = { 1: 'PT', 2: 'TT', 3: 'FT', 4: 'LT' };
    var NUM_TO_STATUS = { 1: 'Draft', 2: 'InReview', 3: 'Approved', 4: 'Rejected', 5: 'Superseded' };

    var MEDIUM_STYLE = {
        Slurry: { color: '#8b5a2b', dash: '' },
        Biogas: { color: '#d9822b', dash: '' },
        Cbg: { color: '#2f9e44', dash: '' },
        Water: { color: '#1c7ed6', dash: '' },
        Digestate: { color: '#5c3d1e', dash: '8 4' },
        Steam: { color: '#868e96', dash: '2 3' },
        Air: { color: '#15aabf', dash: '6 3' },
        Condensate: { color: '#4263eb', dash: '8 3 2 3' },
        Other: { color: '#495057', dash: '' }
    };

    var MEDIUM_LABEL = {
        Slurry: 'Slurry', Biogas: 'Biogas', Cbg: 'CBG', Water: 'Water', Digestate: 'Digestate',
        Steam: 'Steam', Air: 'Air', Condensate: 'Condensate', Other: 'Other'
    };

    function attr(extra) {
        return 'fill="' + FILL + '" stroke="' + STROKE + '" stroke-width="' + SW + '"' + (extra ? ' ' + extra : '');
    }

    function line(x1, y1, x2, y2) {
        return '<line x1="' + x1 + '" y1="' + y1 + '" x2="' + x2 + '" y2="' + y2 + '" stroke="' + STROKE + '" stroke-width="' + SW + '"/>';
    }

    function tank() {
        return '<path d="M-40,-35 L-40,35 A40,12 0 0 0 40,35 L40,-35 A40,12 0 0 0 -40,-35 Z" ' + attr() + '/>' +
            '<ellipse cx="0" cy="-35" rx="40" ry="12" ' + attr() + '/>';
    }

    function digester() {
        return '<path d="M-60,-45 L-60,55 L60,55 L60,-45 A60,25 0 0 0 -60,-45 Z" ' + attr() + '/>' +
            '<line x1="0" y1="-20" x2="0" y2="40" stroke="' + STROKE + '" stroke-width="' + SW + '"/>' +
            '<line x1="-22" y1="30" x2="22" y2="30" stroke="' + STROKE + '" stroke-width="' + SW + '"/>' +
            '<line x1="-16" y1="8" x2="16" y2="8" stroke="' + STROKE + '" stroke-width="' + SW + '"/>';
    }

    function pump() {
        return '<circle cx="0" cy="0" r="20" ' + attr() + '/>' +
            '<path d="M-14,-14 L20,0 L-14,14" ' + attr('fill="none"') + '/>';
    }

    function blower() {
        return '<circle cx="0" cy="0" r="25" ' + attr() + '/>' +
            '<path d="M-14,-14 A20,20 0 0 1 14,-14" ' + attr('fill="none"') + '/>' +
            '<path d="M14,14 A20,20 0 0 1 -14,14" ' + attr('fill="none"') + '/>' +
            '<circle cx="0" cy="0" r="4" fill="' + STROKE + '" stroke="none"/>';
    }

    function flare() {
        return '<rect x="-6" y="-10" width="12" height="50" ' + attr() + '/>' +
            '<path d="M0,-40 C14,-26 14,-14 0,-8 C-14,-14 -14,-26 0,-40 Z" fill="#f59f00" stroke="' + STROKE + '" stroke-width="' + SW + '"/>' +
            '<line x1="-20" y1="40" x2="20" y2="40" stroke="' + STROKE + '" stroke-width="' + SW + '"/>';
    }

    function gasHolder() {
        return '<path d="M-55,10 L-55,40 L55,40 L55,10 Z" ' + attr() + '/>' +
            '<path d="M-55,10 A55,55 0 0 1 55,10 Z" ' + attr('fill="#e7f5ff"') + '/>' +
            '<path d="M-40,10 A40,34 0 0 1 40,10" ' + attr('fill="none"') + '/>';
    }

    function vessel() {
        return '<path d="M-35,-35 L35,-35 A15,35 0 0 1 35,35 L-35,35 A15,35 0 0 1 -35,-35 Z" ' + attr() + '/>' +
            '<line x1="-50" y1="0" x2="-45" y2="0" stroke="' + STROKE + '" stroke-width="' + SW + '"/>';
    }

    function instrumentBubble(type) {
        return '<circle cx="0" cy="0" r="15" ' + attr() + '/>' +
            '<line x1="-15" y1="0" x2="15" y2="0" stroke="' + STROKE + '" stroke-width="1"/>' +
            '<text x="0" y="-3" text-anchor="middle" font-size="9" font-family="Arial, sans-serif" font-weight="bold" fill="' + STROKE + '">' + (type || '') + '</text>';
    }

    var NODE_DRAW = {
        Tank: tank,
        Digester: digester,
        Pump: pump,
        Blower: blower,
        Flare: flare,
        GasHolder: gasHolder,
        Vessel: vessel,
        Instrument: function () { return instrumentBubble(''); },
        Valve: function () { return valveMarkup('Gate'); }
    };

    function valveMarkup(type) {
        var body = '<path d="M-10,-7 L10,7 L10,-7 L-10,7 Z" ' + attr() + '/>';
        switch (type) {
            case 'Ball':
                return body + '<circle cx="0" cy="0" r="3.5" fill="' + STROKE + '"/>';
            case 'Butterfly':
                return '<path d="M-10,-7 L10,7 L10,-7 L-10,7 Z" ' + attr() + '/>' +
                    '<line x1="0" y1="-9" x2="0" y2="9" stroke="' + STROKE + '" stroke-width="' + SW + '"/>';
            case 'Check':
                return '<path d="M-10,-7 L10,7 L10,-7 L-10,7 Z" ' + attr('fill="none"') + '/>' +
                    '<path d="M-4,0 L4,-5 L4,5 Z" fill="' + STROKE + '"/>';
            case 'Globe':
                return body + '<circle cx="0" cy="0" r="3.5" ' + attr() + '/>';
            case 'Relief':
                return '<path d="M-10,8 L0,-2 L10,8 Z" ' + attr() + '/>' +
                    '<line x1="0" y1="-2" x2="0" y2="-12" stroke="' + STROKE + '" stroke-width="' + SW + '"/>' +
                    '<line x1="-5" y1="-12" x2="5" y2="-12" stroke="' + STROKE + '" stroke-width="' + SW + '"/>';
            case 'Control':
                return body +
                    '<line x1="0" y1="0" x2="0" y2="-10" stroke="' + STROKE + '" stroke-width="' + SW + '"/>' +
                    '<path d="M-7,-10 A7,6 0 0 1 7,-10 Z" ' + attr() + '/>';
            case 'Solenoid':
                return body +
                    '<line x1="0" y1="0" x2="0" y2="-9" stroke="' + STROKE + '" stroke-width="' + SW + '"/>' +
                    '<rect x="-5" y="-15" width="10" height="6" ' + attr() + '/>' +
                    '<text x="0" y="-10" text-anchor="middle" font-size="5" font-family="Arial, sans-serif" fill="' + STROKE + '">S</text>';
            default:
                return body + '<line x1="0" y1="-8" x2="0" y2="0" stroke="' + STROKE + '" stroke-width="' + SW + '"/>' +
                    '<line x1="-4" y1="-8" x2="4" y2="-8" stroke="' + STROKE + '" stroke-width="' + SW + '"/>';
        }
    }

    function nodeMarkup(type) {
        var fn = NODE_DRAW[type] || NODE_DRAW.Vessel;
        return fn();
    }

    function size(type) {
        return SIZES[type] || [80, 80];
    }

    function tagOffset(type) {
        var s = size(type);
        return s[1] / 2 + 14;
    }

    function legendItems() {
        var items = [];
        Object.keys(MEDIUM_STYLE).forEach(function (k) {
            items.push({ key: k, label: MEDIUM_LABEL[k], color: MEDIUM_STYLE[k].color, dash: MEDIUM_STYLE[k].dash });
        });
        return items;
    }

    global.PidSymbols = {
        sizes: SIZES,
        size: size,
        tagOffset: tagOffset,
        nodeMarkup: nodeMarkup,
        valveMarkup: valveMarkup,
        instrumentMarkup: instrumentBubble,
        mediumStyle: function (m) { return MEDIUM_STYLE[m] || MEDIUM_STYLE.Other; },
        mediumLabel: function (m) { return MEDIUM_LABEL[m] || m; },
        mediums: Object.keys(MEDIUM_STYLE),
        valveTypes: ['Gate', 'Ball', 'Butterfly', 'Check', 'Globe', 'Relief', 'Control', 'Solenoid'],
        instrumentTypes: ['PT', 'TT', 'FT', 'LT'],
        statusNames: NUM_TO_STATUS,
        symbolNames: NUM_TO_SYMBOL,
        mediumNames: NUM_TO_MEDIUM,
        valveNames: NUM_TO_VALVE,
        instrumentNames: NUM_TO_INSTR,
        legendItems: legendItems
    };
})(window);
