# Teología: corrección de asociaciones entre ventanas — 6-nov-2026

**Trabajo ejecutado en PC: filtro de identidad física, revisión visual independiente con 6.1 Terra, 34 reconstrucciones exploratorias y cuatro réplicas finales.** Se redujo una discrepancia de unos 94–98° entre métodos para una cámara, sin quitarla del mapa. El acuerdo entre XFeat y ALIKED mejora mucho, pero todavía hay tracks contradictorios y sensibilidad óptica: **no se acepta precisión física, no se aplica escala y no se integra un localizador Android**. La revisión independiente no equivale a validación independiente del modelo ni revisión exhaustiva de todas las anotaciones.

Salida nueva `Tools/LocalizacionOffline/salida/20261106-identidad-teologia/`. Los ensayos ópticos anteriores quedan intactos en `20261106-optica-teologia/`. Mismas 71 imágenes de construcción, hashes, características originales y particiones; no se incluyeron consultas ni tableros. No se usaron poses Unity, GPS, etiquetas de distancia o Immersal como verdad. Rama/HEAD y cambios locales conservados; sin reset, cambio de rama, worktree, commit, push, pull, Unity, APK o adb. **No hizo falta internet ni instalar dependencias.**

## Qué se implementó

`experimentos/identidad_teologia_20261106.py` asigna identidades físicas a regiones de las fotos. P = extremo con puerta central, banco/palmera y luminaria; D = extremo opuesto de dos columnas de ventanas; L = lateral largo. Los IDs distinguen columnas y ventanas superiores: identificar la cara sola no basta.

La anotación se hace sobre la vista girada 90° horario para inspección, pero el PNG/keypoints quedan crudos. Se usa `(u,v)=(H−1−y,x)` y su inversa; el +0,5 de COLMAP sigue entrando/saliendo solo en el límite existente. Un match se rechaza cuando sus dos extremos tienen identidades conocidas incompatibles. **Desconocido permanece desconocido y se conserva**, no se interpreta como falso. No se borran características ni imágenes.

Las bandas amplias de cara solo limitan el espacio elegible: **no etiquetan puntos por sí solas**. Se encontró que las máscaras antiguas de fachada incluían barandas, árboles y edificios del fondo. Por eso se restringió la nueva identidad a ventanas superiores visibles, sin usar ventanas inferiores ocluidas para esta corrección. No se afirma que los restantes puntos desconocidos sean del edificio.

Se revisaron láminas completas y keypoints efectivos de ambos métodos. Las anotaciones v1–v3 preliminares se conservaron; **ninguna se usó para reconstruir**. La v4 tiene 69 regiones parciales, congeladas antes de sus ensayos. La v5 corrige cinco límites tras revisar caminos ambiguos; la v6 añade una ventana de una cámara concreta. Todas las versiones y modelos se conservan. Son intervenciones dirigidas de desarrollo, **no evaluación ciega ni evidencia independiente de exactitud**.

## Experimentos y resultados negativos conservados

Se ejecutaron las mismas condiciones de cámara para cada método: baseline registrado y radial candidata **solo del 15T**. El Xiaomi 14 conserva exactamente su configuración. Se verifican de nuevo geometría y mapper, intrínsecos/distorsión fijos, semilla 5, cuatro hilos y límite 300 s; no se reextraen características ni se cambian poses a mano.

1. **Filtro por pares, v4: cuatro reconstrucciones.** Retira 13.932/109.250 correspondencias XFeat y 6.741/53.004 ALIKED. XFeat registra 64 cámaras; ALIKED, 61 incluso con radial, que antes se fragmentaba. Aún aparecen 125/131 tracks conflictivos XFeat y 57/59 ALIKED: una cadena mediante observaciones desconocidas puede volver a unir identidades distintas.
2. **Cuarentena de tracks observados: cuatro reconstrucciones individuales.** Se aíslan todas las observaciones de cada track con identidades incompatibles; esto también retira asociaciones potencialmente correctas. Los hashes de selección, anotación, máscara y cada NPZ de keypoints impiden transferir índices de un método a otro.
3. **Cuarentena común por método: diez reconstrucciones.** Se unen las observaciones conflictivas de baseline/radial y se entrega a ambas la misma lista de exclusiones. Dos rondas en ambos métodos y una tercera solo en XFeat. Se verifica igualdad numérica de todos los pares de entrada/salida y de features; solo pueden diferir los campos ópticos explícitos del 15T. ALIKED llega a cero conflictos conocidos en la segunda ronda, pero las geometrías entre métodos todavía discrepan hasta ~180°. XFeat conserva 6/18 conflictos en la tercera ronda. **No se adopta la cuarentena como solución ni se elige el modelo con menos conflictos.**
4. **Grafo de inliers antes del mapper: ocho reconstrucciones.** Se construye la unión de inliers aceptados bajo ambas ópticas y se compara esa misma unión con otra que aísla componentes conectados con identidades incompatibles. XFeat pasa de 77.667 a 7.311 aristas; ALIKED de 35.626 a 9.948. Un componente XFeat contiene 27.830 características de 65 imágenes y seis identidades: aislarlo pierde muchas asociaciones válidas junto con las falsas. La versión sin conflictos queda reducida a componentes de 6/10 cámaras XFeat o 15/12/8/6 ALIKED. **Cero conflictos no equivale a un mapa útil.** No se suman componentes ni se adopta esta pérdida de cobertura como mejora.
5. **Correcciones visuales v5 y v6: ocho reconstrucciones más cuatro réplicas finales.** Se corrigen límites de cinco regiones y después se anota una ventana antes desconocida, manteniendo las 71 vistas. Se describe el resultado final abajo.

Los componentes nativos pueden solaparse. Por ejemplo, ALIKED en la segunda ronda común genera uno de 15 y otro de 61 cámaras; **las 15 están dentro del de 61**, no son un edificio de 76 cámaras ni necesariamente dos regiones desconectadas. Para las comparaciones se fija previamente el componente con más cámaras, desempate por índice nativo, conservando todos los demás. Si falta el ancla común `142623/0001`, se registra como no comparable; no se elige otro componente para obtener una cifra mejor.

## Puentes falsos identificados y corrección dirigida

`revision-puentes/` conserva el script, ocho láminas, caminos por índices y un manifiesto SHA-256. Se revisaron visualmente cinco láminas; las demás son candidatas, no enlaces ya aprobados/rechazados. Ejemplos confirmados por la revisión y por inspección del agente principal:

- XFeat: `matches-15-27.npz`, índices `(92,17)`, une la esquina de una ventana P con otra del extremo D. El punto de la imagen 27 estaba apenas fuera del rectángulo conservador D/U1.
- ALIKED: `matches-3-69.npz`, `(221,510)`, une D/U1 con D/U2. El punto de 69 estaba justo debajo del límite de su ventana.
- XFeat: `matches-38-64.npz`, `(158,337)`, y `matches-0-64.npz`, `(22,337)`, enlazan ventanas de caras D/P diferentes.
- ALIKED: los caminos desde D/U2 a `142828/0111` y a la ventana corta de L muestran otra unión físicamente equivocada; la esquina de esta última se reconoce en la foto completa.

La v5 amplía cinco regiones según esas fotos. Se inspeccionan todos los puntos recién etiquetados en ambos métodos, no solo el índice que motivó la revisión. La ampliación de la imagen 64 se limita a un parche pequeño junto a la esquina para no etiquetar toda la pared inferior.

Con v5 seguía destacando **`142828/0111` (índice 68)**: diferencia XFeat/ALIKED 98,44° baseline y 94,39° radial. La foto muestra inequívocamente la ventana superior corta del lateral L; esa vista no tenía elemento anotado. La **v6 añade solo esa región** y vuelve a reconstruir, sin retirar la cámara, modificar su pose ni utilizar medidas de cinta. El cambio de pose estimada en ALIKED se produjo tras filtrar correspondencias con esa anotación dirigida; no se impuso una orientación manual ni se identificó qué pose era físicamente correcta.

## Resultado final v6

| Método | Cámara | Cámaras registradas / puntos | Reproyección media, píxeles originales |
|---|---|---|---:|
| XFeat | Baseline | 64 / 6.933 | 1,315 |
| XFeat | Radial 15T | 64 / 6.913 | 1,318 |
| ALIKED | Baseline | 61 / 2.749 | 1,161 |
| ALIKED | Radial 15T | 61 / 2.758 | 1,162 |

El filtro v6 retira **14.536/109.250** enlaces XFeat y **7.303/53.004** ALIKED. Las correspondencias de entrada son iguales entre ambas ópticas dentro de cada método. La verificación epipolar todavía puede producir distintos inliers porque K/D cambian; eso es parte del ensayo.

Sobre **61 cámaras comunes**, usando todos sus centros en la similitud y la misma ancla:

| Comparación entre métodos | Centro mediana / p90 / máximo, % del diámetro | Máxima diferencia angular |
|---|---|---:|
| v5 baseline | 0,734 / 1,282 / 39,177 | 98,44° |
| v6 baseline | **0,295 / 0,571 / 0,995** | **3,52°** |
| v5 radial | 0,713 / 1,300 / 38,891 | 94,39° |
| v6 radial | **0,293 / 0,568 / 0,956** | **3,45°** |

Esto mejora la estabilidad interna y elimina la discrepancia extrema de esa vista en esta comparación. **No son centímetros ni prueba de que las dos soluciones coincidan con el edificio real.** La anotación fue dirigida después de diagnosticar el fallo; se conserva el antes/después y no se presenta como validación independiente.

La sensibilidad óptica sigue abierta: baseline frente a radial v6 cambia como máximo **6,10° en XFeat** (`143744/0068`) y 1,97° en ALIKED. Hay aún **128/121 tracks con identidades conocidas incompatibles en XFeat y 55/57 en ALIKED**: las regiones son parciales y quedan otros caminos por desconocidos. Estas cuentas cambian también con la cobertura de anotación; no son una tasa de error comparable ciegamente entre versiones. **No se acepta el mapa para localización física ni se elige una calibración/motor por estas cifras.**

Cuatro réplicas desde bases nuevas repiten v6 con exactamente las mismas imágenes, características, pares y opciones. **Tres condiciones reproducen la geometría hasta diferencias numéricas mínimas, pero XFeat radial encuentra otra solución:** comparte 64 cámaras con la primera corrida y difiere hasta **14,64 % del diámetro de centros** en `143744/0001` y **3,75°** en `143744/0068`, con mediana de centros 0,332 %. La réplica produce 6.848 puntos frente a 6.913 iniciales. Esto mantiene abierta la ambigüedad aun con la misma semilla; no se atribuye a una nueva calibración ni se escoge la réplica por reproyección. Sus comparaciones completas están en `comparaciones-cierre/` y `resumen-comparaciones.json`. El descenso de discrepancia entre métodos de la tabla anterior corresponde a las primeras corridas v6, no acredita estabilidad entre todas las ejecuciones.

## Medidas: bordes ahora confirmados en fotos

Gael confirmó **«Sí, las cuatro coinciden»** al revisar las líneas verdes de `aristas-interiores-propuestas.jpg`. Se guarda aparte `aristas-interiores-confirmadas.json`, con imagen/hash, segmentos propuestos en píxeles de presentación y su conversión a píxeles crudos. El borrador anterior se conserva.

| Medida | Ventana identificada en la foto | Papel ya fijado |
|---|---|---|
| INTERIOR-C-D = 2,70 m | `143744/0011`, baja junto a la puerta | Única longitud de ajuste exploratorio |
| INTERIOR-R-S = 6,20 m | `143744/0049`, larga baja | Reservada |
| INTERIOR-H-I = 2,70 m | `143744/0116`, primera baja del lateral, derecha de la esquina en la lámina | Reservada |
| INTERIOR-VUELTA-FRENTE = 1,95 m | `143744/0116`, baja a la izquierda de esa esquina | Reservada |

Los segmentos identifican **aristas interiores**, no una nueva confirmación de altura de huincha ni puntos XYZ. No se interpreta el borde de hormigón como extremo de ventana. Falta asociar esas mismas aristas en varias vistas y reconstruirlas con cámaras suficientemente fiables; no se escoge un punto 3D cercano por conveniencia. Repeticiones, incertidumbre y altura exacta de la huincha siguen no informadas. **No se calculó ni aplicó escala, y ninguna medida de ajuste se presenta como validación.** Las tres reservas son controles exploratorios conocidos durante desarrollo; no son evaluación ciega ni sustituyen una prueba final independiente.

## Pruebas, procedencia y continuación

**19 pruebas Python aprobadas.** Las tres nuevas comprueban orientación, jerarquía de etiquetas, desconocidos, conservación de índices/selección, rechazo de sobrescritura, cuarentena de una cadena A–desconocido–B, rechazo de informes de otro dominio de características, unión con observaciones comunes/exclusivas, pares numéricamente iguales y diferencias ópticas solo del 15T. También verifican componentes ambiguos independientemente del orden y el intercambio real de columnas al leer IDs invertidos en la base COLMAP.

`auditoria-grafos.json` comprueba las ocho condiciones de grafo: **22.223 puntos 3D**, cada track dentro de un solo componente de entrada y cada inlier nuevo subconjunto de las correspondencias suministradas; la rama que aísla componentes no reintroduce los conflictos conocidos. Esto comprueba el operador de filtrado, no la geometría física. La auditoría final conserva hashes, cámaras fijas, fuentes y separación de consultas.

`auditoria-final.json` verifica las **38 reconstrucciones**, 136 rutas de PNG conservadas (incluye copias, no 136 capturas únicas), 79.378 comprobaciones de archivos de características/pares y 2.281 cámaras registradas con sus parámetros exactamente fijos. Los 120 archivos locales ajenos a estas ediciones conservan sus hashes; solo se actualizaron intencionalmente Estado-y-Plan, Medidas y el README entre los archivos preexistentes. Todas las entradas conservan las 71 vistas y exclusiones de consulta; el Xiaomi 14 mantiene su configuración. Rama `localizacion-offline` y HEAD `f84256fe0bbccd1acc00fce508c5a06a1039886b` permanecen iguales. Sintaxis, JSON, espacios y el diff del README comprobados.

Las herramientas nuevas son `identidad_teologia_20261106.py`, `cuarentena_comun_20261106.py` y `grafo_identidad_20261106.py`; no se modificó el mapper anterior ni la app. `fuentes-finales/`, los scripts `ejecutar_*.py` y los resúmenes guardan la reproducción de las 38 condiciones. Para repetir un filtro sin sobrescribir, desde raíz:

```sh
PY=Tools/LocalizacionOffline/.venv/bin/python
T=Tools/LocalizacionOffline
SRC=$T/salida/20261106-optica-teologia/71-xfeat-radial_k1
RUN=$T/salida/otra-identidad-xfeat-radial
$PY $T/experimentos/identidad_teologia_20261106.py filtrar --source "$SRC" --annotations "$T/experimentos/teologia-identidades-v6-20261106.json" --output "$RUN"
$PY $T/sfm_aprendido.py reconstruir --output "$RUN" --runtime 300
$PY $T/experimentos/identidad_teologia_20261106.py inspeccionar --source "$RUN" --annotations "$T/experimentos/teologia-identidades-v6-20261106.json"
$PY -m unittest discover -s "$T" -p 'test_*.py' -v
```

Repetir con fuentes `71-xfeat-baseline`, `71-aliked-baseline` y `71-aliked-radial_k1`, siempre salidas distintas. Los operadores común/grafo tienen CLI propia y rechazan fuentes incompatibles. Las versiones de anotación de cada ensayo no se sustituyen por v6 al reproducir su historia.

**Siguiente trabajo concreto:** revisar los tracks y su distribución/profundidad en `143744/0001` y `/0068` comparando ambas ejecuciones radiales, conservar la cámara y determinar qué asociaciones o degeneración sostienen sus soluciones distintas. Extender la identificación visual a los otros puentes contradictorios, usando el material conservado. Después asociar las aristas confirmadas en varias vistas; con geometría aceptable, ajustar solo C–D y evaluar las tres reservas exploratorias. La consulta real requiere además adaptar importación/triangulación/PnP al contrato K/D crudo, todavía no implementado en el localizador. Android conserva Immersal; los cuatro candidatos y la validación óptica/temporal del multicuadro siguen abiertos. No hace falta otra captura general ni instalar Unity/dependencias para iniciar ese trabajo.
