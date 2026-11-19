(function () {
    'use strict';

    var CN_MIN = 20;
    var CN_MAX = 30;

    function qs(selector, root) {
        return (root || document).querySelector(selector);
    }

    function toNumber(el) {
        if (!el) return null;
        var value = parseFloat(String(el.value).replace(',', '.'));
        return isNaN(value) ? null : value;
    }

    function format(value, digits) {
        return value.toLocaleString(undefined, {
            minimumFractionDigits: 0,
            maximumFractionDigits: digits
        });
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

    function initDeleteDialog() {
        var dialog = qs('[data-fs-dialog]');
        var buttons = document.querySelectorAll('[data-fs-delete]');
        if (!dialog || !buttons.length) return;

        var idInput = qs('[data-fs-dialog-id]', dialog);
        var nameOutput = qs('[data-fs-dialog-name]', dialog);
        var cancel = qs('[data-fs-dialog-cancel]', dialog);
        var opener = null;

        Array.prototype.forEach.call(buttons, function (button) {
            button.addEventListener('click', function () {
                var name = button.getAttribute('data-name') || '';
                var id = button.getAttribute('data-id') || '';

                if (typeof dialog.showModal !== 'function') {
                    if (window.confirm('Delete ' + name + '? This cannot be undone.')) {
                        idInput.value = id;
                        dialog.querySelector('form').submit();
                    }
                    return;
                }

                opener = button;
                idInput.value = id;
                if (nameOutput) nameOutput.textContent = name;
                dialog.showModal();
            });
        });

        if (cancel) {
            cancel.addEventListener('click', function () {
                dialog.close();
            });
        }

        dialog.addEventListener('close', function () {
            if (opener) opener.focus();
        });

        dialog.addEventListener('click', function (event) {
            if (event.target === dialog) dialog.close();
        });
    }

    function initPreview() {
        var ts = qs('[data-fs-ts]');
        var vs = qs('[data-fs-vs]');
        var bmp = qs('[data-fs-bmp]');
        var cn = qs('[data-fs-cn]');
        var cost = qs('[data-fs-cost]');
        var density = qs('[data-fs-density]');
        var yieldOut = qs('[data-fs-yield]');
        if (!ts || !vs || !bmp || !yieldOut) return;

        var vsKg = qs('[data-fs-vs-kg]');
        var costM3 = qs('[data-fs-cost-m3]');
        var cnNote = qs('[data-fs-cn-note]');
        var vsNote = qs('[data-fs-vs-note]');

        function update() {
            var tsVal = toNumber(ts);
            var vsVal = toNumber(vs);
            var bmpVal = toNumber(bmp);
            var costVal = toNumber(cost);

            var ready = tsVal !== null && vsVal !== null && bmpVal !== null;
            var vsPerTon = ready ? tsVal * vsVal / 100 * 10 : null;
            var gas = ready ? vsPerTon * bmpVal : null;

            yieldOut.textContent = gas === null ? '\u2014' : format(gas, 1);
            if (vsKg) vsKg.textContent = vsPerTon === null ? '\u2014' : format(vsPerTon, 1) + ' kg';

            if (costM3) {
                costM3.textContent = (gas && gas > 0 && costVal !== null)
                    ? format(costVal / gas, 2)
                    : '\u2014';
            }

            if (cnNote && cn) {
                var cnVal = toNumber(cn);
                if (cnVal === null || cnVal <= 0) {
                    cnNote.textContent = 'Ideal C:N for digestion is about ' + CN_MIN + ' to ' + CN_MAX + '.';
                    cnNote.removeAttribute('data-state');
                } else if (cnVal < CN_MIN) {
                    cnNote.textContent = 'Low C:N. Risk of ammonia build-up; blend with a carbon-rich feedstock.';
                    cnNote.setAttribute('data-state', 'warn');
                } else if (cnVal > CN_MAX) {
                    cnNote.textContent = 'High C:N. Nitrogen may limit digestion; blend with manure.';
                    cnNote.setAttribute('data-state', 'warn');
                } else {
                    cnNote.textContent = 'Within the ideal C:N range of ' + CN_MIN + ' to ' + CN_MAX + '.';
                    cnNote.setAttribute('data-state', 'ok');
                }
            }

            if (vsNote && tsVal !== null && vsVal !== null) {
                if (tsVal > 0 && vsVal === 0) {
                    vsNote.textContent = 'VS is 0, so this feedstock will yield no gas.';
                    vsNote.setAttribute('data-state', 'warn');
                } else {
                    vsNote.textContent = '';
                    vsNote.removeAttribute('data-state');
                }
            }

            if (density && density.value !== '' && toNumber(density) !== null) {
                var densityOut = qs('[data-fs-density-out]');
                if (densityOut) densityOut.textContent = format(toNumber(density), 1) + ' kg/m\u00B3';
            }
        }

        [ts, vs, bmp, cn, cost, density].forEach(function (el) {
            if (el) el.addEventListener('input', update);
        });
        update();
    }

    document.addEventListener('DOMContentLoaded', function () {
        initFilters();
        initDeleteDialog();
        initPreview();
    });
})();
