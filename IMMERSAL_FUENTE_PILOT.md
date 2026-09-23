# Piloto Immersal Fuente 151649

Rama `experiments/immersal-fuente-151649`, separada del piloto Google Geospatial. La única escena
habilitada en Build Settings es `Assets/Scenes/ImmersalFuentePilot.unity`; la escena
`Assets/Scenes/GeospatialPilot.unity` sigue en el proyecto, deshabilitada en Build Settings. El
validador Geospatial existente solo valida su propio piloto y no debe usarse aquí.

## Referencias y límites de la preparación

- Base: **SimpleSample** oficial de Immersal Core `2.4.0`, copiado desde el paquete UPM a la escena
  nueva. Contiene un solo `AR Session`, `XR Origin / Camera Offset / Main Camera` y el prefab
  `ImmersalSDK`. Solo `DeviceLocalization` está registrado en `Localizer`; la alternativa
  `ServerLocalization` del prefab no está en esta escena. `XR Space > XR Map 151649-Fuente` usa
  `DeviceLocalization`, `Map data source = Embed`, sin descarga de mapa ni de PLY.
- Mapa embebido: `Assets/AncoRA/ImmersalFuente/151649-Fuente.bytes`; metadata adjunta
  `Assets/AncoRA/ImmersalFuente/151649-Fuente-metadata.json`. Son copias byte a byte de
  `probarMapaFuente/`, que permanece sin modificar. El ID es **151649**, el nombre **Fuente**,
  alignment identidad, escala 1 y coordenadas 0: **no** es un ancla geográfica.
- `XR Map` tiene una visualización del archivo local
  `Assets/AncoRA/ImmersalFuente/151649-Fuente-sparse.ply` (4.630 puntos). Se importó llamando a
  `XRMapVisualization.LoadPly`, el mismo método del botón oficial **Load local sparse ply file**.
  Para recargarlo: seleccionar `151649-Fuente-vis` bajo XR Map, pulsar **Reset visualization** y
  luego **Load local sparse ply file**, elegir el `.ply` de esa carpeta y guardar la escena.
  La nube PLY es solo una ayuda visual; puede mostrarse/ocultarse en el dispositivo con el botón
  del HUD y arranca oculta. Su aparición no indica localización.
- `XR Map > Cubo prueba Fuente (ajustar en Editor)` usa el material opaco URP/Unlit
  `Assets/AncoRA/ImmersalFuente/CuboFuenteURP.mat`. Posición local inicial **(-4,2; -7,3; -16,5)**
  m y escala **(0,5; 0,5; 0,5)** m. La posición corresponde aproximadamente al anillo verde
  distinguible en la PLY, **no** a las coordenadas 0 ni a los extremos estadísticos del mapa.
  Es una propuesta de edición, no una medición física. El cubo se ve en Scene View para editarlo;
  el diagnóstico lo oculta al arrancar y solo lo habilita tras una localización del mapa con
  `ARSession` en tracking y calidad SDK positiva. `XR Space` aplica directamente la pose aceptada,
  sin el retraso inicial del suavizador de SimpleSample.
- No hay token en escena ni en settings. El SDK 2.4.0 omite `ValidateUser` con token vacío;
  `MapManager.LoadMap` con Embed llama a `Core.LoadMap` y no descarga datos. La carga nativa sin
  token se verificó llamando directamente al plugin en macOS; la inicialización completa y la
  relocalización en Android/iPhone requieren probarse allí.

## Abrir, validar y compilar

Abrir el proyecto con **Unity 6000.6.0f1**, abrir `Assets/Scenes/ImmersalFuentePilot.unity`.
En el menú **AncoRA/Immersal/Validar piloto Fuente** se comprueban escena, referencias, visualización,
material, `Embed`, una cámara/sesión, ajustes y un único loader ARCore/ARKit por plataforma.
Desde CLI, en la raíz del proyecto (usar la ruta local del editor):

```sh
UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -quit -projectPath "$PWD" \
  -executeMethod AncorRA.Editor.ImmersalFuentePilotSetup.CheckNativeMapInEditor -logFile /tmp/immersal-verificar.log
"$UNITY" -batchmode -nographics -quit -buildTarget Android -projectPath "$PWD" \
  -executeMethod AncorRA.Editor.ImmersalFuentePilotSetup.BuildAndroid -logFile /tmp/immersal-android.log
"$UNITY" -batchmode -nographics -quit -buildTarget iOS -projectPath "$PWD" \
  -executeMethod AncorRA.Editor.ImmersalFuentePilotSetup.ExportIos -logFile /tmp/immersal-ios.log
```

No ejecutar dos instancias del Editor sobre el proyecto. En Windows usar `Unity.exe` 6000.6.0f1
con los mismos argumentos. Salidas exclusivas del piloto:
`Builds/Android/ImmersalFuentePilot.apk` y `Builds/iOS_ImmersalFuentePilot/`. Para iOS abrir
`Builds/iOS_ImmersalFuentePilot/Unity-iPhone.xcworkspace` en Xcode, configurar firma del equipo y
ejecutar en un iPhone con ARKit. Ni la compilación ni Play Mode sin XR Simulation prueban el
alineamiento físico. No colocar un token en el prefab, la escena ni el repositorio.

El manifest mantiene AR Foundation/ARCore/ARKit **6.5.0**; con Unity 6000.6 el lock resolvió URP
**17.6.0** aunque el manifest declara 17.0.3, sin rebajar dependencias. OpenXR se instala como
dependencia de Core pero **no** se activa como loader Android/iOS. Hay una discrepancia de Unity
6000.6: tras cambiar de plataforma para compilar Android, puede reescribir el ajuste iOS de
`m_Automatic: 0` a `1`. Además, `PlayerSettings.GetUseDefaultGraphicsAPIs(iOS)` informa `true`
incluso cuando el YAML guardado contiene `m_Automatic: 0`. Los métodos de build restauran la
configuración explícita; verificar `Player > Other Settings > Graphics APIs` en el Editor antes
de exportar. En la exportación iOS Metal es la única API presente.

**Verificación de escritorio (23-09-2026):** Unity 6000.6.0f1 resolvió Core 2.4.0 y compiló
scripts sin errores; el validador confirmó escena/loader/mapa/material. La llamada directa
`Core.LoadMap(151649, bytes)` en el plugin nativo macOS devolvió handle `0` (válido) y **4.570**
puntos; la PLY auxiliar contiene **4.630** puntos y no se usó como mapa. El APK Android IL2CPP
ARM64 se generó en la ruta indicada (45.378.910 bytes, contiene `libPosePlugin.so` ARM64) y el
proyecto iOS se exportó en su ruta distinta (contiene `libPosePlugin.a` ARM64). No se compiló ni
firmó el proyecto Xcode. Unity advirtió que `GetUseDefaultGraphicsAPIs(iOS)` puede informar `true`
pese al ajuste iOS guardado `m_Automatic: 0`; los únicos avisos C# encontrados pertenecen al
paquete Geospatial existente (`FindObjectsSortMode` obsoleto). **No hubo dispositivo Android
conectado y los iPhone físicos figuraban no disponibles:** carga nativa en móviles, ausencia de
autenticación, relocalización entre arranques, error físico y alcance siguen pendientes.

El HUD/log `[AncoRA Immersal]` presenta versión/build GUID, estado ARSession, SDK listo/error,
confirmación de carga nativa del mapa, intentos/éxitos, calidad SDK 0–3, segundos desde el
arranque de la escena hasta primera localización y primera aparición del cubo **en la cámara**.
El propio HUD separa «SDK devolvió localización» de «alineación física NO verificada». Para medir
desde el toque del icono, grabar la pantalla desde antes de abrir la app: el reloj de la escena
no incluye todo el arranque Unity. Si SDK dice «listo» sin señal de carga nativa, es un error:
en 2.4.0 `IsReady` puede ser `true` aunque `LoadMap` falle. Revisar el log antes de valorar
los intentos. Tras perder tracking el cubo se oculta hasta una nueva localización.

## Ajuste único por el equipo

1. En **Scene View**, inspeccionar el anillo verde de `151649-Fuente-vis` dentro de XR Map. Si no
   aparece, seleccionar esa visualización y recargarla con **Load local sparse ply file**.
2. Mover **solo** `Cubo prueba Fuente (ajustar en Editor)` con el gizmo y el Inspector `Transform`
   **Local Position** hasta el punto físico identificable elegido en la fuente. No mover `XR Map`,
   `XR Space`, ni asignar `Static`. Mantener escala aproximada de 0,5 m.
3. Guardar `ImmersalFuentePilot.unity`, recompilar ambas plataformas y registrar en la hoja de campo
   la posición local X/Y/Z, versión/build GUID y una imagen de Scene View. Todos los usuarios
   reciben esa misma posición; no ajustar en cada teléfono.
4. Medir en terreno si aparece **en el mismo punto físico** tras cerrar/abrir la app, si salta y
   cuánto deriva durante 60 s. La PLY y los contadores del SDK nunca reemplazan esa medición.

## Protocolo de terreno por plataforma

Para Android, ejecutar primero `adb devices` y comprobar el serial **antes de cada instalación o
captura** (cambia de equipo). Después `adb -s <serial> install -r Builds/Android/ImmersalFuentePilot.apk`.
No iniciar la app por adb: la abrirá la persona que prueba. Si la instalación exige desinstalar
una app firmada con otra clave, pedir permiso (borra datos guardados). Para iPhone, instalar desde
Xcode con la firma propia; usar la consola de Xcode para los mismos logs.

Desde unos **15, 25 y 40 m**, abrir directamente apuntando a la fuente/escuela **sin acercarse antes**.
En cada distancia y en **Android y iPhone**:

1. Iniciar grabación de pantalla *antes* de tocar el icono; anotar equipo, plataforma, versión/build
   GUID, punto de observación y distancia estimada.
2. Registrar si el plugin cargó el mapa 151649 (log «cargado en plugin local»), si hay intentos,
   resultado de localización, segundos desde escena a primer éxito y a primer cubo en cámara.
   Registrar también el tiempo desde toque de icono con el vídeo.
3. Medir el error visual del cubo contra el mismo detalle físico; grabar pantalla **60 s** sin
   cambiar posición y al moverse lateralmente. Anotar deriva/saltos y pérdida de tracking.
4. Repetir tras cerrar completamente la app (nuevo arranque). Separar corridas con la nube PLY
   **oculta** y **visible** usando el botón del HUD, anotando el estado en vídeo. La nube no cambia
   el mapa binario ni significa pose válida.
5. Si solo hay localización desde cerca, registrar como **límite de cobertura del mapa actual**;
   ni metadata lat/lon=0 ni una compilación permiten afirmar alcance a 40 m.

| Plataforma/equipo | Distancia | Nube | Arranques/éxitos | Carga nativa sin token | 1.º éxito desde escena / icono | 1.er cubo en cámara | Error físico / deriva 60 s | Vídeo y observaciones |
|---|---:|---|---|---|---|---|---|---|
| Android / por registrar | ~15 m | oculta / visible | pendiente | pendiente | pendiente | pendiente | pendiente | pendiente |
| Android / por registrar | ~25 m | oculta / visible | pendiente | pendiente | pendiente | pendiente | pendiente | pendiente |
| Android / por registrar | ~40 m | oculta / visible | pendiente | pendiente | pendiente | pendiente | pendiente | pendiente |
| iPhone / por registrar | ~15 m | oculta / visible | pendiente | pendiente | pendiente | pendiente | pendiente | pendiente |
| iPhone / por registrar | ~25 m | oculta / visible | pendiente | pendiente | pendiente | pendiente | pendiente | pendiente |
| iPhone / por registrar | ~40 m | oculta / visible | pendiente | pendiente | pendiente | pendiente | pendiente | pendiente |
