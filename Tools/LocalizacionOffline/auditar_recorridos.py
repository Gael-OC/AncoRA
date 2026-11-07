"""Preserve supplied exports, verify ZIP contents, audit whole sessions; no pose ground truth."""
import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

import cv2
import numpy as np
from PIL import Image, ImageDraw
from datos import inventory, recording_groups, write_json


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def stats(values):
    return dict(zip(('min', 'median', 'p95', 'max'), map(float, np.quantile(values, [0, .5, .95, 1])))) if len(values) else None


def preserve(sources, out):
    records, zip_members, directories = [], [], []
    for index, source in enumerate(sources):
        source = Path(source).resolve()
        for path in sorted(source.rglob('*')) if source.is_dir() else [source]:
            relative = path.relative_to(source) if source.is_dir() else Path(path.name)
            target = out / 'originales' / str(index) / relative
            if path.is_dir():
                target.mkdir(parents=True, exist_ok=True)
                directories.append(dict(source=str(path), copy=str(target.resolve())))
                continue
            if not path.is_file():
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            expected = sha(path)
            if not target.exists():
                shutil.copy2(path, target)
            if sha(target) != expected:
                raise ValueError(f'Copia distinta: {path}')
            records.append(dict(source=str(path), copy=str(target.resolve()), sha256=expected, bytes=path.stat().st_size))
    by_hash = defaultdict(list)
    for r in records:
        by_hash[r['sha256']].append(r['source'])
    for r in records:
        if Path(r['copy']).suffix.lower() != '.zip':
            continue
        with zipfile.ZipFile(r['copy']) as archive:
            for entry in archive.infolist():
                if entry.is_dir():
                    continue
                # Read through EOF to check CRC, without trusting paths or extracting executable content.
                with archive.open(entry) as stream:
                    digest = hashlib.file_digest(stream, 'sha256').hexdigest()
                zip_members.append(dict(archive=r['source'], member=entry.filename, sha256=digest,
                    bytes=entry.file_size, extracted_sources=by_hash.get(digest, [])))
    write_json(out/'procedencia.json', records)
    write_json(out/'carpetas.json', directories)
    write_json(out/'zip-miembros.json', zip_members)
    write_json(out/'duplicados-exportacion.json', {h:p for h,p in by_hash.items() if len(p)>1})
    print(f'{len(records)} copias verificadas; {len(zip_members)} miembros ZIP con CRC y SHA-256', flush=True)


def audit(out):
    root = out/'originales'
    data = inventory(root, skip_missing=True)
    write_json(out/'inventario.json', data)
    frame_by_path = {f['path']: f for f in data['frames']}
    summaries, sheets, roles, image_metrics = [], defaultdict(list), {}, []
    for group in recording_groups(root):
        log = group['logs'][0]
        rows = [entry[0] for entry in group['entries']]
        if not rows:
            continue
        types = sorted({r.get('tipo_captura','') for r in rows})
        role = {'construccion-arcore':'construction','barrido-arcore':'query'}.get(types[0], 'ambiguous') if len(types)==1 else 'ambiguous'
        sessions = sorted({r.get('sesion',log.parent.name) for r in rows})
        if len(sessions)!=1:
            role='ambiguous'
        for s in sessions:
            if s in roles and roles[s]!=role:
                raise ValueError(f'Roles contradictorios: {s}')
            roles[s]=role
        file_order = [r['captura_t'] for r in rows]
        rows = sorted(rows, key=lambda r: r['captura_t'])
        times = np.array([r['captura_t'] for r in rows]); dt=np.diff(times)
        sensor=np.array([r['timestamp_sensor'] for r in rows]); pose=np.array([r['timestamp_pose_sensor'] for r in rows])
        filenames=[r['archivo'] for r in rows]
        actual={p.name for p in log.parent.glob('*.png')}
        s=dict(session=sessions[0], log=str(log.resolve()), source_logs=[str(p.resolve()) for p in group['logs']],
            overlapping_export_rows=group['overlapping_export_rows'], role=role, types=types,
            phones=sorted({r.get('telefono','') for r in rows}), buildings=sorted({r.get('edificio_punto','') for r in rows}),
            points=sorted({r.get('punto','') for r in rows}), light=sorted({r.get('luz','') for r in rows}),
            rows=len(rows), png=len(actual), duplicate_rows=len(rows)-len(set(filenames)),
            missing=sorted(set(filenames)-actual), orphan=sorted(actual-set(filenames)),
            duration=float(times[-1]-times[0]), fps=float((len(rows)-1)/(times[-1]-times[0])),
            dt=stats(dt), jsonl_order_monotonic=bool((np.diff(file_order)>0).all()),
            monotonic=bool((dt>0).all()), sensor_monotonic=bool((np.diff(sensor)>0).all()),
            sensor_pose_max_abs=float(abs(sensor-pose).max()), sensor_capture_delta_error=stats(abs(np.diff(sensor)-dt)),
            tracking=dict(Counter(r.get('tracking') for r in rows)), epochs=sorted({r.get('epoca_tracking') for r in rows}),
            intrinsics=[json.loads(k) for k in sorted({json.dumps(r['intrinsecos'],sort_keys=True) for r in rows})],
            dimensions=sorted({(r['ancho'],r['alto']) for r in rows}),
            optical_pose_validated=any(r.get('pose_optica_validada',False) for r in rows),
            gaps_over_1s=int((dt>1).sum()), first_wall=rows[0]['hora'], last_wall=rows[-1]['hora'])
        metrics=[]
        for path in sorted(log.parent.glob('*.png')):
            with Image.open(path) as im:
                im.verify()
            image=cv2.imread(str(path),cv2.IMREAD_UNCHANGED)
            if image is None: raise ValueError(path)
            f=frame_by_path.get(str(path.resolve()))
            if f and image.shape[:2]!=(f['height'],f['width']): raise ValueError(f'Dimensiones: {path}')
            small=cv2.resize(image,(480,270),interpolation=cv2.INTER_AREA)
            m=dict(session=sessions[0],file=path.name,sha256=sha(path),shape=list(image.shape),
                laplacian_480=float(cv2.Laplacian(small,cv2.CV_64F).var()),
                black_fraction=float((image<=2).mean()),white_fraction=float((image>=253).mean()))
            metrics.append(m)
        for key in ['laplacian_480','black_fraction','white_fraction']:
            s[key]=stats([m[key] for m in metrics])
        image_metrics.extend(metrics); summaries.append(s)
        files=sorted(log.parent.glob('*.png'))
        for i in sorted(set(np.linspace(0,len(files)-1,min(7,len(files))).round().astype(int))):
            sheets[s['buildings'][0]].append((sessions[0],role,files[i]))
    frames=data['frames']
    groups=defaultdict(list)
    for f in frames:
        groups[f['sha256']].append(f)
    collisions=[dict(sha256=h,frames=[f['id'] for f in fs]) for h,fs in groups.items() if len({roles[f['session']] for f in fs})>1]
    write_json(out/'auditoria.json',dict(sessions=summaries,hash_role_collisions=collisions,missing=data['missing'],unreferenced=data['unreferenced_images']))
    write_json(out/'calidad-imagenes.json',image_metrics)
    queries=[f for f in frames if roles[f['session']]=='query']
    ambiguous=[f for f in frames if roles[f['session']]=='ambiguous']
    write_json(out/'consultas.json',dict(role='query',frames=queries,ground_truth=None))
    write_json(out/'ambiguas.json',dict(role='ambiguous',frames=ambiguous))
    for building in sorted({f['building'] for f in frames}):
        construction=[f for f in frames if f['building']==building and roles[f['session']]=='construction']
        blocked={f['sha256'] for f in queries+ambiguous}
        construction=[f for f in construction if f['sha256'] not in blocked]
        write_json(out/f'{building}-disponibles.json',dict(role='construction',frames=construction,
            excluded_sessions=sorted({f['session'] for f in queries+ambiguous}),excluded_hashes=sorted(blocked),
            selection='All full sessions classified only by consistent tipo_captura; arbitrary scale.'))
    for building,samples in sheets.items():
        # Seven ordinal views per session; previews rotate only for human inspection.
        canvas=Image.new('RGB',(7*200,((len(samples)+6)//7)*410),'#20252b');draw=ImageDraw.Draw(canvas)
        for n,(session,role,path) in enumerate(samples):
            im=Image.open(path).convert('RGB').rotate(-90,expand=True); im.thumbnail((194,345))
            x=n%7*200;y=n//7*410
            canvas.paste(im,(x,y+60));draw.text((x+3,y+3),session[13:28],fill='white');draw.text((x+3,y+19),role,fill='white');draw.text((x+3,y+35),path.name,fill='white')
        canvas.save(out/f'muestras-{building}.jpg',quality=85)
    starts={};log_lines=[]
    for path in sorted(root.rglob('*.log')):
        for line in path.read_text(errors='replace').splitlines():
            if any(t in line for t in ['GRABACION_','Grabaciones copiadas','CAPTURA','tracking','Tracking']):
                log_lines.append(dict(log=str(path.resolve()),line=line))
            if 'GRABACION_INICIO' in line:
                name=line.split('carpeta=')[-1].strip().split('/')[-1];starts[name]=line
    write_json(out/'eventos-registros.json',log_lines)
    write_json(out/'sesiones-log-sin-carpeta.json',{s:l for s,l in starts.items() if s not in roles and '20261105' in s})
    print(Counter((s['buildings'][0],s['role']) for s in summaries),flush=True)
    print('Log sin carpeta:',{s:l for s,l in starts.items() if s not in roles and '20261105' in s},flush=True)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source',action='append',default=[])
    parser.add_argument('--output',required=True)
    parser.add_argument('--audit-only',action='store_true')
    args=parser.parse_args();out=Path(args.output).resolve();out.mkdir(parents=True,exist_ok=True)
    if not args.audit_only: preserve(args.source,out)
    audit(out)


if __name__=='__main__': main()
