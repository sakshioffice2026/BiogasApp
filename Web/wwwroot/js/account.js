(function () {
    'use strict';

    var LABELS = ['Not set', 'Weak', 'Fair', 'Good', 'Strong'];

    function qs(selector, root) {
        return (root || document).querySelector(selector);
    }

    function qsa(selector, root) {
        return Array.prototype.slice.call((root || document).querySelectorAll(selector));
    }

    function initToggles() {
        qsa('[data-ac-toggle]').forEach(function (button) {
            var input = document.getElementById(button.getAttribute('aria-controls'));
            var text = qs('[data-ac-toggle-text]', button);
            if (!input) return;

            button.addEventListener('click', function () {
                var show = input.type === 'password';
                input.type = show ? 'text' : 'password';
                button.setAttribute('aria-pressed', show ? 'true' : 'false');
                if (text) text.textContent = show ? 'Hide' : 'Show';
                input.focus();
            });
        });
    }

    function checkRules(value) {
        return {
            length: value.length >= 8,
            'case': /[a-z]/.test(value) && /[A-Z]/.test(value),
            number: /\d/.test(value),
            symbol: /[^A-Za-z0-9]/.test(value)
        };
    }

    function initMeter() {
        var input = qs('[data-ac-password]');
        var meter = qs('[data-ac-meter]');
        if (!input || !meter) return;

        var strength = qs('[data-ac-strength]', meter);
        var rules = qsa('[data-ac-rule]');

        function update() {
            var value = input.value;
            var result = checkRules(value);
            var met = 0;

            rules.forEach(function (rule) {
                var ok = !!result[rule.getAttribute('data-ac-rule')];
                var state = qs('[data-ac-rule-state]', rule);
                rule.setAttribute('data-met', ok ? 'true' : 'false');
                if (state) state.textContent = ok ? 'met' : 'not met';
                if (ok) met += 1;
            });

            var level = value.length === 0 ? 0 : Math.max(1, met);
            if (!result.length) level = Math.min(level, 1);

            meter.setAttribute('data-level', String(level));
            if (strength) strength.textContent = LABELS[level];
        }

        input.addEventListener('input', update);
        update();
    }

    function initMatch() {
        var password = qs('[data-ac-password]');
        var confirm = qs('[data-ac-confirm]');
        var note = qs('[data-ac-match]');
        if (!password || !confirm || !note) return;

        function update() {
            if (confirm.value === '') {
                note.textContent = '';
                note.removeAttribute('data-state');
                return;
            }

            var same = confirm.value === password.value;
            note.textContent = same ? '\u2713 Passwords match' : '\u2715 Passwords do not match yet';
            note.setAttribute('data-state', same ? 'ok' : 'warn');
        }

        password.addEventListener('input', update);
        confirm.addEventListener('input', update);
        update();
    }

    function initSubmit() {
        qsa('[data-ac-form]').forEach(function (form) {
            var button = qs('[data-ac-submit]', form);
            var text = button ? qs('[data-ac-submit-text]', button) : null;
            if (!button) return;

            var original = text ? text.textContent : '';

            function reset() {
                button.removeAttribute('aria-busy');
                button.disabled = false;
                if (text) text.textContent = original;
            }

            form.addEventListener('submit', function (event) {
                window.setTimeout(function () {
                    if (event.defaultPrevented) return;
                    button.setAttribute('aria-busy', 'true');
                    button.disabled = true;
                    if (text) text.textContent = 'Please wait...';
                }, 0);
            });

            window.addEventListener('pageshow', reset);
        });
    }

    function focusFirstError() {
        var invalid = qs('.input-validation-error');
        if (invalid) invalid.focus();
    }

    document.addEventListener('DOMContentLoaded', function () {
        initToggles();
        initMeter();
        initMatch();
        initSubmit();
        focusFirstError();
    });
})();
