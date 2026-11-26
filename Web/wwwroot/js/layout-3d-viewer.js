import * as THREE from '../lib/three/three.module.js';

const COL = {
    ground: 0xd6d2c9,
    groundPad: 0xc9cbbd,
    grid: 0xc4bfb3,
    pad: 0x52525b,
    curb: 0xf4f4f5,
    road: 0x71717a,
    fence: 0xa1a1aa,
    equip: 0x1e2229,
    edge: 0xd4d4d8,
    edgeLight: 0x9ca3af,
    hospital: 0xffffff,
    school: 0xe6c86e,
    schoolMark: 0xb8a24f,
    house: 0xbfc5ce,
    houseRoof: 0xa8afba,
    tower: 0x6b7280,
    wire: 0x374151,
    plane: 0x9ca3af,
    alert: 0xdc2626,
    bg: 0xeceae5,
    noBuild: 0xa1a1aa
};

const PAD_H = 0.3;
const DEFAULT_MIN_CLEAR = 4.5;
let MIN_CLEAR = DEFAULT_MIN_CLEAR;
const NEAR_M = 15;
const HORIZ_BREACH_M = 3;
const PHASE = 3;
const DEG = Math.PI / 180;
const EARTH_R = 6371008.8;

const STYLE = '' +
    '.l3d-canvas{display:block;width:100%;height:100%;outline:none;touch-action:none}' +
    '.l3d-labels{position:absolute;left:0;top:0;right:0;bottom:0;overflow:hidden;pointer-events:none}' +
    '.l3d-label{position:absolute;left:0;top:0;pointer-events:none;white-space:nowrap;font:11px/1.3 system-ui,-apple-system,Segoe UI,Roboto,sans-serif;padding:1px 5px;border-radius:3px;background:rgba(255,255,255,.9);color:#1e2229;border:1px solid #d4d4d8;will-change:transform}' +
    '.l3d-label.l3d-tag{background:rgba(30,34,41,.88);color:#f4f4f5;border-color:transparent}' +
    '.l3d-label.l3d-bad{background:#dc2626;color:#fff;border-color:#dc2626}' +
    '.l3d-hud{position:absolute;left:0;top:0;right:0;bottom:0;pointer-events:none;font:12px/1.35 system-ui,-apple-system,Segoe UI,Roboto,sans-serif;color:#f4f4f5}' +
    '.l3d-col{position:absolute;display:flex;flex-direction:column;gap:6px}' +
    '.l3d-tl{left:8px;top:8px;max-width:70%}' +
    '.l3d-tr{right:8px;top:8px}' +
    '.l3d-bl{left:8px;bottom:8px;max-width:60%}' +
    '.l3d-panel{pointer-events:auto;background:rgba(30,34,41,.88);border-radius:6px;padding:6px 8px}' +
    '.l3d-panel.l3d-ro{pointer-events:none}' +
    '.l3d-row{display:flex;flex-wrap:wrap;gap:4px;align-items:center}' +
    '.l3d-btn{pointer-events:auto;border:1px solid #52525b;background:#27272a;color:#f4f4f5;border-radius:4px;padding:3px 8px;font-size:12px;cursor:pointer}' +
    '.l3d-btn:hover{background:#3f3f46}' +
    '.l3d-btn:disabled{opacity:.45;cursor:not-allowed}' +
    '.l3d-badge{display:inline-block;padding:3px 9px;border-radius:4px;font-weight:600;background:#27272a;color:#f4f4f5}' +
    '.l3d-badge.l3d-bad{background:#dc2626;color:#fff}' +
    '.l3d-layer{display:flex;gap:6px;align-items:center;cursor:pointer;white-space:nowrap}' +
    '.l3d-title{font-weight:600;margin-bottom:2px}' +
    '.l3d-muted{color:#a1a1aa}' +
    '.l3d-insp-title{font-weight:600}' +
    '.l3d-insp-bad{color:#fca5a5}';

/* ---------- geometry helpers ---------- */

function v3(x, y, h) {
    return new THREE.Vector3(x, h || 0, -y);
}

function segNearest(px, py, ax, ay, bx, by) {
    const dx = bx - ax;
    const dy = by - ay;
    const l2 = dx * dx + dy * dy;
    let t = l2 > 0 ? ((px - ax) * dx + (py - ay) * dy) / l2 : 0;
    t = Math.max(0, Math.min(1, t));
    const x = ax + dx * t;
    const y = ay + dy * t;
    return { x: x, y: y, t: t, d: Math.hypot(px - x, py - y) };
}

function nearestOnPoly(p, poly) {
    let best = null;
    for (let i = 0; i < poly.length; i++) {
        const a = poly[i];
        const b = poly[(i + 1) % poly.length];
        const n = segNearest(p.x, p.y, a.x, a.y, b.x, b.y);
        if (!best || n.d < best.d) best = n;
    }
    return best;
}

function pointInPoly(p, poly) {
    let inside = false;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
        const a = poly[i];
        const b = poly[j];
        if ((a.y > p.y) !== (b.y > p.y) &&
            p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) {
            inside = !inside;
        }
    }
    return inside;
}

function footprintOf(it) {
    const w = it.widthM || 0;
    const l = it.lengthM || 0;
    const pts = [];
    if ((it.shape || '') === 'Circle') {
        const r = w / 2;
        for (let i = 0; i < 24; i++) {
            const a = 2 * Math.PI * i / 24;
            pts.push({ x: it.x + r * Math.cos(a), y: it.y + r * Math.sin(a) });
        }
        return pts;
    }
    const rot = (it.rotationDeg || 0) * DEG;
    const c = Math.cos(rot);
    const s = Math.sin(rot);
    [[-1, -1], [1, -1], [1, 1], [-1, 1]].forEach(function (k) {
        const lx = k[0] * w / 2;
        const ly = k[1] * l / 2;
        pts.push({ x: it.x + lx * c - ly * s, y: it.y + lx * s + ly * c });
    });
    return pts;
}

function sampleFootprint(fp) {
    const pts = [];
    let cx = 0;
    let cy = 0;
    let minX = Infinity;
    let maxX = -Infinity;
    let minY = Infinity;
    let maxY = -Infinity;
    fp.forEach(function (p) {
        cx += p.x;
        cy += p.y;
        minX = Math.min(minX, p.x);
        maxX = Math.max(maxX, p.x);
        minY = Math.min(minY, p.y);
        maxY = Math.max(maxY, p.y);
    });
    cx /= fp.length;
    cy /= fp.length;
    pts.push({ x: cx, y: cy });

    for (let i = 0; i < fp.length; i++) {
        const a = fp[i];
        const b = fp[(i + 1) % fp.length];
        const len = Math.hypot(b.x - a.x, b.y - a.y);
        const n = Math.max(1, Math.min(8, Math.ceil(len / 2)));
        for (let k = 0; k < n; k++) {
            const t = k / n;
            pts.push({ x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t });
        }
    }

    const gx = 10;
    for (let i = 1; i < gx; i++) {
        for (let j = 1; j < gx; j++) {
            const p = { x: minX + (maxX - minX) * i / gx, y: minY + (maxY - minY) * j / gx };
            if (pointInPoly(p, fp)) pts.push(p);
        }
    }
    return pts;
}

function polyDistance(fp, poly) {
    if (!poly || poly.length < 3) return null;
    let d = Infinity;
    fp.forEach(function (p) {
        const n = nearestOnPoly(p, poly);
        if (n && n.d < d) d = n.d;
    });
    return d;
}

function defaultHeight(hazard) {
    switch (hazard) {
        case 'DigesterGas': return 10;
        case 'GasVessel': return 6;
        case 'Tank': return 6;
        case 'Flare': return 12;
        case 'Building': return 4;
        case 'Scrubber': return 8;
        case 'Compressor': return 3.5;
        case 'Cascade': return 2.5;
        case 'Electrical': return 3;
        default: return 3;
    }
}

function disposeTree(root) {
    root.traverse(function (n) {
        if (n.geometry) n.geometry.dispose();
        if (n.material) {
            (Array.isArray(n.material) ? n.material : [n.material]).forEach(function (m) { m.dispose(); });
        }
    });
}

function fmt(v) {
    return (Math.round(v * 10) / 10).toFixed(1);
}

function signed(v) {
    return (v >= 0 ? '+' : '') + fmt(v);
}

function sagFor(len) {
    return 0.045 * len;
}

/* ---------- HT corridor model ---------- */

function corridorFromTowers(pts, source, label) {
    return {
        towers: pts.map(function (p) {
            const h = p.h || 18;
            return { x: p.x, y: p.y, h: h, ah: h - 1.2 };
        }),
        halfW: PHASE,
        source: source,
        label: label
    };
}

function towerLine(cx, cy, dx, dy, h) {
    const pts = [];
    const n = 6;
    const span = 70;
    for (let k = 0; k < n; k++) {
        const o = (k - (n - 1) / 2) * span;
        pts.push({ x: cx + dx * o, y: cy + dy * o, h: h });
    }
    return pts;
}

function derivedCorridor(r, cx, cy) {
    let vx = r.x - cx;
    let vy = r.y - cy;
    const l = Math.hypot(vx, vy) || 1;
    vx /= l;
    vy /= l;
    return corridorFromTowers(towerLine(r.x, r.y, -vy, vx, 18), 'PowerLine receptor (heights assumed)', r.name || 'Power line');
}

function previewCorridor(plot, site, cx, cy) {
    const b = (site.powerSideDeg != null ? site.powerSideDeg : 0) * DEG;
    const sx = Math.sin(b);
    const sy = Math.cos(b);
    let maxP = 0;
    plot.forEach(function (p) {
        maxP = Math.max(maxP, (p.x - cx) * sx + (p.y - cy) * sy);
    });
    const off = Math.max(maxP - 6, 0);
    return corridorFromTowers(towerLine(cx + sx * off, cy + sy * off, -sy, sx, 18), 'preview', 'Preview corridor');
}

function wireAt(c, px, py) {
    let best = null;
    for (let i = 0; i < c.towers.length - 1; i++) {
        const a = c.towers[i];
        const b = c.towers[i + 1];
        const n = segNearest(px, py, a.x, a.y, b.x, b.y);
        if (!best || n.d < best.d) {
            const len = Math.hypot(b.x - a.x, b.y - a.y);
            const y = (a.ah + (b.ah - a.ah) * n.t) - sagFor(len) * 4 * n.t * (1 - n.t);
            best = { d: n.d, y: y, seg: i, t: n.t, px: n.x, py: n.y };
        }
    }
    return best;
}

function lowestSag(c) {
    let low = Infinity;
    for (let i = 0; i < c.towers.length - 1; i++) {
        const a = c.towers[i];
        const b = c.towers[i + 1];
        const len = Math.hypot(b.x - a.x, b.y - a.y);
        low = Math.min(low, (a.ah + b.ah) / 2 - sagFor(len));
    }
    return low;
}

/* ---------- viewer ---------- */

export function createLayout3DViewer(options) {
    const container = options.container;
    const onSelect = options.onSelect || function () { };
    const onClearance = options.onClearance || function () { };
    MIN_CLEAR = options.minClearanceM > 0 ? options.minClearanceM : DEFAULT_MIN_CLEAR;

    const prevPosition = container.style.position;
    const prevOverflow = container.style.overflow;
    if (getComputedStyle(container).position === 'static') container.style.position = 'relative';
    container.style.overflow = 'hidden';

    const styleEl = document.createElement('style');
    styleEl.textContent = STYLE;
    document.head.appendChild(styleEl);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    const canvas = renderer.domElement;
    canvas.className = 'l3d-canvas';
    canvas.setAttribute('tabindex', '0');
    container.appendChild(canvas);

    const labelLayer = document.createElement('div');
    labelLayer.className = 'l3d-labels';
    container.appendChild(labelLayer);

    const hud = document.createElement('div');
    hud.className = 'l3d-hud';
    container.appendChild(hud);

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(COL.bg);
    scene.add(new THREE.HemisphereLight(0xffffff, 0xb9b4a8, 0.85));
    const sun = new THREE.DirectionalLight(0xffffff, 0.8);
    sun.position.set(120, 200, 80);
    scene.add(sun);

    const camera = new THREE.PerspectiveCamera(45, 1, 0.5, 8000);
    camera.position.set(80, 70, 90);
    const target = new THREE.Vector3(0, 0, 0);
    camera.lookAt(target);

    const raycaster = new THREE.Raycaster();
    const ndc = new THREE.Vector2();
    const tmp = new THREE.Vector3();
    const offset = new THREE.Vector3();
    const sph = new THREE.Spherical();
    const right = new THREE.Vector3();
    const upv = new THREE.Vector3();
    const delta = new THREE.Vector3();

    const layers = { ht: true, rec: true, lines: true, edges: true };
    let previewOn = false;

    let data = { site: null, layout: null, equipment: [], selected: null };
    let world = null;
    let gHT = null;
    let gRec = null;
    let gLines = null;
    const labels = [];
    let pickables = [];
    let itemRecs = [];
    let edgeLines = [];
    let hoverInfo = null;
    let selectedKey = null;
    let M = { cx: 0, cz: 0, R: 60, tall: 8, recR: 0, wire: null };
    let corridors = [];
    let corridorKind = 'none';
    let lastSig = null;
    let didFit = false;
    let disposed = false;
    let dirty = true;
    let anim = null;
    let raf = 0;
    let size = { w: 400, h: 400 };

    function invalidate() { dirty = true; }

    /* ---------- HUD ---------- */

    function el(tag, cls, text) {
        const n = document.createElement(tag);
        if (cls) n.className = cls;
        if (text != null) n.textContent = text;
        return n;
    }

    const presetBtns = {};
    const colTL = el('div', 'l3d-col l3d-tl');
    const presetPanel = el('div', 'l3d-panel l3d-row');
    [['overview', 'Overview'], ['wire', 'Wire Clearance'], ['nbr', 'Neighborhood'],
    ['top', 'Top Plan'], ['front', 'Front'], ['side', 'Side']].forEach(function (p) {
        const b = el('button', 'l3d-btn', p[1]);
        b.type = 'button';
        b.addEventListener('click', function () { goPreset(p[0], false); });
        presetPanel.appendChild(b);
        presetBtns[p[0]] = b;
    });
    colTL.appendChild(presetPanel);

    const badgePanel = el('div', 'l3d-panel l3d-row');
    const badge = el('span', 'l3d-badge', '');
    badgePanel.appendChild(badge);
    colTL.appendChild(badgePanel);

    const htPanel = el('div', 'l3d-panel l3d-row');
    const htText = el('span', '', '');
    const previewBtn = el('button', 'l3d-btn', 'Preview corridor');
    previewBtn.type = 'button';
    previewBtn.title = 'Reference HT corridor for testing only. Nothing is saved.';
    previewBtn.addEventListener('click', function () {
        previewOn = !previewOn;
        rebuild();
    });
    htPanel.appendChild(htText);
    htPanel.appendChild(previewBtn);
    colTL.appendChild(htPanel);
    hud.appendChild(colTL);

    const colTR = el('div', 'l3d-col l3d-tr');
    const layerPanel = el('div', 'l3d-panel');
    layerPanel.appendChild(el('div', 'l3d-title', 'LAYERS'));
    [['ht', 'HT Towers & Overhead Wires'], ['rec', 'Hospital, School & Houses'],
    ['lines', 'Receptor Distance Lines'], ['edges', 'CAD Wireframe Overlay']].forEach(function (l) {
        const lab = el('label', 'l3d-layer');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.checked = layers[l[0]];
        cb.addEventListener('change', function () {
            layers[l[0]] = cb.checked;
            applyLayers();
        });
        lab.appendChild(cb);
        lab.appendChild(el('span', '', l[1]));
        layerPanel.appendChild(lab);
    });
    colTR.appendChild(layerPanel);
    hud.appendChild(colTR);

    const colBL = el('div', 'l3d-col l3d-bl');
    const inspPanel = el('div', 'l3d-panel l3d-ro');
    const inspTitle = el('div', 'l3d-insp-title', '');
    const inspBody = el('div', '', '');
    inspPanel.appendChild(inspTitle);
    inspPanel.appendChild(inspBody);
    colBL.appendChild(inspPanel);
    hud.appendChild(colBL);

    function setInspector(info) {
        let show = info;
        if (!show && selectedKey) {
            const rec = itemRecs.find(function (r) { return r.key === selectedKey; });
            if (rec) show = rec.info;
        }
        inspBody.textContent = '';
        if (!show) {
            inspTitle.textContent = 'Inspector';
            const d = el('div', 'l3d-muted', 'Hover or click equipment, receptors or towers.');
            inspBody.appendChild(d);
            return;
        }
        inspTitle.textContent = show.title;
        show.lines.forEach(function (ln) {
            inspBody.appendChild(el('div', ln.bad ? 'l3d-insp-bad' : '', ln.text));
        });
    }

    /* ---------- labels ---------- */

    function addLabel(text, pos, cls, layer) {
        const n = el('div', 'l3d-label' + (cls ? ' ' + cls : ''), text);
        labelLayer.appendChild(n);
        labels.push({ el: n, pos: pos, layer: layer || '' });
    }

    function clearLabels() {
        labels.forEach(function (l) { l.el.remove(); });
        labels.length = 0;
    }

    function updateLabels() {
        const w = size.w;
        const h = size.h;
        labels.forEach(function (l) {
            if (l.layer && !layers[l.layer]) {
                l.el.style.display = 'none';
                return;
            }
            tmp.copy(l.pos).project(camera);
            if (tmp.z > 1 || tmp.z < -1 || Math.abs(tmp.x) > 1.1 || Math.abs(tmp.y) > 1.1) {
                l.el.style.display = 'none';
                return;
            }
            l.el.style.display = 'block';
            l.el.style.transform = 'translate(' + ((tmp.x * 0.5 + 0.5) * w).toFixed(1) + 'px,' +
                ((-tmp.y * 0.5 + 0.5) * h).toFixed(1) + 'px) translate(-50%,-110%)';
        });
    }

    /* ---------- camera ---------- */

    function flyTo(p, t, instant) {
        if (instant) {
            camera.position.copy(p);
            target.copy(t);
            camera.lookAt(target);
            anim = null;
            invalidate();
            return;
        }
        anim = {
            t0: performance.now(),
            dur: 650,
            p0: camera.position.clone(),
            tg0: target.clone(),
            p1: p.clone(),
            tg1: t.clone()
        };
        invalidate();
    }

    function stepAnim(now) {
        if (!anim) return false;
        let k = (now - anim.t0) / anim.dur;
        if (k >= 1) k = 1;
        const e = k < 0.5 ? 4 * k * k * k : 1 - Math.pow(-2 * k + 2, 3) / 2;
        camera.position.lerpVectors(anim.p0, anim.p1, e);
        target.lerpVectors(anim.tg0, anim.tg1, e);
        camera.lookAt(target);
        if (k >= 1) anim = null;
        return true;
    }

    function presetView(name) {
        const R = Math.max(M.R, 30);
        const c = new THREE.Vector3(M.cx, 0, M.cz);
        const tall = Math.max(M.tall, 6);
        switch (name) {
            case 'top':
                return { p: new THREE.Vector3(M.cx, R * 2.0, M.cz + R * 0.002), t: c };
            case 'front':
                return {
                    p: new THREE.Vector3(M.cx, Math.max(tall * 0.6, 6), M.cz + R * 1.9),
                    t: new THREE.Vector3(M.cx, tall * 0.35, M.cz)
                };
            case 'side':
                return {
                    p: new THREE.Vector3(M.cx + R * 1.9, Math.max(tall * 0.6, 6), M.cz),
                    t: new THREE.Vector3(M.cx, tall * 0.35, M.cz)
                };
            case 'nbr': {
                const S = Math.max(M.recR, R) * 1.1;
                return { p: new THREE.Vector3(M.cx + S * 0.55, S * 0.95, M.cz + S * 1.05), t: c };
            }
            case 'wire': {
                if (M.wire) {
                    const w = M.wire;
                    const dist = Math.max(R * 1.3, 70);
                    return {
                        p: new THREE.Vector3(w.x + w.nx * dist, 6, w.z + w.nz * dist),
                        t: new THREE.Vector3(w.x, w.low * 0.55, w.z)
                    };
                }
                return presetView('side');
            }
            default:
                return {
                    p: new THREE.Vector3(M.cx + R * 1.0, R * 0.85 + tall, M.cz + R * 1.2),
                    t: c
                };
        }
    }

    function goPreset(name, instant) {
        const v = presetView(name);
        flyTo(v.p, v.t, instant);
    }

    function orbit(dx, dy) {
        offset.copy(camera.position).sub(target);
        sph.setFromVector3(offset);
        sph.theta -= dx * 0.0055;
        sph.phi = Math.min(Math.PI / 2 - 0.01, Math.max(0.001, sph.phi - dy * 0.0055));
        offset.setFromSpherical(sph);
        camera.position.copy(target).add(offset);
        camera.lookAt(target);
        invalidate();
    }

    function pan(dx, dy) {
        const dist = camera.position.distanceTo(target);
        const k = 2 * dist * Math.tan(camera.fov * DEG / 2) / Math.max(size.h, 1);
        camera.updateMatrix();
        right.setFromMatrixColumn(camera.matrix, 0);
        upv.setFromMatrixColumn(camera.matrix, 1);
        delta.copy(right).multiplyScalar(-dx * k).addScaledVector(upv, dy * k);
        camera.position.add(delta);
        target.add(delta);
        invalidate();
    }

    /* ---------- picking ---------- */

    function isShown(o) {
        while (o) {
            if (!o.visible) return false;
            o = o.parent;
        }
        return true;
    }

    function pickAt(cx, cy) {
        const r = canvas.getBoundingClientRect();
        ndc.set(((cx - r.left) / r.width) * 2 - 1, -((cy - r.top) / r.height) * 2 + 1);
        raycaster.setFromCamera(ndc, camera);
        const hits = raycaster.intersectObjects(pickables, false);
        for (let i = 0; i < hits.length; i++) {
            if (isShown(hits[i].object)) return hits[i].object.userData.info || null;
        }
        return null;
    }

    function refreshEdges() {
        itemRecs.forEach(function (rec) {
            let col = COL.edge;
            if (rec.breach || rec.recBreach) col = COL.alert;
            else if (rec.key === selectedKey || (hoverInfo && hoverInfo.rec === rec)) col = 0xffffff;
            rec.edgeMats.forEach(function (m) { m.color.setHex(col); });
        });
        invalidate();
    }

    let hoverPending = false;
    let lastMouse = null;

    function doHover() {
        hoverPending = false;
        if (disposed || !lastMouse) return;
        const info = pickAt(lastMouse.x, lastMouse.y);
        if (info !== hoverInfo) {
            hoverInfo = info;
            canvas.style.cursor = info ? 'pointer' : 'grab';
            setInspector(info);
            refreshEdges();
        }
    }

    let drag = null;

    canvas.addEventListener('contextmenu', function (e) { e.preventDefault(); });

    canvas.addEventListener('pointerdown', function (e) {
        canvas.setPointerCapture(e.pointerId);
        anim = null;
        drag = {
            x: e.clientX, y: e.clientY, sx: e.clientX, sy: e.clientY,
            btn: e.button, shift: e.shiftKey, moved: false
        };
    });

    canvas.addEventListener('pointermove', function (e) {
        if (drag) {
            const dx = e.clientX - drag.x;
            const dy = e.clientY - drag.y;
            if (Math.abs(e.clientX - drag.sx) + Math.abs(e.clientY - drag.sy) > 4) drag.moved = true;
            if (drag.moved) {
                if (drag.btn === 2 || drag.btn === 1 || drag.shift) pan(dx, dy);
                else orbit(dx, dy);
            }
            drag.x = e.clientX;
            drag.y = e.clientY;
            return;
        }
        lastMouse = { x: e.clientX, y: e.clientY };
        if (!hoverPending) {
            hoverPending = true;
            requestAnimationFrame(doHover);
        }
    });

    canvas.addEventListener('pointerup', function (e) {
        const d = drag;
        drag = null;
        try { canvas.releasePointerCapture(e.pointerId); } catch (err) { /* ignore */ }
        if (d && !d.moved && d.btn === 0) {
            const info = pickAt(e.clientX, e.clientY);
            if (info && info.kind === 'equipment') onSelect(info.eq, info.inst);
            else if (!info) onSelect(null, 1);
        }
    });

    canvas.addEventListener('pointerleave', function () {
        lastMouse = null;
        if (hoverInfo) {
            hoverInfo = null;
            canvas.style.cursor = '';
            setInspector(null);
            refreshEdges();
        }
    });

    canvas.addEventListener('wheel', function (e) {
        e.preventDefault();
        anim = null;
        offset.copy(camera.position).sub(target);
        let len = offset.length() * Math.exp(Math.sign(e.deltaY) * Math.min(Math.abs(e.deltaY), 120) * 0.0012);
        len = Math.max(4, Math.min(5000, len));
        offset.setLength(len);
        camera.position.copy(target).add(offset);
        invalidate();
    }, { passive: false });

    /* ---------- layers ---------- */

    function applyLayers() {
        if (gHT) gHT.visible = layers.ht;
        if (gRec) gRec.visible = layers.rec;
        if (gLines) gLines.visible = layers.lines;
        edgeLines.forEach(function (l) { l.visible = layers.edges; });
        invalidate();
    }

    /* ---------- world build ---------- */

    function clearWorld() {
        if (world) {
            scene.remove(world);
            disposeTree(world);
        }
        world = new THREE.Group();
        scene.add(world);
        gHT = new THREE.Group();
        gRec = new THREE.Group();
        gLines = new THREE.Group();
        world.add(gHT);
        world.add(gRec);
        world.add(gLines);
        clearLabels();
        pickables = [];
        itemRecs = [];
        edgeLines = [];
        hoverInfo = null;
    }

    function edgesOf(mesh, color, light) {
        const mat = new THREE.LineBasicMaterial({ color: light ? COL.edgeLight : color });
        const lines = new THREE.LineSegments(new THREE.EdgesGeometry(mesh.geometry, 25), mat);
        lines.userData.isEdge = true;
        mesh.add(lines);
        edgeLines.push(lines);
        return mat;
    }

    function toMetres(lat, lon, lat0, lon0) {
        const x = (lon - lon0) * DEG * EARTH_R * Math.cos(lat0 * DEG);
        const y = (lat - lat0) * DEG * EARTH_R;
        return { x: x, y: y };
    }

    function gatePointOf(site, layout, plot) {
        if (layout && layout.gatePoint && (layout.gatePoint.x || layout.gatePoint.y)) {
            return { x: layout.gatePoint.x, y: layout.gatePoint.y };
        }
        if (plot.length >= 2) {
            const i = (site.gateEdgeIndex || 0) % plot.length;
            const a = plot[i];
            const b = plot[(i + 1) % plot.length];
            const len = Math.hypot(b.x - a.x, b.y - a.y) || 1;
            const off = site.gateOffsetM != null ? site.gateOffsetM : len / 2;
            const t = Math.max(0, Math.min(len, off)) / len;
            return { x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t };
        }
        return null;
    }

    function buildPlot(plot, gate, site, layout) {
        if (plot.length < 3) return;

        const shp = new THREE.Shape(plot.map(function (p) { return new THREE.Vector2(p.x, p.y); }));
        const padGeo = new THREE.ExtrudeGeometry(shp, { depth: PAD_H, bevelEnabled: false });
        padGeo.rotateX(-Math.PI / 2);
        world.add(new THREE.Mesh(padGeo, new THREE.MeshLambertMaterial({ color: COL.pad })));

        const unit = new THREE.BoxGeometry(1, 1, 1);
        const curbMat = new THREE.MeshLambertMaterial({ color: COL.curb });
        const postGeo = new THREE.CylinderGeometry(0.07, 0.07, 2.2, 6);
        const postMat = new THREE.MeshLambertMaterial({ color: COL.fence });
        const railPos = [];

        for (let i = 0; i < plot.length; i++) {
            const a = plot[i];
            const b = plot[(i + 1) % plot.length];
            const len = Math.hypot(b.x - a.x, b.y - a.y);
            if (len < 0.1) continue;
            const ang = Math.atan2(b.y - a.y, b.x - a.x);

            const steps = Math.max(1, Math.ceil(len / 2));
            for (let k = 0; k < steps; k++) {
                const t0 = k / steps;
                const t1 = (k + 1) / steps;
                const mx = a.x + (b.x - a.x) * (t0 + t1) / 2;
                const my = a.y + (b.y - a.y) * (t0 + t1) / 2;
                if (gate && Math.hypot(mx - gate.x, my - gate.y) < 3.5) continue;
                const seg = new THREE.Mesh(unit, curbMat);
                seg.scale.set(len / steps, 0.45, 0.5);
                seg.position.set(mx, PAD_H + 0.22, -my);
                seg.rotation.y = ang;
                world.add(seg);
            }

            const posts = Math.max(1, Math.ceil(len / 5));
            let prev = null;
            for (let k = 0; k <= posts; k++) {
                const t = k / posts;
                const px = a.x + (b.x - a.x) * t;
                const py = a.y + (b.y - a.y) * t;
                const inGate = gate && Math.hypot(px - gate.x, py - gate.y) < 4;
                if (inGate) { prev = null; continue; }
                const post = new THREE.Mesh(postGeo, postMat);
                post.position.set(px, PAD_H + 1.1, -py);
                world.add(post);
                if (prev) {
                    [0.7, 1.9].forEach(function (hh) {
                        railPos.push(prev.x, PAD_H + hh, -prev.y, px, PAD_H + hh, -py);
                    });
                }
                prev = { x: px, y: py };
            }
        }

        if (railPos.length) {
            const rg = new THREE.BufferGeometry();
            rg.setAttribute('position', new THREE.Float32BufferAttribute(railPos, 3));
            world.add(new THREE.LineSegments(rg, new THREE.LineBasicMaterial({ color: COL.fence })));
        }

        if (layout && layout.roads) {
            const roadMat = new THREE.MeshLambertMaterial({ color: COL.road });
            layout.roads.forEach(function (rd) {
                const pts = rd.points || [];
                for (let i = 0; i < pts.length - 1; i++) {
                    const a = pts[i];
                    const b = pts[i + 1];
                    const len = Math.hypot(b.x - a.x, b.y - a.y);
                    if (len < 0.05) continue;
                    const seg = new THREE.Mesh(unit, roadMat);
                    seg.scale.set(len, 0.04, rd.widthM || 4);
                    seg.position.set((a.x + b.x) / 2, PAD_H + 0.03, -(a.y + b.y) / 2);
                    seg.rotation.y = Math.atan2(b.y - a.y, b.x - a.x);
                    world.add(seg);
                }
            });
        }

        (site.noBuildZones || []).forEach(function (z) {
            const poly = z.polygon || [];
            if (poly.length < 3) return;
            const zs = new THREE.Shape(poly.map(function (p) { return new THREE.Vector2(p.x, p.y); }));
            const zg = new THREE.ShapeGeometry(zs);
            zg.rotateX(-Math.PI / 2);
            const zm = new THREE.Mesh(zg, new THREE.MeshBasicMaterial({
                color: COL.noBuild, transparent: true, opacity: 0.3, side: THREE.DoubleSide, depthWrite: false
            }));
            zm.position.y = PAD_H + 0.06;
            world.add(zm);
            const loop = poly.map(function (p) { return v3(p.x, p.y, PAD_H + 0.08); });
            loop.push(loop[0].clone());
            const lg = new THREE.BufferGeometry().setFromPoints(loop);
            const line = new THREE.Line(lg, new THREE.LineDashedMaterial({ color: 0x71717a, dashSize: 1, gapSize: 0.6 }));
            line.computeLineDistances();
            world.add(line);
        });
    }

    function evaluateItem(rec) {
        const samples = sampleFootprint(rec.fp);
        let best = null;
        corridors.forEach(function (c) {
            let minD = Infinity;
            samples.forEach(function (s) {
                s.w = wireAt(c, s.x, s.y);
                if (s.w && s.w.d < minD) minD = s.w.d;
            });
            if (!isFinite(minD)) return;
            const dh = Math.max(0, minD - c.halfW);
            if (dh > NEAR_M) return;
            let low = Infinity;
            let fallback = null;
            samples.forEach(function (s) {
                if (!s.w) return;
                if (s.w.d - c.halfW <= HORIZ_BREACH_M) low = Math.min(low, s.w.y);
                if (!fallback || s.w.d < fallback.d) fallback = s.w;
            });
            if (!isFinite(low) && fallback) low = fallback.y;
            const dH = low - rec.topY;
            if (!best || dH < best.dH) best = { dH: dH, dh: dh, low: low, corridor: c };
        });
        if (best) {
            rec.dH = best.dH;
            rec.dh = best.dh;
            rec.low = best.low;
            rec.corridor = best.corridor;
            rec.breach = best.dh < HORIZ_BREACH_M && best.dH < MIN_CLEAR;
        }
    }

    function buildEquipment(rec) {
        const it = rec.it;
        const g = new THREE.Group();
        g.position.set(it.x, PAD_H, -it.y);
        g.rotation.y = (it.rotationDeg || 0) * DEG;
        const mat = new THREE.MeshLambertMaterial({ color: COL.equip });
        const h = rec.h;
        rec.edgeMats = [];

        const reg = function (m) {
            m.userData.info = rec.info;
            pickables.push(m);
        };

        if ((it.shape || '') === 'Circle') {
            const r = Math.max((it.widthM || 1) / 2, 0.1);
            const domed = rec.hazard === 'DigesterGas';
            const dome = domed ? Math.min(r * 0.5, h * 0.4) : 0;
            const cylH = Math.max(h - dome, 0.2);
            const cyl = new THREE.Mesh(new THREE.CylinderGeometry(r, r, cylH, 32), mat);
            cyl.position.y = cylH / 2;
            rec.edgeMats.push(edgesOf(cyl, COL.edge, false));
            reg(cyl);
            g.add(cyl);
            if (domed && dome > 0.05) {
                const sphere = new THREE.Mesh(new THREE.SphereGeometry(r, 32, 12, 0, Math.PI * 2, 0, Math.PI / 2), mat);
                sphere.scale.y = dome / r;
                sphere.position.y = cylH;
                reg(sphere);
                g.add(sphere);
            }
        } else {
            const box = new THREE.Mesh(new THREE.BoxGeometry(Math.max(it.widthM || 1, 0.1), h, Math.max(it.lengthM || 1, 0.1)), mat);
            box.position.y = h / 2;
            rec.edgeMats.push(edgesOf(box, COL.edge, false));
            reg(box);
            g.add(box);
        }
        world.add(g);
        addLabel(it.tag + '  H ' + fmt(h) + ' m', v3(it.x, it.y, rec.topY + 0.8), 'l3d-tag', '');
    }

    function buildReceptor(r, cx, cy, plot) {
        const type = r.type || 'Other';
        const ang = Math.atan2(r.y - cy, r.x - cx);
        const g = new THREE.Group();
        g.position.set(r.x, 0, -r.y);
        g.rotation.y = ang + Math.PI / 2;

        let spec;
        switch (type) {
            case 'Hospital': spec = { w: 28, l: 18, h: 16, color: COL.hospital }; break;
            case 'School': spec = { w: 24, l: 12, h: 8, color: COL.school }; break;
            case 'House': spec = { w: 10, l: 8, h: 6, color: COL.house }; break;
            case 'Road': spec = { w: 36, l: 7, h: 0.15, color: 0xa1a1aa }; break;
            case 'Canal': spec = { w: 36, l: 6, h: 0.1, color: 0x9aa5b1 }; break;
            default: spec = { w: 6, l: 6, h: 4, color: COL.house };
        }

        const info = {
            kind: 'receptor',
            title: type + (r.name && r.name !== type ? ' \u00b7 ' + r.name : ''),
            lines: []
        };

        const padR = Math.max(spec.w, spec.l) * 0.95;
        const pad = new THREE.Mesh(new THREE.CircleGeometry(padR, 40), new THREE.MeshLambertMaterial({ color: COL.groundPad }));
        pad.rotation.x = -Math.PI / 2;
        pad.position.y = 0.03;
        g.add(pad);

        const mat = new THREE.MeshLambertMaterial({ color: spec.color });
        const body = new THREE.Mesh(new THREE.BoxGeometry(spec.w, spec.h, spec.l), mat);
        body.position.y = spec.h / 2 + 0.04;
        body.userData.info = info;
        pickables.push(body);
        g.add(body);
        if (type !== 'Road' && type !== 'Canal') edgesOf(body, COL.edgeLight, true);

        if (type === 'Hospital') {
            const crossMat = new THREE.MeshLambertMaterial({ color: COL.tower });
            const c1 = new THREE.Mesh(new THREE.BoxGeometry(7, 0.3, 1.6), crossMat);
            const c2 = new THREE.Mesh(new THREE.BoxGeometry(1.6, 0.3, 7), crossMat);
            c1.position.y = spec.h + 0.2;
            c2.position.y = spec.h + 0.2;
            g.add(c1);
            g.add(c2);
        } else if (type === 'School') {
            const court = new THREE.Mesh(new THREE.PlaneGeometry(14, 10), new THREE.MeshBasicMaterial({ color: 0xdcc77c }));
            court.rotation.x = -Math.PI / 2;
            court.position.set(0, 0.06, spec.l / 2 + 7);
            g.add(court);
            const loopPts = [[-7, -5], [7, -5], [7, 5], [-7, 5], [-7, -5]].map(function (p) {
                return new THREE.Vector3(p[0], 0.08, spec.l / 2 + 7 + p[1]);
            });
            g.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(loopPts), new THREE.LineBasicMaterial({ color: COL.schoolMark })));
        } else if (type === 'House') {
            const roof = new THREE.Mesh(new THREE.ConeGeometry(spec.w * 0.78, 2.4, 4), new THREE.MeshLambertMaterial({ color: COL.houseRoof }));
            roof.rotation.y = Math.PI / 4;
            roof.position.y = spec.h + 1.24;
            roof.userData.info = info;
            pickables.push(roof);
            g.add(roof);
        }
        gRec.add(g);

        info.lines.push({ text: 'Distance from plot edge: ' + fmt(r.distEdge) + ' m' });
        if (type === 'Road' || type === 'Canal') {
            info.lines.push({ text: 'Mapped position (marker only, extent not mapped)' });
        } else {
            info.lines.push({ text: 'Massing (assumed): ' + fmt(spec.l) + ' \u00d7 ' + fmt(spec.w) + ' \u00d7 ' + fmt(spec.h) + ' m' });
        }
        info.lines.push({ text: 'Source: ' + (r.source || 'n/a') + (r.isConfirmed ? ' \u00b7 confirmed' : ' \u00b7 unconfirmed') });

        const np = plot.length >= 3 ? nearestOnPoly(r, plot) : null;
        if (np && !pointInPoly(r, plot)) {
            const pts = [v3(np.x, np.y, PAD_H + 0.4), v3(r.x, r.y, PAD_H + 0.4)];
            const lg = new THREE.BufferGeometry().setFromPoints(pts);
            const line = new THREE.Line(lg, new THREE.LineDashedMaterial({ color: COL.wire, dashSize: 1.6, gapSize: 1.0 }));
            line.computeLineDistances();
            gLines.add(line);
            addLabel(type + ' \u00b7 ' + fmt(r.distEdge) + ' m',
                v3((np.x + r.x) / 2, (np.y + r.y) / 2, PAD_H + 1.2), '', 'lines');
        }
        addLabel(info.title, v3(r.x, r.y, spec.h + 3), 'l3d-tag', 'rec');
        return spec;
    }

    function buildTower(t, dx, dy, c, lowY) {
        const h = t.h;
        const g = new THREE.Group();
        g.position.set(t.x, 0, -t.y);
        g.rotation.y = Math.atan2(dx, -dy);

        const L = 6;
        const pos = [];
        const seg = function (a, b) { pos.push(a[0], a[1], a[2], b[0], b[1], b[2]); };
        const cs = [[-1, -1], [1, -1], [1, 1], [-1, 1]];
        const corner = function (i, j) {
            const w = 1.9 - 1.4 * (i / L);
            return [cs[j][0] * w, h * i / L, cs[j][1] * w];
        };
        for (let i = 0; i <= L; i++) {
            for (let j = 0; j < 4; j++) seg(corner(i, j), corner(i, (j + 1) % 4));
        }
        for (let i = 0; i < L; i++) {
            for (let j = 0; j < 4; j++) {
                seg(corner(i, j), corner(i + 1, j));
                if ((i + j) % 2 === 0) seg(corner(i, j), corner(i + 1, (j + 1) % 4));
                else seg(corner(i, (j + 1) % 4), corner(i + 1, j));
            }
        }
        const lg = new THREE.BufferGeometry();
        lg.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
        g.add(new THREE.LineSegments(lg, new THREE.LineBasicMaterial({ color: COL.tower })));

        const toolMat = new THREE.MeshLambertMaterial({ color: COL.tower });
        const armW = PHASE * 2 + 1.4;
        const arm = new THREE.Mesh(new THREE.BoxGeometry(armW, 0.3, 0.3), toolMat);
        arm.position.y = t.ah;
        g.add(arm);
        [-PHASE, 0, PHASE].forEach(function (o) {
            const ins = new THREE.Mesh(new THREE.BoxGeometry(0.15, 0.8, 0.15), toolMat);
            ins.position.set(o, t.ah - 0.5, 0);
            g.add(ins);
        });

        const info = {
            kind: 'tower',
            title: 'HT tower \u00b7 ' + c.label,
            lines: [
                { text: 'Height: ' + fmt(h) + ' m' },
                { text: 'Conductor attach height: ' + fmt(t.ah) + ' m' },
                { text: 'Lowest wire sag: ' + fmt(lowY) + ' m' },
                { text: 'Required vertical clearance: ' + fmt(MIN_CLEAR) + ' m' },
                { text: c.source === 'preview' ? 'Preview corridor (not saved)' : 'Source: ' + c.source }
            ]
        };
        const proxy = new THREE.Mesh(new THREE.BoxGeometry(4.4, h, 4.4),
            new THREE.MeshBasicMaterial({ transparent: true, opacity: 0, depthWrite: false }));
        proxy.position.y = h / 2;
        proxy.userData.info = info;
        pickables.push(proxy);
        g.add(proxy);
        gHT.add(g);
    }

    function buildCorridor(c) {
        const lowY = lowestSag(c);
        const wireMat = new THREE.MeshLambertMaterial({ color: COL.wire });
        let lowMid = null;

        for (let i = 0; i < c.towers.length; i++) {
            const a = c.towers[Math.max(i - 1, 0)];
            const b = c.towers[Math.min(i + 1, c.towers.length - 1)];
            let dx = b.x - a.x;
            let dy = b.y - a.y;
            const l = Math.hypot(dx, dy) || 1;
            dx /= l;
            dy /= l;
            buildTower(c.towers[i], dx, dy, c, lowY);
        }

        for (let i = 0; i < c.towers.length - 1; i++) {
            const a = c.towers[i];
            const b = c.towers[i + 1];
            const len = Math.hypot(b.x - a.x, b.y - a.y);
            if (len < 1) continue;
            const dx = (b.x - a.x) / len;
            const dy = (b.y - a.y) / len;
            const nx = -dy;
            const ny = dx;
            const sag = sagFor(len);
            [-PHASE, 0, PHASE].forEach(function (o) {
                const pts = [];
                for (let j = 0; j <= 16; j++) {
                    const t = j / 16;
                    const x = a.x + (b.x - a.x) * t + nx * o;
                    const y = a.y + (b.y - a.y) * t + ny * o;
                    const yy = (a.ah + (b.ah - a.ah) * t) - sag * 4 * t * (1 - t);
                    pts.push(v3(x, y, yy));
                }
                const curve = new THREE.CatmullRomCurve3(pts);
                gHT.add(new THREE.Mesh(new THREE.TubeGeometry(curve, 24, 0.07, 5, false), wireMat));
            });
            const mid = (a.ah + b.ah) / 2 - sag;
            if (!lowMid || mid < lowMid.y) {
                lowMid = { y: mid, x: (a.x + b.x) / 2, py: (a.y + b.y) / 2 };
            }

            const plane = new THREE.Mesh(new THREE.PlaneGeometry(len, lowY),
                new THREE.MeshBasicMaterial({
                    color: COL.plane, transparent: true, opacity: 0.14, side: THREE.DoubleSide, depthWrite: false
                }));
            plane.position.set((a.x + b.x) / 2, lowY / 2, -(a.y + b.y) / 2);
            plane.rotation.y = Math.atan2(dy, dx);
            gHT.add(plane);
        }

        if (lowMid) {
            addLabel(c.label + ' \u00b7 lowest sag ' + fmt(lowY) + ' m', v3(lowMid.x, lowMid.py, lowMid.y + 1.5), '', 'ht');
        }
        return lowY;
    }

    function buildDimension(rec) {
        const bad = rec.breach;
        const col = bad ? COL.alert : COL.wire;
        const x = rec.it.x;
        const y = rec.it.y;
        const pts = [v3(x, y, rec.topY), v3(x, y, rec.low)];
        const line = new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts),
            new THREE.LineDashedMaterial({ color: col, dashSize: 0.7, gapSize: 0.4 }));
        line.computeLineDistances();
        gHT.add(line);
        [rec.topY, rec.low].forEach(function (yy) {
            const tp = [v3(x - 0.8, y, yy), v3(x + 0.8, y, yy)];
            gHT.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(tp), new THREE.LineBasicMaterial({ color: col })));
        });
        addLabel('\u0394H ' + signed(rec.dH) + ' m', v3(x, y, (rec.topY + rec.low) / 2), bad ? 'l3d-bad' : '', 'ht');
    }

    function rebuild() {
        clearWorld();

        const site = data.site || {};
        const layout = data.layout || null;
        const eqMap = new Map();
        (data.equipment || []).forEach(function (e) { eqMap.set(e.id, e); });

        const plot = (site.plotCorners || []).map(function (p) { return { x: +p.x, y: +p.y }; });
        let cx = 0;
        let cy = 0;
        if (plot.length) {
            cx = plot.reduce(function (s, p) { return s + p.x; }, 0) / plot.length;
            cy = plot.reduce(function (s, p) { return s + p.y; }, 0) / plot.length;
        }
        let R = 30;
        plot.forEach(function (p) { R = Math.max(R, Math.hypot(p.x - cx, p.y - cy) * 1.3); });

        const lat0 = site.latitude || 0;
        const lon0 = site.longitude || 0;
        const receptors = (site.receptors || []).map(function (r) {
            const m = toMetres(r.latitude, r.longitude, lat0, lon0);
            const np = plot.length >= 3 ? nearestOnPoly(m, plot) : null;
            return {
                type: r.type, name: r.name, x: m.x, y: m.y, source: r.source,
                isConfirmed: r.isConfirmed,
                distEdge: np ? np.d : (r.distanceFromPlotM || 0)
            };
        });

        /* items */
        selectedKey = data.selected ? data.selected.eq + ':' + (data.selected.inst || 1) : null;
        const layoutWarnings = (layout && layout.warnings) || [];
        let tall = 6;
        (layout && layout.items ? layout.items : []).forEach(function (it) {
            const eq = eqMap.get(it.equipmentItemId) || {};
            const hazard = eq.hazardClass || '';
            const h = Math.max(eq.heightM != null ? eq.heightM : defaultHeight(hazard), 0.2);
            const inst = it.instanceNo || 1;
            const rec = {
                key: it.equipmentItemId + ':' + inst,
                eq: it.equipmentItemId,
                inst: inst,
                it: it,
                hazard: hazard,
                h: h,
                topY: PAD_H + h,
                fp: footprintOf(it),
                dH: null, dh: null, low: null, corridor: null,
                breach: false, recBreach: false, edgeMats: []
            };
            tall = Math.max(tall, rec.topY);
            rec.recBreach = layoutWarnings.some(function (w) {
                return w.code === 'CLEARANCE_RECEPTOR' && w.equipmentItemId === it.equipmentItemId && (w.instanceNo || 1) === inst;
            });
            itemRecs.push(rec);
        });

        /* corridors */
        corridors = [];
        corridorKind = 'none';
        (site.htLines || []).forEach(function (ln) {
            const pts = (ln.towers || []).map(function (t) {
                let p = { x: t.x, y: t.y };
                if (t.latitude != null && t.longitude != null) p = toMetres(t.latitude, t.longitude, lat0, lon0);
                return { x: p.x, y: p.y, h: t.heightM || 18 };
            });
            if (pts.length >= 2) corridors.push(corridorFromTowers(pts, 'Site Assessment', ln.name || 'HT line'));
        });
        if (corridors.length) corridorKind = 'mapped';
        if (!corridors.length) {
            const pl = receptors.filter(function (r) { return r.type === 'PowerLine'; })
                .sort(function (a, b) { return a.distEdge - b.distEdge; })[0];
            if (pl) {
                corridors.push(derivedCorridor(pl, cx, cy));
                corridorKind = 'mapped';
            }
        }
        if (!corridors.length && previewOn) {
            corridors.push(previewCorridor(plot, site, cx, cy));
            corridorKind = 'preview';
        }

        itemRecs.forEach(evaluateItem);

        /* metrics */
        let recR = 0;
        receptors.forEach(function (r) { recR = Math.max(recR, Math.hypot(r.x - cx, r.y - cy) + 40); });
        corridors.forEach(function (c) {
            c.towers.forEach(function (t) { recR = Math.max(recR, Math.hypot(t.x - cx, t.y - cy)); });
        });
        M = { cx: cx, cz: -cy, R: R, tall: tall, recR: recR, wire: null };
        if (corridors.length) {
            const c0 = corridors[0];
            const w = wireAt(c0, cx, cy);
            if (w) {
                const a = c0.towers[w.seg];
                const b = c0.towers[w.seg + 1];
                const l = Math.hypot(b.x - a.x, b.y - a.y) || 1;
                const dx = (b.x - a.x) / l;
                const dy = (b.y - a.y) / l;
                M.wire = { x: w.px, z: -w.py, nx: -dy, nz: -dx, low: lowestSag(c0) };
            }
        }

        /* ground */
        const half = Math.max(300, Math.max(R, recR) * 1.6 + 120);
        const gGeo = new THREE.PlaneGeometry(half * 2, half * 2);
        gGeo.rotateX(-Math.PI / 2);
        const ground = new THREE.Mesh(gGeo, new THREE.MeshLambertMaterial({ color: COL.ground }));
        ground.position.set(cx, -0.1, -cy);
        world.add(ground);
        const grid = new THREE.GridHelper(half * 2, Math.min(200, Math.round(half / 10)), COL.grid, COL.grid);
        grid.position.set(cx, -0.05, -cy);
        grid.material.transparent = true;
        grid.material.opacity = 0.55;
        world.add(grid);

        const gate = gatePointOf(site, layout, plot);
        buildPlot(plot, gate, site, layout);

        itemRecs.forEach(function (rec) {
            const it = rec.it;
            const bd = polyDistance(rec.fp, plot);
            const lines = [];
            if ((it.shape || '') === 'Circle') {
                lines.push({ text: '\u00d8 ' + fmt(it.widthM || 0) + ' \u00d7 H ' + fmt(rec.h) + ' m' });
            } else {
                lines.push({ text: 'L \u00d7 W \u00d7 H: ' + fmt(it.lengthM || 0) + ' \u00d7 ' + fmt(it.widthM || 0) + ' \u00d7 ' + fmt(rec.h) + ' m' });
            }
            if (bd != null) lines.push({ text: 'Boundary distance: ' + fmt(bd) + ' m' });
            if (rec.dH != null) {
                lines.push({
                    text: 'Wire clearance \u0394H: ' + signed(rec.dH) + ' m (min ' + fmt(MIN_CLEAR) + ' m)' +
                        (corridorKind === 'preview' ? ' [preview]' : ''),
                    bad: rec.breach
                });
            }
            layoutWarnings.forEach(function (w) {
                if (w.equipmentItemId === it.equipmentItemId && (w.instanceNo || 1) === rec.inst) {
                    const m = w.message || w.code;
                    lines.push({ text: m.length > 120 ? m.slice(0, 117) + '...' : m, bad: w.code === 'CLEARANCE_RECEPTOR' });
                }
            });
            rec.info = { kind: 'equipment', eq: rec.eq, inst: rec.inst, rec: rec, title: it.tag + ' \u00b7 ' + it.name, lines: lines };
            buildEquipment(rec);
        });

        receptors.forEach(function (r) {
            if (r.type === 'PowerLine') return;
            buildReceptor(r, cx, cy, plot);
        });

        corridors.forEach(buildCorridor);
        itemRecs.forEach(function (rec) {
            if (rec.dH != null && rec.dh <= NEAR_M) buildDimension(rec);
        });

        applyLayers();
        refreshEdges();
        setInspector(null);
        updateHud();
        emitClearance();
        resize();

        if (!didFit) {
            didFit = true;
            goPreset('overview', true);
        }
        invalidate();
    }

    /* ---------- clearance / HUD ---------- */

    function wireWarnings() {
        const list = [];
        itemRecs.forEach(function (rec) {
            if (!rec.breach) return;
            const it = rec.it;
            list.push({
                code: 'CLEARANCE_WIRE',
                message: (corridorKind === 'preview' ? '(Preview corridor) ' : '') +
                    it.tag + ' (' + it.name + ') wire clearance \u0394H ' + signed(rec.dH) +
                    ' m, required ' + fmt(MIN_CLEAR) + ' m.',
                ruleStatus: 'Assumed',
                sourceNote: '3D viewer vertical clearance check',
                equipmentItemId: rec.eq,
                instanceNo: rec.inst,
                actualDistanceM: rec.dH,
                requiredDistanceM: MIN_CLEAR
            });
        });
        return list;
    }

    function emitClearance() {
        const warnings = wireWarnings();
        const sig = JSON.stringify(warnings.map(function (w) {
            return [w.equipmentItemId, w.instanceNo, Math.round(w.actualDistanceM * 100)];
        }));
        if (sig === lastSig) return;
        lastSig = sig;
        onClearance({ warnings: warnings });
    }

    function updateHud() {
        presetBtns.wire.disabled = !M.wire;
        presetBtns.wire.title = M.wire ? '' : 'No HT line mapped. Use Preview corridor to test.';

        if (corridorKind === 'mapped') {
            htText.textContent = 'HT line: ' + corridors.length + ' mapped near site';
            previewBtn.style.display = 'none';
        } else if (corridorKind === 'preview') {
            htText.textContent = 'HT line: preview corridor (not saved)';
            previewBtn.style.display = '';
            previewBtn.textContent = 'Hide preview';
        } else {
            htText.textContent = 'HT line: none mapped near site';
            previewBtn.style.display = '';
            previewBtn.textContent = 'Preview corridor';
        }

        let minRec = null;
        let anyBreach = false;
        itemRecs.forEach(function (rec) {
            if (rec.dH == null || rec.dh > NEAR_M) return;
            if (!minRec || rec.dH < minRec.dH) minRec = rec;
            if (rec.breach) anyBreach = true;
        });

        badge.classList.remove('l3d-bad');
        if (!corridors.length) {
            badgePanel.style.display = 'none';
        } else {
            badgePanel.style.display = '';
            if (anyBreach) {
                badge.textContent = 'CLEARANCE BREACH: < ' + fmt(MIN_CLEAR) + ' m (\u0394H ' + signed(minRec.dH) + ' m)';
                badge.classList.add('l3d-bad');
            } else if (minRec) {
                badge.textContent = 'WIRE CLEARANCE: ' + signed(minRec.dH) + ' m';
            } else {
                badge.textContent = 'WIRE CLEARANCE: no equipment near corridor';
            }
        }
    }

    /* ---------- sizing / loop ---------- */

    function resize() {
        const w = container.clientWidth;
        const h = container.clientHeight;
        if (w < 10 || h < 10) return;
        size = { w: w, h: h };
        renderer.setSize(w, h, false);
        canvas.style.width = '100%';
        canvas.style.height = '100%';
        camera.aspect = w / h;
        camera.updateProjectionMatrix();
        invalidate();
    }

    const ro = new ResizeObserver(resize);
    ro.observe(container);

    function frame(now) {
        if (disposed) return;
        raf = requestAnimationFrame(frame);
        const animating = stepAnim(now);
        if (!dirty && !animating) return;
        dirty = false;
        renderer.render(scene, camera);
        updateLabels();
    }

    clearWorld();
    setInspector(null);
    updateHud();
    resize();
    raf = requestAnimationFrame(frame);

    return {
        update: function (next) {
            if (disposed) return;
            data = {
                site: next && next.site ? next.site : null,
                layout: next && next.layout ? next.layout : null,
                equipment: next && next.equipment ? next.equipment : [],
                selected: next && next.selected ? next.selected : null
            };
            rebuild();
        },
        dispose: function () {
            if (disposed) return;
            disposed = true;
            cancelAnimationFrame(raf);
            ro.disconnect();
            if (world) {
                scene.remove(world);
                disposeTree(world);
            }
            clearLabels();
            renderer.dispose();
            renderer.forceContextLoss();
            canvas.remove();
            labelLayer.remove();
            hud.remove();
            styleEl.remove();
            container.style.position = prevPosition;
            container.style.overflow = prevOverflow;
        }
    };
}