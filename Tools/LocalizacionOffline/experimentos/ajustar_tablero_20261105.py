"""Provisional intrinsics; whole heldout sessions, fixed models, no mapping imports."""
import argparse
import hashlib
import itertools
import json
from pathlib import Path
import shutil

import cv2
import numpy as np
from scipy.spatial.transform import Rotation

OBJ = np.zeros((54,3),np.float32)
OBJ[:,:2] = np.mgrid[0:9,0:6].T.reshape(-1,2)*.024
CHECK = np.indices((6,9)).sum(axis=0).ravel()%2 == 0


def K_of(f):
    k=f['metadata']['intrinsecos']
    return np.array([[k['fx'],0,k['cx']],[0,k['fy'],k['cy']],[0,0,1.]],float)


def pose_error(f,K,d,mask=None):
    pts=np.array(f['corners'],np.float32)
    use=np.ones(54,bool) if mask is None else mask
    ok,r,t=cv2.solvePnP(OBJ[use],pts[use],K,d,flags=cv2.SOLVEPNP_ITERATIVE)
    assert ok
    pred=cv2.projectPoints(OBJ,r,t,K,d)[0].reshape(-1,2)
    err=np.linalg.norm(pred-pts,axis=1)
    return {'rvec':r.ravel().tolist(),'tvec_m_provisional':t.ravel().tolist(),
            'rms_px':float(np.sqrt(np.mean(err[~use if mask is not None else use]**2))),
            'normal_tilt_deg':float(np.degrees(np.arccos(np.clip(abs(cv2.Rodrigues(r)[0][2,2]),0,1))))}


def fit(frames,flags):
    return cv2.calibrateCameraExtended([OBJ]*len(frames),[np.array(f['corners'],np.float32) for f in frames],
        (1920,1080),K_of(frames[0]),np.zeros(5),flags=cv2.CALIB_USE_INTRINSIC_GUESS|flags,
        criteria=(cv2.TERM_CRITERIA_EPS+cv2.TERM_CRITERIA_MAX_ITER,150,1e-10))


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True);ap.add_argument('--output',type=Path,required=True)
    args=ap.parse_args();args.output.mkdir(parents=True,exist_ok=False);cv2.setNumThreads(2)
    shutil.copy2(__file__,args.output/Path(__file__).name)
    frames=json.loads((args.source/'detecciones.json').read_text())
    assert len({f['sha256'] for f in frames})==len(frames)
    assert {f['metadata']['telefono'] for f in frames}=={'Xiaomi 25069PTEBG'}
    assert {(f['metadata']['ancho'],f['metadata']['alto']) for f in frames}=={(1920,1080)}
    train=[]
    for session in sorted({f['session'] for f in frames if f['split']=='fit'}):
        fs=[f for f in frames if f['session']==session and f['detected']]
        assert len(fs) >= 9, f'Insufficient distinct detected views: {session}'
        train += [fs[i] for i in np.linspace(0,len(fs)-1,9).round().astype(int)]
    held=[f for f in frames if f['split']=='heldout' and f['detected']]
    assert not {f['session'] for f in train}&{f['session'] for f in held}
    assert not {f['sha256'] for f in train}&{f['sha256'] for f in held}
    assert held and len({f['session'] for f in train}) >= 3
    (args.output/'ajuste-seleccion.json').write_text(json.dumps([f['path'] for f in train],indent=2))
    zero=cv2.CALIB_ZERO_TANGENT_DIST|cv2.CALIB_FIX_K1|cv2.CALIB_FIX_K2|cv2.CALIB_FIX_K3
    specs={'pinhole':zero,'radial_k1':cv2.CALIB_ZERO_TANGENT_DIST|cv2.CALIB_FIX_K2|cv2.CALIB_FIX_K3,'brown5':0}
    models={'registered':{'K':K_of(train[0]).tolist(),'distortion':[0]*5,'fit_rms_px':None,
                          'uses_per_frame_recorded_intrinsics':True}}
    for name,flags in specs.items():
        rms,K,d,rv,tv,std,stde,per=fit(train,flags)
        sens=[]
        for session in sorted({f['session'] for f in train}):
            sub=[f for f in train if f['session']!=session]
            sr,sk,sd,*_=fit(sub,flags)
            sens.append({'excluded_session':session,'rms_px':sr,'K':sk.tolist(),'distortion':sd.ravel().tolist()})
        models[name]={'K':K.tolist(),'distortion':d.ravel().tolist(),'fit_rms_px':rms,
                      'std_intrinsics':std.ravel().tolist(),'leave_one_fit_session_out':sens}
        print(name,'rms',rms,'K',K.tolist(),'d',d.ravel().tolist(),flush=True)
    for name,m in models.items():
        K=np.array(m['K']);d=np.array(m['distortion'])
        results=[]
        for f in frames:
            if not f['detected']: continue
            poseK=K_of(f) if name=='registered' else K
            result={'path':f['path'],'session':f['session'],'split':f['split'],
                    'all_corners':pose_error(f,poseK,d),'checker_pose_only':pose_error(f,poseK,d,CHECK)}
            results.append(result)
        m['per_frame']=results
        m['heldout_summary']={}
        for session in sorted({f['session'] for f in held}):
            rs=[r for r in results if r['session']==session]
            m['heldout_summary'][session]={mode:{'median_rms_px':float(np.median([r[mode]['rms_px'] for r in rs])),
                     'p90_rms_px':float(np.percentile([r[mode]['rms_px'] for r in rs],90))} for mode in ['all_corners','checker_pose_only']}
        grid=np.array([[x,y] for y in [0,270,540,810,1079] for x in [0,480,960,1440,1919]],np.float32).reshape(-1,1,2)
        rect=cv2.undistortPoints(grid,K,d,P=K)
        m['rectification_grid_max_displacement_px']=float(np.linalg.norm(rect-grid,axis=2).max())
    (args.output/'modelos.json').write_text(json.dumps(models,indent=2))
    # Conditional motion diagnostic: board fixed, same epoch; signed orthogonal bases only.
    motion=[]
    for session in sorted({f['session'] for f in frames}):
        fs=[f for f in frames if f['session']==session and f['detected']]
        if len(fs) < 4:
            motion.append({'session':session,'skipped':'Fewer than four detected views; motion unavailable.'})
            continue
        poses=[pose_error(f,K_of(f),np.zeros(5)) for f in fs]
        cvrot=[cv2.Rodrigues(np.array(p['rvec']))[0] for p in poses]
        urot=[Rotation.from_quat(f['metadata']['rot_camara']).as_matrix() for f in fs]
        pairs=[(i,i+3) for i in range(len(fs)-3)]
        bases=[]
        for perm in itertools.permutations(range(3)):
            for signs in itertools.product([-1,1],repeat=3):
                B=np.eye(3)[list(perm)]*np.array(signs)[:,None]
                if np.linalg.det(B)>0: continue
                errs=[]
                for i,j in pairs:
                    optical=cvrot[j]@cvrot[i].T
                    unity=B@(urot[j].T@urot[i])@B.T
                    errs.append(float(np.degrees(Rotation.from_matrix(optical@unity.T).magnitude())))
                bases.append({'basis':B.tolist(),'median_deg':float(np.median(errs)),'p90_deg':float(np.percentile(errs,90))})
        bases.sort(key=lambda b:b['median_deg'])
        pts=np.concatenate([np.array(f['corners']) for f in fs])
        upos=np.array([f['metadata']['pos_camara'] for f in fs])
        motion.append({'session':session,'condition':'Board fixed, not confirmed by operator; PnP ambiguity possible; diagnostic not ground truth.',
              'best_signed_basis_diagnostic':bases[0],'all_bases':bases,
              'corners_bounds':[*pts.min(axis=0).tolist(),*pts.max(axis=0).tolist()],
              'hull_fraction_range':[min(f['hull_area_fraction'] for f in fs),max(f['hull_area_fraction'] for f in fs)],
              'tilt_range_deg':[min(p['normal_tilt_deg'] for p in poses),max(p['normal_tilt_deg'] for p in poses)],
              'unity_position_diameter_units':float(np.linalg.norm(upos[:,None]-upos[None,:],axis=2).max()),
              'sensor_interval_ms':[float(v) for v in np.percentile(np.diff([f['metadata']['timestamp_sensor'] for f in fs])*1000,[0,50,100])],
              'pose_sensor_timestamp_equal_count':sum(f['metadata']['timestamp_sensor']==f['metadata']['timestamp_pose_sensor'] for f in fs)})
    (args.output/'mouvement-et-couverture.json').write_text(json.dumps(motion,indent=2))
    for p in json.loads((args.source/'procedencia.json').read_text()):
        assert hashlib.sha256(Path(p['path']).read_bytes()).hexdigest()==p['sha256']
    # Simple physical/software sanity check: ideal planar projection recovers a known pose.
    testpts=cv2.projectPoints(OBJ,np.array([.1,.2,.05]),np.array([0.,0.,1.]),K_of(train[0]),None)[0].reshape(-1,2)
    test=dict(train[0],corners=testpts.tolist())
    assert pose_error(test,K_of(test),np.zeros(5),CHECK)['rms_px'] < .001
    print('Independent sessions/hashes, ideal projection check and source hashes verified',flush=True)

if __name__=='__main__': main()
