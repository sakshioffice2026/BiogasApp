(function () {
    'use strict';

    var root = document.getElementById('siteEdit');
    if (!root || typeof L === 'undefined') return;

    var URLS = {
        save: root.getAttribute('data-save-url'),
        receptors: root.getAttribute('data-receptors-url'),
        importDxf: root.getAttribute('data-import-url')
    };
    var PROJECT_ID = root.getAttribute('data-project-id');
    var tokenInput = root.querySelector('input[name="__RequestVerificationToken"]');

    var EARTH_R = 6371008.8;
    var D2R = Math.PI / 180;
    var DEFAULT_CENTER = [20.5937, 78.9629];
    var RECEPTOR_RADIUS_M = 500;

    var ZONE_REASONS = [
        ['HighTensionLine', 'High tension line'],
        ['Canal', 'Canal'],
        ['Pipeline', 'Pipeline'],
        ['Easement', 'Easement'],
        ['RoadSetback', 'Road setback'],
        ['Other', 'Other']
    ];

    var RECEPTOR_TYPES = [
        ['School', 'School'],
        ['Hospital', 'Hospital'],
        ['House', 'House'],
        ['Road', 'Road'],
        ['Canal', 'Canal'],
        ['PowerLine', 'Power line'],
        ['Other', 'Other']
    ];

    function $(id) { return document.getElementById(id); }

    var el = {
        lat: $('siteLat'),
        lon: $('siteLon'),
        go: $('btnGoLocation'),
        layerSat: $('btnLayerSat'),
        layerOsm: $('btnLayerOsm'),
        tabs: $('inputModeTabs'),
        cornersText: $('cornersText'),
        rectLength: $('rectLength'),
        rectWidth: $('rectWidth'),
        undo: $('btnUndoPoint'),
        clear: $('btnClearPlot'),
        dxfFile: $('dxfFile'),
        importDxf: $('btnImportDxf'),
        area: $('plotAreaText'),
        gateEdge: $('gateEdge'),
        gateOffset: $('gateOffset'),
        northAngle: $('northAngle'),
        windFrom: $('windFrom'),
        slopeDir: $('slopeDir'),
        powerSide: $('powerSide'),
        gasSide: $('gasSide'),
        notes: $('siteNotes'),
        message: $('siteMessage'),
        noBuildList: $('noBuildList'),
        addNoBuild: $('btnAddNoBuild'),
        receptorBody: $('receptorBody'),
        findReceptors: $('btnFindReceptors'),
        addReceptor: $('btnAddReceptor'),
        save: $('btnSaveSite')
    };

    var state = {
        mode: 'Map',
        lat: null,
        lon: null,
        corners: [],
        closed: false,
        zones: [],
        receptors: [],
        draw: null
    };

    /* ---------- helpers ---------- */

    function esc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function num(input) {
        if (!input) return null;
        var v = parseFloat(input.value);
        return isFinite(v) ? v : null;
    }

    function round(v, d) {
        var f = Math.pow(10, d);
        return Math.round(v * f) / f;
    }

    function hasOrigin() {
        return state.lat !== null && state.lon !== null;
    }

    function toMetres(lat, lon) {
        var cl = Math.cos(state.lat * D2R);
        return {
            x: (lon - state.lon) * D2R * EARTH_R * cl,
            y: (lat - state.lat) * D2R * EARTH_R
        };
    }

    function toLatLon(x, y) {
        var cl = Math.cos(state.lat * D2R);
        return {
            lat: state.lat + y / EARTH_R / D2R,
            lon: state.lon + x / (EARTH_R * cl) / D2R
        };
    }

    function haversine(lat1, lon1, lat2, lon2) {
        var dLat = (lat2 - lat1) * D2R;
        var dLon = (lon2 - lon1) * D2R;
        var a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
            Math.cos(lat1 * D2R) * Math.cos(lat2 * D2R) * Math.sin(dLon / 2) * Math.sin(dLon / 2);
        return 2 * EARTH_R * Math.asin(Math.min(1, Math.sqrt(a)));
    }

    function polygonArea(pts) {
        if (pts.length < 3) return 0;
        var s = 0;
        for (var i = 0; i < pts.length; i++) {
            var a = pts[i];
            var b = pts[(i + 1) % pts.length];
            s += a.x * b.y - b.x * a.y;
        }
        return Math.abs(s) / 2;
    }

    function parsePoints(text) {
        var pts = [];
        var bad = 0;
        String(text || '').split(/\r?\n/).forEach(function (line) {
            line = line.trim();
            if (!line) return;
            var parts = line.split(/[\s,;]+/).filter(Boolean);
            var x = parseFloat(parts[0]);
            var y = parseFloat(parts[1]);
            if (parts.length >= 2 && isFinite(x) && isFinite(y)) pts.push({ x: x, y: y });
            else bad++;
        });
        return { points: pts, bad: bad };
    }

    function formatPoints(pts) {
        return pts.map(function (p) { return round(p.x, 2) + ', ' + round(p.y, 2); }).join('\n');
    }

    function showMessage(lines, type) {
        var list = Array.isArray(lines) ? lines : [lines];
        el.message.className = 'alert alert-' + (type || 'info') + ' py-2 small';
        el.message.innerHTML = list.map(esc).join('<br>');
    }

    function clearMessage() {
        el.message.className = 'd-none';
        el.message.innerHTML = '';
    }

    function request(url, opts) {
        return fetch(url, opts).then(function (r) {
            return r.text().then(function (t) {
                var data = null;
                try { data = t ? JSON.parse(t) : null; } catch (e) { data = null; }
                return { ok: r.ok, status: r.status, data: data };
            });
        });
    }

    function authHeaders(json) {
        var h = {};
        if (json) h['Content-Type'] = 'application/json';
        if (tokenInput) h.RequestVerificationToken = tokenInput.value;
        return h;
    }

    function failText(res, fallback) {
        if (res && res.data && res.data.message) return res.data.message;
        if (res && res.data && res.data.errors && res.data.errors.length) return res.data.errors;
        return fallback + ' (' + (res ? res.status : 'no response') + ').';
    }

    /* ---------- map ---------- */

    var map = L.map('siteMap').setView(DEFAULT_CENTER, 5);

    var satLayer = L.tileLayer(
        'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
        { maxZoom: 19, attribution: 'Tiles &copy; Esri' });
    var osmLayer = L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png',
        { maxZoom: 19, attribution: '&copy; OpenStreetMap contributors' });
    satLayer.addTo(map);

    var ringLayer = L.layerGroup().addTo(map);
    var plotLayer = L.layerGroup().addTo(map);
    var zoneLayer = L.layerGroup().addTo(map);
    var receptorLayer = L.layerGroup().addTo(map);
    var originMarker = null;

    function setLayer(sat) {
        if (sat) {
            if (map.hasLayer(osmLayer)) map.removeLayer(osmLayer);
            if (!map.hasLayer(satLayer)) satLayer.addTo(map);
        } else {
            if (map.hasLayer(satLayer)) map.removeLayer(satLayer);
            if (!map.hasLayer(osmLayer)) osmLayer.addTo(map);
        }
        el.layerSat.classList.toggle('active', sat);
        el.layerOsm.classList.toggle('active', !sat);
    }

    function setOrigin(lat, lon, pan) {
        state.lat = lat;
        state.lon = lon;
        el.lat.value = lat.toFixed(6);
        el.lon.value = lon.toFixed(6);

        if (!originMarker) {
            originMarker = L.marker([lat, lon], { draggable: true, title: 'Plot location' }).addTo(map);
            originMarker.on('dragend', function () {
                var p = originMarker.getLatLng();
                setOrigin(p.lat, p.lng, false);
            });
        } else {
            originMarker.setLatLng([lat, lon]);
        }

        ringLayer.clearLayers();
        L.circle([lat, lon], { radius: 100, color: '#f2a900', weight: 1, dashArray: '4 4', fill: false, interactive: false })
            .bindTooltip('100 m (PESO surroundings)').addTo(ringLayer);
        L.circle([lat, lon], { radius: 500, color: '#6c757d', weight: 1, dashArray: '4 4', fill: false, interactive: false })
            .bindTooltip('500 m (odour)').addTo(ringLayer);

        if (pan) map.setView([lat, lon], Math.max(map.getZoom(), 17));

        syncAllZones();
        renderAll();
    }

    /* ---------- plot ---------- */

    function plotMetres() {
        if (state.mode === 'Map') {
            if (!hasOrigin()) return [];
            return state.corners.map(function (c) { return toMetres(c.lat, c.lon); });
        }
        if (state.mode === 'Typed') return parsePoints(el.cornersText.value).points;

        var l = num(el.rectLength);
        var w = num(el.rectWidth);
        if (l > 0 && w > 0) return [{ x: 0, y: 0 }, { x: l, y: 0 }, { x: l, y: w }, { x: 0, y: w }];
        return [];
    }

    function plotLatLon() {
        if (state.mode === 'Map') return state.corners;
        if (!hasOrigin()) return [];
        return plotMetres().map(function (p) { return toLatLon(p.x, p.y); });
    }

    function renderPlot() {
        plotLayer.clearLayers();
        var ll = plotLatLon();
        var pts = ll.map(function (c) { return [c.lat, c.lon]; });
        var closed = state.mode !== 'Map' || state.closed;

        if (pts.length >= 3 && closed) {
            L.polygon(pts, { color: '#d32f2f', weight: 2, fillOpacity: 0.12, interactive: false }).addTo(plotLayer);
        } else if (pts.length >= 2) {
            L.polyline(pts, { color: '#d32f2f', weight: 2, dashArray: '4 4', interactive: false }).addTo(plotLayer);
        }

        pts.forEach(function (p, i) {
            L.circleMarker(p, {
                radius: i === 0 ? 6 : 4,
                color: '#d32f2f',
                fillColor: i === 0 ? '#ffffff' : '#d32f2f',
                fillOpacity: 1,
                weight: 2,
                interactive: false
            }).addTo(plotLayer);
        });

        el.area.textContent = round(polygonArea(plotMetres()), 0).toLocaleString();
    }

    function addCorner(latlng) {
        if (state.closed) return;

        if (state.corners.length >= 3) {
            var first = state.corners[0];
            var a = map.latLngToContainerPoint([first.lat, first.lon]);
            var b = map.latLngToContainerPoint(latlng);
            if (a.distanceTo(b) < 12) {
                state.closed = true;
                renderPlot();
                return;
            }
        }

        state.corners.push({ lat: latlng.lat, lon: latlng.lng });
        renderPlot();
    }

    /* ---------- no-build zones ---------- */

    function syncZone(z) {
        if (z.ll && hasOrigin()) {
            z.points = z.ll.map(function (c) { return toMetres(c.lat, c.lon); });
        }
    }

    function syncAllZones() {
        state.zones.forEach(syncZone);
    }

    function zoneLatLon(z) {
        if (z.ll) return z.ll;
        if (!hasOrigin()) return [];
        return z.points.map(function (p) { return toLatLon(p.x, p.y); });
    }

    function renderZoneMap() {
        zoneLayer.clearLayers();
        state.zones.forEach(function (z) {
            var pts = zoneLatLon(z).map(function (c) { return [c.lat, c.lon]; });
            if (pts.length >= 3) {
                L.polygon(pts, { color: '#9c27b0', weight: 2, dashArray: '5 4', fillOpacity: 0.15, interactive: false }).addTo(zoneLayer);
            } else if (pts.length >= 2) {
                L.polyline(pts, { color: '#9c27b0', weight: 2, dashArray: '5 4', interactive: false }).addTo(zoneLayer);
            }
            pts.forEach(function (p) {
                L.circleMarker(p, { radius: 3, color: '#9c27b0', fillColor: '#9c27b0', fillOpacity: 1, weight: 1, interactive: false }).addTo(zoneLayer);
            });
        });
    }

    function optionsHtml(list, selected) {
        return list.map(function (o) {
            return '<option value="' + o[0] + '"' + (o[0] === selected ? ' selected' : '') + '>' + esc(o[1]) + '</option>';
        }).join('');
    }

    function renderZoneList() {
        if (!state.zones.length) {
            el.noBuildList.innerHTML = '<div class="nobuild-empty">No no-build zones added.</div>';
            return;
        }

        el.noBuildList.innerHTML = state.zones.map(function (z, i) {
            var drawing = state.draw && state.draw.type === 'zone' && state.draw.zone === z;
            return '<div class="nobuild-row" data-idx="' + i + '">' +
                '<div class="row g-2 align-items-start">' +
                '<div class="col-md-3"><select class="form-select form-select-sm nb-reason">' + optionsHtml(ZONE_REASONS, z.reason) + '</select></div>' +
                '<div class="col-md-4"><textarea class="form-control form-control-sm font-monospace nb-points" rows="3" placeholder="x, y in metres, one corner per line">' + esc(formatPoints(z.points)) + '</textarea></div>' +
                '<div class="col-md-3"><input class="form-control form-control-sm nb-notes" placeholder="Notes" value="' + esc(z.notes || '') + '" /></div>' +
                '<div class="col-md-2 d-flex gap-1">' +
                '<button type="button" class="btn btn-sm btn-outline-primary nb-draw' + (drawing ? ' is-active' : '') + '">' + (drawing ? 'Done' : 'Draw') + '</button>' +
                '<button type="button" class="btn btn-sm btn-outline-danger nb-remove">Remove</button>' +
                '</div></div></div>';
        }).join('');
    }

    function updateZoneText(z) {
        var idx = state.zones.indexOf(z);
        var ta = el.noBuildList.querySelector('.nobuild-row[data-idx="' + idx + '"] .nb-points');
        if (ta) ta.value = formatPoints(z.points);
    }

    function zoneFromRow(target) {
        var row = target.closest('.nobuild-row');
        if (!row) return null;
        return state.zones[parseInt(row.getAttribute('data-idx'), 10)] || null;
    }

    el.addNoBuild.addEventListener('click', function () {
        state.zones.push({ id: null, reason: 'Other', notes: '', points: [], ll: null });
        renderZoneList();
    });

    el.noBuildList.addEventListener('input', function (e) {
        var z = zoneFromRow(e.target);
        if (!z) return;
        if (e.target.classList.contains('nb-points')) {
            z.ll = null;
            z.points = parsePoints(e.target.value).points;
            renderZoneMap();
        } else if (e.target.classList.contains('nb-notes')) {
            z.notes = e.target.value;
        }
    });

    el.noBuildList.addEventListener('change', function (e) {
        var z = zoneFromRow(e.target);
        if (z && e.target.classList.contains('nb-reason')) z.reason = e.target.value;
    });

    el.noBuildList.addEventListener('click', function (e) {
        var z = zoneFromRow(e.target);
        if (!z) return;

        if (e.target.classList.contains('nb-remove')) {
            if (state.draw && state.draw.zone === z) setDraw(null);
            state.zones.splice(state.zones.indexOf(z), 1);
            renderZoneList();
            renderZoneMap();
            return;
        }

        if (e.target.classList.contains('nb-draw')) {
            if (state.draw && state.draw.type === 'zone' && state.draw.zone === z) {
                setDraw(null);
                return;
            }
            if (!hasOrigin()) {
                showMessage('Set the plot location on the map first.', 'warning');
                return;
            }
            if (z.points.length && !window.confirm('Replace the existing corners of this zone?')) return;
            z.points = [];
            z.ll = [];
            updateZoneText(z);
            renderZoneMap();
            setDraw({ type: 'zone', zone: z });
            showMessage('Click each corner of the zone on the map, then press Done.', 'info');
        }
    });

    /* ---------- receptors ---------- */

    function renderReceptorMap() {
        receptorLayer.clearLayers();
        state.receptors.forEach(function (r) {
            L.circleMarker([r.latitude, r.longitude], {
                radius: 6,
                color: '#ff7043',
                fillColor: '#ff7043',
                fillOpacity: r.isConfirmed ? 0.9 : 0.1,
                weight: 2
            }).bindTooltip(esc(r.type + (r.name ? ': ' + r.name : '') + ' (' + round(r.distanceFromPlotM, 0) + ' m)'))
                .addTo(receptorLayer);
        });
    }

    function renderReceptorTable() {
        if (!state.receptors.length) {
            el.receptorBody.innerHTML = '<tr><td colspan="6" class="receptor-empty">No receptors yet. Use Find nearby or Add manually.</td></tr>';
            return;
        }

        el.receptorBody.innerHTML = state.receptors.map(function (r, i) {
            return '<tr data-idx="' + i + '">' +
                '<td><select class="form-select form-select-sm rc-type">' + optionsHtml(RECEPTOR_TYPES, r.type) + '</select></td>' +
                '<td><input class="form-control form-control-sm rc-name" value="' + esc(r.name || '') + '" /></td>' +
                '<td class="text-end">' + round(r.distanceFromPlotM, 0) + '</td>' +
                '<td>' + (r.source === 'MapData' ? 'Map data' : 'Added by you') + '</td>' +
                '<td class="text-center"><input type="checkbox" class="form-check-input rc-confirm"' + (r.isConfirmed ? ' checked' : '') + ' /></td>' +
                '<td class="text-end"><button type="button" class="btn btn-sm btn-outline-danger rc-remove">Remove</button></td>' +
                '</tr>';
        }).join('');
    }

    function receptorFromRow(target) {
        var row = target.closest('tr[data-idx]');
        if (!row) return null;
        return state.receptors[parseInt(row.getAttribute('data-idx'), 10)] || null;
    }

    el.receptorBody.addEventListener('input', function (e) {
        var r = receptorFromRow(e.target);
        if (r && e.target.classList.contains('rc-name')) r.name = e.target.value;
    });

    el.receptorBody.addEventListener('change', function (e) {
        var r = receptorFromRow(e.target);
        if (!r) return;
        if (e.target.classList.contains('rc-type')) r.type = e.target.value;
        if (e.target.classList.contains('rc-confirm')) r.isConfirmed = e.target.checked;
        renderReceptorMap();
    });

    el.receptorBody.addEventListener('click', function (e) {
        if (!e.target.classList.contains('rc-remove')) return;
        var r = receptorFromRow(e.target);
        if (!r) return;
        state.receptors.splice(state.receptors.indexOf(r), 1);
        renderReceptorTable();
        renderReceptorMap();
    });

    function addReceptorAt(latlng) {
        state.receptors.push({
            id: null,
            type: 'Other',
            name: '',
            latitude: latlng.lat,
            longitude: latlng.lng,
            distanceFromPlotM: hasOrigin() ? haversine(state.lat, state.lon, latlng.lat, latlng.lng) : 0,
            source: 'UserAdded',
            isConfirmed: true
        });
        renderReceptorTable();
        renderReceptorMap();
        showMessage('Receptor added. Choose its type and name in the table.', 'success');
    }

    el.addReceptor.addEventListener('click', function () {
        if (!hasOrigin()) {
            showMessage('Set the plot location on the map first.', 'warning');
            return;
        }
        if (state.draw && state.draw.type === 'receptor') {
            setDraw(null);
            return;
        }
        setDraw({ type: 'receptor' });
        showMessage('Click the map where the receptor is.', 'info');
    });

    el.findReceptors.addEventListener('click', function () {
        if (!hasOrigin()) {
            showMessage('Set the plot location on the map first.', 'warning');
            return;
        }

        el.findReceptors.disabled = true;
        showMessage('Searching map data near the plot...', 'info');

        var url = URLS.receptors + '?lat=' + encodeURIComponent(state.lat) +
            '&lon=' + encodeURIComponent(state.lon) + '&radius=' + RECEPTOR_RADIUS_M;

        request(url, { method: 'GET' }).then(function (res) {
            el.findReceptors.disabled = false;
            if (!res.ok || !res.data || !res.data.success) {
                showMessage(failText(res, 'Could not find receptors'), 'danger');
                return;
            }

            var added = 0;
            (res.data.receptors || []).forEach(function (f) {
                var dup = state.receptors.some(function (r) {
                    return r.type === f.type &&
                        haversine(r.latitude, r.longitude, f.latitude, f.longitude) < 10;
                });
                if (dup) return;
                f.isConfirmed = false;
                f.source = f.source || 'MapData';
                state.receptors.push(f);
                added++;
            });

            renderReceptorTable();
            renderReceptorMap();
            showMessage(added + ' receptor(s) found. Tick Confirm for the correct ones.', added ? 'success' : 'info');
        }).catch(function () {
            el.findReceptors.disabled = false;
            showMessage('Could not reach the receptor search. Try again.', 'danger');
        });
    });

    /* ---------- drawing state ---------- */

    function setDraw(d) {
        state.draw = d;
        map.getContainer().classList.toggle('is-drawing', !!d || (state.mode === 'Map' && hasOrigin() && !state.closed));
        el.addReceptor.textContent = d && d.type === 'receptor' ? 'Cancel' : 'Add manually';
        renderZoneList();
    }

    map.on('click', function (e) {
        if (state.draw) {
            if (state.draw.type === 'receptor') {
                addReceptorAt(e.latlng);
                setDraw(null);
            } else if (state.draw.type === 'zone') {
                var z = state.draw.zone;
                z.ll = z.ll || [];
                z.ll.push({ lat: e.latlng.lat, lon: e.latlng.lng });
                syncZone(z);
                updateZoneText(z);
                renderZoneMap();
            }
            return;
        }

        if (state.mode === 'Map' && hasOrigin()) {
            addCorner(e.latlng);
            map.getContainer().classList.toggle('is-drawing', !state.closed);
            return;
        }

        setOrigin(e.latlng.lat, e.latlng.lng, false);
        map.getContainer().classList.toggle('is-drawing', state.mode === 'Map' && !state.closed);
    });

    /* ---------- mode tabs and inputs ---------- */

    function setMode(mode) {
        if (mode === 'Typed' && state.mode === 'Map' && state.corners.length && !el.cornersText.value.trim() && hasOrigin()) {
            el.cornersText.value = formatPoints(plotMetres());
        }

        state.mode = mode;

        Array.prototype.forEach.call(el.tabs.querySelectorAll('[data-mode]'), function (b) {
            b.classList.toggle('active', b.getAttribute('data-mode') === mode);
        });
        Array.prototype.forEach.call(root.querySelectorAll('[data-pane]'), function (p) {
            p.classList.toggle('d-none', p.getAttribute('data-pane') !== mode);
        });

        map.getContainer().classList.toggle('is-drawing', mode === 'Map' && hasOrigin() && !state.closed);
        renderPlot();
    }

    el.tabs.addEventListener('click', function (e) {
        var b = e.target.closest('[data-mode]');
        if (b) setMode(b.getAttribute('data-mode'));
    });

    el.cornersText.addEventListener('input', renderPlot);
    el.rectLength.addEventListener('input', renderPlot);
    el.rectWidth.addEventListener('input', renderPlot);

    el.undo.addEventListener('click', function () {
        if (!state.corners.length) return;
        state.corners.pop();
        state.closed = false;
        renderPlot();
        map.getContainer().classList.toggle('is-drawing', hasOrigin());
    });

    el.clear.addEventListener('click', function () {
        state.corners = [];
        state.closed = false;
        renderPlot();
        map.getContainer().classList.toggle('is-drawing', hasOrigin());
    });

    el.go.addEventListener('click', function () {
        var lat = num(el.lat);
        var lon = num(el.lon);
        if (lat === null || lon === null || lat < -90 || lat > 90 || lon < -180 || lon > 180) {
            showMessage('Enter a valid latitude and longitude.', 'warning');
            return;
        }
        setOrigin(lat, lon, true);
    });

    el.layerSat.addEventListener('click', function () { setLayer(true); });
    el.layerOsm.addEventListener('click', function () { setLayer(false); });

    /* ---------- DXF import ---------- */

    el.importDxf.addEventListener('click', function () {
        var file = el.dxfFile.files && el.dxfFile.files[0];
        if (!file) {
            showMessage('Choose a DXF file first.', 'warning');
            return;
        }

        var fd = new FormData();
        fd.append('file', file);

        el.importDxf.disabled = true;
        showMessage('Reading the DXF boundary...', 'info');

        request(URLS.importDxf, { method: 'POST', headers: authHeaders(false), body: fd }).then(function (res) {
            el.importDxf.disabled = false;
            if (!res.ok || !res.data || !res.data.success) {
                showMessage(failText(res, 'Could not read the DXF'), 'danger');
                return;
            }

            var corners = (res.data.corners || []).map(function (c) { return { x: c.x, y: c.y }; });
            el.cornersText.value = formatPoints(corners);
            setMode('Typed');

            if (res.data.latitude != null && res.data.longitude != null) {
                setOrigin(res.data.latitude, res.data.longitude, true);
            }

            var lines = [res.data.message || 'Boundary imported.'];
            if (!hasOrigin()) lines.push('Set the plot location on the map so the boundary can be placed.');
            showMessage(lines, 'success');
        }).catch(function () {
            el.importDxf.disabled = false;
            showMessage('Could not upload the DXF. Try again.', 'danger');
        });
    });

    /* ---------- save ---------- */

    function intOrZero(input) {
        var v = parseInt(input.value, 10);
        return isFinite(v) ? v : 0;
    }

    function buildPayload() {
        syncAllZones();
        var metres = plotMetres();

        return {
            projectId: PROJECT_ID,
            latitude: state.lat,
            longitude: state.lon,
            inputMode: state.mode,
            plotCorners: metres.map(function (p) { return { x: p.x, y: p.y }; }),
            plotCornersLatLon: state.mode === 'Map'
                ? state.corners.map(function (c) { return { lat: c.lat, lon: c.lon }; })
                : [],
            rectLengthM: state.mode === 'Rectangle' ? num(el.rectLength) : null,
            rectWidthM: state.mode === 'Rectangle' ? num(el.rectWidth) : null,
            plotAreaM2: polygonArea(metres),
            gateEdgeIndex: intOrZero(el.gateEdge),
            gateOffsetM: num(el.gateOffset) || 0,
            northAngleDeg: num(el.northAngle) || 0,
            windFromDeg: num(el.windFrom),
            slopeDirectionDeg: num(el.slopeDir),
            powerSideDeg: num(el.powerSide),
            gasOfftakeSideDeg: num(el.gasSide),
            notes: el.notes.value.trim() || null,
            noBuildZones: state.zones
                .filter(function (z) { return z.points.length >= 3; })
                .map(function (z) {
                    return {
                        id: z.id,
                        reason: z.reason,
                        notes: z.notes || null,
                        polygon: z.points.map(function (p) { return { x: p.x, y: p.y }; })
                    };
                }),
            receptors: state.receptors.map(function (r) {
                return {
                    id: r.id,
                    type: r.type,
                    name: r.name || null,
                    latitude: r.latitude,
                    longitude: r.longitude,
                    distanceFromPlotM: r.distanceFromPlotM,
                    source: r.source,
                    isConfirmed: !!r.isConfirmed
                };
            })
        };
    }

    function adoptSaved(site) {
        state.zones = (site.noBuildZones || []).map(function (z) {
            return {
                id: z.id,
                reason: z.reason,
                notes: z.notes || '',
                points: (z.polygon || []).map(function (p) { return { x: p.x, y: p.y }; }),
                ll: null
            };
        });
        state.receptors = site.receptors || [];
        renderZoneList();
        renderZoneMap();
        renderReceptorTable();
        renderReceptorMap();
        if (site.plotAreaM2) el.area.textContent = round(site.plotAreaM2, 0).toLocaleString();
    }

    el.save.addEventListener('click', function () {
        clearMessage();

        if (!hasOrigin()) {
            showMessage('Plot location is required. Click the map or enter latitude and longitude.', 'warning');
            return;
        }

        var payload = buildPayload();

        if (payload.plotCorners.length < 3) {
            showMessage('The plot needs at least 3 corner points.', 'warning');
            return;
        }
        if (payload.gateEdgeIndex < 0 || payload.gateEdgeIndex >= payload.plotCorners.length) {
            showMessage('Gate side must be between 0 and ' + (payload.plotCorners.length - 1) + '.', 'warning');
            return;
        }

        el.save.disabled = true;
        showMessage('Saving...', 'info');

        request(URLS.save, { method: 'POST', headers: authHeaders(true), body: JSON.stringify(payload) }).then(function (res) {
            el.save.disabled = false;

            if (!res.data) {
                showMessage(failText(res, 'Could not save'), 'danger');
                return;
            }

            if (res.data.site) adoptSaved(res.data.site);

            if (res.ok && res.data.success) {
                showMessage('Plot and site saved.', 'success');
            } else if (res.data.errors && res.data.errors.length) {
                showMessage(['Saved, but the plot has problems:'].concat(res.data.errors), 'warning');
            } else {
                showMessage(failText(res, 'Could not save'), 'danger');
            }
        }).catch(function () {
            el.save.disabled = false;
            showMessage('Could not reach the server. Try again.', 'danger');
        });
    });

    /* ---------- render and load ---------- */

    function renderAll() {
        renderPlot();
        renderZoneMap();
        renderReceptorMap();
    }

    function setNumber(input, v) {
        input.value = v === null || v === undefined ? '' : v;
    }

    function load() {
        var site = null;
        try {
            site = JSON.parse($('siteData').textContent);
        } catch (e) {
            site = null;
        }

        setLayer(true);

        if (site) {
            setNumber(el.gateEdge, site.gateEdgeIndex || 0);
            setNumber(el.gateOffset, site.gateOffsetM || 0);
            setNumber(el.northAngle, site.northAngleDeg || 0);
            setNumber(el.windFrom, site.windFromDeg);
            setNumber(el.slopeDir, site.slopeDirectionDeg);
            setNumber(el.powerSide, site.powerSideDeg);
            setNumber(el.gasSide, site.gasOfftakeSideDeg);
            el.notes.value = site.notes || '';

            state.zones = (site.noBuildZones || []).map(function (z) {
                return {
                    id: z.id,
                    reason: z.reason,
                    notes: z.notes || '',
                    points: (z.polygon || []).map(function (p) { return { x: p.x, y: p.y }; }),
                    ll: null
                };
            });
            state.receptors = site.receptors || [];

            var corners = site.plotCorners || [];
            if (corners.length) el.cornersText.value = formatPoints(corners);
            state.corners = (site.plotCornersLatLon || []).map(function (c) { return { lat: c.lat, lon: c.lon }; });
            state.closed = state.corners.length >= 3;

            if (site.latitude || site.longitude) {
                setOrigin(site.latitude, site.longitude, false);
            }
        }

        setMode('Map');
        renderZoneList();
        renderReceptorTable();
        renderAll();

        if (hasOrigin()) {
            var ll = plotLatLon().map(function (c) { return [c.lat, c.lon]; });
            if (ll.length >= 2) map.fitBounds(L.latLngBounds(ll), { maxZoom: 19, padding: [30, 30] });
            else map.setView([state.lat, state.lon], 17);
        }

        map.getContainer().classList.toggle('is-drawing', state.mode === 'Map' && hasOrigin() && !state.closed);
        setTimeout(function () { map.invalidateSize(); }, 200);
    }

    load();
})();
