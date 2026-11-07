"""Extend two completed, compatible construction matchings; reuse their exact indices."""
import argparse
import hashlib
from itertools import combinations
from pathlib import Path
import shutil
import sys
import time
from types import SimpleNamespace
import numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from datos import read_json,write_json
from caracteristicas import no_network,feature_signature
from offline import backend
from sfm_aprendido import construction_frames


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--old',required=True);p.add_argument('--new',required=True);p.add_argument('--output',required=True)
    a=p.parse_args();sources=[Path(a.old),Path(a.new)];out=Path(a.output)
    data=[read_json(s/'seleccion.json') for s in sources]
    conditions=[read_json(s/'condiciones.json') for s in sources]
    for c in conditions:
        assert 'filter' not in c, 'Use unfiltered, completed matching sources'
        assert c['feature_signature']==feature_signature(c['method'],c['max_side'],c['keypoints'])
    assert all(conditions[0][k]==conditions[1][k] for k in ['method','max_side','keypoints','feature_signature'])
    groups=[construction_frames(d) for d in data]
    for source,frames in zip(sources,groups):
        assert all((source/f'matches-{i}-{j}.npz').is_file() for i,j in combinations(range(len(frames)),2)), 'Incomplete source'
    out.mkdir(parents=True,exist_ok=False)
    frames=[dict(f) for group in groups for f in group]
    # Preserve prior input images too; only new copies are used downstream.
    prior=[]
    for f in frames[:len(groups[0])]:
        target=out/'originales-previos'/f['id'];target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(f['path'],target);assert hashlib.sha256(target.read_bytes()).hexdigest()==f['sha256']
        prior.append(dict(source=f['path'],copy=str(target.resolve()),sha256=f['sha256']))
        f['path']=str(target.resolve())
    merged=dict(role='construction',frames=frames,
        excluded_sessions=sorted(set().union(*(set(d['excluded_sessions']) for d in data))),
        excluded_hashes=sorted(set().union(*(set(d['excluded_hashes']) for d in data))),
        selection=f'Union of {len(groups[0])} prior and {len(groups[1])} new construction views; no query matching.')
    construction_frames(merged)
    write_json(out/'seleccion.json',merged);write_json(out/'procedencia-previa.json',prior)
    write_json(out/'condiciones.json',{**conditions[0], 'sources':[str(s.resolve()) for s in sources],
        'extension_script_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        'pair_selection':'exhaustive union, identical within-source pairs reused; only cross-source pairs computed'})
    args=SimpleNamespace(**{k:conditions[0][k] for k in ['method','max_side','keypoints']})
    index=[(s,i) for s,g in enumerate(groups) for i in range(len(g))]
    for n,(s,i) in enumerate(index):shutil.copy2(sources[s]/f'keypoints-{i}.npz',out/f'keypoints-{n}.npz')
    with no_network():
        model=backend(args);features=[model.cached(f,Path(__file__).resolve().parents[1]/'salida/cache') for f in frames]
        for n,f in enumerate(features):
            np.testing.assert_array_equal(f['keypoints'],np.load(out/f'keypoints-{n}.npz')['keypoints'])
        start=time.perf_counter();count=0
        with (out/'pares.jsonl').open('w') as log:
            import json
            for i,j in combinations(range(len(frames)),2):
                si,oi=index[i];sj,oj=index[j];target=out/f'matches-{i}-{j}.npz'
                reused=si==sj
                if reused:
                    shutil.copy2(sources[si]/f'matches-{oi}-{oj}.npz',target)
                    matches=np.load(target)['matches']
                else:
                    matches=model.match(features[i],features[j]);np.savez_compressed(target,matches=matches);count+=1
                    if count%100==0:print(f'{count}/{len(groups[0])*len(groups[1])} cross-source pairs, {time.perf_counter()-start:.1f}s',flush=True)
                log.write(json.dumps(dict(i=i,j=j,matches=len(matches),reused=reused))+'\n');log.flush()
        print(f'Complete: {count} new pairs',flush=True)


if __name__=='__main__':main()
