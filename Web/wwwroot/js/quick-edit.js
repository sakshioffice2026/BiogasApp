(function () {
    'use strict';

    var root = document.querySelector('[data-qe]');
    var form = document.querySelector('form[data-pj-wizard]');
    var statusForm = document.querySelector('[data-qe-status-form]');
    if (!root || !form) { return; }

    var chips = root.querySelectorAll('.qe-chip');
    var editor = root.querySelector('[data-qe-editor]');
    var host = root.querySelector('[data-qe-host]');
    var label = root.querySelector('[data-qe-editor-label]');
    var applyBtn = root.querySelector('[data-qe-apply]');
    var saveWrap = root.querySelector('[data-qe-save-wrap]');
    var saveBtn = root.querySelector('[data-qe-save]');
    var titleOut = document.querySelector('[data-qe-title]');

    var activeChip = null;
    var control = null;

    function parseOptions(text) {
        return (text || '').split('|').filter(Boolean).map(function (part) {
            var bits = part.split(':');
            return { value: bits[0], label: bits[1], css: bits[2] || '' };
        });
    }

    function isInstant(chip) {
        return !!statusForm && chip.dataset.qeInstant === 'true';
    }

    function formField(name) {
        return form.querySelector('[name="' + name + '"]');
    }

    function fire(el, type) {
        el.dispatchEvent(new Event(type, { bubbles: true }));
    }

    function closeEditor(returnFocus) {
        editor.hidden = true;
        host.textContent = '';
        control = null;
        if (activeChip) {
            activeChip.classList.remove('is-open');
            activeChip.setAttribute('aria-expanded', 'false');
            if (returnFocus) { activeChip.focus(); }
        }
        activeChip = null;
    }

    function buildControl(chip) {
        var type = chip.dataset.qeType;
        var current = chip.dataset.qeValue || '';
        var el;

        if (type === 'text') {
            el = document.createElement('input');
            el.type = 'text';
            el.value = current;
            var source = formField(chip.dataset.qeField);
            if (source && source.maxLength > 0) { el.maxLength = source.maxLength; }
        } else {
            el = document.createElement('select');
            parseOptions(chip.dataset.qeOptions).forEach(function (o) {
                var opt = document.createElement('option');
                opt.value = o.value;
                opt.textContent = o.label;
                opt.selected = o.value === current;
                el.appendChild(opt);
            });
        }

        el.id = 'qe-input';
        el.className = type === 'text' ? 'form-control' : 'form-select';
        return el;
    }

    function openEditor(chip) {
        if (activeChip === chip) { closeEditor(true); return; }
        closeEditor(false);

        activeChip = chip;
        chip.classList.add('is-open');
        chip.setAttribute('aria-expanded', 'true');
        label.textContent = chip.dataset.qeLabel;
        applyBtn.textContent = isInstant(chip) ? 'Apply and save' : 'Apply';

        control = buildControl(chip);
        host.appendChild(control);
        editor.hidden = false;
        control.focus();
        if (control.type === 'text') { control.select(); }
    }

    function writeToForm(chip, value) {
        var name = chip.dataset.qeField;

        if (chip.dataset.qeType === 'radio') {
            var radios = form.querySelectorAll('input[type="radio"][name="' + name + '"]');
            radios.forEach(function (r) {
                r.checked = r.value === value;
                if (r.checked) { fire(r, 'change'); }
            });
            return;
        }

        var field = formField(name);
        if (!field) { return; }
        field.value = value;
        fire(field, 'input');
        fire(field, 'change');
    }

    function updateChip(chip, value) {
        var out = chip.querySelector('[data-qe-out]');
        var text = value;

        if (chip.dataset.qeType !== 'text') {
            var match = parseOptions(chip.dataset.qeOptions).filter(function (o) { return o.value === value; })[0];
            if (match) {
                text = match.label;
                var badge = chip.querySelector('[data-qe-badge]');
                if (badge && match.css) {
                    badge.className = 'bp-chip bp-chip--' + match.css;
                }
            }
        }

        if (out) { out.textContent = text; }
        chip.dataset.qeValue = value;

        if (chip.dataset.qeField === 'Name' && titleOut) { titleOut.textContent = value; }
    }

    function apply() {
        if (!activeChip || !control) { return; }

        var value = control.value.trim();
        if (!value) { control.focus(); return; }

        // Status: save straight away with its own small form; no need to fill the main form.
        if (isInstant(activeChip)) {
            if (value === activeChip.dataset.qeValue) { closeEditor(true); return; }
            statusForm.querySelector('[name="status"]').value = value;
            statusForm.submit();
            return;
        }

        writeToForm(activeChip, value);
        updateChip(activeChip, value);
        if (saveWrap) { saveWrap.hidden = false; }
        closeEditor(true);
    }

    chips.forEach(function (chip) {
        chip.addEventListener('click', function () { openEditor(chip); });
    });

    applyBtn.addEventListener('click', apply);
    root.querySelector('[data-qe-cancel]').addEventListener('click', function () { closeEditor(true); });

    if (saveBtn) {
        saveBtn.addEventListener('click', function () {
            if (form.requestSubmit) { form.requestSubmit(); } else { form.submit(); }
        });
    }

    root.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && activeChip) {
            e.preventDefault();
            closeEditor(true);
        } else if (e.key === 'Enter' && control && e.target === control) {
            e.preventDefault();
            apply();
        }
    });
})();