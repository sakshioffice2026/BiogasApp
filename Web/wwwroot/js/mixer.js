(function () {
    'use strict';

    var SVG_NS = 'http://www.w3.org/2000/svg';
    var BOWL_TOP = 50;
    var BOWL_BOTTOM = 180;
    var PREVIEW_DELAY = 350;

    var PALETTE = [
        ['#00C48C', '#10B5E8'],
        ['#FFB020', '#FF5E7E'],
        ['#10B5E8', '#5B5BF0'],
        ['#5B5BF0', '#FF5E7E'],
        ['#00C48C', '#5B5BF0'],
        ['#FFB020', '#00C48C']
    ];

    var form = document.querySelector('[data-mx-form]');
    if (!form) return;

    var picker = form.querySelector('[data-mx-picker]');
    var addButton = form.querySelector('[data-mx-add]');
    var rowsList = form.querySelector('[data-mx-rows]');
    var template = document.getElementById('mx-row-template');
    var layers = form.querySelector('[data-mx-layers]');
    var defs = form.querySelector('.mx-bowl__svg defs');
    var legend = form.querySelector('[data-mx-legend]');
    var emptyNote = form.querySelector('[data-mx-empty]');
    var summary = form.querySelector('[data-mx-summary]');
    var statusEl = form.querySelector('[data-mx-status]');
    var totalEl = form.querySelector('[data-mx-total]');
    var tsGauge = form.querySelector('[data-gauge="ring"]');
    var cnGauge = form.querySelector('[data-gauge="cn"]');
    var warningsBox = form.querySelector('[data-mx-warnings]');
    var warningList = form.querySelector('[data-mx-warning-list]');
    var targetInput = form.querySelector('[data-mx-target]');
    var targetRange = form.querySelector('[data-mx-target-range]');
    var targetNote = form.querySelector('[data-mx-target-note]');
    var runButton = form.querySelector('[data-mx-run]');
    var projectInput = form.querySelector('input[name="ProjectId"]');

    var library = readJson('mx-library') || [];
    var initialRows = readJson('mx-rows') || [];
    var byId = {};
    library.forEach(function (f) { byId[f.id] = f; });

    var uid = 0;
    var timer = null;
    var controller = null;

    function readJson(id) {
        var el = document.getElementById(id);
        if (!el) return null;
        try {
            return JSON.parse(el.textContent);
        } catch (e) {
            return null;
        }
    }

    function num(el) {
        if (!el || el.value === '') return null;
        var n = parseFloat(String(el.value).replace(',', '.'));
        return isNaN(n) ? null : n;
    }

    function round(value, digits) {
        var f = Math.pow(10, digits);
        return Math.round(value * f) / f;
    }

    function rowEls() {
        return Array.prototype.slice.call(rowsList.querySelectorAll('[data-mx-row]'));
    }

    function readRow(row) {
        return {
            feedstockId: row.querySelector('[data-mx-id]').value,
            tpd: num(row.querySelector('[data-mx-tpd]')) || 0,
            days: num(row.querySelector('[data-mx-days]')) || 365,
            ts: num(row.querySelector('[data-mx-ts]')),
            vs: num(row.querySelector('[data-mx-vs]'))
        };
    }

    function colors(index) {
        return PALETTE[index % PALETTE.length];
    }

    function reindex() {
        rowEls().forEach(function (row, i) {
            Array.prototype.forEach.call(row.querySelectorAll('[name]'), function (input) {
                input.name = input.name.replace(/Rows\[\d+\]/, 'Rows[' + i + ']');
            });
        });
    }

    function refreshPicker() {
        var used = {};
        rowEls().forEach(function (row) {
            used[row.querySelector('[data-mx-id]').value] = true;
        });
        Array.prototype.forEach.call(picker.options, function (opt) {
            if (opt.value) opt.disabled = !!used[opt.value];
        });
    }

    function addRow(data) {
        var feedstock = byId[data.feedstockId];
        if (!feedstock || !template) return null;

        var index = rowEls().length;
        uid += 1;
        var html = template.innerHTML
            .replace(/Rows\[__i__\]/g, 'Rows[' + index + ']')
            .replace(/__i__/g, 'u' + uid);

        var holder = document.createElement('ul');
        holder.innerHTML = html;
        var row = holder.firstElementChild;

        row.querySelector('[data-mx-id]').value = feedstock.id;
        row.querySelector('[data-mx-name]').textContent = feedstock.name;
        row.querySelector('[data-mx-cat]').textContent = feedstock.categoryLabel;

        var tpd = row.querySelector('[data-mx-tpd]');
        var days = row.querySelector('[data-mx-days]');
        var ts = row.querySelector('[data-mx-ts]');
        var vs = row.querySelector('[data-mx-vs]');

        tpd.value = data.tpd > 0 ? data.tpd : '';
        days.value = data.days > 0 ? data.days : 365;
        ts.value = data.ts === null || data.ts === undefined ? '' : data.ts;
        vs.value = data.vs === null || data.vs === undefined ? '' : data.vs;
        ts.placeholder = String(feedstock.ts);
        vs.placeholder = String(feedstock.vs);
        row.querySelector('[data-mx-ts-hint]').textContent = 'Library value ' + feedstock.ts + '%';
        row.querySelector('[data-mx-vs-hint]').textContent = 'Library value ' + feedstock.vs + '%';

        if (ts.value !== '' || vs.value !== '') {
            var details = row.querySelector('details');
            if (details) details.open = true;
        }

        rowsList.appendChild(row);
        refreshPicker();
        return row;
    }

    function removeRow(row) {
        row.parentNode.removeChild(row);
        reindex();
        refreshPicker();
        redraw();
        schedulePreview();
    }

    function activeRows() {
        return rowEls().map(readRow).filter(function (r) {
            return r.feedstockId && r.tpd > 0;
        });
    }

    function ensureGradient(index) {
        if (!defs) return null;
        var id = 'mx-grad-' + index;
        if (!document.getElementById(id)) {
            var c = colors(index);
            var g = document.createElementNS(SVG_NS, 'linearGradient');
            g.setAttribute('id', id);
            g.setAttribute('x1', '0');
            g.setAttribute('y1', '0');
            g.setAttribute('x2', '1');
            g.setAttribute('y2', '1');
            [['0', c[0]], ['1', c[1]]].forEach(function (s) {
                var stop = document.createElementNS(SVG_NS, 'stop');
                stop.setAttribute('offset', s[0]);
                stop.setAttribute('stop-color', s[1]);
                g.appendChild(stop);
            });
            defs.appendChild(g);
        }
        return id;
    }

    function redraw() {
        var rows = rowEls();
        var items = rows.map(function (row, i) {
            var data = readRow(row);
            data.index = i;
            data.row = row;
            data.feedstock = byId[data.feedstockId];
            return data;
        });

        var total = items.reduce(function (sum, r) { return sum + r.tpd; }, 0);

        items.forEach(function (item) {
            var c = colors(item.index);
            var swatch = item.row.querySelector('[data-mx-swatch]');
            if (swatch) swatch.style.background = 'linear-gradient(135deg,' + c[0] + ',' + c[1] + ')';
            var share = item.row.querySelector('[data-mx-share]');
            if (share) share.textContent = (total > 0 ? Math.round(item.tpd / total * 100) : 0) + '%';
        });

        while (layers.firstChild) layers.removeChild(layers.firstChild);
        while (legend.firstChild) legend.removeChild(legend.firstChild);

        var height = BOWL_BOTTOM - BOWL_TOP + 10;
        var y = BOWL_BOTTOM + 10;
        var parts = [];

        if (total > 0) {
            items.forEach(function (item) {
                if (item.tpd <= 0) return;
                var h = item.tpd / total * height;
                y -= h;

                var rect = document.createElementNS(SVG_NS, 'rect');
                rect.setAttribute('x', '20');
                rect.setAttribute('width', '200');
                rect.setAttribute('y', String(round(y, 2)));
                rect.setAttribute('height', String(round(h + 0.5, 2)));
                rect.setAttribute('fill', 'url(#' + ensureGradient(item.index) + ')');
                layers.appendChild(rect);

                var pct = Math.round(item.tpd / total * 100);
                var li = document.createElement('li');
                var dot = document.createElement('span');
                var c = colors(item.index);
                dot.className = 'mx-swatch';
                dot.setAttribute('aria-hidden', 'true');
                dot.style.background = 'linear-gradient(135deg,' + c[0] + ',' + c[1] + ')';
                var label = document.createElement('span');
                label.textContent = item.feedstock.name;
                var value = document.createElement('strong');
                value.textContent = pct + '% (' + round(item.tpd, 2) + ' TPD)';
                li.appendChild(dot);
                li.appendChild(label);
                li.appendChild(value);
                legend.appendChild(li);

                parts.push(item.feedstock.name + ' ' + pct + ' percent');
            });
        }

        if (emptyNote) emptyNote.hidden = total > 0;
        legend.hidden = total <= 0;
        if (summary) {
            summary.textContent = total > 0
                ? 'Blend of ' + round(total, 2) + ' tons per day: ' + parts.join(', ')
                : 'The bowl is empty.';
        }

        localEstimate(items, total);
    }

    function localEstimate(items, total) {
        if (total <= 0) {
            applyGauges(0, 0, 0, 'Green');
            return;
        }

        var ts = 0;
        var carbon = 0;
        var nitrogen = 0;

        items.forEach(function (item) {
            if (item.tpd <= 0 || !item.feedstock) return;
            var tsPct = item.ts !== null ? item.ts : item.feedstock.ts;
            var cn = item.feedstock.cn;
            var solids = item.tpd * tsPct / 100;
            ts += solids;
            carbon += solids * (cn / (cn + 1));
            nitrogen += solids * (1 / (cn + 1));
        });

        var blendedTs = ts / total * 100;
        var blendedCn = nitrogen > 0 ? carbon / nitrogen : 0;
        applyGauges(total, blendedTs, blendedCn, window.BpGauges ? window.BpGauges.zoneFor(blendedCn) : 'Green');
    }

    function applyGauges(totalTpd, tsPercent, cn, zone) {
        if (totalEl) totalEl.textContent = String(round(totalTpd, 2));
        if (window.BpGauges) {
            window.BpGauges.update(tsGauge, round(tsPercent, 2));
            window.BpGauges.update(cnGauge, round(cn, 2), zone);
        }
    }

    function showWarnings(list) {
        if (!warningsBox || !warningList) return;
        while (warningList.firstChild) warningList.removeChild(warningList.firstChild);

        (list || []).forEach(function (text) {
            var li = document.createElement('li');
            var icon = document.createElementNS(SVG_NS, 'svg');
            icon.setAttribute('class', 'bp-ico');
            icon.setAttribute('viewBox', '0 0 24 24');
            icon.setAttribute('aria-hidden', 'true');
            icon.setAttribute('focusable', 'false');
            var path = document.createElementNS(SVG_NS, 'path');
            path.setAttribute('d', 'M12 4 3 20h18L12 4zM12 10v4M12 17v.5');
            icon.appendChild(path);
            var span = document.createElement('span');
            span.textContent = text;
            li.appendChild(icon);
            li.appendChild(span);
            warningList.appendChild(li);
        });

        warningsBox.hidden = !list || list.length === 0;
    }

    function setStatus(text) {
        if (statusEl) statusEl.textContent = text || '';
    }

    function schedulePreview() {
        window.clearTimeout(timer);
        timer = window.setTimeout(runPreview, PREVIEW_DELAY);
    }

    function runPreview() {
        var url = form.getAttribute('data-preview-url');
        var rows = activeRows();

        if (!url) return;
        if (rows.length === 0) {
            showWarnings([]);
            setStatus('');
            return;
        }

        if (controller) controller.abort();
        controller = typeof AbortController === 'function' ? new AbortController() : null;

        var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
        var headers = { 'Content-Type': 'application/json' };
        if (tokenInput) headers.RequestVerificationToken = tokenInput.value;

        var payload = {
            projectId: projectInput ? projectInput.value : null,
            targetSlurryTsPercent: num(targetInput) || 9,
            rows: rows.map(function (r) {
                return {
                    feedstockId: r.feedstockId,
                    dailyTonnageTpd: r.tpd,
                    seasonalAvailabilityDays: r.days,
                    customTsOverride: r.ts,
                    customVsOverride: r.vs
                };
            })
        };

        setStatus('Updating gauges...');

        fetch(url, {
            method: 'POST',
            headers: headers,
            body: JSON.stringify(payload),
            credentials: 'same-origin',
            signal: controller ? controller.signal : undefined
        }).then(function (response) {
            if (!response.ok) throw new Error('Preview failed');
            return response.json();
        }).then(function (data) {
            applyGauges(data.totalFeedTpd, data.blendedTsPercent, data.blendedCnRatio, data.cnGaugeZone);
            showWarnings(data.warnings);
            setStatus('Gauges are up to date.');
        }).catch(function (err) {
            if (err && err.name === 'AbortError') return;
            setStatus('Live check is unavailable. Gauges show a local estimate; you can still run the simulation.');
        });
    }

    function syncTarget(source) {
        var value = num(source);
        if (value === null) return;
        if (source === targetInput && targetRange) targetRange.value = String(value);
        if (source === targetRange && targetInput) targetInput.value = String(value);
        if (targetNote) targetNote.textContent = 'Target ' + round(value, 1) + '%';
    }

    function onAdd() {
        if (!picker.value) {
            picker.focus();
            return;
        }
        var row = addRow({ feedstockId: picker.value, tpd: 0, days: 365, ts: null, vs: null });
        picker.value = '';
        if (row) {
            redraw();
            var field = row.querySelector('[data-mx-tpd]');
            if (field) field.focus();
        }
    }

    function onInput() {
        redraw();
        schedulePreview();
    }

    if (addButton) addButton.addEventListener('click', onAdd);

    picker.addEventListener('keydown', function (event) {
        if (event.key === 'Enter') {
            event.preventDefault();
            onAdd();
        }
    });

    rowsList.addEventListener('input', onInput);

    rowsList.addEventListener('click', function (event) {
        var button = event.target.closest ? event.target.closest('[data-mx-remove]') : null;
        if (!button) return;
        var row = button.closest('[data-mx-row]');
        if (row) {
            removeRow(row);
            picker.focus();
        }
    });

    if (targetInput) {
        targetInput.addEventListener('input', function () {
            syncTarget(targetInput);
            schedulePreview();
        });
    }

    if (targetRange) {
        targetRange.addEventListener('input', function () {
            syncTarget(targetRange);
            schedulePreview();
        });
    }

    form.addEventListener('submit', function (event) {
        reindex();
        if (activeRows().length === 0) {
            event.preventDefault();
            setStatus('Add at least one feedstock with a tonnage above zero.');
            var first = rowsList.querySelector('[data-mx-tpd]');
            if (first) {
                first.focus();
            } else {
                picker.focus();
            }
            return;
        }
        if (runButton) runButton.setAttribute('aria-busy', 'true');
    });

    initialRows.forEach(function (r) {
        addRow({
            feedstockId: r.feedstockId,
            tpd: r.tpd,
            days: r.days,
            ts: r.ts,
            vs: r.vs
        });
    });

    reindex();
    redraw();
    if (activeRows().length > 0) schedulePreview();
})();
