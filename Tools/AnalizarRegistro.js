#!/usr/bin/env node
// Summarises a session log of the tour (registro/paseo-*.log, written by PaseoSessionLog) to see where detection fails:
// attempts per searched building and reason, match rate and seconds per attempt, the filter's verdicts per map, time to
// each building's box, the team's detection tests (DEPURACION_AJUSTE), temperature and battery. Since 1.3.5 also the
// compass: how often the camera heading was usable, how far the searched building lay from it (rumbo_dif) against the
// match rate, and a check of the axis conventions (norte minus the AR heading must stay constant while the phone turns).
//
//   node Tools/AnalizarRegistro.js <paseo-AAAAMMDD-HHMMSS.log> [--csv=intentos.csv]
//
// --csv writes one row per attempt (INTENTO_PASEO joined with DEPURACION_INTENTO by n) for a spreadsheet. In the CSV
// «rumbo» is the compass heading of the camera (INTENTO_PASEO) and «rumbo_ar» the camera's heading in the AR world.

const fs = require('fs');

const args = process.argv.slice(2);
const file = args.find(a => !a.startsWith('--'));
const csvArg = args.find(a => a.startsWith('--csv='));
if (!file) {
    console.error('Uso: node Tools/AnalizarRegistro.js <paseo-*.log> [--csv=intentos.csv]');
    process.exit(2);
}

const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/).filter(Boolean);

// "HH:mm:ss.fff L text": seconds of the day, level letter, text.
function parseLine(line) {
    const m = /^(\d\d):(\d\d):(\d\d)\.(\d{3}) ([IWEXA]) (.*)$/.exec(line);
    if (!m) return null;
    return { t: +m[1] * 3600 + +m[2] * 60 + +m[3] + +m[4] / 1000, level: m[5], text: m[6] };
}

// key=value pairs after a tag such as INTENTO_PASEO.
function fields(text, tag) {
    const at = text.indexOf(tag + ' ');
    if (at < 0) return null;
    const out = {};
    for (const part of text.slice(at + tag.length + 1).split(' ')) {
        const eq = part.indexOf('=');
        if (eq > 0) out[part.slice(0, eq)] = part.slice(eq + 1);
    }
    return out;
}

const num = v => (v === undefined || v === '-' ? NaN : parseFloat(v));
const mean = xs => (xs.length ? xs.reduce((a, b) => a + b, 0) / xs.length : NaN);
const median = xs => {
    if (!xs.length) return NaN;
    const s = [...xs].sort((a, b) => a - b);
    return s.length % 2 ? s[(s.length - 1) / 2] : (s[s.length / 2 - 1] + s[s.length / 2]) / 2;
};
const f1 = x => (Number.isNaN(x) ? '-' : x.toFixed(1));
const pct = (a, b) => (b ? `${Math.round((100 * a) / b)} %` : '-');
const clock = t => {
    const h = Math.floor(t / 3600), m = Math.floor((t % 3600) / 60), s = Math.floor(t % 60);
    return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
};

const attempts = [];
const debugByN = new Map();
const verdicts = new Map(); // map -> {verdict: count}
const shown = [];
const settings = []; // {t, imagen|busqueda}
const states = [];
const photoByN = new Map();
const performance = [];
const tracking = [];
const poses = []; // DEPURACION_POSE samples
let begin = null, version = '-', warnings = 0, errors = 0;
const problems = [];
let image = 'reducida', search = 'por-edificio', compass = 'si';

for (const raw of lines) {
    const line = parseLine(raw);
    if (!line) continue;
    const { t, level, text } = line;
    if (level === 'W') warnings++;
    if (level === 'E' || level === 'X' || level === 'A') {
        errors++;
        if (problems.length < 15) problems.push(`${clock(t)} ${level} ${text}`);
    }
    let v;
    if (text.includes('Inicio del paseo; versión')) version = (/versión ([^,]+)/.exec(text) || [])[1] || version;
    if (text.includes('Comienza el paseo') && begin === null) begin = t;
    if ((v = fields(text, 'INTENTO_PASEO'))) attempts.push({ t, image, search, brujula: compass, ...v });
    else if ((v = fields(text, 'DEPURACION_INTENTO'))) debugByN.set(v.n, v);
    else if ((v = fields(text, 'UBICACION_PASEO'))) {
        const byVerdict = verdicts.get(v.mapa) || {};
        byVerdict[v.veredicto] = (byVerdict[v.veredicto] || 0) + 1;
        verdicts.set(v.mapa, byVerdict);
    } else if ((v = fields(text, 'DEPURACION_AJUSTE'))) {
        if (v.imagen) image = v.imagen;
        if (v.busqueda) search = v.busqueda;
        if (v.brujula) compass = v.brujula;
        settings.push({ t, ...v });
    } else if ((v = fields(text, 'ESTADO_PASEO'))) states.push({ t, ...v });
    else if ((v = fields(text, 'DEPURACION_FOTO'))) photoByN.set(v.n, v.archivo);
    else if ((v = fields(text, 'DEPURACION_RENDIMIENTO'))) performance.push(v);
    else if ((v = fields(text, 'DEPURACION_TRACKING'))) tracking.push({ t, ...v });
    else if ((v = fields(text, 'DEPURACION_POSE'))) poses.push({ t, ...v });
    const show = /Se muestra (.+) con el mapa (\d+)\./.exec(text);
    if (show) shown.push({ t, building: show[1], map: show[2] });
}

const start = parseLine(lines[0])?.t ?? 0;
const end = parseLine(lines[lines.length - 1])?.t ?? start;
console.log(`Registro: ${file}`);
console.log(`Versión ${version} · ${clock(start)}–${clock(end)} (${f1((end - start) / 60)} min) · avisos ${warnings} · errores ${errors}`);
if (begin === null) console.log('No se tocó «Comenzar» (o la versión no lo registra).');

const matched = attempts.filter(a => a.resultado !== 'ninguno');
const secs = xs => xs.map(a => num(a.seg)).filter(x => !Number.isNaN(x));
console.log(`\nIntentos: ${attempts.length} · con coincidencia ${matched.length} (${pct(matched.length, attempts.length)})`);
console.log(`Segundos por intento: con coincidencia ${f1(mean(secs(matched)))} (mediana ${f1(median(secs(matched)))}), ` +
    `sin coincidencia ${f1(mean(secs(attempts.filter(a => a.resultado === 'ninguno'))))} ` +
    `(mediana ${f1(median(secs(attempts.filter(a => a.resultado === 'ninguno'))))})`);

function table(title, keyOf) {
    const groups = new Map();
    for (const a of attempts) {
        const key = keyOf(a);
        if (!groups.has(key)) groups.set(key, []);
        groups.get(key).push(a);
    }
    console.log(`\n${title}`);
    console.log('  grupo                          intentos  aciertos   seg medio  seg sin coincidencia');
    for (const [key, list] of [...groups].sort((a, b) => b[1].length - a[1].length)) {
        const hits = list.filter(a => a.resultado !== 'ninguno');
        const misses = list.filter(a => a.resultado === 'ninguno');
        console.log(`  ${key.padEnd(30)} ${String(list.length).padStart(8)}  ${pct(hits.length, list.length).padStart(8)}  ` +
            `${f1(mean(secs(list))).padStart(10)}  ${f1(mean(secs(misses))).padStart(20)}`);
    }
}

if (attempts.length) {
    table('Por edificio buscado:', a => a.busca);
    table('Por motivo de la búsqueda:', a => a.motivo);
    table('Por mapa encontrado:', a => (a.resultado === 'ninguno' ? 'ninguno' : `mapa ${a.resultado}`));
    if (settings.length) {
        const withCompass = settings.some(x => x.brujula);
        table(withCompass ? 'Por prueba de detección (imagen / búsqueda / brújula):' : 'Por prueba de detección (imagen / búsqueda):',
            a => `${a.image} / ${a.search}${withCompass ? ' / brújula ' + a.brujula : ''}`);
    }
    if (attempts.some(a => a.rumbo_dif !== undefined)) {
        const bucket = a => {
            const d = num(a.rumbo_dif);
            return Number.isNaN(d) ? 'sin dato (sin rumbo, GPS o edificio)' : d <= 35 ? '≤ 35° (edificio de frente)' : d <= 90 ? '35–90°' : '> 90° (edificio detrás o al lado)';
        };
        table('Por diferencia entre el rumbo de la cámara y la dirección GPS al edificio buscado (rumbo_dif):', bucket);
    }
    const inView = attempts.filter(a => debugByN.get(a.n)?.vista && debugByN.get(a.n).vista !== '-');
    if (debugByN.size) table('Por edificio a la vista (DEPURACION_INTENTO):', a => debugByN.get(a.n)?.vista ?? '-');
    if (debugByN.size && !inView.length) console.log('  (ningún intento con un edificio ubicado a la vista)');
}

// The compass (1.3.5): is the heading usable, and does it turn with the AR camera?
const compassPoses = poses.filter(x => x.norte_estado !== undefined);
if (compassPoses.length) {
    const valid = compassPoses.filter(x => !Number.isNaN(num(x.norte)));
    const states = {};
    for (const x of compassPoses) states[x.norte_estado] = (states[x.norte_estado] || 0) + 1;
    const fieldUt = compassPoses.map(x => num(x.campo_ut)).filter(x => !Number.isNaN(x));
    console.log(`\nBrújula (DEPURACION_POSE): rumbo utilizable en ${valid.length} de ${compassPoses.length} muestras (${pct(valid.length, compassPoses.length)}); ` +
        `estados ${Object.entries(states).map(([k, c]) => `${k} ${c}`).join(', ')}; campo medio ${f1(mean(fieldUt))} µT`);
    // norte minus the AR heading is constant while the AR world does not move: its spread says whether the compass turns with the camera.
    const segments = [];
    let current = [];
    let last = null;
    for (const x of valid) {
        const yaw = num(x.rumbo);
        const ok = x.tracking === 'SessionTracking' && !Number.isNaN(yaw);
        if (!ok || (last !== null && x.t - last > 5)) {
            if (current.length) segments.push(current);
            current = [];
        }
        if (ok) current.push((num(x.norte) - yaw) * Math.PI / 180);
        last = x.t;
    }
    if (current.length) segments.push(current);
    const lines = [];
    for (const seg of segments.filter(g => g.length >= 10)) {
        const c = mean(seg.map(Math.cos)), sn = mean(seg.map(Math.sin));
        const r = Math.hypot(c, sn);
        const sigma = Math.sqrt(-2 * Math.log(Math.max(r, 1e-9))) * 180 / Math.PI;
        const offset = ((Math.atan2(sn, c) * 180 / Math.PI) + 360) % 360;
        lines.push(`  ${seg.length} muestras: norte − rumbo AR = ${offset.toFixed(0)}° ± ${sigma.toFixed(0)}°`);
    }
    if (lines.length) {
        console.log('Norte menos el rumbo AR por tramo continuo con tracking (± pequeño = la brújula gira con la cámara; ± grande = ejes mal):');
        for (const l of lines) console.log(l);
    }
}

if (verdicts.size) {
    console.log('\nVeredictos del filtro por mapa (UBICACION_PASEO):');
    for (const [map, byVerdict] of verdicts)
        console.log(`  mapa ${map}: ${Object.entries(byVerdict).map(([k, c]) => `${k} ${c}`).join(', ')}`);
}

if (shown.length) {
    console.log('\nCajas mostradas (desde «Comenzar»):');
    const firstByBuilding = new Map();
    for (const s of shown) if (!firstByBuilding.has(s.building)) firstByBuilding.set(s.building, s);
    for (const [building, s] of firstByBuilding)
        console.log(`  ${building}: primera vez a ${begin === null ? clock(s.t) : f1(s.t - begin) + ' s'} (mapa ${s.map}); ` +
            `mostrada ${shown.filter(x => x.building === building).length} vez/veces`);
} else console.log('\nNinguna caja se mostró.');

if (states.length) {
    const first = states[0], last = states[states.length - 1];
    const thermal = Math.max(...states.map(s => num(s.termico)).filter(x => !Number.isNaN(x)));
    console.log(`\nBatería ${first.bateria} % → ${last.bateria} % · estado térmico máximo ${Number.isFinite(thermal) ? thermal : '-'}` +
        ' (3 = el teléfono ya baja el rendimiento)');
}

if (performance.length) {
    const fps = performance.map(p => num(p.fps)).filter(x => !Number.isNaN(x));
    const worst = performance.map(p => num(p.peor_ms)).filter(x => !Number.isNaN(x));
    const mem = performance.map(p => num(p.mem_mb)).filter(x => !Number.isNaN(x));
    console.log(`Rendimiento: ${f1(mean(fps))} fps en promedio (mínimo ${f1(Math.min(...fps))}), peor cuadro ${Math.max(...worst)} ms, ` +
        `memoria hasta ${Math.max(...mem)} MB`);
}
if (tracking.length) {
    const lost = tracking.filter(x => x.estado !== 'SessionTracking');
    console.log(`Tracking: ${tracking.length} cambios; sin tracking ${lost.length} veces (${[...new Set(lost.map(x => x.motivo))].join(', ') || '-'})`);
}
if (photoByN.size) {
    const missed = attempts.filter(a => a.resultado === 'ninguno' && photoByN.has(a.n)).map(a => photoByN.get(a.n));
    console.log(`Fotos de depuración: ${photoByN.size} (carpeta imagenes/), ${missed.length} de intentos sin coincidencia` +
        (missed.length ? `, por ejemplo ${missed.slice(0, 5).join(', ')}` : ''));
}

if (problems.length) {
    console.log('\nPrimeros errores:');
    for (const p of problems) console.log('  ' + p);
}

if (csvArg) {
    const out = csvArg.slice('--csv='.length);
    // DEPURACION_INTENTO's «rumbo» is the camera heading in the AR world; INTENTO_PASEO's is the compass heading.
    const debugKeys = ['img', 'cam', 'rumbo', 'tracking', 'vista', 'ritmo', 'gps', 'prec', 'edad', 'gps_util'];
    const header = ['hora', 'n', 'busca', 'mapas', 'motivo', 'dist_gps', 'seg', 'resultado', 'conf', 'rumbo', 'rumbo_dif', 'imagen', 'busqueda', 'brujula',
        ...debugKeys.map(k => (k === 'rumbo' ? 'rumbo_ar' : k)), 'foto'];
    const rows = attempts.map(a => {
        const d = debugByN.get(a.n) || {};
        return [clock(a.t), a.n, a.busca, a.mapas, a.motivo, a.dist_gps, a.seg, a.resultado, a.conf, a.rumbo, a.rumbo_dif, a.image, a.search, a.brujula,
            ...debugKeys.map(k => d[k] ?? ''), photoByN.get(a.n) ?? ''].map(x => `"${String(x ?? '').replace(/"/g, '""')}"`).join(',');
    });
    fs.writeFileSync(out, [header.join(','), ...rows].join('\n') + '\n');
    console.log(`\nCSV con ${rows.length} intentos: ${out}`);
}
