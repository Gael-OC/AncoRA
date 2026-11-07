"""Check the external matcher -> COLMAP boundary with known synthetic geometry."""
import hashlib
from itertools import combinations
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

import numpy as np
from PIL import Image

from sfm_aprendido import camera_spec, construction_frames
from sfm_diagnostico import labels, max_angle


class LearnedSfmTests(unittest.TestCase):
    def test_distorted_raw_pixels_agree_with_opencv_and_keep_camera_fixed(self):
        # Exercise the actual database boundary, including tangential terms and k3.
        code = '''
import cv2, numpy as np, pycolmap as pc, sys, tempfile
from pathlib import Path
from sfm_aprendido import camera_spec, import_database, reconstruct
from argparse import Namespace
from datos import write_json
from PIL import Image
import hashlib
k=np.array([[1297.,0,962.],[0,1295.,535.],[0,0,1.]])
xyz=np.array([[x,y,1.] for x in np.linspace(-.7,.7,9) for y in np.linspace(-.4,.4,7)])
for d in ([0.]*5,[.061,0,0,0,0],[-.007,.405,-.0008,.0058,-.575]):
    frame=dict(K=k.tolist(),distortion=d)
    model,params=camera_spec(frame)
    cam=pc.Camera(model=model,width=1920,height=1080,params=params)
    expected=cv2.projectPoints(xyz,np.zeros(3),np.zeros(3),k,np.array(d))[0].reshape(-1,2)
    np.testing.assert_allclose(cam.img_from_cam(xyz)-.5,expected,atol=1e-9)
    np.testing.assert_allclose(cam.cam_from_img(expected+.5),xyz[:,:2],atol=1e-5)
    with tempfile.TemporaryDirectory() as folder:
        out=Path(folder); path=out/'v.png'; Image.new('L',(1920,1080)).save(path)
        frame.update(id='v.png',path=str(path),session='fit',phone='test',width=1920,height=1080,
                     sha256=hashlib.sha256(path.read_bytes()).hexdigest())
        write_json(out/'seleccion.json',dict(role='construction',frames=[frame],excluded_sessions=[],excluded_hashes=[]))
        np.savez(out/'keypoints-0.npz',keypoints=expected)
        import_database(out)
        with pc.Database.open(out/'database.db') as db:
            actual=db.read_all_cameras()[0]
            assert actual.model_name==model
            np.testing.assert_array_equal(actual.params,params)
            np.testing.assert_allclose(db.read_keypoints(1),expected+.5,atol=1e-4)
        sfm=out/'sfm';sfm.mkdir();frames=[]
        rng=np.random.default_rng(15)
        points=rng.uniform([-2,-1,8],[4,1,15],(160,3))
        for i,x in enumerate([0.,1.,3.,5.]):
            f=dict(frame,id=f'v{i}.png',session=f'build-{i}')
            frames.append(f)
            xy=cv2.projectPoints(points-[x,0,0],np.zeros(3),np.zeros(3),k,np.array(d))[0].reshape(-1,2)
            np.savez(sfm/f'keypoints-{i}.npz',keypoints=xy)
            for j in range(i):
                np.savez(sfm/f'matches-{j}-{i}.npz',matches=np.c_[np.arange(160),np.arange(160)])
        write_json(sfm/'seleccion.json',dict(role='construction',frames=frames,excluded_sessions=[],excluded_hashes=[]))
        reconstruct(Namespace(output=str(sfm),runtime=15))
        rec=pc.Reconstruction(sfm/'modelos/0')
        assert rec.num_reg_images()==4 and rec.num_points3D()>=150
        assert rec.compute_mean_reprojection_error()<.001
        for c in rec.cameras.values():
            assert c.model_name==model
            np.testing.assert_array_equal(c.params,params)
        centres={im.name:im.projection_center() for im in rec.images.values()}
        distances=[np.linalg.norm(centres[f'v{i}.png']-centres['v0.png']) for i in [1,2,3]]
        np.testing.assert_allclose(np.array(distances)/distances[0],[1,3,5],atol=.001)
'''
        checked = subprocess.run([sys.executable, "-c", code], cwd=Path(__file__).parent,
                                 capture_output=True, text=True)
        self.assertEqual(checked.returncode, 0, checked.stderr)
        with self.assertRaisesRegex(ValueError, "Distorsión"):
            camera_spec(dict(K=np.eye(3).tolist(), distortion=[float('nan')]*5))

    def test_facade_excludes_foreground_and_angles_are_scale_invariant(self):
        annotation = dict(facade=[[0, 0], [.7, 0], [.7, 1], [0, 1]],
            vegetation=[[.8, 0], [1, 0], [1, 1], [.8, 1]],
            exclude=[[[0, .4], [1, .4], [1, .5], [0, .5]]])
        self.assertEqual(labels(np.array([[.3, .2], [.9, .2], [.3, .45], [.75, .2]]), annotation).tolist(),
                         ["facade", "vegetation", "unknown", "unknown"])
        angle = max_angle(np.array([0., 0., 10.]), [[0, 0, 0], [1, 0, 0]])
        self.assertAlmostEqual(angle, np.degrees(np.arctan(.1)))
        self.assertAlmostEqual(angle, max_angle(np.array([0., 0., 100.]), [[0, 0, 0], [10, 0, 0]]))

    def test_external_indices_pixel_convention_reconstruction_and_query_exclusion(self):
        with tempfile.TemporaryDirectory() as folder:
            out = Path(folder)
            rng = np.random.default_rng(14)
            xyz = rng.uniform([-2, -1, 8], [4, 1, 15], (180, 3))
            frames = []
            k = np.array([[700., 0, 640], [0, 710, 360], [0, 0, 1]])
            for i, x in enumerate([0., 1., 3., 5.]):
                image = out / f"view{i}.png"
                Image.new("L", (1280, 720), color=30 + i).save(image)
                frames.append(dict(id=image.name, path=str(image), session=f"build-{i}",
                    sha256=hashlib.sha256(image.read_bytes()).hexdigest(),
                    width=1280, height=720, K=k.tolist()))
                camera = xyz - [x, 0, 0]
                xy = camera @ k.T
                xy = xy[:, :2] / xy[:, 2:]
                np.savez_compressed(out / f"keypoints-{i}.npz", keypoints=xy)
            for i, j in combinations(range(4), 2):
                matches = np.c_[np.arange(180), np.arange(180)]
                # Wrong indices must pass through verification, not become trusted tracks.
                matches[:30, 1] = rng.permutation(30)
                np.savez_compressed(out / f"matches-{i}-{j}.npz", matches=matches)
            data = dict(role="construction", frames=frames, excluded_sessions=["query"],
                        excluded_hashes=["held-out-hash"])
            (out / "seleccion.json").write_text(json.dumps(data))
            construction_frames(data)
            frames[0]["session"] = "query"
            with self.assertRaisesRegex(ValueError, "Fuga"):
                construction_frames(data)
            frames[0]["session"] = "build-0"
            frames[0]["K"] = None
            with self.assertRaisesRegex(ValueError, "calibración"):
                construction_frames(data)
            frames[0]["K"] = k.tolist()
            here = Path(__file__).parent
            with (out / "test.log").open("w") as log:
                result = subprocess.run([sys.executable, str(here / "sfm_aprendido.py"),
                    "reconstruir", "--output", str(out), "--runtime", "30"],
                    stdout=log, stderr=subprocess.STDOUT, timeout=60)
            self.assertEqual(result.returncode, 0, (out / "test.log").read_text()[-8000:])
            summary = json.loads((out / "resultado.json").read_text())
            self.assertEqual(summary["scale"], "arbitrary, not metres")
            self.assertEqual(summary["models"][0]["images"], 4)
            self.assertGreaterEqual(summary["models"][0]["points"], 150)
            self.assertLess(summary["models"][0]["mean_reprojection_original_px"], 0.1)
            # Read native coordinates and poses in an isolated process as well.
            code = '''
import sys, numpy as np, pycolmap as pc
from pathlib import Path
out=Path(sys.argv[1])
with pc.Database.open(out/'database.db') as db:
    im=db.read_all_images()[0];cam=db.read_camera(im.camera_id)
    xy=db.read_keypoints(im.image_id)
    assert cam.params[2] == 640.5 and cam.params[3] == 360.5
    expected=np.load(out/'keypoints-0.npz')['keypoints']
    np.testing.assert_allclose(xy,expected+.5,atol=1e-4)
rec=pc.Reconstruction(out/'modelos/0')
c={im.name:im.projection_center() for im in rec.images.values()}
dist=[np.linalg.norm(c[f'view{i}.png']-c['view0.png']) for i in [1,2,3]]
np.testing.assert_allclose(np.array(dist)/dist[0],[1,3,5],atol=.01)
rotation=pc.Rotation3d(np.array([[0.,-1,0],[1,0,0],[0,0,1]]))
rec.transform(pc.Sim3d(scale=3.7,rotation=rotation,translation=[8.,-4.,2.]))
(out/'transformed').mkdir()
rec.write(out/'transformed')
'''
            checked = subprocess.run([sys.executable, "-c", code, str(out)], capture_output=True, text=True)
            self.assertEqual(checked.returncode, 0, checked.stderr)
            comparison = subprocess.run([sys.executable, str(here / "sfm_diagnostico.py"), "comparar",
                "--source", str(out / "modelos/0"), "--other", str(out / "transformed"),
                "--anchor", "view0.png", "--dataset", str(out / "seleccion.json"),
                "--output", str(out / "comparison.json")], capture_output=True, text=True)
            self.assertEqual(comparison.returncode, 0, comparison.stderr)
            consistency = json.loads((out / "comparison.json").read_text())
            self.assertLess(consistency["centre_error_percent_diameter"]["max"], 1e-5)
            for row in consistency["relative_rotation_degrees"].values():
                self.assertLess(row["max"], 1e-5)
            # A repeated run must not silently append to or overwrite an experiment.
            repeated = subprocess.run([sys.executable, str(here / "sfm_aprendido.py"),
                "reconstruir", "--output", str(out)], capture_output=True, text=True)
            self.assertNotEqual(repeated.returncode, 0)
            self.assertIn("La base ya existe", repeated.stderr)


if __name__ == "__main__":
    unittest.main()
