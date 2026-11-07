# Nuevos recorridos de construcción — 5-nov-2026

**Los recorridos sí mejoran la geometría: con más vistas intermedias, XFeat pasa de una solución muy inestable a otra bastante más consistente al quitar el primer plano.** Esa mejora no valida por sí sola todas las cámaras ni un mapa métrico. Persisten discrepancias y la unión con las referencias históricas sigue siendo inestable. El siguiente paso es consolidar el candidato construido solo con las capturas nuevas y revisar sus cámaras/enlaces discrepantes, antes de pedir otra salida general.

## Material recibido y conservación

Se revisaron íntegramente `Descargas/ancora ultimas grabacoines/` y las dos exportaciones sueltas Xiaomi del 5-nov indicadas en el encargo. También se buscaron sesiones del 5-nov en el resto de Descargas: solo aparecieron adicionalmente los barridos de la mañana ya tratados en el informe anterior. No se espera otro envío.

La salida nueva es `Tools/LocalizacionOffline/salida/20261105-recorridos/`, ignorada por Git. `procedencia.json` registra ruta original, copia, tamaño y SHA-256 de **2.392 archivos (4.897.793.499 bytes)**. `carpetas.json` conserva el inventario de directorios, incluidos vacíos. Las copias se hicieron con `copy2`, sin escribir en los originales. **7 ZIP, 2.303 miembros no directorio**: todos leídos hasta EOF, con CRC y SHA-256; todos sus contenidos coinciden por hash con archivos extraídos. No había imágenes adicionales recuperables solo desde ZIP.

Hay **2.195 contenidos de archivo únicos** y 144 grupos de exportaciones duplicadas; los ZIP y las versiones históricas se conservan. Las **1.900 imágenes PNG de captura son todas distintas por SHA-256**. Los 431 JPG pertenecen a exportaciones de diagnóstico, con duplicados: no se convierten en imágenes calibradas de construcción. Los 14 archivos `.log` incluyen copias históricas/duplicadas, además de los registros actuales de ambos Xiaomi.

Las dos exportaciones sueltas dejaron de existir en sus rutas iniciales mientras se ejecutaba el análisis: 85 rutas originales ya no estaban al volver a comprobarlas. Sus copias ya estaban verificadas y se conservaron. Un `.DS_Store` cambió externamente; se conservaron ambas versiones. `verificacion-fuentes.json` distingue esos cambios de procedencia de la comprobación satisfactoria de todas las copias contra sus hashes iniciales. Ningún archivo de captura fue modificado por este trabajo.

## Integridad y roles reales

Hay **31 sesiones, 1.900 filas y 1.900 PNG**. En las carpetas entregadas: cero imágenes referidas ausentes, cero PNG sin metadatos, cero filas repetidas por archivo y numeración completa. No se inventaron intrínsecos, tiempos ni poses. Los roles se dedujeron del `tipo_captura` consistente de cada sesión completa; teléfono, versión 1.5.3 y letra no deciden el rol. No se encontró una sesión con rol ambiguo ni colisión de hash entre construcción y consultas.

| Edificio | Sesiones construcción / PNG | Sesiones consulta / PNG |
|---|---:|---:|
| Teología | 5 / 580 | 4 / 107 |
| Ciencias Básicas | 3 / 348 | 10 / 270 |
| X1 | 4 / 462 | 5 / 133 |

El Xiaomi 14 (`23127PN0CG`) hizo seis recorridos y siete consultas. El 15T (`25069PTEBG`) hizo seis recorridos y doce consultas. Teología tiene construcción B/C/A del 14 y B/A del 15T; la captura C del 14 se mantiene como construcción porque así está registrada. Los barridos C/D/E del 15T, incluida la repetición D, permanecen fuera del mapa.

**Exportación parcial respecto del log del Xiaomi 14:** faltan las carpetas completas `barrido-20261105-144109-0aab1f63` (X1-B) y `barrido-20261105-144144-b7820fef` (X1-A). El log informa 27 y 28 guardadas al detener; ese contador todavía podía tener escrituras pendientes y no prueba el total final. No están en ninguno de los ZIP ni en las demás carpetas de Descargas. Se documentan como sesiones citadas sin carpeta; no se inventan imágenes ni se espera recuperarlas para continuar.

## Cámara, tiempos, seguimiento y calidad

Todos los PNG decodifican como gris de un canal, **1920×1080**, y coinciden con sus dimensiones registradas. Los intrínsecos son constantes dentro de cada teléfono y distintos entre ellos:

| Teléfono | fx | fy | cx | cy |
|---|---:|---:|---:|---:|
| Xiaomi 14 | 1305,60535 | 1310,67615 | 964,520752 | 546,497986 |
| Xiaomi 15T | 1267,00427 | 1267,75354 | 954,810242 | 540,56073 |

Los 12 recorridos contienen 115–116 imágenes, duran **59,554–59,948 s entre primera y última imagen** y alcanzan **1,912–1,926 imágenes/s**, no exactamente 2. El mayor intervalo registrado es 0,594 s. Las 19 consultas contienen 26–28 imágenes, duran **4,787–4,967 s** y alcanzan 5,062–5,531 imágenes/s. Son datos medidos, no los límites de interfaz. No se detectaron huecos >1 s en estos recorridos.

Las 1.900 filas declaran `SessionTracking`, una época por sesión y `pose_optica_validada:false`. Los tiempos de sensor y pose son iguales en las filas, pero las diferencias entre intervalos de sensor y captura llegan a unos 108 ms: esto no valida sincronía óptica. No se usaron poses Unity, GPS, etiquetas de distancia ni resultados Immersal para reconstruir, filtrar o dar escala.

Los JSONL de `142714-fd5922d6` (TEO-E) y `143046-0f8a173d` (X1-C) contienen dos filas fuera de orden por finalización asíncrona. Los índices y tiempos de sensor permiten ordenar las filas existentes, sin rellenar ni cambiar sus valores. La auditoría guarda la anomalía de orden original y calcula cadencias ordenando `captura_t`; los originales permanecen intactos. El código del grabador confirma escrituras concurrentes y que `GRABACION_FIN` se emite antes de drenar la cola; por eso varios contadores del log son menores que el número final de PNG/filas.

La revisión visual usa siete posiciones ordinales por sesión y estadísticas sobre todas las imágenes. Se ven cambios de punto de vista y esquinas: las nuevas capturas sí añaden traslación visual útil, aunque no se le atribuye una longitud métrica. Hay encuadres de suelo, una obstrucción corporal en `0039-TEO-A-10m` del recorrido `143053`, paredes poco texturadas, barandas, lámparas y ventanas repetidas. En CB `143137-dba0f5a1`, cinco imágenes tienen >10 % de píxeles ≥253; la vista central está parcialmente tapada y saturada. La causa física no se deduce del histograma. No se usa la varianza de Laplaciano para declarar automáticamente desenfoque en una pared lisa.

![Consultas: una vista central por sesión](../Tools/LocalizacionOffline/salida/20261105-recorridos/consultas-muestras.jpg)

Inspeccionar las consultas para integridad/encuadre no las incorpora al matching. Estas 19 sesiones quedan reservadas como consultas de diagnóstico, con **510 hashes excluidos** de cada manifiesto de construcción. No se ejecutó PnP, ajuste multicuadro ni tuning con ellas; su inspección tampoco se presenta como una prueba final ciega.

## Reconstrucción de Teología

Se reutilizaron `sfm_aprendido.py`, `sfm_diagnostico.py`, pesos y cachés locales. Los procesos de matching y COLMAP se ejecutaron por separado y con sockets bloqueados. Se conserva el prior PINHOLE registrado fijo, hasta 2.048 características, lado máximo 1.024, semilla 5 y verificación geométrica a 4 px originales. No se refinaron focales ni distorsión. Opciones, versiones, matches con índices originales, bases, cámaras, puntos, logs y máscaras se conservan por ensayo.

La comparación principal usa **las mismas 35 vistas**, siete posiciones ordinales por cada una de las cinco sesiones completas de construcción. No se escogieron por éxito geométrico; incluyen las vistas de suelo/obstrucción. Se compararon XFeat+LighterGlue y ALIKED+LightGlue, completos y filtrados con las mismas regiones manuales de fachada. Las regiones son conservadoras y parciales; lo no anotado es desconocido, no vegetación. No son segmentación ni verdad de terreno independiente.

Además se ejecutaron dos sensibilidades: **65 vistas** (13 ordinales por sesión, imágenes intermedias del mismo material), y **84 vistas** (las 49 de construcción del ensayo anterior + 35 nuevas). En la unión se copiaron y verificaron las 49 imágenes previas; se reutilizaron los 1.771 pares internos compatibles y se calcularon solo los 1.715 pares cruzados, conservando exactamente sus índices. La unión sigue la partición histórica documentada: no incorpora las consultas de la mañana reservadas ni las nuevas. Los barridos históricos usados como referencias no cambian por su nombre de carpeta.

**Todos los modelos tienen escala arbitraria.** Los recuentos siguientes no acreditan edificio correcto, estabilidad ni metros:

| Ensayo | Modelo | Cámaras | Puntos 3D | Fachada / entre sesiones | Reproyección media original |
|---|---:|---:|---:|---:|---:|
| XFeat, 35 completo | 0 | 32 | 4818 | 2371 / 1995 | 1.293 px |
| XFeat, 35 fachada | 0 | 30 | 2152 | 2152 / 1824 | 1.248 px |
| ALIKED, 35 completo | 0 | 32 | 2995 | 1077 / 947 | 1.145 px |
| ALIKED, 35 fachada | 0 | 8 | 301 | 301 / 295 | 1.057 px |
| ALIKED, 35 fachada | 1 | 29 | 967 | 967 / 869 | 1.085 px |
| XFeat, 65 completo | 0 | 62 | 11319 | 5483 / 4158 | 1.336 px |
| XFeat, 65 fachada | 0 | 58 | 5824 | 5824 / 4462 | 1.329 px |
| ALIKED, 65 completo | 0 | 62 | 7467 | 2309 / 1809 | 1.179 px |
| ALIKED, 65 fachada | 0 | 55 | 2330 | 2330 / 1844 | 1.200 px |
| XFeat, unión 84 completo | 0 | 81 | 9831 | 4828 / 4282 | 1.394 px |
| XFeat, unión 84 fachada | 0 | 78 | 3973 | 3973 / 3554 | 1.469 px |

En la revisión final de máscaras (v2), XFeat de 35 vistas y solo fachada tiene 2.152 puntos, 1.824 con observaciones entre sesiones, longitud mediana de track 3 y ángulo máximo mediano estimado 32,84°. El completo tiene 2.371 tracks etiquetados fachada y 2.447 desconocidos. En 65 vistas de fachada, XFeat tiene 5.824 puntos (4.462 entre sesiones), track mediano 3 y ángulo máximo mediano 33,52°; ALIKED tiene 2.330 puntos (1.844 entre sesiones). Las pequeñas regiones de vegetación no etiquetan ningún track por mayoría: esto no demuestra ausencia de vegetación en lo desconocido.

La cobertura por cuadrícula 12×8, las proyecciones y las sesiones por track están en `diagnostico.json`, `tracks-diagnostico.json` y `soporte-sesiones.json`. Se ven puntos en ventanas, marcos, juntas y aleros; grandes superficies lisas siguen con poco apoyo. Ni el número de celdas ni el ángulo calculado desde el propio modelo validan cobertura física o paralaje real independiente.

![Construcción de Teología: siete vistas por recorrido](../Tools/LocalizacionOffline/salida/20261105-recorridos/TEO-construccion.jpg)

### Estabilidad y módulos repetidos

Las comparaciones eliminan la libertad de similitud con Sim(3) sobre centros comunes. Las rotaciones se miden relativas a una cámara ancla explícita. Los porcentajes son del diámetro de cámaras del modelo de referencia; **no son centímetros**. Los JSON `consistencia-v2-*.json` y `fuentes/comparar_v2.py` conservan fuentes, anclas y comandos.

La inspección ampliada de proyecciones detectó una lámpara/persona/tablero parcialmente incluidos en una región. Se ampliaron las exclusiones de dos vistas y se repitieron las cinco reconstrucciones filtradas en salidas nuevas `*-fachada-v2`. **La tabla y las cifras finales de este informe usan esa revisión**, igual para ambos métodos; las máscaras y modelos v1 quedan conservados como preliminares. Los modelos completos no cambian; sus diagnósticos semánticos se recalcularon con v2, conservando los anteriores.

- **35 vistas, XFeat completo/fachada v2:** 30 cámaras comunes, centros 2,60 % de mediana y p90 6,42 %. Las rotaciones medianas por recorrido son 2,12° / 10,96° / 0,72° / 2,65° / 2,19°, con una cámara que cambia 77,24°. ALIKED de fachada devuelve componentes de 8 y 29 cámaras, que pueden compartir imágenes pero no se aceptan como un único marco. El componente de 29 cámaras discrepa de XFeat hasta 30,88° en una cámara; el pequeño tiene discrepancias mucho mayores. Hay sensibilidad y cámaras problemáticas pese a la baja reproyección.
- **65 vistas, XFeat completo/fachada v2:** 58 cámaras comunes; centros **0,342 %** de mediana, p90 0,737 %, máximo 1,019 %. Rotaciones medianas por recorrido 0,769° / 1,153° / 0,488° / 2,005° / 1,438°, máximo individual 3,62°. El modelo de fachada conserva **58 cámaras / 5.824 puntos**, con apoyo entre las diez parejas de recorridos. Es una mejora clara sobre la selección más separada.
- **65 vistas, ALIKED completo/fachada v2:** un componente de fachada de **55 cámaras / 2.330 puntos**. Centros 0,196 % de mediana; rotaciones medianas por recorrido 0,295–0,792°, máximo individual 2,57°. La fragmentación de la selección de 35 vistas no persiste en esta condición.
- **XFeat/ALIKED 65 de fachada v2:** 55 cámaras comunes, centros **0,445 %** de mediana y p90 0,782 %. Rotaciones medianas por recorrido 0,817° / 1,375° / 0,416° / 1,712° / 1,349°. Pero `0001-TEO-B-10m` de `143744` discrepa **13,51 %** del diámetro en posición y 5,96° en rotación. Al inicio de `142828` quedan diferencias de hasta 4,45°. Estos casos impiden aceptar todas las cámaras por la mediana. Tener más de cien observaciones en una vista tampoco basta para validarla.
- **Sensibilidad al muestreo:** XFeat 35/65 de fachada v2 comparte 30 cámaras; centros 1,76 % de mediana y hasta 11,25 %. La sesión `142828` cambia una mediana de 9,24° y un máximo de 32,16°. Entre los modelos completos, `0116-TEO-C-15m` de `142828` cambia 79,53°: otro caso concreto para revisar con sus imágenes intermedias.
- **Unión de 84 vistas:** completo/fachada v2 comparte 78 cámaras, con centros 6,35 % de mediana. La histórica B cambia 38,40°; C/D unos 11°; hay cambios individuales cercanos o superiores a 99°. El núcleo Samsung B + históricas E/F se mantiene mucho más consistente. Frente al modelo anterior de fachada, la histórica B cambia 5,22° de mediana, pero alguna cámara de A de la mañana cambia 105,40°: no basta esa mediana para afirmar que se resolvió la ambigüedad.
- **Sensibilidad a la corrección de máscaras:** v1/v2 de 65 vistas cambia poco en la mayoría de cámaras (centros medianos 0,143 % XFeat y 0,071 % ALIKED). En la unión de 84, la misma corrección provoca 11,02 % de mediana en centros y **51,34°** en la histórica B. Esa inestabilidad reproduce el problema global anterior y justifica mantener las históricas fuera del candidato principal nuevo.

Las láminas de tracks eligen por regla fija el menor, el mediano y los tres mayores ángulos entre tracks con ≥4 observaciones y ≥3 sesiones. Son muestras para revisar identidad de detalles entre ventanas y esquinas; un ángulo alto o una marca sobre una ventana no demuestra por sí solo una correspondencia falsa. Las discrepancias geométricas requieren esa revisión, sin adjudicar automáticamente cada fallo a falta de fotos.

![Fachada densa v2: XFeat arriba, ALIKED abajo; cian indica observaciones de puntos reconstruidos](../Tools/LocalizacionOffline/salida/20261105-recorridos/fachada-densa-comparada-v2.jpg)

Las tres vistas se fijaron por posición ordinal central de los recorridos `142623`, `142828` y `143744`, sin seleccionarlas por éxito. Son proyecciones sobre imágenes de construcción, no poses comprobadas físicamente ni consultas localizadas. Las paredes lisas conservan huecos; más puntos sobre ventanas repetidas no prueban identidad física ni escala. Algunas regiones también incluyen arquitectura visible al fondo: la clase fachada no acredita por sí sola la identidad del edificio de cada punto. Esta es otra razón para no tratar CB/X1 automáticamente como negativos puros.

## Ciencias Básicas y X1

Exploración posterior con XFeat a los mismos parámetros: CB, 21 vistas de tres recorridos; X1, 28 vistas de cuatro recorridos. En CB aparecen componentes de **17 cámaras / 2.422 puntos** y **3 / 146**. En X1, **12 / 1.638**, **8 / 823** y **3 / 144**. Los componentes pueden compartir alguna cámara y no se suman como cobertura única ni como un marco común.

Hay desplazamiento y distintos ángulos aprovechables. CB tiene repetición de ventanas altas, bancos/mesas y postes; X1 muestra muchas paredes lisas, sillas/mesas y la carpa de PACE. Las proyecciones de X1 contienen abundantes puntos en ese mobiliario. Estos ensayos no tienen filtrado semántico ni contraste de estabilidad: todas sus etiquetas de diagnóstico se dejan como desconocidas. **No son mapas validados de fachada** ni una comparación final con ALIKED. Conservan material para el siguiente análisis una vez resuelto Teología.

Las consultas de CB/X1 no se clasifican automáticamente como negativos puros de Teología: se revisaron muestras y se debe comprobar la secuencia completa y los detalles efectivamente incluidos en el mapa antes de esa evaluación. No se publicó una tasa de falsas aceptaciones con estas imágenes.

## Decisión y lo que falta realmente

**Consolidar primero el candidato de Teología construido con las 65 vistas nuevas, sin forzar su unión con las históricas.** Revisar las cámaras discrepantes, usando las imágenes intermedias de los 580 PNG disponibles y vecindad temporal por recorrido; auditar enlaces sobre detalles distintivos, sectores y cierre de vuelta. Repetir la sensibilidad de fachada/método sobre ese candidato. Los ensayos densos de este informe siguen usando pares exhaustivos: no prueban todavía un grafo restringido por continuidad temporal. La concordancia mayoritaria entre XFeat y ALIKED permite focalizar la revisión en cámaras concretas; todavía no autoriza umbrales métricos. Después de revisar esos enlaces, la validación de cámara y controles físicos es el siguiente requisito, sin convertir estos porcentajes en error respecto del edificio.

No pedir otra repetición general por falta de material. Solo si ese análisis identifica un enlace que no esté visualmente cubierto corresponde especificar ese tramo concreto. Los dos barridos X1 ausentes no bloquean Teología. Para pasar a mapa métrico sí faltan **controles físicos distribuidos y controles independientes reservados**, además de validar intrínsecos/distorsión y orientación/sincronía óptica por teléfono. No pueden recuperarse del nombre «10 m», de Unity, GPS o Immersal.

No se importó ningún modelo al localizador con umbrales en metros. No se ejecutaron PnP/multicuadro reales ni se eligió motor Android. Los objetivos de alineación 10–20 cm, uso a 10–25 m y arranque ≤5 s siguen sin demostrarse. El tiempo de matching CPU no se usa como latencia móvil.

## Reproducción, pruebas y límites de software

El [README](../Tools/LocalizacionOffline/README.md) contiene los comandos. `auditar_recorridos.py` implementa copia verificada, lectura de ZIP, auditoría de imágenes/sesiones y particiones; reutiliza `datos.inventory`. `experimentos/conectar_recorridos.py` reutiliza extractores, pesos, caché y matches compatibles; `experimentos/evidencia_recorridos.py` genera soporte entre sesiones y láminas de observaciones. Las máscaras están versionadas junto a los experimentos. Fuentes exactas, scripts de selección/comparación, firmas y manifiesto de pesos quedan en `fuentes/` con SHA-256.

**15 pruebas Python aprobadas**, incluidas copia distinta rechazada, ZIP sin extracción insegura, roles según metadatos, filas desordenadas, imágenes huérfanas, exclusión de sesiones/hashes, importación externa, reconstrucción sintética y comparación invariante a similitud. Son pruebas de herramientas, no validación física.

Hubo un intento de reconstrucción iniciado antes de terminar el matching: falló por un NPZ aún ausente. La base parcial y el log se conservan en `incidente-entrada-incompleta/` y `teo-xfeat-sfm.log`; el ensayo válido usó una base nueva después de completar los 595 pares. No se mezclaron sus resultados. Los demás ensayos conservan las relajaciones de inicialización de COLMAP en sus logs; conseguir un componente no supera por sí mismo un criterio geométrico.

No se recompiló el APK ni se modificaron assets o configuración Unity. Se trabajó en `localizacion-offline`, sin reset, restauración al commit anterior, commits ni coautorías. El control final contrasta los hashes de los archivos locales preexistentes; solo `Estado-y-Plan` y el apéndice autorizado del README cambian entre ellos. Sus versiones previas se conservan en `fuentes/antes/`.

## Detalle de las 31 sesiones

`C-` abrevia `construccion-20261105-` y `Q-`, `barrido-20261105-`. Duración entre primera/última captura; etiquetas de distancia sin valor métrico.

| Sesión | Teléfono | Etiqueta | PNG/filas | Duración s | Imágenes/s |
|---|---|---|---:|---:|---:|
| Q-143503-67a9668c | 23127PN0CG | CB-B-10m | 27 | 4.932 | 5.272 |
| Q-143517-cc358999 | 23127PN0CG | CB-C-15m | 27 | 4.810 | 5.405 |
| Q-143541-ccd24c96 | 23127PN0CG | CB-D-15m | 27 | 4.967 | 5.235 |
| Q-143558-deca1ae7 | 23127PN0CG | CB-E-20m | 27 | 4.898 | 5.308 |
| Q-143619-ca165231 | 23127PN0CG | CB-F-25m | 26 | 4.787 | 5.222 |
| Q-144019-264b1149 | 23127PN0CG | X1-D-15m | 27 | 4.867 | 5.342 |
| Q-144044-a72e1cd2 | 23127PN0CG | X1-C-15m | 27 | 4.831 | 5.382 |
| C-142623-6336f9e6 | 23127PN0CG | TEO-B-15m | 116 | 59.732 | 1.925 |
| C-142828-6c2403ae | 23127PN0CG | TEO-C-15m | 116 | 59.755 | 1.925 |
| C-143053-41f02d6d | 23127PN0CG | TEO-A-10m | 116 | 59.697 | 1.926 |
| C-143347-0b3f62cc | 23127PN0CG | CB-A-10m | 116 | 59.853 | 1.921 |
| C-143726-2e569f3b | 23127PN0CG | X1-F-15m | 116 | 59.760 | 1.924 |
| C-143858-9461b9c3 | 23127PN0CG | X1-E-20m | 116 | 59.748 | 1.925 |
| Q-142714-fd5922d6 | 25069PTEBG | TEO-E-15m | 27 | 4.913 | 5.292 |
| Q-142754-7dcd6160 | 25069PTEBG | TEO-D-15m | 27 | 4.860 | 5.349 |
| Q-142805-ab7d3279 | 25069PTEBG | TEO-D-15m | 26 | 4.797 | 5.211 |
| Q-142846-6d86bb85 | 25069PTEBG | TEO-C-15m | 27 | 4.908 | 5.297 |
| Q-142923-f25f7812 | 25069PTEBG | X1-C-10m | 27 | 4.799 | 5.418 |
| Q-143006-5a92f4d4 | 25069PTEBG | X1-D-10m | 26 | 4.874 | 5.129 |
| Q-143046-0f8a173d | 25069PTEBG | X1-C-10m | 26 | 4.938 | 5.062 |
| Q-143137-dba0f5a1 | 25069PTEBG | CB-C-10m | 27 | 4.881 | 5.326 |
| Q-143145-b4e964ac | 25069PTEBG | CB-C-10m | 28 | 4.882 | 5.531 |
| Q-143200-c973df76 | 25069PTEBG | CB-C-10m | 27 | 4.867 | 5.342 |
| Q-143336-b2bb8b06 | 25069PTEBG | CB-D-10m | 28 | 4.922 | 5.486 |
| Q-143420-5d69ca26 | 25069PTEBG | CB-E-10m | 26 | 4.810 | 5.198 |
| C-143459-f58c2426 | 25069PTEBG | CB-A-10m | 116 | 59.850 | 1.921 |
| C-143617-5a0a278d | 25069PTEBG | CB-B-10m | 116 | 59.834 | 1.922 |
| C-143744-192263eb | 25069PTEBG | TEO-B-10m | 116 | 59.948 | 1.918 |
| C-143857-256706aa | 25069PTEBG | TEO-A-10m | 116 | 59.735 | 1.925 |
| C-144030-c0f997b4 | 25069PTEBG | X1-A-10m | 115 | 59.631 | 1.912 |
| C-144200-c7ca4f25 | 25069PTEBG | X1-B-10m | 115 | 59.554 | 1.914 |
