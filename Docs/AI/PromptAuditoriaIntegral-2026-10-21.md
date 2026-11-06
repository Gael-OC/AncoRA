# Prompt para auditoría integral de AncoRA en modo Plan

Copia desde «Actúa…» hasta el final y úsalo en modo Plan con este repositorio abierto.

---

Actúa como auditor técnico senior de Unity, Android y realidad aumentada exterior, con experiencia en AR Foundation, ARCore, Immersal/VPS, localización visual, registro de mapas y rendimiento móvil. Investiga el proyecto real y fuentes primarias en internet. Trabaja en español.

En esta fase debes investigar y entregar un plan completo, concreto y listo para implementar. No implementes correcciones ni cambies código, paquetes, escenas, prefabs, mapas o ajustes del proyecto. No te quedes en una lista genérica de buenas prácticas: necesito conclusiones fundamentadas, decisiones de arquitectura y experimentos que resuelvan las incertidumbres.

## 1. Objetivo y prioridades del producto

AncoRA es un paseo AR del campus UCN con cuatro edificios: Ciencias Básicas, X1, Teología y EIC. Antes de añadir modelos 3D definitivos, necesitamos una base fiable, rápida y pulida.

Hoy hay que apuntar a un edificio, esperar varios segundos y después aparece su contenido. Ha mejorado respecto a versiones anteriores, pero la experiencia sigue siendo lenta y obliga a ir edificio por edificio. La meta es que el teléfono se ubique en el paseo y pueda colocar rápidamente varios edificios conocidos, incluso observados desde lejos, manteniendo sus posiciones al girar y caminar. No confundas tener los archivos cargados con haber localizado el teléfono o situado correctamente todo el entorno.

Prioridades:

1. Recuperar las cajas de antes como representación de trabajo: en la versión actual las cambiaron y funcionan peor. Compara las releases para identificar y proponer restaurar tanto su aspecto/comportamiento como su visibilidad normal. Revisa por separado el cambio de shader y la sustitución de cajas por primitivas con máscaras; no asumas que son el mismo problema. Conserva mejoras de reconocimiento que estén demostradas.
2. Diagnosticar la espera y la necesidad de localizar cada edificio individualmente. Recomendar una arquitectura que permita situar el conjunto del paseo con una localización fiable, si los datos y la precisión lo permiten.
3. Investigar por qué un compañero no pudo instalarla en un POCO X3: recuerda un mensaje de revisión o verificación de seguridad fallida, sin texto exacto disponible.
4. Revisar el resto de la app para preparar una versión estable antes de integrar modelos definitivos.

No prometas reconocimiento instantáneo desde cualquier distancia. Explica los límites físicos, de cobertura visual y de hardware, y fija objetivos medibles.

## 2. Baseline que debes verificar y actualizar

Repositorio: `https://github.com/Gael-OC/AncoRA`.
Ruta de este Mac: `/Users/gael/Documents/GitHub/AncoRA`.

Al preparar este prompt se comprobó:

- Rama `nicowo`, commit `3cabb6d5087fc2ecc5c5f9498bc2b38019fc50de`, tag `paseo-2026-10-21-3`.
- Última publicación consultada: prerelease **AncoRA Paseo 1.3.3 — oclusión y ajuste de cajas en terreno**, publicada el 21-10-2026 a las 18:17, hora de Chile. URL: `https://github.com/Gael-OC/AncoRA/releases/tag/paseo-2026-10-21-3`.
- APK: `AncoRA-Paseo-2026-10-21-3.apk`, 72.747.499 bytes. SHA-256: `861a26385e9d973e3f1086b32c01d76eff3a8221a62db649a0dada4e48e04c39`.
- El APK publicado se descargó y examinó con `aapt` y `apksigner`: paquete `com.ancora.ucnar.paseo`, versión 1.3.3, código 15, **minSdk 34 (Android 14)**, targetSdk 36, solo `arm64-v8a`. Firma v2 válida con certificado `Android Debug`. Eso no demuestra que Play Protect o el instalador de Xiaomi lo acepten.
- Unity `6000.6.0f1`; Immersal Core 2.4.0; AR Foundation/ARCore 6.5.0; ARCore Extensions local 1.56.0. Comprueba versiones efectivamente resueltas: el manifest declara URP 17.0.3, pero el lock registra 17.6.0. No declares un defecto de compatibilidad solo por esa diferencia.
- Escena: `Assets/Scenes/PaseoIngenieria.unity`. Cuatro edificios, siete mapas activos. Cada mapa tiene un XR Space independiente; el contenido se muestra tras localizar ese mapa. Revalida el flujo completo.
- `PaseoSearchPlan` busca un edificio por intento, prioriza el ya localizado a la vista y luego proximidad GPS, con ciclo de rangos `0,1,0,2,0,todos`. Sin GPS ni edificio ubicado busca todos. GPS aceptado hasta 50 m de precisión aunque los edificios pueden estar a 21–60 m: evalúa esa incertidumbre.
- `PaseoPace` solicita intervalos de 0,5 s buscando y 2 s una vez asentado. Esos intervalos no garantizan esa frecuencia real si el intento tarda varios segundos.
- El filtro actual permite una pose inicial provisional y confirma con otra consistente. No repitas como hecho vigente la antigua explicación de que siempre espera dos coincidencias para mostrar la caja.
- 1.3.1 activó reemplazos geométricos en X1, Teología y Ciencias Básicas; EIC conserva la caja. 1.3.3 agregó oclusión con profundidad, máscara de dos pasadas, shader de caja con `ZTest Always` y cajas visibles durante ajuste.
- La release declara 252 pruebas EditMode, humo y validación de escena aprobadas por quien la publicó. Es evidencia histórica, no pruebas ejecutadas por ti ni garantía de precisión física. La oclusión real se declara pendiente de comprobar en teléfono.
- Unity CLI está instalado; versión observada `1.0.0-beta.13`. `unity pipeline list --json` identificó el proyecto sin Pipeline instalado y sin servidor accesible. `unity status` devolvió `STATUS_NO_INSTANCES`, con advertencia de que el sandbox podía ocultar procesos. No concluyas por eso que Unity está cerrado.

Comprueba si existe una release más reciente, incluyendo prereleases, y diferencia rama local, remoto, tag, commit y APK instalado. No audites únicamente `main`. Compara al menos 1.2.2 (`paseo-2026-10-14-2`), 1.2.4 (`paseo-2026-10-21`), 1.3.2 y 1.3.3. El commit `090e6ef` contiene el cambio reciente de oclusión y shader.

## 3. Reconocimiento del proyecto y herramientas reales

Lee instrucciones aplicables, `CLAUDE.md`, `Docs/GuiaPrueba-2026-10-21.md`, el informe histórico `Docs/AI/RevisionProyecto-2026-10-13.md` y las especificaciones/planes de `Docs/superpowers/`. Contrasta siempre la documentación con código, datos y APK: hay información histórica que ya cambió.

Usa las habilidades Unity disponibles de auditoría, diagnóstico, onboarding y validación según corresponda. Inspecciona código propio antes que código de terceros. Sigue, al menos:

- `Assets/AncoRA/Editor/Paseo/PaseoSetup.cs`, `PaseoSmokeTest.cs`, `PaseoReplacementPreview.cs`.
- `Assets/AncoRA/Scripts/Paseo/`: `PaseoTour`, `PaseoLocalization`, `PaseoSearchPlan`, `PaseoMapLoad`, `PaseoSdkStart`, `PaseoPoseFilter`, `PaseoPoseProcessor`, `PaseoLocalizationSettle`, `PaseoVisibility`, `PaseoPace`, `PaseoMapContent`, `PaseoReplacement`, persistencia, UI y diagnóstico.
- `Assets/AncoRA/Scripts/EdificioFacadeFrame.cs`, shaders/materiales en `Assets/AncoRA/Paseo/_Materiales/`, los cuatro `edificio.json`, mapas `.bytes`, escena y configuración XR/URP/Android.
- Pruebas existentes en `Assets/AncoRA/Editor/Tests/`; `Tools/AnalizarRegistro.js`, `Tools/BajarRegistro.ps1`, `Tools/UnityBatch.ps1`, `Tools/RegistroDron/` y datos de `Escenas/`.

Usa **Unity CLI realmente**: consulta ayuda y versión, identifica el proyecto por ruta completa, revisa `unity pipeline list --json`, `unity status --project-path <ruta> --json` y descubre comandos de lectura disponibles si hay conexión. Inspecciona consola, jerarquía, componentes, referencias, sesión AR, configuración de cámara y renderer cuando la conexión lo permita.

Si falta Pipeline/MCP, documenta el límite y continúa la investigación de archivos y artefactos. No instales un bridge o paquete durante Plan. No cierres un Editor ni abras batchmode sobre el mismo proyecto bloqueado. Prepara comandos compatibles con el sistema operativo real; no copies rutas Windows en este Mac. Los métodos `Prepare`, `ApplyFieldAdjustment` y `BuildAndroid` modifican estado: no los ejecutes como si fueran comprobaciones de lectura. Para la fase ejecutable, distingue comprobación de entradas, validación, carga nativa, humo, tests y build, con prerequisitos y salidas esperadas. No des por ejecutada una prueba porque existe un comando.

## 4. Diagnóstico completo de localización y demora

Traza el recorrido: apertura → permisos → AR tracking → inicialización SDK → carga de mapas → captura de imagen → búsqueda → match → filtro/confirmación → actualización de espacios → visibilidad → primer frame mostrado.

Separa tiempos de inicio, espera de planificación, búsqueda fallida, búsqueda acertada, filtrado y render. Investiga:

- Búsqueda del edificio más cercano frente al edificio realmente observado; inanición de otros candidatos; recuperación sin GPS, GPS impreciso/antiguo, varios edificios a la vista y cambios de dirección.
- Coste real de buscar los siete mapas, subconjuntos y mapa único; imagen completa frente a 960×720; detalles lejanos, intrínsecos tras redimensionado, orientación y movimiento entre captura y resultado.
- Concurrencia nativa, propiedad/vida útil de buffers de cámara, cancelación, tareas que sobreviven a pausas/reinicios, resultados antiguos y ausencia de solapamiento inseguro de inicializaciones/intentos.
- Confianza, falsos positivos, pose provisional, confirmación, rechazo de outliers, nivelación, suavizado, deriva, alternancia entre mapas y recuperación tras perder tracking. No ocultes errores con esperas o reintentos arbitrarios.
- Coste de logs, JPG/fotos cada 3 s, escritura, compresión y compartición, GC, CPU/GPU, memoria y calentamiento. Separa perfil de desarrollo del perfil final.

Analiza registros reales disponibles antes de atribuir causas. Da hipótesis priorizadas con evidencia a favor/en contra y un experimento que distinga cada una. Si faltan datos de terreno, especifica una captura mínima útil; sigue con todo lo investigable sin bloquearte por ellos.

## 5. Decisión de arquitectura: localizar el paseo completo

Responde expresamente: **¿basta optimizar la búsqueda actual, necesitamos alinear mapas existentes en un marco común, capturar un mapa del recorrido, dividirlo en sectores conectados o cambiar de tecnología?**

Compara como mínimo:

1. Arquitectura actual mejorada, como referencia de coste/beneficio.
2. Mapas Immersal registrados en coordenadas comunes del campus, colocando todo el contenido conocido desde una localización validada y manteniéndolo con tracking AR/relocalización.
3. Un mapa continuo del paseo o sectores con solape, alineación/stitching y transiciones controladas; no presupongas que un mapa gigantesco será más rápido o robusto.
4. Geospatial/VPS o solución híbrida como alternativa si existe cobertura, compatibilidad y precisión suficientes en el campus. GPS/brújula pueden ayudar a seleccionar o mostrar orientación aproximada; no los presentes como pose precisa de fachada sin medir.

Para cada opción indica precisión, latencia, cobertura desde lejos, deriva, consumo, uso offline, coste/licencia, dependencia externa, mantenimiento de mapas y esfuerzo de migración. Recomienda una principal y una alternativa; propone una prueba pequeña que permita decidir antes de una reescritura.

Examina si los registros y nubes del dron permiten relacionar **todos** los mapas y edificios en un marco consistente. Revisa escala, handedness, ejes, yaw, altura, offsets entre vuelos y calidad/ambigüedad de cada transformación. Los datos del dron se usan actualmente para colocar cajas; tenerlos no demuestra una alineación global precisa. Mide cómo un error angular se amplifica a distancia y cómo se propaga la incertidumbre de un mapa a todos los edificios.

Define la transformación entre mapa, campus y sesión AR; qué raíz se mueve, cómo evitar doble transformación, saltos y correcciones contradictorias; validación cruzada entre edificios; cuándo mostrar todo, cuándo mantener posición y cuándo ocultar o indicar incertidumbre. No sitúes contenido como preciso antes de tener una pose fiable.

Diferencia **modelo 3D visual**, **malla de oclusión** y **mapa visual de localización**: construir uno no sustituye automáticamente a los otros. Si hace falta recapturar, especifica ruta, vistas a altura de peatón, distancias, desplazamiento/paralaje, solapes, iluminación, zonas de conexión y método de validación. Justifica el valor de capturas de dron frente a las vistas reales del teléfono.

## 6. Recuperar y verificar las cajas anteriores

Determina el último estado adecuado a partir de tags/diffs y evidencia visual disponible; usa la 1.2.4 como referencia de comparación anterior a los reemplazos, sin darla por perfecta. Describe qué habría que restaurar de `EdificioFacadeFrame`, materiales, transparencia, bordes, caras, profundidad, culling, orden de render, configuración `reemplazo.activo` y visibilidad.

Investiga `ZTest Always`, ZWrite, stencil, cámara AR y `AROcclusionManager` por separado. Comprueba artefactos entre cajas, objetos cercanos, fachadas, transparencias y teléfonos sin Depth. No propongas revertir toda una release para arreglar la representación.

Separa errores de localización, calibración de caja y render. X1 tiene un desfase de caja documentado; revisar también la altura de EIC, el bloque representado y las correcciones de Ciencias Básicas. Conserva ajustes de terreno y su procedencia. Define comparación visual repetible desde varios ángulos y distancias y una prueba de que regenerar la escena no reintroduce el cambio rechazado.

## 7. POCO X3: instalación, seguridad y compatibilidad

No atribuyas el fallo a Play Protect ni a falta de potencia sin evidencia. Confirma variante exacta (X3, X3 NFC o X3 Pro), modelo, Android/API, MIUI/ROM, ABI, certificación y servicios AR. Si el teléfono usa API inferior a 34, el APK actual ya es incompatible por minSdk; separa ese hecho del mensaje recordado.

Examina el APK publicado, no solo Player Settings: checksum, ZIP, manifest fusionado, min/target SDK, ABI, firma/certificado, permisos, features requeridas, AR Required/Optional y Depth opcional. Compara certificado y package/versionCode con la instalación anterior y entre builds de distintos computadores.

Prepara un árbol diagnóstico para `INSTALL_FAILED_OLDER_SDK`, `UPDATE_INCOMPATIBLE`, downgrade, ABI incompatible, parseo/corrupción, espacio insuficiente, permiso de instalar desde esa fuente, verificación MIUI/Play Protect y disponibilidad de ARCore. Obtén el texto/código exacto y logs del instalador cuando sea posible; explica qué evidencia confirmaría cada causa.

Comprueba la compatibilidad oficial por variante. La lista de Google consultada al preparar el prompt incluye X3 y X3 NFC sin marca de Depth, y X3 Pro con Depth: revalídala. Poder instalar, tener ARCore y soportar Depth son comprobaciones diferentes.

Investiga el mínimo Android realmente requerido por Unity, paquetes y APIs propias; si conviene reducir minSdk, especifica guards/fallbacks y pruebas necesarias. No lo bajes a ciegas. Propón firma de distribución estable y actualizaciones entre computadores sin pérdida de ajustes. No recomiendes desactivar verificaciones de seguridad como solución; una firma APK válida tampoco garantiza aprobación de seguridad.

No desinstales ni borres PlayerPrefs. La documentación establece que la app se abre manualmente: no la lances con `adb monkey` ni otro comando. Si se requiere una instalación diagnóstica después de Plan, exporta primero ajustes y explica cualquier operación con pérdida de datos.

## 8. Investigación web de soluciones y apps similares

Investiga documentación actual, repositorios oficiales y experiencias comparables: **Lens/Live View de Google Maps**, **ARCore Geospatial**, ejemplos y proyectos de **Immersal Multi Map/alineación/stitching**, y al menos dos experiencias reales adicionales de navegación AR, turismo, patrimonio o campus. Puedes evaluar otros proveedores de VPS, verificando primero su vigencia; no presupongas que una oferta histórica sigue operativa.

Para cada caso aporta enlace directo, fecha de consulta, qué hace, cómo obtiene su pose si está documentado, cobertura y limitaciones, idea aplicable a AncoRA y esfuerzo. Si el mecanismo interno no es público, decláralo desconocido. Distingue demostración publicitaria de evidencia técnica y producto de consumo de API reutilizable.

Estudia mapeo previo, coordenadas compartidas, relocalización, caché, carga progresiva, oclusión, modo degradado y guía visual al usuario. No copies la obligación de escanear edificio por edificio si contradice nuestro objetivo. Verifica cobertura del campus, región, versiones de SDK, dispositivos, conectividad, cuotas y costes antes de proponer migraciones.

Fuentes iniciales, que debes verificar y ampliar:

- `https://developers.immersal.com/docs/mapsmapping/howtomap/`
- `https://developers.immersal.com/docs/mapsmapping/developerportal/`
- `https://developers.immersal.com/docs/rest-api/`
- `https://developers.google.com/ar/develop/geospatial`
- `https://developers.google.com/ar/develop/java/geospatial/streetscape-geometry`
- `https://developers.google.com/ar/devices`
- `https://developers.google.com/ar/develop/depth`
- `https://support.google.com/maps/answer/9332056`

## 9. Pulido integral y preparación para modelos

Audita inicio y permisos, cámara negra, errores recuperables, ciclo foreground/background, rotación, safe area, UI/tarjetas/nombres, legibilidad exterior, coherencia de estados y feedback útil durante la búsqueda. Revisa persistencia, ajustes entre mapas/builds, restauración y exportación.

Revisa escenas/prefabs, referencias y GUID, ciclo de vida y liberación de mallas/materiales/buffers, paquetes efectivos, shaders/URP, stripping/IL2CPP, bibliotecas nativas, compilación reproducible y firma. Separa código propio, SDK y generado. Revisa secretos y privacidad de fotos, GPS y registros sin publicar valores sensibles; limita el diagnóstico pesado en la versión final.

Para los futuros modelos define únicamente contratos necesarios: coordenadas y unidades, pivote/base, orientación, escala, límites de carga/memoria/polígonos/texturas, materiales, LOD/culling cuando se justifiquen y estrategia de oclusión. No añadas modelos definitivos ni paquetes por anticipado. Usa cajas como contenido de validación.

## 10. Mediciones y aceptación

Propón un protocolo reproducible para arranque frío/caliente, primera instalación/actualización, red disponible/sin red, GPS bueno/malo/denegado, tracking perdido/recuperado, pausa/reanudación, distintos horarios y teléfonos. Incluye POCO X3 si resulta compatible y los equipos de prueba realmente disponibles.

Prueba puntos comunes desde los que se vean varios edificios y distancias de 15, 30, 50 m o las relevantes tras revisar el campus; registra esas distancias, no las supongas. Incluye caminar, girar, volver sobre el recorrido y al menos 15 minutos de uso continuo. Repite condiciones clave suficientes veces para informar dispersión, número de ensayos y fallos.

Mide por dispositivo/condición:

- Inicio hasta cámara/SDK listo; primera pose provisional y confirmada; tiempo hasta primer edificio y hasta **todos los edificios esperados en vista**.
- Duración de intentos correctos/fallidos, tiempo en cola, edad de la imagen, tasa de éxito y falsos positivos. Latencias p50/p95 cuando la muestra lo permita, y timeouts/fallos por separado.
- Error de alineación con referencia independiente, yaw, jitter, deriva y saltos entre mapas/sectores. No uses repetibilidad de matches como prueba de exactitud absoluta.
- Frames/tiempo CPU-GPU, memoria pico, GC, carga de mapas, temperatura/estado térmico, batería e impacto de fotos/logs/profundidad.
- Instalación y actualización conservando ajustes; representación visual equivalente a las cajas aceptadas.

Fija objetivos numéricos de aceptación propuestos y justificados para latencia, precisión a cada distancia, estabilidad, fluidez y térmicas en el dispositivo mínimo. Distingue objetivo de producto, baseline medida y resultado verificado. Si no hay teléfono/terreno, entrega el protocolo y marca pendientes: Editor, tests verdes y un APK compilado no demuestran experiencia AR real.

## 11. Entregables del modo Plan

Entrega:

1. Diagnóstico ejecutivo que responda qué impide hoy la experiencia deseada, qué cambió en las cajas y qué se sabe del POCO X3.
2. Baseline trazable: release/commit/APK, versiones efectivas, herramientas, comandos, evidencia histórica frente a validación propia y límites del acceso.
3. Hallazgos priorizados por impacto, con ID, severidad, confianza (confirmado/probable/pendiente), archivo/línea o evidencia, causa/hipótesis, corrección propuesta y comprobación de aceptación. Evita duplicar síntomas de una misma causa.
4. Comparativa tecnológica con fuentes y una decisión razonada sobre marco común, mapas del recorrido y necesidad real de nuevas capturas o geometría.
5. Plan de implementación por fases y dependencias: recuperar cajas/compatibilidad, instrumentar y medir lo que falte, validar la arquitectura con una prueba acotada, optimizar e integrar, y cerrar con validación de release. Cada tarea debe indicar archivos/sistemas, beneficio, esfuerzo aproximado, riesgo, tests relevantes, prueba física y rollback.
6. Matriz de dispositivos/escenarios, objetivos de aceptación y comandos reales de Unity CLI/batchmode/Android que se usarán al ejecutar. Incluye resultados esperados y cómo detectar un fallo.
7. Lista breve de datos imprescindibles aún faltantes y un criterio explícito de **listo para integrar modelos 3D**.

La aprobación del plan debe habilitar una ejecución concreta, con decisiones resueltas y verificaciones definidas. Las pruebas de campo pendientes deben quedar como puertas de aceptación explícitas. No cierres con «hay que optimizar» ni con una recomendación de rehacer todo sin evidencia. Prioriza el cambio más sencillo que cumpla el objetivo de ubicar el paseo completo de forma rápida y fiable.
