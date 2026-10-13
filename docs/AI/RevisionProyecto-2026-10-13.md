# Revisión de AncoRA — 13 de octubre de 2026

> Actualización posterior: el usuario autorizó implementar los cambios. El release de pruebas del 13-oct incorpora 152257 y 152259, deja Depth opcional y corrige H3/H4. El resto de este informe conserva la auditoría inicial de siete mapas. Véase `Docs/GuiaPrueba-2026-10-13.md` para el estado del candidato.

**Conclusión:** el paseo tiene una base funcional en el computador: compila, pasan sus 73 pruebas, la escena está bien conectada y sus siete mapas cargan en Immersal. Se generó y verificó un APK nuevo. Falta validar ARCore en el Xiaomi 15T y completar la prueba física de los cuatro edificios. No se puede afirmar todavía que todos localicen y queden alineados correctamente en ese teléfono.

Revisión sobre `main`, commit `6ddaef8`, con el repositorio inicialmente limpio. Se inspeccionaron código propio, escena, configuraciones XR/Android, dependencias, persistencia, pruebas, nubes de puntos, registros y la carpeta de Drive indicada, incluida `gael 9-10`. No se cambiaron scripts, escenas, mapas ni configuraciones de producción. Los cambios automáticos de Unity/Gradle se restauraron al finalizar.

## Validación realizada

| Comprobación | Resultado | Alcance |
|---|---|---|
| Compilación Unity | Aprobada | Unity 6000.6.0f1 en este Mac |
| Pruebas EditMode `AncorRA.Tests` | **73 aprobadas, 0 fallidas, 0 omitidas** | Configuración, carga, visibilidad, poses, ajustes y persistencia |
| `PaseoSetup.CheckNativeMapsInEditor` | Aprobada | Valida escena/proyecto y carga los siete mapas en el plugin |
| Cinco mapas nuevos de Drive | Cargan los cinco | Lectura mediante el plugin nativo del SDK instalado; descarga temporal |
| Registro de los cinco nuevos contra el dron | Ejecutado | Evalúa geometría y ambigüedad; no mide éxito de localización |
| Android IL2CPP / ARM64 | Build aprobado | APK de desarrollo del paseo actual |
| APK | ZIP íntegro y firma v2 verificada | Identidad, ABI, SDK y bibliotecas comprobados con herramientas Android |
| Xiaomi 15T | Pendiente | `adb devices -l` no encontró dispositivos |
| Localización/alineación en campus | Pendiente | No se realizó una prueba física en esta revisión |
| Rendimiento, calor y batería | Pendiente | No se midieron en el teléfono |

Los avisos de compilación observados incluyen APIs obsoletas de Unity, serialización de tipos del paquete ARCore Extensions y compilación de clases grandes de TextMeshPro. No impidieron el build. Unity también avisó sobre assets de prueba de ARCore/ARKit modificados en la caché de paquetes; no son escenas ni mapas propios de la app.

## Mapas que usa actualmente la app

**Los siete cargan; no encontré un archivo activo que falle en la carga nativa del Mac.** La validación de Unity confirmó un XR Space y un filtro propios por mapa, sus archivos embebidos, las cajas y sus coincidencias con `edificio.json`.

| Edificio | ID / archivo | Puntos | Estado físico conocido |
|---|---|---:|---|
| Ciencias Básicas | `152192-csbasicasgael.bytes` | 16.890 | El proyecto documenta prueba y corrección en terreno el 9-oct; no repetida ahora |
| Ciencias Básicas | `152196-csbasicasgael2.bytes` | 9.311 | Caja corregida mediante transferencia desde 152192; probar la transición entre ambos |
| X1 | `152198-x1gael.bytes` | 22.600 | Hubo prueba previa con saltos al alternar; la corrección del código todavía requiere comprobación física |
| X1 | `152200-x1johan.bytes` | 17.797 | Igual: verificar continuidad con 152198 |
| EIC | `152242-Eicnicowo.bytes` | 1.596 | Caja colocada desde el dron; falta revisar posición y altura en terreno |
| Teología | `152195-Teologianicowo.bytes` | 1.636 | Caja colocada desde el dron; sin validación física documentada completa |
| Teología | `152241-Teo67.bytes` | 1.771 | Igual; además comprobar transición con 152195 |

Las siete cajas tienen `colocada: true`. Eso significa que sus poses están definidas, no que su precisión física se haya demostrado. La aplicación muestra volúmenes y nombres; la navegación, fichas y modelos detallados quedan fuera de esta versión.

Ciencias Básicas mide 19,9 × 7,4 × 10,7 m; X1 27,6 × 4,5 × 14 m; Teología 22,3 × 8,3 × 10,7 m; EIC 15,1 × 9,3 × 18,7 m. Son las dimensiones configuradas, no nuevas mediciones de esta revisión.

**EIC requiere especial atención:** su registro guardado tiene aproximadamente 35 % de puntos a menos de 25 cm de la nube del dron. El volumen representa únicamente el bloque elegido por el equipo, y no el ala larga occidental. La documentación registra 26 fotos del dron vacías y una diferencia de nivel del suelo. Las fotos originales no están en este Mac, por lo que no comprobé si ya se recuperaron. Si se quiere representar todo EIC, también habrá que ampliar el contenido; añadir un mapa por sí solo no añade esa ala.

## Comparación con Drive

La [carpeta indicada](https://drive.google.com/drive/u/1/folders/1H8i6KNySZViiC0EVw2aGjtlGaUpP5suy) contiene 18 archivos `.bytes` al contar su subcarpeta `gael 9-10`. Cuatro tienen el mismo nombre y tamaño que mapas actuales: 152192, 152196, 152198 y 152200. No se recibió checksum remoto, por lo que esa comparación no demuestra identidad binaria.

Los mapas actuales **152195, 152241 y 152242 no aparecen en esa carpeta ni en la subcarpeta consultada**. Sus archivos sí están presentes y versionados en el proyecto; no faltan para compilar. Conviene incluirlos en el respaldo compartido si esa carpeta debe ser el inventario del equipo.

### Mapas anteriores que conviene mantener fuera del paseo

Recalculé sus dimensiones con las nubes `-sparse.ply` del repositorio. Se muestra el intervalo entre percentiles 2 y 98 de cada eje nativo, para excluir puntos extremos; no son medidas del edificio.

| Mapa | Extensión X × Y × Z, metros | Diagnóstico |
|---|---|---|
| `152199-eicgael` | 1,24 × 0,49 × 1,91 | Área demasiado pequeña para reconocer la fachada completa |
| `152240-eic4` | 2,79 × 0,77 × 2,65 | Mismo problema |
| `152243-teologiagael` | 1,09 × 0,45 × 1,48 | Mismo problema |
| `152245-teologiagael2` | 4,07 × 1,35 × 4,87 | Cobertura limitada |
| `152247-teologiagael3` | 4,56 × 2,58 × 4,08 | Cobertura limitada |
| `152248-teologiagael4` | 1,66 × 0,54 × 1,75 | Área demasiado pequeña |
| `152250-x1gael3` | 3,95 × 3,22 × 2,24 | Cobertura limitada |

No afirmo que estos binarios sean corruptos o que jamás puedan localizar. El problema es que la geometría capturada no cubre el edificio para este paseo. La documentación previa atribuye la causa a capturas con muy poco desplazamiento: recapturar caminando alrededor/frente a la fachada, con vistas solapadas, evitando limitarse a girar desde un punto.

**152237-eicRightjohan y 152238-eic3** tienen tamaño útil, aproximadamente 27 × 20 × 30 m y 11 × 10 × 20 m. El proyecto documenta registros ambiguos y escaso solape con la nube disponible de EIC. Su carga/localización no queda descartada; falta resolver la pose de la caja con fotos adicionales, referencias comunes o ajuste en terreno. No los activaría con una caja arbitraria.

También existe una nube del mapa **152194-Cienciasbasicas**, excluido previamente por registro ambiguo. Su `.bytes` no está en la app ni en la carpeta de Drive indicada; no lo probé nativamente. La captura `Fachadaingedetras` se documenta sin mapa construido y no está en esta carpeta de binarios.

### Cinco mapas nuevos que faltan evaluar para incorporación

Descargué los cinco, comprobé su carga, extraje sus puntos y ejecuté el registrador del proyecto con escala fija 1 contra `CienciasBasicas.ply`, `X1.ply` o `TeologiaBajo.ply`.

| Mapa de Drive | Puntos | Solape a <25 cm | Margen sobre segunda solución | Recomendación |
|---|---:|---:|---:|---|
| [152257-csbasicasnuevo1](https://drive.google.com/file/d/1T8A8Q9-SYVoOQ966VDrGjm9HiAQSQbD5/view) | 30.304 | 70,6 % | 1,35× | **Candidato prioritario**; escala libre diagnóstica 0,9965 |
| [152259-teologianuevo1](https://drive.google.com/file/d/1Dwm_rRUp89MNexPFc7901z6Y4UcRlGZ6/view) | 20.959 | 71,2 % | 2,20× | **Candidato prioritario**; solución claramente mejor que sus alternativas |
| [152254-teologiagaelnuevo1](https://drive.google.com/file/d/1qT1F_tz1gM17UWHIe4c5k7Zs7MdcU9-O/view) | 25.890 | 82,4 % | 1,05× | Buen solape, pero desplazamiento ambiguo; comprobar posición con referencias |
| [152255-teologiagaelnuevo2](https://drive.google.com/file/d/1bxjoYvxpY9TbjvbNKVPXR_PMdYKYjTkr/view) | 8.880 | 55,5 % | 1,85× | Revisar: el ajuste con escala libre dio 0,9072; puede indicar solape/ajuste insuficiente |
| [152256-x1nuevo1](https://drive.google.com/file/d/1pTj5vcBo7JbeKuBpdengI6tXqsDlzwdD/view) | 6.630 | 44,0 % | 1,05× | Registro ambiguo, incluso en el giro; verificar en terreno antes de colocar la caja |

El registrador avisa de ambigüedad cuando el margen es inferior a 1,3×. Estas cifras describen coincidencia geométrica con el dron; **no son tasas de éxito de Immersal, ni un error real de la caja en el teléfono**. Los cinco tienen una nube extensa: no presentan el problema de las capturas diminutas anteriores. Tampoco basta el número de puntos para decidir cuál localiza mejor.

Para incorporarlos: copiar el `.bytes` al edificio correcto, agregar su entrada a `edificio.json`, definir la pose de su caja en el marco de ese mapa, ejecutar `Prepare`, validar, compilar y probar la transición. **Drive no se sincroniza automáticamente con la app.** No se deben copiar directamente las coordenadas de otro mapa ni dejar una caja sin colocar entre cajas ya colocadas: el último mapa localizado controla la caja visible y provocaría un salto a la pose provisional.

Empezaría con **152257 y 152259**, comparando cobertura y estabilidad con los actuales. La incorporación no es necesaria para que el APK actual arranque. Añadir los cinco simultáneamente sin prueba aumenta los mapas competidores y dificulta aislar errores; decidir después si complementan o sustituyen mapas existentes.

## Hallazgos del código y la configuración

| ID | Prioridad / confianza | Evidencia y efecto | Acción y validación |
|---|---|---|---|
| H1 | Alta para la prueba / compatibilidad desconocida | Xiaomi 15T no figura con ese nombre en la lista oficial consultada; no hubo dispositivo conectado | Confirmar instalación/disponibilidad de Servicios de Google Play para RA y `ARSession: SessionTracking` en el equipo antes de evaluar mapas |
| H2 | Media / confirmada | [AR Core Settings](</Users/gael/Documents/GitHub/AncoRA/Assets/XR/Settings/AR Core Settings.asset:16>) exige Depth; APK declara `com.google.ar.core.depth` obligatorio. [La oclusión está desactivada](/Users/gael/Documents/GitHub/AncoRA/Assets/Scenes/PaseoIngenieria.unity:1404) | Hacer Depth opcional para este paseo; revalidar el manifest y probar una sesión AR. No equivale a resolver falta de ARCore |
| H3 | Media / confirmada por flujo de código | [PaseoTour](/Users/gael/Documents/GitHub/AncoRA/Assets/AncoRA/Scripts/Paseo/PaseoTour.cs:182) descarta resultados cuyo `MapId` no conoce antes de incrementar intentos; DeviceLocalization devuelve ID 0 al fallar | Registrar intentos/fallos globales y éxitos por mapa, sin atribuir un fallo global a un edificio. Comprobar un intento sin coincidencia |
| H4 | Media / confirmada por flujo de código | [CheckSdkStart](/Users/gael/Documents/GitHub/AncoRA/Assets/AncoRA/Scripts/Paseo/PaseoTour.cs:287) guarda un error a los 15 s y no lo limpia; [el banner prioriza ese error](/Users/gael/Documents/GitHub/AncoRA/Assets/AncoRA/Scripts/Paseo/PaseoStatusText.cs:44) | Limpiar el aviso si el SDK queda listo después; probar arranque lento. Hoy puede seguir mostrando error aunque el SDK se recupere |
| H5 | Baja / confirmada | `Tools/UnityBatch.ps1` y el runbook usan rutas fijas de Windows; la especificación inicial tiene inventario y comportamiento antiguos | Documentar comandos para macOS y actualizar el estado operativo usando `edificio.json` y el código actual como fuentes |
| H6 | Baja / presencia confirmada, restricciones desconocidas | Configuración versionada de ARCore Extensions conserva una API key iOS heredada; el paseo no usa Geospatial | Revisar sus restricciones o retirar la configuración heredada cuando se limpie ese piloto. No se reprodujo la clave en este informe |

H3 y H4 se verificaron leyendo el flujo del código; no se reprodujeron con un teléfono. No se aplicaron correcciones durante esta auditoría.

Lo que está bien respaldado: datos validados antes del build, paquetes fijados, Android con un solo ARCoreLoader, cuatro entradas XR sin los duplicados históricos, fondo AR de URP activo, filtro por mapa, una sola caja visible por edificio, recuperación de filtros al perder tracking, transferencia de ajustes entre mapas y persistencia que descarta valores de escenas anteriores. No encontré GUIDs duplicados en assets del proyecto/paquete embebido ni archivos propios relevantes sin `.meta`.

## Instalación y APK para Xiaomi 15T

En este Mac ya están instalados **Unity 6000.6.0f1, Android Build Support, SDK, NDK, OpenJDK, iOS Build Support y adb**. Las dependencias se resolvieron y Android compiló; no falta instalar una herramienta para producir el APK actual. iOS Build Support sigue siendo necesario para compilar el paquete local ARCore Extensions, que importa `UnityEditor.iOS.Xcode` incluso al trabajar para Android.

En el teléfono:

1. Instalar o actualizar **Servicios de Google Play para RA** desde Play Store, si está disponible para el equipo. AR Foundation puede solicitar su actualización al iniciar. Si no está disponible, resolver primero la compatibilidad exacta del modelo/ROM.
2. Instalar [AncoRAPaseo.apk](/Users/gael/Documents/GitHub/AncoRA/Builds/Android/AncoRAPaseo.apk) y permitir la cámara. Requiere Android 14 o superior (`minSdk 34`) y ARM64.
3. Abrir la app manualmente. Immersal Mapper sirve para hacer capturas, pero no es necesario para usar este paseo.

La [lista oficial de ARCore](https://developers.google.com/ar/devices) consultada incluye otros Xiaomi, pero no Xiaomi 15T. Esa ausencia deja la certificación sin confirmar; por sí sola no demuestra que el teléfono sea incapaz. La potencia/cámaras que describe [Xiaomi](https://www.mi.com/global/product/xiaomi-15t/specs/) tampoco prueban compatibilidad con ARCore.

El paseo usa DeviceLocalization con mapas embebidos y sin token de Immersal en la escena. No requiere descargar los mapas de Drive al abrirse ni configurar una API key Geospatial para localizar; el procesamiento de mapas puede funcionar sin internet una vez resuelto el servicio AR. Véanse la [documentación de Google sobre instalación del servicio](https://developers.google.com/ar/develop/unity-arf/enable-arcore) y la [localización en dispositivo de Immersal](https://developers.immersal.com/docs/immersal-sdk/howdoesitwork/).

APK generado: **62,78 MiB**, `com.ancora.ucnar.paseo`, versión `1.0.0`, código `1`, `targetSdk 36`, IL2CPP/`arm64-v8a`, OpenGL ES 3 y build de desarrollo. Incluye `libPosePlugin.so` y las bibliotecas ARCore. Su tamaño no muestra el exceso de espacio ZIP documentado para un APK anterior: contenido comprimido 62,69 MiB.

No se instaló ni se lanzó la app en un teléfono. Si ya existe una instalación firmada desde otro computador, una incompatibilidad de firma debe resolverse preservando antes los ajustes; desinstalar los borra. Este APK es para pruebas, no un release publicado.

## Qué falta y orden recomendado

1. **Confirmar ARCore en el Xiaomi 15T**, primero cámara y `SessionTracking`, luego reconocimiento de edificios. Hacer Depth opcional elimina una restricción adicional innecesaria.
2. **Probar el APK actual como referencia**, comenzando en Ciencias Básicas y pasando por X1, Teología y EIC. Registrar cada mapa que realmente localiza y el tiempo hasta que el filtro fija pose.
3. **Revisar cajas y cambios de mapa**, especialmente X1, Teología y altura de EIC. Guardar/exportar ajustes con el panel y hornearlos para que otro teléfono reciba la misma colocación.
4. **Corregir H3/H4** para que el diagnóstico no confunda fallos de reconocimiento con fallos de arranque.
5. **Evaluar e incorporar primero 152257 y 152259**, con poses propias y prueba comparativa. Resolver la ambigüedad de los demás antes de activarlos.
6. **Completar cobertura de EIC según el alcance deseado**: recuperar/verificar las fotos del dron y decidir si se incluyen su otro lado y el ala larga. Mantener las capturas pequeñas fuera del paseo.

### Recorrido de aceptación en terreno

Abrir el HUD del equipo con cinco toques arriba a la izquierda. Debe mostrar `ARSession: SessionTracking`, `SDK: listo` y los siete mapas cargados. En cada edificio, comprobar nombre, volumen, altura, giro y que la caja no salte mientras se camina. El filtro exige dos ubicaciones consistentes: una primera coincidencia no basta para mostrar contenido.

Probar la alternancia 152192/152196, 152198/152200 y 152195/152241 sin cajas duplicadas ni cambios bruscos. Ubicar dos edificios consecutivos y comprobar que localizar el segundo no desplaza al primero. Llevar la app a segundo plano, volver, perder/recuperar tracking y verificar que las cajas se oculten y reaparezcan tras nuevas poses. Finalmente exportar los ajustes, reiniciar la app y comprobar persistencia.

Anotar por mapa: punto de observación, luz, tiempo de primera pose, tiempo hasta caja visible, estabilidad visual y error aproximado de alineación. Añadir una sesión continua de 10 minutos para revisar calor y fluidez. Los contadores actuales del HUD no sirven como tasa de éxito hasta corregir H3. El éxito en el Mapper tampoco reemplaza comprobar la caja en AncoRA.

## Evidencia conservada

Resultados resumidos, puntos, transformaciones nuevas y checksum del APK: [EvidenciaRevision-2026-10-13.json](/Users/gael/Documents/GitHub/AncoRA/Docs/AI/EvidenciaRevision-2026-10-13.json).

Logs detallados de esta sesión: [pruebas](/private/tmp/ancora-audit-20261013-tests.log), [XML de pruebas](/private/tmp/ancora-audit-20261013-tests.xml), [validación y mapas en Unity](/private/tmp/ancora-audit-20261013-native-unity.log), [build Android](/private/tmp/ancora-audit-20261013-android.log). Los archivos de `/private/tmp` son temporales; el JSON y este informe conservan los resultados principales.
