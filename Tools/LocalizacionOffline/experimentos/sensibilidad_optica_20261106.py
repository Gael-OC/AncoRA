"""New raw-pixel SfM camera sensitivity; no metric fitting or query ingestion."""
import argparse
from collections import Counter
import copy
import hashlib
from itertools import combinations
import json
from pathlib import Path
import shutil
import subprocess
import sys

import numpy as np

T = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(T))
from datos import read_json, write_json
from sfm_aprendido import construction_frames

CALIBRATION = T / 'salida/20261105-calibracion-casa-2/ajuste/modelos.json'
PHONE = 'Xiaomi 25069PTEBG'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def prepare(run):
    run.mkdir(parents=True, exist_ok=False)
    sources = run / 'fuentes'
    sources.mkdir()
    for path in [Path(__file__), T/'sfm_aprendido.py', T/'sfm_diagnostico.py',
                 T/'test_sfm_aprendido.py']:
        shutil.copy2(path, sources/path.name)
    models = read_json(CALIBRATION)
    write_json(run/'calibracion-usada.json', {name: {k: models[name][k] for k in ['K', 'distortion']}
               for name in ['registered', 'radial_k1', 'brown5']})
    provenance = dict(calibration_path=str(CALIBRATION), calibration_sha256=sha(CALIBRATION),
        phone=PHONE, calibration_resolution=[1920,1080],
        transfer_limit='Candidate only: building prior K differs; physical lens/focus identity not recorded.',
        image_transform='none; raw PNG and original keypoints/match indices unchanged',
        scale='arbitrary; no metric controls, queries, boards, Unity, GPS or Immersal used',
        runs=[])
    for size in [65, 71]:
        for method in ['xfeat', 'aliked']:
            src = (T/f'salida/20261105-recorridos/teo-denso-{method}-fachada-v2' if size == 65
                   else T/f'salida/20261105-revision-camaras/ampliado-{method}-fachada')
            original = read_json(src/'seleccion.json')
            construction_frames(original)
            assert len(original['frames']) == size
            configs = ['baseline', 'radial_k1', 'brown5'] if size == 71 else ['baseline', 'radial_k1']
            for config in configs:
                graphs = ['exhaustive', 'temporal'] if size == 65 and method == 'xfeat' and config != 'brown5' else ['exhaustive']
                for graph in graphs:
                    name = f'{size}-{method}-{config}' + ('-temporal' if graph == 'temporal' else '')
                    out = run/name
                    out.mkdir()
                    data = copy.deepcopy(original)
                    for f in data['frames']:
                        if f['phone'] == PHONE and config != 'baseline':
                            if [f['width'], f['height']] != [1920,1080]:
                                raise ValueError('No se extrapola la calibración a otra resolución/recorte.')
                            f['recorded_K'] = f['K']
                            f['K'] = models[config]['K']
                            f['distortion'] = models[config]['distortion']
                            f['calibration_candidate'] = dict(model=config, source_sha256=sha(CALIBRATION),
                                transfer_validated=False, phone=PHONE)
                    construction_frames(data)
                    write_json(out/'seleccion.json', data)
                    conditions = read_json(src/'condiciones.json')
                    conditions.update(source_experiment=str(src), optical_condition=config,
                        intrinsics='Fixed per phone/session, raw pixel Brown distortion; 14 unchanged',
                        pair_selection=graph, calibration_candidate_only=True,
                        sfm_script_sha256=sha(T/'sfm_aprendido.py'))
                    write_json(out/'condiciones.json', conditions)
                    files = []
                    for i in range(size):
                        path = src/f'keypoints-{i}.npz'
                        shutil.copy2(path, out/path.name)
                        files.append(dict(name=path.name, source_sha256=sha(path), output_sha256=sha(out/path.name)))
                    for i,j in combinations(range(size),2):
                        path = src/f'matches-{i}-{j}.npz'
                        allowed = graph == 'exhaustive' or data['frames'][i]['session'] != data['frames'][j]['session'] or j == i+1
                        if allowed:
                            shutil.copy2(path, out/path.name)
                        else:
                            np.savez_compressed(out/path.name, matches=np.empty((0,2),dtype=np.int32))
                        files.append(dict(name=path.name, source_sha256=sha(path),
                                          output_sha256=sha(out/path.name), retained=allowed))
                    write_json(out/'reutilizacion.json', files)
                    provenance['runs'].append(dict(name=name, source=str(src),
                        selection_sha256=sha(src/'seleccion.json'), conditions_sha256=sha(src/'condiciones.json'),
                        baseline_images_sha256=sha(src/'modelos/0/images.bin'),
                        frames=size, phones=dict(Counter(f['phone'] for f in data['frames'])),
                        changed_frames=sum(a!=b for a,b in zip(original['frames'],data['frames']))))
    write_json(run/'procedencia.json', provenance)
    print([r['name'] for r in provenance['runs']], flush=True)


def reconstruct(run, names=None):
    for row in read_json(run/'procedencia.json')['runs']:
        if names is not None and row['name'] not in names:continue
        out = run/row['name']
        with (out/'reconstruccion.log').open('x') as log:
            print('START', row['name'], flush=True)
            subprocess.run([sys.executable,str(T/'sfm_aprendido.py'),'reconstruir',
                            '--output',str(out),'--runtime','300'],stdout=log,stderr=subprocess.STDOUT,check=True)
        print('DONE', row['name'], read_json(out/'resultado.json'), flush=True)


def compare(run):
    from sfm_diagnostico import compare as compare_models
    pairs=[]
    for size in [65,71]:
        for method in ['xfeat','aliked']:
            baseline=f'{size}-{method}-baseline'
            radial=f'{size}-{method}-radial_k1'
            src=Path(next(r['source'] for r in read_json(run/'procedencia.json')['runs'] if r['name']==baseline))
            pairs.extend([(f'{baseline}-original',src,run/baseline),
                          (f'{size}-{method}-radial-baseline',run/baseline,run/radial)])
            if size==71:
                pairs.extend([(f'71-{method}-brown-baseline',run/baseline,run/f'71-{method}-brown5'),
                              (f'71-{method}-brown-radial',run/radial,run/f'71-{method}-brown5'),
                              (f'{method}-radial-65-71',run/f'65-{method}-radial_k1',run/radial)])
        for config in ['baseline','radial_k1'] + (['brown5'] if size==71 else []):
            pairs.append((f'{size}-metodos-{config}',run/f'{size}-xfeat-{config}',run/f'{size}-aliked-{config}'))
    for config in ['baseline','radial_k1']:
        pairs.append((f'65-xfeat-temporal-{config}',run/f'65-xfeat-{config}',run/f'65-xfeat-{config}-temporal'))
    comparisons=run/'comparaciones';comparisons.mkdir(exist_ok=False)
    for name,a,b in pairs:
        dataset=b/'seleccion.json'
        anchor=read_json(dataset)['frames'][0]['id']
        compare_models(argparse.Namespace(source=str(a/'modelos/0'),other=str(b/'modelos/0'),
            dataset=str(dataset),anchor=anchor,output=str(comparisons/f'{name}.json')))
    print('Comparisons:',len(pairs),flush=True)


def repeat(run):
    """Repeat fixed inputs/seed to expose native mapper variation, not tune success."""
    provenance=read_json(run/'procedencia.json');names=[]
    for method in ['xfeat','aliked']:
        for config in ['baseline','radial_k1']:
            name=f'71-{method}-{config}';replica=name+'-replica';src=run/name;out=run/replica
            out.mkdir(exist_ok=False)
            for pattern in ['keypoints-*.npz','matches-*.npz','seleccion.json','condiciones.json','reutilizacion.json']:
                for file in src.glob(pattern):shutil.copy2(file,out/file.name)
            row=copy.deepcopy(next(r for r in provenance['runs'] if r['name']==name))
            row.update(name=replica,replica_of=name,same_inputs_and_seed=True)
            provenance['runs'].append(row);names.append(replica)
    write_json(run/'procedencia.json',provenance)
    reconstruct(run,names)
    from sfm_diagnostico import compare as compare_models
    comparisons=run/'comparaciones-replicas';comparisons.mkdir(exist_ok=False)
    for name in names:
        original=name.removesuffix('-replica');dataset=run/name/'seleccion.json'
        compare_models(argparse.Namespace(source=str(run/original/'modelos/0'),other=str(run/name/'modelos/0'),
            dataset=str(dataset),anchor=read_json(dataset)['frames'][0]['id'],output=str(comparisons/f'{original}.json')))


def diagnose(run):
    import pycolmap as pc
    from PIL import Image, ImageDraw
    from sfm_diagnostico import stats
    rows=[]
    for row in read_json(run/'procedencia.json')['runs']:
        out=run/row['name'];data=read_json(out/'seleccion.json')
        fs={f['id']:f for f in data['frames']}
        models=[]
        for path in sorted((out/'modelos').iterdir()):
            if not (path/'images.bin').exists():continue
            r=pc.Reconstruction(path);details=[];residuals={};cross=Counter()
            for point in r.points3D.values():
                sessions=sorted({fs[r.images[e.image_id].name]['session'] for e in point.track.elements})
                for a,b in combinations(sessions,2):cross[(a,b)]+=1
            for im in r.images.values():
                f=fs[im.name];valid=[(idx,p) for idx,p in enumerate(im.points2D) if p.has_point3D()]
                xy=np.array([p.xy-.5 for _,p in valid]).reshape(-1,2);support=Counter();observations=[]
                errors=[]
                for idx,p in valid:
                    point=r.points3D[p.point3D_id]
                    for session in {fs[r.images[e.image_id].name]['session'] for e in point.track.elements}:
                        if session!=f['session']:support[session]+=1
                    prediction=r.cameras[im.camera_id].img_from_cam(im.cam_from_world()*point.xyz)
                    errors.append(float(np.linalg.norm(prediction-p.xy)))
                    observations.append(dict(index=idx,point=int(p.point3D_id),
                        support=sorted([r.images[e.image_id].name,int(e.point2D_idx)] for e in point.track.elements)))
                residuals.setdefault(f['phone'],[]).extend(errors)
                cells=len({tuple(c) for c in np.clip((xy/[f['width'],f['height']]*4).astype(int),0,3)})
                details.append(dict(id=im.name,observations=len(valid),grid4x4=cells,
                    other_session_support=dict(support),residual_px=stats(errors),tracks=observations))
            models.append(dict(model=path.name,images=r.num_reg_images(),points=r.num_points3D(),
                mean_reprojection_original_px=r.compute_mean_reprojection_error(),
                residual_original_px_by_phone={k:dict(count=len(v),quantiles=stats(v)) for k,v in residuals.items()},
                cross_session_tracks=[dict(a=a,b=b,tracks=n) for (a,b),n in sorted(cross.items())],
                images_detail=details))
            # Inspect distributed tracks of the two most problematic junctions.
            if row['name'] in ['71-xfeat-baseline','71-xfeat-radial_k1','71-aliked-radial_k1']:
                for session,number in [('143053',20),('143744',5),('143744',30)]:
                    im=next((im for im in r.images.values() if session in im.name and Path(im.name).name.startswith(f'{number:04}-')),None)
                    if im is None:continue
                    candidates=[]
                    for idx,p in enumerate(im.points2D):
                        if not p.has_point3D():continue
                        point=r.points3D[p.point3D_id]
                        els=point.track.elements
                        sessions={fs[r.images[e.image_id].name]['session'] for e in els}
                        if len(sessions)>=2:candidates.append((idx,p,point))
                    candidates.sort(key=lambda item:tuple(item[1].xy))
                    selected=[candidates[i] for i in sorted(set(np.linspace(0,len(candidates)-1,min(3,len(candidates))).astype(int)))] if candidates else []
                    canvas=Image.new('RGB',(4*360,max(1,len(selected))*480),'#20252b');draw=ImageDraw.Draw(canvas);manifest=[]
                    for n,(idx,p,point) in enumerate(selected):
                        elements=sorted(point.track.elements,key=lambda e:r.images[e.image_id].name)
                        chosen=[next(e for e in elements if e.image_id==im.image_id)]
                        seen={fs[im.name]['session']}
                        for e in elements:
                            s=fs[r.images[e.image_id].name]['session']
                            if s not in seen:chosen.append(e);seen.add(s)
                        chosen=chosen[:4];obs=[]
                        for col,e in enumerate(chosen):
                            other=r.images[e.image_id];f=fs[other.name];x,y=other.points2D[e.point2D_idx].xy-.5
                            raw=Image.open(f['path']).convert('RGB')
                            d=ImageDraw.Draw(raw);d.ellipse((x-12,y-12,x+12,y+12),outline='cyan',width=3)
                            full=raw.rotate(-90,expand=True);full.thumbnail((220,390))
                            canvas.paste(full,(col*360,n*480+66))
                            crop=raw.crop((int(x)-90,int(y)-90,int(x)+90,int(y)+90)).rotate(-90,expand=True).resize((130,130))
                            canvas.paste(crop,(col*360+225,n*480+190))
                            draw.text((col*360+3,n*480+3),f'{session}/{number:04} track {p.point3D_id}',fill='cyan')
                            draw.text((col*360+3,n*480+20),f["session"][21:27]+' '+Path(f['id']).name[:4],fill='white')
                            draw.text((col*360+3,n*480+37),f'kp {e.point2D_idx} x={x:.1f} y={y:.1f}',fill='white')
                            obs.append(dict(id=other.name,keypoint=int(e.point2D_idx),xy=[float(x),float(y)]))
                        manifest.append(dict(track=int(p.point3D_id),observations=obs))
                    canvas.save(out/f'tracks-{session}-{number:04}.jpg',quality=92)
                    write_json(out/f'tracks-{session}-{number:04}.json',manifest)
        write_json(out/'diagnostico-optico.json',models)
        rows.append(dict(name=row['name'],models=[{k:v for k,v in m.items() if k!='images_detail'} for m in models]))
    write_json(run/'resumen-modelos.json',rows)


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('command',choices=['preparar','reconstruir','comparar','diagnosticar','repetir'])
    p.add_argument('--output',type=Path,required=True)
    a=p.parse_args()
    {'preparar':prepare,'reconstruir':reconstruct,'comparar':compare,'diagnosticar':diagnose,'repetir':repeat}[a.command](a.output.resolve())
