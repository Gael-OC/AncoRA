"""Metric registration of drone photos (RealityScan/COLMAP text) and SfM -> ARCore alignment with convention search.

Pure numpy/scipy/Pillow: no pycolmap and no torch, so it runs anywhere the tests run.
Frames: OpenCV cameras (x right, y down, z forward), T_camera_map maps a metric world point to camera coordinates.
"""
import hashlib
import math
from pathlib import Path

import numpy as np
from scipy.spatial.transform import Rotation, Slerp

PINHOLE_MODELS = ("PINHOLE", "SIMPLE_PINHOLE")


# ---------------------------------------------------------------- COLMAP text

def read_colmap_text(model_dir):
    """Parse cameras.txt / images.txt (/ points3D.txt, only counted) of a COLMAP text model."""
    model_dir = Path(model_dir)
    cameras_file, images_file = model_dir / "cameras.txt", model_dir / "images.txt"
    for file in (cameras_file, images_file):
        if not file.is_file():
            raise ValueError(f"Falta {file.name} en {model_dir}: exporta el modelo COLMAP en formato de texto.")
    cameras = {}
    for line in cameras_file.read_text(encoding="utf-8-sig").splitlines():
        if not line.strip() or line.startswith("#"):
            continue
        token = line.split()
        cid, model, width, height = int(token[0]), token[1], int(token[2]), int(token[3])
        params = [float(v) for v in token[4:]]
        if model not in PINHOLE_MODELS:
            raise ValueError(f"La cámara {cid} es {model}: tiene distorsión. Exporta cámaras PINHOLE "
                             "(imágenes sin distorsión) antes de importar.")
        if model == "PINHOLE":
            fx, fy, cx, cy = params[:4]
        else:
            fx = fy = params[0]
            cx, cy = params[1:3]
        if min(fx, fy, width, height) <= 0:
            raise ValueError(f"Cámara {cid} inválida.")
        cameras[cid] = {"model": model, "width": width, "height": height,
                        "K": [[fx, 0.0, cx], [0.0, fy, cy], [0.0, 0.0, 1.0]]}
    # images.txt has two lines per image; the 2D-points line may be empty, so it is NOT filtered by blankness.
    lines = [l for l in images_file.read_text(encoding="utf-8-sig").splitlines() if not l.startswith("#")]
    images, i = [], 0
    while i < len(lines):
        token = lines[i].split()
        if not token:
            i += 1
            continue
        if len(token) < 10:
            raise ValueError(f"Línea de imagen inválida en images.txt: {lines[i][:60]!r}")
        qw, qx, qy, qz, tx, ty, tz = (float(v) for v in token[1:8])
        cid = int(token[8])
        if cid not in cameras:
            raise ValueError(f"La imagen {token[9]} usa la cámara {cid}, ausente en cameras.txt.")
        images.append({"name": " ".join(token[9:]), "camera": cid, "qvec": [qw, qx, qy, qz], "tvec": [tx, ty, tz]})
        i += 1
        # Points line: multiple of 3 tokens (or empty). A header has exactly 10+ tokens, never a multiple of 3 at 10.
        if i < len(lines) and len(lines[i].split()) % 3 == 0:
            i += 1
    if not images:
        raise ValueError("images.txt no contiene imágenes.")
    points = model_dir / "points3D.txt"
    n_points = None
    if points.is_file():
        n_points = sum(1 for l in points.read_text(encoding="utf-8-sig").splitlines() if l.strip() and not l.startswith("#"))
    return {"cameras": cameras, "images": images, "points3d": n_points}


def cam_from_world(image):
    """4x4 (R | t) of the COLMAP image, in COLMAP units."""
    qw, qx, qy, qz = image["qvec"]
    T = np.eye(4)
    T[:3, :3] = Rotation.from_quat([qx, qy, qz, qw]).as_matrix()
    T[:3, 3] = image["tvec"]
    return T


def camera_center(T):
    return -T[:3, :3].T @ T[:3, 3]


def write_colmap_text(model_dir, cameras, images):
    """Test helper and format reference: cameras = {id: (model, w, h, params)}, images = [(name, T, camera_id)]."""
    model_dir = Path(model_dir)
    model_dir.mkdir(parents=True, exist_ok=True)
    (model_dir / "cameras.txt").write_text(
        "# Camera list\n" + "".join(f"{cid} {m} {w} {h} {' '.join(repr(float(p)) for p in params)}\n"
                                    for cid, (m, w, h, params) in cameras.items()), encoding="utf-8")
    rows = ["# Image list\n"]
    for number, (name, T, cid) in enumerate(images, 1):
        x, y, z, w = Rotation.from_matrix(T[:3, :3]).as_quat()
        values = " ".join(repr(float(v)) for v in (w, x, y, z, *T[:3, 3]))
        rows.append(f"{number} {values} {cid} {name}\n\n")
    (model_dir / "images.txt").write_text("".join(rows), encoding="utf-8")
    (model_dir / "points3D.txt").write_text("# 3D point list\n", encoding="utf-8")


# ------------------------------------------------------------------- georef

def load_georef(data):
    """Campus-georef.json (georef_dron.js): ENU = escala * R * (colmap - centroColmap) + centroEnu on camera centres."""
    try:
        s = float(data["escala"])
        R = np.asarray(data["R"], dtype=float)
        mp = np.asarray(data["centroColmap"], dtype=float)
        mq = np.asarray(data["centroEnu"], dtype=float)
    except KeyError as error:
        raise ValueError(f"La georreferencia no tiene la clave {error}.") from None
    if R.shape != (3, 3) or mp.shape != (3,) or mq.shape != (3,) or not 0 < s < 1000:
        raise ValueError("Georreferencia inválida (escala, R, centroColmap o centroEnu).")
    if not np.allclose(R @ R.T, np.eye(3), atol=1e-6) or np.linalg.det(R) < 0:
        raise ValueError("La matriz R de la georreferencia no es una rotación.")
    return {"s": s, "R": R, "mp": mp, "mq": mq, "residuo": data.get("residuoM"), "camaras": data.get("camaras")}


def georef_pose(T, g):
    """COLMAP cam-from-world -> metric cam-from-ENU. X_enu = s Rg (X - mp) + mq = s Rg X + b."""
    Rg, s = g["R"], g["s"]
    b = g["mq"] - s * Rg @ g["mp"]
    out = np.eye(4)
    out[:3, :3] = T[:3, :3] @ Rg.T
    out[:3, 3] = s * T[:3, 3] - out[:3, :3] @ b
    return out


def campus_world(campus, edificio):
    """4x4 ENU(building cloud) -> ENU(campus). campus.json works in (E, Up, N) with rot(p, yaw) of register.js:
    e' = e cos + n sin, n' = -e sin + n cos, up' = up. Re-expressed here in right-handed (E, N, Up)."""
    try:
        b = campus["edificios"][edificio]
    except KeyError:
        raise ValueError(f"campus.json no tiene el edificio {edificio!r}.") from None
    c, s = math.cos(math.radians(b["yaw"])), math.sin(math.radians(b["yaw"]))
    A = np.eye(4)
    A[:3, :3] = [[c, s, 0], [-s, c, 0], [0, 0, 1]]
    A[:3, 3] = [b["t"][0], b["t"][2], b["t"][1]]
    return A


def apply_world(T_camera_old, A):
    """New world point = A * old world point (rigid): T_camera_new = T_camera_old * A^-1."""
    return T_camera_old @ np.linalg.inv(A)


def sees_box(T, K, width, height, box, max_distance):
    """True if the box centre, or at least 3 of its 8 corners, project inside the image in front of the camera."""
    center, size = np.asarray(box[:3], float), np.asarray(box[3:], float)
    signs = np.array([[x, y, z] for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)])
    pts = np.vstack([center, center + signs * size / 2])
    cam = pts @ T[:3, :3].T + T[:3, 3]
    if np.linalg.norm(camera_center(T) - center) > max_distance:
        return False
    with np.errstate(divide="ignore", invalid="ignore"):
        uv = cam @ np.asarray(K).T
        xy = uv[:, :2] / uv[:, 2:]
    inside = (cam[:, 2] > 0.1) & (xy[:, 0] >= 0) & (xy[:, 0] < width) & (xy[:, 1] >= 0) & (xy[:, 1] < height)
    return bool(inside[0] or inside[1:].sum() >= 3)


def image_info(images_dir, name):
    """(path, sha256, (w, h)) of a photo if present, else (None, None, None)."""
    if not images_dir:
        return None, None, None
    path = Path(images_dir) / name
    if not path.is_file():
        return None, None, None
    from PIL import Image
    with Image.open(path) as image:
        size = image.size
    return str(path.resolve()), hashlib.sha256(path.read_bytes()).hexdigest(), size


def import_realityscan(model_dir, georef, building, session, images_dir=None, campus=None, campus_building=None,
                       box=None, max_distance=60.0):
    """registered_mapping (same schema as importar-colmap) from a RealityScan COLMAP-text export + georeference."""
    g = load_georef(georef)
    model = read_colmap_text(model_dir)
    A = campus_world(campus, campus_building) if campus is not None else None
    if campus is not None and campus_building is None:
        raise ValueError("Con campus.json hay que indicar el edificio.")
    frames, outside, no_image = [], 0, 0
    for image in model["images"]:
        camera = model["cameras"][image["camera"]]
        T = georef_pose(cam_from_world(image), g)
        if A is not None:
            T = apply_world(T, A)
        path, digest, size = image_info(images_dir, image["name"])
        if size is not None and size != (camera["width"], camera["height"]):
            raise ValueError(f"La foto {image['name']} mide {size} y la cámara COLMAP {camera['width']}x{camera['height']}.")
        if box is not None and not sees_box(T, camera["K"], camera["width"], camera["height"], box, max_distance):
            outside += 1
            continue
        frame = {"id": image["name"], "path": path or image["name"], "session": session, "point": "",
                 "building": building, "width": camera["width"], "height": camera["height"],
                 "K": camera["K"], "T_camera_map": T.tolist()}
        if digest:
            frame["sha256"] = digest
        else:
            no_image += 1
        frames.append(frame)
    if len(frames) < 2:
        raise ValueError("Quedan menos de dos cámaras" + (" dentro de la caja de recorte." if box is not None else "."))
    r = g["residuo"] or {}
    detail = f"mediana {r['mediana']:.2f} m, p90 {r['p90']:.2f} m" if "mediana" in r and "p90" in r else "residuo no informado"
    source = f"georreferencia GPS del dron, residuo {detail}, escala {g['s']:.4f}"
    if A is not None:
        source += f"; marco del campus vía campus.json ({campus_building})"
    return {"schema": 1, "kind": "registered_mapping", "frames": frames, "metric_source": source,
            "meters_per_colmap_unit": g["s"], "alignment_verified": False, "ground_truth": None,
            "recorte": None if box is None else {"caja_centro_tamano": list(box), "distancia_max_m": max_distance,
                                                   "descartadas": outside},
            "sin_imagen": no_image, "puntos3d": model["points3d"],
            "advertencia": "Escala y posición salen del GPS del dron (no de medidas de control): son relativas, "
                           "no acreditan alineación física de fachadas."}


# ------------------------------------------------------------- SfM -> ARCore

def umeyama(src, dst):
    """Similarity dst ~ s R src + t (proper rotation)."""
    src, dst = np.asarray(src, float), np.asarray(dst, float)
    mu_s, mu_d = src.mean(0), dst.mean(0)
    X, Y = src - mu_s, dst - mu_d
    U, D, Vt = np.linalg.svd(Y.T @ X / len(src))
    S = np.eye(3)
    if np.linalg.det(U) * np.linalg.det(Vt) < 0:
        S[2, 2] = -1
    R = U @ S @ Vt
    var = (X ** 2).sum() / len(src)
    if var <= 1e-12:
        raise ValueError("Las posiciones SfM son un solo punto.")
    s = float(np.trace(np.diag(D) @ S) / var)
    return s, R, mu_d - s * R @ mu_s


MIRROR = np.diag([-1.0, 1.0, 1.0])


def unity_to_rh(position, quat_xyzw):
    """Unity (left-handed, y up) camera pose -> (position, rotation) with the WORLD mirrored in x.

    The returned rotation is improper (det -1): its columns are still Unity's camera axes (x right, y up, z forward).
    A convention matrix C (see candidate_conventions) turns it into a proper OpenCV camera: R_world_opencv = R @ C.
    """
    R = Rotation.from_quat(quat_xyzw).as_matrix()
    return MIRROR @ np.asarray(position, float), MIRROR @ R


def candidate_conventions():
    """Unity camera axes -> OpenCV camera axes: image rotation 0/90/180/270 deg about z and an optional y flip.

    Only candidates with det(MIRROR @ C) > 0 give a proper rotation. Because the world mirror already costs one
    handedness, the y flip (Unity y up -> OpenCV y down) is mandatory; the four no-flip candidates are reported as
    discarded instead of being fitted with an invalid rotation."""
    out = []
    for degrees in (0, 90, 180, 270):
        for flip in (False, True):
            C = Rotation.from_euler("z", degrees, degrees=True).as_matrix() @ np.diag([1.0, -1.0 if flip else 1.0, 1.0])
            out.append({"rotacion_imagen_grados": degrees, "flip_y": flip, "C": C,
                        "propia": bool(np.linalg.det(MIRROR) * np.linalg.det(C) > 0)})
    return out


def t_camera_ar(position_unity, quat_unity_xyzw, C):
    """Metric AR (world x mirrored from Unity, right-handed) -> OpenCV camera, for secuencia's T_camera_ar."""
    p, R = unity_to_rh(position_unity, quat_unity_xyzw)
    Rwc = R @ C
    T = np.eye(4)
    T[:3, :3] = Rwc.T
    T[:3, 3] = -Rwc.T @ p
    return T


def _frame_time(frame):
    for key in ("pose_sensor_time", "sensor_time", "capture_time"):
        if frame.get(key) is not None:
            return float(frame[key])
    raise ValueError(f"El cuadro {frame.get('id')} no tiene tiempo de sensor ni de captura.")


def _select_trajectory(frames, session):
    rows = [f for f in frames if f.get("position_unity") and f.get("rotation_unity_xyzw")]
    if session:
        rows = [f for f in rows if f.get("session") == session]
    sessions = {f.get("session") for f in rows}
    if not rows:
        raise ValueError("El inventario no tiene cuadros con pose Unity" + (f" en la sesión {session!r}." if session else "."))
    if len(sessions) > 1:
        raise ValueError(f"Hay varias sesiones ({', '.join(sorted(map(str, sessions)))}): indica una con --sesion.")
    if len({f.get("tracking_epoch") for f in rows}) > 1:
        raise ValueError("La sesión mezcla épocas de tracking: ARCore cambió de sistema de coordenadas.")
    rows.sort(key=_frame_time)
    times = np.array([_frame_time(f) for f in rows])
    if np.any(np.diff(times) <= 0):
        raise ValueError("Los tiempos de los cuadros no crecen estrictamente.")
    return rows, times


def _match_images(model, rows):
    by_id = {f["id"]: f for f in rows}
    by_base = {}
    for f in rows:
        by_base.setdefault(Path(f["id"]).name, []).append(f)
    pairs = []
    for image in model["images"]:
        frame = by_id.get(image["name"])
        if frame is None:
            candidates = by_base.get(Path(image["name"]).name, [])
            frame = candidates[0] if len(candidates) == 1 else None
        if frame is not None:
            pairs.append((frame, image))
    return pairs


def align_arcore(inventory, model, session=None, threshold_cm=5.0, min_cameras=10, min_path_m=1.0,
                 max_offset_ms=100.0, step_ms=5.0, max_rotation_deg=3.0, min_lateral_m=0.05):
    rows, times = _select_trajectory(inventory["frames"], session)
    pairs = _match_images(model, rows)
    if len(pairs) < 3:
        raise ValueError(f"Solo {len(pairs)} imágenes del modelo SfM coinciden con el inventario; hacen falta al menos 3.")
    ar = [unity_to_rh(f["position_unity"], f["rotation_unity_xyzw"]) for f in rows]
    ar_pos = np.array([p for p, _ in ar])
    slerp = Slerp(times, Rotation.from_matrix(np.array([MIRROR @ R for _, R in ar])))  # undo the mirror: slerp needs proper rotations
    frame_t = np.array([_frame_time(f) for f, _ in pairs])
    Tcw = [cam_from_world(i) for _, i in pairs]
    sfm_centers = np.array([camera_center(T) for T in Tcw])
    sfm_Rwc = np.array([T[:3, :3].T for T in Tcw])

    def interpolate(delta):
        tq = frame_t + delta
        mask = (tq >= times[0]) & (tq <= times[-1])
        pos = np.stack([np.interp(tq[mask], times, ar_pos[:, k]) for k in range(3)], axis=1)
        return mask, pos, tq[mask]

    def fit(delta):
        mask, pos, _ = interpolate(delta)
        if mask.sum() < 3:
            return None
        s, R, t = umeyama(sfm_centers[mask], pos)
        residual = np.linalg.norm(pos - (s * sfm_centers[mask] @ R.T + t), axis=1)
        return float(np.sqrt(np.mean(residual ** 2))), (s, R, t, mask, residual)

    grid = np.arange(-max_offset_ms, max_offset_ms + 1e-9, step_ms) / 1000.0
    costs = [(fit(d), d) for d in grid]
    costs = [(c[0], d) for c, d in costs if c is not None]
    if not costs:
        raise ValueError("Ningún desfase temporal deja suficientes cuadros dentro del recorrido de ARCore.")
    cost0, best = min(costs)
    # Parabolic refinement around the best grid offset (cost is smooth in the offset).
    neighbours = [fit(best + k * step_ms / 1000.0) for k in (-1, 0, 1)]
    if all(n is not None for n in neighbours):
        a, b, c = (n[0] for n in neighbours)
        denominator = a - 2 * b + c
        if denominator > 1e-18:
            shift = 0.5 * (a - c) / denominator
            if abs(shift) <= 1:
                refined = best + shift * step_ms / 1000.0
                if fit(refined) is not None and fit(refined)[0] <= b:
                    best = refined
    rms, (s, R, t, mask, residual) = fit(best)
    _, pos, tq = interpolate(best)
    n = int(mask.sum())

    # Geometry of the trajectory actually used.
    path_diameter = float(np.max(np.linalg.norm(pos[:, None] - pos[None], axis=2)))
    centred = pos - pos.mean(0)
    sv = np.linalg.svd(centred / math.sqrt(len(pos)), compute_uv=False)
    lateral = float(sv[1])

    # Orientation convention: the one for which R_ar C best matches the similarity-rotated SfM orientation.
    Rar = MIRROR @ slerp(tq).as_matrix()
    predicted = np.einsum("ij,njk->nik", R, sfm_Rwc[mask])
    table = []
    for cand in candidate_conventions():
        if not cand["propia"]:
            continue
        errors = np.degrees(Rotation.from_matrix(np.einsum("nij,jk->nik", Rar, cand["C"]).transpose(0, 2, 1) @ predicted).magnitude())
        table.append({"rotacion_imagen_grados": cand["rotacion_imagen_grados"], "flip_y": cand["flip_y"],
                      "error_mediana_grados": float(np.median(errors)), "error_p90_grados": float(np.percentile(errors, 90)),
                      "C": cand["C"].tolist()})
    table.sort(key=lambda r: r["error_mediana_grados"])
    winner, second = table[0], table[1]

    reasons = []
    if n < min_cameras:
        reasons.append(f"solo {n} cámaras con pose (mínimo {min_cameras})")
    if path_diameter < min_path_m:
        reasons.append(f"recorrido de {path_diameter:.2f} m (mínimo {min_path_m:g} m): un giro casi en el sitio no fija la escala ni la rotación")
    elif lateral < min_lateral_m:
        reasons.append(f"recorrido casi recto (extensión lateral {lateral * 100:.1f} cm): la rotación alrededor de la línea queda indeterminada")
    if rms * 100 >= threshold_cm:
        reasons.append(f"residuo de posición {rms * 100:.1f} cm (umbral {threshold_cm:g} cm)")
    if winner["error_mediana_grados"] >= max_rotation_deg:
        reasons.append(f"ninguna convención de ejes orienta las cámaras (mejor: {winner['error_mediana_grados']:.1f}° de mediana, umbral {max_rotation_deg:g}°)")
    elif second["error_mediana_grados"] < 2 * winner["error_mediana_grados"] and second["error_mediana_grados"] < max_rotation_deg:
        reasons.append("convención ambigua: otra candidata da un error comparable")
    validated = not reasons

    T_ar_sfm = np.eye(4)
    T_ar_sfm[:3, :3] = R
    T_ar_sfm[:3, 3] = t
    return {
        "schema": 1, "kind": "arcore_alignment", "pose_optica_validada": validated,
        "motivo": "; ".join(reasons) if reasons else "residuo de posición y de orientación dentro de los umbrales",
        "sesion": rows[0].get("session"), "camaras": n, "camaras_modelo": len(pairs), "recorrido_m": path_diameter,
        "extension_lateral_m": lateral,
        "residuo_posicion_cm": {"rms": rms * 100, "mediana": float(np.median(residual)) * 100, "max": float(residual.max()) * 100},
        "escala_arcore_por_sfm": s, "escala_sfm_por_arcore": 1 / s,
        "desfase_ms": best * 1000.0,
        "desfase_nota": "la pose ARCore que corresponde a la imagen i es la leída en t_i + desfase",
        "convencion": {"rotacion_imagen_grados": winner["rotacion_imagen_grados"], "flip_y": winner["flip_y"],
                       "C": winner["C"], "error_rotacion_grados": winner["error_mediana_grados"],
                       "nota": "R_mundo_optica = diag(-1,1,1) @ R_unity @ C: espejo de x solo en el mundo; el flip de y es obligatorio por lateralidad"},
        "candidatas": table,
        "T_ar_sfm": T_ar_sfm.tolist(),
        "umbrales": {"residuo_cm": threshold_cm, "camaras_min": min_cameras, "recorrido_min_m": min_path_m,
                     "rotacion_max_grados": max_rotation_deg, "lateral_min_m": min_lateral_m,
                     "desfase_max_ms": max_offset_ms, "paso_ms": step_ms},
        "ids_usados": [f["id"] for (f, _), keep in zip(pairs, mask) if keep],
    }
