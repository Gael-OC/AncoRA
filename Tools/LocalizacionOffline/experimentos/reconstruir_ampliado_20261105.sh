#!/bin/sh
# Run only after both complete matchings; each command is a separate native process.
set -eu
PY=Tools/LocalizacionOffline/.venv/bin/python
T=Tools/LocalizacionOffline
R=${1:-$T/salida/20261105-revision-camaras}
A=$T/experimentos/teologia-71-regiones-20261105.json
for method in xfeat aliked; do
    "$PY" "$T/sfm_diagnostico.py" filtrar-fachada --source "$R/ampliado-$method" --annotations "$A" --output "$R/ampliado-$method-fachada"
    "$PY" "$T/sfm_aprendido.py" reconstruir --output "$R/ampliado-$method" --runtime 300
    "$PY" "$T/sfm_aprendido.py" reconstruir --output "$R/ampliado-$method-fachada" --runtime 300
    "$PY" "$T/sfm_diagnostico.py" analizar --source "$R/ampliado-$method-fachada" --annotations "$A"
    "$PY" "$T/experimentos/evidencia_recorridos.py" --source "$R/ampliado-$method-fachada"
done
