"""Exporta el extractor XFeat (backbone + cabezas) a ONNX y lo compara con PyTorch.

Usa el código y los pesos fijados por `offline.py preparar` (commit de REVISIONS["XFeat"] en `.deps/XFeat`, pesos
`xfeat.pt` dentro de ese repositorio). No descarga nada: si faltan, dice cómo obtenerlos y termina.

Salidas del grafo (entrada: gris 1x1xHxW en [0,1], H y W múltiplos de 32, tamaño FIJO):
  descriptores  1x64xH/8xW/8  mapa denso ya normalizado L2 por celda
  puntos        1x1xHxW       mapa de probabilidad de punto clave (softmax de 65 canales, reordenado a resolución completa)
  fiabilidad    1x1xH/8xW/8   mapa de fiabilidad
NMS 5x5, umbral, top-k por puntaje, muestreo del mapa de descriptores y normalización final quedan FUERA del grafo
(NonZero/argsort con salida de tamaño variable no se exportan bien); son decenas de líneas en C# o C++.

Uso (desde la raíz):
  Tools/LocalizacionOffline/.venv-onnx/Scripts/python.exe Tools/LocalizacionOffline/exportar_onnx.py --width 640 --height 480
"""
import argparse
import hashlib
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
XFEAT_REVISION = "e92685f57f8318b18725c5c8c0bd28c7fe188d9a"
XFEAT_DIR = HERE / ".deps" / "XFeat"
WEIGHTS = XFEAT_DIR / "weights" / "xfeat.pt"


class FaltaDependencia(RuntimeError):
    pass


def comprobar_origen():
    """Verifica código y pesos del banco; nunca descarga."""
    import subprocess
    if not (XFEAT_DIR / "modules" / "xfeat.py").is_file() or not WEIGHTS.is_file():
        raise FaltaDependencia(
            "Falta XFeat. Obtenlo una vez con el banco (usa red solo ahí):\n"
            "  python Tools/LocalizacionOffline/offline.py preparar --methods xfeat-lighterglue\n"
            f"o clona https://github.com/verlab/accelerated_features.git en {XFEAT_DIR} y haz "
            f"`git checkout --detach {XFEAT_REVISION}` (los pesos vienen en weights/xfeat.pt).")
    try:
        actual = subprocess.check_output(["git", "-C", str(XFEAT_DIR), "rev-parse", "HEAD"], text=True).strip()
    except Exception as error:  # git ausente o no es un repositorio
        raise FaltaDependencia(f"No se pudo verificar el commit de XFeat: {error}")
    if actual != XFEAT_REVISION:
        raise FaltaDependencia(f"XFeat está en {actual}, se esperaba {XFEAT_REVISION}.")


def construir(width, height):
    """Devuelve (modelo_original, envoltorio_exportable)."""
    import torch
    import torch.nn as nn
    import torch.nn.functional as F
    if width % 32 or height % 32:
        raise ValueError("width y height deben ser múltiplos de 32 (XFeat los recorta a ese múltiplo).")
    sys.path.insert(0, str(XFEAT_DIR))
    from modules.model import XFeatModel
    net = XFeatModel().eval()
    net.load_state_dict(torch.load(WEIGHTS, map_location="cpu"))

    class Extractor(nn.Module):
        def __init__(self, net):
            super().__init__()
            self.net = net

        def forward(self, x):
            # Misma ventana 8x8 que XFeatModel._unfold2d (canal = fila*8 + columna), con formas estáticas.
            net = self.net
            x = net.norm(x)
            x1 = net.block1(x)
            x2 = net.block2(x1 + net.skip1(x))
            x3 = net.block3(x2)
            x4 = net.block4(x3)
            x5 = net.block5(x4)
            size = (height // 8, width // 8)
            x4 = F.interpolate(x4, size, mode="bilinear")
            x5 = F.interpolate(x5, size, mode="bilinear")
            feats = net.block_fusion(x3 + x4 + x5)
            reliab = net.heatmap_head(feats)
            h8, w8 = size
            patches = x.reshape(1, 1, h8, 8, w8, 8).permute(0, 1, 3, 5, 2, 4).reshape(1, 64, h8, w8)
            logits = net.keypoint_head(patches)
            prob = F.softmax(logits, 1)[:, :64]
            prob = prob.permute(0, 2, 3, 1).reshape(1, h8, w8, 8, 8).permute(0, 1, 3, 2, 4).reshape(1, 1, height, width)
            return F.normalize(feats, dim=1), prob, reliab

    return net, Extractor(net).eval()


def referencia_pytorch(net, imagen):
    """Salidas del código ORIGINAL de XFeat (con su unfold), para validar el envoltorio."""
    import torch
    import torch.nn.functional as F
    with torch.inference_mode():
        feats, kp, rel = net(imagen)
        feats = F.normalize(feats, dim=1)
        scores = F.softmax(kp, 1)[:, :64]
        b, _, h, w = scores.shape
        prob = scores.permute(0, 2, 3, 1).reshape(b, h, w, 8, 8).permute(0, 1, 3, 2, 4).reshape(b, 1, h * 8, w * 8)
    return feats.numpy(), prob.numpy(), rel.numpy()


def imagen_de_prueba(width, height):
    import torch
    torch.manual_seed(0)
    ruido = torch.rand(1, 1, height, width)
    yy, xx = torch.meshgrid(torch.linspace(0, 1, height), torch.linspace(0, 1, width), indexing="ij")
    base = (0.5 + 0.3 * torch.sin(40 * xx) * torch.cos(30 * yy)).clamp(0, 1)
    return (0.9 * base + 0.1 * ruido[0, 0])[None, None]


def exportar(width, height, salida, opset):
    import numpy as np
    import onnx
    import onnxruntime as ort
    import torch
    net, modelo = construir(width, height)
    imagen = imagen_de_prueba(width, height)
    salida = Path(salida)
    salida.parent.mkdir(parents=True, exist_ok=True)
    kwargs = dict(input_names=["gris"], output_names=["descriptores", "puntos", "fiabilidad"],
                  opset_version=opset, do_constant_folding=True)
    try:
        torch.onnx.export(modelo, (imagen,), str(salida), dynamo=False, **kwargs)
    except TypeError:  # torch sin parámetro dynamo
        torch.onnx.export(modelo, (imagen,), str(salida), **kwargs)
    grafo = onnx.load(str(salida))
    onnx.checker.check_model(grafo)
    ops = sorted({n.op_type for n in grafo.graph.node})

    esperado = referencia_pytorch(net, imagen)
    sesion = ort.InferenceSession(str(salida), providers=["CPUExecutionProvider"])
    obtenido = sesion.run(None, {"gris": imagen.numpy()})
    resultado = {"onnx": str(salida), "bytes": salida.stat().st_size, "opset": opset, "operadores": ops,
                 "sha256_pesos": hashlib.sha256(WEIGHTS.read_bytes()).hexdigest(), "diferencias": {}}
    for nombre, a, b in zip(("descriptores", "puntos", "fiabilidad"), esperado, obtenido):
        resultado["diferencias"][nombre] = {"max_abs": float(np.abs(a - b).max()), "rango": float(np.abs(a).max())}
    return resultado


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--width", type=int, default=640)
    parser.add_argument("--height", type=int, default=480)
    parser.add_argument("--opset", type=int, default=17)
    parser.add_argument("--output", default=None, help="Por defecto salida/xfeat-<W>x<H>.onnx")
    parser.add_argument("--tolerance", type=float, default=1e-4)
    args = parser.parse_args()
    try:
        comprobar_origen()
        import onnx, onnxruntime, torch  # noqa: F401
    except FaltaDependencia as error:
        sys.exit(str(error))
    except ImportError as error:
        sys.exit(f"Falta un paquete ({error}). Entorno: uv venv --python 3.12 Tools/LocalizacionOffline/.venv-onnx; "
                 "uv pip install --python <ese python> torch onnx onnxscript onnxruntime numpy "
                 "(torch CPU: --index-url https://download.pytorch.org/whl/cpu).")
    salida = args.output or str(HERE / "salida" / f"xfeat-{args.width}x{args.height}.onnx")
    r = exportar(args.width, args.height, salida, args.opset)
    print(f"{r['onnx']}  {r['bytes'] / 1e6:.2f} MB  opset {r['opset']}")
    print("Operadores:", ", ".join(r["operadores"]))
    peor = 0.0
    for nombre, d in r["diferencias"].items():
        print(f"  {nombre}: diferencia máxima {d['max_abs']:.3e} (valor máx. {d['rango']:.3g})")
        peor = max(peor, d["max_abs"])
    if peor > args.tolerance:
        sys.exit(f"Diferencia {peor:.3e} supera la tolerancia {args.tolerance:.1e}.")
    print("Coincide con PyTorch dentro de la tolerancia.")


if __name__ == "__main__":
    main()
