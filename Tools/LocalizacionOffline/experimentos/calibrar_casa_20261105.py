"""Read-only source audit and checkerboard detection; never feeds mapping manifests."""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

import cv2
import numpy as np
from PIL import Image, ImageDraw

PATTERN = (9, 6)

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--source', type=Path, required=True)
    ap.add_argument('--output', type=Path, required=True)
    ap.add_argument('--heldout-session', action='append', default=[])
    ap.add_argument('--square-m-y', type=float)
    args = ap.parse_args()
    assert args.square_m_y in (None, .024), 'This experiment fits a square 24 mm board only.'
    args.output.mkdir(parents=True, exist_ok=False)
    cv2.setNumThreads(2)
    sources = [{'path': str(p), 'bytes': p.stat().st_size, 'sha256': sha(p)}
               for p in sorted(args.source.rglob('*')) if p.is_file()]
    (args.output / 'procedencia.json').write_text(json.dumps(sources, indent=2))
    shutil.copy2(__file__, args.output / Path(__file__).name)
    sessions = sorted((args.source / 'grabaciones').glob('*/intentos.jsonl'))
    # Freeze whole-session separation before detecting corners or fitting any model.
    assert set(args.heldout_session) <= {p.parent.name for p in sessions}
    split = {p.parent.name: ('heldout' if p.parent.name in args.heldout_session else 'fit')
             if args.heldout_session else ('fit' if i < 3 else 'heldout') for i, p in enumerate(sessions)}
    assert set(split.values()) == {'fit', 'heldout'}
    (args.output / 'particion.json').write_text(json.dumps({
        'reason': 'Explicit heldout sessions fixed before corner detection; other sessions fit.' if args.heldout_session
                  else 'First three sessions fit; remaining four held out before corner detection. Operator roles unknown.',
        'role': 'calibration_only_never_mapping', 'sessions': split,
        'square_measured_one_direction_m': .024, 'other_direction_m': args.square_m_y,
        'uncertainty_m': None, 'assumption': 'Planarity unverified; square dimensions reported in both directions.'
        if args.square_m_y == .024 else 'Square and planar board, provisional pending second direction measurement.'}, indent=2))
    allframes = []
    inventory = []
    sheet = Image.new('RGB', (5 * 290, len(sessions) * 400), 'white')
    draw = ImageDraw.Draw(sheet)
    for si, meta in enumerate(sessions):
        rows = [json.loads(l) for l in meta.read_text().splitlines() if l.strip()]
        names = [r['archivo'] for r in rows]
        assert len(names) == len(set(names)), meta
        actual = {p.name for p in meta.parent.glob('*.png')}
        assert actual == set(names), (meta, actual.symmetric_difference(names))
        picks = np.linspace(0, len(rows)-1, 5).round().astype(int).tolist()
        summary = {'session': meta.parent.name, 'split': split[meta.parent.name], 'count': len(rows),
                   'phones': sorted({r['telefono'] for r in rows}), 'detected': 0,
                   'jsonl_order_monotonic': all(b['timestamp_sensor'] > a['timestamp_sensor'] for a,b in zip(rows, rows[1:]))}
        for i, r in enumerate(rows):
            p = meta.parent / r['archivo']
            gray = cv2.imread(str(p), cv2.IMREAD_GRAYSCALE)
            assert gray is not None and gray.shape == (r['alto'],r['ancho'])
            ok, corners = cv2.findChessboardCornersSB(gray, PATTERN, cv2.CALIB_CB_NORMALIZE_IMAGE)
            d = {'path': str(p), 'sha256': sha(p), 'session': meta.parent.name, 'split': split[meta.parent.name],
                 'metadata': r, 'detected': bool(ok), 'corners': corners.reshape(-1,2).tolist() if ok else None}
            if ok:
                pts = corners.reshape(-1,2)
                d.update({'bounds_px': [*pts.min(axis=0).tolist(), *pts.max(axis=0).tolist()],
                          'hull_area_fraction': cv2.contourArea(cv2.convexHull(pts))/(gray.size),
                          'center_normalized': (pts.mean(axis=0)/[r['ancho'],r['alto']]).tolist()})
                summary['detected'] += 1
            allframes.append(d)
            if i in picks:
                rgb = cv2.cvtColor(gray,cv2.COLOR_GRAY2BGR)
                if ok: cv2.drawChessboardCorners(rgb,PATTERN,corners,ok)
                im = Image.fromarray(cv2.cvtColor(cv2.rotate(rgb,cv2.ROTATE_90_CLOCKWISE),cv2.COLOR_BGR2RGB))
                im.thumbnail((280,350))
                x,y = picks.index(i)*290,si*400
                sheet.paste(im,(x,y+42))
                draw.text((x+4,y+2),meta.parent.name[17:]+' '+split[meta.parent.name],fill='black')
                draw.text((x+4,y+20),r['archivo']+(' OK' if ok else ' NO'),fill='black')
        inventory.append(summary)
        print(json.dumps(summary),flush=True)
    sheet.save(args.output/'contactos.jpg',quality=92)
    (args.output/'detecciones.json').write_text(json.dumps(allframes,indent=2))
    (args.output/'inventario.json').write_text(json.dumps(inventory,indent=2))
    zipchecks=[]
    for zp in args.source.glob('*.zip'):
        with zipfile.ZipFile(zp) as z:
            assert z.testzip() is None
            for info in z.infolist():
                if info.is_dir(): continue
                loose=args.source / zp.stem / info.filename
                digest=hashlib.sha256(z.read(info)).hexdigest()
                zipchecks.append({'zip':str(zp),'member':info.filename,'sha256':digest,
                                  'loose_match':loose.is_file() and sha(loose)==digest})
    (args.output/'zip-verificacion.json').write_text(json.dumps(zipchecks,indent=2))
    assert all(sha(Path(p['path']))==p['sha256'] for p in sources), 'Source changed'
    print('Original hashes unchanged; frames',len(allframes),flush=True)

if __name__ == '__main__':
    main()
