"""Audit existing 0068 support without changing matches, cameras, or models.

Depths, angles and conditioning are conditional on each unaccepted reconstruction.
Upright upper/lower groups describe image location, not a new identity filter.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path

import numpy as np
import pycolmap as pc
from scipy.spatial import ConvexHull
from scipy.spatial.transform import Rotation


def quantiles(values):
    return dict(zip(["min", "p10", "median", "p90", "max"],
                    np.quantile(values, [0, .1, .5, .9, 1]).tolist())) if len(values) else None


def ray_angle(point, a, b):
    ra, rb = a-point, b-point
    return float(np.degrees(np.arctan2(np.linalg.norm(np.cross(ra, rb)), ra @ rb)))


def support_jacobian(camera, camera_points):
    """Numerical local pose sensitivity; translation measured in median-depth units.

    Fixed 3D points and K/D, no robust weighting, no covariance/accuracy claim.
    """
    depth = np.median(camera_points[:, 2])
    eps = 1e-6
    columns = []
    for j in range(6):
        delta = np.zeros(3)
        delta[j % 3] = eps
        if j < 3:
            plus, minus = camera_points+delta*depth, camera_points-delta*depth
        else:
            plus = camera_points @ Rotation.from_rotvec(delta).as_matrix().T
            minus = camera_points @ Rotation.from_rotvec(-delta).as_matrix().T
        columns.append(((camera.img_from_cam(plus)-camera.img_from_cam(minus))/(2*eps)).ravel())
    singular = np.linalg.svd(np.array(columns).T, compute_uv=False)
    return {"singular_values_px": singular.tolist(),
            "largest_over_smallest": float(singular[0]/singular[-1]),
            "translation_unit": "median estimated depth; rotation radians",
            "limit": "local fixed-point conditioning only; associations/map uncertainties ignored"}


def analyze(source):
    rec = pc.Reconstruction(str(source/"modelos/0"))
    frames = json.loads((source/"seleccion.json").read_text())["frames"]
    frame_lookup = {f["id"]: f for f in frames}
    im = next(im for im in rec.images.values() if "143744" in im.name and "/0068-" in im.name)
    f = frame_lookup[im.name]
    camera = rec.cameras[im.camera_id]
    rows, xyz, xy, camera_points = [], [], [], []
    sources = {}
    for k, p in enumerate(im.points2D):
        if not p.has_point3D():
            continue
        pt = rec.points3D[p.point3D_id]
        raw = p.xy-.5
        upright = np.array([f["height"]-1-raw[1], raw[0]])
        # Diagnostic boxes inspected in the upright full photograph, not identity labels.
        u, v = upright
        group = ("upper_slot_region" if 900 <= u <= 1080 and 630 <= v <= 770 else
                 "lower_slot_region" if 890 <= u <= 1080 and 1090 <= v <= 1230 else "other_wall")
        obs = []
        for e in pt.track.elements:
            other = rec.images[e.image_id]
            obs.append({"image": other.name, "kp": int(e.point2D_idx),
                        "raw_xy": (other.points2D[e.point2D_idx].xy-.5).tolist()})
            if other.image_id != im.image_id:
                entry = sources.setdefault(other.name, {"target_kps": set(), "angles": [], "point_ids": set()})
                if int(p.point3D_id) not in entry["point_ids"]:
                    entry["angles"].append(ray_angle(pt.xyz, im.projection_center(), other.projection_center()))
                entry["point_ids"].add(int(p.point3D_id))
                entry["target_kps"].add(k)
        cp = im.cam_from_world()*pt.xyz
        rows.append({"kp": k, "point_id_local_only": int(p.point3D_id), "raw_xy": raw.tolist(),
                     "upright_xy": upright.tolist(), "image_group": group,
                     "estimated_depth_arbitrary_units": float(cp[2]), "observations": obs})
        xyz.append(pt.xyz)
        xy.append(raw)
        camera_points.append(cp)
    unique = list({r["point_id_local_only"]: i for i, r in enumerate(rows)}.values())
    xyz, xy, cp = np.array(xyz)[unique], np.array(xy)[unique], np.array(camera_points)[unique]
    centred = xyz-xyz.mean(axis=0)
    shape = np.linalg.svd(centred, compute_uv=False)
    groups = Counter(r["image_group"] for r in rows)
    groups_unique = Counter(rows[i]["image_group"] for i in unique)
    for name, entry in sources.items():
        entry["target_kps"] = sorted(entry["target_kps"])
        entry["unique_points"] = len(entry.pop("point_ids"))
        entry["parallax_estimated_degrees"] = quantiles(entry.pop("angles"))
    index = next(i for i, frame in enumerate(frames) if frame["id"] == im.name)
    expected = np.load(source/f"keypoints-{index}.npz")["keypoints"]
    assert np.allclose(np.array([p.xy-.5 for p in im.points2D]), expected, rtol=0, atol=1e-4)
    return {"source": str(source), "camera_model": camera.model.name,
            "camera_parameters": camera.params.tolist(), "image": im.name,
            "image_sha256": hashlib.sha256(Path(f["path"]).read_bytes()).hexdigest(),
            "registered_cameras": rec.num_reg_images(), "observations": len(rows),
            "unique_3d_points": len(unique), "location_groups_observations": dict(groups),
            "location_groups_unique_points": dict(groups_unique),
            "convex_hull_fraction_image": float(ConvexHull(xy).volume/(f["width"]*f["height"])),
            "raw_bbox_xy": [xy.min(axis=0).tolist(), xy.max(axis=0).tolist()],
            "estimated_depth": quantiles(cp[:, 2]),
            "3d_centred_singular_values_arbitrary_units": shape.tolist(),
            "3d_thickness_over_extent": float(shape[-1]/shape[0]),
            "fixed_point_pose_jacobian": support_jacobian(camera, cp),
            "sources": sources, "tracks": rows}


def check():
    assert abs(ray_angle(np.zeros(3), np.array([0., 0, 1]), np.array([1., 0, 1]))-45) < 1e-10
    cam = pc.Camera.create_from_model_name(1, "PINHOLE", 1000., 1920, 1080)
    pts = np.array([[x, y, 3.+.1*x] for x in [-1., 0., 1.] for y in [-.5, .5]])
    a, b = support_jacobian(cam, pts), support_jacobian(cam, pts*7)
    assert np.allclose(a["singular_values_px"], b["singular_values_px"], rtol=1e-6)
    assert a["largest_over_smallest"] > 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    check()
    args.output.mkdir(parents=True, exist_ok=False)
    result = {"limit": "Not physical validation; no matching/SfM/scale changes. Raw coordinates subtract COLMAP .5.",
              "conditions": [analyze(args.source/n) for n in
                             ["xfeat-baseline", "xfeat-radial_k1", "xfeat-radial_k1-replica"]]}
    (args.output/"soporte.json").write_text(json.dumps(result, ensure_ascii=False, indent=2)+"\n")
    print([{k: c[k] for k in ["source", "observations", "unique_3d_points",
           "location_groups_unique_points", "convex_hull_fraction_image", "estimated_depth",
           "3d_thickness_over_extent", "fixed_point_pose_jacobian"]} for c in result["conditions"]])


if __name__ == "__main__":
    main()
