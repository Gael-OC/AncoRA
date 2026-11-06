# Contexto de Unity — AncoRA

Actualizado: 30-oct-2026; base `f41cf00` más cambios locales de la demo. La guía del repositorio es `CLAUDE.md`.

- **Confirmado:** Unity 6000.6.0f1, URP 17, Android con IL2CPP/ARM64 y OpenGLES3. AR Foundation/ARCore 6.5 e Immersal Core 2.4.0.
- **Confirmado:** escena única `Assets/Scenes/PaseoIngenieria.unity`. `PaseoSetup.Prepare` la reconstruye desde `Assets/AncoRA/Paseo/<edificio>/edificio.json`.
- **Confirmado:** los cuatro edificios comparten XR Space en modo campus. Cualquier mapa que localice coloca todo el campus; los mapas conservan sus transformaciones medidas.
- **Confirmado:** la demo busca únicamente Teología. Sus mapas comparables son 152259 y 152693; `buscar: false` excluye Ciencias Básicas, X1 y EIC de la búsqueda, aunque sus mapas siguen cargados. EIC mantiene `oculta: true`.
- **Confirmado:** `PaseoTour` coordina búsqueda, filtros, visibilidad y ajustes guardados; `PaseoMapContent` controla el contenido visual de cada edificio. `PaseoPoseProcessor` y `PaseoPoseFilter` filtran las poses; `PaseoLocalization` llama al plugin nativo.
- **Confirmado:** UI Toolkit en código, con vistas en `Scripts/Paseo/Ui`; `PaseoUi` conecta eventos y estado. La hoja Equipo se abre con cinco toques en la esquina superior izquierda.
- **Confirmado:** `modelo` opcional (`academico`, `pabellon`, `clasico`) sustituye la caja visible con una malla opaca guardada por `PaseoBuildingModelBuilder`. La caja sigue siendo la referencia de pose y tamaño. La malla normalizada escala con los ajustes de campo; no se genera geometría nueva en runtime.
- **Confirmado:** el parque conserva tres caminantes en el pasillo de Teología. Su botón no oculta los edificios.
- **Confirmado:** no hay asmdef propios. Runtime en `Assembly-CSharp`, herramientas/pruebas en `Assembly-CSharp-Editor`. Pruebas NUnit EditMode bajo `Assets/AncoRA/Editor/Tests`.
- **Confirmado:** la selección del mapa de demo se guarda en `AncoRA.Paseo.v3.MapaDemo`. Cambiar espera al intento nativo y recarga la escena, con filtro nuevo. Por defecto se busca 152259; se puede seleccionar 152693 desde Equipo.
- **Confirmado:** comentarios en inglés, textos/logs en español, sin paquetes nuevos para la demo. No hay proveedor MCP de Unity instalado ni herramientas de Unity conectadas; se usa Unity por consola.
- **Pendiente de terreno:** comprobar alineación y cobertura reales desde el punto de la demo, cambio de mapa en el teléfono y rendimiento. Un build o una coincidencia de Immersal no prueban alineación física.
- **Prueba 1.5.3:** `PaseoOcclusion` en la cámara ofrece Sin/Cercana/Normal. Cercana filtra Raw Depth CPU por confianza y alcance (3/5/8 m); `PaseoBloques.shader` recorta sin deformar geometría. No usa profundidad automática del fondo en ese modo. Equipo permite mostrar bordes de cajas sobre los modelos y seleccionar manualmente el edificio para ajustar. Guía: `Docs/Oclusion-1.5.3.md`. Gael confirmó buen resultado de la 1.5.2 con 152693; la oclusión aún requiere prueba en terreno.

Validación: con el Editor cerrado, una sola instancia de Unity. Orden del runbook: EditMode → Prepare → CheckNativeMapsInEditor → BuildAndroid. Vistas previas reales: `AncorRA.Editor.PaseoDemoPreview.Export` (necesita gráficos).

Fuentes: `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `CLAUDE.md`, `PaseoSetup.cs`, `PaseoTour.cs`, `PaseoMapContent.cs` y JSON de los cuatro edificios.

## Trabajo offline, 4-nov-2026

Rama `localizacion-offline`. `Tools/LocalizacionOffline` compara SIFT, SIFT+LightGlue, ALIKED+LightGlue y XFeat+LighterGlue
en el computador, e incluye geometría para mapas métricos y refinamiento de secuencias. `PaseoSequenceCapture`, creado
desde la hoja Equipo, registra barridos de 5 s por AR Foundation; `PaseoTour.CaptureSequence` pausa y restaura el localizador
actual. Las nuevas distancias son 10/15/20/25 m; no reinterpretar nombres históricos de 30/50 m.
Build separado: `PaseoSetup.BuildOfflineCaptureAndroid` → `Builds/Android/AncoRA-Capturas-Offline.apk`.
La demo conserva Immersal: no hay todavía motor Android alternativo ni mapa abierto del campus validado. No confundir
los tiempos CPU/homografías con precisión o rendimiento móvil. Estado y validación: `Docs/LocalizacionOffline.md`.

## Revisión offline, 5-nov-2026

- Verificados `ProjectVersion.txt`, `Packages/manifest.json` y Build Settings: Unity 6000.6.0f1, URP 17.0.3, AR Foundation/ARCore 6.5.0, Input System 1.19.0, Immersal 2.4.0 y única escena activa `PaseoIngenieria`. `activeInputHandler: 2` habilita ambos sistemas. No se hallaron asmdef/asmref en `Assets/AncoRA` ni herramientas Unity MCP conectadas.
- `PaseoSequenceCapture` convierte `XRCpuImage` a gris sin transformación y lee `cameraManager.transform` en `LateUpdate`. El campo `timestamp_pose_sensor` copia el timestamp de `frameReceived`; no mide de forma independiente cuándo corresponde la pose óptica. No usar directamente `rot_camara` con el ajuste temporal OpenCV. Faltan calibración, orientación y sincronía de sensor.
- Los modelos de Teología de 65/71 vistas son SfM en escala arbitraria y siguen con cámaras inestables; no integrarlos como metros. La demo mantiene Immersal y el APK de captura no contiene localizador offline nuevo. Evidencia: `Docs/LocalizacionOffline-RevisionCamaras-2026-11-05.md`; controles propuestos: `Docs/LocalizacionOffline-Visita-Teologia.md`.
- Trabajo de esta revisión: solo herramientas Python/documentación, sin modificar assets, escena ni ajustes de Unity. El resultado y los límites físicos son más recientes que los conteos históricos anteriores.
