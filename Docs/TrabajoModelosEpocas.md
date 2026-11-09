# Base compartida para modelos 3D y épocas

Rama: `localizacion-offline`. Actualización: 7-nov-2026. Gael autorizó publicar el estado completo del proyecto e integrar los cambios de la rama compartida. Las restricciones de no hacer commit/push de los encargos anteriores corresponden a aquellas sesiones de investigación.

## Dónde trabajar

- `Assets/AncoRA/Editor/Paseo/PaseoBuildingModelBuilder.cs`: generación de los modelos actuales de demo.
- `Assets/AncoRA/Paseo/_Modelos/`: mallas generadas de Teología, Ciencias Básicas y X1; conservar sus `.meta` para mantener referencias Unity.
- `Assets/AncoRA/Paseo/_Materiales/Edificios/`: materiales de esos modelos.
- `Assets/AncoRA/Scripts/Paseo/PaseoMapContent.cs`: contenido visual de cada edificio y su relación con la caja de referencia.
- `Assets/AncoRA/Paseo/<edificio>/edificio.json`: configuración que usa `PaseoSetup.Prepare` para reconstruir la escena.
- `Assets/AncoRA/Editor/Paseo/PaseoSetup.cs`: preparación, validación y compilación.
- `Assets/AncoRA/Scripts/Paseo/PaseoUi.cs` y `Ui/`: interfaz actual.

Los modelos actuales son maquetas de demo. El selector de épocas está implementado en la rama `epocas` (ver abajo). Para añadirlo, conservar el marco, tamaño y pose de referencia de cada edificio y cambiar el contenido visual dentro de ese marco. No usar los cambios de época para mover mapas o reajustar el localizador. Documentar la convención de origen, escala en metros y orientación de los modelos importados.

La escena `Assets/Scenes/PaseoIngenieria.unity` se reconstruye con `Prepare`: cualquier referencia necesaria de los nuevos modelos debe quedar soportada por la configuración o el generador. Los cambios hechos únicamente a mano en esa escena pueden perderse al reconstruirla.

## Cómo abrir y validar

Unity **6000.6.0f1**, Android, URP 17, AR Foundation/ARCore 6.5.0, Immersal 2.4.0. Abrir con esa versión y esperar la importación. Mantener los archivos `.meta`. Una sola instancia de Unity para este proyecto.

La compilación habitual `AncoRA/Paseo/Compilar APK de Android` conserva el flujo de demo. `BuildCampusRecorderAndroid` genera otro APK con el modo de captura dedicado; usa `ANCORA_CAPTURE` solo durante ese build. No activar ese símbolo permanentemente para trabajar en las épocas. Ambos APK usan el mismo identificador Android; conservar ajustes y grabaciones al instalar.

El grabador añade sesiones de video/sensores ARCore, fotos y exportación conjunta: [guía](GrabadorCampus-2026-11-07.md). La demo conserva Immersal; no hay un motor propio de reconocimiento integrado. El trabajo experimental está descrito en [Estado y plan](LocalizacionOffline-Estado-y-Plan.md) y puede avanzar separado de los modelos.

## Qué no trae el clon

Grabaciones, reconstrucciones/resultados, pesos, entornos Python, APK, cachés y grafo Graphify permanecen locales e ignorados por Git. No son necesarios para comenzar la funcionalidad de modelos/épocas. Los documentos pueden referirse a esos archivos como evidencia local; un clon no los descarga automáticamente.

Usar commits centrados en modelos y épocas para facilitar la integración con el trabajo de captura/localización. Antes de subir, ejecutar las pruebas EditMode `AncorRA.Tests` y compilar Android si se cambia runtime, escena o importación de modelos.

## Validación de esta integración

Se integraron los commits remotos `1282d61` y `e333cee` con la demo/modelos y el grabador local. La escena conserva los modelos y la configuración actual de EIC, validados por el build. El grabador conserva video, fotos y notas; las capturas incorporan también los manifiestos de integridad remotos y el seguimiento de distancia de ARCore. El auditor reconoce tanto `construccion-arcore` como `recorrido-arcore` como construcción, manteniendo las consultas y las sesiones mixtas separadas.

Unity 6000.6.0f1 con gráficos: **555 pruebas EditMode aprobadas, cero fallos y cero omitidas**. Python: **33 pruebas, 31 aprobadas y dos omitidas por dependencias opcionales de exportación ONNX**. Compilación Android de la app normal completada con código 0. Evidencia local en `output/20261107-publicacion/`, fuera de Git. El APK de grabación usado por Gael se conserva; esta integración no instala ni cambia la app de su teléfono. Estos controles no acreditan precisión física ni validación del nuevo motor de reconocimiento.

## Selector de épocas (rama `epocas`, 7-nov)

- **Épocas:** `PaseoEpoch` = 1950 / 1980 / 2000 / Actualidad (`PaseoEpoch.cs`: nombres, claves, tiempos de la transición y aspecto de la cámara de cada época). La elección queda en el teléfono (`AncoRA.Paseo.v3.Epoca`, la primera vez 1950). Log `EPOCA <clave>` y `epoca=<clave>` en la interfaz.
- **Modelos:** cada edificio con `modelo` en `edificio.json` recibe además un modelo de **1950** (cal, piedra, ventanas con postigos, pórtico con frontón, techo de teja con campanario) y uno de **1980** (hormigón, ventanas corridas detrás de quiebravistas, losa plana, alero de entrada). 2000 = el modelo de demo de siempre; Actualidad = ningún modelo (se ve el edificio real; el parque sigue). `PaseoBuildingModelBuilder.BuildEpoch` → `_Modelos/<id>-1950.asset` y `-1980.asset`; materiales en `_Materiales/Edificios/1950|1980/`. Todos son ilustrativos, no reconstrucciones: el año en pantalla dice «Recreación ilustrativa».
- **Marco:** los modelos de época son hijos de la misma caja, con la misma normalización (escala = tamaño de la caja, origen en su centro); no mueven mapas ni tocan el localizador. `PaseoMapContent.SetEpochModel` / `ModelFor` / `SetEpoch`.
- **Transición:** el modelo que se va se desarma de arriba hacia abajo (0,6 s), pausa de 0,15 s con el edificio real y el nuevo se construye desde el suelo (1,3 s), con un borde cálido y dentado. Es `_Reveal` / `_RevealRange` en `AncoRA/Paseo/Bloques` (MaterialPropertyBlock por modelo; a 1 el shader se comporta como antes, también para el parque). Mientras está cortado dibuja el interior en oscuro.
- **Filtro de cámara:** un volumen global de URP por época (`_Materiales/Epocas/Epoca*.asset`: mezcla de canales, ajuste de color, grano y viñeta), mezclados por `PaseoEpochFilter` con los mismos tiempos; el post-procesado de la cámara se apaga cuando ninguno pesa. 1950 = blanco y negro con grano fuerte, rayas, polvo y parpadeo (`PaseoFilmOverlay`, dibujado por la interfaz); 1980 = sepia con algo de desgaste; 2000 = colores lavados y cálidos; Actualidad = sin filtro. El grabador (modo captura) fuerza Actualidad.
- **Interfaz:** selector de cuatro segmentos bajo la tarjeta y el año grande al centro al cambiar.
- **Validado:** 567 pruebas EditMode con gráficos (0 fallos, 0 omitidas), `Prepare`, `Validate`, `CheckNativeMapsInEditor` y `BuildAndroid` OK. Vistas de los modelos: `AncoRA/Paseo/Exportar vista previa de edificios demo` (`Builds/Demo/<id>-1950.png`, `-1980.png`, `-1950-a-medias.png`). **Sin ver en el teléfono:** el filtro sobre la imagen de ARCore, el costo del post-procesado y cómo se lee la transición en terreno.
