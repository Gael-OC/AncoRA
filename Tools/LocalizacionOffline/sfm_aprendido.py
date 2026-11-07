"""Image-only learned-feature SfM; arbitrary scale, never a metric localization map.

Run matching and reconstruction in separate processes: PyTorch and COLMAP's native
OpenMP runtimes conflict on this Mac. Features remain in original OpenCV pixels.
"""
import argparse
import hashlib
import json
from itertools import combinations
from pathlib import Path
import time

import numpy as np

from datos import read_json, write_json


def camera_spec(frame):
    """Raw OpenCV pixels + Brown [k1,k2,p1,p2,k3], with COLMAP's .5 offset."""
    k = np.asarray(frame["K"], dtype=float)
    if (k.shape != (3, 3) or not np.isfinite(k).all() or
            min(k[0, 0], k[1, 1]) <= 0 or k[0, 1] != 0 or k[1, 0] != 0 or
            not np.array_equal(k[2], [0, 0, 1])):
        raise ValueError("K debe ser una matriz de cámara sin skew.")
    d = np.asarray(frame.get("distortion", [0.] * 5), dtype=float)
    if d.shape != (5,) or not np.isfinite(d).all():
        raise ValueError("Distorsión debe ser Brown [k1,k2,p1,p2,k3] finita.")
    params = [k[0, 0], k[1, 1], k[0, 2] + .5, k[1, 2] + .5]
    if not d.any():
        return "PINHOLE", params
    if d[4] == 0:
        return "OPENCV", params + d[:4].tolist()
    return "FULL_OPENCV", params + d.tolist() + [0., 0., 0.]


def construction_frames(data):
    if data.get("role") != "construction":
        raise ValueError("Se requiere una selección explícita de construcción.")
    frames = data["frames"]
    excluded_sessions = set(data["excluded_sessions"])
    excluded_hashes = set(data["excluded_hashes"])
    if not frames or len({f["id"] for f in frames}) != len(frames):
        raise ValueError("Construcción vacía o identificadores duplicados.")
    for f in frames:
        if f["session"] in excluded_sessions or f["sha256"] in excluded_hashes:
            raise ValueError("Fuga de consultas en la construcción.")
        if not f.get("K") or not np.isfinite(f["K"]).all():
            raise ValueError("No se inventa calibración para imágenes sin metadatos.")
        k = np.asarray(f["K"])
        if k.shape != (3, 3) or min(k[0, 0], k[1, 1], f["width"], f["height"]) <= 0:
            raise ValueError("Intrínsecos inválidos.")
        camera_spec(f)
        name = Path(f["id"])
        if name.is_absolute() or ".." in name.parts or any(c.isspace() for c in f["id"]):
            raise ValueError("Nombre COLMAP relativo sin espacios requerido.")
        if hashlib.sha256(Path(f["path"]).read_bytes()).hexdigest() != f["sha256"]:
            raise ValueError(f"Cambió la imagen: {f['id']}")
    return frames


def select_views(args):
    data = read_json(args.dataset)
    groups = {}
    for frame in construction_frames(data):
        groups.setdefault(frame["session"], []).append(frame)
    chosen = []
    for session in sorted(groups):
        frames = sorted(groups[session], key=lambda f: f["id"])
        indices = sorted(set(np.linspace(0, len(frames)-1, min(7, len(frames))).round().astype(int)))
        chosen.extend(frames[i] for i in indices)
    if Path(args.output).exists():
        raise ValueError("La selección ya existe; usa otra salida.")
    write_json(args.output, {**data, "frames": chosen,
        "selection": "Seven uniformly spaced ordinal frames per construction session, before matching."})


def correspondences(args):
    from caracteristicas import feature_signature, no_network
    from offline import backend
    data = read_json(args.dataset)
    frames = construction_frames(data)
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=False)
    write_json(out / "seleccion.json", data)
    write_json(out / "condiciones.json", dict(
        method=args.method, max_side=args.max_side, keypoints=args.keypoints,
        feature_signature=feature_signature(args.method, args.max_side, args.keypoints),
        script_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        scale="arbitrary, not metres", intrinsics="fixed recording PINHOLE prior; distortion unvalidated",
        unity_poses_used=False, metric_controls_used=False, query_images_used=False,
        pair_selection="exhaustive within the fixed construction selection", seed=5))
    start = time.perf_counter()
    with no_network():
        model = backend(args)
        features = [model.cached(f, args.cache) for f in frames]
        for i, f in enumerate(features):
            np.savez_compressed(out / f"keypoints-{i}.npz", keypoints=f["keypoints"])
        pairs = list(combinations(range(len(frames)), 2))
        with (out / "pares.jsonl").open("w") as stream:
            for n, (i, j) in enumerate(pairs, 1):
                before = time.perf_counter()
                matches = model.match(features[i], features[j])
                np.savez_compressed(out / f"matches-{i}-{j}.npz", matches=matches)
                stream.write(json.dumps(dict(i=i, j=j, matches=len(matches),
                                             seconds=time.perf_counter() - before)) + "\n")
                stream.flush()
                if n % 100 == 0 or n == len(pairs):
                    print(f"{args.method}: {n}/{len(pairs)} pares, {time.perf_counter()-start:.1f} s", flush=True)


def import_database(out):
    """Import external indices, then verify epipolar geometry; no SIFT rematching."""
    import pycolmap as pc
    frames = construction_frames(read_json(out / "seleccion.json"))
    dbpath = out / "database.db"
    if dbpath.exists():
        raise ValueError("La base ya existe; conserva el ensayo y usa otra salida.")
    cameras, camera_ids, image_ids, keypoints = {}, [], [], []
    verification = pc.TwoViewGeometryOptions(compute_relative_pose=True)
    verification.ransac.random_seed = 5
    verification.ransac.max_error = 4.
    diagnostics = []
    with pc.Database.open(dbpath) as db:
        for i, f in enumerate(frames):
            # OpenCV pixel centres start at 0; COLMAP at .5. Shift principal point too,
            # preserving raw pixels and distortion, rather than pretending rectification.
            model, params = camera_spec(f)
            camera_key = (f.get("phone"), f["session"], f["width"], f["height"], model, *params)
            if camera_key not in cameras:
                cam = pc.Camera(model=model, width=f["width"], height=f["height"],
                                params=params, has_prior_focal_length=True)
                cam.camera_id = db.write_camera(cam)
                cameras[camera_key] = cam
            cam = cameras[camera_key]
            camera_ids.append(cam)
            image_ids.append(db.write_image(pc.Image(name=f["id"], camera_id=cam.camera_id)))
            with np.load(out / f"keypoints-{i}.npz", allow_pickle=False) as saved:
                xy = saved["keypoints"].astype(np.float32) + .5
            if xy.ndim != 2 or xy.shape[1] != 2 or not np.isfinite(xy).all():
                raise ValueError("Coordenadas de características inválidas.")
            keypoints.append(xy)
            db.write_keypoints(image_ids[-1], xy)
        for i, j in combinations(range(len(frames)), 2):
            with np.load(out / f"matches-{i}-{j}.npz", allow_pickle=False) as saved:
                matches = saved["matches"]
            if matches.ndim != 2 or matches.shape[1] != 2 or not np.issubdtype(matches.dtype, np.integer):
                raise ValueError("Índices de correspondencias inválidos.")
            if matches.size and (matches.min() < 0 or
                                (matches >= [len(keypoints[i]), len(keypoints[j])]).any()):
                raise ValueError("Índice de correspondencia fuera de rango.")
            matches = matches.astype(np.uint32)
            db.write_matches(image_ids[i], image_ids[j], matches)
            geom = pc.estimate_two_view_geometry(camera_ids[i], keypoints[i], camera_ids[j], keypoints[j],
                                                 matches, verification)
            db.write_two_view_geometry(image_ids[i], image_ids[j], geom)
            diagnostics.append(dict(i=i, j=j, matches=len(matches), inliers=len(geom.inlier_matches),
                                    config=str(geom.config), tri_angle_degrees=float(np.degrees(geom.tri_angle))))
        # Create one independent frame per image; no multi-camera or motion prior.
        pc.apply_rig_config([], db)
    write_json(out / "verificacion.json", dict(pycolmap=pc.__version__,
               options=verification.todict(), pairs=diagnostics))
    return dbpath


def reconstruct(args):
    import pycolmap as pc
    out = Path(args.output)
    dbpath = import_database(out)
    images = out / "imagenes"
    for f in read_json(out / "seleccion.json")["frames"]:
        target = images / f["id"]
        target.parent.mkdir(parents=True, exist_ok=True)
        target.symlink_to(Path(f["path"]).resolve())
    opts = pc.IncrementalPipelineOptions(num_threads=4, random_seed=5, min_model_size=3,
        max_runtime_seconds=args.runtime, ba_refine_focal_length=False,
        ba_refine_principal_point=False, ba_refine_extra_params=False)
    opts.mapper.abs_pose_refine_focal_length = False
    opts.mapper.abs_pose_refine_extra_params = False
    opts.mapper.random_seed = opts.triangulation.random_seed = 5
    # Do not use the structure-less fallback to register views without 3D support.
    opts.structure_less_registration_fallback = False
    write_json(out / "mapper-options.json", json.loads(json.dumps(opts.todict(), default=str)))
    models = pc.incremental_mapping(dbpath, images, out / "modelos", options=opts)
    summary = []
    for n, rec in models.items():
        path = out / "modelos" / str(n)
        rec.write_text(path)
        rec.export_PLY(path / "points.ply")
        summary.append(dict(model=n, images=rec.num_reg_images(), points=rec.num_points3D(),
            has_3d_points=rec.num_points3D() > 0,
            mean_track_length=rec.compute_mean_track_length(),
            mean_reprojection_original_px=rec.compute_mean_reprojection_error(),
            names=[im.name for im in rec.images.values()]))
    write_json(out / "resultado.json", dict(scale="arbitrary, not metres", models=summary))
    print(summary, flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    selection = sub.add_parser("seleccionar")
    selection.add_argument("dataset")
    selection.add_argument("--output", required=True)
    selection.set_defaults(run=select_views)
    match = sub.add_parser("correspondencias")
    match.add_argument("dataset")
    match.add_argument("--method", choices=["xfeat-lighterglue", "aliked-lightglue"], required=True)
    match.add_argument("--max-side", type=int, default=1024)
    match.add_argument("--keypoints", type=int, default=2048)
    match.add_argument("--cache", default=str(Path(__file__).parent / "salida" / "cache"))
    match.add_argument("--output", required=True)
    match.set_defaults(run=correspondences)
    sfm = sub.add_parser("reconstruir")
    sfm.add_argument("--output", required=True)
    sfm.add_argument("--runtime", type=int, default=180)
    sfm.set_defaults(run=reconstruct)
    args = parser.parse_args()
    from caracteristicas import no_network
    with no_network():
        args.run(args)


if __name__ == "__main__":
    main()
