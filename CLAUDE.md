# AncoRA

App de realidad aumentada para UCN / PACE (solo Android). El usuario apunta el teléfono a un edificio del campus desde la
vereda (15–50 m) y aparece encima una caja del tamaño real. Se ubica con **mapas Immersal** (SDK 2.4.0, en el teléfono), sin
cartel ni calibración. A futuro la caja se reemplaza por el edificio como era antes; **eso está postergado** hasta que la
ubicación sea rápida, centrada y funcione con todos los edificios a la vez.

Idioma: textos en pantalla y logs en español; comentarios del código en inglés.

**Historia completa** (versiones 1.1–1.3.6, pruebas de terreno, pilotos, decisiones): `Docs/HISTORIA.md`. Leerla solo si
la tarea lo necesita. Tags: `archivo/cartel-pace` (app del cartel), `archivo/pilotos-immersal` (pilotos Immersal y Geospatial).

## Estado (2026-10-26)

- Escena única `Assets/Scenes/PaseoIngenieria.unity`, 4 edificios, 7 mapas, **un XR Space por mapa**: cada edificio se
  ubica por separado. Todas las cajas vienen del dron (la corrección a mano de X1 del 23-oct se descartó).
- Rama `limpieza/auditoria`: se borraron el reemplazo 3D, la oclusión, el botón «Vista», las pruebas A/B de la 1.3.6, la
  brújula, ARCore Extensions, iOS/ARKit y datos sin uso. **Próximos pasos: `Docs/PLAN-2026-10-26.md`.**
- Prueba del 26-oct (nublado, 1.3.6): apuntando a una cara **mapeada**, 67–80 % de los intentos ubican. Lo que falla es
  la cobertura (cada edificio tiene ~1 cara mapeada), la arquitectura (un espacio por mapa) y la velocidad en teléfonos de
  gama media (Xiaomi 25069PTEBG: 4–5× más lento que el S25). El efecto del sol es probable pero no está medido.

## Entorno

| | |
|---|---|
| Unity | 6000.6.0f1, URP (`ARBackgroundRendererFeature` en `URP-Performant-Renderer.asset`) |
| AR | AR Foundation 6.5, ARCore 6.5, Immersal Core 2.4.0 (paquete git) |
| Build | Android, IL2CPP + ARM64, APK suelto `Builds/Android/AncoRAPaseo.apk`, paquete `com.ancora.ucnar.paseo` |
| Repo | `github.com/Gael-OC/AncoRA` (**público**: nunca subir claves ni tokens) |

Teléfonos: Xiaomi 14 (`dd5da89f`), Galaxy S25 (`RFCY11HW8QT`), Xiaomi 25069PTEBG de la compañera. Solo uno conectado a la
vez: **`adb devices` antes de cada llamada**.

Carpetas: en Windows trabajar en `C:\dev\AncoRA-immersal` (fuera de OneDrive; OneDrive ya duplicó objetos de
`XRGeneralSettings.asset`). En macOS, `/Users/gael/Documents/GitHub/AncoRA`. Las fotos del dron (`Imgs/`) y los proyectos de
RealityScan están solo en el PC de escritorio (`C:\Users\nicol\Documents\AncoRA`).

## Runbook

Editor cerrado; **una sola instancia de Unity a la vez**. Orden al cambiar mapas o datos: pruebas → `Prepare` →
`CheckNativeMapsInEditor` → `BuildAndroid`.

Windows (`Tools/UnityBatch.ps1` espera a Unity y resume; va en UTF-8 con BOM):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.Prepare
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.BuildAndroid
```

macOS: `/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath <repo>`
más `-runTests -testPlatform EditMode -testFilter AncorRA.Tests -testResults <xml> -logFile <log>` (sin `-quit`) para las
pruebas, o `-quit -executeMethod AncorRA.Editor.PaseoSetup.<Metodo> -logFile <log>` para un método.

Menús `AncoRA/Paseo/…` (todos `public static`): comprobar datos, preparar escena, validar, carga nativa, aplicar ajuste de
campo, compilar APK, exportar nubes de los mapas, prueba de humo.

Instalar: `adb -s <serial> install -r Builds/Android/AncoRAPaseo.apk`. **No abrir la app por adb** (el usuario la abre).
`INSTALL_FAILED_UPDATE_INCOMPATIBLE` = otro keystore de debug (cada computador firma con el suyo): desinstalar, **con
permiso**, porque borra los ajustes guardados en PlayerPrefs.

Registros de terreno: la app escribe un archivo por sesión y «Compartir registro» (hoja del equipo) arma un zip.
`Tools/BajarRegistro.ps1` los baja por adb; `node Tools/AnalizarRegistro.js <paseo-*.log> [--csv=…]` los resume (excluye de
los tiempos lo que cruza una pausa de la app). `node Tools/MapaCobertura/armar_mapa.js [salida.html] --diag` muestra qué
caras de cada edificio cubre cada mapa.

`BuildAndroid` compila siempre en modo desarrollo (logs y hoja del equipo activos). Todavía no hay build de release.

## Datos

`Assets/AncoRA/Paseo/<Edificio>/edificio.json` + `.bytes` manda: `Prepare` rearma la escena desde cero. Agregar un edificio =
carpeta nueva + `Prepare`. `gps` es el centro de la caja (de la nube del dron).

| Edificio | Mapas | Cara bien cubierta |
|---|---|---|
| Ciencias Básicas | `152192`, `152196`, `152257` | frente SO |
| EIC | `152242` | ninguna (1 596 puntos) |
| X1 | `152198`, `152200` | frente SE |
| Teología | `152259` | frente NO (el candidato `152693` cubre el costado NE) |

Cajas desde el dron: `Escenas/<Edificio>/` tiene la nube georreferenciada (`.ply` + `-georef.json`), el registro de cada
mapa (`<id>-registro.json`, rígido, escala 1) y `caja-dron.json`. Scripts: `Tools/AlinearDron.ps1` (RealityScan → nube con
escala del GPS), `Tools/RegistroDron/registrar_mapa.js`, `caja_dron.js`, `mapa_contra_mapa.js`. Cada vuelo trae su propio
sesgo de GPS (hasta ~4 m), así que dos edificios de vuelos distintos no comparten marco exacto.

## Cómo funciona la ubicación

- `PaseoLocalization` reemplaza a `DeviceLocalization`: cada intento llama `Core.LocalizeImage(n, ids, …)` solo con los
  mapas que elige `PaseoSearchPlan` (edificio a la vista, luego el más cercano por GPS, ciclo 0,1,0,2,0,todos; sin GPS,
  todos). Un id sin *handle* hace fallar todo el intento. Una línea `INTENTO_PASEO` por intento.
- `PaseoPoseProcessor` + `PaseoPoseFilter` en cada XR Space: mide en la caja, deja solo el giro vertical, ignora >5° de
  inclinación, muestra con la primera ubicación («Confirmando») y confirma con una segunda a ≤0,6 m; umbral que crece con lo
  caminado; promedio ponderado por confianza. Una línea `UBICACION_PASEO` por ubicación.
- Si ARCore pierde el tracking se ocultan las cajas y se reinician los filtros.
- `PaseoPace`: un intento cada 0,5 s buscando, cada 2 s mirando un edificio ya ubicado.

## Trampas y causas raíz (no reinvestigar)

- **SDK no listo en la primera apertura**: configura la cámara una sola vez; si el diálogo de permiso la tenía apagada, nunca
  ubica. `PaseoSdkStart` reintenta `sdk.Initialize()` cuando ARCore ya sigue y `PaseoLocalization` fue configurado. Nunca
  solapar dos inicios.
- **Cámara negra**: `XRGeneralSettings.asset` con objetos duplicados (OneDrive). Debe tener Standalone, Android y WebGL; Android
  con un solo `ARCoreLoader`.
- **Logcat del Xiaomi recorta las líneas de la app**: usar el registro de sesión del teléfono. `Prepare` deja `Log`/`Warning`
  sin traza.
- `MapManager.RegisterMap` lee el transform local del `XR Map` una vez al iniciar el SDK (relación mapa→espacio). El SDK
  solo registra los `XR Map` activos en su `Awake`. `OnLocalizationResult` llega antes de que se mueva el XR Space.
- UI Toolkit: las vistas se arman en código; el tema fija la fuente de cada texto (`PaseoUiKit.ApplyFonts` después de crear
  textos); `ScreenToPanel` espera origen arriba a la izquierda. Toda interfaz va dentro de `GuiSafeArea.Rect`.
- Liberar en `OnDestroy` materiales clonados y mallas generadas; desactivar antes de `Destroy`.
- Campo serializado nuevo en un componente de la escena: escribirlo vía `Prepare`, no confiar en el inicializador.
- Cambio de significado de algo en `PlayerPrefs`: subir el prefijo (`AncoRA.Paseo.v1.`) o migrar.
- `GpsFix` no debe exigir edad ≥ 0 (el reloj del GPS va adelantado).
- Artefactos que aparecen tras abrir o compilar y no se commitean: `preloadedAssets` en `ProjectSettings.asset`,
  `URP-Performant.asset`, `GvhProjectSettings.xml`, `AndroidResolverDependencies.xml`, los `OpenXR*` de `Assets/XR` (OpenXR
  llega como dependencia de Immersal; el proyecto no lo usa).

## Reglas

- Un solo `ARSession`/`XROrigin`/cámara; sin `ServerLocalization` ni token en código, escena, logs o Git.
- No declarar «alineado» sin medirlo en el sitio. Separar: mapa cargado / SDK obtuvo pose / caja físicamente alineada.
- Las capturas para mapas se hacen **caminando**, no girando en un lugar (las de 1–5 m de recorrido no sirven).
- No agregar funciones nuevas (modelos, efectos, pruebas A/B) antes de medir lo básico con datos repetibles.
