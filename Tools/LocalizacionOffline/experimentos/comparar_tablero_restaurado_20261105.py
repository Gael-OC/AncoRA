"""Compare restored detections and joint-fit sensitivity; never move heldout sessions into fit."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

import cv2
import numpy as np
from ajustar_tablero_20261105 import CHECK, K_of, OBJ, pose_error

BASE = Path(__file__).resolve().parents[1] / 'salida'

def read(path):
    return json.loads(path.read_text())

def main():
    ap=argparse.ArgumentParser();ap.add_argument('action',choices=['preparar','comparar']);ap.add_argument('--run',type=Path,required=True)
    args=ap.parse_args()
    previous=BASE/'20261105-calibracion-casa'
    second=BASE/'20261105-calibracion-casa-2'
    restored=args.run/'primera-redetectada'
    frames=read(restored/'detecciones.json')+read(second/'analisis/detecciones.json')
    assert len(frames)==487 and len({f['sha256'] for f in frames})==487
    train=[f for f in frames if f['split']=='fit'];held=[f for f in frames if f['split']=='heldout']
    assert len({f['session'] for f in train})==12 and len({f['session'] for f in held})==6
    assert not {f['session'] for f in train}&{f['session'] for f in held}
    if args.action=='preparar':
        out=args.run/'conjunto';out.mkdir(exist_ok=False)
        shutil.copy2(__file__,out/Path(__file__).name)
        old={f['sha256']:f for f in read(previous/'analisis/detecciones.json')}
        delta=[]
        for f in read(restored/'detecciones.json'):
            o=old[f['sha256']]
            assert f['metadata']==o['metadata'] and f['detected']==o['detected']
            if f['detected']:
                # Report ordering changes separately from numerical corner changes.
                a,b=np.array(f['corners']),np.array(o['corners'])
                direct=np.linalg.norm(a-b,axis=1)
                reverse=np.linalg.norm(a-b[::-1],axis=1)
                delta.append({'path':f['path'],'max_direct_delta_px':float(direct.max()),
                              'max_best_order_delta_px':float(min(direct.max(),reverse.max()))})
        (out/'redeteccion-vs-cache.json').write_text(json.dumps(delta,indent=2))
        provenance=read(restored/'procedencia.json')+read(second/'analisis/procedencia.json')
        (out/'procedencia.json').write_text(json.dumps(provenance,indent=2))
        (out/'detecciones.json').write_text(json.dumps(frames,indent=2))
        (out/'particion.json').write_text(json.dumps({
            'role':'calibration_only_never_mapping', 'assumption':'Shared intrinsics across deliveries is only a sensitivity experiment; focus not measured.',
            'fit_sessions':sorted({f['session'] for f in train}), 'heldout_sessions':sorted({f['session'] for f in held}),
            'board_square_m':[.024,.024], 'reason':'Preserve both original fit/heldout partitions unchanged; no heldout images in joint fit.'},indent=2))
        print('Prepared',len(frames),'frames; redetection max px',max(d['max_direct_delta_px'] for d in delta),flush=True)
        return
    out=args.run/'comparacion';out.mkdir(exist_ok=False)
    shutil.copy2(__file__,out/Path(__file__).name)
    new=read(second/'ajuste/modelos.json');joint=read(args.run/'ajuste-conjunto/modelos.json')
    selected=read(args.run/'ajuste-conjunto/ajuste-seleccion.json')
    assert len(selected)==108 and len(set(selected))==108
    assert not set(selected)&{f['path'] for f in held}
    conditions={'registered':None,**{'second_'+n:m for n,m in new.items() if n!='registered'},
                **{'joint_'+n:m for n,m in joint.items() if n!='registered'}}
    report={}
    for condition,model in conditions.items():
        results=[]
        for f in held:
            K=K_of(f) if model is None else np.array(model['K'])
            d=np.zeros(5) if model is None else np.array(model['distortion'])
            e=pose_error(f,K,d,CHECK)
            results.append({'session':f['session'],'sha256':f['sha256'],'rms_px':e['rms_px']})
        report[condition]={'per_frame':results,'sessions':{}}
        for s in sorted({r['session'] for r in results}):
            e=[r['rms_px'] for r in results if r['session']==s]
            report[condition]['sessions'][s]={'median_rms_px':float(np.median(e)),'p90_rms_px':float(np.percentile(e,90))}
    # Is the previous motion disagreement removed merely by using new K/distortion?
    B=np.array([[0.,-1,0],[-1,0,0],[0,0,1]])
    from scipy.spatial.transform import Rotation
    motion={}
    for condition in ['registered','second_radial_k1','second_brown5']:
        model=conditions[condition];motion[condition]={}
        for s in sorted({f['session'] for f in frames}):
            fs=sorted([f for f in frames if f['session']==s],key=lambda f:f['metadata']['timestamp_sensor'])
            poses=[pose_error(f,K_of(f) if model is None else np.array(model['K']),
                              np.zeros(5) if model is None else np.array(model['distortion'])) for f in fs]
            rr=[cv2.Rodrigues(np.array(r['rvec']))[0] for r in poses]
            ur=[Rotation.from_quat(f['metadata']['rot_camara']).as_matrix() for f in fs]
            errors=[]
            for i in range(len(fs)-3):
                j=i+3;optical=rr[j]@rr[i].T;unity=B@(ur[j].T@ur[i])@B.T
                errors.append(float(np.degrees(Rotation.from_matrix(optical@unity.T).magnitude())))
            motion[condition][s]={'median_deg':float(np.median(errors)),'p90_deg':float(np.percentile(errors,90))}
    (out/'reservadas.json').write_text(json.dumps(report,indent=2))
    (out/'movimiento-con-calibracion.json').write_text(json.dumps(motion,indent=2))
    # Registered K must be taken from each frame, including across deliveries.
    registered_joint={r['path']:r for r in joint['registered']['per_frame']}
    for f in held:
        expected=pose_error(f,K_of(f),np.zeros(5),CHECK)['rms_px']
        assert abs(expected-registered_joint[f['path']]['checker_pose_only']['rms_px'])<1e-7
    # Existing synthetic recovery check, with both recorded intrinsics.
    for f in [frames[0],frames[-1]]:
        pts=cv2.projectPoints(OBJ,np.array([.1,.2,.05]),np.array([0.,0.,1.]),K_of(f),None)[0].reshape(-1,2)
        assert pose_error(dict(f,corners=pts.tolist()),K_of(f),np.zeros(5),CHECK)['rms_px']<.001
    for r in read(args.run/'conjunto/procedencia.json'):
        assert hashlib.sha256(Path(r['path']).read_bytes()).hexdigest()==r['sha256']
    for condition,values in report.items():
        print(condition,{s[17:24]:round(v['median_rms_px'],4) for s,v in values['sessions'].items()},flush=True)

if __name__=='__main__':main()
