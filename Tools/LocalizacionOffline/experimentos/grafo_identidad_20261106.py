"""Reject whole ambiguous verified correspondence components before native SfM."""
from collections import Counter
from itertools import combinations
from pathlib import Path
import shutil
import sys

import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from datos import read_json,write_json
from experimentos.cuarentena_comun_20261106 import compatible_inputs
from experimentos.identidad_teologia_20261106 import identity_labels,load_annotations,sha


def components(sizes,pairs,elements):
    offsets=np.r_[0,np.cumsum(sizes)];parent=np.arange(offsets[-1]);rank=np.zeros(len(parent),dtype=np.int8)
    def find(a):
        while parent[a]!=a:
            parent[a]=parent[parent[a]];a=parent[a]
        return int(a)
    for (i,j),matches in sorted(pairs.items()):
        if not 0<=i<j<len(sizes):raise ValueError('Par canónico i<j requerido.')
        if matches.shape!=(len(matches),2) or (len(matches) and ((matches<0).any() or (matches>=[sizes[i],sizes[j]]).any())):
            raise ValueError('Índice de arista inválido.')
        for x,y in matches:
            a=find(offsets[i]+int(x));b=find(offsets[j]+int(y))
            if a==b:continue
            if rank[a]<rank[b]:a,b=b,a
            parent[b]=a
            if rank[a]==rank[b]:rank[a]+=1
    roots=np.array([find(n) for n in range(len(parent))]);known={}
    for i,values in enumerate(elements):
        if len(values)!=sizes[i]:raise ValueError('Etiquetas y características no coinciden.')
        for k in np.flatnonzero(values!=''):known.setdefault(int(roots[offsets[i]+k]),set()).add(str(values[k]))
    bad={root for root,values in known.items() if len(values)>1}
    rejected=np.isin(roots,sorted(bad));filtered={}
    for (i,j),matches in pairs.items():
        filtered[(i,j)]=matches[~rejected[offsets[i]+matches[:,0]] & ~rejected[offsets[j]+matches[:,1]]]
    counts=Counter(roots.tolist());conflicts=[]
    for root in sorted(bad):
        nodes=np.flatnonzero(roots==root);images=np.searchsorted(offsets[1:],nodes,side='right')
        conflicts.append(dict(root=root,observations=len(nodes),images=dict(Counter(map(int,images))),elements=sorted(known[root])))
    report=dict(total_features=len(parent),components_including_isolated=len(counts),
        component_size_histogram=dict(Counter(counts.values())),largest_component=max(counts.values(),default=0),
        conflicting_components=conflicts,rejected_features=int(rejected.sum()),
        rejected_by_image=[int(rejected[offsets[i]:offsets[i+1]].sum()) for i in range(len(sizes))],
        rejected_by_element=dict(Counter(str(v) for i,values in enumerate(elements) for v in values[rejected[offsets[i]:offsets[i+1]]] if v)))
    return filtered,roots,offsets,report


def prepare(a,b,run,method,annotations,facade_path):
    import pycolmap as pc
    compatible_inputs(a,b);data=read_json(a/'seleccion.json');frames=data['frames'];ann=load_annotations(annotations,data)
    facade=read_json(facade_path)['frames'];sizes=[];elements=[]
    for i,f in enumerate(frames):
        with np.load(a/f'keypoints-{i}.npz',allow_pickle=False) as saved:xy=saved['keypoints']
        sizes.append(len(xy));elements.append(identity_labels(xy,f,ann['frames'][f['id']],facade[f['id']])[1])
    pairs={};counts=[]
    with pc.Database.open(a/'database.db') as da,pc.Database.open(b/'database.db') as db:
        names=[{im.name:im.image_id for im in d.read_all_images()} for d in [da,db]]
        for i,j in combinations(range(len(frames)),2):
            rows=[]
            for d,ids in zip([da,db],names):
                rows.append({tuple(map(int,m)) for m in d.read_two_view_geometry(ids[frames[i]['id']],ids[frames[j]['id']]).inlier_matches})
            with np.load(a/f'matches-{i}-{j}.npz',allow_pickle=False) as saved:raw=saved['matches']
            rawset={tuple(map(int,m)) for m in raw};union=rows[0]|rows[1]
            if not union<=rawset:raise ValueError('La verificación inventó índices de correspondencia.')
            mask=np.array([tuple(map(int,m)) in union for m in raw],dtype=bool)
            pairs[(i,j)]=raw[mask]
            counts.append(dict(i=i,j=j,baseline=len(rows[0]),radial=len(rows[1]),common=len(rows[0]&rows[1]),
                baseline_only=len(rows[0]-rows[1]),radial_only=len(rows[1]-rows[0]),union=len(pairs[(i,j)])))
    retained,roots,offsets,report=components(sizes,pairs,elements)
    graph=run/f'grafo-{method}';graph.mkdir(exist_ok=False)
    np.savez_compressed(graph/'componentes.npz',roots=roots,offsets=offsets)
    write_json(graph/'componentes.json',report);write_json(graph/'verificados-fuente.json',counts)
    write_json(graph/'procedencia.json',dict(baseline=str(a.resolve()),radial=str(b.resolve()),
        database_sha256=[sha(p/'database.db') for p in [a,b]],selection_sha256=[sha(p/'seleccion.json') for p in [a,b]],
        annotations_sha256=sha(annotations),facade_sha256=sha(facade_path),keypoints_sha256=[sha(a/f'keypoints-{i}.npz') for i in range(len(frames))]))
    for policy,edges in [('union',pairs),('consistente',retained)]:
        for source,config in [(a,'baseline'),(b,'radial_k1')]:
            out=run/f'{method}-{config}-grafo-{policy}';out.mkdir(exist_ok=False)
            shutil.copy2(source/'seleccion.json',out/'seleccion.json')
            for i in range(len(frames)):shutil.copy2(source/f'keypoints-{i}.npz',out/f'keypoints-{i}.npz')
            for (i,j),m in edges.items():np.savez_compressed(out/f'matches-{i}-{j}.npz',matches=m)
            write_json(out/'condiciones.json',{**read_json(source/'condiciones.json'),'component_graph':dict(
                policy=policy,source_graph=str(graph.resolve()),annotations_sha256=sha(annotations),
                warning='Union of inliers from two optical hypotheses; whole conflicting components removed, including possibly valid links. Unknown is not verified.',
                all_frames_and_keypoints_preserved=True,metric_controls_used=False)})
        compatible_inputs(run/f'{method}-baseline-grafo-{policy}',run/f'{method}-radial_k1-grafo-{policy}')
    print(method,'verified union edges',sum(len(m) for m in pairs.values()),'retained',sum(len(m) for m in retained.values()),
          'conflicting components',len(report['conflicting_components']),'removed features',report['rejected_features'],flush=True)


if __name__=='__main__':
    import argparse
    p=argparse.ArgumentParser(description=__doc__)
    for flag in ['baseline','radial','output','annotations','facade']:p.add_argument('--'+flag,type=Path,required=True)
    p.add_argument('--method',choices=['xfeat','aliked'],required=True);x=p.parse_args()
    prepare(x.baseline,x.radial,x.output,x.method,x.annotations,x.facade)
