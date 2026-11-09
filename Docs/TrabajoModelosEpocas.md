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
- **Modelos (actualizado 9-nov):** cada edificio con `modelo` en `edificio.json` recibe el edificio de `FreeBuilding3DMAX.rar` para **1950** y `Rv_Building_3.obj`, de `43-rv_building_3.rar`, para **1980**. **2000** conserva su modelo de demo; **Actualidad** muestra el edificio real sin modelo (el parque sigue). Sustituyen las maquetas procedurales de 1950/1980. `PaseoBuildingModelBuilder.BuildEpoch` → `_Modelos/<id>-1950.asset` y `-1980.asset`; materiales en `_Materiales/Edificios/1950|1980/`. La asociación de los modelos a los años es ilustrativa; no acredita el aspecto histórico del campus.
- **Marco:** los modelos de época son hijos de la misma caja, con la misma normalización (escala = tamaño de la caja, origen en su centro); no mueven mapas ni tocan el localizador. `PaseoMapContent.SetEpochModel` / `ModelFor` / `SetEpoch`.
- **Transición:** el modelo que se va se desarma de arriba hacia abajo (0,6 s), pausa de 0,15 s con el edificio real y el nuevo se construye desde el suelo (1,3 s), con un borde cálido y dentado. Es `_Reveal` / `_RevealRange` en `AncoRA/Paseo/Bloques` (MaterialPropertyBlock por modelo; a 1 el shader se comporta como antes, también para el parque). Mientras está cortado dibuja el interior en oscuro.
- **Filtro de cámara:** un volumen global de URP por época (`_Materiales/Epocas/Epoca*.asset`: mezcla de canales, ajuste de color, grano y viñeta), mezclados por `PaseoEpochFilter` con los mismos tiempos; el post-procesado de la cámara se apaga cuando ninguno pesa. 1950 = blanco y negro con grano fuerte, rayas, polvo y parpadeo (`PaseoFilmOverlay`, dibujado por la interfaz); 1980 = sepia con algo de desgaste; 2000 = colores lavados y cálidos; Actualidad = sin filtro. El grabador (modo captura) fuerza Actualidad.
- **Interfaz:** selector de cuatro segmentos bajo la tarjeta y el año grande al centro al cambiar.
- **Validado:** 567 pruebas EditMode con gráficos (0 fallos, 0 omitidas), `Prepare`, `Validate`, `CheckNativeMapsInEditor` y `BuildAndroid` OK. Vistas de los modelos: `AncoRA/Paseo/Exportar vista previa de edificios demo` (`Builds/Demo/<id>-1950.png`, `-1980.png`, `-1950-a-medias.png`). **Sin ver en el teléfono:** el filtro sobre la imagen de ARCore, el costo del post-procesado y cómo se lee la transición en terreno.

## Modelos suministrados (9-nov-2026)

Las fuentes convertidas están en `Assets/AncoRA/Paseo/_FuentesModelos/1950|1980/modelo.json`. Cada JSON guarda malla, normales, UV, submallas, materiales y SHA-256 del archivo original. Las seis texturas difusas de FreeBuilding se conservan en `1950/Texturas/`; el OBJ usa sus seis colores originales. `PaseoEpochModelImporter` prepara los assets de Unity sin necesitar Blender ni 3ds Max para abrir, regenerar o compilar el proyecto. El shader `PaseoBloques` combina ahora textura y color; conserva el corte de construcción y la oclusión.

Convención: caja unitaria centrada en `(0,0,0)`, suelo en `y=-0.5`, arriba `+Y`, fachadas largas hacia `±Z`; `localScale` es el tamaño medido en metros. Todos los modelos comparten la pose de la caja. El MAX suministrado almacena el edificio con Y arriba; el OBJ entra a Blender con Z arriba. La conversión respeta ambos casos, invierte el orden de los triángulos al cambiar a las coordenadas de Unity y transforma las normales. Se conservan estructura y baranda de FreeBuilding; se excluyen pavimento y faroles externos, y se simplifica la baranda para móvil. Resultado: 12.489 triángulos (1950) y 17.256 (1980), seis materiales cada uno.

Para repetir la conversión, extraer los dos RAR fuera de `Assets`, disponer de Blender y del importador [io_scene_max 1.9.2](https://github.com/nrgsille76/io_scene_max), y ejecutar (ajustar rutas):

```powershell
blender -b --python Tools/ConvertirModelosEpocas.py -- --rv Rv_Building_3.obj --max FreeBuilding/building.max --max-importer ruta/io_scene_max-main --output Assets/AncoRA/Paseo/_FuentesModelos
```

Luego ejecutar `AncoRA/Paseo/Preparar escena del paseo` (`PaseoSetup.Prepare`) y `PaseoSetup.Validate`. Las pruebas `PaseoEpochTests` verifican fuente, dimensiones, normales, UV, materiales/texturas, selección de cada época y vuelta a 2000/Actualidad.

Validación del 9-nov: **570 pruebas EditMode aprobadas, cero fallos y cero omitidas**; `Prepare` y `Validate` correctos. Se revisaron las vistas renderizadas de las tres épocas y el corte de construcción; la vista frontal confirma el techo de FreeBuilding arriba. La compilación Android alcanzó la compilación de shaders y falló porque el Control de aplicaciones de Windows bloqueó `AndroidPlayer/NiceIO.dll` (0x800711C7); no se generó APK. Evidencia local en `output/epocas-importacion/` y vistas en `Builds/Demo/`, fuera de Git. Falta la prueba en el teléfono.
