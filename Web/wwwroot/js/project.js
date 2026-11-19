(function () {
    'use strict';

    var TEMP_MIN = -50;
    var TEMP_MAX = 60;
    var MIN_AREA = 2500;

    function qs(selector) {
        return document.querySelector(selector);
    }

    function toNumber(el) {
        var value = parseFloat(el && el.value);
        return isNaN(value) ? null : value;
    }

    function initFilters() {
        var forms = document.querySelectorAll('form[data-autosubmit]');
        Array.prototype.forEach.call(forms, function (form) {
            form.addEventListener('change', function (event) {
                if (event.target && event.target.type === 'radio') {
                    if (form.requestSubmit) {
                        form.requestSubmit();
                    } else {
                        form.submit();
                    }
                }
            });
        });
    }

    function initPlotArea() {
        var length = qs('[data-plot-length]');
        var width = qs('[data-plot-width]');
        var output = qs('[data-plot-output]');
        if (!length || !width || !output) return;

        var value = output.querySelector('[data-plot-value]');
        var note = output.querySelector('[data-plot-note]');

        function update() {
            var area = (toNumber(length) || 0) * (toNumber(width) || 0);
            var ok = area >= MIN_AREA;

            output.setAttribute('data-ok', ok ? 'true' : 'false');
            if (value) value.textContent = Math.round(area).toLocaleString() + ' m\u00B2';
            if (note) {
                note.textContent = ok
                    ? 'Meets the 2,500 m\u00B2 minimum'
                    : 'Needs at least 2,500 m\u00B2';
            }
        }

        length.addEventListener('input', update);
        width.addEventListener('input', update);
        update();
    }

    function initTemperature() {
        var min = qs('[data-temp-min]');
        var max = qs('[data-temp-max]');
        var range = qs('[data-temp-range]');
        var output = qs('[data-temp-output]');
        if (!min || !max || !range) return;

        function percent(value) {
            var p = (value - TEMP_MIN) / (TEMP_MAX - TEMP_MIN) * 100;
            return Math.min(100, Math.max(0, p));
        }

        function update() {
            var low = toNumber(min);
            var high = toNumber(max);
            if (low === null || high === null) return;

            range.style.setProperty('--pj-from', percent(low) + '%');
            range.style.setProperty('--pj-to', percent(high) + '%');

            if (output) {
                output.textContent = low < high
                    ? 'Seasonal swing of ' + (high - low) + ' \u00B0C'
                    : 'Minimum must be lower than the maximum';
            }
        }

        min.addEventListener('input', update);
        max.addEventListener('input', update);
        update();
    }

    document.addEventListener('DOMContentLoaded', function () {
        initFilters();
        initPlotArea();
        initTemperature();
    });
})();
