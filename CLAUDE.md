# AncoRA

App de realidad aumentada para UCN / PACE. Ubica el teléfono frente a edificios reales del campus con **mapas
Immersal** (sin cartel ni calibración del usuario) y superpone contenido virtual de tamaño real: un marco sobre la
fachada o una caja 3D que hace de edificio.

El idioma de trabajo es español. Los textos en pantalla y los mensajes de log van en español; los
comentarios en el código van en inglés.

| Escena (`Assets/Scenes/`) | Qué es |
|---|---|
| `ImmersalTeologiaDemo.unity` | Demo de Teología: dos mapas, selector y caja 3D por mapa. La más avanzada. |
| `ImmersalEdificioPilot.unity` | Piloto del edificio de ingeniería (G6): dos mapas alineados a mano y un marco en la fachada. |
| `ImmersalFuentePilot.unity` | Piloto de la Fuente (mapa 151649). Referencia y respaldo: **no modificar**. |
| `GeospatialPilot.unity` | Piloto ARCore Geospatial + VPS (ver `GEOSPATIAL_PILOT.md`). |

**Nada de esto está verificado en terreno todavía**: hay builds, pruebas de humo y validadores, pero ninguna medida de
tiempo de localización, error visual ni deriva.

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
APK        Builds\Android\<piloto>.apk   (cada piloto tiene su APK y su applicationId)
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
  '-executeMethod','AncorRA.Editor.ImmersalTeologiaDemoSetup.BuildAndroid',
  '-logFile','<ruta al log>')
```

Cada piloto tiene su propio `BuildAndroid`, que fija la escena, el applicationId y el nombre del APK; usar ese y no el
genérico `BuildAndroid.Build` (que compila las escenas habilitadas en Build Settings a `AncoRA.apk`). Sin
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
adb -s <serial> install -r Builds/Android/<piloto>.apk
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

- `AncoRA/Immersal/Teologia/…` — comprobar datos, preparar escena, validar, carga nativa, aplicar medidas y ajuste de
  campo, pruebas de humo.
- `AncoRA/Immersal/Edificio/…` — lo mismo para el piloto de ingeniería, más la alineación estimada A-B.
- `AncoRA/Immersal/…` — crear, activar y validar el piloto Fuente.
- `AncoRA/Crear escena limpia Geospatial`, `AncoRA/Configurar piloto Geospatial`, `AncoRA/Validar piloto Geospatial`.

Todos son `public static` para poder llamarlos con `-executeMethod`.

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
`GEOSPATIAL_PILOT.md`).

El detalle completo está en el `CLAUDE.md` de ese tag (`git show archivo/cartel-pace:CLAUDE.md`). Lo que sirve fuera de
esa app:

- **`arcoreimg` necesita keypoints.** Un cartel mayormente de color plano daba puntaje 0; solo un recorte del 46 % superior
  llegó a 100. El tamaño declarado debe cubrir la imagen **entera**, o el contenido deriva a lo largo del rayo de la
  cámara.
- **ARCore reconoce sobre la imagen CPU**, que es 640×480 por defecto. `CameraConfigurationTuner` pedía la configuración
  con más píxeles (1920×1080 en el Xiaomi 14). El mismo límite puede afectar a Immersal (ver pendientes de Teología).
- **Límite de 8 m del ancla:** Google recomienda mantener el contenido a menos de 8 m del ancla, porque más allá aparece
  deriva rotacional. Para volúmenes grandes, partir en varias anclas.
- **Cloud Anchors** no resuelven "mucho más lejos" (comparan contra el mapa del hospedaje). Con API Key el TTL máximo es
  24 h; las persistentes exigen *keyless authorization*, cuenta de Google Cloud y facturación.

---

## Pendientes generales

- [ ] Decidir `.gitignore` para los artefactos de build.
- [ ] Decidir si se restaura `SimulationLoader` en Standalone (XR Simulation en Play mode del Editor).

---

## Piloto Immersal del edificio de ingeniería

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

`Prepare` rechaza los IDs 151649 (Fuente) y 90687–90690 (ejemplos del SDK) y crea la escena
`Assets/Scenes/ImmersalEdificioPilot.unity` (ya creada con los mapas G6).

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
- **Caja desde la nube del dron (2026-09-26): 20 × 7,4 × 9,6 m** (ancho × alto × fondo). Detalle en la sección *Teología:
  nube del dron*. `EstimarCajaPly.js` no sirve para el tamaño: con ~1000 puntos la extensión cambia de 3 a 27 m según el
  umbral.
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

### Teología: nube del dron (agregado 2026-09-26)

`Escenas/Teologia/Teologia.ply` (commiteada, 13,6 MB, fuera de `Assets` para que Unity no la importe) es la nube que
el usuario armó en **RealityScan** con 87 fotos del DJI (tres caras; la trasera no se pudo volar). Son los tie points
(~203 mil, con color, en metros, eje Z arriba). La exportación COLMAP (`sparse/0`, ~68 MB de texto) quedó local, sin
commitear.

**La app no la usa.** El teléfono se ubica solo con los mapas Immersal. La nube es una referencia de medición, el
"plano 3D" del edificio, y sirve para:
- medir el edificio;
- ubicar la caja en cada mapa sin ajustarla a mano;
- alinear mapas entre sí o recalcular todo si se hacen mapas Immersal nuevos;
- a futuro, un modelo con caras (RealityScan → Calculate Model) sirve de oclusor.

**Escala.** En RealityScan se fijó con puntos de control en las esquinas superiores y una distancia de 20 m en la cara
larga. El usuario dio 20 × 10 m, pero son números redondos: la nube da un fondo de 9,6–9,7 m (proporción 2,07:1). O el
edificio mide 20 × 9,6, o mide ~20,6 × 10. **Una medida con huincha resuelve la escala** y todo lo que depende de ella.
Sin puntos de control, la escala del GPS salía un 30 % chica ("7,15 unit" donde había 10 m). Lección: siempre fijar una
distancia real en RealityScan.

**Medidas de la nube** (marco local: `u` a lo largo de la fachada, `v` hacia el fondo; el script usa un giro de 83,25°):

| Elemento | Posición |
|---|---|
| Caras cortas | u = −10,05 y +9,93 (salientes a ±10,5) |
| Fachada larga | v = 0,17 (el exterior es −v); ventanas hundidas 0,5–1 m detrás |
| Fondo | ~9,8 (sale de las caras cortas; la trasera no se fotografió) |
| Base de fachada | z ≈ 0,2 |
| Borde superior | z ≈ 7,6 |
| Pasto delante | 0,3–1,1 m más alto que la base |

La nube está **inclinada ~0,9° a lo largo**, porque el GPS del dron no fija bien la vertical. Manda la gravedad de
Immersal.

**Registro de los mapas contra el dron** (scripts exploratorios en `Tools/RegistroDron/`, con constantes y rutas fijas):

| Script | Qué hace |
|---|---|
| `register.js` | Búsqueda global: votación 2D por giro + ICP de 4 grados de libertad |
| `fieldfit.js` | Campo gaussiano + Nelder–Mead + bootstrap |
| `ab_direct.js` | Registro Mapa 2 → Mapa 1 sin pasar por el dron |
| `constrained.js` | Afina fijando la posición a lo largo de la fachada |
| `walls_in_map.js` | Dónde caen los puntos de cada mapa respecto de las paredes |
| `boxes.js` | Calcula la pose de la caja en cada mapa |

- **La fachada lisa y repetitiva deja la posición a lo largo de ella mal determinada**, porque las nubes Immersal tienen
  ~1000–1500 puntos. Lo que la fija son los puntos en las caras cortas.
- Escalar libremente degenera: encoge la nube hacia zonas densas. Por eso se usa **escala 1**.
- **Mapa 2** (`151716`): encaje propio (mapa→dron: giro −79,57°, t = (3,88; 2,91; −15,58)). Lo fija su cara corta
  derecha. Precisión ~±0,5 m y ±0,5°.
- **Mapa 1** (`151714`): su encaje propio dudaba ~2 m a lo largo. Se colocó a través del Mapa 2 con el registro directo
  Mapa 2 → Mapa 1 (giro 93,55°, t = (1,35; 0,21; 26,95)). Ese registro coincide con el del dron en 0,4°, 0,3 m transversal
  y 0,07 m en altura. Después se afinaron giro, profundidad y altura contra el dron (giro −174,12°, t = (8,66; 2,80;
  11,20)). Así colocado, sus paredes y la estructura baja más allá de la cara izquierda (u ≈ −13) caen igual que en el dron
  y en el Mapa 2. Precisión ~±1 m a lo largo de la fachada.
- **Caja resultante**, en coordenadas locales Unity de cada XR Map (Immersal PLY → Unity = (−x, y, z)):

  | Mapa | Posición | Giro |
  |---|---|---|
  | Mapa 1 | (12,44; 1,10; 12,01) | −89,13° |
  | Mapa 2 | (14,24; 0,99; 11,59) | 176,32° |

  La cara +Z (la X) es la fachada larga fotografiada. Las poses anteriores estaban 7–10 m corridas y la del Mapa 2 giraba
  90° de más. Están en `teologia-medidas.json` y en la escena, con `defaultPoseASet/BSet` en falso porque no están
  verificadas: «Ambos» sigue pidiendo confirmar la caja en cada mapa.
- Hay puntos de los mapas "dentro" de la caja: están a la altura de las ventanas y detrás del plano. Son vidrio o
  interiores, no un error.

### Interfaz: notch y esquinas redondeadas (agregado 2026-09-26)

`GuiSafeArea.Rect` es el rectángulo IMGUI usable, con origen arriba a la izquierda. Combina `Screen.safeArea` (la cámara
perforada; el proyecto tiene `androidRenderOutsideSafeArea: 1`) con el radio de las esquinas redondeadas, que
`safeArea` no incluye. El radio se lee de Android con `WindowInsets.getRoundedCorner` (API 31+). Si falla, se estima
~0,22 pulgadas según los dpi.

El margen es 0,3·r, porque un punto a (m, m) de una esquina de radio r queda en pantalla si m ≥ r·(1 − 1/√2). Lo usan
el aviso de estado, el HUD, la zona de 5 toques (`ImmersalEdificioPilotDiagnostics`) y la barra de ajuste
(`ImmersalEdificioFieldAdjust`, dentro de un `GUI.BeginGroup`).

**Toda interfaz nueva debe dibujarse dentro de `GuiSafeArea.Rect`.** El log `[AncoRA UI] Esquinas redondeadas: …` muestra
los valores reales en el teléfono. Instalado en el Xiaomi 14 el 2026-09-26; **falta confirmar en pantalla** que el aviso
quede bajo la cámara y que los botones de abajo no se corten.

### Teología: pendientes (2026-09-26)

- [ ] Probar en terreno la caja nueva en «Solo Mapa 2» y en «Solo Mapa 1». Si hay que corregir: «Copiar valores» →
  `ApplyFieldAdjustment`.
- [ ] Medir con huincha un lado del edificio para fijar la escala (hoy depende de un "20 m" redondeado).
- [ ] Confirmar en el Xiaomi 14 la interfaz con notch y esquinas.
- [ ] Filtro de pose: `ImmersalEdificioPilotSetup` borra `PoseFilter`/`PoseSmoother` y pone `ProcessPoses = false`, así
  que cada localización mueve la caja de golpe. Se propuso rechazar poses incoherentes, promediar las primeras N y
  congelar con un ancla ARCore (como hacía `ImageAnchorBuildingProbe`, en el tag `archivo/cartel-pace`). **Aún no se implementa.**
- [ ] Confirmar que el SDK pida la resolución de cámara máxima: la imagen CPU de ARCore es 640×480 por defecto, que es el
  mismo problema del cartel. No se pudo revisar porque faltaba `Library`.
- [ ] Decidir qué hacer con los artefactos de build de la rama Immersal (sobre todo el `proguard-user.txt` que se duplica).
