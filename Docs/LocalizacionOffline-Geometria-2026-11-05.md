# Reconstrucción aprendida de Teología — 5-nov-2026

**Resultado: hay soporte 3D parcial de fachada con XFeat+LighterGlue, pero no un mapa global estable ni métrico.**
El ensayo restringido a fachada conserva 2.771 puntos y 48 vistas. Sin embargo, algunas conexiones entre sesiones
cambian decenas de grados al quitar el primer plano o cambiar la resolución. No corresponde escalar el conjunto actual
y presentarlo como un mapa validado. El siguiente trabajo de captura debe aportar una trayectoria continua con
traslación que conecte esas vistas; el núcleo parcial se conserva como referencia para comparar.

Trabajo sobre `localizacion-offline`, base `f84256fe0bbccd1acc00fce508c5a06a1039886b`. Se conservaron los cambios locales
de demo, oclusión y Unity. No se recompiló el APK, no se usaron poses Unity/GPS/Immersal para reconstruir y no se ejecutó
localización PnP ni ajuste multicuadro con este modelo de escala arbitraria.

## Datos y separación

- Construcción disponible: **182 imágenes**, 54 de Samsung A/B del 5-nov y 128 de las cinco sesiones históricas de
  Teología del 28-oct. Las históricas se copiaron a `salida/20261105/historicas-construccion/` y se verificaron con los
  SHA-256 de `salida/campus.json`; las copias nuevas ya verificadas se conservaron.
- Selección: **49 imágenes**, siete posiciones ordinales equiespaciadas por sesión, fijadas antes del matching.
  No se eligieron por resultado geométrico. Incluye una vista del suelo y vistas parcialmente tapadas: se conservan
  en la selección y no se les atribuye soporte de fachada. La regla se reprodujo exactamente con la CLI.
- D Samsung y E Xiaomi nuevos: **54 consultas reservadas**, sin extracción ni matching en esta etapa. Las sesiones
  históricas D/E sí pertenecen a construcción: sus fechas e identificadores se mantienen distintos.
- Teología J: **28 imágenes solo 2D**, excluidas del ensayo 3D. No se añadieron intrínsecos, tiempos ni poses.
- Se verificó separación por sesión, identificador y hash. La evaluación D/E ya fue explorada en el trabajo anterior;
  sigue sin ser una prueba final ciega. No se ajustó ningún parámetro con sus imágenes en este trabajo.

Todo lo aportado se considera el material completo. No se espera recuperar otras carpetas ni cámaras del dron.
La selección utiliza vistas de ese material, no las 182 imágenes exhaustivamente.

## Integración y condiciones

`sfm_aprendido.py` reutiliza `Features`, sus pesos, firmas y caché. Exporta características y correspondencias externas,
las importa con sus índices originales y ejecuta verificación geométrica de COLMAP antes del mapper. No vuelve a
emparejar con SIFT ni transforma inliers de homografía en tracks 3D confiables.

Condiciones compartidas: CPU, hasta 2.048 características, 1.176 pares exhaustivos de las mismas 49 vistas, semilla 5,
PINHOLE con intrínsecos registrados fijos, error de verificación de 4 píxeles originales y opciones completas guardadas.
Las coordenadas de características vuelven a la resolución original antes de SfM. Se desplazan características y
punto principal +0,5 px al entrar a COLMAP para preservar los rayos bajo sus convenciones de píxel.
El [formato oficial de COLMAP](https://colmap.github.io/database.html) documenta la convención y la distinción entre
matches crudos y geometría verificada; las operaciones empleadas pertenecen a la
[API de pycolmap](https://colmap.github.io/pycolmap/pycolmap.html).

**Los intrínsecos son un prior aproximado, no una calibración óptica validada.** La distorsión, rectificación y orientación
óptica de los registros siguen sin comprobarse físicamente. No se refinaron focales para presentar después ese ajuste
como calibración. No hay controles físicos, metros por unidad ni verdad de terreno.

Matching con PyTorch y reconstrucción con pycolmap se ejecutan en procesos separados por el conflicto de sus runtimes
OpenMP en este Mac. Los comandos bloquean conexiones de socket. Cada reconstrucción crea una base nueva y rechaza
reutilizar una existente. Se desactivó el fallback de registro sin estructura; aun así, COLMAP devolvió dos componentes
sin puntos 3D, que se identifican y **no se aceptan como reconstrucciones**.
Se conservó la relajación automática de inicialización de COLMAP cuando no encuentra un par inicial; los logs la
registran. No se interpreta conseguir un componente tras esa relajación como superar un criterio de aceptación.

Se ejecutaron tres extracciones/matchings: XFeat y ALIKED a 1024 px, y una prueba de sensibilidad de XFeat a 1440 px,
conservando el límite de puntos. Son **3.528 pares procesados**, no ensayos independientes de localización.
ALIKED no se repitió a 1440: la comparación entre métodos corresponde exclusivamente a 1024.

Para cada uno se reconstruyó el matching completo y otro filtrado por regiones manuales conservadoras de fachada:
se retiene un match solo si ambos extremos están dentro de esas regiones, sin cambiar sus índices. Las mismas
anotaciones se usan para ambos métodos y resoluciones. Se excluyen lámparas/personas identificables; suelo, barandas,
cielo y bordes no anotados quedan como desconocidos. Las regiones de vegetación son parciales, no una segmentación
exhaustiva ni verdad semántica independiente. Archivo versionado: `experimentos/teologia-regiones-20261105.json`.

## Resultado geométrico

Todos los modelos de esta tabla tienen **escala arbitraria**. La reproyección se expresa en píxeles **originales**,
no en los píxeles reducidos del ensayo SIFT anterior.

| Método / condición | Modelo | Vistas | Puntos 3D | Tracks etiquetados fachada | Fachada con observaciones en ≥2 sesiones | Reproyección media |
|---|---:|---:|---:|---:|---:|---:|
| XFeat 1024, completo | 0 | 46 | 5.092 | 2.648 | 2.466 | 1,433 px |
| XFeat 1024, solo fachada | 0 | 48 | 2.771 | 2.771 | 2.416 | 1,465 px |
| ALIKED 1024, completo | 0 | 22 | 2.048 | 327 | 307 | 1,203 px |
| ALIKED 1024, solo fachada | 0 | 21 | 296 | 296 | 295 | 1,224 px |
| ALIKED 1024, solo fachada | 1 | 12 | 118 | 118 | 115 | 1,092 px |
| XFeat 1440, completo | 0 | 46 | 4.832 | 2.115 | 1.962 | 1,370 px |
| XFeat 1440, solo fachada | 0 | 46 | 2.102 | 2.102 | 2.068 | 1,386 px |

ALIKED completo devuelve además un componente de 19 vistas y **cero puntos**; ALIKED solo fachada, otro de cuatro
vistas y **cero puntos**. Su error 0 no representa una buena reconstrucción. Los modelos válidos de ALIKED solo fachada
son componentes separados: no comparten una escala ni un marco registrado entre sí.

Un track se etiqueta como fachada/vegetación si tiene al menos dos observaciones de esa clase y son mayoría estricta
de sus observaciones. Los demás quedan desconocidos. En XFeat completo hay 554 tracks etiquetados vegetación y 1.890
desconocidos; en ALIKED completo, 726 de vegetación y 995 desconocidos. **Desconocido no significa vegetación.**

La comparación justa entre los dos extractores usa las mismas 49 vistas. El ensayo SIFT previo usaba solo las 54
imágenes A/B nuevas, sin estas históricas: la mejora sobre aquel ensayo no puede atribuirse solo al extractor.

## Fachada, tracks, paralaje y cobertura

La inspección de proyecciones muestra puntos en ventanas, marcos, aleros y algunas juntas de pared. En XFeat solo
fachada no quedan observaciones de vegetación por construcción. Es evidencia de soporte de fachada, no de una nube
dominada por el cubresuelo como en el ensayo SIFT anterior.

| Componente de fachada | Longitud mediana de track | Mediana del ángulo máximo por track |
|---|---:|---:|
| XFeat 1024, modelo 0 | 5 vistas | 16,57° |
| ALIKED 1024, modelo 0 | 8 vistas | 17,88° |
| ALIKED 1024, modelo 1 | 8 vistas | 22,59° |
| XFeat 1440, modelo 0 | 6 vistas | 18,07° |

Los ángulos proceden de las cámaras y puntos **estimados**. No son una medida independiente de paralaje real y pueden
ser grandes en una conexión errónea entre ventanas repetidas. En XFeat completo, la mediana de vegetación es 2,33°:
la mayor parte de esos tracks sigue aportando poca base geométrica.

La cobertura se cuantificó con una cuadrícula 12×8 sobre cada imagen. Se cuentan celdas cuyo centro pertenece a la
región anotada y contienen alguna observación de fachada; no se usa un hull que rellene huecos ni se interpreta el
recuento como porcentaje de superficie física. Ejemplos centrales fijados por posición en la serie:

| Vista de construcción | XFeat solo fachada: observaciones / celdas ocupadas | ALIKED solo fachada: observaciones / celdas ocupadas |
|---|---:|---:|
| Histórica E, `0018` | 198 / 16 de 22 | 83 / 14 de 22 |
| Histórica F, `0018` | 419 / 20 de 30 | 86 / 17 de 30 |
| Samsung B nuevo, `0014` | 790 / 29 de 43 | 171 / 22 de 43 |

Las paredes lisas siguen con huecos. Los puntos en marcos de ventanas repetidas no garantizan que se haya escogido
la ventana correcta en otra sesión. Las visualizaciones de tracks que conectan la histórica B con Samsung B nuevo
se concentran en una ventana: no ofrecen por sí solas una conexión distribuida entre todas las fachadas.

## Consistencia: por qué no se acepta el mapa global

Se compararon soluciones quitando su libertad de similitud: una Sim(3) ajustada a centros de cámaras comunes, sin
referencias físicas. Las rotaciones se compararon relativas a la cámara central de Samsung B nuevo, de modo que un
grupo mal colocado no pueda ocultarse rotando todo el modelo. La prueba sintética comprueba que un cambio puro de
escala, rotación y traslación produce discrepancia cero en esta comparación.

- **XFeat 1024 completo frente a solo fachada:** 46 cámaras comunes, diferencia mediana de centros de 7,78 % del
  diámetro entre cámaras. La histórica B cambia una mediana de **49,86°**; A nuevo, 4,09°; históricas C/D, 2,92°/2,66°.
- **XFeat completo 1024 frente a 1440:** la histórica B cambia una mediana de **45,98°**, con máximo 70,34°;
  A nuevo cambia 7,47° y C/D unos 7,9°. Más resolución no soluciona la ambigüedad global.
- **XFeat 1440 completo frente a solo fachada:** la histórica B todavía cambia una mediana de 16,66° y un máximo
  de 55,15°. Los recuentos de puntos y la reproyección baja no bastan para elegir una de estas soluciones.
- **Núcleo parcial B nuevo + históricas E/F:** XFeat y ALIKED solo fachada comparten 21 cámaras. Diferencia mediana
  de centros 0,43 % del diámetro, p90 0,82 % y máximo 1,75 %. Las medianas de rotación relativa por sesión son
  0,15°/0,84°/0,10°. Es el sector con mejor respaldo cruzado, sin acreditar exactitud física.
- **Históricas C/D:** el segundo componente de ALIKED solo fachada concuerda parcialmente con XFeat en 12 cámaras:
  mediana de centros 0,91 % del diámetro y medianas de rotación 0,67°/0,41°. ALIKED no lo conecta al núcleo anterior.

Estas diferencias son medidas de **estabilidad interna**, no errores respecto de un edificio medido. No se traducen a
centímetros. Las discrepancias grandes de B/A impiden aceptar el conjunto, aunque dos sectores se sostengan localmente.
No hay evidencia para seleccionar una conexión global como correcta solo por tener más puntos.

Se conservan también dos PLY de diagnóstico del núcleo B/E/F: 1.419 puntos XFeat y 290 ALIKED, seleccionados por
≥3 observaciones dentro del núcleo y ≥2 de sus sesiones. Son subconjuntos de los modelos de fachada, sin nueva
optimización, registro métrico ni cámaras listas para el localizador.

## Decisión y siguiente trayectoria de construcción

**No pasar el modelo global actual a la etapa de mapa métrico ni al localizador.** XFeat sigue siendo el candidato
principal por su soporte de fachada en esta comparación; ALIKED sigue como contraste. Hay progreso respecto del
fallo en vegetación, pero no una validación de todo Teología. La nueva captura necesaria debe resolver la conexión
entre sectores y las ventanas repetidas; repetir barridos rotatorios desde los mismos puntos no aporta esa evidencia.

Protocolo propuesto para una futura captura, no datos ya obtenidos:

1. Recorrer la vereda accesible junto a la fachada que muestran B nuevo y las históricas E/F, pasando por posiciones
   intermedias hasta enlazar con las vistas de A nuevo y B histórica. Mantener el edificio en imagen. En el cambio de
   cara, incluir vistas oblicuas intermedias con la esquina y rasgos de ambos lados a la vez. No hace falta recuperar
   ninguna carpeta anterior para realizar este recorrido futuro.
2. Tomar imágenes nítidas desde estaciones sucesivas **con traslación**, distribuidas por el frente completo y el enlace
   de esquina; como pauta inicial, pasos de 0,5–1 m y 70–80 % de solapamiento visual. Son valores de planificación que
   deben adaptarse a la vereda, no medidas inferidas de estos modelos. Mantener zoom/cámara/lente constantes.
3. Mantener pared, aleros, esquinas y detalles distintivos repartidos por el encuadre. Evitar que vegetación/barandas
   ocupen la mayor parte de la imagen; no depender de una sola ventana para enlazar grupos. En las estaciones de
   enlace, incluir varios rasgos reconocibles de cada lado. No cruzar barandas para conseguir otra base.
4. Hacer una segunda pasada por una franja accesible distinta si existe, conservando solapamiento y cambiando la
   posición de cámara. Documentar las estaciones y los cambios de tracking; las poses Unity siguen siendo telemetría
   hasta validar su relación con el sensor. La construcción debe ser una secuencia conectada, distinta de las consultas
   de cinco segundos desde un lugar.
5. Antes de convertir el resultado a metros, medir y documentar controles físicos distribuidos en ancho, altura y,
   cuando sea posible, profundidad, dejando otros independientes para comprobarlo. No usar la etiqueta «10/15 m»,
   las poses Unity, GPS ni una nube Immersal como sustituto de esos controles. Validar además intrínsecos/distorsión y
   orientación/sincronía de la imagen CPU por teléfono.
6. Repetir la prueba completo/solo fachada y entre métodos. Exigir conexiones distribuidas y geometría estable entre
   sectores; después verificar con los controles reservados. Solo entonces corresponde mapa métrico → PnP →
   multicuadro → medición Android. Harán falta nuevas consultas independientes para una evaluación final ciega.

Los objetivos de 10–20 cm, cobertura 10–25 m y arranque ≤5 s **siguen sin demostrarse**. No se mide latencia móvil a
partir del matching CPU ni se elige un motor Android definitivo.

## Artefactos, reproducción y validación

Salida local conservada, ignorada por Git: `Tools/LocalizacionOffline/salida/20261105/sfm-aprendido/`.

- `disponibles.json`, `seleccion.json`, `seleccion-reproducida.json`: copias verificadas y selección reproducida.
- `xfeat/`, `aliked/`, `xfeat-fachada/`, `aliked-fachada/`, `xfeat-1440/`, `xfeat-1440-fachada/`: características,
  matches, condiciones, bases SQLite, verificación de pares, opciones, modelos COLMAP binarios/texto, PLY y diagnósticos.
- `diagnostico.json` de cada ensayo, `tracks-diagnostico.json` por modelo y `consistencia-*.json`: resultados detallados.
- `fachada-comparada.png`, `tracks-xfeat-0-38.png`, `tracks-xfeat-fachada-0-38.png`, `observaciones/*.png` por modelo:
  evidencia visual. Cian = región/puntos de fachada; magenta = vegetación anotada; amarillo = desconocidos en los
  ensayos completos. Las vistas previas pueden girarse; originales y coordenadas no cambian.
- `nucleo-fachada.ply` y su JSON en las dos salidas de fachada a 1024: subconjuntos parciales descritos arriba.
- `pruebas.log`: **13 pruebas Python aprobadas**, incluidas importación externa, rechazo de consultas, descarte de
  coincidencias sintéticas falsas, recuperación de cámaras conocidas, convención de píxel y comparación invariante
  ante similitud. No son pruebas de precisión física.
- `fuentes/` y `fuentes-sha256.json`: herramientas, manifiesto de pesos y copia exacta del script usado para los tres
  matchings, cuyo hash coincide con sus condiciones guardadas. `auditoria-final.json` verifica las seis bases y la
  conservación de los 53 archivos locales preexistentes incluidos en el control inicial.

La herramienta versionada y los comandos para repetir están en el [README del banco](../Tools/LocalizacionOffline/README.md).
La prueba sintética usa las mismas funciones de importación y mapper que los ensayos reales. Se comprobaron los
hashes de los 53 archivos locales ajenos incluidos en el control inicial. No se modificaron assets ni configuración de Unity y no
había una razón concreta para recompilar el APK.
