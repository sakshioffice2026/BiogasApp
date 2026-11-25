(function () {
    'use strict';

    var root = document.getElementById('pidEditor');
    var card = document.getElementById('vendorCard');
    if (!root || !card) return;

    var diagramId = root.getAttribute('data-diagram-id');
    var listUrl = card.getAttribute('data-datasheets-url');
    var saveUrl = card.getAttribute('data-save-url');
    var tokenInput = root.querySelector('input[name="__RequestVerificationToken"]');

    function $(id) { return document.getElementById(id); }

    var el = {
        tag: $('vendorTag'),
        make: $('vendorMake'),
        model: $('vendorModel'),
        material: $('vendorMaterial'),
        weight: $('vendorWeight'),
        price: $('vendorPrice'),
        currency: $('vendorCurrency'),
        delivery: $('vendorDelivery'),
        remarks: $('vendorRemarks'),
        save: $('btnVendorSave'),
        msg: $('vendorMsg')
    };

    var items = {};

    function esc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function showMessage(text, isError) {
        el.msg.textContent = text || '';
        el.msg.className = 'small mt-2 ' + (isError ? 'text-danger' : 'text-success');
    }

    function toNumber(value) {
        var v = (value || '').trim();
        if (v === '') return null;
        var n = Number(v);
        return isNaN(n) ? null : n;
    }

    function toInt(value) {
        var n = toNumber(value);
        return n === null ? null : Math.round(n);
    }

    function fill(tag) {
        var item = items[tag];
        var v = (item && item.vendor) || {};
        el.make.value = v.make || '';
        el.model.value = v.model || '';
        el.material.value = v.material || '';
        el.weight.value = v.weightKg == null ? '' : v.weightKg;
        el.price.value = v.price == null ? '' : v.price;
        el.currency.value = v.currency || '';
        el.delivery.value = v.deliveryWeeks == null ? '' : v.deliveryWeeks;
        el.remarks.value = v.remarks || '';
        showMessage('', false);
    }

    function load() {
        fetch(listUrl + '?id=' + encodeURIComponent(diagramId), { credentials: 'same-origin' })
            .then(function (r) {
                if (!r.ok) throw new Error('Could not load equipment.');
                return r.json();
            })
            .then(function (set) {
                var list = (set && set.items) || [];
                items = {};
                el.tag.innerHTML = list.map(function (i) {
                    items[i.tag] = i;
                    return '<option value="' + esc(i.tag) + '">' + esc(i.tag) + ' - ' + esc(i.name) + '</option>';
                }).join('');

                if (list.length) fill(list[0].tag);
                else showMessage('No equipment on this drawing.', true);
            })
            .catch(function (err) {
                showMessage(err.message, true);
            });
    }

    function save() {
        var tag = el.tag.value;
        if (!tag) return;

        var payload = {
            tag: tag,
            make: el.make.value,
            model: el.model.value,
            material: el.material.value,
            weightKg: toNumber(el.weight.value),
            price: toNumber(el.price.value),
            currency: el.currency.value,
            deliveryWeeks: toInt(el.delivery.value),
            remarks: el.remarks.value
        };

        var headers = { 'Content-Type': 'application/json' };
        if (tokenInput) headers.RequestVerificationToken = tokenInput.value;

        el.save.disabled = true;
        fetch(saveUrl + '?id=' + encodeURIComponent(diagramId), {
            method: 'POST',
            headers: headers,
            credentials: 'same-origin',
            body: JSON.stringify(payload)
        })
            .then(function (r) {
                if (!r.ok) throw new Error('Could not save vendor data.');
                if (items[tag]) items[tag].vendor = payload;
                showMessage('Vendor data saved for ' + tag + '.', false);
            })
            .catch(function (err) {
                showMessage(err.message, true);
            })
            .then(function () {
                el.save.disabled = false;
            });
    }

    el.tag.addEventListener('change', function () { fill(el.tag.value); });
    var dsLink = $('btnDatasheetPage');
    if (dsLink) {
        var dsBase = dsLink.getAttribute('href');
        var dsSync = function () {
            var t = el.tag.value;
            dsLink.setAttribute('href', t ? dsBase + (dsBase.indexOf('?') < 0 ? '?' : '&') + 'tag=' + encodeURIComponent(t) : dsBase);
        };
        el.tag.addEventListener('change', dsSync);
        dsLink.addEventListener('click', dsSync);
    }
    el.save.addEventListener('click', save);

    load();
})();
