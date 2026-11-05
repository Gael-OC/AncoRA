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


if __name__ == "__main__":
    unittest.main()
