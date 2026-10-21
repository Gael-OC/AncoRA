# AncoRA

App de realidad aumentada para UCN / PACE. Ubica el teléfono frente a edificios reales del campus con **mapas
Immersal** (sin cartel ni calibración del usuario) y superpone contenido virtual de tamaño real: un marco sobre la
fachada o una caja 3D que hace de edificio.

El idioma de trabajo es español. Los textos en pantalla y los mensajes de log van en español; los
comentarios en el código van en inglés.

**Estado (2026-10-21):** el **paseo virtual** por los edificios de la escuela está implementado (escena
`PaseoIngenieria.unity`, 4 edificios, 7 mapas, todos con su caja colocada) con filtro de pose. Las cajas de X1, Teología y EIC están colocadas desde
las nubes del dron y la de Ciencias Básicas se corrigió en terreno. Lo único probado en terreno es Ciencias Básicas y que
X1 alternaba entre sus dos mapas; tiempos, error visual y deriva siguen sin medirse. Desde 1.2.3 cada intento busca un
edificio a la vez, elegido por GPS (ver *1.2.3*); falta medir en terreno cuánto más rápido ubica. Los pilotos anteriores
están en el tag `archivo/pilotos-immersal`.

---

## Repositorio

Ramas: **`main`** (hasta 1.2.2; ordenado el 2026-10-08: se integró la rama Immersal y se borraron las demás) y
**`wip/busqueda-gps`** (1.2.3, búsqueda por edificio con GPS; sin integrar a `main` mientras no se pruebe en terreno),
**`feature/reemplazo-edificio`** (1.3.0, sale de la 1.2.3: el edificio real desaparece y otro objeto toma su lugar; ver
*Reemplazo del edificio*) y **`nicowo`** (1.3.1, sale de la 1.2.3 y trae integrado el reemplazo: rama de trabajo para
refinar la detección con logs de depuración, antes de sacar la versión final; ver *Rama nicowo*).

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
| `C:\Users\nicol\Documents\AncoRA` | Clon de trabajo del PC de escritorio (fuera de OneDrive). Las fotos del dron (`Imgs/`) y los proyectos de RealityScan solo existen aquí (ver *Fotogrametría con dron*). |

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
  '-executeMethod','AncorRA.Editor.PaseoSetup.BuildAndroid',
  '-logFile','<ruta al log>')
```

Usar `Tools/UnityBatch.ps1` (ver *Paseo virtual*), que espera a que Unity termine. Sin `-executeMethod` hace solo una
compilación de scripts, que es más rápida para revisar errores.

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
adb -s <serial> install -r Builds/Android/AncoRAPaseo.apk
```

**No lanzar la app por adb** (`monkey -p ... LAUNCHER`). El usuario prefiere abrirla él; ya rechazó
ese comando varias veces. `adb install` sí está bien.

Si aparece `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, es una instalación previa firmada con otra clave:
hay que desinstalar primero, y eso **borra lo guardado en PlayerPrefs** (por ejemplo, las cajas ajustadas en terreno). Pedir
permiso antes. Un `DELETE_FAILED_INTERNAL_ERROR` al desinstalar suele ser cosmético — verificar con
`firstInstallTime` si realmente se reinstaló.

Cada computador firma los builds con **su propio keystore de debug**. Instalar en el teléfono un APK hecho en otro equipo
(notebook ↔ PC de escritorio) siempre da `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, así que también hay que desinstalar.

### Menús del Editor

`AncoRA/Paseo/…`: comprobar datos, preparar escena, validar, carga nativa, aplicar ajuste de campo, compilar APK, prueba
de humo y vista previa de los reemplazos. Todos son `public static` para llamarlos con `-executeMethod`.

---

## Artefactos de build que NO son fuentes

Estos archivos aparecen como modificados después de los builds o al abrir el proyecto. No se commitean:

- `ProjectSettings/ProjectSettings.asset` → `preloadedAssets`: lo puebla `XRGeneralBuildProcessor` al empezar el build.
- `Assets/Plugins/Android/{mainTemplate.gradle,settingsTemplate.gradle,proguard-user.txt}`: los rellenan los preprocesadores
  de ARCore Extensions. En `proguard-user.txt` el bloque «Module Progurad Rules» queda **duplicado** en cada build.
- `Assets/Settings/URP-Performant.asset`.
- `ProjectSettings/GvhProjectSettings.xml` (External Dependency Manager).
- `ProjectSettings/Packages/com.unity.testtools.codecoverage/Settings.json`.
- `Assets/XR/Settings/OpenXR Editor Settings.asset`, `Assets/XR/Settings/OpenXR Package Settings.asset` y
  `Assets/XR/Loaders/OpenXRLoader.asset`: los toca el paquete OpenXR al importar. El proyecto no usa OpenXR; lo que importa
  es que `XRGeneralSettings.asset` no cambie (solo ARCore).
- `ProjectSettings/AndroidResolverDependencies.xml` (External Dependency Manager, en el build de Android).
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

### El SDK de Immersal no queda listo en la primera apertura — configura la cámara una sola vez

Visto en el Xiaomi 14 el 2026-10-13 (1.1.3): a los 15 s salía «El SDK no quedó listo» y nada se ubicaba. `ImmersalSDK.Initialize`
configura la cámara una vez (`ARFoundationSupport.ConfigurePlatform`: 10 intentos cada 100 ms) y, si la cámara no corre
(el diálogo de permiso de cámara de la primera apertura la tiene apagada), queda `IsReady = false` toda la sesión: nadie
reintenta. En el log se ve «Localizer configured» sin «Platform configured» (la línea «Configuring ARF Platform» no sale con
logging Verbose). Desde 1.1.4 `PaseoTour` llama otra vez a `sdk.Initialize()` cuando ARCore ya está en tracking
(`PaseoSdkStart`): es seguro porque salta mapas y localizador ya hechos y solo reconfigura la cámara. **Nunca solapar dos
inicios**: configurarían el localizador dos veces y la sesión de Immersal se cae (clave duplicada en sus tareas); por eso el
primer reintento espera 6 s y los mapas cargados. El log dice `se inicia otra vez (intento N)`.

### Logcat del Xiaomi no guarda las líneas de la app — usar el registro de la sesión

Visto el 2026-10-14 tras una prueba de campo con 1.2.1: `adb logcat -d` no tenía **ninguna** línea de Unity, aunque la app
corrió ~20 minutos; las del sistema desde antes de abrirla sí estaban. El buffer `main` es de 2 MB y logd recorta primero
a la app que más escribe, que era AncoRA: cada `Debug.Log` salía con 5–10 líneas de traza y la app se ubicaba dos veces por
segundo. Desde 1.2.2:
- `PaseoSessionLog` escribe **un archivo por sesión** en el teléfono (los 10 últimos), con las líneas del paseo, las de
  Immersal y todo aviso o error, una línea cada uno (`PaseoLogFormat`), más una línea
  `ESTADO_PASEO bateria=… termico=… margen=… tracking=… comenzado=… sdk=… visibles=… ritmo=… intentos=… fallos=… gps=…`
  cada 10 s (`termico` es `PowerManager.getCurrentThermalStatus`, 3 = el teléfono ya baja el rendimiento; `margen` es
  `getThermalHeadroom(10)`, 1,0 = a punto de bajarlo; `gps` es la precisión en m, `-` sin posición). En 1.2.2 `termico` y
  `margen` salían siempre `-1`/`NaN`: se liberaba `currentActivity`, que es de Unity (corregido en 1.2.3, igual que el
  aviso falso de esquinas de `GuiSafeArea`). Desde 1.2.3 cada intento de ubicación deja una línea `INTENTO_PASEO` (ver
  *1.2.3*). Se baja con:
  `adb -s <serial> pull /sdcard/Android/data/com.ancora.ucnar.paseo/files/registro`
- `Prepare` deja los `Log` y `Warning` **sin traza** (`StackTraceLogType.None`; los errores la conservan) y `Validate` lo
  exige, así logcat también dura más.
- Los ajustes de caja del teléfono se leen con `adb shell run-as com.ancora.ucnar.paseo cat shared_prefs/com.ancora.ucnar.paseo.v2.playerprefs.xml`
  (el build de desarrollo es *debuggable*).

### Verificado que **no** era el problema

Manifest declara `android.permission.CAMERA` · los `.so` de ARCore están presentes y cargan · ARCore
1.56 instalado en ambos teléfonos · el GUID del loader coincide · `ARBackgroundRendererFeature`
presente y activo · stripping en default.

---

## Trampas al tocar el código

**La interfaz es UI Toolkit (desde 1.2.0); el paseo ya no usa IMGUI.** Lo que conviene saber:

- Las vistas (`Scripts/Paseo/Ui/`) arman sus `VisualElement` en código, sin UXML, para que las pruebas EditMode las
  construyan y lean sin panel. Los botones llaman a métodos públicos (`PressStart`, `Press(acción, ahora)`), y las
  pruebas los usan directamente.
- **El tema por defecto fija la fuente de cada texto**, así que heredarla desde la raíz no alcanza:
  `PaseoUiKit.ApplyFonts` pone Inter en línea en cada `TextElement` (semibold en los marcados `is-strong`). **Llamarlo
  de nuevo después de crear textos** (filas del panel, nombres flotantes).
- El tema también estiliza los botones: en `Paseo.uss` se repiten `:hover` y `:active` de cada clase de botón.
- `RuntimePanelUtils.ScreenToPanel` espera píxeles con origen **arriba** a la izquierda (como `GuiSafeArea.Rect`); las
  posiciones de `Input`/`WorldToScreenPoint` tienen el origen abajo. Para pasar del mundo al panel se usa
  `CameraTransformWorldToPanel`.

Si alguna vez vuelve IMGUI (el tag `archivo/cartel-pace` lo usa): `OnGUI` corre una vez para `Layout` y otra para el
evento real, y si emiten distinta cantidad de controles Unity tira `Mismatched LayoutGroup`; los cambios de panel se
encolan hasta después de `GUILayout.EndArea()`.

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

## Paseo virtual (desde 2026-10-08)

Paseo **en el campus** por los edificios de la escuela de ingeniería. Una escena carga los mapas Immersal de todos los
edificios con **un XR Space por mapa**: ubicarse con un mapa mueve solo su espacio, así que se pueden ver dos edificios a la
vez, cada uno en su lugar. Por edificio se muestra la caja del mapa que localizó último. Diseño:
`docs/superpowers/specs/2026-10-08-paseo-virtual-design.md`; plan: `docs/superpowers/plans/2026-10-08-paseo-virtual.md`.

| Edificio (carpeta) | Nombre visible | Mapas | Caja |
|---|---|---|---|
| `CienciasBasicas` | Ciencias Básicas | `152192-csbasicasgael`, `152196-csbasicasgael2`, `152257-csbasicasnuevo1` | corregida en terreno; nuevo mapa conserva la corrección |
| `EIC` | EIC *(provisional)* | `152242-Eicnicowo` (2026-10-09) | desde el dron (solo el bloque) |
| `X1` | X1 | `152198-x1gael`, `152200-x1johan` (agregado el 2026-10-08) | desde el dron |
| `Teologia` | Teología | `152259-teologianuevo1` (desde 1.1.3; `152195` y `152241` salieron) | desde el dron |

Los siete `.bytes` cargan en el plugin del Editor (1596 a 30304 puntos). Llegaron sin `-metadata.json` ni `-sparse.ply`:
el SDK toma id y nombre del nombre del archivo y deja la alineación en identidad, que es lo que el paseo necesita.
**`152199-eicgael` no cubre el edificio**: sus 3389 puntos caben en 1,2 × 0,5 × 1,9 m (los demás mapas miden 9–55 m), así
que solo ubicaría mirando ese punto y no hay cómo colocar su caja desde el dron. La causa está en su captura: la cámara se
movió solo 2,4 × 4,0 m en 79 fotos (giró en un lugar), y sin desplazamiento el Mapper no triangula la fachada lejana; las
capturas buenas recorren 12–36 m. `152194-Cienciasbasicas` (un tercer mapa de Ciencias Básicas) no registró contra la nube
del dron (14 % de puntos a < 25 cm, soluciones a 60 m entre sí) y quedó fuera del paseo; el archivo está en Descargas.

**Capturas crudas del Mapper** (`Descargas\AncoRA-20261009T004116Z-1-001.zip`, de Drive): un zip por mapa con una `.png` y un
`.json` por foto (pose `px/py/pz` + `r00..r22`, intrínsecos `fx/fy/ox/oy`, GPS del teléfono). Sirven para diagnosticar un
mapa sin abrir el portal. `Fachadaingedetras.zip` (fachada trasera de ingeniería, 30 fotos, sin GPS) **no tiene mapa
construido** y su cámara también se movió poco (2,8 × 7,6 m).

**Datos:** `Assets/AncoRA/Paseo/<Edificio>/edificio.json` + `.bytes` (metadata y `.ply` opcionales). `edificio.json` manda:
`Prepare` rearma la escena desde cero cada vez. Agregar un edificio = carpeta nueva + `Prepare`. El campo opcional
`"gps": {"latitud", "longitud"}` es el centro de la caja sobre el suelo, sacado de la nube del dron (`centroEnu` de
`caja-dron.json` + `lat0`/`lon0` de su `-georef.json`); sin él, `Validate` avisa y la búsqueda por cercanía deja ese
edificio después de los que tienen GPS.

**Código:** runtime en `Assets/AncoRA/Scripts/Paseo/` (`PaseoTour`, `PaseoMapContent`, `PaseoPoseProcessor`, `PaseoUi`,
método de ubicación `PaseoLocalization` + lógica pura `PaseoConfig`, `PaseoAdjustment`, `PaseoVisibility`,
`PaseoStatusText`, `PaseoBoxStore`, `PaseoPlacement`, `PaseoMapLoad`, `PaseoPoseFilter`, `PaseoLocalizationSettle`,
`PaseoBoxSync`, `PaseoSdkStart`, `PaseoBoxNudge`, `PaseoStartFlow`, `PaseoPermissionFlow`, `PaseoCard`, `PaseoCardChoice`,
`PaseoTapGesture`, `PaseoMapRow`, `PaseoPace`, `PaseoLogFormat`, `PaseoSearchPlan` (con `PaseoGeo` y `PaseoGpsFix`),
`PaseoAttempt`, `PaseoActivity`, `PaseoReplacementLayout`, `PaseoDebug`; registro `PaseoSessionLog`; reemplazo del edificio
`PaseoReplacement` con el shader `_Materiales/PaseoFondo.shader`); vistas UI Toolkit en
`Scripts/Paseo/Ui/` (`PaseoWelcomeView`, `PaseoCameraView`, `PaseoLabelsView`, `PaseoTeamSheet`, `PaseoUiKit`); estilos,
tema, `PanelSettings` y fuente Inter (copiada de la instalación de Unity, licencia OFL en `Fuentes/Inter-LICENSE.txt`) en
`Assets/AncoRA/Paseo/_Ui/`; Editor en `Assets/AncoRA/Editor/Paseo/`; pruebas EditMode en `Assets/AncoRA/Editor/Tests/` (226).
Diseño de la interfaz: `docs/superpowers/specs/2026-10-13-interfaz-guia-diagnostico-design.md`; plan de la fase 1:
`docs/superpowers/plans/2026-10-13-fase1-interfaz.md`.

**Filtro de pose (2026-10-09, medido en la caja desde el 2026-10-13).** Cada XR Space tiene `ProcessPoses` encendido y un
`PaseoPoseProcessor` como único procesador (lo pone `Prepare`, lo exige `Validate`). La lógica está en `PaseoPoseFilter`:
- **Se mide donde está la caja, no en el origen del mapa.** Las cajas están a 10–33 m del origen de su mapa, y medido ahí
  un giro de 5° movía la caja de Teología 2,8 m sin que el filtro lo notara. Cuánto difieren dos ubicaciones = cuánto se
  mueve la esquina de la caja que más se mueve. El procesador lee la caja (`PaseoMapContent`) de su XR Space en cada frame,
  así que sigue los cambios del panel de ajuste.
- **Solo giro en torno a la vertical.** El mapa y ARCore están alineados con la gravedad (el registro con el dron ya lo
  supone), así que la inclinación de cada ubicación es ruido: se quita girando en torno a la cámara, que queda donde la ponen
  los dos marcos. Una ubicación inclinada más de 5° se ignora (`inclinada`): es una coincidencia mala.
- **Desde 1.2.3 la caja aparece con la primera ubicación** (`fijada`), y la tarjeta dice «Confirmando». Una segunda
  ubicación que la movería ≤ 0,6 m la confirma y la tarjeta pasa a «Ubicado». Hasta 1.2.2 no se mostraba nada hasta que
  coincidieran dos, y en la prueba del 14-oct la caja tardaba decenas de segundos en aparecer.
- Después acepta solo ubicaciones que moverían la caja ≤ 1 m y sigue el **promedio de las últimas 6** aceptadas, **ponderado
  por la confianza** de Immersal (peso completo desde 50, mínimo 0,1). El promedio se toma en el centro de la caja.
- **Descartadas seguidas que coinciden entre sí** re-anclan ahí (la fijación estaba mal): 2 mientras la caja no está
  confirmada (descansa en una sola ubicación), 3 después.
- Entre promedios la caja **se desliza** en 0,5 s (su centro en línea recta); al fijar o re-anclar se coloca de inmediato.
- Un mapa cuenta como ubicado solo con una ubicación que el filtro **usó** (fijada, aceptada o re-anclada): antes, una
  descartada también lo marcaba y lo volvía el mapa visible y editable de su edificio (`PaseoLocalizationSettle.Decide`).
- Si ARCore pierde el tracking, `PaseoTour` reinicia los filtros (`XRSpace.TriggerResetScene`) y no da un mapa por ubicado
  hasta que su filtro vuelva a tener pose.
- **Una línea de log por ubicación**, para calibrar con logcat:
  `UBICACION_PASEO mapa=152198 veredicto=aceptada conf=34 incl=0.62 mueve=0.23 dist=18.4 confirmada=1` (números con
  punto; `mueve` = cuánto movería la caja respecto de la pose filtrada, `-` antes de fijar; `dist` = cámara al centro de la
  caja, en m; `confirmada` = la caja ya descansa en dos ubicaciones que coinciden). Veredictos: `fijada`, `aceptada`,
  `lejos`, `reanclada`, `inclinada` (`esperando` salió en 1.2.3). La hoja del equipo muestra la última por mapa y cuántas
  usó de las que vio.
- Los umbrales (0,6 m, 1 m, 5°, confianza 50) son una primera estimación, sin medir en terreno: calibrarlos con esos logs.
  Falta decidir un mínimo de confianza; por ahora solo pondera.

**Correcciones en edificios con varios mapas (2026-10-09).** Cada mapa guarda la misma caja física en su propio marco.
Antes, el panel corregía solo el mapa ubicado y la vista **alternaba** entre la caja corregida y la vieja según qué mapa
ubicaba último: en terreno X1 «se volvía loco» aunque se corrigiera. Ahora `PaseoTour.SaveEdit` copia la corrección a los
demás mapas colocados del edificio con `PaseoBoxSync.Carry`: el cambio visto desde la caja vieja (movimiento en sus ejes +
giro en torno a su centro) se aplica a cada otra caja. No se copia desde ni hacia una caja sin colocar. Los dos mapas de X1
coinciden entre sí a 0,34 m (alineados directo mapa contra mapa), así que el salto venía de la corrección, no del registro.

**Comandos** (Editor cerrado; `Tools/UnityBatch.ps1` espera a Unity y resume el resultado):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSmokeTest.Run
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.Prepare
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.CheckNativeMapsInEditor
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.BuildAndroid
```

`UnityBatch.ps1` va guardado como UTF-8 **con BOM**: Windows PowerShell 5.1 lee sin BOM como ANSI y la «Ó» rompe el script.
`Prepare` también fija los gráficos de iOS (Metal, override activo): al guardar los ajustes, Unity 6000.6 los volvía
automáticos en `ProjectSettings.asset`.

APK de desarrollo: `Builds/Android/AncoRAPaseo.apk`, paquete `com.ancora.ucnar.paseo`, «AncoRA Paseo».

**Preparación del 13-oct:** versión 1.1.0, código Android 2. Se incorporan solo `152257` (Ciencias Básicas) y
`152259` (Teología), registrados contra el dron a escala 1. Sus cajas se transfieren desde los mapas 152192 y 152195:
Ciencias Básicas conserva la corrección hecha en terreno y las poses existentes no cambian. `152254`/`152256` quedan
fuera por registro ambiguo; `152255` requiere revisar el ajuste de escala. Depth es opcional: la oclusión está desactivada.
El HUD cuenta intentos/fallos globales y éxitos por mapa; un arranque tardío del SDK limpia el error. ARCore no soportado
tiene prioridad en el banner. `Prepare` conserva el GUID de la escena al regenerarla.

**1.1.1 (13-oct):** código Android 3. Mismos nueve mapas y cajas que 1.1.0; cambia el filtro de pose (medido en la caja,
nivelado, ponderado por confianza, una descartada ya no ubica el mapa) y cada ubicación deja una línea `UBICACION_PASEO`
en logcat (ver *Filtro de pose*).

**1.1.2 (13-oct):** código Android 4. Mismos nueve mapas. La sesión de Immersal intenta ubicarse **cada 0,5 s** (el SDK traía
2 s): la ráfaga de inicio del SDK dura 15 s o 10 éxitos en total y no vuelve, así que en el segundo edificio se intentaba
una vez cada 2 s, y con mapas que fallan la mayoría de los intentos las dos ubicaciones que pide el filtro tardaban
mucho. Lo fija `Prepare` y lo exige `Validate` (`PaseoSetup.LocalizationIntervalSeconds`). Medir en terreno calor y
batería en una sesión de 10 minutos; si calienta, subir a 1 s.

**1.1.3 (13-oct):** código Android 5. Teología queda solo con `152259` (20 959 puntos, registro claro con el dron: margen
2,2×); salen `152195` y `152241` (~1 700 puntos, registro ambiguo), que cuando ubicaban último podían mover la caja. Lo
único que cubrían mejor que `152259` es el costado +X del edificio: si desde ese lado no ubica, hace falta un mapa de ese
costado. El paseo queda con 7 mapas.

**1.1.4 (13-oct):** código Android 6. Reintenta el inicio del SDK cuando la cámara no corría al configurarse (ver *Causas
raíz*: «El SDK de Immersal no queda listo en la primera apertura»).

**1.2.0 (13-oct):** código Android 7. Interfaz nueva en UI Toolkit, estilo iOS (fase 1 del spec de la interfaz):
- **Bienvenida** en cada apertura: «Comenzar» pide la cámara y después la ubicación, de a un diálogo
  (`PaseoPermissionFlow` relee el permiso cuando la app recupera el foco; uno negado para siempre no la cuelga). **La
  `ARSession` queda apagada en la escena** (lo fija `Prepare`, lo exige `Validate`) y la enciende la bienvenida; el
  paseo (`PaseoTour.Begin`) empieza a contar el arranque del SDK recién ahí. Sin cámara la bienvenida explica y se
  queda; sin ubicación el paseo arranca igual. El botón «?» la vuelve a mostrar.
- **Cámara:** píldora de estado arriba (`PaseoStatusText`), tarjeta abajo con una pista o el edificio reconocido (el
  más centrado, con la `descripcion` nueva de `edificio.json`) y el nombre de cada edificio flotando sobre su caja.
- **Hoja del equipo** (5 toques arriba a la izquierda): una fila por mapa (carga, ubicado/visible, último veredicto del
  filtro con su confianza, usadas/vistas) y el ajuste de caja.
- Salen `PaseoHud`, `PaseoFieldAdjust` y `PaseoLabel` (IMGUI y `TextMesh`).

**1.2.1 (14-oct):** código Android 8. El reintento de inicio del SDK contaba su espera de 6 s desde «Comenzar» (con la
`ARSession` apagada tras la bienvenida, el arranque propio del SDK siempre falla): la app tardaba al menos eso en empezar a
ubicarse. Ahora cuenta desde la carga de la escena. Publicada como pre-release `paseo-2026-10-14`.

**1.2.2 (14-oct):** código Android 9. Registro de la sesión en el teléfono y logs sin traza (ver *Causas raíz*: «Logcat del
Xiaomi no guarda las líneas de la app»). **Ritmo de ubicación adaptable** (`PaseoPace`, aplicado por `PaseoTour` a
`ImmersalSession.SessionUpdateInterval`): un intento cada 0,5 s mientras no haya una caja ubicada a la vista; cada 2 s cuando
la cámara lleva 3 s mirando un edificio ya ubicado; vuelve a 0,5 s apenas se deja de mirarlo. En la prueba del 14-oct, con
0,5 s fijo, la app gastó ~1,2 núcleos de CPU en promedio y el teléfono quedó con la CPU a 82–90 °C y la batería a 41 °C.
El log dice `Ritmo de ubicación: un intento cada …`.

**1.2.3 (21-oct):** código Android 10. Sale del registro de la prueba del 14-oct con 1.2.2. En 2 min 20 s hubo solo 18
intentos, aunque el ritmo era de 0,5 s: cada intento **sin** coincidencia tardaba 4–16 s (9,5 s en promedio), y uno que
ubicaba tardaba 0,7–2,5 s. El `DeviceLocalization` del SDK le pide al plugin **los 7 mapas** en cada intento (`n = 0`),
unos 120 mil puntos.
- **Búsqueda por edificio.** `PaseoLocalization` reemplaza a `DeviceLocalization` como único método del `Localizer` (lo
  pone `Prepare`, lo exige `Validate`). Llama a `Core.LocalizeImage(n, ids, …)` solo con los mapas que elige
  `PaseoSearchPlan`. El orden de los edificios es: el ubicado que está a la vista, después los más cercanos por GPS y
  después el orden de la configuración. En cada ciclo de 6 intentos se busca así: rango 0, 1, 0, 2, 0 y al final **todos
  los mapas**, por si el GPS se equivoca. Sin GPS ni edificio a la vista, cada intento busca en todos los mapas, como
  antes. El GPS sirve con precisión ≤ 50 m y antigüedad ≤ 30 s (`PaseoGpsFix`). Se enciende al cerrar la bienvenida si
  hay permiso de ubicación y pide cada posición aunque el teléfono no se mueva. Los centros de los edificios están a 21–60 m
  entre sí. Que un intento fallido baje de ~10 s a 1,5–3 s es una estimación: **falta medirlo en terreno**.
- Un intento cuyo edificio no tiene mapas cargados busca en todos (un id sin *handle* hace fallar todo el intento,
  `MapHandleMapping.IdsToHandles`), y su línea lo dice.
- **Una línea por intento:** `INTENTO_PASEO n=12 busca=Teologia mapas=1 motivo=gps dist_gps=18 seg=1.84 resultado=152259 conf=14`
  (`motivo`: `gps`, `a-la-vista`, `orden` o `todos`; `dist_gps` = teléfono al centro del edificio; `resultado` = mapa o
  `ninguno`).
- **Aviso de búsqueda** bajo la píldora (`PaseoActivity`): «Buscando Teología · 7 imágenes sin coincidencia»; en verde
  «Coincidencia con Teología · confianza 14»; en ámbar las descartadas con su motivo (inclinada o que movería la caja). En
  la prueba del 14-oct el equipo intentó varias veces con Teología sin saber si la app veía algo.
- **La caja aparece con la primera ubicación**, con «Confirmando» (ver *Filtro de pose*).
- El reintento de inicio del SDK ya no espera 6 s fijos: arranca apenas `PaseoLocalization` fue configurado, que es el
  último paso del inicio propio del SDK.
- `edificio.json` trae `gps` en los 4 edificios.

**Rama `nicowo` (1.2.4 y 1.3.1, 21-oct):** códigos Android 12 y 13. Es la rama de trabajo para refinar la detección
antes de sacar la versión final. La 1.2.4 se publicó como pre-release `paseo-2026-10-21`. La 1.3.1 integra además la rama
del reemplazo, activo en Teología (cubo), Ciencias Básicas (esfera) y X1 (cilindro); EIC sigue con su caja (ver *Reemplazo
del edificio*). Agrega depuración que solo existe en builds de desarrollo (`PaseoDebug.Enabled` = `Debug.isDebugBuild`;
`BuildAndroid` siempre compila así). Un build de release de la versión final sale sin nada de esto, sin tocar código.
- `DEPURACION_INTENTO n=12 img=1920x1080 cam=-3.21,1.48,7.90 rumbo=123 tracking=SessionTracking vista=Teologia ritmo=0.5 gps=-29.964054,-71.349144 prec=6 edad=1.2 gps_util=1`
  después de cada `INTENTO_PASEO` (mismo `n`). `img` es la imagen de la cámara: con `reducida=1` el plugin la baja a
  960 × 720 como máximo. `cam` y `rumbo` son la cámara AR al capturar, en el mundo AR. `vista` es el edificio ubicado a la
  vista. El GPS va **crudo** aunque no sirva (`gps_util=0`). Con esto un intento fallido se puede ubicar después.
- `DEPURACION_TRACKING estado=… motivo=… t=…` en cada cambio de estado de la `ARSession` o de `notTrackingReason`;
  `DEPURACION_CAMARA config=1920x1080@30 reducida=1` una vez, cuando AR empieza a seguir; `DEPURACION_GPS estado=…
  activado_por_usuario=…` en cada cambio del servicio de ubicación.
- **Pruebas de detección** en la hoja del equipo (solo en builds de desarrollo): «Imagen: reducida / completa» cambia
  `ImmersalSDK.downsample`, y «Búsqueda: un edificio por intento / todos los mapas» fuerza la búsqueda en todos los mapas
  (`PaseoSearchPlan.EveryMap`). Cada cambio deja `DEPURACION_AJUSTE imagen=… t=…` o `busqueda=… t=…`. Sirven para comparar
  en el mismo lugar cuánto tarda y cuánto ubica cada opción. Los dos vuelven a lo normal al reabrir la app.
- Para refinar con datos: correr un recorrido, bajar el registro (`adb pull …/files/registro`) y cruzar `INTENTO_PASEO`
  con `DEPURACION_INTENTO` por `n`.

**Reemplazo del edificio (1.3.0, rama `feature/reemplazo-edificio`, 21-oct):** código Android 11. Al ubicar un edificio, el
edificio real **desaparece de la pantalla** y otro objeto ocupa su lugar. A futuro, la idea es mostrar el edificio como era
antes, en 3D y animado. El MVP pone objetos simples: en la rama del reemplazo, un cubo de 4 m que gira sobre Teología; en
`nicowo` (1.3.1), además una esfera en Ciencias Básicas y un cilindro en X1. Se configura por edificio en `edificio.json`:

```json
"reemplazo": { "activo": true, "modelo": "", "forma": "cubo", "escala": 4, "posicion": [0, 1, 0], "giro": 0, "girar": 45, "ocultar": true, "margen": 1 }
```

- `modelo`: un `.fbx`, `.obj` o `.prefab` en la carpeta del edificio, con el pivote en su base. Vacío = la `forma` (`cubo`,
  `esfera`, `cilindro` o `cápsula`, una primitiva de Unity apoyada en su base; desde 1.3.1). Un `.glb`
  necesita el paquete glTFast, que el proyecto no tiene. `escala` es uniforme (para el cubo, su lado en m). `posicion` se
  mide desde el centro de la base de la caja, en sus ejes. `giro` es el giro inicial y `girar` los grados por segundo en
  torno a la vertical.
- **Cómo desaparece:** la cámara no ve lo que hay detrás del edificio, así que se pinta encima. `PaseoReplacement` arma
  una máscara, que es la caja agrandada `margen` m por cada lado (arriba y abajo también). La dibuja con el shader
  `AncoRA/Paseo/Fondo`: caras interiores (`Cull Front`), opaco y escribiendo profundidad, con cielo sobre el horizonte y
  suelo bajo él según hacia dónde mira la cámara. Las caras interiores quedan detrás del modelo, así que el modelo se dibuja
  delante sin trucos de orden. Con `ocultar: false` no hay máscara y el modelo queda sobre la imagen real.
- La caja amarilla no se dibuja en un edificio con reemplazo. `PaseoMapContent.IsShown` es lo que usan la tarjeta y los
  nombres flotantes. La máscara sigue el tamaño de la caja en cada frame, así que el ajuste del panel del equipo también la
  mueve.
- Los modelos van iluminados: `Prepare` agrega la «Luz de los reemplazos» (direccional, sin sombras) y luz ambiente plana.
  `Validate` exige la luz, el reemplazo igual a `edificio.json` y el shader de fondo sin errores.
- **Vista previa sin terreno:** `AncorRA.Editor.PaseoReplacementPreview.Render` escribe `Logs/reemplazos/<edificio>-<mapa>-antes.png`
  y `-despues.png`, vistos desde el frente a la altura de los ojos. Necesita GPU, así que se corre con
  `Unity.exe -batchmode -quit -projectPath … -executeMethod AncorRA.Editor.PaseoReplacementPreview.Render` **sin**
  `-nographics`; `UnityBatch.ps1` siempre lo pasa.
- **Límites:** el borrado es tan bueno como la caja. Donde la caja quede corta se asoma el edificio real, y el `margen`
  lo compensa a cambio de tapar más alrededor. El cielo y el suelo tienen colores fijos (propiedades del material
  `PaseoReemplazoFondo`, que `Prepare` rehace) y no se parecen al cielo real de ese día. Lo que hay detrás del edificio
  (árboles, otros edificios) desaparece con él. Falta: GIF o video (Unity no reproduce GIF; sería un `.mp4` con
  `VideoPlayer`), las animaciones propias de un modelo y tomar el color del cielo de la imagen de la cámara. Sin probar en terreno.

**macOS (Editor cerrado):** usar el ejecutable
`/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity` con
`-batchmode -nographics -projectPath /Users/gael/Documents/GitHub/AncoRA`. Para tests, agregar
`-runTests -testPlatform EditMode -testFilter AncorRA.Tests -testResults <xml> -logFile <log>` (sin `-quit`).
Para métodos, agregar `-quit -executeMethod AncorRA.Editor.PaseoSetup.<Metodo> -logFile <log>`.
Orden al cambiar mapas: tests → `Prepare` → `CheckNativeMapsInEditor` → `BuildAndroid`.
La prueba de humo `AncorRA.Editor.PaseoSmokeTest.Run` usa mapas de ejemplo y restaura la lista de escenas.
Guía de terreno y validación del candidato: `Docs/GuiaPrueba-2026-10-13.md`.

**En terreno:** cada caja sin colocar aparece a 12 m delante de la cámara con «(sin colocar)». Hoja del equipo: 5 toques
arriba a la izquierda → sección «Ajuste de caja» (edita la caja del último mapa ubicado; se guarda en el teléfono con prefijo
`AncoRA.Paseo.v1.`). Los botones mueven la caja como la ve alguien parado frente a la cara con la X: Izquierda/Derecha,
Más cerca/Más lejos, Bajar/Subir, Girar, y ancho/alto/fondo, con paso grueso (1 m, 5°, 2 m), medio (0,25 m, 1°, 0,5 m) o
fino (0,05 m, 0,2°, 0,1 m) (`PaseoBoxNudge`). «Copiar valores» → pegar en `Assets/AncoRA/Paseo/ajuste-campo.json` →
`PaseoSetup.ApplyFieldAdjustment` (escribe `edificio.json` y rearma la escena). Ciencias Básicas se coloca una vez en cada
uno de sus dos mapas. Cada ajuste del teléfono recuerda de qué valores horneados partió: si la escena se hornea de nuevo con
valores distintos, el ajuste viejo se descarta solo (y no vuelve a `edificio.json`). La hoja marca «valores del teléfono».

**Pendientes del paseo:**
- [ ] 1.2.3 en terreno: con las líneas `INTENTO_PASEO`, medir cuánto tarda un intento sobre un edificio frente a uno sobre
  todos los mapas, y si el GPS elige el edificio que se mira (`motivo=gps`, `dist_gps`). Si junto a los edificios el GPS
  apunta mal seguido, dar más intentos al rango 1 o a «todos» en `PaseoSearchPlan`.
- [ ] Prueba en terreno: cada edificio localiza; con dos a la vista, ubicarse con el segundo **no mueve** el primero;
  Ciencias Básicas cambia de mapa sin duplicar la caja; nombre legible desde la distancia de observación.
- [ ] Revisar en terreno las cajas puestas desde el dron (X1, Teología); corregir con el panel si hace falta y hornear con
  `ApplyFieldAdjustment`. **Ciencias Básicas ya se corrigió en terreno el 2026-10-09** (mapa `152192`: posición
  (−11,64; 1,60; 3,59), giro 79,1°, 19,9 × 7,4 × 10,7 m; dictado por chat y llevado al `152196` con `PaseoBoxSync`):
  respecto del dron, 1,23 m más atrás, 0,26 m a lo largo y 0,5 m más abajo.
- [ ] El APK pesa ~84 MB aunque su contenido comprimido es el mismo que el de 66 MB: espacio sin usar dentro del zip, no
  en `Builds/` (borrar el APK antes del build no cambia nada). Revisar el empaquetado de Gradle.
- [x] Mapa de EIC rehecho: `152242-Eicnicowo` (30 × 27 m, capturado desde el patio norte). `Fachadaingedetras` sigue sin
  construir y se movió poco al capturarlo.
- **Un mapa sin colocar no debe estar en un edificio que ya tiene uno colocado**: la vista muestra la caja del mapa que
  ubicó último, así que cuando ubica el sin colocar la caja salta 12 m delante de la cámara. Por eso salió `152199` el
  2026-10-09.
- **Mapas guardados sin usar** (`Escenas/<Edificio>/mapas-sin-usar/`, ignorada por git; originales en el Drive compartido
  `AncoRA/bytes`, subcarpeta `gael 9-10`; sus nubes `-sparse.ply` sí están en `Escenas/<Edificio>/`):
  - Diminutos (la captura casi no se movió, igual que `152199`): `152199-eicgael`, `152240-eic4` (EIC); `152243`, `152245`,
    `152247`, `152248-teologiagael*` (Teología, 1–5 m); `152250-x1gael3` (X1, 4 × 2 m). **Las capturas de Gael tienen
    este problema**: hay que recapturar caminando, no girando en un lugar.
  - `152195-Teologianicowo` y `152241-Teo67` (Teología, ~1 700 puntos cada uno): salieron en 1.1.3, reemplazados por
    `152259`. Sus cajas, registros y nubes siguen en `Escenas/Teologia/` por si vuelven.
  - `152331-teologianublado` (Teología, 19 231 puntos, capturado nublado el 13-oct): **no entra todavía**. Contra
    `TeologiaBajo.ply` calza el 60 % a < 25 cm pero es ambiguo (segunda solución a 1,03×, la caja cambia 1,4–1,6 m), y
    registrado directo contra `152259` deja la caja a 2,2 m y 1,5° de donde la pone el dron (como control, `152257`
    contra `152192` coincide a 0,37 m). Ve el frente (80 %) y el lado +X, que ya cubren otros mapas; el 45 % de sus
    puntos no está junto al edificio y ~2 % está a kilómetros (nubes del cielo triangulados). Con su caja así, la de
    Teología saltaría ~2 m cuando ubica último. Entraría con la caja colocada en terreno (panel de ajuste) contra la de
    `152259`. Su origen está a ~190 m del edificio: queda al borde de la búsqueda gruesa de `register.js` (±160 m) y
    fuera de la de `ab_direct.js` (±120 m). Para registrar mapa contra mapa así, `Tools/RegistroDron/mapa_contra_mapa.js`
    centra antes las dos nubes y lleva la caja de un mapa al otro.
  - De EIC con buen tamaño pero sin registro: `152237-eicRightjohan` (27 × 30 m) y `152238-eic3` (11 × 20 m). Contra
    `EIC2.ply` dan 16 % y 19 %, ambiguos; contra `152242` no comparten nada (6 %) o se contradicen con el dron. Ven el
    lado de EIC que el dron no cubre: se colocan cuando estén las 26 fotos que faltan.
- [ ] EIC: revisar en terreno su caja, sobre todo la altura. Es solo el **bloque** de 15,1 × 9,3 × 18,7 m (opción elegida
  por el usuario); el ala larga de ~38 m al oeste no está representada. El suelo que ve su mapa queda 1,57 m bajo la base de
  la caja: el terreno tiene dos niveles (−5 y −2 m en la nube) y el mapa se capturó desde el lado bajo.
- [ ] Volver a subir las **26 fotos del dron del 2026-10-09 que llegaron en cero** desde Drive (15:32:10–15:32:32 y
  15:33:10–15:33:36, en `Imgs/2026-10-09/_vacias/`): son la vuelta alrededor de EIC que falta. Con ellas mejora la nube
  `EIC2` y el registro de `152242` (hoy 35 % a < 25 cm, el más débil del paseo).
- [ ] Nombre visible definitivo de EIC.

### Fotogrametría con dron (2026-10-08)

Nubes de puntos de los edificios del paseo, armadas en RealityScan con fotos del DJI Mini 5 Pro, para **medir cada
edificio y colocar su caja dentro de cada mapa** sin ajustarla a mano en terreno. La app no las usa.

**Fotos:** vuelo del 2026-10-08 (163 fotos y 4 videos) en `Imgs/<Edificio>/`, ignorada por git. `Imgs/separacion.csv` dice
qué foto fue a qué carpeta: se separaron por corridas de captura (hora EXIF y saltos de GPS) y mirando una muestra de cada
una; el GPS solo no alcanza porque los edificios están a 20–40 m y en un mismo tramo el dron pasaba de uno a otro. El vuelo
del 25-sep (solo Teología, 87 fotos) sigue en `C:\Users\nicol\Downloads\Fotos Ing`. Los videos no se han usado.

**Flujo** (un comando por edificio; RealityScan 2.2 sin interfaz, no hace falta cerrar el que esté abierto):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\AlinearDron.ps1 -Edificio X1 -Fotos .\Imgs\X1
```

Alinea, exporta la nube cruda y las poses (COLMAP) y llama a `Tools/RegistroDron/georef_dron.js`. En `Escenas/<Edificio>/`
quedan `<Proyecto>.ply` (x este, y norte, z arriba, metros) y `<Proyecto>-georef.json` (transformación, corrección por
vuelo, residuos), que **sí se commitean**; el `.rsproj`, su carpeta de datos y `rs-<Proyecto>/` (nube cruda, poses,
reporte) están en `.gitignore`. Los proyectos se abren en RealityScan para revisarlos o calcular una malla.

**Por qué la escala sale del GPS y no de RealityScan:**
- Las fotos que exporta DJI Fly **no traen latitud ni longitud en el EXIF**, solo en el XMP (`drone-dji:GpsLatitude`…).
  RealityScan las usa como referencia blanda, pero su reporte dice «Georreferenciado False» y la escala con que termina
  **cambia en cada corrida**: la misma alineación de X1 salió a 0,995 y a 1,498 del GPS.
- La trayectoria del GPS sí calza con las cámaras reconstruidas, a 0,1–0,2 m. `georef_dron.js` ajusta una semejanza
  (escala, giro, traslación) de las cámaras al GPS y la aplica a la nube.
- **Cada vuelo trae su propio sesgo de GPS**: la altitud absoluta del XMP cambió 37 m entre dos días y 2,45 m entre dos vuelos
  de la misma tarde, y en horizontal se corrió 3,8 m (EIC). Los vuelos se distinguen por `AbsoluteAltitude −
  RelativeAltitude` (constante dentro de un vuelo); cada vuelo salvo el principal recibe su propio desplazamiento 3D.
  z = 0 es el despegue del vuelo principal.
- Por lo mismo, lo que decía antes este archivo («sin puntos de control la escala del GPS salía un 30 % chica») estaba
  equivocado: esa nube nunca tuvo escala de GPS.

| Edificio | Fotos alineadas | Error de alineación | Residuo cámara↔GPS | Escala corregida |
|---|---|---|---|---|
| X1 | 50/50 | 0,64 px | 0,18 m | 0,995 |
| Ciencias Básicas | 28/34 | 0,66 px | 0,13 m | 1,424 |
| EIC (dos vuelos) | 52/52 | 0,61 px | 0,18 m | 0,979 |
| Teología (`TeologiaAlto`, a 10–14 m) | 25/27 | 0,64 px | 0,10 m | 1,077 |

**Teología mide ~22,3 m, no 20.** El GPS de los dos vuelos y el mapa Immersal coinciden: el vuelo del 25-sep da un factor
1,117 sobre `Teologia.ply` (escalada a mano a 20 m), con el GPS del 8-oct el techo mide 22,5 × 10,9 m con aleros, y el mapa
`152195` registrado con escala libre contra la nube del 25-sep georreferenciada da 0,985. La caja vieja (20 × 7,4 × 9,6 m)
era ~10 % chica. Registrar las dos nubes de dron entre sí con escala libre **no decidió nada**: se solapan poco (una mira
fachadas desde 4–6 m, la otra el techo desde arriba).

**Escala del GPS contra ARCore.** Cada mapa Immersal es métrico por ARCore; registrado contra su nube con escala libre da
0,981 (X1), 0,985 (Teología), 0,975 y 1,067 (Ciencias Básicas, el segundo con solape parcial). La escala del GPS sirve sin
puntos de control.

### Cajas del paseo desde el dron (2026-10-08)

```powershell
# 1. Nube de cada mapa desde su .bytes -> Escenas/<Edificio>/<id>-<nombre>-sparse.ply (formato y ejes del portal)
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoMapClouds.Export
```
```bash
# 2. Registrar el mapa contra la nube del dron (giro + traslación, escala 1) -> <id>-registro.json
node Tools/RegistroDron/registrar_mapa.js Escenas/X1/X1.ply Escenas/X1/152198-x1gael-sparse.ply Escenas/X1/152198-registro.json
# 3. Caja en la nube del dron y su pose en cada mapa -> caja-dron.json y edificio.json (colocada = true)
node Tools/RegistroDron/caja_dron.js Escenas/X1/X1.ply Escenas/X1/caja-dron.json --region=-19,-21,6,18 --techo=3.2,4.9 --claro=150 --frente=3.8,-0.2 --escribir=Assets/AncoRA/Paseo/X1/edificio.json Escenas/X1/152198-registro.json Escenas/X1/152198-x1gael-sparse.ply
```

Los parámetros de cada edificio están en su `caja-dron.json`. Después: `PaseoSetup.Prepare` y `BuildAndroid`.

| Edificio | Mapa → nube | Puntos del mapa a < 25 cm | Caja (ancho × alto × fondo) | Cómo se midió |
|---|---|---|---|---|
| X1 | `152198`, `152200` → `X1.ply` | 89 % (rms 0,12 m) y 73 % (rms 0,18 m) | 27,6 × 4,5 × 14,0 m | techo claro a 3,2–4,9 m; frente dado (fachada de las carpas) |
| Ciencias Básicas | `152192`, `152196` → `CienciasBasicas.ply` | 74 % y 65 % | 19,9 × 7,9 × 10,7 m (en terreno: 19,9 × 7,4 × 10,7) | techo claro a 5,6–7,0 m; **corregida en terreno el 2026-10-09** |
| Teología | `152195`, `152241` → `TeologiaBajo.ply` | 60 % (rms 0,22 m) y 58 % (rms 0,23 m) | 22,3 × 8,3 × 10,7 m | `--huella`: paredes medidas en la nube del 25-sep, llevadas a metros |
| EIC | `152242` → `EIC2.ply` (vuelos del 8-oct + 10 fotos del 9-oct) | 35 % (rms 0,26 m) | 15,1 × 9,3 × 18,7 m | techo claro a 4–7,5 m del bloque; frente dado hacia el patio norte. Contra `EIC.ply` (solo 8-oct) no registraba (21 %, ambiguo) |

- `TeologiaBajo.ply` es la nube del 25-sep georreferenciada con su GPS: tiene las fachadas que ve el mapa (desde el suelo).
  Contra `TeologiaAlto.ply`, que ve sobre todo el techo, el mapa calzaba solo un 21 %. La caja se mide en la **misma** nube
  contra la que se registró el mapa: cada vuelo tiene su propio sesgo de GPS.
- Comprobación independiente con solo `edificio.json`: los puntos de cada mapa caen en la cara +Z (la de la X) y casi
  ninguno en la −Z, y el suelo del mapa queda a 0,02–0,14 m de la base de la caja.
- Una fachada lisa deja una segunda solución corrida ~1–1,5 m a lo largo de ella, con peor puntaje (Teología 48 % contra
  60 %): esa es la incertidumbre esperable.
- `caja_dron.js` elige como frente la cara que mira a los puntos del mapa; en X1 eligió una cara corta (el mapa se carga hacia
  un extremo) y hubo que darle `--frente`. Sin `--claro`, la vegetación a la altura del techo agranda y gira la huella.

**Trampas:**
- `register.js` `icp()` pone la escala en 1 cuando `fitScale` es falso: para probar una escala fija, escalar los puntos
  antes de llamarlo.
- La CLI de RealityScan exporta PLY y COLMAP con **los últimos ajustes usados en su interfaz** (PLY ASCII con color, COLMAP
  texto). `-importTrajectory` existe, pero necesita un XML de ajustes que solo genera su diálogo. El reporte sale en el idioma
  de la interfaz (español).
- Ejes: COLMAP = (x, −z, y) del PLY de RealityScan.

**Pendientes:**
- [ ] Ciencias Básicas: 6 fotos sin alinear y una sola fachada bien cubierta; EIC: solo la parte visible.
- [ ] Los scripts viejos de `Tools/RegistroDron/` (`boxes.js`, `constrained.js`, `walls_in_map.js`, `fieldfit.js`,
  `ab_direct.js`) tienen constantes y rutas de los mapas retirados de Teología; los del paseo son `registrar_mapa.js` y
  `caja_dron.js`.

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

### Código que quedó (`Assets/AncoRA/Scripts/`)

| Archivo | Rol |
|---|---|
| `EdificioFacadeFrame.cs` | Marco plano o caja 3D (12 aristas + 6 caras, sólida o translúcida, X en la cara +Z, pivote en el centro); malla generada y liberada por código. Lo usa el paseo. |
| `GuiSafeArea.cs` | Rectángulo usable de la pantalla (notch + esquinas redondeadas); `PaseoUi` lo lleva al panel de UI Toolkit. |
| `PipelineMaterials.cs` | Materiales según el render pipeline. |

El resto se retiró: el paseo lo reemplaza (ver *Paseo virtual*).

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
  localización movía el contenido de golpe. El paseo usa su propio filtro (ver *Filtro de pose*). El `PoseSmoother` del
  SDK no sirve encadenado detrás de otro procesador: arranca en el origen del mundo y barre desde ahí. El ancla ARCore no
  se implementó: promediar ubicaciones nuevas ya absorbe la deriva de ARCore.
- Con `ProcessPoses` encendido, `XRSpace` llama a sus procesadores con `NewData` en cada ubicación y con `Update` en cada
  frame (sobre el último dato de la cadena); si el dato sale con `Ignore`, el espacio no se mueve.
- Mantener el contenido visible tras la primera ubicación mientras `ARSession` siga en tracking, aunque la calidad de
  Immersal caiga a 0, evita que desaparezca al mirar zonas sin mapa.
- Los `Debug.Log` de un build **de release** no aparecían en `adb logcat`; para probar hay que usar
  `BuildOptions.Development`.
- Un mapa solo localiza en las caras que se escanearon.
- `DeviceLocalization` llama a `Core.LocalizeImage` con `n = 0`, es decir, con **todos** los mapas cargados. El plugin acepta
  una lista (`LocalizeImage(n, mapIds, …)`), pero si un id no tiene *handle* (`MapHandleMapping.IdsToHandles`) falla el
  intento entero. El método de ubicación es un `ILocalizationMethod` que el `Localizer` lista y cada `XR Map` referencia, y
  el SDK lo configura como **último** paso de su inicio (`ImmersalSDK.ConfigureComponents`: mapas → plataforma →
  localizador). El paseo usa el suyo (`PaseoLocalization`, ver *1.2.3*).

### Reglas

- Una sola `ARSession`/`XROrigin`/cámara, un `PaseoLocalization` (en lugar de `DeviceLocalization`), sin `ServerLocalization`, sin token
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
  nube da un fondo de 9,6–9,7 m. El GPS de los dos vuelos y el mapa Immersal `152195` coinciden en ~22,3 m, así que esa
  escala era ~10 % chica (ver *Fotogrametría con dron*). En metros reales esta nube es `TeologiaBajo.ply`.
- La nube está **inclinada ~1° a lo largo** respecto de la vertical del GPS. Manda la gravedad de Immersal.
- **Una fachada lisa y repetitiva deja mal determinada la posición a lo largo de ella**, porque las nubes Immersal tienen
  ~1000–1500 puntos. Lo que la fija son los puntos de las caras cortas.
- Registrar con **escala 1**: escalar libremente encoge la nube hacia zonas densas.
- Puntos de los mapas "dentro" del edificio, a la altura de las ventanas, son vidrio o interiores, no un error.
- Con ~1000 puntos, estimar el **tamaño** del edificio desde la nube Immersal no sirve (cambia de 3 a 27 m según el
  umbral); de ella salen bien el suelo, el giro y el centro.

### Interfaz: notch y esquinas redondeadas

`GuiSafeArea.Rect` es el rectángulo usable en píxeles, con origen arriba a la izquierda (`PaseoUi` lo convierte con
`RuntimePanelUtils.ScreenToPanel` y lo aplica como márgenes del área segura). Combina `Screen.safeArea` (la cámara
perforada; el proyecto tiene `androidRenderOutsideSafeArea: 1`) con el radio de las esquinas redondeadas, que
`safeArea` no incluye. El radio se lee de Android con `WindowInsets.getRoundedCorner` (API 31+). Si falla, se estima
~0,22 pulgadas según los dpi. El margen es 0,3·r, porque un punto a (m, m) de una esquina de radio r queda en pantalla
si m ≥ r·(1 − 1/√2).

**Toda interfaz nueva debe dibujarse dentro de `GuiSafeArea.Rect`.** El log `[AncoRA UI] Esquinas redondeadas: …` muestra
los valores reales. **Falta confirmar en pantalla** que el aviso quede bajo la cámara y que los botones de abajo no se
corten.

### Pendientes heredados

- [ ] Resolución de cámara (revisado el 2026-10-13 en el código del SDK): la escena pide FullHD en Android
  (`m_AndroidResolution: 2`, la configuración de ARCore más cercana a 1920×1080), pero `ImmersalSDK.m_Downsample` está
  encendido y fija `LocalizationMaxPixels = 960×720`: la imagen se ubica a ~1100×620. Probar en terreno con downsample
  apagado (más detalle en fachadas lejanas, ubicaciones más lentas).
- [ ] Evaluar quitar el paquete ARCore Extensions: solo lo usaba Geospatial, y es el que exige el módulo iOS Build
  Support y el que ensucia las plantillas de gradle en cada build.
