#!/usr/bin/env python3
"""Local visual-localization workbench. Only `preparar` is allowed to use the network."""
import argparse
import hashlib
import importlib.metadata
import json
from pathlib import Path
import subprocess
import time

from datos import inventory, read_json, select_pairs, write_json

HERE = Path(__file__).resolve().parent


def download_weight(url, target):
    """Bounded range requests avoid silently accepting truncated large release downloads."""
    from concurrent.futures import ThreadPoolExecutor
    import urllib.request
    import zipfile
    target = Path(target)
    if target.is_file():
        try:
            with zipfile.ZipFile(target) as archive:
                if archive.testzip() is None:
                    return
        except zipfile.BadZipFile:
            pass
    target.parent.mkdir(parents=True, exist_ok=True)
    def request(start, end):
        for attempt in range(3):
            try:
                req = urllib.request.Request(url, headers={"Range": f"bytes={start}-{end}"})
                with urllib.request.urlopen(req, timeout=45) as response:
                    content_range = response.headers.get("Content-Range", "")
                    if response.status != 206 or not content_range.startswith(f"bytes {start}-{end}/"):
                        raise ValueError("El servidor no respetó la descarga parcial.")
                    payload = response.read(end - start + 2)
                    if len(payload) != end - start + 1:
                        raise ValueError("Descarga incompleta.")
                    return payload, int(content_range.split("/")[-1])
            except Exception:
                if attempt == 2:
                    raise
    first, size = request(0, 1023)
    chunks = [(start, min(start + 1024 * 1024 - 1, size - 1)) for start in range(1024, size, 1024 * 1024)]
    temp = target.with_suffix(".download")
    with temp.open("wb") as stream, ThreadPoolExecutor(max_workers=6) as pool:
        stream.write(first)
        for n, (payload, _) in enumerate(pool.map(lambda pair: request(*pair), chunks), 1):
            stream.write(payload)
            if n % 8 == 0:
                print(f"{target.name}: {min(size, 1024 + n * 1024 * 1024) / size:.0%}", flush=True)
    with zipfile.ZipFile(temp) as archive:
        if archive.testzip() is not None:
            raise ValueError(f"Peso corrupto: {target.name}")
    temp.replace(target)


def prepare(args):
    from caracteristicas import Features, METHODS, REVISIONS
    for name, repo in (("LightGlue", "https://github.com/cvg/LightGlue.git"),
                       ("XFeat", "https://github.com/verlab/accelerated_features.git")):
        target = HERE / ".deps" / name
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            subprocess.run(["git", "clone", "--filter=blob:none", repo, str(target)], check=True)
        subprocess.run(["git", "-C", str(target), "checkout", "--detach", REVISIONS[name]], check=True)
        actual = subprocess.check_output(["git", "-C", str(target), "rev-parse", "HEAD"], text=True).strip()
        if actual != REVISIONS[name]:
            raise ValueError(f"Versión inesperada de {name}: {actual}")
    previous = read_json(HERE / ".weights" / "manifest.json") if (HERE / ".weights" / "manifest.json").is_file() else {}
    for method in args.methods:
        print(f"Preparando {method}…", flush=True)
        if method in ("sift-lightglue", "aliked-lightglue"):
            name = method.split("-")[0] + "_lightglue"
            download_weight(f"https://github.com/cvg/LightGlue/releases/download/v0.1_arxiv/{name}.pth",
                            HERE / ".weights" / "checkpoints" / f"{name}_v0-1_arxiv.pth")
        if method == "aliked-lightglue":
            download_weight("https://github.com/Shiaoming/ALIKED/raw/main/models/aliked-n16.pth",
                            HERE / ".weights" / "checkpoints" / "aliked-n16.pth")
        Features(method, 640, 1024)
    weights = list((HERE / ".weights").rglob("*.pth")) + list((HERE / ".deps" / "XFeat" / "weights").glob("*.pt"))
    write_json(HERE / ".weights" / "manifest.json", {
        "revisions": REVISIONS,
        "methods": sorted(set(args.methods) | set(previous.get("methods", []))),
        "files": {str(p.relative_to(HERE)): hashlib.sha256(p.read_bytes()).hexdigest() for p in weights},
        "packages": {n: importlib.metadata.version(n) for n in ("numpy", "opencv-python-headless", "torch", "torchvision", "kornia")}})
    print("Pesos preparados y registrados. Los demás comandos rechazan conexiones de red.")


def backend(args):
    from caracteristicas import Features, REVISIONS
    if args.method != "sift":
        manifest = read_json(HERE / ".weights" / "manifest.json")
        if manifest["revisions"] != REVISIONS:
            raise ValueError("Versiones distintas de las preparadas; vuelve a ejecutar preparar.")
        if args.method not in manifest.get("methods", []):
            raise ValueError(f"Faltan pesos de {args.method}; ejecuta preparar --methods {args.method}.")
        for name, revision in REVISIONS.items():
            if subprocess.check_output(["git", "-C", str(HERE / ".deps" / name), "rev-parse", "HEAD"], text=True).strip() != revision:
                raise ValueError(f"Cambió el código de {name}; no se reutilizará su índice.")
        for name, digest in manifest["files"].items():
            if hashlib.sha256((HERE / name).read_bytes()).hexdigest() != digest:
                raise ValueError(f"Peso ausente o modificado: {name}")
    return Features(args.method, args.max_side, args.keypoints)


def pairs(args):
    import cv2
    import numpy as np
    from caracteristicas import METHODS
    data = read_json(args.dataset)
    chosen = select_pairs(data["frames"], args.limit)
    if not chosen:
        raise ValueError("Se necesitan sesiones y puntos de vista diferentes para comparar.")
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    methods = METHODS if args.method == "todos" else [args.method]
    with output.open("w", encoding="utf-8") as stream:
        for method in methods:
            args.method = method
            model = backend(args)
            # Warm up before timing the benchmark; cached extraction time is never counted as a cache hit latency.
            model.extract(chosen[0][0]["path"])
            print(f"{method}: {len(chosen)} pares", flush=True)
            for a, b, same in chosen:
                fa, fb = (model.cached(f, args.cache) for f in (a, b))
                start = time.perf_counter()
                matches = model.match(fa, fb)
                elapsed = time.perf_counter() - start
                count, fraction = 0, 0.
                if len(matches) >= 4:
                    p, q = fa["keypoints"][matches[:, 0]], fb["keypoints"][matches[:, 1]]
                    H, mask = cv2.findHomography(p, q, cv2.USAC_MAGSAC, 3.)
                    if H is not None and mask is not None:
                        count = int(mask.sum())
                        fraction = count / len(matches)
                row = {"method": method, "reference": a["id"], "query": b["id"], "same_building": same,
                       "matches": len(matches), "homography_inliers": count, "inlier_ratio": fraction,
                       "reference_extract_seconds": float(fa["extract_seconds"]),
                       "query_extract_seconds": float(fb["extract_seconds"]), "match_seconds": elapsed,
                       "max_side": args.max_side, "keypoints": args.keypoints,
                       "kind": "pair_diagnostic_not_pose", "device": "CPU de este computador"}
                stream.write(json.dumps(row, allow_nan=False) + "\n")
                stream.flush()


def import_colmap(args):
    import numpy as np
    import pycolmap
    data = read_json(args.dataset)
    reconstruction = pycolmap.Reconstruction(args.model)
    frames = {f["id"]: f for f in data["frames"]}
    registered = []
    for image in reconstruction.images.values():
        if image.name not in frames:
            continue
        frame = dict(frames[image.name])
        transform = image.cam_from_world()
        T = np.eye(4)
        T[:3] = transform.matrix()
        T[:3, 3] *= args.meters_per_unit
        camera = reconstruction.cameras[image.camera_id]
        if (camera.width, camera.height) != (frame["width"], frame["height"]):
            raise ValueError(f"La cámara COLMAP y la imagen tienen resoluciones distintas: {image.name}")
        if camera.model_name not in ("PINHOLE", "SIMPLE_PINHOLE"):
            raise ValueError("Exporta cámaras PINHOLE sin distorsión y sus imágenes rectificadas antes de importar.")
        frame["K"] = camera.calibration_matrix().tolist()
        frame["T_camera_map"] = T.tolist()
        registered.append(frame)
    if len(registered) < 2:
        raise ValueError("No hay dos imágenes registradas cuyos nombres coincidan con los ids del inventario.")
    write_json(args.output, {"schema": 1, "kind": "registered_mapping", "frames": registered,
                            "metric_source": args.metric_source, "meters_per_colmap_unit": args.meters_per_unit,
                            "alignment_verified": False, "ground_truth": None})


def import_realityscan(args):
    from alineacion import import_realityscan as build
    box = None
    if args.recortar:
        box = [float(v) for v in args.recortar.split(",")]
        if len(box) != 6 or min(box[3:]) <= 0:
            raise ValueError("--recortar necesita 6 números: cx,cy,cz,sx,sy,sz (centro y tamaño, metros).")
    if bool(args.campus_json) != bool(args.edificio_campus):
        raise ValueError("--campus-json y --edificio-campus van juntos.")
    campus = read_json(args.campus_json) if args.campus_json else None
    model = Path(args.model)
    result = build(model, read_json(args.georef), args.building or args.edificio_campus or "campus",
                   args.session or f"dron-{model.resolve().name}", images_dir=args.images, campus=campus,
                   campus_building=args.edificio_campus, box=box, max_distance=args.distancia_max)
    write_json(args.output, result)
    print(f"{len(result['frames'])} cámaras métricas ({result['sin_imagen']} sin foto en --images); "
          f"fuente: {result['metric_source']}. No acredita alineación física.")


def align_arcore(args):
    from alineacion import align_arcore as align, read_colmap_text
    inventory_data = read_json(args.dataset)
    result = align(inventory_data, read_colmap_text(args.model), session=args.sesion, threshold_cm=args.umbral_cm,
                   min_cameras=args.min_camaras, min_path_m=args.min_recorrido, max_offset_ms=args.desfase_max_ms,
                   step_ms=args.paso_ms, max_rotation_deg=args.umbral_grados)
    write_json(args.output, result)
    if args.inventario_salida:
        used = set(result["ids_usados"])
        for frame in inventory_data["frames"]:
            if result["pose_optica_validada"] and frame["id"] in used:
                frame["optical_pose_validated"] = True
                frame["optical_pose_convention"] = result["convencion"]
                frame["optical_pose_time_offset_ms"] = result["desfase_ms"]
        write_json(args.inventario_salida, inventory_data)
    r = result["residuo_posicion_cm"]
    print(f"{'VALIDADA' if result['pose_optica_validada'] else 'NO validada'}: {result['motivo']}. "
          f"{result['camaras']} cámaras, recorrido {result['recorrido_m']:.2f} m, residuo {r['rms']:.2f} cm (máx. {r['max']:.2f}), "
          f"escala ARCore/SfM {result['escala_arcore_por_sfm']:.4f}, desfase {result['desfase_ms']:+.1f} ms, "
          f"imagen girada {result['convencion']['rotacion_imagen_grados']}° flip_y={result['convencion']['flip_y']} "
          f"({result['convencion']['error_rotacion_grados']:.2f}°).")


def build_map(args):
    import numpy as np
    from caracteristicas import feature_signature
    from geometria import triangulate
    data = read_json(args.dataset)
    if data.get("kind") != "registered_mapping" or not data.get("metric_source"):
        raise ValueError("El mapa necesita cámaras registradas y una escala medida; no basta una nube PLY.")
    model = backend(args)
    frames = data["frames"]
    features = [model.cached(f, args.cache) for f in frames]
    accum = [{} for _ in frames]
    for i, a in enumerate(frames):
        for j in range(i + 1, len(frames)):
            b = frames[j]
            if a["building"] != b["building"]:
                continue
            matches = model.match(features[i], features[j])
            xyz, valid = triangulate(features[i]["keypoints"][matches[:, 0]], features[j]["keypoints"][matches[:, 1]],
                                     np.asarray(a["K"]), np.asarray(b["K"]),
                                     np.asarray(a["T_camera_map"]), np.asarray(b["T_camera_map"]))
            for (ki, kj), point in zip(matches[valid], xyz[valid]):
                accum[i].setdefault(int(ki), []).append(point)
                accum[j].setdefault(int(kj), []).append(point)
    references = []
    for f, values in zip(frames, accum):
        if len(values) < 20:
            continue
        indices, points = [], []
        for index, candidates in values.items():
            candidates = np.asarray(candidates)
            point = np.median(candidates, axis=0)
            # Reject inconsistent triangulations rather than averaging incompatible surfaces.
            if np.max(np.linalg.norm(candidates - point, axis=1)) > 0.20:
                continue
            indices.append(index)
            points.append(point.tolist())
        if len(indices) >= 20:
            references.append({"frame": f, "indices": indices, "xyz": points})
    if len(references) < 2:
        raise ValueError("Triangulación insuficiente: faltan vistas con paralaje y correspondencias coherentes.")
    write_json(args.output, {"schema": 1, "kind": "visual_map", "method": args.method,
                            "max_side": args.max_side, "keypoints": args.keypoints,
                            "feature_signature": feature_signature(args.method, args.max_side, args.keypoints),
                            "metric_source": data["metric_source"], "alignment_verified": data.get("alignment_verified", False),
                            "references": references})
    print(f"Mapa: {len(references)} referencias; no acredita alineación física.")


def localize(args):
    import numpy as np
    from caracteristicas import feature_signature
    from scipy.spatial.transform import Rotation
    from geometria import estimate_pose
    visual_map, data = read_json(args.map), read_json(args.dataset)
    if visual_map["kind"] != "visual_map":
        raise ValueError("No es un mapa visual.")
    for key in ("method", "max_side", "keypoints"):
        setattr(args, key, visual_map[key])
    if visual_map.get("feature_signature") != feature_signature(args.method, args.max_side, args.keypoints):
        raise ValueError("El mapa usa otro extractor, código o pesos; vuelve a construirlo antes de localizar.")
    refs = visual_map["references"]
    ref_hashes = {r["frame"]["sha256"] for r in refs}
    ref_sessions = {r["frame"]["session"] for r in refs}
    if any(f["sha256"] in ref_hashes or f["session"] in ref_sessions for f in data["frames"]):
        raise ValueError("Fuga de datos: una consulta pertenece a una sesión de construcción del mapa.")
    model = backend(args)
    stored = [model.cached(r["frame"], args.cache) for r in refs]
    model.extract(data["frames"][0]["path"])
    rows = []
    for frame in data["frames"]:
        start = time.perf_counter()
        query = model.extract(frame["path"])
        candidates = []
        for ref, feat in zip(refs, stored):
            matches = model.match(query, feat)
            index = {k: p for k, p in zip(ref["indices"], ref["xyz"])}
            matches = [(q, r) for q, r in matches if int(r) in index]
            xyz = [index[int(r)] for _, r in matches]
            xy = [query["keypoints"][q] for q, _ in matches]
            result = estimate_pose(xyz, xy, frame["K"])
            if result is not None:
                candidates.append((result, ref["frame"]["id"], ref["frame"]["building"], xyz, xy))
        best = None
        for candidate in sorted(candidates, key=lambda c: c[0]["inliers"], reverse=True):
            pose = np.linalg.inv(candidate[0]["T_camera_map"])
            support = []
            for other in candidates:
                p = np.linalg.inv(other[0]["T_camera_map"])
                angle = np.rad2deg(Rotation.from_matrix(pose[:3, :3].T @ p[:3, :3]).magnitude())
                if np.linalg.norm(pose[:3, 3] - p[:3, 3]) <= 0.30 and angle <= 2:
                    support.append(other)
            if len(support) >= 2:
                best = candidate
                break
        row = {"id": frame["id"], "session": frame["session"], "capture_time": frame.get("capture_time"),
               "accepted": best is not None, "seconds": time.perf_counter() - start,
               "method": args.method, "alignment_verified": visual_map["alignment_verified"], "ground_truth": None}
        if best:
            result, reference, building, xyz, xy = best
            row.update(T_camera_map=result["T_camera_map"].tolist(), inliers=result["inliers"],
                       reprojection_median_px=result["reprojection_median_px"], reference=reference, building=building,
                       xyz=np.asarray(xyz)[result["indices"]].tolist(), xy=np.asarray(xy)[result["indices"]].tolist(), K=frame["K"])
        rows.append(row)
        print(f"{frame['id']}: {'pose' if best else 'sin pose'} ({row['seconds']:.2f} s)", flush=True)
    write_json(args.output, {"schema": 1, "kind": "localizations", "device": "CPU de este computador", "rows": rows})


def sequence(args):
    import numpy as np
    from geometria import refine_sequence
    data = read_json(args.dataset)
    observations = data["observations"]
    if not observations or len({o["tracking_epoch"] for o in observations}) != 1:
        raise ValueError("No se pueden mezclar sesiones o reinicios de seguimiento.")
    times = [o["capture_time"] for o in observations]
    if times != sorted(times) or times[-1] - times[0] > 5:
        raise ValueError("La ventana debe estar ordenada y durar como máximo 5 segundos.")
    T, error = refine_sequence(observations, np.asarray(data["initial_T_map_ar"]))
    write_json(args.output, {"schema": 1, "T_map_ar": T.tolist(), "reprojection_median_px": error,
                            "alignment_verified": False, "ground_truth": None})


def report(args):
    import platform
    import statistics
    rows = []
    for file in args.files:
        rows.extend(json.loads(line) for line in Path(file).read_text().splitlines() if line.strip())
    configurations = sorted({(r["max_side"], r["keypoints"]) for r in rows})
    settings = "; ".join(f"lado máximo {side} px, hasta {points} características" for side, points in configurations)
    lines = ["# Diagnóstico inicial de correspondencias offline", "",
             f"Equipo de ejecución: {platform.system()} {platform.machine()}; CPU, sin aceleración móvil.", "",
             f"Configuración: {settings}.", "",
             "**Estos pares no miden pose 3D, precisión en centímetros ni tiempo de inicio en Android.**",
             "La etiqueta ‘mismo edificio’ tampoco garantiza solapamiento entre las vistas.", "",
             "| Método | Pares del mismo edificio / otros | Mediana inliers mismo / otros | Extracción consulta (s) | Comparación (s) |",
             "|---|---:|---:|---:|---:|"]
    for method in dict.fromkeys(r["method"] for r in rows):
        selected = [r for r in rows if r["method"] == method]
        pos = [r["homography_inliers"] for r in selected if r["same_building"]]
        neg = [r["homography_inliers"] for r in selected if not r["same_building"]]
        median = lambda values: f"{statistics.median(values):.3f}" if values else "—"
        lines.append(f"| {method} | {len(pos)} / {len(neg)} | {median(pos)} / {median(neg)} | "
                     f"{median([r['query_extract_seconds'] for r in selected])} | {median([r['match_seconds'] for r in selected])} |")
    lines.extend(["", "## Límites", "", "- Ensayo diagnóstico sobre imágenes estáticas de sesiones distintas.",
                  "- Los tiempos de extracción guardados en caché son el cómputo original; no la lectura de la caché.",
                  "- Una homografía puede ajustar coincidencias falsas: sus inliers no acreditan una ubicación.",
                  "- Falta mapa abierto registrado, calibración óptica y controles de fachada independientes.",
                  "- No se elige ganador ni se declara cumplido el objetivo de 10–20 cm con este ensayo.", ""])
    Path(args.output).parent.mkdir(parents=True, exist_ok=True)
    Path(args.output).write_text("\n".join(lines), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    p = sub.add_parser("inventario")
    p.add_argument("root")
    p.add_argument("--output", required=True)
    p.add_argument("--skip-missing", action="store_true")
    p = sub.add_parser("preparar")
    p.add_argument("--methods", nargs="+", default=["sift-lightglue", "aliked-lightglue", "xfeat-lighterglue"],
                   choices=["sift-lightglue", "aliked-lightglue", "xfeat-lighterglue"])
    p = sub.add_parser("informe")
    p.add_argument("files", nargs="+")
    p.add_argument("--output", required=True)
    p = sub.add_parser("importar-realityscan", help="Exportación COLMAP de texto de RealityScan + georreferencia GPS -> cámaras métricas")
    p.add_argument("model", help="carpeta con cameras.txt, images.txt y points3D.txt")
    p.add_argument("--georef", required=True, help="JSON con el formato de Escenas/Campus/Campus-georef.json")
    p.add_argument("--campus-json", help="Escenas/Campus/campus.json (opcional): pasa al marco del campus")
    p.add_argument("--edificio-campus", help="clave de edificio en campus.json (p. ej. X1)")
    p.add_argument("--building", help="etiqueta de edificio (por defecto, --edificio-campus o «campus»)")
    p.add_argument("--session", help="sesión (por defecto dron-<nombre de la carpeta>)")
    p.add_argument("--images", help="carpeta con las fotos (da path, sha256 y comprueba dimensiones)")
    p.add_argument("--recortar", help="cx,cy,cz,sx,sy,sz: conserva cámaras que ven esa caja (marco de salida, metros)")
    p.add_argument("--distancia-max", type=float, default=60.0, help="distancia máxima cámara-caja con --recortar")
    p.add_argument("--output", required=True)
    p = sub.add_parser("alinear-arcore", help="Alinea un modelo SfM con las poses ARCore de un barrido y valida la pose óptica")
    p.add_argument("dataset", help="inventario con position_unity, rotation_unity_xyzw, tiempos e intrínsecos")
    p.add_argument("--model", required=True, help="carpeta COLMAP de texto de esas mismas imágenes")
    p.add_argument("--sesion", help="sesión del inventario (obligatoria si hay varias)")
    p.add_argument("--umbral-cm", type=float, default=5.0)
    p.add_argument("--umbral-grados", type=float, default=3.0)
    p.add_argument("--min-camaras", type=int, default=10)
    p.add_argument("--min-recorrido", type=float, default=1.0)
    p.add_argument("--desfase-max-ms", type=float, default=100.0)
    p.add_argument("--paso-ms", type=float, default=5.0)
    p.add_argument("--inventario-salida", help="copia del inventario con optical_pose_validated=true si se valida")
    p.add_argument("--output", required=True)
    for name in ("pares", "mapa", "localizar", "secuencia", "importar-colmap"):
        p = sub.add_parser(name)
        p.add_argument("dataset")
        p.add_argument("--output", required=True)
        if name in ("pares", "mapa", "localizar"):
            p.add_argument("--method", default="sift")
            p.add_argument("--max-side", type=int, default=1024)
            p.add_argument("--keypoints", type=int, default=2048)
            p.add_argument("--cache", default=str(HERE / "salida" / "cache"))
        if name == "pares":
            p.add_argument("--limit", type=int, default=12)
        if name == "localizar":
            p.add_argument("--map", required=True)
        if name == "importar-colmap":
            p.add_argument("--model", required=True)
            p.add_argument("--meters-per-unit", type=float, required=True)
            p.add_argument("--metric-source", required=True)
    args = parser.parse_args()
    try:
        if args.command == "preparar":
            prepare(args)
            return
        from caracteristicas import no_network
        with no_network():
            if args.command == "inventario":
                result = inventory(args.root, args.skip_missing)
                write_json(args.output, result)
                print(f"{len(result['frames'])} imágenes importadas; {len(result['missing'])} ausentes excluidas; "
                      f"{len(result['unreferenced_images'])} imágenes sin metadatos excluidas; sin verdad de terreno.")
            elif args.command == "pares":
                pairs(args)
            elif args.command == "mapa":
                build_map(args)
            elif args.command == "localizar":
                localize(args)
            elif args.command == "secuencia":
                sequence(args)
            elif args.command == "importar-colmap":
                if not 0 < args.meters_per_unit < 100000:
                    raise ValueError("Escala inválida.")
                import_colmap(args)
            elif args.command == "importar-realityscan":
                import_realityscan(args)
            elif args.command == "alinear-arcore":
                align_arcore(args)
            elif args.command == "informe":
                report(args)
    except (ValueError, FileNotFoundError, ImportError, RuntimeError) as error:
        parser.exit(2, f"Error: {error}\n")


if __name__ == "__main__":
    main()
