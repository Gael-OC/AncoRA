"""Import recordings without treating Immersal's output as ground truth."""
import hashlib
import json
from itertools import zip_longest
from pathlib import Path

SCHEMA = 1


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8")


def inventory(root, skip_missing=False):
    root = Path(root).resolve()
    frames, seen, missing = [], set(), []
    for log in sorted(root.rglob("intentos.jsonl")):
        for number, line in enumerate(log.read_text(encoding="utf-8-sig").splitlines(), 1):
            if not line.strip():
                continue
            row = json.loads(line)
            file = (log.parent / row["archivo"]).resolve()
            if not file.is_relative_to(root):
                raise ValueError(f"Imagen ausente o fuera de la carpeta: {log}:{number}")
            if not file.is_file():
                if not skip_missing:
                    raise ValueError(f"Imagen ausente: {log}:{number}; usa --skip-missing para excluirla explícitamente.")
                missing.append(str(file.relative_to(root)))
                continue
            if file in seen:
                continue
            seen.add(file)
            k = row["intrinsecos"]
            if min(k["fx"], k["fy"], row["ancho"], row["alto"]) <= 0:
                raise ValueError(f"Cámara inválida: {log}:{number}")
            frames.append({
                "id": str(file.relative_to(root)), "path": str(file),
                "session": row.get("sesion", log.parent.name), "point": row.get("punto", ""),
                "building": row.get("edificio_punto", ""), "light": row.get("luz", ""),
                "phone": row.get("telefono", ""), "width": row["ancho"], "height": row["alto"],
                "K": [[k["fx"], 0, k["cx"]], [0, k["fy"], k["cy"]], [0, 0, 1]],
                "capture_time": row.get("captura_t"), "sensor_time": row.get("timestamp_sensor"),
                "logged_time": row.get("t"), "tracking": row.get("tracking"),
                "position_unity": row.get("pos_camara"), "rotation_unity_xyzw": row.get("rot_camara"),
                "screen_rotation_xyzw": row.get("rot_pantalla"),
                "pose_sensor_time": row.get("timestamp_pose_sensor"),
                "tracking_epoch": row.get("epoca_tracking"),
                "sensor_display_matrix_column_major": row.get("matriz_display"),
                "screen_orientation": row.get("pantalla"),
                "optical_pose_validated": row.get("pose_optica_validada", False),
                "sha256": hashlib.sha256(file.read_bytes()).hexdigest(),
            })
    if not frames:
        raise ValueError(f"No hay grabaciones en {root}")
    # A transferred folder may contain PNGs without its JSONL (or extra images after a failed write).
    # Report them explicitly; filenames cannot supply camera calibration or capture timestamps.
    unreferenced = sorted(str(p.relative_to(root)) for p in root.rglob("*")
                          if p.is_file() and p.suffix.lower() in (".png", ".pgm", ".ppm")
                          and p.resolve() not in seen)
    return {"schema": SCHEMA, "kind": "recordings", "root": str(root), "frames": frames,
            "missing": missing, "unreferenced_images": unreferenced, "ground_truth": None,
            "warning": "Las etiquetas identifican el edificio; no acreditan pose ni alineación."}


def select_pairs(frames, limit=24):
    """Separate sessions AND viewpoints; adjacent video frames are not independent tests."""
    groups = {}
    for f in frames:
        groups.setdefault((f["session"], f["point"]), []).append(f)
    representatives = [v[len(v) // 2] for v in groups.values()]
    positive, negative = {}, {}
    for i, a in enumerate(representatives):
        for b in representatives[i + 1:]:
            if a["session"] == b["session"] or a["sha256"] == b["sha256"]:
                continue
            same = a["building"] == b["building"]
            if same and a["point"] == b["point"]:
                continue
            key = a["building"] if same else tuple(sorted((a["building"], b["building"])))
            (positive if same else negative).setdefault(key, []).append((a, b, same))
    def balanced(groups):
        return [pair for row in zip_longest(*(groups[k] for k in sorted(groups))) for pair in row if pair is not None][:limit]
    return balanced(positive) + balanced(negative)
