"""Directed 71+7 construction experiment; preserve v6 and rebuild geometry independently."""
import argparse
import copy
import hashlib
from itertools import combinations
from pathlib import Path
import shutil
import subprocess
import sys
from types import SimpleNamespace
import numpy as np
T=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(T))
from datos import read_json,write_json
from sfm_aprendido import construction_frames
from sfm_diagnostico import filter_facade
from identidad_teologia_20261106 import filter_pairs

def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def prepare(r):
    r.mkdir(parents=True,exist_ok=True)
    if any((r/name).exists() for name in ['siete-seleccion.json','seleccion-78.json','fachada-78.json','identidades-78.json','fuentes']):
        raise FileExistsError('Use a new experiment; preserve previous selections and annotations.')
    new=read_json(T/'salida/20261106-origen-imagenes/siete-vistas-intermedias-candidatas.json')
    construction_frames(new)
    assert len(new['frames'])==7
    write_json(r/'siete-seleccion.json',new)
    facade=read_json(T/'experimentos/teologia-71-regiones-20261105.json')
    ann=read_json(T/'experimentos/teologia-identidades-v6-20261106.json')
    old=read_json(T/'salida/20261105-revision-camaras/ampliado-xfeat/seleccion.json')
    assert len(old['frames'])==71
    # Drawn on full upright photos, before any new matching/SfM result.
    boxes={2:[.20,.30,.63,.46],3:[.15,.31,.56,.46],4:[.10,.32,.52,.47]}
    polygons={2:[(.18,.24),(1,.12),(1,.60),(.18,.60)],
      3:[(.09,.25),(1,.12),(1,.60),(.09,.60)],
      4:[(.05,.26),(1,.12),(1,.60),(.05,.60)],
      66:[(0,.20),(1,.21),(1,.60),(0,.60)],
      67:[(0,.22),(1,.15),(1,.60),(0,.60)],
      69:[(.30,.27),(1,.17),(1,.60),(.30,.60)],
      70:[(.20,.28),(1,.14),(1,.60),(.20,.60)]}
    lamps={2:[],3:[(.91,.27,1,.62)],4:[(.83,.27,1,.62)],66:[(.49,0,.69,.62)],67:[(.64,0,.82,.62)],69:[(.70,0,.85,.62)],70:[(.58,0,.73,.62)]}
    proof=[]
    for f in new['frames']:
        n=int(Path(f['id']).name[:4]);w,h=f['width'],f['height']
        def raw(poly):return [[v*(w-1)/w,(1-u)*(h-1)/h] for u,v in poly]
        def rect(b):x,y,X,Y=b;return [(x,y),(X,y),(X,Y),(x,Y)]
        poly=[(u,min(v,.55)) for u,v in polygons[n]]
        facade['frames'][f['id']]={'facade':raw(poly),'vegetation':[], 'exclude':[raw(rect(b)) for b in lamps[n]]}
        ann['frames'][f['id']]={'sha256':f['sha256'],'face_bands':[['P' if n<10 else 'L',-.001,1.001]],'elements':[['P/U1',boxes[n]]] if n<10 else []}
        proof.append(dict(id=f['id'],sha256=f['sha256'],apt_for_experiment=True,
          identity='P/U1 same physical upper window' if n<10 else 'L wall and narrow slots; slot module identity left unknown',
          contribution='Continuity around 0001' if n<10 else 'Continuity around 0068; lamp/people excluded by conservative wall crop',
          selection_basis='Ordinal neighbours fixed before matching; no success-based exclusion'))
    assert all(facade['frames'][k]==v for k,v in read_json(T/'experimentos/teologia-71-regiones-20261105.json')['frames'].items())
    assert all(ann['frames'][k]==v for k,v in read_json(T/'experimentos/teologia-identidades-v6-20261106.json')['frames'].items())
    merged={**old,'frames':old['frames']+new['frames'],'selection':'71 v6 fixed baseline plus seven preselected ordinal neighbours; no query/board/historical images'}
    construction_frames(merged);write_json(r/'seleccion-78.json',merged)
    ann['selection_sha256']=sha(r/'seleccion-78.json');ann['notes'].append('78 experiment: all 71 v6 entries unchanged; seven new manual entries before matching. L narrow slots lack reliable physical element identity and stay unknown.')
    facade['description']+=' Seven new upper-wall regions; all 71 baseline masks unchanged. Narrow slots are not assigned window identities.'
    write_json(r/'identidades-78.json',ann);write_json(r/'fachada-78.json',facade);write_json(r/'revision-siete.json',proof)
    sources=r/'fuentes';sources.mkdir(exist_ok=False)
    for p in [Path(__file__),T/'sfm_aprendido.py',T/'sfm_diagnostico.py',T/'experimentos/conectar_recorridos.py',T/'experimentos/identidad_teologia_20261106.py']:
        shutil.copy2(p,sources/p.name)
    write_json(r/'fuentes.json',{str(p):sha(p) for p in sources.iterdir()})

def conditions(r):
    calibration=T/'salida/20261105-calibracion-casa-2/ajuste/modelos.json';cal=read_json(calibration)['radial_k1']
    proof=[]
    for method in ['xfeat','aliked']:
        union=r/f'union-{method}';fac=r/f'fachada-{method}'
        filter_facade(SimpleNamespace(source=union,output=fac,annotations=r/'fachada-78.json'))
        # Existing 71 matches must reproduce the original v6 filtered input exactly.
        filtered=r/f'identidad-{method}'
        filter_pairs(fac,filtered,r/'identidades-78.json',r/'fachada-78.json')
        prior=T/f'salida/20261106-identidad-teologia/{method}-baseline-pares-v6'
        for i,j in combinations(range(71),2):
            np.testing.assert_array_equal(np.load(prior/f'matches-{i}-{j}.npz')['matches'],np.load(filtered/f'matches-{i}-{j}.npz')['matches'])
        for config in ['baseline','radial_k1']:
            out=r/f'{method}-{config}';out.mkdir(exist_ok=False)
            data=copy.deepcopy(read_json(filtered/'seleccion.json'))
            for f in data['frames']:
                if config=='radial_k1' and f['phone']=='Xiaomi 25069PTEBG':
                    assert [f['width'],f['height']]==[1920,1080]
                    f['recorded_K']=copy.deepcopy(f['K']);f['K']=cal['K'];f['distortion']=cal['distortion']
                    f['calibration_candidate']=dict(model=config,source_sha256=sha(calibration),transfer_validated=False,phone=f['phone'])
            construction_frames(data);write_json(out/'seleccion.json',data)
            c=read_json(filtered/'condiciones.json');c.update(optical_condition=config,calibration_candidate_only=True,
                intrinsics='Fixed raw-image K/D per phone/session; radial second series only 15T including all seven additions; Xiaomi14 unchanged',sfm_script_sha256=sha(T/'sfm_aprendido.py'))
            write_json(out/'condiciones.json',c)
            for pattern in ['keypoints-*.npz','matches-*.npz']:
                for p in filtered.glob(pattern):shutil.copy2(p,out/p.name)
            proof.append(dict(name=out.name,frames=78,all_71_v6_pairs_numerically_identical=True,
                seven_have_same_radial_as_prior_15t=config=='radial_k1',calibration_sha256=sha(calibration),
                keypoints={p.name:sha(p) for p in out.glob('keypoints-*.npz')},pairs={p.name:sha(p) for p in out.glob('matches-*.npz')}))
    write_json(r/'condiciones-verificadas.json',proof)

def repeat(r):
    src=r/'xfeat-radial_k1';out=r/'xfeat-radial_k1-replica';out.mkdir(exist_ok=False)
    for pattern in ['keypoints-*.npz','matches-*.npz','seleccion.json','condiciones.json']:
        for p in src.glob(pattern):shutil.copy2(p,out/p.name)

def reconstruct(r):
    for name in ['xfeat-baseline','xfeat-radial_k1','aliked-baseline','aliked-radial_k1','xfeat-radial_k1-replica']:
        out=r/name
        with (out/'reconstruccion.log').open('x') as log:
            print('START',name,flush=True)
            subprocess.run([sys.executable,str(T/'sfm_aprendido.py'),'reconstruir','--output',str(out),'--runtime','300'],stdout=log,stderr=subprocess.STDOUT,check=True)
        print('DONE',name,[(m['model'],m['images'],m['points']) for m in read_json(out/'resultado.json')['models']],flush=True)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('command',choices=['preparar','condiciones','replica','reconstruir']);p.add_argument('--run',type=Path,required=True);a=p.parse_args()
    {'preparar':prepare,'condiciones':conditions,'replica':repeat,'reconstruir':reconstruct}[a.command](a.run)
