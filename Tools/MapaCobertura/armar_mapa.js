#!/usr/bin/env node
// Builds one self-contained HTML page (top-down campus map) that shows how the app "sees" the buildings today:
// each building's box, the points of every Immersal map, how many points cover each face of each box, and where the
// field tests found a map or failed.
//
//   node Tools/MapaCobertura/armar_mapa.js [salida.html] [--diag]
//
// Default output: Registros/mapa-cobertura.html (ignored by git). --diag prints the numbers behind the page.
// No npm dependencies. The page layout, styles and script live in plantilla.html; this file only prepares the data
// and writes it into the template as one JSON block.
//
// Inputs (all read from the repository):
//   Assets/AncoRA/Paseo/<Edificio>/edificio.json   box size, GPS centre, and each map's box pose in its OWN frame
//   Escenas/<Edificio>/<id>-<nombre>-sparse.ply     map point clouds, native Immersal axes (PaseoMapClouds.cs)
//   Escenas/<Edificio>/<id>-registro.json           map -> drone cloud registration (registrar_mapa.js)
//   Escenas/<Edificio>/<Nube>-georef.json           drone cloud lat0/lon0 (georef_dron.js)
//   Escenas/<Edificio>/<Nube>.ply                   drone cloud, ENU metres (faint base layer)
//   Escenas/<Edificio>/caja-dron.json               box as estimated from the drone (to show how far the field moved it)
//   Registros/*/paseo-*-intentos.csv (+ .log)       field attempts (AnalizarRegistro.js), version from the log
//
// Frames. A map point is native Immersal (x, y, z); Unity map-local is (-x, y, z) (register.js readImmersal). The
// registration gives drone (E, Up, N) = rot(map local, yaw) + t, with rot() and apply() taken from register.js. The
// drone cloud is ENU metres around its own lat0/lon0, so every cloud is carried to ONE campus frame (east/north metres
// from the mean of the four building GPS centres) through latitude/longitude, with the same spherical conversion
// georef_dron.js used (Re = 6378137, cos(lat0)).
// A box is a Unity transform in the map frame: centre = caja.posicion, yaw = caja.giro, local axes X = (cos, -sin) and
// Z = (sin, cos) in (x, z); Z+ is the front face (the one with the X).
const fs = require('fs'), path = require('path'), vm = require('vm');
const R = require('../RegistroDron/register.js');

const REPO = path.resolve(__dirname, '..', '..');
const args = process.argv.slice(2);
const DIAG = args.includes('--diag');
const OUT = path.resolve(args.find(a => !a.startsWith('--')) || path.join(REPO, 'Registros', 'mapa-cobertura.html'));

const BUILDINGS = ['CienciasBasicas', 'EIC', 'Teologia', 'X1'];
// Drone cloud drawn as the faint base of each building: the one its maps were registered against.
const BASE_CLOUD = { CienciasBasicas: 'CienciasBasicas', EIC: 'EIC2', Teologia: 'TeologiaBajo', X1: 'X1' };

// Face coverage: points of the building's maps in a band around each vertical face of the box, in the map's own frame.
const BANDA = {
    dentro: 1,       // m inside the face plane
    fuera: 3,        // m outside the face plane
    sobreSuelo: 0.4, // from this far above the box base (floor texture is not facade)
    sobreTecho: 1,   // up to this far above the box top
};
// Level of a face by the density of its best map (points per metre of face width). The best-map densities of the 16
// faces fall in four groups with gaps between them: 0.1-0.4 (nothing there), 1.4-21 (a few stray points),
// 66-124 (seen at an angle from the side) and 225-731 (scanned from in front). The thresholds sit in the gaps.
const UMBRALES = { ninguno: 1, bajo: 40, medio: 200 };
// A phone counts as "in front of" a face when the nearest point of the face is 5-40 m away and the direction from that
// point to the phone is within 45 degrees of the face normal. (Measured from the nearest point, not the centre: a
// 35 m facade seen from 10 m to one side is still "in front of" it.)
const FRENTE = { angulo: 45, min: 5, max: 40 };

const RADIO_BASE = 80;   // m from the campus origin kept from the drone clouds
const RADIO_MAX = 220;   // m from the campus origin; farther map points are sky features triangulated far away
const CELDA_MAPA = 0.2;  // m, plan-view dedupe of drawn map points (counts always use every point)
const MAX_BASE = 15000;  // drone points kept for the base layer

// Why the maps that cannot be placed were left out. Source: CLAUDE.md, sections «Paseo virtual» and «Cajas del paseo
// desde el dron». Point counts and sizes come from the .ply files.
const MOTIVOS = {
    152199: 'No cubre el edificio: la cámara casi no se movió al capturar y no hay cómo colocar su caja desde el dron.',
    152240: 'Captura diminuta: la cámara casi no se movió. Hay que recapturar caminando.',
    152237: 'Sin registro: contra la nube del dron calza 16 % (ambiguo); contra el mapa 152242 no comparte nada o se contradice con el dron. Ve el lado de EIC que el dron no cubre.',
    152238: 'Sin registro: contra la nube del dron calza 19 % (ambiguo); contra el mapa 152242 no comparte nada o se contradice con el dron. Ve el lado de EIC que el dron no cubre.',
    152250: 'Captura diminuta: la cámara casi no se movió. Hay que recapturar caminando.',
    152194: 'No registró contra la nube del dron (14 % de puntos a menos de 25 cm, soluciones a 60 m entre sí).',
    152243: 'Captura diminuta: la cámara casi no se movió. Hay que recapturar caminando.',
    152245: 'Captura diminuta: la cámara casi no se movió. Hay que recapturar caminando.',
    152247: 'Captura diminuta: la cámara casi no se movió. Hay que recapturar caminando.',
    152248: 'Captura diminuta: la cámara casi no se movió. Hay que recapturar caminando.',
};

// Maps that are registered but not in the app. Source: CLAUDE.md, «Mapas guardados sin usar». A registered map that is
// in neither edificio.json nor this list is a CANDIDATE: a new map under evaluation (see «Candidatos» below).
const NOTAS_SIN_USAR = {
    152195: 'Salió en 1.1.3: unos 1.700 puntos y registro ambiguo; lo reemplazó 152259.',
    152241: 'Salió en 1.1.3: unos 1.700 puntos y registro ambiguo; lo reemplazó 152259.',
    152331: 'No entra todavía: calza 60 % pero es ambiguo (segunda solución a 1,03 veces, la caja cambia 1,4 a 1,6 m). Capturado nublado el 13-oct.',
};

// ---------------------------------------------------------------------------------------------------------------------
// Small helpers
const K = Math.PI / 180, RE = 6378137;
const round = (v, d) => { const f = 10 ** d; return Math.round(v * f) / f; };
const norm360 = a => ((a % 360) + 360) % 360;
const readJson = f => JSON.parse(fs.readFileSync(f, 'utf8'));
const rel = f => path.relative(REPO, f).split(path.sep).join('/');
const p = (...a) => path.join(REPO, ...a);
const log = (...a) => console.log(...a);

function b64(typed) { return Buffer.from(typed.buffer, typed.byteOffset, typed.byteLength).toString('base64'); }

// Binary PLY (float x y z + uchar r g b), checked against the file size, read with register.js' axis convention.
function readSparse(file) {
    const buf = fs.readFileSync(file), marker = 'end_header\n', end = buf.indexOf(marker) + marker.length;
    const n = +/element vertex (\d+)/.exec(buf.slice(0, end).toString('latin1'))[1];
    if (end + n * 15 !== buf.length) throw new Error(`${rel(file)}: el tamaño no calza con ${n} vértices de 15 bytes.`);
    return R.readImmersal(file);
}

// ASCII ENU PLY of the drone clouds (x east, y north, z up).
function readEnu(file) {
    const L = fs.readFileSync(file, 'utf8').split('\n');
    let h = 0; while (!L[h].startsWith('end_header')) h++;
    const out = [];
    for (let i = h + 1; i < L.length; i++) {
        const a = L[i].split(' ');
        if (a.length >= 3) out.push([+a[0], +a[1], +a[2]]);
    }
    return out;
}

// ---------------------------------------------------------------------------------------------------------------------
// Campus frame
const edificios = {};
for (const d of BUILDINGS) edificios[d] = readJson(p('Assets', 'AncoRA', 'Paseo', d, 'edificio.json'));
const latC = BUILDINGS.reduce((a, d) => a + edificios[d].gps.latitud, 0) / BUILDINGS.length;
const lonC = BUILDINGS.reduce((a, d) => a + edificios[d].gps.longitud, 0) / BUILDINGS.length;
const llToCampus = (lat, lon) => [(lon - lonC) * K * RE * Math.cos(latC * K), (lat - latC) * K * RE];
const enuToLatLon = (g, e, n) => [g.lat0 + n / (K * RE), g.lon0 + e / (K * RE * Math.cos(g.lat0 * K))];
const cloudToCampus = (g, e, n) => llToCampus(...enuToLatLon(g, e, n));
const georefCache = {};
function georef(dir, cloudFile) {
    const name = cloudFile.replace(/\.ply$/, '');
    const key = dir + '/' + name;
    return georefCache[key] || (georefCache[key] = readJson(p('Escenas', dir, `${name}-georef.json`)));
}

// ---------------------------------------------------------------------------------------------------------------------
// Box geometry
// Corners of a box (centre c = [x, y, z], yaw in degrees, half sizes) in the frame it was given, as [x, z] pairs in the
// order -X-Z, +X-Z, +X+Z, -X+Z.
function boxCorners(c, yaw, hw, hd) {
    const t = yaw * K, X = [Math.cos(t), -Math.sin(t)], Z = [Math.sin(t), Math.cos(t)];
    return [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([sx, sz]) => [c[0] + sx * hw * X[0] + sz * hd * Z[0], c[2] + sx * hw * X[1] + sz * hd * Z[1]]);
}

// Number of map points in the band of each face, in the map's own frame. A point belongs to at most one face (the one
// it is closest to being outside of), so box corners do not count twice.
function faceCounts(points, caja, tamano) {
    const t = caja.giro * K, cs = Math.cos(t), sn = Math.sin(t);
    const hw = tamano[0] / 2, hh = tamano[1] / 2, hd = tamano[2] / 2;
    const out = { '+Z': 0, '+X': 0, '-Z': 0, '-X': 0 };
    for (const q of points) {
        const v = q[1] - caja.posicion[1];
        if (v < -hh + BANDA.sobreSuelo || v > hh + BANDA.sobreTecho) continue;
        const dx = q[0] - caja.posicion[0], dz = q[2] - caja.posicion[2];
        const u = dx * cs - dz * sn, w = dx * sn + dz * cs;
        let best = null;
        for (const [name, s, along, half] of [['+Z', w - hd, u, hw], ['-Z', -w - hd, u, hw], ['+X', u - hw, w, hd], ['-X', -u - hw, w, hd]]) {
            if (s < -BANDA.dentro || s > BANDA.fuera || Math.abs(along) > half) continue;
            if (!best || s > best.s) best = { name, s };
        }
        if (best) out[best.name]++;
    }
    return out;
}

const CARAS = [
    { clave: '+Z', nombre: 'frente', corner: [3, 2], largo: 0 },   // -X+Z -> +X+Z
    { clave: '+X', nombre: 'lado +X', corner: [1, 2], largo: 1 },  // +X-Z -> +X+Z
    { clave: '-Z', nombre: 'atrás', corner: [0, 1], largo: 0 },    // -X-Z -> +X-Z
    { clave: '-X', nombre: 'lado -X', corner: [0, 3], largo: 1 },  // -X-Z -> -X+Z
];
const NIVELES = ['ninguno', 'bajo', 'medio', 'alto'];
const nivelDe = d => (d < UMBRALES.ninguno ? 'ninguno' : d < UMBRALES.bajo ? 'bajo' : d < UMBRALES.medio ? 'medio' : 'alto');

// ---------------------------------------------------------------------------------------------------------------------
// Maps: placed ones (in the app or not) and the ones that cannot be placed
const usedIds = new Set();
for (const d of BUILDINGS) for (const m of edificios[d].mapas) usedIds.add(m.id);

const mapas = [];       // placed maps, with drawn points
const sinColocar = [];  // maps without a registration
const aportes = {};     // building -> [{ id, caja, tamano, esquinas (campus frame), counts, centre/yaw (campus frame) }]
// Candidates: registered maps that are not in edificio.json and not in NOTAS_SIN_USAR, i.e. maps the user wants to
// evaluate before adding them. They have no box of their own: the app's box is carried into their frame through the
// drone cloud (the same chain Tools/RegistroDron/caja_dron.js uses), so their face counts are comparable.
// building -> [{ id, caja: { posicion, giro } (candidate frame), counts }]
const candidatos = {};
const diag = [];
const tipoDe = id => (usedIds.has(id) ? 'uso' : NOTAS_SIN_USAR[id] ? 'sinUsar' : 'candidato');
let totalDibujados = 0;

function dedupe(pts) {
    const seen = new Set(), keep = [];
    for (const q of pts) {
        const k = Math.round(q[0] / CELDA_MAPA) * 100003 + Math.round(q[1] / CELDA_MAPA);
        if (seen.has(k)) continue;
        seen.add(k); keep.push(q);
    }
    return keep;
}

const hueOrder = { CienciasBasicas: 0, EIC: 1, Teologia: 2, X1: 3 };
for (const d of BUILDINGS) {
    const dir = p('Escenas', d);
    // Candidates go last: their box comes from the maps the app already uses, which must be processed first.
    const sparse = fs.readdirSync(dir).filter(n => /-sparse\.ply$/.test(n))
        .sort((a, b) => ((tipoDe(+a.split('-')[0]) === 'candidato') - (tipoDe(+b.split('-')[0]) === 'candidato')) || (a < b ? -1 : a > b ? 1 : 0));
    let slot = 0;
    for (const f of sparse) {
        const id = +f.split('-')[0], nombre = f.replace(/^\d+-/, '').replace(/-sparse\.ply$/, '');
        const tipo = tipoDe(id), usado = tipo === 'uso', candidato = tipo === 'candidato';
        const regFile = path.join(dir, `${id}-registro.json`);
        const pts = readSparse(path.join(dir, f));
        if (!fs.existsSync(regFile)) {
            const xs = pts.map(q => q[0]).sort((a, b) => a - b), zs = pts.map(q => q[2]).sort((a, b) => a - b);
            const span = a => a[Math.floor(a.length * 0.98)] - a[Math.floor(a.length * 0.02)];
            sinColocar.push({ id, nombre, edificio: d, puntos: pts.length, ancho: round(span(xs), 1), fondo: round(span(zs), 1), motivo: MOTIVOS[id] || 'Sin registro contra la nube del dron.' });
            continue;
        }
        const reg = readJson(regFile), g = georef(d, reg.dron);
        const T = { yaw: reg.yaw, s: 1, t: reg.t };
        // Every point to the campus frame; far outliers (sky features) leave the drawn set but stay in the count.
        const campus = [];
        for (const q of pts) {
            const dr = R.apply(T, q);
            const c = cloudToCampus(g, dr[0], dr[2]);
            if (Math.hypot(c[0], c[1]) <= RADIO_MAX) campus.push(c);
        }
        const dib = dedupe(campus);
        const ex = new Int16Array(dib.length), en = new Int16Array(dib.length);
        dib.forEach((c, i) => { ex[i] = Math.round(c[0] * 10); en[i] = Math.round(c[1] * 10); });
        const slotIdx = usado ? slot++ : -1;
        const mapaOut = {
            id, nombre, edificio: d, usado, tipo, ranura: slotIdx, nota: usado ? '' : candidato ? 'Nuevo, no está en la app.' : (NOTAS_SIN_USAR[id] || ''),
            total: pts.length, fuera: pts.length - campus.length, dibujados: dib.length,
            registro: {
                nube: reg.dron, inliers25: reg.inliers25, puntos: reg.puntosMapa, rms: round(reg.rms, 3),
                margen: round(reg.margenSobreSegunda, 2), ambiguo: reg.margenSobreSegunda < 1.3, escalaLibre: round(reg.escalaLibre, 3),
            },
            xy: b64(ex) + '|' + b64(en),
        };
        mapas.push(mapaOut);
        totalDibujados += dib.length;
        // Box of this map, if the app has one.
        const cfg = edificios[d].mapas.find(m => m.id === id);
        if (cfg && cfg.caja && cfg.caja.colocada) {
            const tam = edificios[d].tamano, hw = tam[0] / 2, hd = tam[2] / 2;
            const local = boxCorners(cfg.caja.posicion, cfg.caja.giro, hw, hd);
            const esquinas = local.map(([x, z]) => { const dr = R.apply(T, [x, cfg.caja.posicion[1], z]); return cloudToCampus(g, dr[0], dr[2]); });
            const cd = R.apply(T, cfg.caja.posicion);
            const centro = cloudToCampus(g, cd[0], cd[2]);
            const counts = faceCounts(pts, cfg.caja, tam);
            (aportes[d] = aportes[d] || []).push({ id, esquinas, centro, giro: norm360(cfg.caja.giro + reg.yaw), counts, cloudCentre: cd, cloud: reg.dron, g });
        }
        if (candidato) {
            // Carry the app's box into the candidate's frame: drone = rot(map, yaw) + t, so map = rot(drone - t, -yaw) and
            // giro = boxYaw - yaw. The reference is the mean of the boxes of the maps in use that share the candidate's cloud.
            const refs = (aportes[d] || []).filter(a => a.cloud === reg.dron);
            if (!refs.length) {
                log(`AVISO: el candidato ${id} (${d}) no tiene un mapa en uso registrado contra ${reg.dron}; no se pueden contar sus caras.`);
            } else {
                const mean = k => refs.reduce((sum, a) => sum + a.cloudCentre[k], 0) / refs.length;
                const cD = [mean(0), mean(1), mean(2)];
                const sy = refs.reduce((sum, a) => sum + Math.sin(a.giro * K), 0), cy = refs.reduce((sum, a) => sum + Math.cos(a.giro * K), 0);
                const giroD = Math.atan2(sy, cy) / K;
                const pos = R.rot([cD[0] - reg.t[0], cD[1] - reg.t[1], cD[2] - reg.t[2]], -reg.yaw);
                const caja = { posicion: pos, giro: ((giroD - reg.yaw + 540) % 360 + 360) % 360 - 180 };
                const counts = faceCounts(pts, caja, edificios[d].tamano);
                (candidatos[d] = candidatos[d] || []).push({ id, caja, counts });
                mapaOut.caja = { posicion: pos.map(v => round(v, 3)), giro: round(caja.giro, 2) };
                // Where the box lies in the map's frame can be off by metres when the registration is ambiguous.
                diag.push(`  candidato ${id}: caja en su marco (${pos.map(v => v.toFixed(3))}) giro ${caja.giro.toFixed(2)}; puntos por cara ${JSON.stringify(counts)}`);
            }
        }
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Buildings: consensus box in the campus frame, faces, coverage
const CARDINALES = ['N', 'NE', 'E', 'SE', 'S', 'SO', 'O', 'NO'];
const cardinal = deg => CARDINALES[Math.round(norm360(deg) / 45) % 8];
const edificiosOut = BUILDINGS.map(d => {
    const e = edificios[d], A = aportes[d] || [], tam = e.tamano;
    const gps = llToCampus(e.gps.latitud, e.gps.longitud);
    const cen = [A.reduce((s, a) => s + a.centro[0], 0) / A.length, A.reduce((s, a) => s + a.centro[1], 0) / A.length];
    // Circular mean of the box yaw (Unity yaw in the E/N frame).
    const sy = A.reduce((s, a) => s + Math.sin(a.giro * K), 0), cy = A.reduce((s, a) => s + Math.cos(a.giro * K), 0);
    const giro = norm360(Math.atan2(sy, cy) / K);
    const spread = Math.max(...A.map(a => Math.hypot(a.centro[0] - cen[0], a.centro[1] - cen[1])));
    const yawSpread = Math.max(...A.map(a => Math.abs(((a.giro - giro + 540) % 360) - 180)));
    // Corners in the campus frame, from the consensus centre and yaw (frame already E/N; Unity yaw as in register.js).
    const hw = tam[0] / 2, hd = tam[2] / 2;
    const flat = boxCorners([cen[0], 0, cen[1]], giro, hw, hd).map(([x, z]) => [x, z]);
    const caras = CARAS.map(c => {
        const a = flat[c.corner[0]], b = flat[c.corner[1]];
        const mid = [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2];
        const out = [mid[0] - cen[0], mid[1] - cen[1]], len = Math.hypot(...out);
        const normal = [out[0] / len, out[1] / len];
        const largo = tam[c.largo === 0 ? 0 : 2];
        const porMapa = A.map(a2 => ({ mapa: a2.id, puntos: a2.counts[c.clave], densidad: round(a2.counts[c.clave] / largo, 1) }));
        const mejor = porMapa.reduce((m, x) => (x.densidad > m.densidad ? x : m), porMapa[0]);
        // Candidate maps (not in the app): their counts, and the best-map level if they were added.
        const delCandidato = (candidatos[d] || []).map(k => ({ mapa: k.id, puntos: k.counts[c.clave], densidad: round(k.counts[c.clave] / largo, 1) }));
        let conCandidato = null;
        if (delCandidato.length) {
            const mejorCon = delCandidato.reduce((m, x) => (x.densidad > m.densidad ? x : m), mejor);
            const nivelCon = nivelDe(mejorCon.densidad);
            conCandidato = {
                mapa: mejorCon.mapa, puntos: mejorCon.puntos, densidad: mejorCon.densidad, nivel: nivelCon,
                mejora: NIVELES.indexOf(nivelCon) > NIVELES.indexOf(nivelDe(mejor.densidad)),
                // The candidate is the best map of the face, not just the best of the maps in use.
                gana: mejorCon.mapa !== mejor.mapa,
            };
        }
        return {
            clave: c.clave, nombre: c.nombre, largo: round(largo, 1), p0: a.map(v => round(v, 2)), p1: b.map(v => round(v, 2)), centro: mid.map(v => round(v, 2)),
            normal: normal.map(v => round(v, 4)), rumbo: round(norm360(Math.atan2(normal[0], normal[1]) / K), 1), cardinal: cardinal(Math.atan2(normal[0], normal[1]) / K),
            porMapa, candidatos: delCandidato, conCandidato, mejor: mejor.mapa, mejorPuntos: mejor.puntos, mejorDensidad: mejor.densidad, suma: porMapa.reduce((s, x) => s + x.puntos, 0), nivel: nivelDe(mejor.densidad),
        };
    });
    // Box as the drone cloud estimated it, to show how far the field moved it.
    let cajaDron = null, desvio = null;
    const cj = readJson(p('Escenas', d, 'caja-dron.json'));
    const g = A[0].g;
    const dc = cloudToCampus(g, cj.centroEnu[0], cj.centroEnu[1]);
    const dsz = cj.tamano;
    cajaDron = boxCorners([dc[0], 0, dc[1]], cj.giroEnuUnity, dsz[0] / 2, dsz[2] / 2).map(v => v.map(x => round(x, 2)));
    desvio = { metros: round(Math.hypot(cen[0] - dc[0], cen[1] - dc[1]), 2), grados: round(Math.abs(((giro - norm360(cj.giroEnuUnity) + 540) % 360) - 180), 1), tamanoDron: dsz.map(v => round(v, 1)) };
    // Check against the drone box: this map's box centre in its drone cloud frame vs centroEnu.
    for (const a of A) {
        const dist = Math.hypot(a.cloudCentre[0] - cj.centroEnu[0], a.cloudCentre[2] - cj.centroEnu[1]);
        diag.push(`  ${d} ${a.id}: centro de la caja en la nube ${a.cloud} = (${a.cloudCentre[0].toFixed(2)}, ${a.cloudCentre[2].toFixed(2)}) contra centroEnu de caja-dron (${cj.centroEnu[0].toFixed(2)}, ${cj.centroEnu[1].toFixed(2)}): ${(dist * 100).toFixed(1)} cm; giro ${a.giro.toFixed(2)} contra ${norm360(cj.giroEnuUnity).toFixed(2)} (tamaño de la caja ${tam.join(' x ')} contra ${cj.tamano.map(v => v.toFixed(1)).join(' x ')})`);
    }
    return {
        id: d, nombre: e.nombre, tamano: tam, color: hueOrder[d],
        gps: gps.map(v => round(v, 2)), gpsLatLon: [e.gps.latitud, e.gps.longitud],
        centro: cen.map(v => round(v, 2)), giro: round(giro, 2), distGps: round(Math.hypot(cen[0] - gps[0], cen[1] - gps[1]), 2),
        esquinas: flat.map(v => v.map(x => round(x, 2))), esquinasDron: cajaDron, desvioDron: desvio,
        dispersionMapas: { metros: round(spread, 2), grados: round(yawSpread, 2) },
        mapas: A.map(a => a.id), candidatos: (candidatos[d] || []).map(k => k.id), caras,
    };
});

// ---------------------------------------------------------------------------------------------------------------------
// Field attempts
function parseCsv(txt) {
    const rows = []; let row = [], cur = '', q = false;
    for (let i = 0; i < txt.length; i++) {
        const c = txt[i];
        if (q) { if (c === '"') { if (txt[i + 1] === '"') { cur += '"'; i++; } else q = false; } else cur += c; }
        else if (c === '"') q = true;
        else if (c === ',') { row.push(cur); cur = ''; }
        else if (c === '\n' || c === '\r') { if (c === '\r' && txt[i + 1] === '\n') i++; row.push(cur); cur = ''; if (row.length > 1 || row[0] !== '') rows.push(row); row = []; }
        else cur += c;
    }
    if (cur !== '' || row.length) { row.push(cur); rows.push(row); }
    return rows;
}
const idToBuilding = {};
for (const m of mapas) idToBuilding[m.id] = m.edificio;

const sesiones = [], intentos = [];
const visto = new Set();
const regDir = p('Registros');
const fuentesRegistros = [];
if (fs.existsSync(regDir)) {
    const carpetas = fs.readdirSync(regDir).filter(n => fs.statSync(path.join(regDir, n)).isDirectory()).sort();
    for (const c of carpetas) {
        const dir = path.join(regDir, c);
        let modelo = '';
        if (fs.existsSync(path.join(dir, 'telefono.txt'))) modelo = (/^Modelo: (.+)$/m.exec(fs.readFileSync(path.join(dir, 'telefono.txt'), 'utf8')) || [])[1] || '';
        for (const f of fs.readdirSync(dir).filter(n => /^paseo-\d{8}-\d{6}-intentos\.csv$/.test(n)).sort()) {
            const sid = f.slice(6, 21);
            if (visto.has(sid)) continue;
            const rows = parseCsv(fs.readFileSync(path.join(dir, f), 'utf8'));
            const head = rows[0] || [], ix = Object.fromEntries(head.map((k, i) => [k, i]));
            if (ix.gps === undefined) continue; // sessions older than 1.3.2 have no phone position
            const logFile = path.join(dir, `paseo-${sid}.log`);
            let version = '?', build = '';
            if (fs.existsSync(logFile)) {
                const m = /Inicio del paseo; versión ([\w.]+), build (\w+)/.exec(fs.readFileSync(logFile, 'utf8'));
                if (m) { version = m[1]; build = m[2]; }
            }
            const compass = ix.rumbo_ar !== undefined; // since 1.3.5 «rumbo» is the compass heading
            const sIdx = sesiones.length;
            let n = 0, aciertos = 0;
            for (const r of rows.slice(1)) {
                const m = /^(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)$/.exec(r[ix.gps] || '');
                if (!m) continue;
                const pos = llToCampus(+m[1], +m[2]);
                const res = r[ix.resultado] === 'ninguno' ? 0 : +r[ix.resultado];
                const num = k => { const v = r[ix[k]]; return v === undefined || v === '-' || v === '' ? null : +v; };
                n++; if (res) aciertos++;
                intentos.push({
                    s: sIdx, h: r[ix.hora], n: +r[ix.n], busca: r[ix.busca], mapas: +r[ix.mapas], motivo: r[ix.motivo], distGps: num('dist_gps'), seg: num('seg'),
                    res, conf: num('conf'), rd: ix.rumbo_dif !== undefined ? num('rumbo_dif') : null, rumbo: compass ? num('rumbo') : null,
                    foto: r[ix.foto] || '', e: round(pos[0], 1), n2: round(pos[1], 1), prec: num('prec'), util: num('gps_util'),
                });
            }
            visto.add(sid);
            if (!n) continue;
            const dd = sid.slice(0, 8), hh = sid.slice(9, 15);
            sesiones.push({ id: sid, version, build, modelo, fecha: `${dd.slice(0, 4)}-${dd.slice(4, 6)}-${dd.slice(6, 8)}`, hora: `${hh.slice(0, 2)}:${hh.slice(2, 4)}`, intentos: n, aciertos, brujula: compass, fuente: `${c}/${f}` });
            fuentesRegistros.push(`${c}/${f}`);
        }
    }
}
// Attempts of the same session keep the file order; sessions sorted by date.
const orden = sesiones.map((s, i) => i).sort((a, b) => (sesiones[a].fecha + sesiones[a].hora).localeCompare(sesiones[b].fecha + sesiones[b].hora));
const remap = new Map(orden.map((old, nw) => [old, nw]));
const sesionesOrd = orden.map(i => sesiones[i]);
for (const a of intentos) a.s = remap.get(a.s);
intentos.sort((a, b) => a.s - b.s || a.n - b.n);

// In front of which faces was each attempt made?
for (const a of intentos) {
    a.caras = [];
    edificiosOut.forEach((b, bi) => b.caras.forEach((c, ci) => {
        // Nearest point of the face segment to the phone.
        const dx = c.p1[0] - c.p0[0], dy = c.p1[1] - c.p0[1];
        const tt = Math.max(0, Math.min(1, ((a.e - c.p0[0]) * dx + (a.n2 - c.p0[1]) * dy) / (dx * dx + dy * dy)));
        const v = [a.e - (c.p0[0] + tt * dx), a.n2 - (c.p0[1] + tt * dy)], dist = Math.hypot(v[0], v[1]);
        if (dist < FRENTE.min || dist > FRENTE.max) return;
        const ang = Math.acos(Math.max(-1, Math.min(1, (v[0] * c.normal[0] + v[1] * c.normal[1]) / dist))) / K;
        if (ang <= FRENTE.angulo) a.caras.push([bi, ci, round(dist, 1), round(ang, 1)]);
    }));
    a.caras.sort((x, y) => x[2] - y[2]);
}

// ---------------------------------------------------------------------------------------------------------------------
// Drone base layer
const cloudsUsadas = [];
const basePts = [];
for (const d of BUILDINGS) {
    const nube = BASE_CLOUD[d], file = p('Escenas', d, `${nube}.ply`), g = georef(d, `${nube}.ply`);
    const pts = readEnu(file), suelo = readJson(p('Escenas', d, 'caja-dron.json')).baseZ; // height above this building's ground
    cloudsUsadas.push(rel(file));
    for (const q of pts) {
        const c = cloudToCampus(g, q[0], q[1]);
        if (Math.hypot(c[0], c[1]) <= RADIO_BASE) basePts.push([c[0], c[1], q[2] - suelo]);
    }
}
let celdaBase = 0.5, base = [];
for (; ; celdaBase += 0.25) {
    const cells = new Map();
    for (const q of basePts) {
        const k = Math.floor(q[0] / celdaBase) * 100003 + Math.floor(q[1] / celdaBase);
        const old = cells.get(k);
        if (!old || q[2] > old[2]) cells.set(k, q);
    }
    base = [...cells.values()];
    if (base.length <= MAX_BASE) break;
}
const bx = new Int16Array(base.length), by = new Int16Array(base.length), bz = new Uint8Array(base.length);
base.forEach((q, i) => { bx[i] = Math.round(q[0] * 10); by[i] = Math.round(q[1] * 10); bz[i] = Math.max(0, Math.min(255, Math.round(q[2] * 4))); });

// ---------------------------------------------------------------------------------------------------------------------
// View bounds: boxes, attempts and the part of the used maps near the buildings
const xsAll = [], ysAll = [];
for (const b of edificiosOut) for (const q of b.esquinas) { xsAll.push(q[0]); ysAll.push(q[1]); }
for (const a of intentos) { xsAll.push(a.e); ysAll.push(a.n2); }
const mnE = Math.min(...xsAll) - 20, mxE = Math.max(...xsAll) + 20, mnN = Math.min(...ysAll) - 20, mxN = Math.max(...ysAll) + 20;

// ---------------------------------------------------------------------------------------------------------------------
// Page
const mapasOut = mapas.map(m => ({ ...m }));
const datos = {
    generado: (d => d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'))(new Date()),
    origen: { lat: round(latC, 6), lon: round(lonC, 6) },
    encuadre: [mnE, mnN, mxE, mxN].map(v => round(v, 0)),
    banda: BANDA, umbrales: UMBRALES, frente: FRENTE,
    edificios: edificiosOut, mapas: mapasOut, sinColocar,
    base: { celda: celdaBase, n: base.length, e: b64(bx), n2: b64(by), z: b64(bz) },
    sesiones: sesionesOrd, intentos,
    fuentes: {
        edificios: BUILDINGS.map(d => `Assets/AncoRA/Paseo/${d}/edificio.json`),
        nubesMapa: mapas.map(m => `Escenas/${m.edificio}/${m.id}-${m.nombre}-sparse.ply`),
        registros: mapas.map(m => `Escenas/${m.edificio}/${m.id}-registro.json`),
        nubesDron: cloudsUsadas,
        cajasDron: BUILDINGS.map(d => `Escenas/${d}/caja-dron.json`),
        intentos: fuentesRegistros.map(f => `Registros/${f}`),
    },
};

const plantilla = fs.readFileSync(path.join(__dirname, 'plantilla.html'), 'utf8');
// JSON inside a <script>: no raw '<', and no raw line separators.
const BS = String.fromCharCode(92); // a backslash
const json = JSON.stringify(datos).replace(/</g, BS + 'u003c').replace(new RegExp('[' + String.fromCharCode(0x2028, 0x2029) + ']', 'g'), c => BS + 'u' + c.charCodeAt(0).toString(16));
if (!plantilla.includes('/*DATOS*/')) throw new Error('plantilla.html no tiene el marcador /*DATOS*/.');
const html = plantilla.replace('/*DATOS*/', () => json);
fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, html);

log(`Escrito ${OUT} (${(Buffer.byteLength(html) / 1024).toFixed(0)} KB)`);
log(`Mapas colocados: ${mapas.length} (${mapas.filter(m => m.tipo === 'uso').length} en la app, ${mapas.filter(m => m.tipo === 'sinUsar').length} sin usar, ${mapas.filter(m => m.tipo === 'candidato').length} candidatos); sin colocar: ${sinColocar.length}; intentos con GPS: ${intentos.length} en ${sesionesOrd.length} sesiones; base del dron: ${base.length} puntos (celda ${celdaBase} m).`);

if (DIAG) {
    log('\nComprobación de las cajas contra caja-dron.json (nube del dron):');
    diag.forEach(l => log(l));
    log('\nCaja de cada edificio en el marco común (centro este/norte desde el origen) y distancia al gps de edificio.json:');
    for (const b of edificiosOut) {
        log(`  ${b.id}: centro (${b.centro}) giro ${b.giro}; gps (${b.gps}); distancia ${b.distGps} m; mapas coinciden a ${b.dispersionMapas.metros} m / ${b.dispersionMapas.grados}°; contra la caja del dron: ${b.desvioDron.metros} m y ${b.desvioDron.grados}° (dron ${b.desvioDron.tamanoDron.join(' x ')} m, actual ${b.tamano.join(' x ')} m)`);
    }
    log('\nPuntos por cara (banda: ' + JSON.stringify(BANDA) + ')');
    for (const b of edificiosOut) for (const c of b.caras) {
        if (c.conCandidato) log(`  [candidato] ${b.id} ${c.clave}: ${c.candidatos.map(x => `${x.mapa}: ${x.puntos} (${x.densidad}/m)`).join(', ')}; mejor con el candidato ${c.conCandidato.densidad}/m (mapa ${c.conCandidato.mapa}) -> ${c.conCandidato.nivel}${c.conCandidato.mejora ? ' (MEJORA desde ' + c.nivel + ')' : ''}`);
        log(`  ${b.id.padEnd(16)} ${c.clave} ${c.nombre.padEnd(8)} rumbo ${String(c.rumbo).padStart(5)} ${c.cardinal.padEnd(2)} largo ${String(c.largo).padStart(4)}  ` +
            c.porMapa.map(x => `${x.mapa}: ${String(x.puntos).padStart(5)} (${String(x.densidad).padStart(6)}/m)`).join('  ') + `  mejor ${c.mejorDensidad}/m -> ${c.nivel}  suma ${c.suma}`);
    }
    log('\nMapas:');
    for (const m of mapas) log(`  ${m.id} ${m.nombre} (${m.edificio}${m.usado ? '' : m.tipo === 'candidato' ? ', CANDIDATO' : ', sin usar'}): ${m.total} puntos, ${m.dibujados} dibujados, ${m.fuera} fuera de ${RADIO_MAX} m; registro ${m.registro.inliers25}/${m.registro.puntos}, margen ${m.registro.margen}${m.registro.ambiguo ? ' (ambiguo)' : ''}`);
    log('\nSin colocar:');
    for (const m of sinColocar) log(`  ${m.id} ${m.nombre}: ${m.puntos} puntos, ${m.ancho} x ${m.fondo} m. ${m.motivo}`);
    log('\nSesiones:');
    sesionesOrd.forEach((s, i) => log(`  [${i}] ${s.fecha} ${s.hora} v${s.version} ${s.modelo}: ${s.intentos} intentos, ${s.aciertos} ubicaron${s.brujula ? ', con brújula' : ''}`));
    // Run the page's own statistics code (between the LOGICA markers) so the report matches what the page shows.
    const src = plantilla.slice(plantilla.indexOf('/*LOGICA*/'), plantilla.indexOf('/*FIN-LOGICA*/'));
    const ctx = { DATOS: datos, out: null };
    vm.createContext(ctx);
    vm.runInContext(src + '\nout = { stats: estadisticas(DATOS.intentos.map((_, i) => i)), lista: dondeMapear(estadisticas(DATOS.intentos.map((_, i) => i))) };', ctx);
    log('\nIntentos por cara (todos los intentos):');
    DATOS_PRINT(ctx.out.stats);
    log('\nDónde mapear:');
    for (const l of ctx.out.lista.prioridad) log('  PRIORIDAD ' + l.texto);
    for (const l of ctx.out.lista.sinPruebas) log('  SIN PRUEBAS ' + l.texto);
    for (const l of ctx.out.lista.conPuntos) log('  CON PUNTOS ' + l.texto);
    log('\nLo que arreglaría el candidato (caras que suben de nivel):');
    for (const l of ctx.out.lista.arregla) log('  ' + l.texto);
}
function DATOS_PRINT(stats) {
    for (const b of edificiosOut) b.caras.forEach((c, ci) => {
        const s = stats[b.id + '|' + c.clave] || {};
        log(`  ${b.id.padEnd(16)} ${c.clave} ${c.nombre.padEnd(8)} ${c.nivel.padEnd(7)} intentos ${s.total}, aciertos ${s.hits}; con la cámara hacia el edificio ${s.mirTotal}, aciertos ${s.mirHits}`);
    });
}
