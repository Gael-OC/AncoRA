// Cuts one building out of a georeferenced drone cloud that covers the whole campus, so registrar_mapa.js and
// caja_dron.js search near that building only (a campus cloud has four similar buildings to slide onto).
//
//   node Tools/RegistroDron/recortar_nube.js <campus.ply> <campus-georef.json> <salida.ply> --gps=lat,lon [--radio=40]
//   node Tools/RegistroDron/recortar_nube.js <campus.ply> <campus-georef.json> <salida.ply> --centro=E,N [--radio=40]
//
// The cloud is the ASCII PLY of georef_dron.js (x east, y north, z up, metres, origin lat0/lon0 of its georef JSON).
// The cut is a vertical cylinder; the output keeps the input's header and point lines untouched, so every point stays
// in the campus frame.
const fs = require('fs');

function main() {
  const args = process.argv.slice(2);
  const opt = Object.fromEntries(args.filter(a => a.startsWith('--')).map(a => a.slice(2).split('=')));
  const [inPath, georefPath, outPath] = args.filter(a => !a.startsWith('--'));
  if (!outPath || (!opt.gps && !opt.centro)) {
    console.error('Uso: node recortar_nube.js <campus.ply> <campus-georef.json> <salida.ply> --gps=lat,lon|--centro=E,N [--radio=40]');
    process.exit(2);
  }
  const radius = +(opt.radio || 40);
  let center;
  if (opt.centro) center = opt.centro.split(',').map(Number);
  else {
    // Same projection as georef_dron.js: equirectangular around (lat0, lon0).
    const { lat0, lon0 } = JSON.parse(fs.readFileSync(georefPath, 'utf8'));
    const [lat, lon] = opt.gps.split(',').map(Number), k = Math.PI / 180, Re = 6378137;
    center = [(lon - lon0) * k * Re * Math.cos(lat0 * k), (lat - lat0) * k * Re];
  }
  const lines = fs.readFileSync(inPath, 'utf8').split('\n');
  let h = 0; while (!lines[h].startsWith('end_header')) h++;
  const kept = [];
  for (let i = h + 1; i < lines.length; i++) {
    const a = lines[i].split(' '); if (a.length < 3) continue;
    if (Math.hypot(+a[0] - center[0], +a[1] - center[1]) <= radius) kept.push(lines[i]);
  }
  const header = lines.slice(0, h + 1).map(l => l.startsWith('element vertex') ? `element vertex ${kept.length}` : l);
  header.splice(header.length - 1, 0, `comment Recorte de ${radius} m alrededor de E ${center[0].toFixed(2)}, N ${center[1].toFixed(2)} (recortar_nube.js)`);
  fs.writeFileSync(outPath, header.join('\n') + '\n' + kept.join('\n') + '\n');
  console.log(`${outPath}: ${kept.length} de ${lines.length - h - 1} puntos, centro E ${center[0].toFixed(2)} N ${center[1].toFixed(2)}, radio ${radius} m`);
}

main();
