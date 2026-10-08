# AncoRA

App de realidad aumentada para UCN / PACE. Ubica el teléfono frente a edificios reales del campus con **mapas
Immersal** (sin cartel ni calibración del usuario) y superpone contenido virtual de tamaño real: un marco sobre la
fachada o una caja 3D que hace de edificio.

El idioma de trabajo es español. Los textos en pantalla y los mensajes de log van en español; los
comentarios en el código van en inglés.

**Estado (2026-10-08):** se está diseñando el **paseo virtual** por tres edificios de la escuela (ver *Paseo virtual*).
El proyecto quedó sin escenas: los pilotos anteriores se retiraron al tag `archivo/pilotos-immersal` y solo se conservó
el código de ejecución reutilizable. Nada se ha verificado todavía en terreno (tiempo de localización, error visual,
deriva).

---

## Repositorio

Solo existe la rama **`main`** (ordenado el 2026-10-08: se integró la rama Immersal y se borraron las demás).

La primera versión de la app, que anclaba una casa virtual a un **cartel PACE UCN** con rastreo de imagen, ya **no
está en `main`**: el piloto Geospatial (`6f9eac9`) quitó la plantilla Mobile AR y todo el código del cartel. Quedó
archivada en el tag **`archivo/cartel-pace`**, con el modo presentación, la sombra de contacto y la oclusión. Ver
*Archivo: la app del cartel*.

```bash
git switch -c cartel archivo/cartel-pace
```

Los pilotos Immersal y Geospatial que siguieron están en el tag **`archivo/pilotos-immersal`** (ver *Immersal: lo
aprendido en los pilotos*).

### Carpetas de trabajo

| Carpeta | Estado |
|---|---|
| `C:\dev\AncoRA-immersal` | Worktree en `main`, **fuera de OneDrive**. Trabajar aquí. |
| `C:\Users\nicol\OneDrive\Desktop\AncoRA` | Repo original, en *detached HEAD* sobre `archivo/cartel-pace`. No trabajar aquí. |
| `C:\Users\nicol\Documents\AncoRA` | Clon de trabajo del PC de escritorio (fuera de OneDrive). |

OneDrive ya causó un conflicto de sincronización que duplicó objetos dentro de `Assets/XR/XRGeneralSettings.asset` y
dejó la app sin cámara (ver *Causas raíz*). Si algo se comporta de forma inexplicable en los assets de una copia dentro
de OneDrive, sospechar de OneDrive antes que del código.

---

## Entorno

| | |
|---|---|
| Unity | 6000.6.0f1 |
| Render pipeline | URP (`ARBackgroundRendererFeature` presente y activo en `URP-Performant-Renderer.asset`) |
| AR | AR Foundation 6.5, ARCore 6.5, ARKit 6.5, Immersal Core 2.4.0, ARCore Extensions (paquete local) |
| XR | XR Management 4.6.0, XR Interaction Toolkit 3.5.1 |
| Build Android | IL2CPP + ARM64, APK suelto (no App Bundle) |
| Repo | `github.com/Gael-OC/AncoRA` |

### Rutas

```
Unity      C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe
adb        C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe
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
  '-projectPath','C:\dev\AncoRA-immersal',
  '-executeMethod','BuildAndroid.Build',
  '-logFile','<ruta al log>')
```

`BuildAndroid.Build` compila las escenas habilitadas en Build Settings a `AncoRA.apk` (hoy no hay ninguna). Sin
`-executeMethod` hace solo una compilación de scripts, que es más rápida para revisar errores.

La ruta de `-projectPath` depende del equipo (ver *Carpetas de trabajo*). Un clon recién hecho no trae `Library`, así
que el primer build importa todo el proyecto (~15 min).

**Todo Unity que compile este proyecto necesita el módulo iOS Build Support**, aunque el build sea de Android: el paquete
local `com.google.ar.core.arfoundation.extensions` usa `UnityEditor.iOS` y sin el módulo falla con `CS0234` en
`IOSPostProcessBuild.cs`. Se instala sin abrir el Hub:
`"C:\Program Files\Unity Hub\Unity Hub.exe" -- --headless install-modules --version 6000.6.0f1 -m ios`
(en el PC de escritorio ya se instaló el 2026-09-26).

Notas:

- **El proceso vuelve antes de que Unity termine.** Esperar sondeando `tasklist` por `Unity.exe`,
  no confiar en el retorno de `Start-Process`.
- **El Editor no puede estar abierto**: el proyecto queda bloqueado y el batchmode falla.
- El tamaño que reporta el log es `BuildSummary.totalSize`, una métrica interna de Unity, **no el peso del APK**.
- Los métodos llamados con `-executeMethod` deben ser `public static`; una excepción devuelve código de salida 1.

### Instalar

```bash
adb devices                      # confirmar el serial primero
adb -s <serial> install -r Builds/Android/AncoRA.apk
```

**No lanzar la app por adb** (`monkey -p ... LAUNCHER`). El usuario prefiere abrirla él; ya rechazó
ese comando varias veces. `adb install` sí está bien.

Si aparece `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, es una instalación previa firmada con otra clave:
hay que desinstalar primero, y eso **borra lo guardado en PlayerPrefs** (por ejemplo, las cajas de Teología). Pedir
permiso antes. Un `DELETE_FAILED_INTERNAL_ERROR` al desinstalar suele ser cosmético — verificar con
`firstInstallTime` si realmente se reinstaló.

Cada computador firma los builds con **su propio keystore de debug**. Instalar en el teléfono un APK hecho en otro equipo
(notebook ↔ PC de escritorio) siempre da `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, así que también hay que desinstalar.

### Menús del Editor

Por ahora no hay menús `AncoRA/…`: los de los pilotos se retiraron con ellos. Los del paseo deben ser `public static`
para poder llamarlos con `-executeMethod`.

---

## Artefactos de build que NO son fuentes

Estos archivos aparecen como modificados después de los builds o al abrir el proyecto. No se commitean:

- `ProjectSettings/ProjectSettings.asset` → `preloadedAssets`: lo puebla `XRGeneralBuildProcessor` al empezar el build.
- `Assets/Plugins/Android/{mainTemplate.gradle,settingsTemplate.gradle,proguard-user.txt}`: los rellenan los preprocesadores
  de ARCore Extensions. En `proguard-user.txt` el bloque «Module Progurad Rules» queda **duplicado** en cada build.
- `Assets/Settings/URP-Performant.asset`.
- `ProjectSettings/GvhProjectSettings.xml` (External Dependency Manager).
- `ProjectSettings/Packages/com.unity.testtools.codecoverage/Settings.json`.
- `Assets/XR/Settings/OpenXR Editor Settings.asset`, que es nuevo. El proyecto no usa OpenXR.
- `Assets/Resources/PerformanceTestRun*.json`.

Pendiente decidir si van a `.gitignore`.

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

Se arregló con el menú `AncoRA/Reparar configuracion XR` (`XrSettingsDoctor.cs`), que **ya no está en `main`**: vive en
el tag `archivo/cartel-pace`. Si vuelve a pasar, contar los objetos `XRGeneralSettings` del `.asset` (deben ser 4).

### Verificado que **no** era el problema

Manifest declara `android.permission.CAMERA` · los `.so` de ARCore están presentes y cargan · ARCore
1.56 instalado en ambos teléfonos · el GUID del loader coincide · `ARBackgroundRendererFeature`
presente y activo · stripping en default.

---

## Trampas al tocar el código

**IMGUI exige simetría entre pasadas.** `OnGUI` corre una vez para el evento `Layout` y otra para el
evento real. Si las dos pasadas emiten distinta cantidad de controles, Unity tira
`Mismatched LayoutGroup`. Por eso:

- Un cambio que altera cuántos controles se dibujan (por ejemplo, cambiar de panel) se **encola** y se aplica recién
  después de `GUILayout.EndArea()`.
- Los flags que deciden ramas se leen **una vez** al principio del método.

**Los objetos creados en runtime hay que liberarlos.** Asignar `Renderer.material` **clona** el
material y esa copia no tiene dueño; las mallas generadas por código tampoco. Si una malla se reconstruye en cada
cambio de medida, una fuga puntual se vuelve continua. Liberarlos en `OnDestroy`.

**`Destroy` es diferido al final del frame.** Al reemplazar el contenido hay que desactivar el objeto
saliente antes de destruirlo, o se dibuja encima de su reemplazo por un frame.

**El asmdef de ARCore declara `includePlatforms: ["Android", "Editor"]`.** Cualquier uso de
`UnityEngine.XR.ARCore` desde `Assembly-CSharp` va tras `#if UNITY_ANDROID || UNITY_EDITOR`, o el
build de iOS no compila.

**Campos serializados nuevos**: al agregar un `[SerializeField]` a un componente que ya está en una
escena, escribir el valor también en el YAML de la escena (o pasar por el `Prepare` del piloto) en vez de confiar en el
inicializador del campo.

**Persistencia**: si cambia el significado de un valor guardado en `PlayerPrefs`, **subir la versión del prefijo o
migrar**. Reutilizarlo en silencio deja el contenido corrido sin ninguna pista de por qué.

---

## Archivo: la app del cartel (tag `archivo/cartel-pace`)

Superponía una casa virtual de 18 × 10 × 5 m sobre una casa real, anclada a un cartel PACE UCN de su fachada con
`ARTrackedImageManager` (`ImageAnchorBuildingProbe`, calibración por seis caras, HUD IMGUI, casa paramétrica). Se
abandonó porque, a la distancia desde la que se ve el edificio entero, el cartel ocupa muy pocos píxeles para
detectarse, y una pose sacada de un solo plano cercano amplifica el error sobre un volumen grande (ver
`GEOSPATIAL_PILOT.md`, en el tag `archivo/pilotos-immersal`).

El detalle completo está en el `CLAUDE.md` de ese tag (`git show archivo/cartel-pace:CLAUDE.md`). Lo que sirve fuera de
esa app:

- **`arcoreimg` necesita keypoints.** Un cartel mayormente de color plano daba puntaje 0; solo un recorte del 46 % superior
  llegó a 100. El tamaño declarado debe cubrir la imagen **entera**, o el contenido deriva a lo largo del rayo de la
  cámara.
- **ARCore reconoce sobre la imagen CPU**, que es 640×480 por defecto. `CameraConfigurationTuner` pedía la configuración
  con más píxeles (1920×1080 en el Xiaomi 14). El mismo límite puede afectar a Immersal (ver *Pendientes heredados*).
- **Límite de 8 m del ancla:** Google recomienda mantener el contenido a menos de 8 m del ancla, porque más allá aparece
  deriva rotacional. Para volúmenes grandes, partir en varias anclas.
- **Cloud Anchors** no resuelven "mucho más lejos" (comparan contra el mapa del hospedaje). Con API Key el TTL máximo es
  24 h; las persistentes exigen *keyless authorization*, cuenta de Google Cloud y facturación.

---

## Pendientes generales

- [ ] Decidir `.gitignore` para los artefactos de build.
- [ ] Decidir si se restaura `SimulationLoader` en Standalone (XR Simulation en Play mode del Editor).

---

## Paseo virtual (en diseño, desde 2026-10-08)

**Objetivo:** un paseo **en el campus** por tres edificios de la escuela de ingeniería: **Teología**, **Ciencias
Básicas** y **X1**. Una sola app que carga los mapas Immersal de los tres; frente a cada edificio el teléfono se ubica
con su mapa y muestra su contenido. Los edificios son distintos, así que cada contenido vive en el marco de su propio
mapa y **no hace falta alinear los mapas entre sí**.

Se empieza **desde cero con mapas nuevos** (los pilotos anteriores se retiraron, ver más abajo).

| Edificio | Mapa | Estado |
|---|---|---|
| Ciencias Básicas | `152194-Cienciasbasicas` | Solo el `.bytes`, en Descargas. Faltan `-metadata.json` y `-sparse.ply`. |
| Teología | `152195-Teologianicowo` | Solo el `.bytes`, en Descargas. Faltan `-metadata.json` y `-sparse.ply`. |
| X1 | — | Fotos tomadas, **aún no subidas** al portal. |

El diseño (contenido de cada edificio, escena, automatización) está en conversación; todavía no hay código del paseo.

---

## Immersal: lo aprendido en los pilotos (tag `archivo/pilotos-immersal`)

El 2026-10-08 se retiraron los cuatro pilotos para empezar el paseo: **Fuente** (`151649`), **edificio G6**
(`151686`/`151687`, dos mapas alineados a mano y un marco en la fachada), **Teología** (`151714`/`151716`, selector de
mapas y caja por mapa) y **Geospatial**. Escenas, datos, scripts de preparación (`Prepare`/`Validate`/`BuildAndroid` por
piloto), pruebas de humo y documentos (`IMMERSAL_EDIFICIO_PILOT.md`, `EDIFICIO_GUIA_CAPTURA.md`,
`IMMERSAL_FUENTE_PILOT.md`, `GEOSPATIAL_PILOT.md`) están en el tag. Ninguno llegó a probarse en terreno.

```bash
git show archivo/pilotos-immersal:CLAUDE.md
```

### Código que quedó (`Assets/AncoRA/Scripts/`, todavía con nombres de los pilotos)

| Archivo | Rol |
|---|---|
| `EdificioFacadeFrame.cs` | Marco plano o caja 3D (12 aristas + 6 caras, sólida o translúcida, X en la cara +Z); malla generada y liberada por código. |
| `EdificioStatusText.cs` | Banner con el paso actual: iniciando → cargando mapa → buscando el edificio → ubicado / se perdió. |
| `ImmersalEdificioPilotDiagnostics.cs` | Muestra el contenido solo tras localización aceptada + tracking + pose ya aplicada; log y HUD del equipo (5 toques arriba a la izquierda). |
| `ImmersalEdificioFieldAdjust.cs` | Panel en el teléfono para mover y dimensionar la caja; «Copiar valores» deja un JSON en portapapeles, log y `persistentDataPath`. |
| `ImmersalMapAlignment.cs` | Guarda la pose manual de cada `XR Map` y la reaplica en `Awake`, antes del registro del SDK. |
| `TeologiaBoxStore.cs` | Guarda la caja por mapa en `PlayerPrefs` (prefijo `AncoRA.Teologia.v1.`). |
| `TeologiaMapSelector.cs` | Apaga los `XR Map` no elegidos antes del `Awake` del SDK. Lo usan el panel y el diagnóstico. |
| `GuiSafeArea.cs` | Rectángulo IMGUI usable (notch + esquinas redondeadas). |
| `PipelineMaterials.cs` | Materiales según el render pipeline. |

El panel y el diagnóstico dependen de `TeologiaMapSelector`; generalizarlos es parte del diseño del paseo. La
automatización de Editor del paseo se escribe de nuevo, usando la del tag como referencia.

### Hechos del SDK 2.4.0 que condicionan el diseño

- `MapManager.RegisterMap` lee el transform local del `XR Map` **una vez al iniciar el SDK**: esa es la
  relación mapa→espacio. `XRMap.ApplyAlignment()` la pisa con la metadata del portal (identidad para mapas
  sin alinear); por eso existe `ImmersalMapAlignment`.
- El SDK **solo registra los `XR Map` activos** en su `Awake`. Para elegir mapas hay que apagarlos antes
  (`DefaultExecutionOrder(-3000)`), y cambiar la elección exige recargar la escena.
- El evento `OnLocalizationResult` se dispara **antes** de `SceneUpdater`/`XRSpace`; mostrar contenido en
  ese evento lo dejaría un instante en el origen.
- Si dos mapas localizan en el mismo ciclo, gana el último resultado aplicado al XR Space.
- La preparación de los pilotos borraba `PoseFilter`/`PoseSmoother` y ponía `ProcessPoses = false`, así que cada
  localización movía el contenido de golpe. Se propuso rechazar poses incoherentes, promediar las primeras N y congelar
  con un ancla ARCore; **no se implementó**.
- Mantener el contenido visible tras la primera ubicación mientras `ARSession` siga en tracking, aunque la calidad de
  Immersal caiga a 0, evita que desaparezca al mirar zonas sin mapa.
- Los `Debug.Log` de un build **de release** no aparecían en `adb logcat`; para probar hay que usar
  `BuildOptions.Development`.
- Un mapa solo localiza en las caras que se escanearon.

### Reglas

- Una sola `ARSession`/`XROrigin`/cámara, un `DeviceLocalization`, sin `ServerLocalization`, sin token
  (nunca en código, escena, logs ni Git). IL2CPP, ARM64, URP; un solo loader por plataforma, sin OpenXR.
- No declarar «alineado» sin medirlo en el sitio. Separar siempre: **mapa cargado** / **SDK obtuvo
  pose** / **contenido físicamente alineado**. Un build correcto no demuestra precisión ni tiempos.
- Las fotos del DJI no entran al Mapper gratuito; sirven para geometría externa (RealityScan, COLMAP, ODM), no para
  localizar.
- Unity de forma **secuencial**, nunca dos instancias sobre el mismo proyecto. Antes de cada `adb`,
  `adb devices`. No abrir la app por adb. Pedir permiso antes de desinstalar.
- No generar un APK con datos de ejemplo del SDK como si fuera el producto.

### Nube del dron de Teología

`Escenas/Teologia/Teologia.ply` (13,6 MB, fuera de `Assets` para que Unity no la importe) es la nube armada en
**RealityScan** con 87 fotos del DJI, tres caras (la trasera no se pudo volar): ~203 mil tie points con color, en metros,
eje Z arriba. **La app no la usa**: es el "plano 3D" del edificio, para medirlo y para colocar la caja dentro de un mapa
Immersal sin ajustarla a mano. Los scripts de `Tools/RegistroDron/` la registraban contra los mapas viejos; sus
constantes y rutas apuntan a esos mapas y hay que adaptarlas a los nuevos.

- **Escala.** Se fijó con puntos de control y una distancia de 20 m en la cara larga, pero 20 era un número redondo: la
  nube da un fondo de 9,6–9,7 m. **Una medida con huincha resuelve la escala.** Sin puntos de control, la escala del GPS
  salía un 30 % chica. Siempre fijar una distancia real en RealityScan.
- La nube está **inclinada ~0,9° a lo largo** (el GPS del dron no fija bien la vertical). Manda la gravedad de Immersal.
- **Una fachada lisa y repetitiva deja mal determinada la posición a lo largo de ella**, porque las nubes Immersal tienen
  ~1000–1500 puntos. Lo que la fija son los puntos de las caras cortas.
- Registrar con **escala 1**: escalar libremente encoge la nube hacia zonas densas.
- Puntos de los mapas "dentro" del edificio, a la altura de las ventanas, son vidrio o interiores, no un error.
- Con ~1000 puntos, estimar el **tamaño** del edificio desde la nube Immersal no sirve (cambia de 3 a 27 m según el
  umbral); de ella salen bien el suelo, el giro y el centro.

### Interfaz: notch y esquinas redondeadas

`GuiSafeArea.Rect` es el rectángulo IMGUI usable, con origen arriba a la izquierda. Combina `Screen.safeArea` (la cámara
perforada; el proyecto tiene `androidRenderOutsideSafeArea: 1`) con el radio de las esquinas redondeadas, que
`safeArea` no incluye. El radio se lee de Android con `WindowInsets.getRoundedCorner` (API 31+). Si falla, se estima
~0,22 pulgadas según los dpi. El margen es 0,3·r, porque un punto a (m, m) de una esquina de radio r queda en pantalla
si m ≥ r·(1 − 1/√2).

**Toda interfaz nueva debe dibujarse dentro de `GuiSafeArea.Rect`.** El log `[AncoRA UI] Esquinas redondeadas: …` muestra
los valores reales. **Falta confirmar en pantalla** que el aviso quede bajo la cámara y que los botones de abajo no se
corten.

### Pendientes heredados

- [ ] Filtro de pose (ver *Hechos del SDK*).
- [ ] Confirmar que el SDK pida la resolución de cámara máxima: la imagen CPU de ARCore es 640×480 por defecto, el mismo
  problema que tenía el cartel.
- [ ] Medir con huincha un lado de Teología para fijar la escala de la nube del dron.
- [ ] Evaluar quitar el paquete ARCore Extensions: solo lo usaba Geospatial, y es el que exige el módulo iOS Build
  Support y el que ensucia las plantillas de gradle en cada build.
