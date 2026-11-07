"""Physical facade/window identity constraints; construction-only, arbitrary-scale SfM."""
import argparse
from collections import Counter
import hashlib
from itertools import combinations
from pathlib import Path
import shutil
import sys

import numpy as np

T = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(T))
from datos import read_json, write_json
from sfm_aprendido import construction_frames
from sfm_diagnostico import labels


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def upright_coordinates(xy, width, height):
    """PIL rotate(-90): (u,v)=(H-1-y,x), normalized at pixel centres."""
    xy = np.asarray(xy, dtype=float).reshape(-1, 2)
    return np.c_[(height-1-xy[:, 1])/(height-1), xy[:, 0]/(width-1)]


def identity_labels(xy, frame, annotation, facade):
    uv = upright_coordinates(xy, frame['width'], frame['height'])
    known_facade = labels(np.asarray(xy)/[frame['width'], frame['height']], facade) == 'facade'
    faces = np.full(len(uv), '', dtype='U40')
    elements = np.full(len(uv), '', dtype='U40')
    for face, lo, hi in annotation['face_bands']:
        inside = known_facade & (uv[:, 0] > lo) & (uv[:, 0] < hi)
        if np.any(faces[inside] != ''):
            raise ValueError('Anotaciones de caras solapadas.')
        faces[inside] = face
    for element, box in annotation['elements']:
        u0,v0,u1,v1 = box
        inside = known_facade & (faces != '') & (uv[:, 0] > u0) & (uv[:, 0] < u1) & (uv[:, 1] > v0) & (uv[:, 1] < v1)
        if np.any(elements[inside] != ''):
            raise ValueError('Anotaciones de elementos solapadas.')
        # Element IDs include their physical face, avoiding contradictory hierarchy.
        if np.any(faces[inside] != element.split('/')[0]):
            raise ValueError('El elemento no pertenece a la cara anotada.')
        elements[inside] = element
    # The old facade mask includes some background. Only manually identified
    # window regions carry identity; a broad face band is merely a spatial gate.
    faces[elements == ''] = ''
    return faces, elements


def incompatible(a, b):
    fa, ea = a; fb, eb = b
    return ((fa != '') & (fb != '') & (fa != fb)) | ((ea != '') & (eb != '') & (ea != eb))


def conflicting_identities(observations):
    return any(len({o[key] for o in observations if o[key]}) > 1 for key in ['face','element'])


def load_annotations(path, data):
    ann = read_json(path)
    if ann['coordinate_system'] != 'upright_clockwise_normalized_pixel_centres':
        raise ValueError('Sistema de coordenadas de anotaciones desconocido.')
    if set(ann['frames']) != {f['id'] for f in data['frames']}:
        raise ValueError('Las anotaciones no corresponden a la selección completa.')
    for f in data['frames']:
        a = ann['frames'][f['id']]
        if a['sha256'] != f['sha256']:
            raise ValueError('La identidad se anotó sobre otra imagen.')
        for face,lo,hi in a['face_bands']:
            if face not in ann['physical_faces'] or not -0.01 <= lo < hi <= 1.01:
                raise ValueError('Cara o banda inválida.')
        for element,box in a['elements']:
            if element not in ann['physical_elements'] or len(box) != 4 or not np.isfinite(box).all():
                raise ValueError('Elemento o rectángulo inválido.')
            if not (0 <= box[0] < box[2] <= 1 and 0 <= box[1] < box[3] <= 1):
                raise ValueError('Rectángulo fuera de imagen.')
    return ann


def filter_pairs(source, output, annotations, facade_path, quarantine=None):
    data = read_json(source/'seleccion.json');frames = construction_frames(data)
    ann = load_annotations(annotations, data);facade = read_json(facade_path)['frames']
    output.mkdir(parents=True, exist_ok=False)
    identities = [];proof = [];sizes = {};keypoint_hashes = {}
    for i,f in enumerate(frames):
        path = source/f'keypoints-{i}.npz'
        with np.load(path, allow_pickle=False) as saved:xy = saved['keypoints']
        sizes[f['id']] = len(xy)
        keypoint_hashes[f['id']] = sha(path)
        identities.append(identity_labels(xy, f, ann['frames'][f['id']], facade[f['id']]))
        shutil.copy2(path, output/path.name)
        proof.append(dict(name=path.name, source_sha256=sha(path), output_sha256=sha(output/path.name)))
    banned = {}
    if quarantine is not None:
        q = read_json(quarantine)
        if q['selection_sha256'] != sha(source/'seleccion.json'):
            raise ValueError('La cuarentena corresponde a otras imágenes/cámaras.')
        if q['keypoint_sha256'] != keypoint_hashes:
            raise ValueError('La cuarentena corresponde a otras características/índices.')
        if q['annotations_sha256'] != sha(annotations) or q['facade_sha256'] != sha(facade_path):
            raise ValueError('La cuarentena corresponde a otras anotaciones.')
        if not q['conflicting_tracks']:
            raise ValueError('No hay tracks de identidad conflictiva que aislar.')
        for track in q['conflicting_tracks']:
            for o in track['observations']:
                if o['id'] not in sizes or not 0 <= o['keypoint'] < sizes[o['id']]:
                    raise ValueError('Observación de cuarentena fuera de rango.')
                banned.setdefault(o['id'], set()).add(o['keypoint'])
    counts = []
    for i,j in combinations(range(len(frames)),2):
        path = source/f'matches-{i}-{j}.npz'
        with np.load(path, allow_pickle=False) as saved:m = saved['matches']
        bad = incompatible(tuple(v[m[:,0]] for v in identities[i]), tuple(v[m[:,1]] for v in identities[j]))
        quarantined = np.zeros(len(m), dtype=bool)
        for col,index in [(0,i),(1,j)]:
            quarantined |= np.isin(m[:,col],list(banned.get(frames[index]['id'],set())))
        kept = m[~bad & ~quarantined]
        np.savez_compressed(output/path.name, matches=kept)
        counts.append(dict(i=i,j=j,before=len(m),identity_conflicts=int(bad.sum()),
                           quarantine=int((quarantined & ~bad).sum()),after=len(kept)))
        proof.append(dict(name=path.name, source_sha256=sha(path), output_sha256=sha(output/path.name)))
    write_json(output/'seleccion.json',data)
    conditions = read_json(source/'condiciones.json')
    write_json(output/'condiciones.json',{**conditions, 'identity_filter':dict(
        source=str(source.resolve()), annotations=str(annotations.resolve()),annotations_sha256=sha(annotations),
        facade_sha256=sha(facade_path),quarantine_source=str(quarantine) if quarantine else None,
        quarantine_sha256=sha(quarantine) if quarantine else None,
        policy='Reject only incompatible known physical identities; unknown preserved. Quarantine whole observed conflicting tracks, if supplied.',
        all_input_frames_preserved=True, keypoints_unchanged=True, metric_controls_used=False,
        image_transform='none; labels converted from upright display to original keypoint domain')})
    write_json(output/'filtro-identidad.json',dict(pairs=counts,quarantined_observations={k:sorted(v) for k,v in banned.items()},
        totals={key:sum(c[key] for c in counts) for key in ['before','identity_conflicts','quarantine','after']}))
    write_json(output/'procedencia-filtro.json',proof)
    print(output.name, read_json(output/'filtro-identidad.json')['totals'], flush=True)


def inspect_tracks(source, annotations, facade_path):
    import pycolmap as pc
    from PIL import Image, ImageDraw
    data = read_json(source/'seleccion.json');ann = load_annotations(annotations,data)
    facade = read_json(facade_path)['frames'];frames = {f['id']:f for f in data['frames']}
    keypoint_hashes = {f['id']:sha(source/f'keypoints-{i}.npz') for i,f in enumerate(data['frames'])}
    summaries = []
    for model in sorted((source/'modelos').iterdir()):
        if not (model/'images.bin').exists():continue
        rec = pc.Reconstruction(model);identity = {};coverage = [];conflicts = []
        for im in rec.images.values():
            f = frames[im.name];xy = np.array([p.xy-.5 for p in im.points2D]).reshape(-1,2)
            identity[im.image_id] = identity_labels(xy,f,ann['frames'][im.name],facade[im.name])
            valid = np.array([p.has_point3D() for p in im.points2D]);faces,elements = identity[im.image_id]
            coverage.append(dict(id=im.name,observations=int(valid.sum()),faces=dict(Counter(faces[valid])),elements=dict(Counter(elements[valid]))))
        tagged = 0
        for pid,p in rec.points3D.items():
            observations = []
            for e in p.track.elements:
                faces,elements = identity[e.image_id]
                observations.append(dict(id=rec.images[e.image_id].name,keypoint=int(e.point2D_idx),
                    face=str(faces[e.point2D_idx]),element=str(elements[e.point2D_idx])))
            faces = {o['face'] for o in observations if o['face']};elements = {o['element'] for o in observations if o['element']}
            if faces or elements:tagged += 1
            if conflicting_identities(observations):
                conflicts.append(dict(point=int(pid),faces=sorted(faces),elements=sorted(elements),observations=observations))
        report = dict(model=model.name,images=rec.num_reg_images(),points=rec.num_points3D(),
            selection_sha256=sha(source/'seleccion.json'),model_images_sha256=sha(model/'images.bin'),
            keypoint_sha256=keypoint_hashes,annotations_sha256=sha(annotations),facade_sha256=sha(facade_path),
            tagged_tracks=tagged,conflicting_tracks=conflicts,image_coverage=coverage,
            warning='No conflicting labels is necessary but insufficient: unknown regions and same-module mismatches remain unchecked.')
        write_json(model/'conflictos-identidad.json',report)
        summaries.append({k:v for k,v in report.items() if k not in ['conflicting_tracks','image_coverage','keypoint_sha256']} | dict(conflicts=len(conflicts)))
    write_json(source/'identidad-modelos.json',summaries)
    print(source.name,summaries,flush=True)


def render_annotations(dataset, annotations, facade_path, output):
    from PIL import Image, ImageDraw
    data = read_json(dataset);ann = load_annotations(annotations,data);facade = read_json(facade_path)['frames']
    output.mkdir(parents=True,exist_ok=False);groups = {}
    colours={'P':'#ffbf00','L':'#00ffff','D':'#ff55cc'}
    for i,f in enumerate(data['frames']):
        im = Image.open(f['path']).convert('RGB').rotate(-90,expand=True).resize((360,640))
        draw = ImageDraw.Draw(im);a = ann['frames'][f['id']]
        for face,lo,hi in a['face_bands']:
            draw.rectangle((max(0,lo)*359,2,min(1,hi)*359,637),outline=colours[face],width=3)
            draw.text((max(0,lo)*359+4,20),face,fill=colours[face])
        for element,box in a['elements']:
            u0,v0,u1,v1=box;draw.rectangle((u0*359,v0*639,u1*359,v1*639),outline='lime',width=2)
            draw.text((u0*359,v0*639),element,fill='lime')
        with np.load(dataset.parent/f'keypoints-{i}.npz',allow_pickle=False) as saved:xy=saved['keypoints']
        faces,elements=identity_labels(xy,f,a,facade[f['id']]);uv=upright_coordinates(xy,f['width'],f['height'])
        for (u,v),face,element in zip(uv,faces,elements):
            if face:draw.ellipse((u*359-1,v*639-1,u*359+1,v*639+1),fill='lime' if element else colours[face])
        draw.rectangle((0,0,359,17),fill='black');draw.text((4,3),f'{i:02d} '+Path(f['id']).name,fill='white')
        im.save(output/f'{i:02d}.jpg',quality=94);groups.setdefault(f['session'],[]).append(im)
    for session,ims in groups.items():
        sheet=Image.new('RGB',(4*360,640*((len(ims)+3)//4)),'#20252b')
        for n,im in enumerate(ims):sheet.paste(im,(n%4*360,n//4*640))
        sheet.save(output/f'sesion-{session.split("-")[2]}.jpg',quality=94)


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('command',choices=['filtrar','inspeccionar','laminas'])
    p.add_argument('--source',type=Path,required=True);p.add_argument('--annotations',type=Path,required=True)
    p.add_argument('--facade',type=Path,default=T/'experimentos/teologia-71-regiones-20261105.json')
    p.add_argument('--output',type=Path);p.add_argument('--quarantine',type=Path)
    a=p.parse_args()
    if a.command=='filtrar':
        if not a.output:p.error('filtrar requiere --output')
        filter_pairs(a.source,a.output,a.annotations,a.facade,a.quarantine)
    elif a.command=='inspeccionar':inspect_tracks(a.source,a.annotations,a.facade)
    else:
        if not a.output:p.error('laminas requiere --output')
        render_annotations(a.source/'seleccion.json',a.annotations,a.facade,a.output)
