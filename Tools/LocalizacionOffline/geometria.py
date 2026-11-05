"""Camera geometry in OpenCV coordinates: world-to-camera poses, x right/y down/z forward."""
import cv2
import numpy as np
from scipy.optimize import least_squares
from scipy.spatial.transform import Rotation


def pose_matrix(rvec, tvec):
    pose = np.eye(4)
    pose[:3, :3] = cv2.Rodrigues(np.asarray(rvec, dtype=float))[0]
    pose[:3, 3] = np.asarray(tvec).reshape(3)
    return pose


def estimate_pose(xyz, xy, K, threshold=3.0, min_inliers=20):
    xyz, xy, K = (np.asarray(v, dtype=np.float64) for v in (xyz, xy, K))
    if len(xyz) < max(6, min_inliers) or not all(np.isfinite(v).all() for v in (xyz, xy, K)):
        return None
    ok, r, t, indices = cv2.solvePnPRansac(xyz, xy, K, None, iterationsCount=1000,
                                         reprojectionError=threshold, confidence=0.999,
                                         flags=cv2.SOLVEPNP_EPNP)
    if not ok or indices is None or len(indices) < min_inliers:
        return None
    keep = indices.ravel()
    r, t = cv2.solvePnPRefineLM(xyz[keep], xy[keep], K, None, r, t)
    T = pose_matrix(r, t)
    projected = cv2.projectPoints(xyz, r, t, K, None)[0].reshape(-1, 2)
    errors = np.linalg.norm(projected - xy, axis=1)
    depths = (xyz @ T[:3, :3].T + T[:3, 3])[:, 2]
    keep = np.flatnonzero((errors <= threshold) & (depths > 0))
    if len(keep) < min_inliers or len(keep) / len(xyz) < 0.25:
        return None
    return {"T_camera_map": T, "indices": keep, "inliers": len(keep),
            "reprojection_median_px": float(np.median(errors[keep]))}


def triangulate(a, b, K0, K1, T0, T1, threshold=3.0):
    a, b = np.asarray(a), np.asarray(b)
    if not len(a):
        return np.empty((0, 3)), np.zeros(0, dtype=bool)
    h = cv2.triangulatePoints(K0 @ T0[:3], K1 @ T1[:3], a.T.astype(float), b.T.astype(float))
    finite = np.abs(h[3]) > 1e-10
    xyz = (h[:3] / np.where(finite, h[3], 1)).T
    valid = finite & np.isfinite(xyz).all(axis=1)
    centers = [np.linalg.inv(T)[:3, 3] for T in (T0, T1)]
    rays = [xyz - c for c in centers]
    cosine = np.sum(rays[0] * rays[1], axis=1) / np.maximum(1e-12, np.linalg.norm(rays[0], axis=1) * np.linalg.norm(rays[1], axis=1))
    valid &= cosine < np.cos(np.deg2rad(1.0))
    for xy, K, T in ((a, K0, T0), (b, K1, T1)):
        camera = xyz @ T[:3, :3].T + T[:3, 3]
        p = camera @ K.T
        projected = p[:, :2] / np.maximum(p[:, 2:], 1e-12)
        valid &= (camera[:, 2] > 0) & (np.linalg.norm(projected - xy, axis=1) <= threshold)
    return xyz, valid


def refine_sequence(observations, initial_map_from_ar, threshold=3.0):
    """Fit one map<-AR rigid transform to all 2D-3D observations, with fixed short-window odometry.

    Every T_camera_ar must already describe the raw optical camera in the SAME AR epoch.
    Unity/display rotations must be converted by the capture adapter, not guessed here.
    """
    if sum(len(o["xyz"]) for o in observations) < 20:
        raise ValueError("La secuencia necesita al menos 20 correspondencias 2D–3D.")
    seed = np.asarray(initial_map_from_ar, dtype=float)
    x0 = np.r_[Rotation.from_matrix(seed[:3, :3]).as_rotvec(), seed[:3, 3]]

    def unpack(x):
        T = np.eye(4)
        T[:3, :3] = Rotation.from_rotvec(x[:3]).as_matrix()
        T[:3, 3] = x[3:]
        return T

    def residual(x):
        ar_from_map = np.linalg.inv(unpack(x))
        errors = []
        for o in observations:
            T = np.asarray(o["T_camera_ar"]) @ ar_from_map
            xyz = np.asarray(o["xyz"]) @ T[:3, :3].T + T[:3, 3]
            uv = xyz @ np.asarray(o["K"]).T
            p = uv[:, :2] / np.maximum(uv[:, 2:], 1e-6)
            error = p - np.asarray(o["xy"])
            error[xyz[:, 2] <= 0] = 1000
            errors.append(error.ravel())
        return np.concatenate(errors)

    result = least_squares(residual, x0, loss="huber", f_scale=threshold, max_nfev=100)
    return unpack(result.x), float(np.median(np.linalg.norm(residual(result.x).reshape(-1, 2), axis=1)))
