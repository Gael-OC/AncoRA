"""Union of observed identity conflicts, with identical inputs for two optical conditions."""
from copy import deepcopy
from itertools import combinations
from pathlib import Path
import sys

import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from datos import read_json, write_json
from experimentos.identidad_teologia_20261106 import filter_pairs, sha


def compatible_inputs(a,b):
    da,db=[deepcopy(read_json(p/'seleccion.json')) for p in [a,b]]
    ca,cb=[read_json(p/'condiciones.json') for p in [a,b]]
    if ca.get('feature_signature') != cb.get('feature_signature') or ca['method'] != cb['method']:
        raise ValueError('Métodos/características distintos.')
    if ca['optical_condition']!='baseline' or cb['optical_condition']!='radial_k1':
        raise ValueError('Se requieren las condiciones baseline y radial_k1 explícitas.')
    if len(da['frames'])!=len(db['frames']):raise ValueError('Selecciones distintas.')
    for i,(fa,fb) in enumerate(zip(da['frames'],db['frames'])):
        if sha(a/f'keypoints-{i}.npz') != sha(b/f'keypoints-{i}.npz'):
            raise ValueError('Índices de características distintos.')
        if fa.get('phone')=='Xiaomi 25069PTEBG':
            candidate=fb.get('calibration_candidate',{})
            if (fb.get('recorded_K')!=fa['K'] or candidate.get('phone')!=fa['phone'] or
                    candidate.get('model')!='radial_k1'):
                raise ValueError('Cambio de calibración del 15T sin procedencia.')
            # Only these explicit optical fields may differ; every other frame field is fixed.
            for key in ['K','distortion','recorded_K','calibration_candidate']:
                fa.pop(key,None);fb.pop(key,None)
    if da!=db:raise ValueError('Selección o metadatos distintos fuera de la óptica del 15T.')
    for i,j in combinations(range(len(da['frames'])),2):
        with np.load(a/f'matches-{i}-{j}.npz',allow_pickle=False) as x, np.load(b/f'matches-{i}-{j}.npz',allow_pickle=False) as y:
            if not np.array_equal(x['matches'],y['matches']):
                raise ValueError(f'Correspondencias fuente distintas: {i}-{j}.')


def common_quarantine(a,b,oa,ob,annotations,facade,report_dir):
    compatible_inputs(a,b)
    data=[read_json(p/'seleccion.json') for p in [a,b]]
    reports=[];evidence=[]
    for source in [a,b]:
        group=[]
        for model in sorted((source/'modelos').iterdir()):
            path=model/'conflictos-identidad.json'
            if not path.exists():continue
            report=read_json(path)
            if (report['selection_sha256']!=sha(source/'seleccion.json') or
                    report['model_images_sha256']!=sha(model/'images.bin') or
                    report['annotations_sha256']!=sha(annotations) or report['facade_sha256']!=sha(facade)):
                raise ValueError('El informe de conflicto cambió de modelo/anotación.')
            expected={f['id']:sha(source/f'keypoints-{i}.npz') for i,f in enumerate(read_json(source/'seleccion.json')['frames'])}
            if report['keypoint_sha256']!=expected:raise ValueError('El informe usa otros índices.')
            group.extend(report['conflicting_tracks'])
            evidence.append(dict(path=str(path.resolve()),sha256=sha(path),selection_sha256=report['selection_sha256'],model_images_sha256=report['model_images_sha256']))
        reports.append(group)
    observations=[{(o['id'],o['keypoint']) for track in group for o in track['observations']} for group in reports]
    if not any(observations):raise ValueError('No hay conflictos para aislar.')
    report_dir.mkdir(parents=True,exist_ok=False)
    for source,out,d in zip([a,b],[oa,ob],data):
        path=report_dir/f'{source.name}.json'
        write_json(path,dict(selection_sha256=sha(source/'seleccion.json'),
            keypoint_sha256={f['id']:sha(source/f'keypoints-{i}.npz') for i,f in enumerate(d['frames'])},
            annotations_sha256=sha(annotations),facade_sha256=sha(facade),conflicting_tracks=reports[0]+reports[1],
            source_evidence=evidence, intervention='Union of all observations in physically conflicting tracks from both optical hypotheses; includes potentially valid associations.'))
        filter_pairs(source,out,annotations,facade,path)
    # Numeric equality is the experimental control; epipolar verification will still differ with K/D.
    compatible_inputs(oa,ob)
    write_json(report_dir/'resumen.json',dict(baseline_observations=len(observations[0]),radial_observations=len(observations[1]),
        common=len(observations[0]&observations[1]),baseline_only=len(observations[0]-observations[1]),
        radial_only=len(observations[1]-observations[0]),union=len(observations[0]|observations[1]),
        source_pairs_numeric_equal=True,output_pairs_numeric_equal=True,source_evidence=evidence))


if __name__=='__main__':
    import argparse
    p=argparse.ArgumentParser(description=__doc__)
    for flag in ['baseline','radial','output-baseline','output-radial','annotations','facade','report']:
        p.add_argument('--'+flag,type=Path,required=True)
    a=p.parse_args()
    common_quarantine(a.baseline,a.radial,a.output_baseline,a.output_radial,a.annotations,a.facade,a.report)
