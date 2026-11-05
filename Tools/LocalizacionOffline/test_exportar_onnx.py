"""Pruebas de la exportación ONNX de XFeat. Se saltan si faltan torch, onnx, onnxruntime o el código/pesos fijados."""
import importlib.util
import tempfile
import unittest
from pathlib import Path

import exportar_onnx


def _faltante():
    for nombre in ("torch", "onnx", "onnxruntime", "onnxscript"):
        if importlib.util.find_spec(nombre) is None:
            return f"falta el paquete {nombre}"
    try:
        exportar_onnx.comprobar_origen()
    except exportar_onnx.FaltaDependencia as error:
        return str(error).splitlines()[0]
    return None


class ExportarOnnxTest(unittest.TestCase):
    def test_sin_origen_el_mensaje_explica_como_obtenerlo(self):
        original = exportar_onnx.XFEAT_DIR
        try:
            exportar_onnx.XFEAT_DIR = Path(tempfile.gettempdir()) / "no-existe-xfeat"
            with self.assertRaises(exportar_onnx.FaltaDependencia) as contexto:
                exportar_onnx.comprobar_origen()
            self.assertIn("preparar", str(contexto.exception))
        finally:
            exportar_onnx.XFEAT_DIR = original

    def test_tamano_no_multiplo_de_32_se_rechaza(self):
        motivo = _faltante()
        if motivo:
            self.skipTest(motivo)
        with self.assertRaises(ValueError):
            exportar_onnx.construir(650, 480)

    def test_onnx_coincide_con_pytorch(self):
        motivo = _faltante()
        if motivo:
            self.skipTest(motivo)
        with tempfile.TemporaryDirectory() as carpeta:
            r = exportar_onnx.exportar(128, 96, Path(carpeta) / "x.onnx", 17)
        self.assertEqual({"descriptores", "puntos", "fiabilidad"}, set(r["diferencias"]))
        for nombre, d in r["diferencias"].items():
            self.assertLess(d["max_abs"], 1e-4, nombre)
            self.assertGreater(d["rango"], 0.0, nombre)


if __name__ == "__main__":
    unittest.main()
