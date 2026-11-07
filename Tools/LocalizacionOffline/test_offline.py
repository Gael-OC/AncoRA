import json
from pathlib import Path
import socket
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import cv2
import numpy as np
from scipy.spatial.transform import Rotation

from caracteristicas import Features, no_network
from datos import inventory, select_pairs
from geometria import estimate_pose, pose_matrix, refine_sequence, triangulate
import offline
from alineacion import (MIRROR, align_arcore, cam_from_world, import_realityscan, load_georef,
                       read_colmap_text, t_camera_ar, write_colmap_text)


def project(xyz, T, K):
    camera = xyz @ T[:3, :3].T + T[:3, 3]
    p = camera @ K.T
    return p[:, :2] / p[:, 2:]


class GeometryTests(unittest.TestCase):
    def setUp(self):
        rng = np.random.default_rng(28)
        self.xyz = rng.uniform([-2., -1., 8.], [2., 1., 20.], (160, 3))
        self.K = np.array([[1100., 0, 960], [0, 1100., 540], [0, 0, 1]])
        self.T = pose_matrix([0.015, -0.08, 0.02], [0.8, 0.1, -0.3])

    def test_camera_pose_rejects_false_matches_and_recovers_metric_position(self):
        xy = project(self.xyz, self.T, self.K)
        xy[:40] = np.random.default_rng(1).uniform([0, 0], [1920, 1080], (40, 2))
        result = estimate_pose(self.xyz, xy, self.K)
        self.assertIsNotNone(result)
        self.assertGreaterEqual(result["inliers"], 115)
        np.testing.assert_allclose(result["T_camera_map"], self.T, atol=1e-4)

    def test_no_pose_from_too_few_or_nonfinite_points(self):
        self.assertIsNone(estimate_pose(self.xyz[:5], np.zeros((5, 2)), self.K))
        self.assertIsNone(estimate_pose(self.xyz, np.full((160, 2), np.nan), self.K))

    def test_triangulation_rejects_zero_baseline_and_recovers_metric_points(self):
        identity = np.eye(4)
        a, b = project(self.xyz, identity, self.K), project(self.xyz, self.T, self.K)
        xyz, keep = triangulate(a, b, self.K, self.K, identity, self.T)
        self.assertGreater(int(keep.sum()), 100)
        np.testing.assert_allclose(xyz[keep], self.xyz[keep], atol=1e-6)
        _, same = triangulate(a, a, self.K, self.K, identity, identity)
        self.assertFalse(same.any())

    def test_sequence_recovers_one_map_alignment_from_multiple_camera_poses(self):
        map_ar = pose_matrix([0.01, 0.05, -0.01], [0.4, -0.2, 0.3])
        observations = []
        for x in (0., 0.25, 0.5):
            camera_ar = pose_matrix([0, -0.02 * x, 0], [-x, 0, 0])
            xy = project(self.xyz, camera_ar @ np.linalg.inv(map_ar), self.K)
            xy[:5] += 80  # Temporal refinement must resist repeated false correspondences.
            observations.append(dict(xyz=self.xyz, xy=xy, K=self.K, T_camera_ar=camera_ar))
        seed = map_ar.copy()
        seed[:3, 3] += [0.1, -0.05, 0.04]
        result, error = refine_sequence(observations, seed)
        self.assertLess(np.linalg.norm(result[:3, 3] - map_ar[:3, 3]), 0.03)
        self.assertLess(error, 0.5)


class DataTests(unittest.TestCase):
    def test_import_never_uses_immersal_result_as_ground_truth(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            (root / "photo.png").write_bytes(b"image")
            row = dict(archivo="photo.png", ancho=1920, alto=1080,
                       intrinsecos=dict(fx=1200, fy=1200, cx=960, cy=540),
                       resultado=dict(mapa=152693, conf=100), t=12)
            (root / "intentos.jsonl").write_text(json.dumps(row))
            data = inventory(root)
            self.assertIsNone(data["ground_truth"])
            self.assertIsNone(data["frames"][0]["capture_time"])
            self.assertNotIn("resultado", data["frames"][0])

    def test_rejects_paths_outside_recording_directory(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            (root / "intentos.jsonl").write_text(json.dumps(dict(archivo="../../outside.png")))
            with self.assertRaises(ValueError):
                inventory(root)

    def test_inventory_reports_images_without_metadata_without_inventing_frames(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            session = root / "complete"
            session.mkdir()
            (session / "photo.png").write_bytes(b"image")
            orphan = root / "Teologia J"
            orphan.mkdir()
            (orphan / "0001-TEO-C-15m.png").write_bytes(b"orphan image")
            row = dict(archivo="photo.png", ancho=1920, alto=1080,
                       intrinsecos=dict(fx=1200, fy=1200, cx=960, cy=540))
            (session / "intentos.jsonl").write_text(json.dumps(row))
            data = inventory(root)
            self.assertEqual(len(data["frames"]), 1)
            self.assertEqual(data["unreferenced_images"], ["Teologia J/0001-TEO-C-15m.png"])

    def test_pair_queries_do_not_reuse_session_viewpoint_or_image(self):
        frames = [dict(id=str(i), session=str(i), point=p, building=b, sha256=str(i))
                  for i, (p, b) in enumerate((("A", "TEO"), ("A", "TEO"), ("B", "TEO"), ("A", "EIC")))]
        pairs = select_pairs(frames)
        self.assertTrue(any(same for _, _, same in pairs))
        self.assertTrue(any(not same for _, _, same in pairs))
        self.assertFalse(any(a["point"] == b["point"] and same for a, b, same in pairs))

    def test_network_is_blocked_and_restored(self):
        original = socket.socket.connect
        with no_network(), socket.socket() as s:
            with self.assertRaisesRegex(RuntimeError, "Red deshabilitada"):
                s.connect(("127.0.0.1", 9))
        self.assertIs(socket.socket.connect, original)

    def test_real_sift_feature_coordinates_survive_resize(self):
        with tempfile.TemporaryDirectory() as d:
            rng = np.random.default_rng(3)
            image = rng.integers(0, 256, (480, 640), dtype=np.uint8)
            file = Path(d) / "texture.png"
            cv2.imwrite(str(file), image)
            model = Features("sift", 320, 256)
            f = model.extract(file)
            self.assertGreater(len(f["keypoints"]), 20)
            self.assertTrue((f["keypoints"][:, 0] < 640).all())
            self.assertGreater(len(model.match(f, f)), 20)


class PipelineTests(unittest.TestCase):
    setUp = GeometryTests.setUp

    def test_registered_map_to_held_out_query_and_leakage_rejection(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            transforms = [pose_matrix([0, 0, 0], [-x, 0, 0]) for x in (0., 0.8, 1.6)]
            frames = [dict(id=f"ref-{i}", path=f"ref-{i}", sha256=f"hash-{i}", session=f"map-{i}",
                           building="TEO", K=self.K.tolist(), T_camera_map=T.tolist()) for i, T in enumerate(transforms)]
            query = dict(id="query", path="query", sha256="query-hash", session="held-out", K=self.K.tolist())
            features = {f["id"]: {"keypoints": project(self.xyz, T, self.K)} for f, T in zip(frames, transforms)}
            features["query"] = {"keypoints": project(self.xyz, self.T, self.K)}
            class Frontend:
                def cached(_, frame, cache): return features[frame["id"]]
                def extract(_, path): return features[path]
                def match(_, a, b): return np.c_[np.arange(160), np.arange(160)]
            mapping = root / "mapping.json"
            mapping.write_text(json.dumps(dict(kind="registered_mapping", metric_source="synthetic test only", frames=frames)))
            args = SimpleNamespace(dataset=str(mapping), output=str(root / "map.json"), cache="", method="sift", max_side=1024, keypoints=2048)
            with patch.object(offline, "backend", return_value=Frontend()):
                offline.build_map(args)
                args.map = args.output
                args.output = str(root / "poses.json")
                args.dataset = str(root / "queries.json")
                Path(args.dataset).write_text(json.dumps(dict(frames=[query])))
                offline.localize(args)
                row = json.loads(Path(args.output).read_text())["rows"][0]
                self.assertTrue(row["accepted"])
                self.assertFalse(row["alignment_verified"])
                self.assertIsNone(row["ground_truth"])
                np.testing.assert_allclose(row["T_camera_map"], self.T, atol=1e-4)
                query["session"] = "map-0"
                Path(args.dataset).write_text(json.dumps(dict(frames=[query])))
                with self.assertRaisesRegex(ValueError, "Fuga de datos"):
                    offline.localize(args)
                query["session"] = "held-out"
                Path(args.dataset).write_text(json.dumps(dict(frames=[query])))
                visual_map = json.loads(Path(args.map).read_text())
                visual_map["feature_signature"] = "outdated-weights"
                Path(args.map).write_text(json.dumps(visual_map))
                with self.assertRaisesRegex(ValueError, "otro extractor"):
                    offline.localize(args)


def look_at(center, target, up=(0., 0., 1.)):
    """OpenCV camera-to-world rotation (x right, y down, z forward) in a right-handed z-up world."""
    z = np.asarray(target, float) - center
    z /= np.linalg.norm(z)
    x = np.cross(z, up)
    x /= np.linalg.norm(x)
    return np.c_[x, np.cross(z, x), z]


def camera_center_of(T):
    return -T[:3, :3].T @ T[:3, 3]


def trajectory(t):
    """Smooth walk with changing speed, a few metres long, in a right-handed ENU-like world."""
    u = t + 0.15 * np.sin(1.7 * t)
    return np.array([1.2 * u, 0.8 * np.sin(0.9 * u) + 0.03 * u * u, 1.6 + 0.2 * np.cos(1.3 * u)])


class RealityScanImportTests(unittest.TestCase):
    def make_scene(self, folder):
        """Cameras with known metric poses -> COLMAP text in an arbitrary frame + the georef that undoes it."""
        rng = np.random.default_rng(5)
        K = np.array([[1300., 0, 960], [0, 1300., 540], [0, 0, 1]])
        truth = {}
        for i in range(12):
            center = np.array([-10. + 2.0 * i, -20. + rng.uniform(-1, 1), 8. + rng.uniform(-1, 1)])
            Rwc = look_at(center, [0., 0., 6.])
            T = np.eye(4)
            T[:3, :3] = Rwc.T
            T[:3, 3] = -Rwc.T @ center
            truth[f"DJI_{i:04d}.JPG"] = T          # metric camera-from-ENU
        s, Rg = 0.947, Rotation.from_euler("zyx", [31, 4, -2], degrees=True).as_matrix()
        mp, mq = np.array([-3., -30., -4.]), np.array([0.5, -0.1, 28.])
        images = []
        for name, T in truth.items():
            c_colmap = Rg.T @ (camera_center_of(T) - mq) / s + mp     # inverse of ENU = s Rg (colmap - mp) + mq
            R_cw = T[:3, :3] @ Rg
            Tc = np.eye(4)
            Tc[:3, :3] = R_cw
            Tc[:3, 3] = -R_cw @ c_colmap
            images.append((name, Tc, 1))
        write_colmap_text(Path(folder) / "model", {1: ("PINHOLE", 1920, 1080, [1300., 1300., 960., 540.])}, images)
        georef = {"escala": s, "R": Rg.tolist(), "centroColmap": mp.tolist(), "centroEnu": mq.tolist(),
                  "residuoM": {"mediana": 0.47, "p90": 0.83, "max": 1.4}}
        return truth, georef, K

    def test_recovers_metric_poses_and_records_source(self):
        with tempfile.TemporaryDirectory() as folder:
            truth, georef, K = self.make_scene(folder)
            result = import_realityscan(Path(folder) / "model", georef, "X1", "dron-vuelo")
            self.assertEqual(result["kind"], "registered_mapping")
            self.assertIn("georreferencia GPS del dron, residuo mediana 0.47 m", result["metric_source"])
            self.assertFalse(result["alignment_verified"])
            self.assertEqual(len(result["frames"]), 12)
            for frame in result["frames"]:
                np.testing.assert_allclose(frame["T_camera_map"], truth[frame["id"]], atol=1e-6)
                np.testing.assert_allclose(frame["K"], K)
                self.assertEqual((frame["building"], frame["session"]), ("X1", "dron-vuelo"))

    def test_crop_keeps_only_cameras_that_see_the_box(self):
        with tempfile.TemporaryDirectory() as folder:
            truth, georef, _ = self.make_scene(folder)
            full = import_realityscan(Path(folder) / "model", georef, "X1", "s")
            near = import_realityscan(Path(folder) / "model", georef, "X1", "s", box=[0, 0, 6, 4, 4, 6], max_distance=21)
            self.assertLess(len(near["frames"]), len(full["frames"]))
            self.assertEqual(near["recorte"]["descartadas"], len(full["frames"]) - len(near["frames"]))
            with self.assertRaisesRegex(ValueError, "menos de dos"):
                import_realityscan(Path(folder) / "model", georef, "X1", "s", box=[500, 500, 6, 4, 4, 6])

    def test_campus_transform_uses_east_north_up_conventions(self):
        with tempfile.TemporaryDirectory() as folder:
            truth, georef, _ = self.make_scene(folder)
            campus = {"edificios": {"X1": {"yaw": 90.0, "t": [10.0, 3.0, -4.0]}}}   # (E, Up, N)
            result = import_realityscan(Path(folder) / "model", georef, "X1", "s", campus=campus, campus_building="X1")
            frame = result["frames"][0]
            center_campus = camera_center_of(np.asarray(frame["T_camera_map"]))
            e, n, u = camera_center_of(truth[frame["id"]])
            # rot(p, 90) of register.js: e' = n, n' = -e; then t = (E 10, Up 3, N -4)
            np.testing.assert_allclose(center_campus, [n + 10.0, -e - 4.0, u + 3.0], atol=1e-6)

    def test_rejects_distorted_cameras_and_missing_keys(self):
        with tempfile.TemporaryDirectory() as folder:
            truth, georef, _ = self.make_scene(folder)
            (Path(folder) / "model" / "cameras.txt").write_text("1 OPENCV 1920 1080 1300 1300 960 540 0.1 0 0 0\n")
            with self.assertRaisesRegex(ValueError, "distorsión"):
                import_realityscan(Path(folder) / "model", georef, "X1", "s")
            with self.assertRaisesRegex(ValueError, "centroEnu"):
                load_georef({k: v for k, v in georef.items() if k != "centroEnu"})

    def test_image_hash_and_size_check(self):
        with tempfile.TemporaryDirectory() as folder:
            truth, georef, _ = self.make_scene(folder)
            photos = Path(folder) / "fotos"
            photos.mkdir()
            for name in truth:
                cv2.imwrite(str(photos / name), np.zeros((1080, 1920, 3), np.uint8))
            result = import_realityscan(Path(folder) / "model", georef, "X1", "s", images_dir=photos)
            self.assertEqual(result["sin_imagen"], 0)
            self.assertEqual(len(result["frames"][0]["sha256"]), 64)
            cv2.imwrite(str(photos / "DJI_0000.JPG"), np.zeros((100, 100, 3), np.uint8))
            with self.assertRaisesRegex(ValueError, "mide"):
                import_realityscan(Path(folder) / "model", georef, "X1", "s", images_dir=photos)


class ArcoreAlignmentTests(unittest.TestCase):
    SIM = dict(s=2.7, R=Rotation.from_euler("zyx", [40, 7, -5], degrees=True).as_matrix(), t=np.array([3., -1., 0.5]))

    def scene(self, folder, degrees, shift=0.03, n=24, duration=4.8, speed=1.0):
        """AR rows hold true(t_i - shift); SfM images hold true(t_i) in an arbitrary scaled frame."""
        C = Rotation.from_euler("z", degrees, degrees=True).as_matrix() @ np.diag([1., -1., 1.])
        times = 100.0 + np.arange(n) * duration / n

        def true_pose(t):
            u = speed * (t - 100.0)
            p = trajectory(u)
            return p, look_at(p, [2. + np.sin(t - 100.0), 3., 2.8 + 0.4 * np.sin(t - 100.0)])

        frames, images = [], []
        for i, t in enumerate(times):
            p_ar, R_ar = true_pose(t - shift)          # what ARCore logged for frame i (right-handed, OpenCV camera)
            R_unity = MIRROR @ R_ar @ C.T              # R_world_opencv = MIRROR @ R_unity @ C
            quat = Rotation.from_matrix(R_unity).as_quat()
            frames.append({"id": f"s/{i:04d}.png", "session": "barrido", "tracking_epoch": 0, "sensor_time": float(t),
                           "position_unity": (MIRROR @ p_ar).tolist(), "rotation_unity_xyzw": quat.tolist(),
                           "K": [[1300, 0, 960], [0, 1300, 540], [0, 0, 1]], "width": 1920, "height": 1080})
            p_img, R_img = true_pose(t)                # the SfM camera sees the world at the image time
            c_sfm = self.SIM["R"].T @ (p_img - self.SIM["t"]) / self.SIM["s"]
            R_cw = (self.SIM["R"].T @ R_img).T
            T = np.eye(4)
            T[:3, :3] = R_cw
            T[:3, 3] = -R_cw @ c_sfm
            images.append((f"s/{i:04d}.png", T, 1))
        write_colmap_text(Path(folder) / "sfm", {1: ("PINHOLE", 1920, 1080, [1300., 1300., 960., 540.])}, images)
        return {"frames": frames}, read_colmap_text(Path(folder) / "sfm")

    def test_recovers_scale_offset_and_axis_convention(self):
        for degrees in (0, 90, 180, 270):
            with self.subTest(rotation=degrees), tempfile.TemporaryDirectory() as folder:
                inventory_data, model = self.scene(folder, degrees)
                result = align_arcore(inventory_data, model)
                self.assertTrue(result["pose_optica_validada"], result["motivo"])
                self.assertLess(result["residuo_posicion_cm"]["rms"], 0.5)
                self.assertAlmostEqual(result["escala_arcore_por_sfm"], self.SIM["s"], delta=0.01)
                self.assertAlmostEqual(result["desfase_ms"], 30.0, delta=3.0)
                convention = result["convencion"]
                self.assertEqual((convention["rotacion_imagen_grados"], convention["flip_y"]), (degrees, True))
                self.assertLess(convention["error_rotacion_grados"], 0.5)
                np.testing.assert_allclose(np.asarray(result["T_ar_sfm"])[:3], np.c_[self.SIM["R"], self.SIM["t"]], atol=0.05)

    def test_t_camera_ar_agrees_with_the_sfm_camera(self):
        with tempfile.TemporaryDirectory() as folder:
            inventory_data, model = self.scene(folder, 90, shift=0.0)
            result = align_arcore(inventory_data, model)
            C = np.asarray(result["convencion"]["C"])
            frame = inventory_data["frames"][3]
            T_ar = t_camera_ar(frame["position_unity"], frame["rotation_unity_xyzw"], C)
            # SfM camera centre mapped to AR must be the AR camera centre; same for the optical axis.
            sim = np.asarray(result["T_ar_sfm"])
            c_sfm = camera_center_of(cam_from_world(model["images"][3]))
            c_ar = result["escala_arcore_por_sfm"] * sim[:3, :3] @ c_sfm + sim[:3, 3]
            np.testing.assert_allclose(camera_center_of(T_ar), c_ar, atol=1e-2)
            axis_sfm = cam_from_world(model["images"][3])[2, :3]
            np.testing.assert_allclose(T_ar[2, :3], sim[:3, :3] @ axis_sfm, atol=1e-2)

    def test_pure_rotation_sweep_is_not_validated(self):
        with tempfile.TemporaryDirectory() as folder:
            inventory_data, model = self.scene(folder, 0, speed=0.012)   # about 0.1 m of travel, camera still turning
            result = align_arcore(inventory_data, model)
            self.assertFalse(result["pose_optica_validada"])
            self.assertLess(result["recorrido_m"], 0.2)
            self.assertIn("recorrido", result["motivo"])

    def test_position_noise_above_threshold_and_few_cameras_are_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            inventory_data, model = self.scene(folder, 0, shift=0.0)
            noisy = json.loads(json.dumps(inventory_data))
            rng = np.random.default_rng(3)
            for frame in noisy["frames"]:
                frame["position_unity"] = (np.array(frame["position_unity"]) + rng.normal(0, 0.12, 3)).tolist()
            result = align_arcore(noisy, model)
            self.assertFalse(result["pose_optica_validada"])
            self.assertIn("residuo de posición", result["motivo"])
            few = align_arcore(inventory_data, model, min_cameras=40)
            self.assertFalse(few["pose_optica_validada"])
            self.assertIn("cámaras con pose", few["motivo"])

    def test_cli_writes_report_and_marks_inventory_only_when_validated(self):
        with tempfile.TemporaryDirectory() as folder:
            inventory_data, _ = self.scene(folder, 0)
            root = Path(folder)
            (root / "inv.json").write_text(json.dumps(inventory_data))
            args = SimpleNamespace(dataset=str(root / "inv.json"), model=str(root / "sfm"), sesion=None, umbral_cm=5.0,
                                   umbral_grados=3.0, min_camaras=10, min_recorrido=1.0, desfase_max_ms=100.0,
                                   paso_ms=5.0, inventario_salida=str(root / "inv2.json"), output=str(root / "r.json"))
            offline.align_arcore(args)
            self.assertTrue(json.loads((root / "r.json").read_text())["pose_optica_validada"])
            marked = json.loads((root / "inv2.json").read_text())["frames"]
            self.assertGreaterEqual(sum(bool(f.get("optical_pose_validated")) for f in marked), 20)


if __name__ == "__main__":
    unittest.main()
