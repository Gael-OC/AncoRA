# Tablero en casa, segunda entrega — 5-nov-2026

**Resultado:** esta entrega mejora claramente la captura y permite una **calibración candidata útil para ensayos en PC del Xiaomi 15T**, con parámetros más estables y comprobación en dos carpetas fuera del ajuste. **No se necesita repetir la serie completa ahora.** No equivale todavía a aceptar las cámaras del mapa, la sincronía ni precisión en centímetros. La [primera entrega](LocalizacionOffline-CalibracionCasa-2026-11-05.md) se conserva como diagnóstico histórico.

## Medidas y material recibido

Gael informó ahora «12 de largo y 12 de ancho», como continuación de la medición de **cinco cuadrados consecutivos**: se registran 120 mm / cinco = **24 mm por cuadrado en ambos ejes**. Son medidas informadas por el usuario, no inferidas del PDF ni de las fotos. No indicó instrumento, incertidumbre ni repetición; la planitud de la hoja sigue sin certificación. No interpretar esos 12 cm como el tamaño total del tablero de 10 × 7 cuadrados.

Raíz original intacta: `/Users/gael/Downloads/tablero calibración 2/`.

- `grabaciones/`: **11 carpetas, 298 PNG de 1920 × 1080 y 11 JSONL**, 27 cuadros por sesión salvo `231403` con 28. Cada imagen tiene exactamente una fila y todos los archivos referenciados existen; no hay PNG sobrantes ni hashes de imágenes duplicados. Todos identifican **Xiaomi 25069PTEBG (15T)**, Android 16, app 1.5.3, build `a7b6d56522944330a40389cc20ce7cb4`, gris, `Portrait`, `SessionTracking`, sin reducción del plugin. No se recibió Xiaomi 14 ni Samsung.
- Registro exportado: 103 JPG pequeños, tres logs y `telefono.txt`; ZIP de los mismos **107 archivos**, CRC correcto y todos los SHA-256 iguales a sus archivos sueltos. Incluye los registros anteriores; no son tres nuevos teléfonos ni 103 originales adicionales para calibrar. El ajuste solo usa PNG con JSONL.
- `procedencia.json`: tamaño/hash inicial de los **418 archivos** recibidos (353.401.032 bytes, incluidos ZIP y `.DS_Store`), comprobados de nuevo al terminar. ZIP SHA-256: `02611a6a498f8e391949e029eef881bc2c018fce19c106055b31128c7b5889de`.
- Log nuevo `paseo-20261105-231150.log`: cita los once inicios y `config=1920x1080@30`. En varios `GRABACION_FIN` el contador dice 26 aunque la carpeta final tiene 27 PNG y 27 filas válidas. Se conserva la discrepancia: el contador al detener no reemplaza la auditoría final; es compatible con escrituras pendientes, no evidencia de un PNG faltante en la entrega.

La K registrada es constante en estas 298 filas: **fx 1255,0188; fy 1255,34167; cx 968,141357; cy 538,539001 px**. Es ligeramente distinta de la primera entrega (1251,72/1252,73/970,67/538,05); no se impuso la K anterior al segundo conjunto ni se presupuso que una calibración sea universal por modelo de teléfono. No hay metadatos de foco/exposición, identificador físico de lente o distorsión. Las etiquetas `CB-A-10m`/`sombra` se mantienen como etiquetas originales del grabador, sin usarlas como ubicación o medida real.

## Revisión visual y separación de sesiones

Se detectaron **54 esquinas en 298/298 imágenes**. Se revisaron todos los tableros mediante once láminas de recortes y el encuadre de cinco ordinales por carpeta (55 imágenes completas). Los PNG se analizaron sin giro; solo las láminas se giran 90° horario para verlas. Hay mucho mejor tamaño, nitidez y distribución que en la primera entrega. Alguna transición está movida, por ejemplo `231353/0004`; no se eliminó a posteriori para mejorar cifras. No se observa patrón recortado en las vistas completas revisadas; las esquinas detectadas están dentro del sensor en todos los cuadros. Las fotos no permiten certificar planitud submilimétrica.

Se verificó el orden indicado por Gael con las imágenes. Hay **seis** carpetas después de las cinco iniciales; dos muestran perspectivas laterales del mismo signo, lo que explica una captura más que en su enumeración aproximada. No se fuerza una etiqueta inventada para cada movimiento del operador.

| Orden / inicio / sufijo | Observación visual | Partición fijada antes de detectar o ajustar |
|---|---|---|
| 1 / 23:12:24 / `559777b6` | Centro, casi frontal | Ajuste |
| 2 / 23:12:47 / `e2c2e724` | Superior izquierda de pantalla | Ajuste |
| 3 / 23:13:05 / `1fe4459b` | Inferior derecha | Ajuste |
| 4 / 23:13:21 / `03970d20` | Superior derecha | Ajuste |
| 5 / 23:13:38 / `5bb36caf` | Inferior izquierda | **Reservada periférica** |
| 6 / 23:13:53 / `edd1045d` | Perspectiva lateral, algo más lejos al principio | Ajuste |
| 7 / 23:14:03 / `a7af10fd` | Perspectiva lateral del mismo signo, más cerca; 28 PNG | Ajuste |
| 8 / 23:14:17 / `6b2aa2ab` | Perspectiva lateral opuesta | Ajuste |
| 9 / 23:14:30 / `9fafc813` | Perspectiva vertical marcada | Ajuste |
| 10 / 23:14:41 / `edde6de5` | Inclinación vertical opuesta, más moderada | Ajuste |
| 11 / 23:14:55 / `b7031199` | Centro, más cerca | **Reservada central** |

El polígono de esquinas interiores ocupa **2,24–8,95 %** del área por imagen (mediana 5,32 %), frente a 1,13–2,11 % antes. La unión de esquinas va de x = **252–1773** e y = **102–897** en sensor crudo 1920 × 1080; ya cubre regiones laterales ausentes antes. La periferia extrema y algunas regiones entre grupos siguen sin observación: el rectángulo envolvente no significa cobertura continua. La normal estimada con K registrada llega a ~42°, con perspectivas laterales de ambos signos y verticales. Son ángulos ópticos estimados suponiendo papel plano, no medidas externas.

**Nueve sesiones de ajuste, dos reservadas completas**: se eligieron nueve ordinales equiespaciados entre las detecciones de cada sesión de ajuste, **81 imágenes para estimar K/distorsión**. Las **54 imágenes** de las dos carpetas reservadas no participaron en ajuste, sensibilidad ni exclusión de imágenes. Las restantes 163 imágenes de las carpetas de ajuste no se presentan como validación independiente. No se mezcló la primera entrega en el nuevo ajuste. Las reservas se fijaron tras la inspección de encuadre inicial y antes de la detección/optimización; son comprobación separada del ajuste, no evaluación final ciega. Comparar modelos con ellas las convierte en validación de desarrollo.

## Calibración y comprobación

Mismos modelos y método que antes: `findChessboardCornersSB`, cuadrícula 9 × 6, OpenCV `calibrateCameraExtended` con K registrada como inicio, geometría 24 × 24 mm. Se ajustaron PINHOLE, radial k1 y Brown 5; no se añadieron modelos a partir del resultado reservado. Para comprobar, cada imagen reservada estima **solo su pose con 27 esquinas alternadas** y evalúa RMS euclídeo con las otras 27, con K/distorsión fijas.

| Modelo | fx / fy (px) | cx / cy (px) | RMS ajuste (px) | Reservada periférica: mediana / p90 RMS (px) | Reservada central: mediana / p90 RMS (px) |
|---|---|---|---:|---:|---:|
| Registrado, distorsión cero como hipótesis | 1255,02 / 1255,34 | 968,14 / 538,54 | — | 0,648 / 0,678 | 0,379 / 0,464 |
| PINHOLE | 1303,25 / 1299,86 | 970,00 / 535,82 | 0,550 | 0,657 / 0,685 | 0,335 / 0,431 |
| Radial k1 | **1297,30 / 1295,83** | **962,00 / 535,43** | **0,491** | **0,453 / 0,472** | **0,372 / 0,450** |
| Brown 5 | 1304,56 / 1302,56 | 980,02 / 535,47 | 0,469 | 0,446 / 0,466 | 0,330 / 0,388 |

El radial da **k1 = 0,0612693**, con k2/k3/p1/p2 fijos en cero. Su focal es +3,37 % / +3,22 % frente a la registrada de esta entrega. Al retirar cada una de las nueve sesiones de ajuste, fx queda **1295,18–1299,29**, fy **1293,74–1298,58**, cx **958,27–965,89**, cy **533,15–536,37 px**, y k1 **0,05416–0,06612**. Es mucho más estable que el primer ensayo, cuyo k1 variaba 0,00062–0,03849 con el muestreo disponible entonces. Los dos ajustes usan datos distintos; no se presenta esa diferencia como medida física de la lente.

Brown da `[k1,k2,p1,p2,k3] = [-0,007495,0,405215,-0,000839,0,005879,-0,575195]`. En la rejilla del sensor su derivada radial mínima es **+0,467**, frente a la negativa del primer Brown: este nuevo ajuste no muestra aquel pliegue radial en la rejilla comprobada. Eso no certifica que la inversión sea válida en todo punto ni que los bordes no observados estén calibrados. Los términos altos aún cambian al quitar sesiones (k3 −0,700 a −0,512; cx 973,58–991,02 px). La deformación directa máxima extrapolada sobre el sensor es ~49 px para radial y ~35 px para Brown: **son predicciones, no correcciones verificadas en esos extremos**.

**Decisión:** conservar radial k1 como candidato simple y estable para ensayos de cámara en PC, y Brown como sensibilidad. Hay evidencia de que la hipótesis PINHOLE registrada sin distorsión deja más residuo en la región periférica comprobada. No hay motivo para aplicar automáticamente la K ajustada a todas las imágenes históricas ni a Android: faltan control de foco/configuración, validación óptica más amplia y la comprobación del mapa. No se modificaron las K de construcción ni se rectificó ningún mapa. Los resultados numéricos completos están en `ajuste/modelos.json`; no se creó una falsa calibración de otro teléfono.

Los errores más grandes que en la primera entrega no significan peor captura: ahora el tablero es mayor y ocupa zonas nuevas, donde el primer ensayo no comprobaba nada. Un residuo subpíxel de un plano, incluso reservado, no mide pose absoluta en centímetros; la pose estimada puede absorber parte del error de K. Persisten posibles efectos de planitud, impresión, foco, movimiento y modelo óptico. No se identifica uno de ellos como causa única.

Como **diagnóstico retrospectivo**, se aplicaron los nuevos modelos a las detecciones guardadas de los 189 PNG anteriores, sin releer esos PNG ni ajustar K/distorsión de nuevo. Mediana/p90 RMS con mitad de esquinas reservadas para residuo: registrado propio de cada imagen 0,214/0,299 px; radial nuevo 0,207/0,286; Brown nuevo 0,205/0,278. No aparece una degradación grande en la zona central anterior. Esas imágenes ya eran conocidas y no constituyen evaluación ciega ni comprueban cámaras del edificio.

## Orientación y poses: avance y límite

La base candidata previa `B = [[0,-1,0],[-1,0,0],[0,0,1]]`, equivalente a `[-y_unity,-x_unity,z_unity]`, vuelve a ser la de menor discrepancia entre las 24 bases de cambio de mano probadas en cada sesión. Con K registrada y distorsión cero como hipótesis, las discrepancias de rotación relativa separadas por tres cuadros son **0,28–0,79° de mediana por sesión** y **0,49–1,61° de p90**. Son mejores que la oscilación movida anterior, sin atribuir la mejora exclusivamente a sincronía. El diagnóstico es condicional a tablero fijo, cuya posición respecto de la pared parece conservarse visualmente; no hay confirmación instrumental de inmovilidad.

Los cierres condicionales de traslación de tablero frente a Unity dan 0,0021–0,0060 m de mediana por sesión. **No son precisión física en milímetros:** ambas poses son estimaciones y comparten hipótesis de tablero/ejes/escala. Unity no es verdad de terreno ni se ajustó la escala del papel para hacerla coincidir. La conversión óptica sigue como candidata, sin integrar.

Los tiempos de sensor crecen con intervalos 166–200 ms, mediana ~199 ms. `timestamp_pose_sensor == timestamp_sensor` en **298/298** filas; el grabador sigue copiando tiempo de cámara y leyendo Unity en `LateUpdate`. No hay sello independiente de pose, foco, exposición ni lectura por filas. **Sincronía no validada**, sin corrección de desfase. La nueva entrega se compone de posturas bastante estables, no de una pareja identificada de movimiento suave/rápido comparable. Si el próximo trabajo exige multicuadro en movimiento, hay que instrumentar el tiempo real de pose y comprobar la captura; no pedir por rutina otra serie completa de tablero ni prometer resolverlo solo con más PNG.

## Primera entrega restaurada: comprobación directa y ensayo conjunto

Gael devolvió la primera entrega a su ruta original. Se verificaron sus 270 rutas: **los 269 archivos de captura/exportación son idénticos por SHA-256** a los recibidos inicialmente; solo cambió `.DS_Store`, auxiliar de Finder. Los 189 PNG y siete JSONL se copiaron y verificaron en una **salida nueva** `salida/20261105-calibracion-restaurada/copia-primera/grabaciones/`, para reproducir este ensayo sin depender de mover otra vez la carpeta de Descargas. Ningún original fue editado, movido o borrado. La segunda entrega conserva sus 418 hashes.

Se repitió `findChessboardCornersSB` sobre los **189 PNG restaurados**, no solo sobre el JSON anterior: 54 esquinas en todos, metadatos idénticos y **diferencia máxima de coordenadas 0,000 px** frente a la detección guardada, con el mismo orden. También se revisó la nueva lámina de encuadres completos por sesión. Las observaciones anteriores sobre tamaño pequeño y movimiento siguen vigentes; no faltaban imágenes ni había un resultado obsoleto de caché.

Se ensayó además un ajuste conjunto **sin tocar ninguna reserva original**: las tres sesiones de ajuste de la primera entrega más las nueve de la segunda, nueve ordinales por sesión = **108 imágenes para ajustar**. Las cuatro sesiones reservadas de la primera y las dos de la segunda = **162 imágenes de comprobación**, por sesión y hash separados. Los 487 PNG de las dos entregas son distintos. Ninguna consulta o imagen del edificio intervino. Las reservas ya son datos de desarrollo conocidos, no evaluación final ciega. La medida 24 × 24 mm se aplica como la confirmación de Gael sobre el tablero usado; incertidumbre y planitud siguen sin certificar.

El ensayo conjunto supone provisionalmente una misma óptica entre las dos entregas; sus K registradas difieren ligeramente y no hay metadatos de foco que demuestren equivalencia. Como control de software, la comparación de intrínsecos **registrados** toma K de cada imagen, sin imponer a la segunda entrega el prior de la primera. Esta corrección del comparador no altera las tablas anteriores de cada entrega, donde K era constante.

| Radial k1 | Solo segunda entrega | Conjunto de las dos |
|---|---:|---:|
| fx / fy px | 1297,301 / 1295,834 | 1297,602 / 1296,027 |
| cx / cy px | 962,001 / 535,434 | 962,208 / 535,518 |
| k1 | 0,061269 | 0,060456 |
| RMS de ajuste px | 0,4912 | 0,4334 |

La máxima diferencia entre los cuatro intrínsecos radiales es **0,301 px**. No interpretar el RMS menor del conjunto como mejora de precisión: se cambió la mezcla de imágenes y se añadieron tableros más pequeños, que suelen dar residuos absolutos menores. Al retirar una de las 12 sesiones de ajuste, fx conjunto queda 1295,55–1299,48 px y k1 0,05334–0,06510. PINHOLE y Brown también cambian menos de 0,38 px en sus cuatro intrínsecos respecto del ajuste de la segunda entrega. El conjunto aporta una prueba de estabilidad, no una nueva calibración física absoluta.

Comprobación con la misma regla de mitad de esquinas para pose y mitad para residuo, **medianas RMS en px por carpeta reservada**:

| Inicio reservado | K registrada propia, D=0 | Radial de segunda | Radial conjunto |
|---|---:|---:|---:|
| 22:54:11 | 0,2137 | 0,1987 | 0,1984 |
| 22:54:23 | 0,1923 | 0,1856 | 0,1864 |
| 22:54:49 | 0,2686 | 0,2649 | 0,2643 |
| 22:55:02 | 0,2890 | 0,2676 | 0,2679 |
| 23:13:38, periférica nueva | 0,6477 | 0,4525 | 0,4530 |
| 23:14:55, central nueva | 0,3787 | 0,3722 | 0,3674 |

Las diferencias entre usar radial de segunda o conjunto son pequeñas y no constituyen una mejora física demostrada. **Se conserva el radial de la segunda entrega como candidato base; el conjunto queda como sensibilidad reproducible.** La primera entrega sí sirve para comprobar compatibilidad con vistas más pequeñas y con otro momento de captura. No justifica desplazar automáticamente las cámaras del mapa ni usar Unity como control.

Se repitió la comparación de movimiento con la base candidata fija anterior, ahora con K/distorsión de la segunda entrega. En `22:54:49`, los desacuerdos angulares mediana/p90 son **5,97°/7,58°** con K registrada sin distorsión, **6,00°/8,35°** con radial nuevo y **5,86°/8,34°** con Brown nuevo. Por tanto, **la discrepancia no desaparece al recalibrar la lente**. La sesión permanece como caso de diagnóstico útil; no se borra por su desenfoque. No se puede distinguir todavía entre tiempo de pose/exposición, movimiento de tablero, rolling shutter, tracking o error de PnP. El tablero fijo sigue siendo condición visual no confirmada instrumentalmente; no se estimó retraso ni se validó sincronía a partir de timestamps iguales. La base de ejes se mantuvo fija, no se eligió otra para reducir este error.

Salida: `salida/20261105-calibracion-restaurada/`: `restauracion.json` guarda auditoría y rutas de copia; `primera-redetectada/` conserva detecciones nuevas; `conjunto/` manifiesto y partición; `ajuste-conjunto/` modelos/sensibilidades; `comparacion/reservadas.json` y `movimiento-con-calibracion.json` guardan resultados completos. No se sobrescribieron las salidas previas. El caso de prueba sintético y la comprobación de K por imagen con ambos priors pasan. No hacen falta más fotos generales del 15T por esta revisión.

Para reproducir tras disponer de la copia verificada en una **salida nueva** (los directorios de salida deben no existir):

```sh
PY=Tools/LocalizacionOffline/.venv/bin/python
T=Tools/LocalizacionOffline
R=$T/salida/20261105-calibracion-restaurada
$PY $T/experimentos/calibrar_casa_20261105.py --source "$R/copia-primera" --output "$R/primera-redetectada" --square-m-y 0.024
$PY $T/experimentos/comparar_tablero_restaurado_20261105.py preparar --run "$R"
$PY $T/experimentos/ajustar_tablero_20261105.py --source "$R/conjunto" --output "$R/ajuste-conjunto"
$PY $T/experimentos/comparar_tablero_restaurado_20261105.py comparar --run "$R"
```

`R` indica la estructura del ensayo ejecutado; para repetir, elegir otro nombre, copiar allí `copia-primera` (o usar la raíz restaurada como `--source`) y no reutilizar los directorios de salida existentes. El comparador conserva las bases de ambas entregas y no vuelve a estimarlas con las reservas.

## Qué sigue y reproducción

Para Gael: **guardar estas carpetas; no repetir ahora los once barridos del 15T**. La medida del segundo eje y las vistas cercanas/periféricas ya están aportadas. Si se incorpora Xiaomi 14, necesitará captura/calibración propias. Sigue pendiente la medición física distribuida y reservada de Teología según la [hoja de visita](LocalizacionOffline-Visita-Teologia.md); el tablero no mide el edificio ni corrige por sí solo sus cámaras inestables o su escala arbitraria. No se demostró 10–20 cm ni desempeño Android.

**Disponibilidad actualizada tras restaurar:** las dos entregas vuelven a estar presentes. Los 269 archivos relevantes de la primera coinciden con el inventario inicial; solo `.DS_Store` cambió. Los 418 de la segunda siguen idénticos. La ausencia de las rutas de la primera documentada al cierre anterior quedó resuelta; la sección nueva detalla la redetección desde los PNG y la copia verificada para reproducirla.

Salida nueva `Tools/LocalizacionOffline/salida/20261105-calibracion-casa-2/`: `analisis/` conserva procedencia, partición y detecciones; `ajuste/` selección, modelos, sensibilidades y poses; **`revision-final/`** láminas de todos los tableros, cobertura, extrapolación y transferencia retrospectiva. `revision-visual/` es la salida previa a añadir transferencia, conservada. `previa.jpg` muestra un encuadre por carpeta. Los scripts ejecutados están copiados junto a sus resultados. Las fuentes compartidas solo se ampliaron para admitir reservas explícitas/segunda medida, comprobar resolución y suficientes detecciones, tolerar detecciones ausentes en revisión y comparar la entrega previa. Las instantáneas del primer ensayo permanecen intactas.

Para repetir en una **salida nueva** desde raíz:

```sh
PY=Tools/LocalizacionOffline/.venv/bin/python
T=Tools/LocalizacionOffline
R=$T/salida/repeticion-calibracion-casa-2
$PY $T/experimentos/calibrar_casa_20261105.py --source '/Users/gael/Downloads/tablero calibración 2' --output "$R/analisis" --heldout-session barrido-20261105-231338-5bb36caf --heldout-session barrido-20261105-231455-b7031199 --square-m-y 0.024
$PY $T/experimentos/ajustar_tablero_20261105.py --source "$R/analisis" --output "$R/ajuste"
$PY $T/experimentos/revisar_tablero_20261105.py --run "$R" --previous-run "$T/salida/20261105-calibracion-casa" --output "$R/revision-final"
```

Mismo entorno local instalado: OpenCV 5.0.0, NumPy 2.5.4, SciPy 1.18.1 y Pillow 13.0.0. Se comprobaron integridad, hashes, separación 9/2 por sesión, independencia por hash respecto de la primera entrega, PnP sintético ideal <0,001 px y proyección de distorsión cero. Compilación sintáctica Python y revisión de espacios de los archivos tocados aprobadas. Son controles de software, no validación física. Los originales de esta entrega, mapas y consultas no fueron modificados; ambas entregas de tablero fuera del mapa. Sin red, adb, recompilar APK, commits, push, cambio de rama o worktree.
