"""Compare all declared 78-view conditions and retained 71-view baselines without selecting a winner."""
import argparse
from collections import Counter
from itertools import combinations
from pathlib import Path
import sys
from types import SimpleNamespace
import numpy as np
T=Path(__file__).resolve().parents[1];sys.path.insert(0,str(T))
from datos import read_json,write_json
from sfm_diagnostico import compare,stats,max_angle
from identidad_teologia_20261106 import inspect_tracks,identity_labels

def main(r):
    import pycolmap as pc
    names=['xfeat-baseline','xfeat-radial_k1','aliked-baseline','aliked-radial_k1','xfeat-radial_k1-replica']
    conditions={};selected={};records={};dataset=read_json(r/'seleccion-78.json')
    allnames={f['id'] for f in dataset['frames']};newnames={f['id'] for f in read_json(r/'siete-seleccion.json')['frames']}
    facade=read_json(r/'fachada-78.json')['frames'];ann=read_json(r/'identidades-78.json')['frames'];frames={f['id']:f for f in dataset['frames']}
    for name in names:
        out=r/name
        inspect_tracks(out,r/'identidades-78.json',r/'fachada-78.json')
        models=read_json(out/'resultado.json')['models'];best=sorted(models,key=lambda m:(-m['images'],int(m['model'])))[0]
        selected[name]=out/'modelos'/str(best['model']);rec=pc.Reconstruction(selected[name]);records[name]=rec
        registered={im.name for im in rec.images.values()};rows=[]
        for im in rec.images.values():
            f=frames[im.name];points=[(idx,p,rec.points3D[p.point3D_id]) for idx,p in enumerate(im.points2D) if p.has_point3D()]
            uv=np.array([p.xy-.5 for _,p,_ in points]).reshape(-1,2)
            faces,elements=identity_labels(uv,f,ann[im.name],facade[im.name])
            depth=[];angle=[];support=[]
            for idx,p,xyz in points:
                depth.append(float((im.cam_from_world()*xyz.xyz)[2]));angle.append(max_angle(xyz.xyz,[rec.images[e.image_id].projection_center() for e in xyz.track.elements]))
                support.extend(rec.images[e.image_id].name for e in xyz.track.elements if e.image_id!=im.image_id)
            cells=np.clip((uv/[f['width'],f['height']]*[4,4]).astype(int),[0,0],[3,3])
            rows.append(dict(id=im.name,observations=len(points),known_elements=dict(Counter(elements)),
                occupied_cells_4x4=len({tuple(c) for c in cells}),depth_arbitrary=stats(depth),
                max_track_angle_degrees=stats(angle),support_images=dict(Counter(support)),
                warning='Track angles depend on unvalidated estimated cameras/associations; not independent parallax truth.'))
        modelsets=[set(m['names']) for m in models]
        conditions[name]=dict(selected_model=best['model'],selection_rule='Most registered cameras; native numeric model index tie-break, fixed before comparison',
            all_components=[{k:v for k,v in m.items() if k!='names'} for m in models],
            overlaps=[dict(a=models[i]['model'],b=models[j]['model'],shared=len(modelsets[i]&modelsets[j])) for i,j in combinations(range(len(models)),2)],
            registered=len(registered),points=best['points'],reprojection_px=best['mean_reprojection_original_px'],
            unregistered=sorted(allnames-registered),new_registered=sorted(newnames&registered),new_unregistered=sorted(newnames-registered),
            conflicts=read_json(out/'identidad-modelos.json'),cameras=rows)
    pairs=[('metodos-baseline',r/'xfeat-baseline',r/'aliked-baseline'),('metodos-radial',r/'xfeat-radial_k1',r/'aliked-radial_k1'),
           ('xfeat-optica',r/'xfeat-baseline',r/'xfeat-radial_k1'),('aliked-optica',r/'aliked-baseline',r/'aliked-radial_k1'),
           ('xfeat-radial-replica',r/'xfeat-radial_k1',r/'xfeat-radial_k1-replica')]
    for method in ['xfeat','aliked']:
        for config in ['baseline','radial_k1']:
            old=T/f'salida/20261106-identidad-teologia/{method}-{config}-pares-v6'
            pairs.append((f'{method}-{config}-71-78',old,r/f'{method}-{config}'))
    pairs.append(('xfeat-radial-replica71-78',T/'salida/20261106-identidad-teologia/xfeat-radial_k1-replica-v6',r/'xfeat-radial_k1'))
    outdir=r/'comparaciones';outdir.mkdir(exist_ok=False);comparisons={}
    for name,a,b in pairs:
        def choose(p):
            ms=read_json(p/'resultado.json')['models'];m=sorted(ms,key=lambda m:(-m['images'],int(m['model'])))[0];return p/'modelos'/str(m['model'])
        ap,bp=choose(a),choose(b);output=outdir/f'{name}.json'
        try:
            compare(SimpleNamespace(source=ap,other=bp,dataset=r/'seleccion-78.json',anchor=dataset['frames'][0]['id'],output=output))
            d=read_json(output);comparisons[name]={k:d[k] for k in ['common_cameras','centre_error_percent_diameter']}
            comparisons[name]['max_angle_deg']=max(c['relative_rotation_delta_degrees'] for c in d['cameras'])
            comparisons[name]['targets']=[c for c in d['cameras'] if '143744' in c['id'] and Path(c['id']).name[:4] in ['0001','0068']]
        except ValueError as e:comparisons[name]={'not_comparable':str(e)}
    write_json(r/'resumen-resultados.json',dict(conditions=conditions,comparisons=comparisons,warning='No metric fit; no map accepted; all components retained and counts are not summed.'))
    print('CONDITIONS',[(n,d['registered'],d['points'],len(d['new_registered'])) for n,d in conditions.items()])
    print('COMPARISONS',comparisons)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--run',type=Path,required=True);main(p.parse_args().run)
