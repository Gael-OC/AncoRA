# Banco de localización offline

Próxima captura de construcción: [APK de recorrido y guía de prueba](../../Docs/LocalizacionOffline-PruebaConstruccion.md).
El nuevo modo de 60 s a 2 imágenes/s se compila con `AncorRA.Editor.PaseoSetup.BuildConstructionCaptureAndroid` y genera
`Builds/Android/AncoRA-Construccion-Offline.apk`. Las consultas mantienen sus barridos de cinco segundos.

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

## Pruebas

```sh
Tools/LocalizacionOffline/.venv/bin/python -m unittest discover -s Tools/LocalizacionOffline -p 'test_*.py' -v
```

Comprueban rechazo de correspondencias falsas, recuperación de pose/escala conocida, paralaje, refinamiento temporal,
importación segura, bloqueo de red, separación de sesiones y mapa → consulta con geometría controlada.

## Registros del 5-nov-2026

Diagnóstico y decisiones: [informe de terreno](../../Docs/LocalizacionOffline-Registros-2026-11-05.md).
Copias verificadas, inventarios, particiones, scripts reproducibles, correspondencias y ensayo SfM quedan en
`salida/20261105/` (ignorado por Git). La reconstrucción exploratoria usa escala arbitraria y no se importa como mapa
métrico. Las imágenes sin JSONL se mantienen en un conjunto separado, apto solo para diagnóstico de correspondencias.

## Reconstrucción aprendida solo desde imágenes

Resultado ejecutado: [geometría de Teología](../../Docs/LocalizacionOffline-Geometria-2026-11-05.md). XFeat sostiene
fachada parcial sin vegetación, pero las conexiones globales son inestables. La escala es arbitraria:
**no importar estos modelos al localizador con umbrales en metros**. D/E nuevos quedaron reservados y J no participó.

`sfm_aprendido.py` importa correspondencias de XFeat+LighterGlue o ALIKED+LightGlue con sus índices originales y verifica
geometría antes del mapper. El PINHOLE registrado es un prior fijo; no valida distorsión, escala ni sincronía óptica.
Para sensibilidades explícitas, el frame admite `distortion: [k1,k2,p1,p2,k3]` en píxeles originales:
COLMAP usa OPENCV/FULL_OPENCV con K/D fijas. No rectifica ni transforma características; D ausente mantiene el
baseline PINHOLE. Esto **no habilita aún distorsión en `mapa`, `localizar` ni `secuencia`**: su contrato sigue
siendo PINHOLE rectificado. No pasarles directamente estos modelos crudos ni una escala arbitraria.
Matching y SfM corren en procesos separados por OpenMP en macOS. Características y punto principal se desplazan
juntos +0,5 px al entrar a COLMAP, conservando los rayos. Los comandos bloquean red y rechazan una base existente.

Entrada: manifiesto con `role: "construction"`, `frames`, `excluded_sessions` y `excluded_hashes` de las consultas;
cada frame requiere `K`, dimensiones, ruta y hash. `seleccionar` fija hasta siete posiciones ordinales equiespaciadas
por sesión. Las 182 vistas disponibles y las 49 seleccionadas se conservan en `salida/20261105/sfm-aprendido/`.

Ejemplo para un **ensayo nuevo**, desde la raíz del repositorio:

```sh
SFM_PY=Tools/LocalizacionOffline/.venv/bin/python
SFM_TOOLS=Tools/LocalizacionOffline
SFM_RUN=Tools/LocalizacionOffline/salida/ensayo-xfeat
$SFM_PY $SFM_TOOLS/sfm_aprendido.py seleccionar $SFM_TOOLS/salida/20261105/sfm-aprendido/disponibles.json --output $SFM_TOOLS/salida/seleccion-nueva.json
$SFM_PY $SFM_TOOLS/sfm_aprendido.py correspondencias $SFM_TOOLS/salida/seleccion-nueva.json --method xfeat-lighterglue --output $SFM_RUN
$SFM_PY $SFM_TOOLS/sfm_aprendido.py reconstruir --output $SFM_RUN
$SFM_PY $SFM_TOOLS/sfm_diagnostico.py analizar --source $SFM_RUN --annotations $SFM_TOOLS/experimentos/teologia-regiones-20261105.json
$SFM_PY $SFM_TOOLS/sfm_diagnostico.py filtrar-fachada --source $SFM_RUN --annotations $SFM_TOOLS/experimentos/teologia-regiones-20261105.json --output $SFM_RUN-fachada
$SFM_PY $SFM_TOOLS/sfm_aprendido.py reconstruir --output $SFM_RUN-fachada
$SFM_PY $SFM_TOOLS/sfm_diagnostico.py analizar --source $SFM_RUN-fachada --annotations $SFM_TOOLS/experimentos/teologia-regiones-20261105.json
$SFM_PY $SFM_TOOLS/sfm_diagnostico.py comparar --source $SFM_RUN/modelos/0 --other $SFM_RUN-fachada/modelos/0 --dataset $SFM_TOOLS/salida/seleccion-nueva.json --anchor grabacionesSamsung/barrido-20261105-103656-acadbf2e/0014-TEO-B-10m.png --output $SFM_TOOLS/salida/consistencia-nueva.json
```

Repetir con `--method aliked-lightglue` y otra salida para comparar, conservando selección y parámetros.
`--max-side 1440` corresponde a la sensibilidad adicional de XFeat, no a la comparación principal a 1024.
Las regiones manuales corresponden solo a esas 49 imágenes; otra selección requiere sus propias anotaciones.

El diagnóstico guarda tracks, ángulos estimados, reproyección en píxeles originales, cobertura por cuadrícula y
proyecciones. Fuera de las regiones conservadoras la clase es desconocida. Los componentes sin puntos 3D se marcan
explícitamente; su reproyección cero no significa éxito. La comparación entre modelos mide estabilidad interna,
no exactitud métrica. La suite sintética comprueba la integración y la invariancia ante una similitud pura.

## Recorridos recibidos la tarde del 5-nov-2026

[Informe y decisión](../../Docs/LocalizacionOffline-Recorridos-2026-11-05.md): 31 sesiones íntegras entregadas,
1.900 PNG únicos; el log cita además dos barridos X1 sin carpeta. Teología aporta 580 imágenes de construcción.
La selección densa de 65 vistas mejora mucho ambos métodos; quedan cámaras discrepantes y la unión histórica es inestable. No importarlos al localizador métrico.

Salida conservada: `salida/20261105-recorridos/`. `procedencia.json` guarda los SHA-256 iniciales de las 2.392
copias; `zip-miembros.json`, el CRC verificado y SHA-256 de cada miembro. Los originales de las exportaciones
sueltas ya no estaban en Descargas al finalizar, pero sus bytes están preservados en `originales/1` y `originales/2`.
`originales/0` corresponde a `ancora ultimas grabacoines`. No sobrescribir estas copias para repetir.

La auditoría se puede reproducir desde las copias conservadas, usando **otra salida**:

```sh
PY=Tools/LocalizacionOffline/.venv/bin/python
TOOLS=Tools/LocalizacionOffline
BASE=$TOOLS/salida/20261105-recorridos
RUN=$TOOLS/salida/repeticion-recorridos
$PY $TOOLS/auditar_recorridos.py --source "$BASE/originales/0" --source "$BASE/originales/1" --source "$BASE/originales/2" --output "$RUN"
$PY $TOOLS/sfm_aprendido.py seleccionar "$RUN/TEO-disponibles.json" --output "$RUN/TEO-seleccion.json"
$PY $TOOLS/sfm_aprendido.py correspondencias "$RUN/TEO-seleccion.json" --method xfeat-lighterglue --output "$RUN/teo-xfeat"
$PY $TOOLS/sfm_aprendido.py reconstruir --output "$RUN/teo-xfeat" --runtime 240
$PY $TOOLS/sfm_diagnostico.py analizar --source "$RUN/teo-xfeat" --annotations "$TOOLS/experimentos/teologia-recorridos-regiones-v2-20261105.json"
$PY $TOOLS/sfm_diagnostico.py filtrar-fachada --source "$RUN/teo-xfeat" --annotations "$TOOLS/experimentos/teologia-recorridos-regiones-v2-20261105.json" --output "$RUN/teo-xfeat-fachada-v2"
$PY $TOOLS/sfm_aprendido.py reconstruir --output "$RUN/teo-xfeat-fachada-v2" --runtime 240
$PY $TOOLS/sfm_diagnostico.py analizar --source "$RUN/teo-xfeat-fachada-v2" --annotations "$TOOLS/experimentos/teologia-recorridos-regiones-v2-20261105.json"
$PY $TOOLS/experimentos/evidencia_recorridos.py --source "$RUN/teo-xfeat-fachada-v2"
```

Repetir las mismas operaciones con `--method aliked-lightglue` y salidas `teo-aliked`/`teo-aliked-fachada-v2`.
Esperar a que **termine** cada matching antes de reconstruir. No reutilizar una base parcial si falló un proceso;
conservar el incidente y crear una base nueva en otra salida. La auditoría rechaza una copia existente diferente.
Los JSONL originales nunca se ordenan ni editan: las estadísticas se calculan ordenando sus tiempos registrados,
y `jsonl_order_monotonic` conserva si el orden de escritura era distinto. La partición sigue `tipo_captura`
por sesión completa, excluye también los hashes de las consultas y deja roles contradictorios como ambiguos.

La sensibilidad de densidad usa el manifiesto conservado `TEO-seleccion-densa.json`: 13 ordinales equiespaciados
por sesión, 65 vistas. Ejecutar `correspondencias` con ese manifiesto en una salida nueva, reconstruir con
`--runtime 300` y usar `experimentos/teologia-recorridos-densa-regiones-v2-20261105.json` para analizar/filtrar.
Sus rutas apuntan a las copias protegidas; no añade consultas ni interpola calibración.

La unión con las 49 referencias previas reutiliza pares compatibles y calcula solo los cruzados:

```sh
$PY $TOOLS/experimentos/conectar_recorridos.py --old "$TOOLS/salida/20261105/sfm-aprendido/xfeat" --new "$RUN/teo-xfeat" --output "$RUN/teo-union-xfeat"
$PY $TOOLS/sfm_aprendido.py reconstruir --output "$RUN/teo-union-xfeat" --runtime 300
$PY $TOOLS/sfm_diagnostico.py filtrar-fachada --source "$RUN/teo-union-xfeat" --annotations "$BASE/union-regiones-v2.json" --output "$RUN/teo-union-xfeat-fachada-v2"
$PY $TOOLS/sfm_aprendido.py reconstruir --output "$RUN/teo-union-xfeat-fachada-v2" --runtime 300
```

La extensión verifica firmas, hashes, completitud de ambos matchings y coincidencia exacta de keypoints antes
de conservar sus índices. Los ensayos de CB/X1 siguen `seleccionar` → `correspondencias` → `reconstruir` sobre
sus respectivos manifiestos. Sus máscaras vacías dejan todo como desconocido: son proyecciones exploratorias,
sin validación semántica ni de estabilidad. Los `consistencia-v2-*.json` conservan fuentes y anclas exactas para
repetir cada comparación con `sfm_diagnostico.py comparar`. `fuentes/` archiva los scripts y el manifiesto de pesos.

Las cifras finales usan las máscaras v2 y salidas `*-fachada-v2`: se corrigieron exclusiones de lámpara, persona y tablero al revisar las proyecciones ampliadas. Las máscaras/salidas sin v2 son preliminares conservadas. `fuentes/comparar_v2.py` repite las comparaciones finales; `generar_informe.py` reproduce el informe desde los JSON. Repetir también ALIKED sobre las mismas 65 vistas para el contraste denso.

## Revisión de cámaras y seis vistas intermedias

Resultados y advertencias: [revisión de cámaras](../../Docs/LocalizacionOffline-RevisionCamaras-2026-11-05.md); preparación de campo: [visita de Teología](../../Docs/LocalizacionOffline-Visita-Teologia.md). Los resultados están en `salida/20261105-revision-camaras/`. Ninguno es métrico ni se debe importar al localizador con umbrales en metros.

Para reproducir la ampliación en una salida nueva, después de preparar las 65 vistas y sus matchings completos conservados:

```sh
PY=Tools/LocalizacionOffline/.venv/bin/python
T=Tools/LocalizacionOffline
B=$T/salida/20261105-recorridos
R=$T/salida/otra-ampliacion
$PY $T/experimentos/ampliar_vecindades_20261105.py "$R"
$PY $T/sfm_aprendido.py correspondencias "$R/TEO-intermedias-seleccion.json" --method xfeat-lighterglue --output "$R/intermedias-xfeat"
$PY $T/experimentos/conectar_recorridos.py --old "$B/teo-denso-xfeat" --new "$R/intermedias-xfeat" --output "$R/ampliado-xfeat"
$PY $T/sfm_aprendido.py correspondencias "$R/TEO-intermedias-seleccion.json" --method aliked-lightglue --output "$R/intermedias-aliked"
$PY $T/experimentos/conectar_recorridos.py --old "$B/teo-denso-aliked" --new "$R/intermedias-aliked" --output "$R/ampliado-aliked"
$T/experimentos/reconstruir_ampliado_20261105.sh "$R"
$PY $T/experimentos/comparar_ampliado_20261105.py "$R"
```

Esperar a que terminen ambos matchings; el script de reconstrucción ejecuta COLMAP por separado de PyTorch. Reutiliza los 2.080 pares originales, calcula 15 entre intermedias y 390 cruzados por método. Las seis máscaras se agregan a las 65 v2, sin modificar estas últimas. El conector conservado inicialmente rotulaba cualquier unión como «49+35» y progreso `/1715`; se corrigieron sus rótulos para que indiquen los tamaños reales. Los artefactos ya ejecutados mantienen el rótulo antiguo y las listas reales verificadas 65+6. Las salidas no deben reemplazar los modelos base. La sensibilidad temporal previa está descrita con sus comandos en el informe.

## Sensibilidad óptica del 15T ejecutada el 6-nov

[Resultados y límites](../../Docs/LocalizacionOffline-OpticaTeologia-2026-11-06.md): 12 condiciones nuevas y cuatro
réplicas sobre 65/71 vistas. Baseline registrado, radial y Brown; solo cambia Xiaomi 25069PTEBG, no Xiaomi 14.
Se mantienen PNG/características/índices y máscaras originales, verificando geometría desde bases nuevas.
La transferencia no mejora de forma fiable la geometría: hay fragmentación, inversiones y tracks entre ventanas distintas.
No se aceptó mapa, aplicó escala ni ejecutó localización métrica sobre estas salidas.

Para repetir en una salida inexistente, desde raíz:

```sh
PY=Tools/LocalizacionOffline/.venv/bin/python
T=Tools/LocalizacionOffline
R=$T/salida/otra-sensibilidad-optica
$PY $T/experimentos/sensibilidad_optica_20261106.py preparar --output "$R"
$PY $T/experimentos/sensibilidad_optica_20261106.py reconstruir --output "$R"
$PY $T/experimentos/sensibilidad_optica_20261106.py comparar --output "$R"
$PY $T/experimentos/sensibilidad_optica_20261106.py repetir --output "$R"
$PY $T/experimentos/sensibilidad_optica_20261106.py diagnosticar --output "$R"
```

Salida conservada `salida/20261106-optica-teologia/`: `procedencia.json`, hashes por archivo en `reutilizacion.json`,
modelos/logs, comparaciones, distribución de tracks y `revision-identidad.json` con observaciones concretas.
`medidas-asociacion-confirmada.json` registra las cuatro ventanas/ancho interior confirmados y separación previa:
INTERIOR-C-D para ajuste exploratorio; R-S, H-I y la nueva INTERIOR-VUELTA-FRENTE de 1,95 m reservadas.
Aristas 2D/3D e incertidumbre siguen pendientes; ninguna medida se ha usado para ajustar.

## Corrección de identidad de ventanas, 6-nov

[Resultado ejecutado](../../Docs/LocalizacionOffline-IdentidadTeologia-2026-11-06.md): 38 reconstrucciones adicionales,
revisión independiente con 6.1 Terra, filtro de correspondencias entre identidades físicas incompatibles y pruebas
contra uniones transitivas. La v6 reduce la discrepancia de `142828/0111` sin retirarla; XFeat/ALIKED comparten 61 cámaras y
su mayor diferencia angular baja de ~94–98° a ~3,5°. Es estabilidad interna, no precisión en centímetros.
Persisten tracks contradictorios y sensibilidad óptica. Los intentos de aislar componentes completos se conservan
como resultados negativos: dan cero conflictos conocidos a costa de perder el mapa global.

El filtro conserva PNG, keypoints, índices y todas las imágenes de entrada. Las regiones desconocidas permanecen
sin aprobar ni rechazar. Solo se etiquetan ventanas superiores visibles; la máscara antigua de fachada contiene
algunas zonas ajenas al edificio y no sirve por sí sola como identidad física.

```sh
PY=Tools/LocalizacionOffline/.venv/bin/python
T=Tools/LocalizacionOffline
SRC=$T/salida/20261106-optica-teologia/71-xfeat-radial_k1
RUN=$T/salida/otra-identidad-xfeat-radial
$PY $T/experimentos/identidad_teologia_20261106.py filtrar --source "$SRC" --annotations "$T/experimentos/teologia-identidades-v6-20261106.json" --output "$RUN"
$PY $T/sfm_aprendido.py reconstruir --output "$RUN" --runtime 300
$PY $T/experimentos/identidad_teologia_20261106.py inspeccionar --source "$RUN" --annotations "$T/experimentos/teologia-identidades-v6-20261106.json"
```

Elegir una salida inexistente; repetir con las fuentes baseline/ALIKED correspondientes para comparar.
`cuarentena_comun_20261106.py` verifica mismo dominio de características y pares numéricamente iguales entre
las dos hipótesis ópticas. `grafo_identidad_20261106.py` conserva el control unión de inliers y el ensayo de
componentes descartados. Son diagnósticos condicionados por las anotaciones y ambas reconstrucciones, no verdad.
La salida `salida/20261106-identidad-teologia/` conserva fuentes, modelos, logs, auditorías, anotaciones, caminos de
puentes falsos y la confirmación fotográfica de los cuatro bordes de cinta. Ninguna escala aplicada; los valores
reservados no se usan para filtrar ni elegir modelos. Importación/PnP/multicuadro/Android siguen pendientes de
sus contratos ópticos y validación física; este filtro no habilita un motor nuevo en la app.
La repetición final también conserva un resultado negativo: tres condiciones se reproducen, pero XFeat radial
encuentra otra solución con diferencias máximas de 14,64 % del diámetro de centros y 3,75° pese a iguales
entradas/semilla. El acuerdo entre las primeras corridas v6 no se presenta como estabilidad garantizada.

## JSONL numerados y procedencia de las 71 vistas

La lectura de inventario/auditoría reconoce `intentos.jsonl` y copias numeradas como `intentos (1).jsonl` en la
misma carpeta, elimina filas idénticas solapadas entre exportaciones y rechaza metadatos contradictorios para
una misma imagen. Los roles se comprueban sobre todas las filas: una copia adicional no convierte consultas en
construcción. No se modifican los JSONL originales.

[Revisión de Descargas y procedencia](../../Docs/LocalizacionOffline-OrigenImagenes-2026-11-06.md): las 71 son una
selección de 580 PNG de construcción de la tarde. Se recuperaron metadatos de 26 PNG históricos omitidos al leer
solo el nombre canónico; 25 son de Teología. Quedan por clasificar, sin añadirlos al mapa o a consultas ni aplicarles
la calibración de casa. `salida/20261106-origen-imagenes/` conserva inventarios, hashes y siete vistas adyacentes
candidatas para el siguiente ensayo; no contiene una reconstrucción nueva con ese material.
