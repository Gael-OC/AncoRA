# Respaldo para pausar — 6-nov-2026

Gael autorizó subir los cambios para continuar más adelante. Esta autorización se limita al respaldo; el prompt de continuación mantiene sus restricciones para el trabajo posterior.

Destino: [codex/respaldo-localizacion-offline-20261106](https://github.com/Gael-OC/AncoRA/tree/codex/respaldo-localizacion-offline-20261106). Instantánea WIP de código, escena, recursos versionables, documentos, anotaciones y herramientas. No significa que el mapa o la integración Android estén aceptados.

Se prepara con padre local `f84256fe0bbccd1acc00fce508c5a06a1039886b`, sin integrar los dos commits remotos. Se usa un repositorio temporal para crear/publicar la instantánea: la rama local `localizacion-offline`, HEAD, índice y cambios locales se conservan. La rama remota `localizacion-offline` no se sobrescribe. No se hace pull, merge, reset, cambio de rama ni worktree.

## Qué queda solo en este computador

Git excluye las capturas de Descargas/Registros, `Tools/LocalizacionOffline/salida/` (reconstrucciones, diagnósticos, manifiestos y resultados), `.weights/`, `.deps/`, `.venv/`, el entorno y grafo Graphify, builds y cachés. Este respaldo **no reemplaza una copia de esos datos**. Conservar estas carpetas y las fuentes originales; un clon nuevo de GitHub por sí solo no reproduce el estado experimental. Se conserva localmente pero se excluye de la instantánea el cambio regenerado de `Assets/XR/Settings/OpenXR Package Settings.asset`, conforme a CLAUDE.md.

Validación del respaldo: 20 pruebas Python aprobadas; revisión de los archivos seleccionados para evitar credenciales y datos excluidos. No se volvió a compilar Unity ni APK para este respaldo. La validación de Unity descrita en documentos anteriores corresponde a aquellas jornadas, no a una ejecución nueva.

## Para continuar

En este mismo computador se puede reutilizar [el prompt completo](LocalizacionOffline-Prompt-Continuar.md). Primero comprobar el estado y leer [Estado-y-Plan](LocalizacionOffline-Estado-y-Plan.md) y [SiguienteSesion](LocalizacionOffline-SiguienteSesion.md). La instantánea remota no obliga a cambiar de rama ni descargar nada sobre los cambios locales.

El siguiente trabajo real sigue siendo el ensayo dirigido de 71 a 78 vistas alrededor de `143744/0001` y `/0068`, con identidad comprobada y baseline conservado; después aristas 2D/3D, escala exploratoria con C–D y comprobación con las tres reservas. Las 25 fotos históricas se clasifican aparte. Todavía no hay mapa métrico aceptado ni nuevo localizador Android; no hace falta otra captura general ni instalar Unity para retomar ese ensayo en PC.
