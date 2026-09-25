# AncoRA

App de realidad aumentada para UCN / PACE. Superpone un edificio virtual sobre una casa real,
anclado a un cartel PACE UCN montado en su fachada.

El idioma de trabajo es español. Los textos en pantalla y los mensajes de log van en español; los
comentarios en el código van en inglés.

---

## Entorno

| | |
|---|---|
| Unity | 6000.6.0f1 |
| Render pipeline | URP (`ARBackgroundRendererFeature` presente y activo en `URP-Performant-Renderer.asset`) |
| AR | AR Foundation 6.5, ARCore 6.5, ARKit 6.5 |
| XR | XR Management 4.6.0, XR Interaction Toolkit 3.5.1 |
| Build Android | IL2CPP + ARM64, APK suelto (no App Bundle) |
| Repo | `github.com/Gael-OC/AncoRA` |

El proyecto vive dentro de **OneDrive**. Eso ya causó un conflicto de sincronización que duplicó
objetos dentro de `Assets/XR/XRGeneralSettings.asset` y dejó la app sin cámara (ver *Causas raíz*).
Si algo se comporta de forma inexplicable en los assets, sospechar de OneDrive antes que del código.

### Rutas

```
Unity      C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe
adb        C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe
arcoreimg  Library\PackageCache\com.unity.xr.arcore@<hash>\Tools~\Windows\arcoreimg.exe
APK        Builds\Android\AncoRA.apk
```

### Dispositivos de prueba

| Equipo | Serial adb | Modelo |
|---|---|---|
| Xiaomi 14 | `dd5da89f` | 23127PN0CC |
| Galaxy S25 | `RFCY11HW8QT` | SM-S931B |

Ambos con Android 16, arm64-v8a y ARCore 1.56. **Solo uno está conectado a la vez** y se intercambian
seguido: verificar el serial con `adb devices` antes de cada llamada, nunca asumirlo.

El Xiaomi 14 expone una configuración de cámara de **1920×1080 a 30 fps** (el default de ARCore es
640×480).

---

## Runbook

### Compilar

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
Start-Process -FilePath $unity -ArgumentList @(
  '-quit','-batchmode','-nographics',
  '-projectPath','C:\Users\nicol\OneDrive\Desktop\AncoRA',
  '-executeMethod','BuildAndroid.Build',
  '-logFile','<ruta al log>')
```

Sin `-executeMethod` hace solo una compilación de scripts, que es más rápida para revisar errores.

Notas:

- **El proceso vuelve antes de que Unity termine.** Esperar sondeando `tasklist` por `Unity.exe`,
  no confiar en el retorno de `Start-Process`.
- **El Editor no puede estar abierto**: el proyecto queda bloqueado y el batchmode falla.
- El log reporta `[BuildAndroid] OK: ... (813.0 MB, N min)`. Esos 813 MB son
  `BuildSummary.totalSize`, una métrica interna de Unity. **El APK real pesa ~40 MB.**
- Si `arcoreimg` falla, aparece `Error building XRReferenceImageLibrary`. Que no haya esa línea es
  la señal de que la base de imágenes se horneó bien.

### Instalar

```bash
adb devices                      # confirmar el serial primero
adb -s <serial> install -r Builds/Android/AncoRA.apk
```

**No lanzar la app por adb** (`monkey -p ... LAUNCHER`). El usuario prefiere abrirla él; ya rechazó
ese comando varias veces. `adb install` sí está bien.

Si aparece `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, es una instalación previa firmada con otra clave:
hay que desinstalar primero, y eso **borra la calibración guardada** en PlayerPrefs. Pedir permiso
antes. Un `DELETE_FAILED_INTERNAL_ERROR` al desinstalar suele ser cosmético — verificar con
`firstInstallTime` si realmente se reinstaló.

### Menús del Editor

- `AncoRA/Reconstruir librería de imágenes de referencia`
- `AncoRA/Mostrar contenido de la librería`
- `AncoRA/Diagnosticar configuracion XR`
- `AncoRA/Reparar configuracion XR (quitar duplicados)`

Todos son `public static` para poder llamarlos con `-executeMethod`.

---

## Arquitectura

### Runtime — `Assets/MobileARTemplateAssets/Scripts/`

| Archivo | Rol |
|---|---|
| `ImageAnchorBuildingProbe.cs` | Detecta el cartel, fija la pose, pide el ancla nativa y coloca el contenido. Es el centro de todo. |
| `CalibrationDebugHud.cs` | Panel IMGUI de calibración. **Se agrega solo en runtime**, no hay que cablearlo en la escena. |
| `CameraConfigurationTuner.cs` | Pide la configuración de cámara con más píxeles en la imagen CPU. También se agrega solo. |
| `HouseMeshBuilder.cs` | Genera la casa paramétrica a dos aguas, en metros reales. |
| `XrStartupDiagnostics.cs` | **TEMPORAL.** Loguea el arranque XR en las cinco etapas de `RuntimeInitializeOnLoad`. **Sacar antes de la entrega final.** |

### Editor — `Assets/Editor/`

| Archivo | Rol |
|---|---|
| `AncoraReferenceLibrarySetup.cs` | Reconstruye `HouseReferenceLibrary.asset`. La librería guarda las texturas como mitades de GUID serializadas, así que editar el YAML a mano produce una librería que no apunta a nada: hay que pasar por los métodos de extensión del Editor. |
| `XrSettingsDoctor.cs` | Diagnostica y repara `XRGeneralSettings.asset`. |
| `BuildAndroid.cs` | Punto de entrada del build por línea de comandos. |

El GUID del script del probe es `afda0b0073f443cc801f12177abdfd48` y está referenciado desde
`SampleScene.unity`. **Conservar el namespace `AncorRA.AR` y el nombre de la clase** al refactorizar,
o la escena pierde el componente.

### Cómo se coloca el contenido

1. `ARTrackedImageManager.trackablesChanged` entrega la imagen rastreada.
2. `TryComputeBasis` arma un marco **alineado a la gravedad**: arriba es la gravedad real, adelante
   es la normal del cartel aplanada al plano horizontal.

   La normal **se detecta, no se asume**: se prueba cada eje local de la imagen y gana el que apunta
   más directo a la cámara. La documentación de AR Foundation es ambigua sobre cuál eje local es la
   normal — dice `+Y` para XR Simulation mientras ARCore documenta `+Y` con `+Z` hacia abajo de la
   imagen — y los dos discrepan en dónde queda `+X`. Además, un edificio nunca se inclina con el
   ruido de roll del rastreo de imagen.
3. Se acumulan muestras hasta que la pose se estabiliza (dispersión bajo umbral), y se promedia.
4. `LockPose` congela el marco en el mundo y pide un ancla nativa con
   `ARAnchorManager.TryAddAnchorAsync(pose)`. **Esa pose va en espacio de mundo de Unity**
   (verificado en el código del paquete).
5. `ApplyCalibration` coloca el contenido dentro de ese marco.

**El eje `+Z` del marco apunta hacia afuera del cartel, hacia quien lo mira.** O sea: *adelante* es
la calle, *atrás* es el terreno.

### Calibración por seis caras

El contenido **no** se coloca con "tamaño + pivote". Se colocan las seis caras por separado, cada
una medida como una distancia desde el cartel: `ExtentRight`, `ExtentLeft`, `ExtentUp`,
`ExtentDown`, `ExtentBack`, `ExtentFront`.

La razón: el cartel está montado donde lo montaron — en este caso a media altura de la fachada, no
en una esquina ni en el centro de nada. Cualquier pivote fijo obliga a acomodar el edificio alrededor
de un punto que no le corresponde. Seis extensiones dicen simplemente dónde está cada cara, que es
lo que se puede ir a medir con una huincha.

`SizeMeters` pasa a ser derivado. La colocación manda la esquina mínima del contenido a
`(-left, -down, -back)` y el yaw gira el edificio en torno al cartel.

El ajuste de escala se calcula contra los **bounds medidos** del contenido, no asumiendo un cubo
unitario. Eso es lo que permite meter un `.fbx` authored a cualquier escala y que caiga en metros
reales.

### Persistencia

Claves de `PlayerPrefs`: `AncoRA.Calibration.v2.<nombreImagen>.<campo>`

Se guarda por imagen objetivo, así que cada cartel tiene su propia calibración. Cuando el esquema
cambió a seis caras, las calibraciones viejas **se migraron** en vez de descartarse: si existen las
claves `SizeX/Y/Z` pero no las `Right2/Left2/...`, se reconstruyen las extensiones con la convención
de fachada-y-suelo que regía entonces.

Si el significado de un valor guardado vuelve a cambiar, **subir la versión del prefijo o migrar**.
Reutilizarlo en silencio deja el edificio corrido sin ninguna pista de por qué.

---

## Causas raíz ya encontradas

No volver a investigarlas desde cero.

### Cámara negra, sin pedir permiso — `XRGeneralSettings` duplicado

`Assets/XR/XRGeneralSettings.asset` tenía **7** objetos `XRGeneralSettings` en vez de 4 (Standalone,
Android e iPhone duplicados), casi seguro por un conflicto de OneDrive.

Precargar un objeto de un `.asset` carga **todos** los objetos de ese archivo, así que todos corren
`Awake()` y cada uno hace `s_Instance = this`: gana el último. Varios duplicados tenían la lista de
loaders vacía. `Api.loaderPresent` de ARCore es un estático cacheado una sola vez que lee
`XRGeneralSettings.Instance`; cacheó `false`, el descriptor de sesión nunca se registró, y
`ARCoreLoader` reportó `Failed to load session subsystem`. Sin sesión no hay cámara, no hay permiso
y la pantalla queda negra.

Arreglado con `AncoRA/Reparar configuracion XR`. Verificado en dispositivo:
`ANCORA-DIAG [SubsystemRegistration] settings=True manager=True loaders=[ARCoreLoader] sessionDescriptors=[ARCore-Session]`

**Regresión conocida:** el set de duplicados que se eliminó era el que llevaba `SimulationLoader` en
Standalone, así que **XR Simulation en Play mode del Editor quedó sin loader**. Pendiente de decidir
si se vuelve a agregar.

### El cartel no se detectaba — `arcoreimg` fallaba en todos los builds

`arcoreimg` devolvía `Failed to get enough keypoints from target image`, lo que dejaba
`library does not contain any ARCore data`. Estuvo fallando en **todos** los builds, enmascarado por
el problema anterior.

Causa: el diseño del cartel. Tres cuartos de su superficie son magenta plano con reflejos de espejo
— cero features estables. Solo la parte de arriba (escudo UCN, logotipo PACE UCN y el "2" grande)
tiene contenido rastreable.

Barrido empírico sobre `IMG_0719` con `arcoreimg eval-img`:

| Recorte (% de altura desde arriba) | Puntaje |
|---|---|
| Foto completa | 0 |
| 44 % | 30 |
| **45–47 %** | **100** |
| 48 % | 20 |

Se eligió **46 %** por quedar en el centro de la meseta. Remuestrear a 2048 px **puntúa mejor** que
usar la resolución nativa (1909×2077 daba 80).

`PaceUcnHorizontal` (IMG_0708) falló en todas las variantes probadas — patrón repetitivo de
chevrones — y se eliminó del proyecto.

### Otras causas ya corregidas

- **Tamaño declarado equivocado.** Debe cubrir la imagen **entera**, no solo el panel dentro de ella.
  Los proveedores derivan la distancia del tamaño aparente dividido por el declarado, así que un
  tamaño que solo mide el panel reporta el cartel más cerca de lo que está y el contenido deriva a lo
  largo del rayo de la cámara. Actual: `PaceUcnVertical` = **1 × 1,088 m**.
- **Texturas importadas deformadas.** Un `.meta` copiado traía `nPOTScale: 1` y `maxTextureSize: 2048`,
  que convertían un origen de 1904×4471 en una textura de 1024×2048. Deben ir `nPOTScale: 0`,
  `maxTextureSize: 4096`, `textureCompression: 0`. `AncoraReferenceLibrarySetup` ahora lee las
  dimensiones **del archivo fuente** con `TextureImporter.GetSourceTextureWidthAndHeight` y da error
  si la textura importada quedó con otra proporción.
- **`m_MaxNumberOfMovingImages: 1`** hacía que el proveedor reestimara la pose cada frame. Debe ser `0`.
- **El bloqueo por 15 frames consecutivos de `Tracking`** nunca se cumplía, porque un solo frame
  `Limited` reiniciaba el contador. Reemplazado por ventana de estabilidad con promediado.
- **Faltaba `ARAnchorManager` en la escena.** Se agregaba con `AddComponent` en `Awake`, lo que corre
  contra el arranque de la sesión porque el manager tiene `[DefaultExecutionOrder]` y
  `[RequireComponent(XROrigin)]`. Ahora está en la escena y los fallos de anclaje **se reportan** en
  vez de caer en un fallback silencioso que se hacía pasar por éxito.

### Verificado que **no** era el problema

Manifest declara `android.permission.CAMERA` · los `.so` de ARCore están presentes y cargan · ARCore
1.56 instalado en ambos teléfonos · el GUID del loader coincide · `ARBackgroundRendererFeature`
presente y activo · stripping en default.

---

## Artefactos de build que NO son fuentes

Estos dos archivos aparecen como modificados después de **cada** build. No son código fuente:

| Archivo | Quién lo regenera |
|---|---|
| `Assets/Scenes/HouseReferenceLibrary.asset` → `m_DataStore` | `ARCoreImageLibraryBuildProcessor.OnPreprocessBuild` rehornea la base con `arcoreimg` en cada build de Android |
| `ProjectSettings/ProjectSettings.asset` → `preloadedAssets` | `XRGeneralBuildProcessor` la puebla al empezar el build |

Lo que está commiteado en el repo son restos de un build anterior; el estado limpio es el vacío.
**No entrar en pánico si aparecen vacíos** — el build los rellena. Pendiente decidir si van a
`.gitignore`.

Lo mismo con `Assets/Resources/PerformanceTestRun*.json`, que se genera solo.

---

## Datos del sitio

- **Cartel**: 1 m de ancho × 2,3 m de alto, vertical, montado **a media altura** de la fachada y
  aproximadamente al centro de su largo.
- **Referencia declarada**: 1 × 1,088 m (recorte del 46 % superior). El origen de pose es el centro
  de **ese recorte**, no del cartel completo — queda unos 0,6 m sobre el punto medio del panel.
- **Casa**: 18 m de largo × 10 m de fondo × 5 m de alto, techo a dos aguas.
- **Distancia de observación**: ~5 m. El usuario *puede* acercarse, pero prefiere no tener que
  hacerlo.

### Alcance de detección

La distancia de detección escala con el **ancho físico declarado** de la referencia. Google
recomienda que la imagen ocupe ≥25 % del cuadro, lo que con 1 m de ancho y un FOV típico da ~3 m.

ARCore reconoce sobre la **imagen CPU**, no sobre la textura de vista previa, y esa imagen es VGA
(640×480) por defecto. `CameraConfigurationTuner` pide la configuración con más píxeles;
**en el Xiaomi 14 consigue 1920×1080 a 30 fps**, o sea 3× más resolución lineal. Eso debería llevar
la detección a ~9 m. **Falta confirmarlo caminando hacia atrás.**

### Riesgo abierto: el límite de 8 m del ancla

Google recomienda mantener el contenido **dentro de 8 m del ancla**, porque más allá aparece deriva
rotacional cuando ARCore corrige el espacio-mundo. La casa mide 18 × 10 m, así que **sus esquinas
quedan a ~13 m del cartel**.

Al panear a lo largo de la fachada desde 5 m, si las esquinas lejanas se despegan del edificio real,
la solución es **partir el edificio en varias anclas**, no un marcador más grande ni servicios en la
nube.

---

## Localización: por qué no hace falta la nube

Se evaluaron y **descartaron por ahora**:

- **Geospatial API**: no hace falta si la detección a 5 m funciona.
- **Cloud Anchors persistentes**: resuelven "otra sesión, otro ángulo, otro teléfono", pero **no**
  "mucho más lejos" — el mecanismo compara contra el mapa de features construido al hospedar. Además
  con API Key el TTL máximo es **24 horas**; para anclas persistentes hace falta *keyless
  authorization* (OAuth con huella SHA-1 del certificado de firma), cuenta de Google Cloud y
  facturación. El paquete ARCore Extensions para AR Foundation 6 (rama `arf6`) está en **beta**.

**Para varias personas no se necesita nada de eso: el cartel ya es el ancla compartida.** Cada
teléfono que detecta el mismo cartel físico obtiene el mismo marco de referencia. Lo único que hoy no
se comparte son los números de calibración, que viven en `PlayerPrefs` de cada equipo. La solución es
calibrar una vez, usar **"Copiar valores al log"** del HUD y dejar esos valores como default de la
escena.

---

## Trampas al tocar el código

**IMGUI exige simetría entre pasadas.** `OnGUI` corre una vez para el evento `Layout` y otra para el
evento real. Si las dos pasadas emiten distinta cantidad de controles, Unity tira
`Mismatched LayoutGroup`. Por eso:

- El cambio de forma (Caja ↔ Casa) se **encola** en `m_PendingShape` y se aplica recién después de
  `GUILayout.EndArea()`, porque cambia cuántos sliders dibuja la sección de medidas.
- Los flags que deciden ramas (`isHouse`, el tuner) se leen **una vez** al principio del método.

**Los objetos creados en runtime hay que liberarlos.** Asignar `Renderer.material` **clona** el
material y esa copia no tiene dueño; las mallas generadas por código tampoco. El contorno de aristas
se reconstruye en cada cambio de medida, así que una fuga puntual se vuelve continua. Hay
`OnDestroy` en el HUD y en el probe para eso.

**`Destroy` es diferido al final del frame.** Al reemplazar el contenido hay que desactivar el objeto
saliente antes de destruirlo, o se dibuja encima de su reemplazo por un frame.

**El asmdef de ARCore declara `includePlatforms: ["Android", "Editor"]`.** Cualquier uso de
`UnityEngine.XR.ARCore` desde `Assembly-CSharp` va tras `#if UNITY_ANDROID || UNITY_EDITOR`, o el
build de iOS no compila.

**Campos serializados nuevos**: al agregar un `[SerializeField]` a un componente que ya está en la
escena, escribir el valor también en el YAML de `SampleScene.unity` en vez de confiar en el
inicializador del campo.

---

## Pendientes

- [ ] Medir la distancia real de detección a 1920×1080 y compararla con el default.
- [ ] Verificar si las esquinas lejanas de la casa derivan (límite de 8 m del ancla).
- [ ] **Sacar `XrStartupDiagnostics.cs`** antes de la entrega final.
- [ ] Decidir si se restaura `SimulationLoader` en Standalone (XR Simulation en el Editor).
- [ ] Hornear la calibración en la escena para repartir a los testers.
- [ ] Decidir `.gitignore` para los artefactos de build.
- [ ] `HouseTarget.png` (9,8 MB) sigue en `Assets/AR/ReferenceImages/` sin que la librería lo use.

---

## Piloto Immersal del edificio (rama `experiments/immersal-fuente-151649`)

Trabajo en un worktree fuera de OneDrive: `C:\dev\AncoRA-immersal` (creado con
`git worktree add` desde el repo de OneDrive, que sigue en `feat/mvp-presentacion` con su trabajo sin
commitear). Nada de esta sección está commiteado todavía.

**Objetivo:** un único marco virtual de tamaño real sobre la fachada del edificio de ingeniería, estable
y sin calibración del usuario, con **dos mapas Immersal `.bytes` (A centro, B contiguo) alineados a mano**
bajo un mismo XR Space. El plan gratuito no incluye el stitching automático del portal (Enterprise).

**Documentos:** `EDIFICIO_GUIA_CAPTURA.md` (guía de terreno y lista de faltantes),
`IMMERSAL_EDIFICIO_PILOT.md` (diseño, comandos CLI, alineación A↔B, protocolo de aceptación).
La Fuente (`IMMERSAL_FUENTE_PILOT.md`, `ImmersalFuentePilot.unity`, `ImmersalFuentePilotSetup`) es
referencia y respaldo: **no modificarla**; su validador exige el mapa 151649.

### Código nuevo (compila; prueba de humo OK con datos de ejemplo del SDK)

| Archivo | Rol |
|---|---|
| `Assets/AncoRA/Editor/ImmersalEdificioPilotSetup.cs` | `CheckInputs`, `Prepare`, `ValidateProject`, `Validate`, `CheckNativeMapsInEditor`, `BuildAndroid`, `ExportIos`. Se niegan a seguir sin los datos reales. |
| `Assets/AncoRA/Editor/ImmersalEdificioSmokeTest.cs` | Prueba de humo del automatismo con los mapas de ejemplo del SDK, en carpeta y escena temporales que borra. |
| `Assets/AncoRA/Scripts/ImmersalMapAlignment.cs` | Guarda pos/rot manual de cada `XR Map` y la reaplica en `Awake` (antes del registro del SDK). |
| `Assets/AncoRA/Scripts/EdificioFacadeFrame.cs` | Marco único (ancho/alto reales, hijo del XR Space); malla generada y liberada por código. |
| `Assets/AncoRA/Scripts/ImmersalEdificioPilotDiagnostics.cs` | Muestra el marco solo tras localización aceptada + tracking + pose ya aplicada; log `[AncoRA Edificio]`; HUD solo del equipo (5 toques arriba a la izquierda). |

### Hechos del SDK 2.4.0 que condicionan el diseño

- `MapManager.RegisterMap` lee el transform local del `XR Map` **una vez al iniciar el SDK**: esa es la
  relación mapa→espacio. `XRMap.ApplyAlignment()` la pisa con la metadata del portal (identidad para mapas
  sin alinear); por eso existe `ImmersalMapAlignment` y `Validate` detecta el transform desalineado.
- El evento `OnLocalizationResult` se dispara **antes** de `SceneUpdater`/`XRSpace`; mostrar contenido en
  ese evento lo dejaría un instante en el origen.
- Si dos mapas localizan en el mismo ciclo, gana el último resultado aplicado al XR Space.
- Sin `-executeMethod` los métodos deben ser `public static`; una excepción devuelve código de salida 1.

### Datos de entrada (recibidos 2026-09-24; medidas del marco ESTIMADAS)

```
Assets/AncoRA/ImmersalEdificio/MapaA/<id>-<nombre>.bytes  (+ -metadata.json, -sparse.ply)
Assets/AncoRA/ImmersalEdificio/MapaB/<id>-<nombre>.bytes  (+ -metadata.json, -sparse.ply)
Assets/AncoRA/ImmersalEdificio/edificio-medidas.json      (frameWidthMeters, frameHeightMeters)
```

`Prepare` rechaza los IDs 151649 (Fuente) y 90687–90690 (ejemplos del SDK). La escena
`Assets/Scenes/ImmersalEdificioPilot.unity` **no existe** hasta que haya datos reales.

### Reglas del piloto

- Una sola `ARSession`/`XROrigin`/cámara, un `DeviceLocalization`, sin `ServerLocalization`, sin token
  (nunca en código, escena, logs ni Git). IL2CPP, ARM64, URP; un solo loader por plataforma, sin OpenXR.
- No declarar «plano alineado» sin medirlo en el sitio. Separar siempre: **mapa cargado** / **SDK obtuvo
  pose** / **marco físicamente alineado**. Un build correcto no demuestra precisión ni tiempos.
- Las fotos del DJI no entran al Mapper gratuito; sirven para geometría externa (COLMAP/ODM), no para
  localizar.
- Unity de forma **secuencial**, nunca dos instancias sobre el mismo proyecto. Antes de cada `adb`,
  `adb devices`. No abrir la app por adb. Pedir permiso antes de desinstalar.
- Builds con applicationId `<id>.edificio` (restaurado al terminar) y salida
  `Builds/Android/ImmersalEdificioPilot.apk`, para no pisar la Fuente.

### Estado y pendientes

- [x] **Compilación resuelta (2026-09-24):** el Unity de Windows necesitaba el módulo iOS Build Support solo
  para que compilara el paquete local `com.google.ar.core.arfoundation.extensions` (`CS0234` en
  `IOSPostProcessBuild.cs`). Se instaló con Unity Hub y el proyecto compila.
- [x] `ImmersalEdificioSmokeTest.Run` (exit 0), `ValidateProject` (exit 0) y `CheckInputs`/`BuildAndroid`
  (exit 1 y lista de faltantes, sin generar APK) comprobados por CLI.
- [ ] Probar un `BuildAndroid` completo solo cuando existan datos reales (no generar APK con datos de
  ejemplo como si fuera el piloto).
- [x] Mapas recibidos (A `151686-G6` lejos, B `151687-G6down` cerca), `Prepare`, `Validate` y carga nativa OK. Medidas del marco (47 × 17 m; fachada = cara larga, los ~22 m son los costados) **estimadas de las nubes PLY**: medir de verdad y poner `estimated=false`.
- [ ] B tiene una alineación ESTIMADA por registro de nubes PLY (`Tools/EstimarAlineacionPly.js`, `ApplyEstimatedAlignment`; pos (45,59; -4,21; 4,64) m, giro -77,2°; giro y desnivel fiables, X/Z ambiguo unos metros). Afinarla en Scene View con detalles físicos, marcar `Adjusted By Team`, colocar el marco y `BuildAndroid`.
- [ ] Todo el protocolo de aceptación en terreno (tiempos, error visual en 4 detalles, deriva 60 s, saltos
  al cambiar de mapa) en Android.
- [ ] Al terminar el trabajo en el worktree: revisar `git status`, commitear por separado y decidir si
  se elimina el worktree (`git worktree remove`).

### Ajuste de campo desde la app (agregado 2026-09-24)

`Assets/AncoRA/Scripts/ImmersalEdificioFieldAdjust.cs`: panel del HUD del equipo (5 toques arriba a la izquierda →
«Ajuste: Mapa B / Marco») para mover B (pos X/Y/Z, giro Y) y el marco (pos, giro, ancho, alto) en el teléfono.
**El SDK lee la pose de B una sola vez al arrancar**, así que el panel también actualiza `MapEntry.Relation`
(clase mutable); el cambio se ve en la siguiente localización de B. «Copiar valores» deja un JSON en el
portapapeles, en el log (`AJUSTE_CAMPO`) y en `persistentDataPath`. Aplicarlo:
`ImmersalEdificioPilotSetup.ApplyFieldAdjustment` con `Assets/AncoRA/ImmersalEdificio/ajuste-campo.json`.
Sin persistencia entre arranques a propósito. Ver `IMMERSAL_EDIFICIO_PILOT.md`.

---

## Demo Teología: 1 mapa + caja 3D editable (agregado 2026-09-25)

Mapa Immersal `151714-Teologia` (un solo mapa, 1058 puntos) y una **caja 3D** que hace de edificio, para ver si el
teléfono se ubica bien y la caja queda fija. Reusa la automatización del piloto del edificio en modo un mapa
(`Paths.SingleMap`); el piloto de 2 mapas y la Fuente no cambian. Nada está verificado en terreno.

| Qué | Dónde |
|---|---|
| Datos (copia de `Locaciones/Teologia`) | `Assets/AncoRA/ImmersalTeologia/Mapa/` + `teologia-medidas.json` |
| Escena | `Assets/Scenes/ImmersalTeologiaDemo.unity` |
| Menús / `-executeMethod` | `AncorRA.Editor.ImmersalTeologiaDemoSetup.{CheckInputs,Prepare,Validate,CheckNativeMapsInEditor,ApplyMeasurements,ApplyFieldAdjustment,BuildAndroid}` |
| APK | `Builds/Android/ImmersalTeologiaDemo.apk`, paquete `com.ancora.ucnar.teologia`, etiqueta «AncoRA Teologia» |
| Prueba de humo (mapa de ejemplo del SDK) | `ImmersalEdificioSmokeTest.RunTeologia` |
| Caja inicial | `node Tools/EstimarCajaPly.js <sparse.ply> <medidas.json> [--tamano=ancho,alto,prof]` |

- `EdificioFacadeFrame` con `depthMeters > 0` dibuja una caja (12 aristas + 6 caras); con 0 sigue siendo el marco plano.
  Relleno sólido (tapa el edificio) o translúcido, alternable desde el panel del equipo.
- Panel del equipo (5 toques arriba a la izquierda → «Ajuste: Caja»): posición X/Y/Z, giro Y, ancho, alto, profundidad,
  sólido/transparente. Sin «Mapa B» porque hay un solo mapa. «Copiar valores» → `ApplyFieldAdjustment` con
  `Assets/AncoRA/ImmersalTeologia/ajuste-campo.json`.
- **El tamaño de la caja (20 × 8 × 12 m) es un valor por defecto, no una medida ni una estimación**: con ~1000 puntos la
  extensión del edificio cambia de 3 a 27 m según el umbral. De la nube salen solo suelo, giro y centro. Ajustarla en el teléfono.
- El mapa cubre 3 caras: en la cara sin mapear no localiza.
- Texto en pantalla (Teología): banner grande siempre visible con el paso actual (`EdificioStatusText`: iniciando → cargando
  mapa → buscando el edificio → ubicado / se perdió), HUD del equipo y barra «Ajuste: Caja» visibles desde el inicio
  (`showStatusBanner` y `hudVisibleAtStart` en el diagnóstico; en el piloto de ingeniería siguen apagados). Los 5 toques
  arriba a la izquierda ocultan solo el HUD detallado. Prueba: `ImmersalEdificioSmokeTest.RunStatusText`.

### Teología: dos mapas con menú y caja por mapa (actualizado 2026-09-25)

Ahora la demo trae **dos mapas del mismo edificio** (`151714-Teologia` en `MapaA/`, `151716-Teologia2` en `MapaB/`) y un
menú en el teléfono (barra de abajo → «Mapas»): **Solo Mapa 1 / Solo Mapa 2 / Ambos**. Por defecto arranca en Solo Mapa 2.

- `TeologiaMapSelector` (`DefaultExecutionOrder(-3000)`) apaga el XR Map que no se usa **antes del Awake del SDK**, que
  solo registra los mapas activos. Cambiar de modo guarda la elección y **recarga la escena**. Ambos XR Map se guardan
  activos en la escena; validado por `Validate`.
- La caja se guarda **por mapa** en `PlayerPrefs` (`TeologiaBoxStore`, prefijo `AncoRA.Teologia.v1.`): una pose solo vale
  dentro del marco de su mapa. Tamaño y relleno son del edificio y se comparten. Si cambia el significado de un valor,
  subir el prefijo. «Restaurar escena» borra lo guardado. (Esto reemplaza la regla «sin persistencia» del piloto de ingeniería,
  que sigue igual: allí no hay selector.)
- **Ambos:** la alineación del Mapa 2 dentro del Mapa 1 se **deriva** de las dos cajas (`DeriveAlignment`: T = qA·qB⁻¹,
  pos = posA − T·posB). Hay que colocar la caja sobre el mismo edificio en Solo Mapa 1 y en Solo Mapa 2. La caja lleva una
  **X en la cara delantera (+Z)** para no poner el giro 180° distinto en cada mapa. En Ambos la pose no se edita.
- **Mantener la caja visible** (`keepVisibleAfterFirstLocalization`): tras la primera ubicación se queda mientras
  `ARSession` siga en tracking, aunque la calidad de Immersal caiga a 0; el texto avisa «posición mantenida».
- «Copiar valores» trae un bloque `teologia` con la caja de cada mapa; `ApplyFieldAdjustment` lo deja en la escena.
- APK de **desarrollo** (`BuildOptions.Development`, marca «Development Build»): `Debug.Log` llega a logcat. Los logs del
  build de release no aparecían con `adb logcat`.
- Pruebas: `RunStatusText`, `RunTeologia` (1 mapa), `RunTeologiaSelector` (2 mapas + selector), `Run` (ingeniería).
  Nada de esto prueba localización ni cambio de modo en el teléfono.
