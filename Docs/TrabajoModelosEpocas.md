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

Los modelos actuales son maquetas de demo; el selector de épocas todavía no está implementado. Para añadirlo, conservar el marco, tamaño y pose de referencia de cada edificio y cambiar el contenido visual dentro de ese marco. No usar los cambios de época para mover mapas o reajustar el localizador. Documentar la convención de origen, escala en metros y orientación de los modelos importados.

La escena `Assets/Scenes/PaseoIngenieria.unity` se reconstruye con `Prepare`: cualquier referencia necesaria de los nuevos modelos debe quedar soportada por la configuración o el generador. Los cambios hechos únicamente a mano en esa escena pueden perderse al reconstruirla.

## Cómo abrir y validar

Unity **6000.6.0f1**, Android, URP 17, AR Foundation/ARCore 6.5.0, Immersal 2.4.0. Abrir con esa versión y esperar la importación. Mantener los archivos `.meta`. Una sola instancia de Unity para este proyecto.

La compilación habitual `AncoRA/Paseo/Compilar APK de Android` conserva el flujo de demo. `BuildCampusRecorderAndroid` genera otro APK con el modo de captura dedicado; usa `ANCORA_CAPTURE` solo durante ese build. No activar ese símbolo permanentemente para trabajar en las épocas. Ambos APK usan el mismo identificador Android; conservar ajustes y grabaciones al instalar.

El grabador añade sesiones de video/sensores ARCore, fotos y exportación conjunta: [guía](GrabadorCampus-2026-11-07.md). La demo conserva Immersal; no hay un motor propio de reconocimiento integrado. El trabajo experimental está descrito en [Estado y plan](LocalizacionOffline-Estado-y-Plan.md) y puede avanzar separado de los modelos.

## Qué no trae el clon

Grabaciones, reconstrucciones/resultados, pesos, entornos Python, APK, cachés y grafo Graphify permanecen locales e ignorados por Git. No son necesarios para comenzar la funcionalidad de modelos/épocas. Los documentos pueden referirse a esos archivos como evidencia local; un clon no los descarga automáticamente.

Usar commits centrados en modelos y épocas para facilitar la integración con el trabajo de captura/localización. Antes de subir, ejecutar las pruebas EditMode `AncorRA.Tests` y compilar Android si se cambia runtime, escena o importación de modelos.
