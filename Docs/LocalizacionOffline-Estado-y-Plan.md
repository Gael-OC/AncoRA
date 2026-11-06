# AncoRA offline — estado y plan de trabajo

**Pausa y respaldo autorizado (6-nov):** código y documentos en la rama `codex/respaldo-localizacion-offline-20261106`, sin integrar los dos commits remotos ni cambiar el estado local. Las capturas/resultados/pesos/entornos ignorados permanecen solo en este computador: conservarlos. [Alcance del respaldo y cómo retomar](LocalizacionOffline-RespaldoGitHub-2026-11-06.md).

Actualizado al cierre del 6-nov-2026, tras los ensayos geométricos, confirmación de aristas, revisión de Descargas y recuperación de metadatos históricos. **Este es el punto de entrada para continuar el trabajo.**
El [plan original](LocalizacionOffline-PlanOriginal.md) se conserva sin modificaciones. Los informes técnicos quedan
como evidencia; no hace falta leerlos todos para entender la siguiente tarea.

**Continuación en una sesión nueva:** el [encargo vigente del 6-nov](LocalizacionOffline-SiguienteSesion.md) contiene el plan completo, entradas exactas, límites y entregables. El [prompt listo para copiar](LocalizacionOffline-Prompt-Continuar.md) remite a ese encargo. La versión del 5-nov se conserva como [histórico](LocalizacionOffline-SiguienteSesion-2026-11-05-Historico.md), no es el plan operativo actual.

**Qué se ejecutó:** 16 ensayos ópticos y después 38 de identidad/réplicas, corrección de asociaciones físicas, confirmación fotográfica de las cuatro medidas y corrección del importador de JSONL numerados; última suite de 20 pruebas aprobadas. **Qué sigue preparado, sin ejecutar:** anotar/comparar siete vistas adicionales para pasar de 71 a 78 e inspeccionar/clasificar los 25 históricos recuperados. Después, asociación de aristas 3D, escala/comprobación y localización real si la geometría lo permite. No hay aceptación métrica ni localizador nuevo Android. La siguiente sesión debe ejecutar ese trabajo en PC, sin repetir solo el inventario o este resumen.

## Procedencia comprobada y material adicional recuperado

Las **71 vistas actuales salen de los 580 PNG de construcción de Teología de `Descargas/ancora ultimas grabacoines`**, cinco recorridos de la tarde del 5-nov: 43 seleccionadas de tres recorridos del Xiaomi 14 y 28 de dos del 15T. Son las 65 ordinales anteriores más seis intermedias; las otras 509 se conservan. `Ancora grabaciones` también está analizada, pero corresponde al material anterior de la mañana y no forma parte de esas 71. [Carpetas, sesiones y comprobación por hash](LocalizacionOffline-OrigenImagenes-2026-11-06.md).

La revisión de `Descargas/grabaciones` encontró una omisión del importador histórico: no leía `intentos (1).jsonl`. Se corrigió la lectura compartida con auditoría, deduplicación y rechazo de metadatos contradictorios. **350 PNG históricos quedan con metadatos, ninguno huérfano; 20 referencias a PNG ausentes de CB se conservan como faltantes.** Se recuperan para revisión **25 imágenes de Teología** que no estaban en el conjunto histórico usado: 24 Samsung A y una 15T E. Su rol queda **sin asignar**, sin transferir calibración ni alterar reservas. Los JPG de los registros exportados comprobados son de 270×480 y se mantienen como apoyo visual. **20 pruebas Python aprobadas.**

**Siguiente ensayo preparado:** siete PNG de construcción `143744/0002–0004` y `/0066–0067,0069–0070`, alrededor de las cámaras problemáticas `/0001` y `/0068`. Hashes y exclusiones comprobados en `salida/20261106-origen-imagenes/siete-vistas-intermedias-candidatas.json`. Falta anotar/verificar sus detalles y ejecutar el ensayo ampliado; **todavía no se reconstruyó con esas siete ni con los 25 históricos recuperados**. No hace falta pedir otra captura general para comenzar.

## Resultado más reciente: corrección de identidad de ventanas

Se implementó un filtro de identidades físicas sobre las **mismas 71 vistas y características**, con revisión visual independiente de un subagente **6.1 Terra**. Se conservan desconocidos y todas las imágenes; no se borran cámaras para mejorar cifras. La anotación final distingue ventanas superiores visibles de tres caras. **No hizo falta instalar nada, usar internet ni abrir Unity.**

Se ejecutaron **34 reconstrucciones exploratorias y cuatro réplicas finales**, conservando también resultados negativos de cuarentena y grafos. La versión final **v6** corrige conexiones de `142828/0111` a ventanas de otras caras: esa cámara deja de discrepar 94–98° entre XFeat y ALIKED **sin retirarla**. Con las 61 cámaras comunes, el desacuerdo de centros mediana/máximo pasa de ~0,73/39 % en v5 a **0,29/1,00 %** en v6 baseline, y la mayor diferencia angular baja a **3,52°**; con radial, 0,29/0,96 % y 3,45°. Son estabilidad interna tras similitud, **no centímetros ni aceptación física**. La corrección fue dirigida por el diagnóstico; no es validación independiente.

V6 registra **64 cámaras XFeat / 6.933 puntos baseline o 6.913 radial**, y **61 ALIKED / 2.749 o 2.758** en sus primeras corridas. Persisten tracks de identidad contradictoria y sensibilidad a K/D: hasta **6,10° en XFeat** entre baseline/radial. **Las réplicas confirman tres condiciones, pero XFeat radial cambia hasta 14,64 % del diámetro y 3,75° con las mismas entradas/semilla**: todavía admite soluciones distintas. Se conservan ambas. Aislar todos los componentes ambiguos consigue cero conflictos conocidos, pero rompe el edificio en mapas pequeños: ese ensayo negativo no se adopta. **Mejora el acuerdo entre métodos en la primera corrida v6, pero el mapa sigue sin aceptación métrica.**

Se guardan código, 38 condiciones, comparaciones, revisión de puentes con índices/fotos, auditoría y **19 pruebas Python aprobadas** en `Tools/LocalizacionOffline/salida/20261106-identidad-teologia/`. Las anotaciones v1–v3 fueron revisiones preliminares sin reconstruir; v4/v5/v6 y sus resultados se conservan. [Informe de la corrección ejecutada](LocalizacionOffline-IdentidadTeologia-2026-11-06.md).

**Medidas:** Gael confirmó que las líneas verdes de `aristas-interiores-propuestas.jpg` coinciden con los cuatro bordes internos medidos. `aristas-interiores-confirmadas.json` conserva esa respuesta y los segmentos en coordenadas de imagen cruda. Se identificaron aristas, no XYZ ni altura de cinta. Falta asociarlas en varias vistas con geometría aceptable; **no se calculó/aplicó escala ni se usaron reservas para elegir modelo**. El siguiente trabajo es resolver los otros puentes contradictorios y la sensibilidad restante, y preparar esa asociación 3D.

## Sensibilidad óptica anterior: calibración aplicada a Teología

Se ejecutaron **12 condiciones nuevas y cuatro réplicas** sobre las mismas selecciones de 65/71 vistas, con XFeat y ALIKED: baseline con K registrada, candidato radial y sensibilidad Brown; también sensibilidad de conexiones internas de XFeat 65. Se mantienen imágenes crudas y características/correspondencias idénticas; COLMAP admite ahora distorsión fija OPENCV/FULL_OPENCV y verifica/reconstruye desde cero. Solo cambian K/D del **15T**; el Xiaomi 14 conserva su configuración. **No se ha mejorado de forma fiable la geometría ni aceptado un mapa.**

XFeat 71 conserva 63 cámaras y pasa de 6.393 a 6.446 puntos, reproyección 1,337→1,319 px; sin embargo, `143744/0005` cambia **55,11°**, 19,95 % del diámetro y queda apoyada en solo 2/16 celdas. ALIKED 71 radial se fragmenta: baseline 61 cámaras frente a componentes de **56 y 3**, con `143744/0049` desplazada 112,19°. Ambas condiciones se reproducen en las réplicas. En XFeat 65 radial la restricción temporal todavía provoca una inversión de **179,47°**. Son diferencias internas, no metros ni errores físicos.

La inspección identifica enlaces concretos entre módulos distintos: los tracks XFeat radial **3112** y **1080** unen ventanas diferentes, incluido el enlace desde la cara con dos columnas de ventanas hacia la ventana sobre la puerta de la cara opuesta, con residuos de apenas 0,67–2,56 px. Una máscara genérica de fachada y RANSAC no evitan esa confusión. También se conserva un ejemplo ALIKED visualmente compatible; no todas las conexiones entre esas sesiones son falsas. No se retiraron cámaras ni podaron tracks para mejorar cifras. **La siguiente corrección se centra en identidad de módulos/caras y enlaces cruzados, antes de otro ajuste métrico.**

Salida nueva `Tools/LocalizacionOffline/salida/20261106-optica-teologia/`: 16 reconstrucciones, 25 comparaciones, diagnósticos por cámara/teléfono, fotografías/índices de los enlaces, procedencia y código. **16 pruebas Python aprobadas**, incluidas reconstrucciones sintéticas con distorsión y cámara fija. El [informe ejecutado](LocalizacionOffline-OpticaTeologia-2026-11-06.md) contiene baseline, límites, medidas y reproducción. Los originales siguen intactos. La localización PnP/métrica del banco y Android todavía no admiten automáticamente estos modelos crudos; no se alimentaron con una escala inventada.

## Medidas recibidas y aclaradas hoy, 6-nov

Gael informó **R–S = 6,2 m horizontal, H–I = 2,70 m y C–D = 2,70 m**, y aclaró que midió **por dentro del marco de las ventanas**, de inicio a fin. Se registraron como `INTERIOR-R-S`, `INTERIOR-H-I` e `INTERIOR-C-D`: no son automáticamente los extremos de hormigón/aleros de la lámina ni los exteriores metálicos de la pauta posterior. **A–B y E–F no se midieron por altura; G sigue descartado.**

**Aclaración de esta sesión:** Gael confirmó C–D = ventana baja junto a la puerta (panel 1), H–I = primera baja del lateral (panel 3), R–S = larga baja (panel 2). Añadió **INTERIOR-VUELTA-FRENTE = 1,95 m**, ventana baja a la izquierda de H–I al otro lado de la esquina, panel 3. Las cuatro son anchos izquierda→derecha de borde interno a borde interno, sin hormigón. Se conocen las ventanas; falta asociar sus aristas interiores en varias fotos a geometría 3D aceptable. No constan altura exacta de cinta, repeticiones o incertidumbre.

**Partición fijada antes de cualquier cálculo métrico:** INTERIOR-C-D será la única medida de ajuste exploratorio, condicionada a soporte suficiente; INTERIOR-R-S, INTERIOR-H-I e INTERIOR-VUELTA-FRENTE quedan reservadas. El cambio respecto a la antigua designación reservada C–D es explícito y solo afecta al nuevo control interior; nunca será validación independiente después de usarse para ajustar. **No se calculó/aplicó escala.** Los círculos antiguos y sus designaciones se conservan como historia; no se confunden con los bordes medidos. [Registro de medidas](LocalizacionOffline-Medidas-2026-11-06.md).

## En qué estamos y qué haremos ahora

**Estamos preparando un mapa propio y comprobable de Teología. Todavía no hay un nuevo localizador offline listo en Android.** La demo actual sigue con Immersal y el APK usado sirve para capturar. La meta original no cambió: apuntar desde 10–25 m, barrido de hasta 5 s, funcionamiento local y alineación física de 10–20 cm. Esas cifras siguen siendo objetivos, no resultados.

| Parte del trabajo | Qué tenemos | Qué falta para avanzar |
|---|---|---|
| Datos y herramientas | Banco PC con cuatro candidatos, recorridos y consultas separados; 580 imágenes nuevas de construcción de Teología | Mantener separación y usar el material existente; no repetir recorridos generales |
| Cámara del Xiaomi 15T | Candidato estable con 487 PNG; aplicado como K/D completas a una sensibilidad nueva de SfM, solo 15T | Transferencia al edificio no aceptada: no mejora fiable; PnP/localizador aún exige flujo óptico compatible |
| Forma del edificio | Filtro de identidad, 38 ensayos nuevos y reducción de la discrepancia extrema; XFeat/ALIKED v6 discrepan como máximo ~3,5° | Resolver tracks todavía contradictorios y sensibilidad óptica; no hay aceptación métrica |
| Tamaño real | Cuatro anchos y bordes interiores confirmados en fotos, partición AJ/RES; escala arbitraria | Asociar las mismas aristas en varias vistas/3D; ajustar solo C–D y comprobar las tres reservas |
| Movimiento de ARCore | Ejes candidatos y diagnóstico; un barrido movido sigue discrepando ~6° | Comprobar conversión óptica y tiempo real de pose antes de confiar en varias imágenes |
| Localización y Android | PnP/multicuadro probados con sintéticos; el APK no ejecuta todavía este nuevo motor | Probar sobre mapa/cámaras aceptables, comparar candidatos y luego integrar/medir en teléfono |

**Para qué sirvió el tablero:** ayuda a relacionar los píxeles con los rayos de la cámara y estudiar distorsión. Ya dio un candidato aprovechable del 15T. No mide Teología, no corrige por sí solo una ventana asociada a otra y no prueba que la pose Unity corresponda al instante de la foto. Por eso pasamos ahora a controles físicos y a aplicar/verificar la óptica en el banco, en vez de seguir acumulando calibraciones.

**Visita del viernes 6-nov:** ya se recibieron cuatro anchos interiores y la identificación de sus ventanas. Las aclaraciones actuales tienen prioridad sobre la [pauta de jornada anterior](LocalizacionOffline-Jornada-2026-11-06.md), que pedía otros bordes. No repetir la captura general por rutina ni pedir alturas inaccesibles: G/techo y A–B/E–F siguen descartados. Faltan documentar aristas 2D/3D, incertidumbre y repeticiones; las longitudes no equivalen a coordenadas XYZ ni precisión global.

**Siguiente trabajo en PC:** corregir los enlaces de ventanas repetidas identificados y revisar su extensión a los demás tracks; asociar aristas interiores en referencias múltiples. La sensibilidad de K/distorsión por teléfono ya está ejecutada arriba y no acepta aún el mapa. Con cámaras aceptables, ajustar escala solo con AJ y comprobar RES; después habilitar una ruta coherente K/D para consulta real de una imagen. Multicuadro depende además de pose/tiempo. Solo entonces comparar los cuatro métodos de extremo a extremo e integrar candidatos en Android. No hace falta otra captura general para iniciar esa corrección.

**Ramas revisadas:** GitHub confirma `nicowo=d92cfe5` del 23-oct, ancestro de HEAD: no tiene novedades pendientes. Los dos commits nuevos están en `origin/localizacion-offline` (`1282d61`, `e333cee`). Aportan integridad de grabaciones y exportación ONNX reutilizables, pero se solapan con el recorrido local y la escena. Una previsualización de tres vías muestra conflictos; no se aplicaron. El validador temporal nuevo puede marcar como válida una sincronía no identificable: un contraejemplo sintético devuelve −35 ms frente a +30 ms simulados, aunque sus diez pruebas seleccionadas pasan. No usar esa etiqueta como aceptación de nuestra cámara. El diseño Android que adelanta XFeat no reemplaza la comparación de los cuatro candidatos. [Revisión de ramas y decisiones](LocalizacionOffline-RevisionRamas-2026-11-05.md).

Los apartados siguientes conservan el detalle de los resultados anteriores; la pauta operativa vigente es la de arriba.

## Primera entrega restaurada: resultado más reciente

La primera entrega volvió completa: **269 archivos de captura/exportación coinciden por SHA-256** con los iniciales; solo cambió `.DS_Store`. Se guardó una copia verificada de sus 189 PNG y siete JSONL en una salida nueva y se repitió la detección: **54 esquinas en 189/189, diferencia máxima 0,000 px frente al resultado guardado**. La segunda entrega mantiene sus 418 hashes. Ya no faltan las rutas de los originales.

**Sí es útil como comprobación y diagnóstico.** Se añadió solo el material de ajuste de la primera al de la segunda: 12 sesiones / 108 vistas seleccionadas, conservando las seis sesiones reservadas / 162 imágenes fuera del ajuste. Los cuatro intrínsecos del radial conjunto cambian como máximo **0,301 px** respecto del candidato de la segunda serie; los residuos reservados apenas cambian (periferia nueva 0,4525 frente a 0,4530 px). No se demuestra mejora física por juntarlas: se mantiene el radial de segunda como candidato base y el conjunto como sensibilidad. El error registrado de cada imagen usa su propia K, porque los priors de ambas entregas son diferentes.

La primera también conserva el caso de movimiento `22:54:49`: el desacuerdo angular mediano sigue **~6°** al usar la nueva K/distorsión. **Recalibrar la lente no resuelve por sí solo esa inconsistencia.** Sin sello independiente de pose ni confirmación instrumental de tablero fijo, no se atribuye a una causa única ni se declara sincronía validada. No hace falta otra serie general del 15T por esta revisión.

Evidencia y comandos en la sección «Primera entrega restaurada» del [informe del tablero](LocalizacionOffline-CalibracionCasa2-2026-11-05.md). Salida nueva: `Tools/LocalizacionOffline/salida/20261105-calibracion-restaurada/`, con copia de datos, redetección, partición preservada, sensibilidad conjunta y comprobaciones de movimiento. No se alteraron originales, mapas, consultas o APK. Las medidas de casilla siguen siendo 24 × 24 mm, informadas por Gael; la escala arbitraria y las cámaras inestables de Teología siguen pendientes de controles físicos.

## Tablero en casa: segunda entrega y candidato base

[Informe de la segunda entrega](LocalizacionOffline-CalibracionCasa2-2026-11-05.md). **Xiaomi 15T, once barridos / 298 PNG íntegros con metadatos**, 54 esquinas detectadas en todos. Se revisaron los 298 tableros y 55 encuadres completos. Hay centro, cuatro zonas de esquina, perspectivas laterales opuestas y verticales; el patrón es mayor y mucho más nítido. **No hace falta repetir esta serie completa.** Gael confirmó cinco cuadrados = 12 cm en ambos ejes: **24 × 24 mm por casilla**. Ya no falta la segunda medida; no constan incertidumbre/instrumento ni certificación de planitud.

**Calibración candidata para ensayos PC, sin aplicar al mapa:** nueve carpetas para ajuste, con 81 vistas seleccionadas; las carpetas 23:13:38 (inferior izquierda) y 23:14:55 (centro) quedaron fuera del ajuste, 54 imágenes. K registrada de esta entrega: fx/fy 1255,02/1255,34 px. Radial k1 estimado: fx/fy **1297,30/1295,83**, cx/cy **962,00/535,43**, k1 **0,06127**. Al retirar una sesión de ajuste, fx varía 1295,18–1299,29 px y k1 0,05416–0,06612: mayor estabilidad que antes. En la carpeta periférica reservada, mediana RMS 0,648 px con K registrada sin distorsión frente a **0,453 px con radial**; en centro 0,379 frente a 0,372 px. La pose se estima con 27 esquinas y el residuo con las otras 27. Brown mejora algo más y queda como sensibilidad; ya no presenta el pliegue radial del primer ensayo en la rejilla comprobada. No se presenta este conjunto de desarrollo como evaluación final ciega.

La nueva calibración no degrada mucho el tablero central de la primera entrega en una comprobación retrospectiva. **No transferirla automáticamente a todas las cámaras de construcción:** faltan control de foco/configuración y comprobación del mapa; la periferia extrema y la planitud siguen limitando el alcance. Un residuo subpíxel no demuestra exactitud física. Los detalles y coeficientes están en el informe y `ajuste/modelos.json`, conservando radial como candidato simple para PC y Brown como sensibilidad.

La base óptica candidata `[-y_unity,-x_unity,z_unity]` vuelve a ser compatible con los movimientos: discrepancias medianas por sesión 0,28–0,79°, condicionales a tablero fijo. **La sincronía sigue pendiente:** tiempos pose/sensor iguales 298/298 por cómo los escribe el grabador, sin sello independiente del instante de pose. No se usó Unity como verdad ni se integró todavía la conversión óptica. Para multicuadro en movimiento hace falta instrumentación y comprobación específicas, no otra repetición general de tablero.

**Qué debe hacer Gael ahora:** conservar estas carpetas; no se pide otra serie del 15T por rutina. Xiaomi 14 aún no está recibido y requiere calibración propia si se usará. Siguen pendientes los controles físicos de Teología, incluidos reservados. El mapa mantiene escala arbitraria y cámaras inestables, sin precisión de 10–20 cm demostrada.

Salida nueva `Tools/LocalizacionOffline/salida/20261105-calibracion-casa-2/`, con hashes de los 418 archivos recibidos verificados, reservas previas al ajuste, esquinas, calibraciones, sensibilidades y láminas finales en `revision-final/`. Originales intactos; ambos conjuntos de tablero y consultas fuera del mapa. [Primera entrega](LocalizacionOffline-CalibracionCasa-2026-11-05.md) y su salida se conservan como historia; sus peticiones de otra medida y vistas mayores ya fueron atendidas. Sin recompilar APK ni usar adb.

## Revisión geométrica anterior y salida preparada

Se revisaron las cámaras con imágenes de construcción intermedias y se ejecutó un grafo que conserva solo vistas consecutivas dentro de cada sesión, manteniendo exhaustivos los enlaces entre sesiones. La cámara `143744/0030` de XFeat cambia 178° con ese grafo; `143053/0020` también cambia de posición con ambos métodos. Después se reconstruyó con seis vistas adicionales, las mismas para ambos métodos y con máscaras propias: **71 vistas seleccionadas; XFeat 63 cámaras/6.393 puntos y ALIKED 61/2.759**. Los dos registran las seis intermedias, pero siguen diferencias grandes: entre métodos, `143053/0015–0025` llega a 17,10 % del diámetro y `143744/0005` a 14,09 %. En el registro hay avisos numéricos en intentos de ajuste. Estos porcentajes son estabilidad interna, no centímetros. No hay aún mapa validado ni una solución elegida.

La auditoría detectó que `timestamp_pose_sensor` copia el tiempo de cuadro de cámara: no es un timestamp independiente de la pose Unity leída en `LateUpdate`. La orientación de sensor, distorsión y sincronía necesitan validación por teléfono. El banco PnP y temporal solo está comprobado con datos sintéticos; no hay motor nuevo en Android. Las 510 consultas nuevas siguen excluidas por sesión/hash. La unión histórica sigue separada e inestable. El [informe de revisión](LocalizacionOffline-RevisionCamaras-2026-11-05.md) incluye experimentos, decisiones por cámara, límites y reproducción.

**Referencia general de medición:** [hoja de medición y calibración de Teología](LocalizacionOffline-Visita-Teologia.md), con [fotografías de puntos propuestos](LocalizacionOffline-Controles-Teologia.jpg), controles distribuidos, controles reservados y tablero por teléfono. Las posiciones marcadas requieren confirmación en terreno; no se han inventado medidas. El APK existente es de captura y no prueba el nuevo localizador; no se recompiló ni tocó el teléfono.

## El objetivo no cambió

El visitante apunta a un edificio desde 10–25 m, hace un barrido breve de hasta cinco segundos y el teléfono coloca
el contenido virtual sin conexión. El objetivo de alineación es 10–20 cm sobre referencias físicas de fachada.
Confiabilidad primero; velocidad y consumo después, entre los candidatos que funcionen.

Arquitectura propuesta: mapa 3D propio preparado en PC → reconocimiento de detalles en el teléfono → cálculo de
posición con geometría → combinación de varias imágenes y movimiento de ARCore → colocación del contenido.
SIFT, SIFT+LightGlue, ALIKED+LightGlue y XFeat+LighterGlue son candidatos dentro de esa arquitectura.
Todavía no se eligió un ganador definitivo ni se sustituyó Immersal en Android.

## La distinción que faltaba explicar

| Trabajo | Quién lo hace | Para qué sirve | Captura |
|---|---|---|---|
| Construir el mapa | El equipo, al preparar un edificio | Saber dónde están los detalles de la fachada en 3D | Recorrido con desplazamiento, vistas conectadas y solapamiento |
| Usar/probar el localizador | El visitante o una persona evaluando | Averiguar dónde está el teléfono dentro de ese mapa | Barrido breve de hasta cinco segundos |

**El recorrido de construcción no es un nuevo requisito para el visitante.** Tampoco tiene que durar cinco segundos.
La pauta anterior de barridos fue útil para capturar consultas, pero no aseguró las vistas conectadas necesarias
para construir el mapa. No se interpreta esto como un error de quien siguió esa pauta.

Tener una nube o modelo 3D visible tampoco equivale a tener un mapa listo para este localizador: necesitamos asociar
detalles de las fotografías con puntos 3D, cámaras compatibles, escala real y controles independientes. Los datos del
campus existentes no acreditan por sí solos la precisión requerida. No esperamos otras carpetas del PC ni del dron.

## Lo que hicimos y dónde estamos

| Etapa | Trabajo realizado | Estado real |
|---|---|---|
| Herramientas y captura | Banco Python con los cuatro métodos, pesos locales y grabador de barridos/recorridos Android | Implementado; 31 sesiones nuevas y 1.900 PNG auditados. Calibración y sincronía ópticas pendientes |
| Reconocimiento entre fotos | Comparaciones sobre los registros propios, incluidos cambios de sesión/teléfono | Diagnóstico realizado. XFeat dio señales favorables; no mide precisión 3D ni elige motor |
| Mapa 3D de Teología | XFeat frente a ALIKED sobre vistas nuevas; sensibilidades con 35/65 vistas y unión de 84 con las anteriores | Mejora fuerte al densificar XFeat; quedan cámaras discrepantes y la unión histórica sigue inestable. **Etapa actual** |
| Escala y calibración | Segunda entrega 15T: medidas en ambos ejes, calibración candidata más estable y dos sesiones reservadas | Pendientes transferencia a captura de edificio, planitud/incertidumbre, sincronía y Xiaomi 14. Teología sigue en escala arbitraria |
| Localización real y varias imágenes | Cálculo de pose y refinamiento temporal implementados y comprobados con datos sintéticos | Falta ejecutar y medir sobre un mapa real validado; aún no sabemos cuánto mejora ARCore |
| Android y elección final | Existe APK de captura; la demo conserva Immersal | Falta integrar y medir candidatos en S25, Xiaomi 14 y Xiaomi 15T |
| Ampliación del piloto | El plan contempla EIC y después los cuatro edificios | Pendiente; primero completar un caso de principio a fin en Teología |

Las pruebas de software aprobadas verifican que las herramientas hacen lo esperado en casos controlados. No son
pruebas de alineación del edificio, arranque en cinco segundos o precisión en centímetros.

El nuevo material contiene **12 recorridos de construcción y 19 consultas**, íntegros dentro de sus 31 carpetas:
1.900 PNG únicos con metadatos. Teología aporta cinco recorridos y 580 imágenes de construcción, de ambos Xiaomi;
sus cuatro consultas (107 imágenes) permanecen fuera del mapa. CB aporta tres recorridos y X1 cuatro. El log cita
además dos barridos finales de X1 cuyas carpetas no están en lo entregado ni en los ZIP; se documentan y no bloquean
Teología. No se espera otro envío. Copias y auditoría reproducibles: `salida/20261105-recorridos/`.

Las cifras finales usan máscaras revisadas **v2**, tras ampliar la exclusión de una lámpara/persona/tablero y otro
poste; los ensayos preliminares se conservaron y se repitieron las reconstrucciones filtradas. Con las mismas 35
vistas nuevas, XFeat conserva 30 cámaras y 2.152 puntos de fachada; un recorrido cambia 10,96° de mediana frente al
modelo completo y una cámara 77,24°. ALIKED devuelve componentes independientes de 8 y 29 cámaras, con algunas
imágenes compartidas. La unión de las 49 vistas anteriores y las 35 nuevas conserva 78 cámaras y 3.973 puntos de
fachada, pero la histórica B cambia 38,40° entre completo/fachada y 51,34° al corregir las máscaras. Añadir vistas
no valida por sí solo esa conexión.

**La selección más densa de 65 vistas sí mejora mucho XFeat:** 58 cámaras y 5.824 puntos de fachada, 4.462 con apoyo
entre sesiones. Completo frente a fachada: discrepancia mediana de centros 0,342 % del diámetro y rotaciones medianas
por recorrido de 0,49–2,00°. Quedan cámaras con diferencias de hasta 3,62° y sensibilidad al muestreo; la escala sigue
arbitraria. **ALIKED sobre las mismas 65 vistas también mejora:** un componente de fachada de 55 cámaras y 2.330
puntos. Ambos métodos de fachada comparten 55 cámaras, con discrepancia mediana de centros de 0,445 % y rotaciones
medianas por recorrido de 0,42–1,71°. Sin embargo, `0001-TEO-B-10m` del recorrido `143744` discrepa 13,51 % del
diámetro en posición; al inicio de `142828` hay diferencias angulares de hasta 4,45°. Esos casos se revisaron con imágenes intermedias; el ensayo de 71 vistas reveló otras discrepancias, descritas arriba. La corrección de máscaras afecta mucho menos a estos dos modelos densos que a la unión histórica.
Este es un candidato mejor para consolidar, sin forzar su unión con las históricas. La mejora y los fallos se documentan por separado; no acreditan
alineación en centímetros ni permiten usar aún el modelo como mapa métrico.

No fue una comparación multicuadro final entre los cuatro candidatos. SIFT no queda descartado para esa evaluación.
XFeat y ALIKED se mantienen para el diagnóstico geométrico; ningún motor Android está elegido.

## Orden de trabajo desde ahora

1. **Consolidar los candidatos de Teología de 65/71 vistas nuevas con el material recibido.** Revisar cámaras discrepantes
   y conexiones entre ventanas/módulos repetidos, usando la vecindad temporal de los recorridos y enlaces comprobados
   sobre detalles distintivos. Revisar sectores y cierre de vuelta; repetir exclusión del primer plano y estabilidad.
   No pedir otra captura general antes de identificar un enlace concreto sin cobertura. Entregable: geometría
   conectada de fachada con correspondencias distribuidas. Tener muchos puntos no basta.
2. **Validar escala, cámaras y contenido.** Obtener controles físicos y otros reservados para comprobar el mapa;
   validar intrínsecos, orientación y sincronía del sensor/ARCore. Comprobar por separado tamaño y colocación del
   contenido virtual. Entregable: mapa métrico y errores documentados, o causas concretas que impidan aceptarlo.
3. **Comparar localización sobre ese mapa.** Probar los cuatro candidatos con las mismas referencias y consultas;
   comparar una imagen con varias imágenes y movimiento de ARCore. Entregable: resultados por condición, con
   correspondencias verificadas y errores físicos. Mantener construcción, ajuste y evaluación separados.
4. **Medir en Android y elegir.** Integrar los candidatos que superen geometría y medir el proceso completo en los
   tres teléfonos. Entregable: confiabilidad, alineación, tiempo y consumo reales; elegir con esos resultados.
5. **Ampliar y comprobar respaldo.** Extender a EIC y después a Ciencias Básicas/X1; evaluar distancias, luz, ángulos,
   recuperación de tracking y marcadores de respaldo. El marcador sigue siendo parte del plan, no una función ya validada.

No se avanzará de etapa por tener un script implementado: debe cumplirse la comprobación correspondiente con datos reales.
SCR y SLAM propio permanecen como alternativas posteriores si los fallos medidos lo justifican; no abren trabajo ahora.

## Próximos pasos verificables

1. En PC, revisar la identidad y distribución de tracks en `143053/0015–0025`, `143744/0001–0011` y la conexión de esquina `143744/0020–0030`; contrastar el grafo y los modelos de 65/71 vistas. Las intermedias ayudan, pero aún no se ha demostrado qué pose es correcta ni un cierre de vuelta independiente.
2. Usar en PC los candidatos de la [segunda entrega del tablero](LocalizacionOffline-CalibracionCasa2-2026-11-05.md) para estudiar cámara y transferencia al mapa, sin asumir sincronía ni recalibrar automáticamente todas las referencias. No se pide repetir la serie del 15T; falta Xiaomi 14 si se va a usar. En terreno, realizar la [visita de medición](LocalizacionOffline-Visita-Teologia.md): documentar puntos accesibles y medir con incertidumbre e independencia. No repetir recorridos generales. Sin una medición 3D fiable de puntos distribuidos, la escala/forma no se pueden aceptar.
3. Con controles y cámara válidos, ajustar escala y comprobar puntos reservados; después evaluar PnP y multicuadro con consultas separadas. Los 107 PNG de consulta de Teología han sido inspeccionados y no son prueba ciega final.
4. Preparar un prototipo Android solo con un mapa métrico y conversión óptica comprobados; integrar el motor justificado y medir todo el ciclo sin red. Elegir entre los cuatro métodos por confiabilidad y resultados físicos, no por comodidad o tiempo CPU.

El grabador actual puede servir para una visita de medición/calibración. No demuestra localización del nuevo mapa ni los objetivos de 10–25 m, ≤5 s y 10–20 cm. Los puntos y medidas físicos faltantes no deben sustituirse por etiquetas de distancia, GPS, Unity o Immersal.

## Cómo se sabrá que el proyecto funciona

Siguen siendo objetivos pendientes del plan original: ≥95 % de inicios correctos en ≤5 s por teléfono; desfase
mediano ≤10 cm y p95 ≤20 cm; cero falsas aceptaciones en al menos 1.000 secuencias negativas; estabilidad y recuperación
al mover/tapar el teléfono; funcionamiento local e independencia para reconstruir mapas.

D/E de la mañana siguen separados del mapa y J se limita a análisis 2D. Las 19 consultas de la tarde también están
separadas por sesión y hash; solo se revisaron integridad y encuadre, sin matching ni PnP. Su inspección no se presenta
como evaluación final ciega. Para declarar éxito harán falta consultas independientes y controles físicos.

## Documentación de referencia

- [Medidas informadas el 6-nov](LocalizacionOffline-Medidas-2026-11-06.md): tres longitudes interiores, A–B/E–F no medidos y correspondencias pendientes.

- [Pauta para el 6-nov, teléfonos y cinta](LocalizacionOffline-Jornada-2026-11-06.md): qué medir y fotografiar mañana; AJ/RES separados y límites de acceso.
- [Revisión de nicowo y commits remotos](LocalizacionOffline-RevisionRamas-2026-11-05.md): aportes, solapamientos y por qué el validador temporal no acredita sincronía.

- [Segundo tablero de casa, 15T](LocalizacionOffline-CalibracionCasa2-2026-11-05.md): resultado actual, medida de ambos ejes, calibración candidata, comprobación y límites.
- [Primer tablero de casa](LocalizacionOffline-CalibracionCasa-2026-11-05.md): diagnóstico histórico, conservado.

- [Visita de medición y calibración](LocalizacionOffline-Visita-Teologia.md): hoja, controles propuestos y qué traer.
- [Revisión de cámaras y experimentos 65/71](LocalizacionOffline-RevisionCamaras-2026-11-05.md): evidencia y límites actuales.
- [Sensibilidad óptica ejecutada el 6-nov](LocalizacionOffline-OpticaTeologia-2026-11-06.md): 16 reconstrucciones, enlaces incorrectos identificados y cuatro medidas asociadas a ventanas.
- [Corrección de identidad ejecutada después](LocalizacionOffline-IdentidadTeologia-2026-11-06.md): 38 reconstrucciones adicionales, discrepancia extrema reducida y bordes interiores confirmados en fotos.
- [Plan original](LocalizacionOffline-PlanOriginal.md): requisitos y arquitectura de partida, conservados literalmente.
- [Implementación y captura](LocalizacionOffline.md): herramientas, build de captura y validaciones de software.
- [Auditoría de registros](LocalizacionOffline-Registros-2026-11-05.md): integridad, particiones y comparación inicial.
- [Geometría de Teología](LocalizacionOffline-Geometria-2026-11-05.md): modelos, evidencia, límites y trayectoria propuesta.
- [Recorridos de la tarde](LocalizacionOffline-Recorridos-2026-11-05.md): inventario completo, auditoría, comparación 35/65/84 vistas, CB/X1 y evidencia visual.
- [README del banco](../Tools/LocalizacionOffline/README.md): comandos para reproducir.
- [Siguiente sesión](LocalizacionOffline-SiguienteSesion.md): encargo que originó esta revisión; consultar los resultados actuales arriba.
- [Encargo de continuación ejecutado](LocalizacionOffline-Continuacion.md): registro histórico del trabajo anterior.
