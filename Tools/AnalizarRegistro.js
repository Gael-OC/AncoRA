#!/usr/bin/env node
// Summarises a session log of the tour (registro/paseo-*.log, written by PaseoSessionLog) to see where detection fails:
// attempts per searched building and reason, match rate and seconds per attempt, the filter's verdicts per map, time to
// each building's box, temperature and battery. Logs of older versions are read too: fields this version no longer writes
// (rumbo, rumbo_dif, contraste, solver, prior, realce_ms, the compass of DEPURACION_POSE, the team's tests) are ignored.
//
// Time spent with the app paused (DEPURACION_APP pausa=1 ... pausa=0) is not the app's: an attempt or a performance sample
// that spans one is left out of the seconds and frame statistics (an attempt still counts as a hit or a miss), and the
// summary says how many were left out.
//
//   node Tools/AnalizarRegistro.js <paseo-AAAAMMDD-HHMMSS.log> [--csv=intentos.csv]
//
// --csv writes one row per attempt (INTENTO_PASEO joined with DEPURACION_INTENTO by n) for a spreadsheet. «rumbo_ar» is the
// camera's heading in the AR world (DEPURACION_INTENTO); «en_pausa» is 1 for an attempt that spans a pause.

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
// Linear-interpolation percentile (p in 0-100) of a list of numbers; NaN for an empty list.
const percentile = (xs, p) => {
    if (!xs.length) return NaN;
    const s = [...xs].sort((a, b) => a - b);
    const at = ((s.length - 1) * p) / 100;
    const lo = Math.floor(at), hi = Math.ceil(at);
    return s[lo] + (s[hi] - s[lo]) * (at - lo);
};
const pct = (a, b) => (b ? `${Math.round((100 * a) / b)} %` : '-');
const clock = t => {
    const h = Math.floor(t / 3600), m = Math.floor((t % 3600) / 60), s = Math.floor(t % 60);
    return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
};

const attempts = [];
const debugByN = new Map();
const verdicts = new Map(); // map -> {verdict: count}
const shown = [];
const settings = []; // {t, imagen|busqueda}, from logs of the versions that had those tests
const states = [];
const photoByN = new Map();
const performance = [];
const tracking = [];
const pauses = []; // {from, to}: seconds of the day while the app was paused
let begin = null, version = '-', warnings = 0, errors = 0;
const problems = [];
let image = 'reducida', search = 'por-edificio';
let pausedSince = null;

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
    if ((v = fields(text, 'INTENTO_PASEO'))) attempts.push({ t, image, search, ...v });
    else if ((v = fields(text, 'DEPURACION_INTENTO'))) debugByN.set(v.n, v);
    else if ((v = fields(text, 'UBICACION_PASEO'))) {
        const byVerdict = verdicts.get(v.mapa) || {};
        byVerdict[v.veredicto] = (byVerdict[v.veredicto] || 0) + 1;
        verdicts.set(v.mapa, byVerdict);
    } else if ((v = fields(text, 'DEPURACION_AJUSTE'))) {
        if (v.imagen) image = v.imagen;
        if (v.busqueda) search = v.busqueda;
        if (v.imagen || v.busqueda) settings.push({ t, ...v });
    } else if ((v = fields(text, 'DEPURACION_APP'))) {
        if (v.pausa === '1' && pausedSince === null) pausedSince = t;
        else if (v.pausa === '0' && pausedSince !== null) {
            pauses.push({ from: pausedSince, to: t });
            pausedSince = null;
        }
    } else if ((v = fields(text, 'ESTADO_PASEO'))) states.push({ t, ...v });
    else if ((v = fields(text, 'DEPURACION_FOTO'))) photoByN.set(v.n, v.archivo);
    else if ((v = fields(text, 'DEPURACION_RENDIMIENTO'))) performance.push({ t, ...v });
    else if ((v = fields(text, 'DEPURACION_TRACKING'))) tracking.push({ t, ...v });
    const show = /Se muestra (.+) con el mapa (\d+)\./.exec(text);
    if (show) shown.push({ t, building: show[1], map: show[2] });
}

if (pausedSince !== null) pauses.push({ from: pausedSince, to: Infinity });

// An attempt ran from t - seg to t (INTENTO_PASEO is written when it ends); paused when that span meets a pause.
const spans = (from, to) => pauses.some(p => to >= p.from && from <= p.to);
for (const a of attempts) a.paused = spans(a.t - (num(a.seg) || 0), a.t);

const start = parseLine(lines[0])?.t ?? 0;
const end = parseLine(lines[lines.length - 1])?.t ?? start;
console.log(`Registro: ${file}`);
console.log(`Versión ${version} · ${clock(start)}–${clock(end)} (${f1((end - start) / 60)} min) · avisos ${warnings} · errores ${errors}`);
if (begin === null) console.log('No se tocó «Comenzar» (o la versión no lo registra).');

const matched = attempts.filter(a => a.resultado !== 'ninguno');
// Seconds of the attempts that did not span a pause of the app.
const secs = xs => xs.filter(a => !a.paused).map(a => num(a.seg)).filter(x => !Number.isNaN(x));
const pausedAttempts = attempts.filter(a => a.paused).length;
const pausedTotal = pauses.reduce((sum, p) => sum + (Number.isFinite(p.to) ? p.to - p.from : end - p.from), 0);
console.log(`\nIntentos: ${attempts.length} · con coincidencia ${matched.length} (${pct(matched.length, attempts.length)})`);
console.log(`Segundos por intento: con coincidencia ${f1(mean(secs(matched)))} (mediana ${f1(median(secs(matched)))}), ` +
    `sin coincidencia ${f1(mean(secs(attempts.filter(a => a.resultado === 'ninguno'))))} ` +
    `(mediana ${f1(median(secs(attempts.filter(a => a.resultado === 'ninguno'))))}, p95 ${f1(percentile(secs(attempts.filter(a => a.resultado === 'ninguno')), 95))})`);
if (pauses.length)
    console.log(`App en pausa ${pauses.length} vez/veces (${f1(pausedTotal)} s en total): ${pausedAttempts} intento(s) que la cruzan quedan fuera de los segundos ` +
        `(no de los aciertos); ver DEPURACION_APP.`);
const rmses = matched.map(a => num(a.rmse)).filter(x => !Number.isNaN(x));
if (rmses.length) console.log(`Error de reproyección (rmse) de las coincidencias: mediana ${median(rmses).toFixed(3)}, p95 ${percentile(rmses, 95).toFixed(3)}`);

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
    if (settings.length) table('Por prueba de detección de versiones anteriores (imagen / búsqueda):', a => `${a.image} / ${a.search}`);
    const inView = attempts.filter(a => debugByN.get(a.n)?.vista && debugByN.get(a.n).vista !== '-');
    if (debugByN.size) table('Por edificio a la vista (DEPURACION_INTENTO):', a => debugByN.get(a.n)?.vista ?? '-');
    if (debugByN.size && !inView.length) console.log('  (ningún intento con un edificio ubicado a la vista)');
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
    // A sample covers the time since the previous one (5 s); one that meets a pause of the app measures the pause, not the frames.
    const usable = performance.filter((p, i) => !spans(i > 0 ? performance[i - 1].t : p.t - 5, p.t));
    const fps = usable.map(p => num(p.fps)).filter(x => !Number.isNaN(x));
    const worst = usable.map(p => num(p.peor_ms)).filter(x => !Number.isNaN(x));
    const mem = performance.map(p => num(p.mem_mb)).filter(x => !Number.isNaN(x));
    console.log(`Rendimiento: ${f1(mean(fps))} fps en promedio (mínimo ${f1(Math.min(...fps))}), peor cuadro ${Math.max(...worst)} ms, ` +
        `memoria hasta ${Math.max(...mem)} MB` +
        (usable.length < performance.length ? ` · ${performance.length - usable.length} de ${performance.length} muestras cruzan una pausa de la app y no cuentan` : ''));
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
    // DEPURACION_INTENTO's «rumbo» is the camera heading in the AR world.
    const debugKeys = ['img', 'cam', 'rumbo', 'tracking', 'vista', 'ritmo', 'gps', 'prec', 'edad', 'gps_util'];
    const header = ['hora', 'n', 'busca', 'mapas', 'motivo', 'dist_gps', 'seg', 'resultado', 'conf', 'rmse', 'imagen', 'busqueda',
        ...debugKeys.map(k => (k === 'rumbo' ? 'rumbo_ar' : k)), 'foto', 'en_pausa'];
    const rows = attempts.map(a => {
        const d = debugByN.get(a.n) || {};
        return [clock(a.t), a.n, a.busca, a.mapas, a.motivo, a.dist_gps, a.seg, a.resultado, a.conf, a.rmse, a.image, a.search,
            ...debugKeys.map(k => d[k] ?? ''), photoByN.get(a.n) ?? '', a.paused ? 1 : 0].map(x => `"${String(x ?? '').replace(/"/g, '""')}"`).join(',');
    });
    fs.writeFileSync(out, [header.join(','), ...rows].join('\n') + '\n');
    console.log(`\nCSV con ${rows.length} intentos: ${out}`);
}
