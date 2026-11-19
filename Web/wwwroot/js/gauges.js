(function () {
    'use strict';

    var CN_SCALE_MAX = 50;
    var CN_OPTIMAL_MIN = 20;
    var CN_OPTIMAL_MAX = 30;
    var CN_CRITICAL_MIN = 15;
    var CN_CRITICAL_MAX = 35;

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function round(value, digits) {
        var factor = Math.pow(10, digits);
        return Math.round(value * factor) / factor;
    }

    function zoneFor(cn) {
        if (cn >= CN_OPTIMAL_MIN && cn <= CN_OPTIMAL_MAX) return 'Green';
        if (cn >= CN_CRITICAL_MIN && cn <= CN_CRITICAL_MAX) return 'Yellow';
        return 'Red';
    }

    function toNumber(value) {
        var n = parseFloat(value);
        return isNaN(n) ? 0 : n;
    }

    function setText(el, selector, text) {
        var target = el.querySelector(selector);
        if (target) target.textContent = text;
    }

    function updateRing(el, value) {
        var max = toNumber(el.getAttribute('data-max')) || 100;
        var unit = el.getAttribute('data-unit') || '';
        el.style.setProperty('--bp-val', round(clamp(value / max * 100, 0, 100), 1));
        el.setAttribute('data-value', String(value));
        setText(el, '[data-gauge-value]', round(value, 1) + unit);
    }

    function updateCn(el, value, zone) {
        el.style.setProperty('--bp-pos', round(clamp(value / CN_SCALE_MAX * 100, 0, 100), 1));
        el.setAttribute('data-value', String(value));
        el.setAttribute('data-zone', zone || zoneFor(value));
        setText(el, '[data-gauge-value]', String(round(value, 2)));
    }

    function update(el, value, zone) {
        if (!el) return;
        var number = toNumber(value);
        var type = el.getAttribute('data-gauge');
        if (type === 'ring') {
            updateRing(el, number);
        } else if (type === 'cn') {
            updateCn(el, number, zone);
        }
    }

    function init(root) {
        var scope = root || document;
        var gauges = scope.querySelectorAll('[data-gauge]');
        Array.prototype.forEach.call(gauges, function (el) {
            update(el, el.getAttribute('data-value'), el.getAttribute('data-zone'));
        });
    }

    window.BpGauges = {
        init: init,
        update: update,
        zoneFor: zoneFor
    };

    document.addEventListener('DOMContentLoaded', function () {
        init();
    });
})();
