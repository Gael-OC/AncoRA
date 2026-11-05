"""Four fixed CPU baselines. Downloads are allowed ONLY by the explicit preparar command."""
from contextlib import contextmanager
import hashlib
from pathlib import Path
import socket
import sys
import time

import cv2
import numpy as np

HERE = Path(__file__).resolve().parent
METHODS = ("sift", "sift-lightglue", "aliked-lightglue", "xfeat-lighterglue")
REVISIONS = {"LightGlue": "5a650c444718caffc2669dc0094cde908f42dd73",
             "XFeat": "e92685f57f8318b18725c5c8c0bd28c7fe188d9a"}


def feature_signature(method, max_side, keypoints):
    manifest = HERE / ".weights" / "manifest.json"
    weights = hashlib.sha256(manifest.read_bytes()).hexdigest() if manifest.is_file() else "sin-pesos"
    code = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    identity = f"{method}:{max_side}:{keypoints}:{REVISIONS}:{cv2.__version__}:{weights}:{code}"
    return hashlib.sha256(identity.encode()).hexdigest()


@contextmanager
def no_network():
    connect, connect_ex = socket.socket.connect, socket.socket.connect_ex
    def blocked(*args, **kwargs):
        raise RuntimeError("Red deshabilitada: ejecuta preparar una vez para obtener los pesos locales.")
    socket.socket.connect = socket.socket.connect_ex = blocked
    try:
        yield
    finally:
        socket.socket.connect, socket.socket.connect_ex = connect, connect_ex


class Features:
    def __init__(self, method="sift", max_side=1024, keypoints=2048):
        if method not in METHODS or max_side < 64 or keypoints < 32:
            raise ValueError("Método, resolución o cantidad de puntos inválidos.")
        self.method, self.max_side, self.keypoints = method, max_side, keypoints
        self.extractor = self.matcher = self.torch = None
        if method.startswith("sift"):
            self.extractor = cv2.SIFT_create(nfeatures=keypoints, contrastThreshold=0.0066667)
        if method == "sift":
            return
        import torch
        self.torch = torch
        torch.set_num_threads(4)
        torch.hub.set_dir(str(HERE / ".weights"))
        sys.path.insert(0, str(HERE / ".deps" / "LightGlue"))
        if method.startswith("xfeat"):
            sys.path.insert(0, str(HERE / ".deps" / "XFeat"))
            from modules.xfeat import XFeat
            # Force the reference benchmark to CPU even on machines with CUDA.
            self.extractor = XFeat(top_k=keypoints)
            self.extractor.dev = torch.device("cpu")
            self.extractor.net.cpu()
            from modules.lighterglue import LighterGlue
            self.matcher = LighterGlue().eval().cpu()
            self.matcher.dev = torch.device("cpu")
            self.matcher.net.conf.flash = False
        else:
            # LightGlue imports its optional COLMAP SIFT backend at package import. On macOS its native OpenMP
            # conflicts with PyTorch's runtime. We use OpenCV SIFT, and import COLMAP only in its separate CLI command.
            saved_colmap = sys.modules.get("pycolmap")
            sys.modules["pycolmap"] = None
            try:
                from lightglue import ALIKED, LightGlue
            finally:
                if saved_colmap is None:
                    del sys.modules["pycolmap"]
                else:
                    sys.modules["pycolmap"] = saved_colmap
            name = "aliked" if method.startswith("aliked") else "sift"
            if name == "aliked":
                self.extractor = ALIKED(max_num_keypoints=keypoints).eval().cpu()
            self.matcher = LightGlue(features=name, flash=False).eval().cpu()

    def extract(self, path):
        image = cv2.imread(str(path), cv2.IMREAD_GRAYSCALE)
        if image is None:
            raise ValueError(f"No se puede leer la imagen: {path}")
        h, w = image.shape
        ratio = min(1., self.max_side / max(h, w))
        size = (max(1, round(w * ratio)), max(1, round(h * ratio)))
        small = cv2.resize(image, size, interpolation=cv2.INTER_AREA) if size != (w, h) else image
        scale = np.array([size[0] / w, size[1] / h], dtype=np.float32)
        start = time.perf_counter()
        if self.method.startswith("sift"):
            kp, desc = self.extractor.detectAndCompute(small, None)
            desc = np.empty((0, 128), dtype=np.float32) if desc is None else desc
            # RootSIFT, as used by the learned SIFT matcher.
            desc = np.sqrt(desc / np.maximum(desc.sum(axis=1, keepdims=True), 1e-12))
            feat = {"keypoints": np.array([p.pt for p in kp], dtype=np.float32).reshape(-1, 2),
                    "descriptors": desc, "scales": np.array([p.size for p in kp], dtype=np.float32),
                    "oris": np.deg2rad(np.array([p.angle for p in kp], dtype=np.float32))}
        else:
            torch = self.torch
            tensor = torch.from_numpy(small.copy()).float()[None, None] / 255.
            with torch.inference_mode():
                if self.method.startswith("xfeat"):
                    raw = self.extractor.detectAndCompute(tensor)[0]
                else:
                    raw = {k: v[0] for k, v in self.extractor.extract(tensor, resize=None).items()}
            feat = {k: v.detach().cpu().numpy() for k, v in raw.items() if hasattr(v, "detach")}
        feat["keypoints"] = (feat["keypoints"] + 0.5) / scale - 0.5
        if "scales" in feat:
            feat["scales"] /= float(np.sqrt(scale.prod()))
        feat["image_size"] = np.array([w, h], dtype=np.float32)
        feat["extract_seconds"] = np.array(time.perf_counter() - start)
        return feat

    def match(self, a, b):
        if min(len(a["keypoints"]), len(b["keypoints"])) < 2:
            return np.empty((0, 2), dtype=np.int64)
        if self.method == "sift":
            matcher = cv2.BFMatcher(cv2.NORM_L2)
            ab = matcher.knnMatch(a["descriptors"].astype(np.float32), b["descriptors"].astype(np.float32), k=2)
            ba = matcher.match(b["descriptors"].astype(np.float32), a["descriptors"].astype(np.float32))
            reverse = {m.queryIdx: m.trainIdx for m in ba}
            return np.array([(m.queryIdx, m.trainIdx) for m, n in ab
                             if m.distance < 0.8 * n.distance and reverse.get(m.trainIdx) == m.queryIdx],
                            dtype=np.int64).reshape(-1, 2)
        torch = self.torch
        def tensor(f):
            return {k: torch.from_numpy(np.asarray(v).copy()).float()[None]
                    for k, v in f.items() if k != "extract_seconds"}
        with torch.inference_mode():
            ta, tb = tensor(a), tensor(b)
            if self.method.startswith("xfeat"):
                result = self.matcher({**{k + "0": v for k, v in ta.items()}, **{k + "1": v for k, v in tb.items()}})
            else:
                result = self.matcher({"image0": ta, "image1": tb})
        return result["matches"][0].detach().cpu().numpy().astype(np.int64)

    def cached(self, frame, folder):
        # Settings and source hash are part of the key; changed descriptors cannot silently reuse an index.
        if hashlib.sha256(Path(frame["path"]).read_bytes()).hexdigest() != frame["sha256"]:
            raise ValueError(f"La imagen cambió desde el inventario: {frame['id']}")
        signature = feature_signature(self.method, self.max_side, self.keypoints)
        identity = f"{signature}:{frame['sha256']}"
        key = hashlib.sha256(identity.encode()).hexdigest()
        path = Path(folder) / (key + ".npz")
        if path.is_file():
            with np.load(path, allow_pickle=False) as saved:
                return {k: saved[k] for k in saved.files}
        result = self.extract(frame["path"])
        path.parent.mkdir(parents=True, exist_ok=True)
        np.savez_compressed(path, **result)
        return result
