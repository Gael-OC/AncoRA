# Teología: ensayo dirigido de 71 a 78 vistas — 6-nov-2026

**Ejecutado: matching de las siete vistas, cuatro reconstrucciones nuevas y una réplica radial XFeat, en bases nuevas.** Las imágenes ayudan al apoyo y a la repetibilidad local de `143744/0001`, pero **no resuelven el mapa**: `143744/0068` cambia 33,54° entre baseline y radial XFeat y sigue sin registrarse en ALIKED. No se elige una óptica/réplica, no se aplica escala ni se integra un localizador Android.

Salida: `Tools/LocalizacionOffline/salida/20261106-ensayo-78/`. Las 71 v6, modelos anteriores, originales, ensayos negativos y cambios locales permanecen conservados. Rama `localizacion-offline`, HEAD `f84256fe0bbccd1acc00fce508c5a06a1039886b`, referencia remota dos commits por delante. Sin reset/restore, cambio de rama, worktree, commit, push, pull, integración del respaldo remoto, internet, Unity, APK ni adb. Se reutilizó el Python y pesos instalados.

## Dos soluciones v6 de 71: evidencia previa

`salida/20261106-ambiguedad-v6-revision/` conserva fotos, scripts, todos los tracks por observación y comparación de las dos corridas radiales XFeat. Las selecciones, opciones mapper y verificación coinciden; las tablas SQLite `images`, `keypoints`, `matches` y `two_view_geometries` coinciden por contenido ordenado, incluidos los 2.485 pares. **La divergencia aparece después de la verificación geométrica**, con iguales entradas; no se atribuye a otras correspondencias suministradas o a otro valor de calibración.

| Cámara | Observaciones primera / réplica | Índices 2D comunes | Tracks completos iguales / cambiados entre comunes | Exclusivos primera / réplica |
|---|---:|---:|---:|---:|
| 143744/0001 | 119 / 69 | 23 | 2 / 21 | 96 / 46 |
| 143744/0068 | 41 / 37 | 37 | 28 / 9 | 4 / 0 |

Los tracks se comparan por listas de `(imagen, índice 2D)`, no por IDs 3D, que no son estables entre reconstrucciones. En `/0001` la primera corrida ocupa seis celdas crudas 4×4; la réplica, cuatro de una sola columna. `/0068` concentra 40/41 o 37/37 observaciones en dos celdas contiguas, con profundidades p10–p90 2,679–2,770 o 2,732–2,806 unidades arbitrarias. Eso sugiere apoyo poco distribuido y aproximadamente planar; no prueba una causa única ni exactitud física. Los grandes ángulos calculados de tracks dependen de cámaras/asociaciones todavía no validadas.

**Enlace visual concreto:** `/0001` kp19 está junto a la esquina inferior derecha de P/U1. La primera corrida lo une con `142828/0039` kp205 y `143744/0005` kp337, compatibles visualmente con esa ventana junto al reflector. La réplica lo incorpora a un track de 24 observaciones que también incluye `142623/0001` kp8, `143053/0001` kp185 y `143744/0020` kp126, en otras ventanas. Se revisó la lámina `0001-kp19.jpg` y fotos completas. Es un contraejemplo dirigido de identidad, **no acredita todo el modelo inicial ni explica por sí solo los 14,64 % de variación**. No se podó ese track ni se retiró la cámara.

## Selección, anotaciones y reutilización

Se abrieron completas `0002–0004`, `0066–0067` y `0069–0070` del recorrido 15T `143744`. Se conservaron las siete elegidas por vecindad ordinal, antes de observar resultados; no se eligieron por éxito de SfM. Las 71 entradas de fachada e identidad v6 quedan exactamente iguales dentro de las 78.

- `0002–0004`: continuidad de P/U1, la ventana superior junto al reflector/palmera. Identidad propia conservadora de esa ventana, marco y un margen de muro alrededor, sin afirmar que solo etiqueta vidrio.
- `0066–0067,0069–0070`: continuidad de pared/ranuras del lateral L. Las ranuras no tienen identidad física de módulo suficientemente comprobada: **sin elementos etiquetados**, permanecen desconocidas. Una banda de cara sola no asigna identidad.
- Revisión independiente Terra detectó intrusiones de farolas y cabezas en las máscaras nuevas. Se corrigieron **antes del filtrado y de reconstruir**, se conservan las dos versiones preliminares y se congeló la final. Las siete máscaras se limitan a la pared superior, con exclusiones explícitas de postes; se conserva intencionalmente la luminaria solar adherida como detalle estático del edificio, fuera de P/U1. Las 71 máscaras previas no se corrigen silenciosamente.

Por método: **21 pares entre nuevas + 497 cruzados = 518 calculados; 2.485 anteriores reutilizados, total 3.003.** El conector comprueba firmas actuales, completitud de fuentes, hashes de imágenes y coordenadas/orden de keypoints de caché antes de conservar índices. El filtro de fachada y luego identidad se aplica a la unión. Los 2.485 pares filtrados anteriores de cada método se compararon numéricamente contra su v6: **todos idénticos**. Los keypoints de las 71 también coinciden con v6; no se intercambian índices entre métodos.

El filtro de identidad sobre fachada pasa XFeat de 120.698 a 100.454 enlaces, retirando 20.244 incompatibles conocidos; ALIKED, de 62.286 a 52.827, retirando 9.459. Desconocido se conserva en ese filtro; estas cuentas no miden la tasa física de acierto.

Baseline mantiene K registrada/D=0. Radial aplica la **K completa y D exacta de la segunda serie de casa solo a los 35 frames 15T**, incluidas las siete nuevas; los 43 Xiaomi 14 conservan su configuración registrada. Fuente y SHA de calibración en `condiciones-verificadas.json`; no se rectifican PNG ni keypoints. COLMAP desplaza conjuntamente centros principales y características +0,5 px. Verificación, bases y mapper se vuelven a ejecutar: K/D fijas, semilla 5, cuatro hilos, límite 300 s. La réplica usa exactamente las mismas entradas radiales y otra base.

## Reconstrucción de 78 y cámaras ausentes

Todos los ensayos produjeron un único componente retenido. Se conserva cada componente nativo y no se suman cámaras de componentes. La regla de informe, fijada antes, es más cámaras y desempate por índice nativo.

| Condición | Cámaras / puntos | Reproyección media px crudos | Tracks con identidades conocidas incompatibles |
|---|---:|---:|---:|
| XFeat baseline | 69 / 6.782 | 1,3274 | 125 |
| XFeat radial | 69 / 7.055 | 1,3160 | 128 |
| ALIKED baseline | 66 / 3.007 | 1,1313 | 58 |
| ALIKED radial | 66 / 3.052 | 1,1338 | 55 |
| XFeat radial réplica | 69 / 7.006 | 1,3282 | 124 |

**Los cinco registran `0002`, `0003`, `0004`, `0066` y `0067`; ninguno registra `0069` o `0070`.** Se conservan las siete en todas las entradas. No se pierden cámaras previamente registradas en sus correspondientes componentes v6: XFeat conserva 64 y suma cinco; ALIKED conserva 61 y suma cinco. Las ausentes anteriores y nuevas se enumeran individualmente en `resumen-resultados.json`.

Ausentes comunes a ambos métodos: `142623/0097,0106,0116`, `142828/0001,0030`, `143053/0039`, `143857/0087` y las nuevas `143744/0069,0070`. ALIKED tampoco registra `143744/0068,0106` ni `143857/0001`. **El acuerdo XFeat/ALIKED no comprueba 0068 porque no está en la intersección.**

El diagnóstico adicional `no-registradas-diagnostico.json` sigue cada inlier a puntos 3D del **modelo final**, sin cambiar entradas/umbrales. 0069 tiene solo 21/26 índices 2D con algún apoyo 3D XFeat baseline/radial (una celda), y 25/25 ALIKED (dos celdas). 0070 tiene 0/0 XFeat y 28/27 ALIKED. Todos quedan por debajo de los **30 inliers mínimos de pose absoluta** configurados, incluso antes de comprobar si esos candidatos son físicamente correctos. Esto explica la insuficiencia del apoyo disponible en el modelo final; no reconstruye la historia exacta de cada intento del mapper. Los pares brutos pueden tener cientos de matches, pero gran parte cae fuera de la pared superior conservadora: no equivale a cientos de puntos 3D de fachada. No se relajan máscaras ni umbrales para forzar registro.

## Qué ayudó y qué persiste

Sim(3) usa todos los centros comunes, con ancla `142623/0001`; no se excluyen cámaras por residuo. Los porcentajes se refieren al diámetro del primer modelo de cada comparación y **no son metros**. Las muestras comunes varían por comparación: no interpretar máximos sobre conjuntos distintos como una prueba controlada de precisión.

| Comparación | Cámaras comunes | Centros mediana / máximo, % diámetro | Máximo angular |
|---|---:|---:|---:|
| 78 XFeat/ALIKED baseline | 66 | 0,416 / 2,582 | 5,81° |
| 78 XFeat/ALIKED radial | 66 | 0,286 / 1,917 | 4,37° |
| 78 XFeat baseline/radial | 69 | 0,489 / 11,667 | **33,54° en 0068** |
| 78 ALIKED baseline/radial | 66 | 0,381 / 0,881 | 3,49° |
| 78 XFeat radial/réplica | 69 | 0,075 / 1,132 | 3,17° |

**0001:** en las dos nuevas radiales cambia **0,219 % / 0,605°**, frente a 14,64 % en las dos v6 de 71. Su apoyo pasa de 119/69 observaciones en aquellas corridas a **166/169**, seis celdas en ambas. La radial 78 queda cerca de la primera radial 71 (0,212 % / 0,454° para esta cámara), pero a 14,825 % de la réplica 71. Las tres vistas nuevas aportan continuidad y apoyo; dos ejecuciones más próximas **no prueban que desaparecieron todas las soluciones alternativas ni autorizan escoger una réplica**.

**0068:** las nuevas radiales difieren **0,729 % / 1,837°**. Baseline/radial 78 difieren **11,667 % / 33,536°**, y baseline 71→78 cambia 9,486 % / 25,273°; la sensibilidad óptica empeora respecto a los ~6,10° de v6. Sigue apoyada en 38/38/39 observaciones XFeat, solo dos o tres celdas, **todas de identidad desconocida**. La distribución/profundidad y los tracks deben comprobarse físicamente antes de usar su pose. No se borra la cámara, no se adopta radial por quedar más cerca de una corrida anterior y no se atribuye el cambio solo a la lente.

`revision-tracks78/revision.json` compara explícitamente tracks nuevos: 0001 radial/réplica comparte 151 índices, 129 tracks completos iguales y 22 cambiados; 138/166 tracks radiales incorporan 0002/3/4. En 0068, baseline/radial comparte 28 índices pero solo siete tracks completos iguales; 21 cambiados. Radial/réplica comparte 36, con 29 tracks iguales y siete cambiados. **Solo cinco tracks de 0068 incluyen alguna nueva, siempre 0066 y además vistas antiguas**: la vecindad añadida aporta poco apoyo distribuido a esta cámara. Las tablas de cámaras/keypoints/matches/geometría de las dos nuevas radiales son idénticas por contenido, incluidos los 3.003 pares.

La lámina `revision-tracks78/0068-kp584.jpg` muestra cambio de asociación sobre el mismo montante (142623/0078 kp49→kp1261, separación 9,375 px); por sí sola no certifica falsedad ni causa del cambio de pose. La auditoría `observaciones-repetidas.json` encuentra tracks con varios índices de una misma imagen: 102/94/299/309/103 tracks en las cinco condiciones, separaciones máximas ~6,76–7,50 px. Son observaciones próximas desde un mismo centro de cámara, no vistas independientes adicionales; no se ha demostrado que sean el mismo detalle. Este hallazgo no demuestra un bug ni explica por sí solo la ambigüedad. Se conserva sin podar.

**Decisión:** conservar 71 como baseline y 78 como ensayo de desarrollo útil pero insuficiente. La ayuda localizada a 0001 convive con inestabilidad de 0068, dos nuevas no registradas y conflictos conocidos en todos los modelos. Buen residuo o mayor acuerdo entre dos métodos no validan forma/escala. No se ejecuta ajuste métrico sobre cámaras aún no aceptables.

## Los 25 históricos y las aristas: trabajo separado ejecutado

Revisión visual completa, metadatos y hashes de los 25 en `salida/20261106-78-historicos-revision/`. Los hashes son únicos entre ellos y no coinciden con los 182 candidatos históricos ni con la selección histórica usada; hay semejanza visual considerable.

- Samsung `20261028-113656`: 24 TEO-A, 0075–0098. Hasta 0090 casi el mismo encuadre; desde 0091 se abre hacia el lateral; persona en 0084–0086. Sesión completa: **51 PNG, 27 CB y 24 TEO**; CB/0045 ya es referencia histórica. No dividir por etiqueta de edificio para aparentar independencia.
- 15T `20261028-115641`: 0032-TEO-E muestra otra esquina, poste y barandas. La sesión completa tiene **27 TEO**; 26 ya eran candidatos, siete se seleccionaron para SfM y 0019 figura como referencia. 0032 no es consulta independiente de aquella sesión.

Función documentada: **diagnóstico/desarrollo histórico por sesión completa**, conservando asignaciones anteriores. No se añadió ninguna al mapa ni a consultas; no se transfirió calibración de casa. La inspección no identifica una carencia concreta que justifique ahora un nuevo SfM histórico. No se afirma que ayudaron geométricamente: no fueron reconstruidas.

Se avanzó además la asociación multivista de las cuatro ventanas confirmadas: `salida/20261106-aristas-multivista-revision/asociaciones-propuestas.json` contiene vistas candidatas nuevas, segmentos manuales y hashes. C–D tiene candidatas `143857/0116` y `142828/0011`; R–S, `142828/0078`; H–I, `142623/0001`; vuelta/frente, `142623/0030`. Las fotos muestran las mismas ventanas; **los trazos son propuestas aproximadas del agente, no una nueva confirmación del usuario ni puntos extremos equivalentes entre vistas**. Hay oclusión parcial en la candidata 15T de C–D. Se requiere refinamiento en originales y geometría aceptable; no se eligieron puntos 3D próximos por conveniencia, trianguló ni aplicó escala.

C–D=2,70 m sigue como única longitud de ajuste exploratorio; 6,20/2,70/1,95 m siguen reservadas. Altura de cinta, repeticiones e incertidumbre siguen no informadas. Las reservas no eligieron cámaras, óptica ni umbrales. No se pide identificar otra vez las ventanas, alcanzar puntos altos, otro recorrido general ni otra serie de tablero del 15T.

## Reproducción, comprobaciones y app

Las herramientas reutilizan `sfm_aprendido.py`, `conectar_recorridos.py`, `sfm_diagnostico.py` e `identidad_teologia_20261106.py`; solo se añadieron dos orquestadores de experimento. `fuentes/` conserva snapshots iniciales, corregidos/finales y los dos documentos de inicio antes de actualizarlos. `reproduccion-anotaciones-verificada.json` comprueba que el script final reproduce exactamente las 78 entradas de fachada/identidad; los preliminares no se sustituyen por el final.

En una **ruta nueva**, desde raíz; esperar a que terminen ambos matchings antes de reconstruir:

```sh
PY=Tools/LocalizacionOffline/.venv/bin/python
T=Tools/LocalizacionOffline
R=$T/salida/otra-prueba-78
$PY $T/experimentos/ensayo_78_20261106.py preparar --run "$R"
$PY $T/sfm_aprendido.py correspondencias "$R/siete-seleccion.json" --method xfeat-lighterglue --output "$R/siete-xfeat"
$PY $T/experimentos/conectar_recorridos.py --old "$T/salida/20261105-revision-camaras/ampliado-xfeat" --new "$R/siete-xfeat" --output "$R/union-xfeat"
$PY $T/sfm_aprendido.py correspondencias "$R/siete-seleccion.json" --method aliked-lightglue --output "$R/siete-aliked"
$PY $T/experimentos/conectar_recorridos.py --old "$T/salida/20261105-revision-camaras/ampliado-aliked" --new "$R/siete-aliked" --output "$R/union-aliked"
$PY $T/experimentos/ensayo_78_20261106.py condiciones --run "$R"
$PY $T/experimentos/ensayo_78_20261106.py replica --run "$R"
$PY $T/experimentos/ensayo_78_20261106.py reconstruir --run "$R"
$PY $T/experimentos/analizar_78_20261106.py --run "$R"
```

La [auditoría independiente de resultados](../Tools/LocalizacionOffline/salida/20261106-78-auditoria-resultados/informe.md) comprueba modelos reales, K/D, las diez comparaciones y pares anteriores. Los logs conservan 16 avisos CHOLMOD en XFeat baseline, cuatro de factorización Eigen en XFeat radial y cinco en ALIKED radial; ALIKED baseline y réplica radial no tienen esos avisos. Los cinco producen modelo: no son abortos completos, tampoco se omiten o se usa su ausencia para elegir réplica.

**20 pruebas Python aprobadas de nuevo**, más comprobación real de igualdad de los 4.970 pares previos filtrados y reproducción de las anotaciones. Son pruebas de software y estabilidad interna, no validación física. Se actualizó Graphify local; avisa que 280/518 archivos tienen formatos sin extractor, por lo que el grafo es parcial y se verificó el código directamente.

El mapa permite investigar correspondencias, tracks y sensibilidad de pose a escala arbitraria. **No acredita 10–20 cm, escala, localización métrica real ni arranque Android.** La app conserva Immersal y los cuatro candidatos siguen abiertos. Para probar el nuevo localizador en la app faltan cámaras/identidad aceptables, aristas 3D y escala con reservas, contrato de imagen cruda K/D coherente en importación/triangulación/PnP/proyección (o rectificar y reextraer), consulta real separada en PC, comparación de candidatos y puerto/medición Android. Multicuadro requiere además conversión óptica y sincronía real: timestamp pose copiado y caso de casa de ~6° siguen pendientes.

La auditoría de cierre `auditoria-cierre.json` comprobó **149 archivos preexistentes ajenos a las ediciones intactos**, ninguna ausencia, rama/HEAD iguales y únicamente Estado-y-Plan, SiguienteSesion y README modificados intencionalmente entre los archivos de inicio. Además: 312 verificaciones de imagen por hash entre las cuatro condiciones, 339 cámaras registradas con K/D exactas, 19.031 verificaciones de equivalencia de archivos/pares y los ocho `images.bin` v6 coincidentes con sus hashes registrados. Las 71 anotaciones se mantienen exactamente. Sintaxis de los dos orquestadores y espacios del README comprobados; las máscaras finales se reproducen desde la fuente final. Las copias iniciales/corregidas siguen conservadas.
