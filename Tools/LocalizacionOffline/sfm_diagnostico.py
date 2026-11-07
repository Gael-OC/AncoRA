"""Inspect arbitrary-scale SfM against conservative, manually drawn image regions."""
import argparse
from collections import Counter
import hashlib
from itertools import combinations
from pathlib import Path
import shutil

import cv2
import numpy as np
from PIL import Image, ImageDraw

from datos import read_json, write_json


def inside(xy, polygon):
    if not polygon:
        return np.zeros(len(xy), dtype=bool)
    contour = np.asarray(polygon, dtype=np.float32)
    return np.array([cv2.pointPolygonTest(contour, (float(x), float(y)), False) >= 0
                     for x, y in xy], dtype=bool)


def labels(xy, annotation):
    result = np.full(len(xy), "unknown", dtype="U10")
    result[inside(xy, annotation["vegetation"])] = "vegetation"
    result[inside(xy, annotation["facade"])] = "facade"
    for polygon in annotation["exclude"]:
        result[inside(xy, polygon)] = "unknown"
    return result


def stats(values):
    return dict(zip(["min", "p10", "median", "p90", "max"],
                    np.quantile(values, [0, .1, .5, .9, 1]).tolist())) if len(values) else None


def max_angle(point, centres):
    rays = np.asarray(centres) - point
    rays /= np.maximum(np.linalg.norm(rays, axis=1, keepdims=True), 1e-12)
    return float(np.degrees(np.arccos(np.clip((rays @ rays.T).min(), -1, 1))))


def filter_facade(args):
    source, out = Path(args.source), Path(args.output)
    annotations = read_json(args.annotations)
    data = read_json(source / "seleccion.json")
    out.mkdir(parents=True, exist_ok=False)
    keep = []
    for i, f in enumerate(data["frames"]):
        file = source / f"keypoints-{i}.npz"
        with np.load(file, allow_pickle=False) as saved:
            xy = saved["keypoints"] / [f["width"], f["height"]]
        keep.append(labels(xy, annotations["frames"][f["id"]]) == "facade")
        shutil.copy2(file, out / file.name)
    counts = []
    for i, j in combinations(range(len(data["frames"])), 2):
        with np.load(source / f"matches-{i}-{j}.npz", allow_pickle=False) as saved:
            matches = saved["matches"]
        filtered = matches[keep[i][matches[:, 0]] & keep[j][matches[:, 1]]]
        np.savez_compressed(out / f"matches-{i}-{j}.npz", matches=filtered)
        counts.append(dict(i=i, j=j, raw=len(matches), facade=len(filtered)))
    conditions = read_json(source / "condiciones.json")
    write_json(out / "seleccion.json", data)
    write_json(out / "condiciones.json", {**conditions, "filter": "both endpoints in manual facade regions",
        "annotations_sha256": hashlib.sha256(Path(args.annotations).read_bytes()).hexdigest(),
        "source_experiment": str(source.resolve()), "filtered_pairs": counts})


def compare(args):
    """Internal sensitivity only: remove Sim(3) gauge, never infer metric accuracy."""
    import pycolmap as pc
    from scipy.spatial.distance import pdist
    from scipy.spatial.transform import Rotation
    a, b = [pc.Reconstruction(p) for p in (args.source, args.other)]
    ia, ib = [{im.name: im for im in rec.images.values()} for rec in (a, b)]
    names = sorted(ia.keys() & ib.keys())
    if len(names) < 3 or args.anchor not in names:
        raise ValueError("Se requieren ≥3 cámaras comunes y un ancla común explícita.")
    frames = {f["id"]: f for f in read_json(args.dataset)["frames"]}
    ca, cb = [np.array([ims[n].projection_center() for n in names]) for ims in (ia, ib)]
    similarity = pc.estimate_sim3d(cb, ca)
    diameter = float(pdist(ca).max())
    if similarity is None or diameter <= 1e-12:
        raise ValueError("La similitud de cámaras está degenerada.")
    errors = np.linalg.norm(ca - similarity * cb, axis=1) / diameter * 100
    # Relative rotations to one shared optical camera remove the orientation gauge
    # without allowing a bad cluster to rotate the entire least-squares fit.
    rotation = (ia[args.anchor].cam_from_world().rotation.matrix().T @
                ib[args.anchor].cam_from_world().rotation.matrix())
    rows = []
    for n, error in zip(names, errors):
        delta = (ia[n].cam_from_world().rotation.matrix() @
                 (ib[n].cam_from_world().rotation.matrix() @ rotation.T).T)
        rows.append(dict(id=n, session=frames[n]["session"], centre_error_percent_diameter=float(error),
                         relative_rotation_delta_degrees=float(np.degrees(Rotation.from_matrix(delta).magnitude()))))
    sessions = {session: stats([r["relative_rotation_delta_degrees"] for r in rows if r["session"] == session])
                for session in sorted({r["session"] for r in rows})}
    write_json(args.output, dict(source=str(args.source), other=str(args.other), anchor=args.anchor,
        common_cameras=len(names), centre_error_percent_diameter=stats(errors),
        relative_rotation_degrees=sessions, cameras=rows,
        warning="Internal stability, not physical accuracy. Similarity fits all common camera centres; no metres."))


def analyze(args):
    import pycolmap as pc
    out = Path(args.source)
    annotations = read_json(args.annotations)["frames"]
    frames = {f["id"]: f for f in read_json(out / "seleccion.json")["frames"]}
    reports = []
    for model in sorted((out / "modelos").iterdir()):
        if not model.is_dir() or not (model / "images.bin").exists():
            continue
        rec = pc.Reconstruction(model)
        imlabels, imrows, centres = {}, [], {}
        overlay = model / "observaciones"
        overlay.mkdir(exist_ok=True)
        for imid, im in rec.images.items():
            f = frames[im.name]
            xy = np.array([p.xy - .5 for p in im.points2D]).reshape(-1, 2)
            semantic = labels(xy / [f["width"], f["height"]], annotations[im.name])
            imlabels[imid] = semantic
            centres[imid] = im.projection_center()
            valid = np.array([p.has_point3D() for p in im.points2D])
            counts = Counter(semantic[valid])
            # Spatial occupancy, not a convex hull spanning empty facade regions.
            grid = np.array([[(x+.5)/12, (y+.5)/8] for y in range(8) for x in range(12)])
            eligible = labels(grid, annotations[im.name]) == "facade"
            cells = np.clip((xy[valid & (semantic == "facade")] / [f["width"], f["height"]]
                             * [12, 8]).astype(int), [0, 0], [11, 7])
            occupied = {int(y*12+x) for x, y in cells}
            occupied_eligible = sum(k in occupied for k in np.flatnonzero(eligible))
            imrows.append(dict(id=im.name, session=f["session"], observations=int(valid.sum()),
                labels=dict(counts), facade_grid_occupied=occupied_eligible,
                facade_grid_eligible=int(eligible.sum())))
            canvas = Image.open(f["path"]).convert("RGB").resize((960, 540))
            draw = ImageDraw.Draw(canvas)
            ann = annotations[im.name]
            for key, colour in [("facade", "cyan"), ("vegetation", "magenta")]:
                if ann[key]:
                    draw.line([(x*960, y*540) for x, y in ann[key] + ann[key][:1]], fill=colour, width=2)
            for polygon in ann["exclude"]:
                draw.line([(x*960, y*540) for x, y in polygon + polygon[:1]], fill="orange", width=2)
            for p, label in zip(xy[valid], semantic[valid]):
                x, y = p / [f["width"], f["height"]] * [960, 540]
                colour = dict(facade="cyan", vegetation="magenta", unknown="yellow")[label]
                draw.ellipse((x-2, y-2, x+2, y+2), fill=colour)
            index = list(frames).index(im.name)
            draw.rectangle((0, 0, 960, 24), fill="black")
            draw.text((5, 5), f"{index} {f['session']} {Path(im.name).name} {dict(counts)}", fill="white")
            canvas.save(overlay / f"{index:02d}.png")
        tracks = []
        for point_id, p in rec.points3D.items():
            els = p.track.elements
            counts = Counter(imlabels[e.image_id][e.point2D_idx] for e in els)
            facade_sessions = {frames[rec.images[e.image_id].name]["session"] for e in els
                               if imlabels[e.image_id][e.point2D_idx] == "facade"}
            all_sessions = {frames[rec.images[e.image_id].name]["session"] for e in els}
            category = "unknown"
            for label in ["facade", "vegetation"]:
                if counts[label] >= 2 and counts[label] > len(els)/2:
                    category = label
            tracks.append(dict(id=point_id, length=len(els), category=category,
                observations=dict(counts), sessions=len(all_sessions),
                facade_sessions=len(facade_sessions), reprojection_original_px=p.error,
                max_angle_degrees=max_angle(p.xyz, [centres[e.image_id] for e in els])))
        grouped = {}
        for category in ["all", "facade", "vegetation", "facade_cross_session"]:
            ts = [t for t in tracks if category == "all" or t["category"] == category or
                  (category == "facade_cross_session" and t["category"] == "facade" and t["facade_sessions"] >= 2)]
            grouped[category] = dict(count=len(ts), track_length=stats([t["length"] for t in ts]),
                reprojection_original_px=stats([t["reprojection_original_px"] for t in ts]),
                max_angle_degrees=stats([t["max_angle_degrees"] for t in ts]))
        # Save the point identities to make selective facade inspection reproducible.
        write_json(model / "tracks-diagnostico.json", tracks)
        report = dict(model=model.name, images=rec.num_reg_images(),
            has_3d_points=rec.num_points3D() > 0,
            sessions=dict(Counter(frames[im.name]["session"] for im in rec.images.values())),
            points=rec.num_points3D(), mean_reprojection_original_px=rec.compute_mean_reprojection_error(),
            groups=grouped, images_detail=imrows)
        reports.append(report)
        pictures = sorted(overlay.glob("*.png"))
        sheet = Image.new("RGB", (1440, 270*((len(pictures)+2)//3)), (24, 28, 33))
        for n, p in enumerate(pictures):
            sheet.paste(Image.open(p).resize((480, 270)), (n%3*480, n//3*270))
        sheet.save(model / "observaciones.png")
    write_json(out / "diagnostico.json", dict(scale="arbitrary, not metres",
        annotation_limit="Conservative manual regions, incomplete coverage; unknown is not vegetation.",
        geometry_limit="Angles and depths from the estimated model, not independent measurements.", models=reports))
    print([(r["model"], r["images"], r["points"], {k: v["count"] for k, v in r["groups"].items()})
           for r in reports], flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["filtrar-fachada", "analizar", "comparar"])
    parser.add_argument("--source", required=True)
    parser.add_argument("--annotations")
    parser.add_argument("--output")
    parser.add_argument("--other")
    parser.add_argument("--anchor")
    parser.add_argument("--dataset")
    args = parser.parse_args()
    from caracteristicas import no_network
    with no_network():
        if args.command == "comparar":
            if not all([args.other, args.anchor, args.dataset, args.output]):
                parser.error("comparar requiere --other, --anchor, --dataset y --output")
            compare(args)
        elif not args.annotations:
            parser.error("Se requiere --annotations")
        elif args.command == "filtrar-fachada":
            if not args.output:
                parser.error("filtrar-fachada requiere --output")
            filter_facade(args)
        else:
            analyze(args)


if __name__ == "__main__":
    main()
