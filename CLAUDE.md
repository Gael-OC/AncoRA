# AncoRA

App de realidad aumentada para UCN / PACE. Ubica el teléfono frente a edificios reales del campus con **mapas
Immersal** (sin cartel ni calibración del usuario) y superpone contenido virtual de tamaño real: un marco sobre la
fachada o una caja 3D que hace de edificio.

El idioma de trabajo es español. Los textos en pantalla y los mensajes de log van en español; los
comentarios en el código van en inglés.

**Estado (2026-10-08):** el **paseo virtual** por los edificios de la escuela está implementado (escena
`PaseoIngenieria.unity`, 4 edificios, 5 mapas) y compila a APK. Las cajas de X1, Ciencias Básicas y Teología están
colocadas desde las nubes del dron; la de EIC no. **Nada se ha verificado todavía en terreno** (tiempo de localización,
error visual, deriva). Los pilotos anteriores están en el tag `archivo/pilotos-immersal`.

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

`AncoRA/Paseo/…`: comprobar datos, preparar escena, validar, carga nativa, aplicar ajuste de campo, compilar APK y prueba
de humo. Todos son `public static` para llamarlos con `-executeMethod`.

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

## Paseo virtual (desde 2026-10-08)

Paseo **en el campus** por los edificios de la escuela de ingeniería. Una escena carga los mapas Immersal de todos los
edificios con **un XR Space por mapa**: ubicarse con un mapa mueve solo su espacio, así que se pueden ver dos edificios a la
vez, cada uno en su lugar. Por edificio se muestra la caja del mapa que localizó último. Diseño:
`docs/superpowers/specs/2026-10-08-paseo-virtual-design.md`; plan: `docs/superpowers/plans/2026-10-08-paseo-virtual.md`.

| Edificio (carpeta) | Nombre visible | Mapas |
|---|---|---|
| Edificio (carpeta) | Nombre visible | Mapas | Caja |
|---|---|---|---|
| `CienciasBasicas` | Ciencias Básicas | `152192-csbasicasgael`, `152196-csbasicasgael2` | desde el dron |
| `EIC` | EIC *(provisional)* | `152199-eicgael` | **sin colocar**: el mapa es diminuto |
| `X1` | X1 | `152198-x1gael` | desde el dron |
| `Teologia` | Teología | `152195-Teologianicowo` (agregado el 2026-10-08) | desde el dron |

Los cinco `.bytes` cargan en el plugin del Editor (1636 a 22600 puntos). Llegaron sin `-metadata.json` ni `-sparse.ply`:
el SDK toma id y nombre del nombre del archivo y deja la alineación en identidad, que es lo que el paseo necesita.
**`152199-eicgael` no cubre el edificio**: sus 3389 puntos caben en 1,2 × 0,5 × 1,9 m (los demás mapas miden 9–55 m), así
que solo ubicaría mirando ese punto y no hay cómo colocar su caja desde el dron. `152194-Cienciasbasicas` (un tercer mapa de
Ciencias Básicas) no registró contra la nube del dron (14 % de puntos a < 25 cm, soluciones a 60 m entre sí) y quedó fuera
del paseo; el archivo está en Descargas.

**Datos:** `Assets/AncoRA/Paseo/<Edificio>/edificio.json` + `.bytes` (metadata y `.ply` opcionales). `edificio.json` manda:
`Prepare` rearma la escena desde cero cada vez. Agregar un edificio = carpeta nueva + `Prepare`.

**Código:** runtime en `Assets/AncoRA/Scripts/Paseo/` (`PaseoTour`, `PaseoMapContent`, `PaseoLabel`, `PaseoHud`,
`PaseoFieldAdjust` + lógica pura `PaseoConfig`, `PaseoAdjustment`, `PaseoVisibility`, `PaseoStatusText`, `PaseoBoxStore`,
`PaseoPlacement`, `PaseoMapLoad`); Editor en `Assets/AncoRA/Editor/Paseo/`; pruebas EditMode en `Assets/AncoRA/Editor/Tests/` (48).

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

**En terreno:** cada caja sin colocar aparece a 12 m delante de la cámara con «(sin colocar)». Panel del equipo: 5 toques
arriba a la izquierda → «Ajuste: Caja» (edita la caja del último mapa ubicado; se guarda en el teléfono con prefijo
`AncoRA.Paseo.v1.`). «Copiar valores» → pegar en `Assets/AncoRA/Paseo/ajuste-campo.json` →
`PaseoSetup.ApplyFieldAdjustment` (escribe `edificio.json` y rearma la escena). Ciencias Básicas se coloca una vez en cada
uno de sus dos mapas. Cada ajuste del teléfono recuerda de qué valores horneados partió: si la escena se hornea de nuevo con
valores distintos, el ajuste viejo se descarta solo (y no vuelve a `edificio.json`). El HUD marca «valores del teléfono».

**Pendientes del paseo:**
- [ ] Prueba en terreno: cada edificio localiza; con dos a la vista, ubicarse con el segundo **no mueve** el primero;
  Ciencias Básicas cambia de mapa sin duplicar la caja; nombre legible desde la distancia de observación.
- [ ] Revisar en terreno las cajas puestas desde el dron (X1, Ciencias Básicas, Teología); corregir con el panel si hace
  falta y hornear con `ApplyFieldAdjustment`.
- [ ] Rehacer el mapa de EIC cubriendo la fachada, o colocar su caja a mano en terreno.
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
| X1 | `152198` → `X1.ply` | 89 % (rms 0,12 m) | 27,6 × 4,5 × 14,0 m | techo claro a 3,2–4,9 m; frente dado (fachada de las carpas) |
| Ciencias Básicas | `152192`, `152196` → `CienciasBasicas.ply` | 74 % y 65 % | 19,9 × 7,9 × 10,7 m | techo claro a 5,6–7,0 m |
| Teología | `152195` → `TeologiaBajo.ply` | 60 % (rms 0,22 m) | 22,3 × 8,3 × 10,7 m | `--huella`: paredes medidas en la nube del 25-sep, llevadas a metros |

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
| `GuiSafeArea.cs` | Rectángulo IMGUI usable (notch + esquinas redondeadas). |
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
- [ ] Evaluar quitar el paquete ARCore Extensions: solo lo usaba Geospatial, y es el que exige el módulo iOS Build
  Support y el que ensucia las plantillas de gradle en cada build.
