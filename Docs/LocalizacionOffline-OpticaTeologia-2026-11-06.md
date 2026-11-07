# Teología: sensibilidad nueva con la calibración del 15T — 6-nov-2026

**Ejecutado en PC: 16 reconstrucciones nuevas. La calibración candidata no mejora de forma fiable la geometría del edificio.** Hay pequeñas mejoras de reproyección, pero también cámaras desplazadas/invertidas, fragmentación y correspondencias entre ventanas distintas. No se acepta un mapa, no se aplica escala y no se ejecuta una localización real con umbrales métricos sobre él.

Salida nueva: `Tools/LocalizacionOffline/salida/20261106-optica-teologia/`. Los modelos anteriores, características, imágenes, consultas y tablero se conservan. Rama `localizacion-offline`, HEAD `f84256f`, dos commits remotos pendientes según la referencia local; sin cambiar rama, worktree, reset, commit, push, pull, red, Unity, APK o adb.

## Representación de cámara y comparación

Se revisó `PaseoSequenceCapture`: PNG gris R8, dimensiones originales, `Transformation.None`; la matriz display se guarda pero no se aplica al PNG. K se escala a la resolución del buffer. Unity se lee en `LateUpdate` y `timestamp_pose_sensor` sigue copiando tiempo de cámara; no acredita sincronía.

La modificación mínima está en `sfm_aprendido.py`: admite distorsión Brown explícita `[k1,k2,p1,p2,k3]` por frame. Usa `PINHOLE` si D=0, `OPENCV` para radial k1 y `FULL_OPENCV` para Brown con k3 y denominador radial nulo. Todos los intrínsecos/distorsión permanecen fijos en verificación/mapper/BA. La agrupación conserva teléfono, sesión, resolución, modelo y parámetros. No se combina calibración entre teléfonos.

**No se rectifican las imágenes ni se modifican sus coordenadas.** Se copian exactamente las características y correspondencias existentes de fachada v2; se vuelve a estimar geometría epipolar y reconstrucción desde bases nuevas. La suma +0,5 se aplica a keypoints y centro principal al entrar a COLMAP, conservando rayos también con distorsión. Las máscaras originales siguen siendo compatibles porque no cambian píxeles.

La calibración procede exactamente de `20261105-calibracion-casa-2/ajuste/modelos.json`, con SHA-256 en `procedencia.json` y copia compacta de K/D en `calibracion-usada.json`. Se usa la K completa, no solo la focal:

- Radial: fx 1297,301334251563; fy 1295,833795649715; cx 962,000710142634; cy 535,434170656056; k1 0,061269316204976006; restantes cero.
- Brown: K y los cinco coeficientes completos se copian del mismo archivo, sin redondearlos en el cálculo.
- Solo `Xiaomi 25069PTEBG`, 1920×1080: 26/65 o 28/71 frames cambian. Los 39/65 o 43/71 del Xiaomi 14 mantienen exactamente K registrada y la hipótesis D=0. No se calibra el Xiaomi 14 por transferencia.

La K registrada del 15T en el edificio era fx/fy 1267,00427/1267,75354, distinta de la de casa. La transferencia absoluta de K/D a la misma resolución es **una hipótesis de sensibilidad**, no prueba de foco, recorte o lente física idénticos. No hay metadatos suficientes para aceptar esa equivalencia.

Cada método compara sus propias correspondencias sobre las mismas selecciones de 65/71 vistas, pesos, 2.048 características, lado 1.024, máscaras y exclusiones originales. Baseline = K registrada/D=0. Semilla 5, cuatro hilos y límite 300 s, mismas opciones que las reconstrucciones conservadas. No se retiró ninguna cámara de las entradas para mejorar resultados. `reutilizacion.json` registra hashes por archivo; `procedencia.json` conserva fuentes y exclusiones por sesión/hash.

## Resultados de reconstrucción

Reproyección media sobre píxeles originales; **no mide exactitud física**. Cada componente tiene su propia escala arbitraria; no se suman como un único mapa.

| Vistas / método | Condición | Cámaras / puntos por componente | Reproyección media px |
|---|---|---|---:|
| 65 XFeat | Baseline nuevo | 58 / 5.795 | 1,313 |
| 65 XFeat | Radial | 58 / 5.839 | 1,341 |
| 65 ALIKED | Baseline nuevo | 55 / 2.330 | 1,200 |
| 65 ALIKED | Radial | 55 / 2.365 | 1,190 |
| 71 XFeat | Baseline nuevo | 63 / 6.393 | 1,337 |
| 71 XFeat | Radial | 63 / 6.446 | 1,319 |
| 71 XFeat | Brown | 63 / 6.176 | 1,322 |
| 71 ALIKED | Baseline nuevo | 61 / 2.758 | 1,183 |
| 71 ALIKED | Radial | **56 / 2.008 + 3 / 56** | 1,210 / 0,729 |
| 71 ALIKED | Brown | 61 / 2.738 | 1,180 |
| 65 XFeat temporal | Baseline nuevo | 58 / 5.360 | 1,328 |
| 65 XFeat temporal | Radial | 58 / 5.409 | 1,320 |

El grafo temporal conserva solo pares consecutivos dentro de cada sesión y todos los enlaces entre sesiones. Comprueba sensibilidad; no es un grafo de identidad aprobado. Ambos mantienen las mismas 65 entradas.

Se repitieron desde cero las cuatro condiciones baseline/radial de 71 vistas: **mismos conteos, incluida la fragmentación de ALIKED**. Máxima diferencia entre réplicas de centros <0,000226 % del diámetro y rotación <0,000663°. Es reproducibilidad local de estas condiciones, no precisión del mapa.

Los baselines se compararon además con los modelos históricos. Las opciones de mapper, versión pycolmap 4.2.1 y resultados de verificación de todos los pares coinciden. XFeat 71 reproduce su modelo; ALIKED 71 cambia como máximo 0,016 % del diámetro. **XFeat 65 vuelve a encontrar una solución alternativa:** `143744/0030` difiere 177,51° y 78,20 % respecto del original conservado, pese a iguales pares verificados/opciones. No se atribuye ese cambio a la calibración, porque sucede con el baseline sin calibración. Expone otra vez la ambigüedad del mapa; no se reemplaza el original por el más conveniente.

Los logs se conservan íntegros, incluidos avisos `not positive definite`: 41 en baseline XFeat 65, 101 en radial XFeat 65, 55 en radial temporal; 20/19 en baseline/réplica XFeat 71 y 15 por corrida ALIKED radial 71. Los modelos producidos no ocultan estos avisos.

## Estabilidad, conexiones e identidad

Las 21 comparaciones principales y cuatro de réplicas están en `comparaciones/` y `comparaciones-replicas/`. Se elimina la libertad de similitud usando **todos** los centros comunes; no se excluyen cámaras extremas del ajuste. Los porcentajes usan el diámetro de centros del primer modelo, no metros. Las rotaciones son relativas a `142623/0001`, como en los diagnósticos anteriores.

- **XFeat 71 radial frente a baseline:** 63 cámaras comunes, centros mediana/p90/máximo 0,989/4,534/19,948 %. `143744/0005` cambia **55,11°** y 19,95 %; su apoyo cae de 141 observaciones en 5/16 celdas a 77 en **2/16**. `143053/0020` y `/0025` cambian 12,72 % y 12,46 %. Más puntos y menor residuo no significan geometría mejor.
- **ALIKED 71 radial frente a baseline:** 56 comunes en el componente principal, mediana 6,085 %, máximo **80,549 % / 112,19°** en `143744/0049`. El segundo componente queda separado; no se presenta su residuo menor como éxito.
- **XFeat 65 radial exhaustivo frente a temporal:** `143744/0030` cambia **179,47° / 78,92 %**. En temporal tiene 116 observaciones en 4/16 celdas, 115 apoyadas por una misma sesión adicional. La nueva óptica no resuelve la inversión. `143053/0020` todavía cambia 13,70 %.
- **Entre métodos a 71 con radial:** mediana 6,059 %, máximo 80,538 %; con baseline eran 0,594 % y 17,097 %. Brown conserva componentes grandes, pero entre métodos aún llega a 16,801 % y 40,65°; no se elige Brown para esconder los problemas radiales.

`diagnostico-optico.json` por condición registra cámaras presentes, observaciones, cuadrícula 4×4, apoyo por sesión, identidad de cada observación y residuos por teléfono. Por ejemplo, en XFeat 71 la mediana residual del **15T** pasa de 1,348 a 1,364 px con radial, mientras la del **14**, cuya K no cambia, pasa de 1,370 a 1,332 px. La pequeña mejora global no demuestra que la lente del 15T haya quedado mejor modelada en el edificio; también cambian estructura y tracks.

Se inspeccionaron nueve láminas de tres muestras distribuidas por coordenada en `143053/0020`, `143744/0005` y `/0030`, más ampliaciones. **Hallazgos visuales concretos**, conservados en `revision-identidad.json`:

1. XFeat radial 71, track **3112**: `143053/0020` kp **1314**, píxel crudo (684,81; 722,31), enlazado a `142623/0001` kp **284**, (538,63; 579,81). En la primera es la ventana alta del extremo derecho, junto al edificio del fondo; en la segunda es la ventana alta próxima a la esquina, otro módulo de la misma cara. Residuos 0,666/1,188 px. [Ampliación](../Tools/LocalizacionOffline/salida/20261106-optica-teologia/identidad-xfeat-3112.jpg).
2. Track **1080**: `143053/0020` kp **1607**, `142623/0011` kp **9** y `143744/0001` kp **1219** forman un solo punto en radial. La tercera foto es la ventana alta **sobre la puerta de la cara opuesta**; las otras muestran módulos de la cara con dos columnas de ventanas. Residuos 2,563/1,047/1,142 px. [Ampliación](../Tools/LocalizacionOffline/salida/20261106-optica-teologia/identidad-xfeat-1080.jpg).
3. Como contraste, ALIKED radial track **1501**, `143053/0020` kp **961** y `142623/0001` kp **1614**, resulta visualmente compatible con la **misma** ventana alta derecha. No todas las uniones entre esas sesiones son falsas. Eso no acredita su profundidad o pose. [Ampliación](../Tools/LocalizacionOffline/salida/20261106-optica-teologia/identidad-1501.jpg).

Los dos tracks incorrectos vuelven a aparecer como un solo punto en la réplica radial; en baseline/Brown esas mismas observaciones no forman un único punto. **Corregir óptica puede cambiar qué ambigüedad se acepta**, sin arreglar identidad. Los ejemplos se seleccionaron para diagnóstico tras observar los modelos; no son evaluación ciega ni explican por sí solos todos los fallos. No se podaron esos enlaces ni cámaras para favorecer una cifra. Siguiente corrección justificada: anotar identidad física de módulos/caras y verificar los tracks cruzados antes de otra reconstrucción; una máscara genérica «fachada» no distingue ventanas repetidas.

## Cuatro medidas interiores: asociación y partición anterior a escala

Gael confirmó durante esta ejecución: todas son **anchos izquierda→derecha entre bordes internos de la ventana, sin el hormigón**. Confirmó las tres ventanas de la lámina y añadió una cuarta. No se infiere altura de cinta, repeticiones o incertidumbre.

| ID conservado/nuevo | Ventana confirmada | Ancho informado | Papel fijado para el próximo ensayo métrico |
|---|---|---:|---|
| INTERIOR-C-D | Baja junto a la puerta, panel 1; `143744/0011` | 2,70 m | **Ajuste exploratorio**, condicionado a identificar aristas/3D |
| INTERIOR-R-S | Larga baja, panel 2; `143744/0049` | 6,20 m | Reservada |
| INTERIOR-H-I | Primera baja del lateral, panel 3; `143744/0116` | 2,70 m | Reservada |
| INTERIOR-VUELTA-FRENTE | Baja a la izquierda de H–I, al otro lado de la esquina, panel 3; `143744/0116` | 1,95 m | **Nueva reserva** |

Cambio explícito: los controles originales C–D/R–S/H–I figuraban como reservados y sus círculos no equivalen a estos bordes interiores. Para el siguiente ajuste con cinta se propone y fija **INTERIOR-C-D como única longitud de ajuste**; nunca se presentará después como validación independiente. Se mantienen R–S/H–I fuera del ajuste y se suma la nueva de 1,95 m como reserva. Si C–D no tiene soporte suficiente, se registra como no ejecutable; no se absorbe una reserva silenciosamente. Esta es separación de desarrollo, no prueba final ciega.

**Ninguna escala calculada ni aplicada.** `medidas-asociacion-confirmada.json` conserva el mensaje, identificación de ventana, fuente fotográfica/hash, separación y los campos 2D/3D aún nulos. `ventanas-para-asociar.jpg` es la hoja inicial, anterior a la confirmación; su rótulo de asociación propuesta se conserva como procedencia. Las tres imágenes están registradas en los cuatro componentes principales baseline/radial de 71, pero eso **no significa que los bordes internos medidos estén triangulados correctamente**. Falta marcar las aristas interiores reales en varias fotos, verificar su identidad y comprobar sensibilidad de su triangulación con cámaras aceptables. No se usa el alero como sustituto ni se reparten los 6,20 m entre módulos.

A–B/E–F siguen sin medir por altura; G descartado. No se pide alcanzar esos puntos, otro recorrido general o repetir calibración del 15T.

## Localización y alcance del código

El mapa ya permite investigar correspondencias, construir tracks 3D y estudiar pose relativa a escala arbitraria. **No permite todavía justificar una ubicación física de 10–20 cm ni dar por aceptable su escala/forma.** Por eso no se importó como mapa métrico con `meters-per-unit=1` ni se probaron consultas como si los umbrales de 20/30 cm tuvieran sentido.

El cambio de esta sesión es **SfM**, no una integración Android o una habilitación general de distorsión en el banco: `importar-colmap` todavía rechaza OPENCV/FULL_OPENCV, triangulación/PnP/secuencia del localizador siguen bajo contrato PINHOLE rectificado/D=0. No alimentarlos con estas cámaras crudas. Para pasar a consulta real habrá que admitir K/D coherentes en importación/triangulación/PnP/proyección, incluida la conversión −0,5 del centro principal al salir de COLMAP, o rectificar referencias/consultas y volver a extraer características compatibles. La representación elegida aquí permite la primera vía sin remuestrear imágenes.

Orden para probar el motor nuevo en la app: corregir identidad de módulos y aceptar cámaras → localizar aristas medidas, ajustar solo INTERIOR-C-D y comprobar reservas → localización PC de una imagen/sesión separada con óptica coherente → comparar los cuatro candidatos de extremo a extremo → portar extractor/matcher/mapa/PnP y puente de contenido a Android y medirlos por teléfono. Multicuadro añade conversión óptica y sincronía física pendientes; los tiempos copiados, el ajuste ARCore remoto y el caso de casa con ~6° no se convierten en aceptación. La demo sigue usando Immersal.

## Reproducción y comprobaciones

Desde raíz y con una salida **que no exista**:

```sh
PY=Tools/LocalizacionOffline/.venv/bin/python
T=Tools/LocalizacionOffline
R=$T/salida/otra-sensibilidad-optica
$PY $T/experimentos/sensibilidad_optica_20261106.py preparar --output "$R"
$PY $T/experimentos/sensibilidad_optica_20261106.py reconstruir --output "$R"
$PY $T/experimentos/sensibilidad_optica_20261106.py comparar --output "$R"
$PY $T/experimentos/sensibilidad_optica_20261106.py repetir --output "$R"
$PY $T/experimentos/sensibilidad_optica_20261106.py diagnosticar --output "$R"
$PY -m unittest discover -s "$T" -p 'test_*.py' -v
```

16 pruebas aprobadas. La nueva comprobación contrasta proyección/inversión OpenCV↔COLMAP para PINHOLE, radial y Brown, la base real con +0,5 y una reconstrucción sintética con cuatro cámaras por modelo: recuperación de razones de distancias conocidas, reproyección <0,001 px y parámetros exactamente fijos. Son pruebas de software, no de precisión física. Los JSON numéricos y logs contienen las cifras sin redondear; `fuentes/` conserva la preparación inicial y `fuentes-finales/` el código final de esta ejecución. La auditoría final comprueba integridad de imágenes/características/pares/modelos anteriores, separación de consultas y que las cámaras del Xiaomi 14 no cambiaron de configuración.

`auditoria-final.json`: 201 rutas de imágenes verificadas (incluye copias de los mismos PNG, no 201 capturas únicas), 38.430 verificaciones de archivos de características/pares entre condiciones, 958 cámaras registradas comprobadas contra sus parámetros fijos y los cuatro `images.bin` fuente idénticos a sus hashes previos. Las exclusiones de consultas permanecen iguales. Los 94 archivos locales ajenos a las ediciones de esta tarea conservan sus hashes; rama y HEAD siguen iguales. Sintaxis y espacios comprobados en los siete archivos de código/documentación editados, además de `git diff --check` del README. No se atribuyen los espacios preexistentes de assets Unity a este trabajo.
