# Registros offline del 5 de noviembre de 2026

Los datos permiten validar la captura y seguir comparando extractores. **Todavía no permiten validar un mapa de la
fachada, precisión de 10–20 cm ni un inicio en cinco segundos.** XFeat+LighterGlue merece la siguiente prueba geométrica:
mejora las correspondencias entre sesiones en este ensayo, sin convertirse por ello en un localizador validado.

Auditoría realizada en `localizacion-offline`, conservando los cambios locales anteriores. No se modificaron los
originales de Descargas y no se recompiló el APK ni se cambiaron assets de Unity durante el análisis de registros.

## Procedencia y conservación

Se revisaron las cinco exportaciones `AncoRA-registro-…-20261105-…`, `grabacionesSamsung`,
`barrido-20261105-104222-f3ea96b4`, `Teología J` y sus tres ZIP. Son datos nuevos del 5-nov; las grabaciones del
28-oct y los registros del 23/27/29-oct se mantuvieron separados como históricos.

- 339 archivos copiados y comprobados con SHA-256 en `Tools/LocalizacionOffline/salida/20261105/`.
- Los tres ZIP pasan CRC; sus 28, 28 y 84 archivos coinciden byte por byte con las carpetas correspondientes.
- Los 15 logs exportados corresponden a 11 sesiones de app. Las exportaciones repetidas son prefijos del log más largo;
  se analiza este último una sola vez. Los 179 JPEG exportados corresponden a 110 JPEG distintos, sin conflictos.
- Los JPEG de depuración se conservan aparte: no aportan la calibración de los PNG originales.
- 136 PNG nuevos decodificables, en gris, 1920×1080. No hay duplicados exactos por hash de archivo ni de píxeles.

La carpeta de salida está ignorada por Git. `procedencia.json` registra origen, copia, tamaño y hash;
`deduplicacion.json`, `auditoria.json` y `diagnostico-logs.json` contienen los controles detallados.

## Integridad de las capturas

| Captura | Teléfono | Etiqueta guardada | PNG / filas | Duración entre imágenes | Cadencia |
|---|---|---|---:|---:|---:|
| `103656-acadbf2e` | Samsung SM-S931B | TEO-B-10m, sol | 27 / 27 | 4,828 s | 5,386/s |
| `103806-9a9d4757` | Samsung SM-S931B | TEO-D-10m, sol | 27 / 27 | 4,836 s | 5,377/s |
| `104229-922d5574` | Samsung SM-S931B | TEO-A-10m, sol | 27 / 27 | 4,839 s | 5,373/s |
| `104222-f3ea96b4` | Xiaomi 25069PTEBG | TEO-E-15m, sol | 27 / 27 | 4,831 s | 5,382/s |
| «Teología J» | Sin metadatos por imagen | TEO-C-15m en el nombre | 28 / 0 | Desconocida | Desconocida |

Los identificadores abreviados pertenecen a carpetas `barrido-20261105-…`. Las cuatro capturas completas tienen
numeración 1–27, nombres y etiquetas coincidentes, correspondencia PNG↔JSONL exacta y ningún archivo ausente o extra.
No se reinterpretaron D-10m ni E-15m según la pauta propuesta de puntos: se conservó lo registrado. No hay ensayos nuevos
a 20/25 m ni de otros edificios o luces.

Intrínsecos constantes dentro de cada barrido y compatibles con sus dimensiones, con focales positivas y punto principal
dentro de la imagen. Valores `fx, fy, cx, cy`, en píxeles:

- Samsung B y D: `1321,7085; 1324,12622; 965,755737; 536,834656`.
- Samsung A: `1314,55652; 1313,66675; 960,80188; 532,390137`.
- Xiaomi E: `1257,07227; 1254,63892; 974,487061; 536,681519`.

Los tiempos de captura y sensor crecen estrictamente. Los intervalos de captura están entre 0,167 y 0,205 s, sin huecos
largos; 27 imágenes en una ventana de cinco segundos es compatible con la planificación por frames a hasta 6/s. Todos
los registros declaran `SessionTracking`, una sola época por barrido, pantalla Portrait, matriz display constante y
`pose_optica_validada:false`. Los cuaterniones tienen norma unitaria dentro de 10⁻⁷.

`timestamp_sensor` y `timestamp_pose_sensor` coinciden en las filas, pero esto solo acredita lo que escribió el grabador:
no valida la pose óptica ni la sincronización real de la pose leída en LateUpdate. La máxima discrepancia entre intervalos
de sensor y de captura es 11,25 ms. No se convirtieron las poses Unity en cámaras calibradas ni se aplicó refinamiento
multicuadro físico con ellas.

En los cuatro intervalos completos no hay cambios de tracking, avisos de interrupción, errores de acceso a imagen ni
intentos de Immersal terminados. Después de cada uno vuelve a aparecer `INTENTO_PASEO`, compatible con la pausa y
restauración previstas. Fuera de esos intervalos hay 116 errores `Could not acquire camera image` en los 11 logs,
alrededor de arranques/reanudaciones; no demuestran pérdida de imágenes en estos cuatro barridos.

El Xiaomi E dice `GRABACION_FIN imagenes=26`, pero llegaron 27 PNG con 27 filas válidas. `PaseoRecorder.Disable`
escribe ese contador antes de terminar todas las escrituras pendientes: ese log no es un inventario final. No se descartó
la imagen 27 para hacer coincidir el contador.

## Capturas mencionadas en los logs pero no disponibles

Los logs únicos contienen 12 inicios y finales normales de barrido. Solo cuatro llegaron completos. Los 28 PNG de
«Teología J», su etiqueta C-15m y su cantidad son compatibles con el barrido del Xiaomi M2102J20SG
`barrido-20261105-103946-ac7dc11b`, cuyo log dice 28 imágenes. Es una asociación probable, no una restitución de
metadatos: falta su `intentos.jsonl`; no se inventaron teléfono, intrínsecos, tiempos ni tracking para esas imágenes.

Además faltan las carpetas Samsung que los logs sí registran como terminadas:

| Carpeta completa esperada | Etiqueta | Imágenes indicadas al detener |
|---|---|---:|
| `barrido-20261105-101927-56b7a905` | TEO-B-10m | 27 |
| `barrido-20261105-101934-51a6caca` | TEO-B-10m | 27 |
| `barrido-20261105-101955-20f8d842` | TEO-B-10m | 27 |
| `barrido-20261105-102025-05ee6582` | TEO-B-10m | 27 |
| `barrido-20261105-102304-a48ae5d4` | TEO-B-10m | 27 |
| `barrido-20261105-104121-fd5c9f05` | TEO-A-10m | 27 |
| `barrido-20261105-104144-be4ce43f` | TEO-A-10m | 27 |

Gael confirmó después de la auditoría que lo entregado es todo el material que tiene. Las siete carpetas y el JSONL de J
se consideran no disponibles: no se condiciona el avance a recuperarlos ni se reconstruyen sus metadatos por conjetura.

## Inspección visual y movimiento

Se inspeccionaron inicio, centro y final de las cinco series, el inicio/final de D a mayor tamaño y correspondencias
seleccionadas. La exposición es utilizable: ningún frame tiene más de 0,28 % de píxeles ≥253. Hay textura en ventanas,
barandas y vegetación; grandes paredes lisas, ventanas repetidas y vegetación cercana dificultan la geometría útil.
La nitidez varía, especialmente al final de D, pero no hay imágenes negras ni una exposición global inutilizable.
La varianza del Laplaciano se guarda como diagnóstico, sin usarla como medida comparable de enfoque entre escenas.

Las imágenes están giradas respecto de la presentación vertical, de forma compatible con su orientación de sensor.
Las vistas previas pueden girarse para leerlas; los originales y las coordenadas del banco permanecen intactos.

Las poses Unity, tomadas solo como telemetría sin calibrar, indican diámetro de posiciones de 0,097 m en B,
0,158 m en A, 0,656 m en D y 0,368 m en E; ángulo respecto del primer frame de 20,6°, 22,6°, 24,2° y 86,0° respectivamente.
Predomina el giro. E cubre más fachada, pero sus extremos pueden perder solapamiento. No se utilizaron estos
desplazamientos para escalar una reconstrucción ni para afirmar paralaje óptico.

Evidencia visual local: `muestras.png`, `muestras-D.png`, `matches-D.png`, `matches-J.png`,
`matches-historica.png` y `matches-negativa.png` en la carpeta de salida.

## Comparación reproducible de los cuatro métodos

Configuración fijada: lado máximo 1024 px, hasta 2048 características, homografía MAGSAC a 3 px de la imagen original,
semilla OpenCV 5, pesos locales y red bloqueada. Mismos pares para cada método. Se eligieron inicio, centro y final por
posición en la serie, sin seleccionar retrospectivamente los frames con más matches.

Partición por barridos completos, sin sesiones ni hashes compartidos:

- Construcción: A y B del Samsung, 54 imágenes (`construccion.json`).
- Evaluación: D del Samsung y E del Xiaomi 15T, 54 imágenes (`evaluacion.json`).
- Diagnóstico solo 2D: «Teología J», 28 imágenes sin calibración (`solo-imagenes-J.json`).
- Referencias históricas de diagnóstico: una imagen central de cada una de las 12 sesiones disponibles del 28-oct,
  copiadas y verificadas antes de usarlas; cinco son de Teología y siete de otros edificios.

Se ejecutaron **249 pares × 4 métodos = 996 comparaciones**, no 996 ensayos independientes de localización:
75 pares históricos/nuevos de Teología, 105 con edificios distintos, 54 entre construcción nueva y consultas D/E/J,
y 15 internos a las series. Estos últimos solo evalúan continuidad visual y se informan aparte.

| Método | Mediana H: históricas→nuevas TEO (75) | Mediana H: otros edificios (105) | Mediana H: construcción→D/E/J (54) | Pares nuevos con ≥20 inliers H |
|---|---:|---:|---:|---:|
| SIFT | 5 | 4 | 4,5 | 2/54 |
| SIFT+LightGlue | 4 | 4 | 5 | 5/54 |
| ALIKED+LightGlue | 5 | 4 | 6 | 10/54 |
| XFeat+LighterGlue | 9 | 0 | 7 | 16/54 |

H significa homografía. El recuento ≥20 es descriptivo, no un criterio de aceptación del localizador. Dentro de una misma
serie las medianas suben a 184/239/424/199 respectivamente; mezclarlas con las consultas separadas inflaría el resultado.
Ningún método supera 20 inliers H en los 105 pares con otros edificios (máximos 6/7/10/12). Eso no equivale a probar
cero falsos en 1.000 secuencias negativas.

XFeat obtiene hasta 201 inliers entre la referencia histórica TEO-D-15m y el inicio del Xiaomi E; la visualización muestra
correspondencias sobre ventanas y barandas, pese al cambio de luz/teléfono. Entre Samsung B y D llega a 56 sobre ventanas;
entre A y J, a 75. Son ejemplos útiles para continuar con XFeat y conservar ALIKED como comparación. Las medianas
entre sesiones siguen bajas y las etiquetas de edificio no aseguran solapamiento. Las homografías pueden apoyar
coincidencias incorrectas o solo una superficie plana; no se estimaron errores en centímetros con ellas.

Los tiempos de extracción y matching están en `comparacion.jsonl`, tomados en CPU de este Mac. La caché conserva el
tiempo de extracción original. No representan carga de mapa, recuperación de referencias, pose, arranque ni rendimiento
Android; no se usa la suma de estos tiempos para afirmar el objetivo de cinco segundos.

## Ensayo de reconstrucción abierta

Se ejecutó COLMAP/pycolmap 4.2.1 con las 54 imágenes de construcción A/B, reducidas a 1024×576, SIFT de hasta 2048
puntos y matching exhaustivo CPU. PINHOLE con los intrínsecos registrados como aproximación fija; distorsión y
rectificación sin validar. Semilla 5 y opciones completas en el script. **Sin poses Unity, GPS, escala métrica ni
imágenes D/E/J.** COLMAP relajó automáticamente sus restricciones de inicialización tras fallar las primeras búsquedas.

Resultado: un modelo de 27 imágenes, todas de B, 1.238 puntos, longitud media de track 16,51 y reproyección media
0,314 px en la imagen reducida. A no se registró. La inspección de los 826 puntos observados en la imagen central de B
muestra que se concentran casi por completo en los arbustos/cubresuelo; la fachada queda sin soporte útil. La mediana
del ángulo máximo por track es 2,02°, en ese modelo estimado.

El bajo error de reproyección no rescata este mapa: reconstruye principalmente el primer plano, tiene escala arbitraria
y no permite comprobar la fachada. Se conserva como evidencia en `sfm-construccion/modelos/0/` (binario y texto COLMAP)
y `sfm-construccion/puntos-B-central.png`. No se importó como `registered_mapping`, no se le asignó
`meters-per-unit=1` ni se ejecutó PnP con umbrales en metros sobre esa escala arbitraria. Este resultado corresponde
a ese ensayo SIFT; no demuestra que sea imposible reconstruir con otro extractor o con más vistas.

## Corrección y validación del banco

Se reprodujo una omisión silenciosa: `inventario` devolvía 108 frames sin avisar que había otros 28 PNG sin filas.
`datos.inventory` ahora devuelve `unreferenced_images`, y la CLI informa cuántos se excluyeron por falta de metadatos.
No se convierten nombres de archivo en calibración. La prueba de regresión falla antes del cambio y pasa después.

Validación: 10 pruebas de base aprobadas; 11 aprobadas después del cambio. Importación real final: 108 imágenes,
0 ausentes y 28 sin metadatos excluidas explícitamente. Los cuatro métodos finalizaron sin red. No hubo cambios Unity
que justificaran recompilar o volver a ejecutar su suite completa.

## Siguiente paso concreto

1. Agotar una reconstrucción solo desde imágenes con **XFeat+LighterGlue como candidato principal y ALIKED+LightGlue
   como comparación**. Usar A/B y referencias históricas de Teología disponibles para construcción; seleccionar vistas
   desde ese conjunto. Mantener D/E como consultas y J solo como diagnóstico 2D. La evaluación ya explorada no es un
   ensayo final ciego; no incorporar sus imágenes al mapa ni ajustar umbrales con ellas para luego anunciar éxito.
2. Comprobar primero cobertura 3D de fachada, tracks multivista, paralaje y consistencia entre vistas de construcción;
   no aceptar un modelo dominado por vegetación. Mantener escala arbitraria explícita mientras falten controles.
   Si las fotos/cámaras del dron del PC ya existen, aprovecharlas como otra fuente de construcción cuando estén
   accesibles; no son un requisito para iniciar el ensayo con las imágenes actuales. En este Mac no se encontraron
   `Imgs/` ni cámaras COLMAP de esos vuelos. No convertir PLY de Immersal en un mapa con descriptores inexistentes.
3. Una vez obtenida geometría de fachada útil, registrar la reconstrucción con controles físicos y comprobarla con otros reservados;
   validar orientación/sincronía óptica por teléfono. Solo entonces ejecutar mapa→PnP→multicuadro en las consultas
   separadas y medir su error físico. Si el ensayo con lo disponible sigue sin producir geometría de fachada, la siguiente
   captura necesaria sería una trayectoria de construcción con traslación y solapamiento, distinta de los barridos de
   consulta de cinco segundos. Documentar el fallo y un protocolo concreto antes de pedir nuevas capturas.
4. Llevar los candidatos que superen esa verificación a Android y medir el proceso completo en los tres teléfonos.
   Los datos actuales no permiten elegir definitivamente el motor ni dar por cumplidos 10–20 cm, ≤5 s o cobertura 10–25 m.

Scripts locales reproducibles: `preparar_datos.py`, `auditar.py`, `comparar.py`, `reconstruir.py` y
`visualizar_matches.py` bajo `Tools/LocalizacionOffline/salida/20261105/`. `reconstruir.py` rechaza reutilizar una base
existente; para repetir el ensayo se debe conservar la salida anterior y usar otra carpeta de experimento.
