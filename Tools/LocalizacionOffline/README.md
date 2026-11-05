# Banco de localización offline

Implementación inicial para comparar SIFT, SIFT+LightGlue, ALIKED+LightGlue y XFeat+LighterGlue con datos propios.
Incluye importación de registros, comparación 2D, construcción de mapas desde cámaras registradas, localización
PnP/RANSAC y refinamiento multicuadro con poses ópticas normalizadas. La aplicación Android conserva su localizador
actual; **este banco todavía no lo sustituye por un motor Android**.

## Instalar y preparar una sola vez

Probado con Python 3.12 en macOS ARM64. Desde la raíz del repositorio:

```sh
uv venv --python python3.12 Tools/LocalizacionOffline/.venv
uv pip install --python Tools/LocalizacionOffline/.venv/bin/python -r Tools/LocalizacionOffline/requirements.txt
Tools/LocalizacionOffline/.venv/bin/python Tools/LocalizacionOffline/offline.py preparar
```

Solo `preparar` usa red: obtiene los repositorios en commits fijos y los pesos oficiales. Las descargas grandes se
verifican por longitud y CRC; `.weights/manifest.json` conserva SHA-256 y versiones. `.deps/`, `.weights/`, el entorno,
imágenes e índices no se suben a Git. Archivar esos directorios permite conservar la herramienta si desaparece un origen.
Los otros comandos bloquean conexiones de socket; un peso ausente no provoca una descarga silenciosa.

Licencias de origen: [LightGlue](https://github.com/cvg/LightGlue/blob/main/LICENSE) Apache 2.0,
[ALIKED](https://github.com/Shiaoming/ALIKED/blob/main/LICENSE) BSD-3,
[XFeat](https://github.com/verlab/accelerated_features/blob/main/LICENSE) Apache 2.0 y
[OpenCV](https://opencv.org/license/) Apache 2.0. No se utiliza el extractor/peso original de SuperPoint.
Retener los avisos de los componentes utilizados al distribuirlos. Las dependencias del banco no se añaden al APK.

## Importar y comparar registros

```sh
Tools/LocalizacionOffline/.venv/bin/python Tools/LocalizacionOffline/offline.py inventario /ruta/grabaciones --output Tools/LocalizacionOffline/salida/dataset.json
Tools/LocalizacionOffline/.venv/bin/python Tools/LocalizacionOffline/offline.py pares Tools/LocalizacionOffline/salida/dataset.json --method todos --max-side 1024 --keypoints 2048 --limit 12 --output Tools/LocalizacionOffline/salida/pares.jsonl
Tools/LocalizacionOffline/.venv/bin/python Tools/LocalizacionOffline/offline.py informe Tools/LocalizacionOffline/salida/pares.jsonl --output Tools/LocalizacionOffline/salida/informe.md
```

Si faltan fotos, `inventario` se detiene; `--skip-missing` permite excluirlas explícitamente y deja su lista en el JSON.
Los PNG/PGM/PPM presentes sin fila de metadatos se enumeran en `unreferenced_images` y se cuentan al importar;
no se les inventan intrínsecos, tiempos ni poses a partir del nombre del archivo.
El inventario guarda hashes y nunca convierte la respuesta de Immersal en verdad de terreno. Los pares se reparten
entre edificios y combinaciones de edificios; no comparan una imagen consigo misma ni positivos del mismo punto.
`--limit` fija el máximo de pares del mismo edificio y de otros edificios por método.

**Una homografía no es una pose 3D.** ‘Mismo edificio’ no garantiza vistas solapadas. Los inliers y tiempos de estos
pares son un diagnóstico, no recall, precisión en centímetros ni latencia de inicio Android. La extracción en caché
conserva el tiempo de cómputo original. Repetir con 640/1024/1440 y 1024/2048 puntos; mantener las mismas parejas.

## Crear un mapa abierto y localizar

Se necesita una reconstrucción COLMAP con cámaras PINHOLE e imágenes rectificadas, nombres iguales a `id` del inventario,
y escala obtenida de medidas físicas. No importar una nube Immersal PLY como si incluyera descriptores y cámaras.

```sh
Tools/LocalizacionOffline/.venv/bin/python Tools/LocalizacionOffline/offline.py importar-colmap salida/referencias.json --model /ruta/modelo-colmap --meters-per-unit 1.0 --metric-source "Medidas de control documentadas" --output salida/registradas.json
Tools/LocalizacionOffline/.venv/bin/python Tools/LocalizacionOffline/offline.py mapa salida/registradas.json --method aliked-lightglue --output salida/mapa.json
Tools/LocalizacionOffline/.venv/bin/python Tools/LocalizacionOffline/offline.py localizar salida/consultas.json --map salida/mapa.json --output salida/poses.json
```

`meters-per-unit` debe ser la escala real; 1.0 solo corresponde si el modelo ya está en metros. No equivale a comprobar
alineación de fachadas. `registered_mapping.frames` también puede construirse con `T_camera_map` explícita: matrices 4×4
que llevan mundo métrico a cámara OpenCV (x derecha, y abajo, z delante), `K`, `building`, `id`, `session`, `path`, `sha256`.
El resto son los campos del inventario.

Cada método triangula sus propias características con las mismas cámaras. Se exige paralaje >1°, profundidad positiva
y reproyección ≤3 px; se rechazan triangulaciones que discrepen >20 cm. El localizador verifica PnP con RANSAC y LM,
al menos 20 inliers y apoyo de dos referencias a ≤30 cm/2°. Son umbrales iniciales, pendientes de calibración, no una
garantía de precisión. Una consulta no puede compartir sesión ni hash con ninguna referencia. Los resultados incluyen
correspondencias geométricas, pero `ground_truth` permanece nulo.
El mapa conserva la firma del extractor, código, parámetros y pesos. Un cambio exige reconstruirlo; no se reutilizan
índices de características de una versión distinta.

## Secuencias

`secuencia entrada.json --output salida.json` ajusta una transformación rígida `T_map_ar` con pérdida robusta Huber.
Entrada: `initial_T_map_ar` (4×4) y `observations`: `capture_time`, `tracking_epoch`, `xyz` (N×3, metros del mapa),
`xy` (N×2, píxeles), `K` (3×3) y `T_camera_ar` (4×4, AR métrico → cámara óptica OpenCV).
La ventana debe estar ordenada, durar ≤5 s y pertenecer a una única época de tracking. Solo debe contener correspondencias
geométricamente verificadas. La odometría se fija dentro de esta ventana: no es un SLAM ni corrige errores de escala.

**No pasar `rot_camara` de los registros directamente como `T_camera_ar`.** Hay que validar la conversión entre imagen
cruda del sensor, cámara de presentación y ejes Unity/OpenCV. Los nuevos barridos guardan tiempos de sensor y pose,
matriz display en orden de columnas, orientación de pantalla y `pose_optica_validada:false` precisamente para no ocultarlo.
El ajuste multicuadro está probado con cámaras sintéticas calibradas; su uso físico sigue pendiente de esa validación.

## Mapa métrico desde el dron y validación de la pose óptica

Sin mapa métrico de fachada, la fuente de metros son las fotos del dron (RealityScan, escala por GPS, ~1 %). Dos comandos,
ambos solo numpy/scipy (sin pycolmap ni torch):

```sh
# Cámaras métricas desde una exportación COLMAP de texto (cameras.txt, images.txt, points3D.txt) + georreferencia
offline.py importar-realityscan /ruta/colmap-txt --georef Escenas/Campus/Campus-georef.json --building X1     --images /ruta/fotos --recortar 12,-40,7,40,25,14 --distancia-max 60 --output salida/dron-X1.json
# Opcional: --campus-json Escenas/Campus/campus.json --edificio-campus X1 (pasa al marco del campus)

# Pose óptica: SfM de un barrido contra las poses ARCore del inventario
offline.py alinear-arcore salida/dataset.json --model /ruta/colmap-txt-del-barrido --sesion barrido-20261105-...     --output salida/alineacion.json --inventario-salida salida/dataset-validado.json
```

`importar-realityscan` produce el mismo `registered_mapping` que `importar-colmap` (alimenta a `mapa`). Exige PINHOLE o
SIMPLE_PINHOLE y rechaza modelos con distorsión. La georreferencia sigue la fórmula de `georef_dron.js`
(`ENU = escala·R·(colmap − centroColmap) + centroEnu`, sobre los centros de `images.txt`; no sobre el PLY, cuyos ejes son
(x, −z, y)). `metric_source` queda como «georreferencia GPS del dron, residuo mediana … m, p90 … m»: el residuo del GPS
(≈ 0,5 m en el campus) limita la posición absoluta, no la escala relativa. Con `--campus-json` se aplica además
`yaw + t` de `campus.json`, que está en (E, arriba, N): aquí se reexpresa en (E, N, arriba) para conservar un marco
derecho (una cámara OpenCV no puede vivir en un marco izquierdo). Vale solo si el modelo está en el marco de la nube de ese
edificio. `--recortar cx,cy,cz,sx,sy,sz` (caja alineada a ejes en el marco de salida) deja las cámaras a ≤ `--distancia-max`
m que ven el centro de la caja o ≥ 3 de sus esquinas. Con `--images` se guardan `path` y `sha256` y se comprueban las
dimensiones; sin ellas `mapa` no puede usar el cuadro.

`alinear-arcore` estima la similitud SfM → ARCore (Umeyama) con las posiciones de las cámaras, barre un desfase temporal
−100…+100 ms (paso 5 ms, refinado parabólico; la pose ARCore de la imagen *i* es la leída en `t_i + desfase`, con
interpolación lineal/slerp) y, con esa similitud, compara orientaciones para elegir la convención de ejes Unity → OpenCV:
rotación de la imagen 0/90/180/270° y flip de y. El espejo de x del mundo ya gasta una lateralidad, así que solo las
candidatas con flip de y son rotaciones válidas; las otras cuatro se descartan (no se ajustan). `R_mundo_óptica =
diag(−1,1,1) · R_unity · C`; `t_camera_ar()` arma la `T_camera_ar` de `secuencia` con esa `C`. Salida: escala
SfM/ARCore, desfase, residuo de posición en cm (rms/mediana/máx), error de rotación, tabla de candidatas y `T_ar_sfm`.
`pose_optica_validada` es `true` solo si: ≥ 10 cámaras, recorrido (diámetro de posiciones) ≥ 1 m, extensión lateral ≥ 5 cm
(un recorrido recto deja indeterminado el giro alrededor de la línea), residuo rms < 5 cm, error mediano de rotación < 3° y
convención no ambigua (la segunda candidata ≥ 2× peor). Todos los umbrales son opciones. `--inventario-salida` copia el
inventario marcando `optical_pose_validated`, la convención y el desfase en las imágenes usadas, solo si se validó.
Suposición no verificada con datos reales: que `rot_camara` sea el cuaternión de Unity (xyzw) del cuerpo de la cámara de
presentación y `pos_camara` su posición; si el residuo no baja de 5 cm con ninguna convención, el culpable es esa lectura,
el desfase o la deriva de ARCore, no el algoritmo.

## Pruebas

```sh
Tools/LocalizacionOffline/.venv/bin/python -m unittest discover -s Tools/LocalizacionOffline -p 'test_*.py' -v
```

Comprueban rechazo de correspondencias falsas, recuperación de pose/escala conocida, paralaje, refinamiento temporal,
importación segura, bloqueo de red, separación de sesiones y mapa → consulta con geometría controlada, importación
RealityScan sintética (georreferencia, recorte, campus, distorsión) y alineación ARCore con convención y desfase conocidos.
En Windows: `Tools/LocalizacionOffline/.venv/Scripts/python -m unittest …`. Las pruebas que necesitan torch se saltan solas.

## Registros del 5-nov-2026

Diagnóstico y decisiones: [informe de terreno](../../Docs/LocalizacionOffline-Registros-2026-11-05.md).
Copias verificadas, inventarios, particiones, scripts reproducibles, correspondencias y ensayo SfM quedan en
`salida/20261105/` (ignorado por Git). La reconstrucción exploratoria usa escala arbitraria y no se importa como mapa
métrico. Las imágenes sin JSONL se mantienen en un conjunto separado, apto solo para diagnóstico de correspondencias.
