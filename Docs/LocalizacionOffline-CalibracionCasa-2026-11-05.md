# Tablero en casa — revisión del 5-nov-2026

**Resultado:** siete barridos íntegros del **Xiaomi 15T (25069PTEBG)**, 189 PNG, tablero completo y 54 esquinas detectadas en todos. Las posiciones e inclinaciones distintas sí aportan información. Se obtuvo una calibración **provisional**, con cuatro sesiones completas fuera del ajuste. La cobertura periférica, la estabilidad de parámetros y la segunda medida del papel no permiten aceptar todavía una calibración para todo el sensor. No cambiar las cámaras del mapa ni aplicar los coeficientes exploratorios.

## Material realmente recibido

Raíz leída, sin modificar: `/Users/gael/Downloads/probar tablero calibración/`.

- `grabaciones/`: siete carpetas `barrido-…`, cada una con 27 PNG de 1920 × 1080, gris, y un `intentos.jsonl` con 27 filas. Todos los archivos referenciados existen; no hay PNG sin fila, nombres repetidos ni hashes PNG duplicados. El orden de sensor es creciente en cada JSONL. Todas las filas identifican `Xiaomi 25069PTEBG`, Android 16, app 1.5.3, build `a7b6d56522944330a40389cc20ce7cb4`, `SessionTracking`, `Portrait`, sin reducción por plugin. No aparece Xiaomi 14 ni Samsung en estas capturas.
- `AncoRA-registro-Xiaomi-25069PTEBG-20261105-225519/`: `telefono.txt`, dos logs (14:26 y 22:52) y 69 JPG de **270 × 480**: 36 bajo `imagenes/20261105-142634` y 33 bajo `imagenes/20261105-225222`. La inspección de muestras distingue campus previo y casa; son previsualizaciones del registro, no otros 69 originales calibrables con JSONL. No se mezclaron con los PNG ni con el mapa. La vista de estas muestras tampoco convierte consultas previas en evaluación ciega.
- ZIP del mismo registro: CRC correcto y sus 72 miembros coinciden por SHA-256 con los 72 archivos sueltos. SHA-256 del ZIP: `2528786f07a81a32b6981c11f818dc8dc14356c8b5fd9e14d5aa5fd35db9788e`.
- `procedencia.json` conserva tamaño y SHA-256 de los **270 archivos** recibidos, incluidos ZIP y `.DS_Store` (195.925.680 bytes en total). Se volvieron a comprobar sus hashes después del análisis.

El log de las 22:52 cita los siete inicios recibidos. El mensaje inicial `config=640x480@30` no describe las dimensiones de los PNG posteriores: estos y sus JSONL son coherentes en 1920 × 1080. No se deduce de ese mensaje una identidad física de lente ni una calibración a 640 × 480. No hay identificador de lente, exposición, foco, rolling shutter ni coeficientes de distorsión en las filas. El modelo de teléfono no sustituye esos campos faltantes.

Las etiquetas `CB-A-10m`/`sombra` y `barrido-arcore` quedaron seleccionadas en el grabador. El encargo y las imágenes identifican este conjunto como **calibración en casa**, sin atribuirle distancia de 10 m ni pertenencia a CB. No se importó al inventario de construcción ni al de consultas del mapa.

## Papel y revisión visual

Se renderizó el PDF Carta y se comprobó la cuadrícula de **10 × 7 cuadrados / 9 × 6 esquinas interiores**. Gael informó **12 cm en cinco cuadrados**, por tanto **24 mm por casilla en la dirección medida**. No consta qué eje físico midió ni una segunda medición perpendicular, instrumento o incertidumbre. Las medidas nominales del PDF no son mediciones de la impresión. En la aclaración posterior, Gael confirmó variaciones de posición y pidió inspección visual, sin aportar otra longitud.

Se revisaron las 189 vistas del tablero mediante siete láminas de recortes, además de 35 imágenes completas distribuidas en los barridos. No se observa recorte del patrón ni oclusión de esquinas. Hay buen contraste y cuadros con bordes nítidos, alternados con imágenes movidas. Detectar esquinas no equivale a certificar nitidez o planitud. La hoja parece apoyada en la pared; las fotos no certifican que no tenga ondulaciones. Los recortes se giraron 90° horario **solo para verlos**; detección, ajuste y errores usan los PNG crudos sin giro.

| Inicio / sufijo de carpeta | Qué muestran las imágenes | Nitidez y uso en este ensayo |
|---|---|---|
| 22:53:33 / `a902c8f3` | Vista más centrada y frontal al inicio; desplazamiento hacia el final | Mayormente nítida, poca inclinación; ajuste |
| 22:53:47 / `84a16195` | Tablero sube/baja y cambia de lado al variar el punto de vista | Vistas útiles; 0011–0012 y 0017–0018 muestran movimiento; ajuste |
| 22:54:00 / `4c729a83` | Aproximación lateral, perspectiva y giro progresivo | Aporta inclinación; movimiento en 0008 y 0022, entre otros; ajuste |
| 22:54:11 / `5b9773d1` | Cambios verticales/laterales más amplios, algo más cerca | Inicio 0001–0003 movido, también vistas nítidas como 0004; reservada |
| 22:54:23 / `597ba4e9` | Giro fuerte del encuadre en ambos sentidos y cambios de perspectiva | Nitidez variable; 0019–0021 y 0026–0027 movidas; reservada |
| 22:54:49 / `6da67480` | Cambios rápidos y oscilación del tablero en el encuadre | Movimiento muy evidente, especialmente 0010–0018 y 0022–0023; reservada, diagnóstico |
| 22:55:02 / `a4eb8789` | Vista lateral y giro; alterna quietud con movimiento | Hay vistas nítidas; 0007 y 0014, entre otras, movidas; reservada |

La descripción es visual, no una asignación inventada de los roles que pretendía el operador. La pared y el soporte parecen conservar el tablero, pero Gael **no confirmó expresamente** que estuviera fijo durante cada barrido. Las conclusiones de movimiento quedan condicionadas a ello.

El polígono de las esquinas interiores ocupa solo **1,13–2,11 %** del área de la imagen por cuadro; una arista de casilla promedia 23,7–33,0 px según la imagen. Hay suficientes píxeles para detectar el patrón, pero no suficiente extensión sobre el sensor para determinar su distorsión periférica. Entre todas las sesiones, las esquinas quedan en x = 565–1340 e y = 45–914 sobre 1920 × 1080. **No se observó el ~29 % izquierdo ni el ~30 % derecho del sensor crudo**; las cuatro esquinas periféricas están sin cubrir. En pantalla vertical esto afecta particularmente los extremos superior e inferior. En las tres sesiones de ajuste la cobertura es aún menor: x = 615–1334, y = 182–890. Hay variación de inclinación; con K registrada y tablero supuesto cuadrado/plano, la normal estimada llega a ~32°. No faltan todas las inclinaciones: falta combinar vistas cercanas, inclinadas y periféricas nítidas.

## Ajuste y comprobación separados

Antes de detectar esquinas o ajustar se guardó `analisis/particion.json`: primeras tres sesiones para ajuste, últimas cuatro reservadas. No se cambiaron los grupos al ver los errores. De cada sesión de ajuste se tomaron nueve ordinales equiespaciados (1, 4, 7, 11, 14, 17, 21, 24 y 27): **27 imágenes para estimar intrínsecos; 108 imágenes de otras cuatro carpetas para comprobarlos**. Las otras 54 imágenes de las tres primeras sesiones no se presentan como validación independiente. No se filtraron a posteriori las imágenes movidas para mejorar el resultado.

OpenCV `findChessboardCornersSB`, patrón (9, 6), normalización de imagen; coordenadas originales. Tres modelos fijados antes de comprobar: PINHOLE con cuatro intrínsecos libres, radial con solo k1, y Brown de cinco coeficientes. Todos parten de K registrada; la geometría supone casillas cuadradas y planas de 0,024 m. La segunda medida falta: las traslaciones son provisionales y un estiramiento desigual del papel puede sesgar la calibración. Un cambio de escala uniforme no cambia K, pero una relación de ejes errónea sí.

| Modelo | fx / fy (px) | cx / cy (px) | RMS de ajuste (px) | Reservadas: mediana / p90 de RMS (px) |
|---|---|---|---:|---:|
| Registrado, distorsión cero como hipótesis | 1251,72 / 1252,73 | 970,67 / 538,05 | — | 0,252 / 0,308 |
| PINHOLE estimado | 1271,81 / 1268,22 | 964,11 / 541,03 | 0,163 | 0,246 / 0,325 |
| Radial k1 | 1289,00 / 1286,09 | 965,98 / 539,51 | 0,161 | 0,241 / 0,321 |
| Brown 5 | 1299,86 / 1297,00 | 986,28 / 525,75 | 0,158 | 0,241 / 0,316 |

La comprobación de la tabla estima **solo la pose por imagen con 27 esquinas alternadas** y evalúa RMS euclídeo sobre las otras 27, con K/distorsión fijas. También se guardó el residuo usando las 54 para pose, pero no se confunde con esquinas reservadas. Son vistas independientes del ajuste de K, del mismo entorno/dispositivo y ya inspeccionadas; no una prueba final ciega ni una medida de error en centímetros. Ajustar pose de un plano puede absorber errores de K: residuos pequeños en este patrón pequeño no validan todo el campo visual.

La focal PINHOLE estimada cambia +1,60 % / +1,24 % frente a la registrada; al excluir de ajuste una de las tres sesiones, fx varía **1261–1304 px**. Con radial k1 la estimación completa es k1 = 0,02621, pero al quitar una sesión varía **0,00062–0,03849**. Brown completo da `[k1,k2,p1,p2,k3] = [-0,01633, 0,64423, -0,00382, 0,00577, -1,96221]`; al quitar una sesión, cy varía **463–586 px**. No hay estabilidad suficiente para reemplazar K o afirmar distorsión nula.

**Brown no es una corrección utilizable:** al extrapolar sus coeficientes sobre la rejilla de todo el sensor, el desplazamiento directo llega a ~581 px y la derivada radial mínima es −3,16: el modelo se pliega fuera de la zona observada. El inversor `undistortPoints` devuelve solo ~17 px de desplazamiento máximo en otra rejilla; ese número por sí solo ocultaría el problema de inversión. La revisión directa está en `revision-visual/distorsion-extrapolada.json`. No se generaron imágenes rectificadas para el mapa ni se seleccionó Brown por su menor residuo. El modelo radial simple extrapola hasta ~21 px, tampoco medidos en esos bordes. La comparación no prueba si ARCore entrega imagen previamente rectificada.

## Orientación, poses y tiempo

K es idéntica en las 189 filas; `pantalla=Portrait`, `rot_pantalla` identidad y `orientacion_imagen=0`. La matriz display se conserva y no se aplicó a los PNG. Visualmente un giro horario de 90° deja vertical el texto y la escena; eso no significa que se deba girar el buffer antes de usar K.

Diagnóstico condicional: con tablero fijo y K registrada sin distorsión, se estimó pose óptica por imagen y se compararon movimientos separados por tres cuadros (~0,6 s) con las rotaciones Unity. Se probaron las 24 bases ortogonales de ejes con determinante −1; la misma base obtuvo el menor error mediano en las siete sesiones:

```
[x_cv, y_cv, z_cv] = [-y_unity, -x_unity, z_unity]
B = [[0,-1,0],[-1,0,0],[0,0,1]]
```

Es una **hipótesis de ejes encontrada con estos datos**, no una conversión integrada ni validada en otro teléfono. Se compara `Rcv_j Rcv_iᵀ` con `B (Ru_jᵀ Ru_i) Bᵀ`; no se comparan cuaterniones crudos como si compartieran convención. El cambio de mano y el giro de sensor son coherentes con la presentación observada. La identidad simétrica del tablero, el tamaño pequeño, desenfoque, planitud y K provisional limitan PnP.

| Inicio | Desacuerdo angular mediano / p90 | Cierre de traslación mediano condicional |
|---|---:|---:|
| 22:53:33 | 0,78° / 1,71° | 0,019 m |
| 22:53:47 | 1,18° / 2,26° | 0,014 m |
| 22:54:00 | 0,43° / 2,14° | 0,015 m |
| 22:54:11 | 1,34° / 2,35° | 0,027 m |
| 22:54:23 | 2,07° / 3,51° | 0,032 m |
| 22:54:49 | **5,97° / 7,58°** | **0,058 m** |
| 22:55:02 | 1,77° / 2,74° | 0,017 m |

El cierre de traslación compara `Cj−Ci` del tablero con `Riᵀ B Ruiᵀ (pj−pi)` de Unity, suponiendo unidades y forma del papel correctas. **Son discrepancias internas entre dos estimaciones, no precisión física ni validación de escala Unity.** No se ajustó escala a Unity ni se la usó como verdad. La base se eligió exploratoriamente con las propias sesiones; las cifras tampoco son validación independiente de esa selección. La sesión 22:54:49 presenta el desacuerdo más claro y desenfoque fuerte; estos datos no permiten atribuirlo exclusivamente a retraso, tracking, rolling shutter o error óptico.

Los intervalos de sensor son 166–200 ms (mediana ~199 ms), 27 muestras en ~5 s. `timestamp_pose_sensor == timestamp_sensor` en **189/189** filas, pero el código copia el tiempo del cuadro (`frameReceived`) al campo de pose y lee Unity en `LateUpdate`. No hay sello independiente del instante de pose, ni exposición/lectura de filas. No se estimó ni corrigió un desfase físico ni se declaró sincronía validada. El multicuadro sigue pendiente de instrumentación y comprobación óptica, especialmente durante movimiento rápido.

## Qué captura concreta falta

Conservar todo este material: **no hace falta repetir los siete barridos completos** ni regresar al campus para el tablero.

1. **Medir el papel en el eje perpendicular** al ya medido: cinco casillas contiguas, anotar ambos extremos, valor, eje, regla e incertidumbre razonable. Repetir las dos direcciones; no asumir que la segunda da 12 cm. Confirmar soporte plano. Si hay estiramiento desigual u ondulación, corregir el tablero antes de capturar.
2. **Xiaomi 15T, nuevas vistas para ajuste:** con buena luz, misma cámara/configuración sin zoom, acercarse hasta que el tablero sea mucho mayor (como pauta de encuadre, ~la mitad del ancho visible, siempre completo y con margen). Hacer tres barridos lentos: (a) cerca y centrado, frontal y luego inclinado arriba/abajo; (b) tablero cerca de las zonas superior izquierda e inferior derecha de la pantalla; (c) cerca de superior derecha e inferior izquierda, con perspectiva lateral real de ~20–40°. Mover el teléfono alrededor del tablero fijo; girarlo sobre sí mismo no sustituye las inclinaciones de perspectiva. Mantener cada postura ~0,5–1 s para guardar cuadros nítidos. No cortar ninguna esquina. La finalidad concreta es cubrir periferia y tamaños que faltan en los siete barridos, no acumular más vistas pequeñas centrales.
3. **Cuarto barrido nuevo reservado:** otras posiciones cercanas y periféricas; guardarlo como `COMPROBACION`, separado por carpeta y sin usarlo para ajustar K ni elegir parámetros. Los cuatro barridos actuales de comprobación ya son diagnóstico conocido; conservarlos, no convertirlos en prueba ciega nueva.
4. **Para poses:** repetir específicamente la oscilación rápida de **22:54:49**, con tablero inequívocamente fijo y buena luz; hacer primero otro barrido suave comparable. Mantener el patrón completo y más grande, evitar que todos los cuadros sean barridos. Anotar `MOVIMIENTO_SUAVE`/`MOVIMIENTO_RAPIDO` con sus carpetas. Esto puede revelar inconsistencias, pero el grabador actual sigue sin sello independiente de pose; una repetición no garantiza resolver sincronía.
5. **Xiaomi 14:** no está entregado. Si se quiere habilitarlo, necesita su propia serie de ajuste, comprobación y movimiento. No transferirle K, distorsión ni la base de ejes del 15T. No se solicita Samsung por asumir que participó.

El siguiente paso de Teología sigue siendo medir controles físicos distribuidos y reservados, resolver las cámaras inestables y validar escala/forma. El tablero en casa no proporciona medidas del edificio, no escala su SfM y no demuestra 10–20 cm.

## Salida y reproducción

Salida nueva, ignorada por Git: `Tools/LocalizacionOffline/salida/20261105-calibracion-casa/`. Contiene procedencia, partición, esquinas, selección de ajuste, modelos por imagen, sensibilidades, comparaciones de movimiento y láminas. `analisis/contactos.jpg` muestra el encuadre completo de cinco ordinales por sesión; `revision-visual/esquinas-*.jpg` muestra **todos** los tableros; `revision-visual/cobertura.png` muestra las esquinas sobre el sensor crudo (naranja ajuste, azul reservado). Los recortes, gráficos y render son derivados nuevos; los originales siguen en Descargas. Los archivos preliminares fuera de `revision-visual/` se conservan, no sustituyen originales.

Desde la raíz, con una **salida distinta que aún no exista**:

```sh
PY=Tools/LocalizacionOffline/.venv/bin/python
T=Tools/LocalizacionOffline
R=$T/salida/repeticion-calibracion-casa
$PY $T/experimentos/calibrar_casa_20261105.py --source '/Users/gael/Downloads/probar tablero calibración' --output "$R/analisis"
$PY $T/experimentos/ajustar_tablero_20261105.py --source "$R/analisis" --output "$R/ajuste"
$PY $T/experimentos/revisar_tablero_20261105.py --run "$R" --output "$R/revision-visual"
```

OpenCV 5.0.0, NumPy 2.5.4, SciPy 1.18.1, Pillow 13.0.0, entorno local ya instalado. Los scripts rechazan sobrescribir salidas; conservan una copia propia junto al resultado. Se ejecutaron verificaciones de integridad, separación por sesión/hash, recuperación PnP de un plano sintético ideal (<0,001 px) y distorsión cero sintética. Esas verificaciones prueban operaciones del análisis, no óptica real. No se cambió Unity, APK, mapas, escenas, configuración ni particiones de consultas; no se usó adb, red, GPS o Immersal como referencia; no se hizo commit/push ni se cambió de rama.
