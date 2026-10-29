# AncoRA

App de realidad aumentada para UCN / PACE (solo Android). El usuario apunta el teléfono a un edificio del campus desde la
vereda (15–50 m) y aparece encima una caja del tamaño real. Se ubica con **mapas Immersal** (SDK 2.4.0, en el teléfono), sin
cartel ni calibración. A futuro la caja se reemplaza por el edificio como era antes; **eso está postergado** hasta que la
ubicación sea rápida, centrada y funcione con todos los edificios a la vez.

Idioma: textos en pantalla y logs en español; comentarios del código en inglés.

**Historia completa** (versiones 1.1–1.3.6, pruebas de terreno, pilotos, decisiones): `Docs/HISTORIA.md`. Leerla solo si
la tarea lo necesita. Tags: `archivo/cartel-pace` (app del cartel), `archivo/pilotos-immersal` (pilotos Immersal y Geospatial).

## Estado (2026-10-29, versión 1.5.0, código Android 26)

- **1.5.0: parque animado sobre los edificios.** `parque` en `edificio.json` (`tema` ciencias/pista/saludo/baile, `escala`
  1,5 = personajes de 2,7 m, `frente` +z/-z/+x/-x, la cara del frente es una suposición) → `Prepare` arma bajo cada `Caja …`
  un hijo `Parque` (`PaseoPark`, armado por `PaseoParkBuilder`) con personajes de bloques y adornos low-poly hechos solo con
  mallas integradas de Unity y materiales `_Materiales/Bloques/` (shader `AncoRA/Paseo/Bloques`, luz fija, cola
  Transparent+10 para no quedar teñidos por el relleno de la caja). Nada se crea en runtime. Lógica pura con pruebas:
  `PaseoParkLayout`, `PaseoParkPath`, `PaseoBlockMotion` (caminar, trotar, saltar, saludar, bailar), `PaseoParkSchedule`.
  Botón «Parque» bajo el «?» (`AncoRA.Paseo.v3.Parque`, encendido por defecto; log `PARQUE encendido/apagado`). 182
  renderers en todo el campus. Diseño: `Docs/superpowers/specs/2026-10-29-parque-animado-design.md`. **Sin ver en el teléfono**:
  falta comprobar que brazos y piernas giren hacia el lado correcto y que el tamaño se lea desde la vereda.

- **1.4.3: re-anclaje del filtro de pose.** Prueba del 29-oct (1.4.2, campus): fijó con `152192` confianza 24 corrida ~2 m y
  descartó como `lejos` todo lo que vino después (Teología 13 y 24, que coincidían entre sí; X1 **128**), porque re-anclar pedía
  descartadas **seguidas** a ≤ 0,6 m y una que no coincidía borraba la cuenta. Ahora `PaseoPoseFilter` recuerda las últimas 6
  descartadas (`RejectedMemory`, aunque no sean seguidas) y re-ancla (a) un grupo que coincide a ≤ 1 m (`RelockAgreementMeters`)
  de 2 (3 confirmada) **que pese más que el apoyo de la pose actual** (pesos de la ventana atenuados por la distancia de cámara,
  como el promedio), o (b) una sola con confianza ≥ 50 que pese 2× el apoyo (`StrongRelock*`). Dos coincidencias débiles entre
  edificios ya no mueven una pose firme. **Sin probar en terreno.**

- **1.4.2: resolución de la imagen 1440×810 por defecto, selector y prueba del teléfono.** `PaseoLocalization.MaxPixels`
  (`PaseoResolution`: 691200 = 960·720, **1166400 = 1440·810 por defecto**, 0 = completa) se aplica con
  `Core.SetInteger("LocalizationMaxPixels")` justo antes de cada `Core.LocalizeImage`, porque el SDK lo vuelve a 960·720 en cada
  `Initialize`. Hoja del equipo → «Resolución de la imagen»: tres botones y «Probar este teléfono» (`PaseoPhoneTest`): pausa la
  búsqueda (`Suspended`/`Busy`), cronometra 4 imágenes de las grabaciones del 28-oct (`Assets/AncoRA/Resources/PaseoPrueba`, gzip del
  buffer gris; 2 ubican en 152198/152192, 2 no) y elige el tamaño más grande cuyo intento medio cabe en 5 s. Mide 1440·810 solo si
  2,0× lo de 960·720 cabe, y la completa solo si 2,2× lo de 1440·810 cabe (razones del Xiaomi 14 con esas 4 imágenes: 2,08 / 4,14 /
  9,10 s). La elección queda en `AncoRA.Paseo.v3.Resolucion` (+ `Origen`, `Fecha`); «Restaurar escena» no la borra. Logs:
  `RESOLUCION_PASEO`, `PRUEBA_TELEFONO`, `px=WxH` al final de `INTENTO_PASEO`, `max_pixeles` en `intentos.jsonl` (`PaseoReplay` lo
  usa; sin la clave, 960·720). APK `AncoRAPaseo.apk` (= `-1.4.2-campus.apk`) y respaldo `-1.4.2-por-edificio.apk`.
  **Sin probar en terreno.**

- **1.4.1: caja de X1 solo con el edificio.** La de antes (desde `X1.ply`) metía los toldos y la entrada con dos pilares del
  frente en el techo: 27,6 × 4,5 × 14 m y **girada 10,8°**. Medida de nuevo en `Campus.ply` con solo el techo claro (z 5,0–6,3,
  dentro de un polígono): **27,2 × 4,7 × 9,4 m**, centro corrido 1,75 m (`Escenas/Campus/caja-X1.json`). El giro queda a 1,2°
  del de la corrección a mano del 23-oct, que se había descartado (su tamaño sí estaba inflado). Los mapas no cambian.

- **1.4.0: modo campus.** Un solo XR Space con los mapas de los 4 edificios y las 4 cajas: la primera ubicación con cualquier mapa
  muestra todos los edificios. Sale de la nube del dron del 28-oct (`Escenas/Campus/`, ver *Datos*). Cada edificio conserva sus
  cajas y mapas tal cual; lo nuevo es `campus` en `edificio.json` (pose de la caja en el marco común). En modo campus una
  primera ubicación con confianza < 20 no se muestra sola: espera otra que coincida (`PaseoPoseFilter.SingleLockConfidence`,
  veredicto `pendiente`). El filtro mide cada ubicación en la caja del edificio de su mapa. Prefijo de `PlayerPrefs` `v3`.
  Sin `campus` en ningún edificio, la escena vuelve a un XR Space por edificio. **Sin probar en terreno.** Guía:
  `Docs/GuiaPrueba-1.4.1.md`. APK `AncoRAPaseo.apk` (= `-1.4.1-campus.apk`) y respaldo `AncoRAPaseo-1.4.1-por-edificio.apk`
  (mismos mapas, un espacio por edificio, se instala encima con `install -r`).
- EIC sigue siendo el punto débil: `152242` (1 596 puntos) y `152795` (provisional) no coinciden entre sí (2°, 1,5 m) y
  `152237` y `152238` contra la nube del campus siguen ambiguos (37 % y 44 %, soluciones muy distintas casi empatadas). Falta
  mapear EIC caminando.

- Escena única `Assets/Scenes/PaseoIngenieria.unity`, 4 edificios, 8 mapas, **un XR Space y una caja por edificio**, con todos
  sus mapas dentro (`PaseoBuildingFrame`: el marco del edificio es el del primer mapa con la caja colocada; cada otro mapa
  lleva T = B_R · B_M⁻¹ como transform local; ubicar con cualquiera mueve el mismo espacio y la misma caja). Todas las cajas vienen del dron (la corrección a mano de X1 del 23-oct se descartó).
- 1.3.8: arregla el filtro de cámara al suelo (liberaba mal la imagen y cortaba la búsqueda), el ciclo de búsqueda ya no usa «todos»
  (0,1,0,2,0,3 por GPS; sin GPS, de uno en uno), tarjeta «Levanta el teléfono», y 11 mapas (152794 X1, 152796 Teología, 152795 EIC
  provisional; fuera 152793, 152797, 152798). Sin probar en terreno. Detalle y alternativas: `Docs/PLAN-2026-10-26.md` (actualización 27-oct).
- 1.3.9: modo **Grabar** (hoja del equipo, solo build de desarrollo): por intento guarda la imagen completa que recibe
  `Core.LocalizeImage` (PNG sin pérdida, 1 canal, orientación del sensor) y una línea en `intentos.jsonl` con cámara, GPS, qué se
  buscó, resultado y el punto/luz elegidos. Es el banco de pruebas del paso 7 del plan (la reproducción offline queda para después).
  Guía de terreno: `Docs/GuiaGrabacion-2026-10-28.md`. Sin probar en teléfono.
- 28-oct (terreno, 15 sesiones, todo a ~15 m por las barandas y con luz nublada): `AndroidMinSdkVersion` bajó de 34 a 26 (el APK no se
  instalaba en Android 12; **falta recompilar y probar en un teléfono con Android 12**). Reproducción offline de las grabaciones:
  `AncorRA.Editor.PaseoReplay.RunFromCommandLine` (`-replayRoot <carpeta con las sesiones> -replayOut <x.jsonl>`, 22 min para 350
  imágenes) y `node Tools/AnalizarReproduccion.js <x.jsonl>`. Reproduce al teléfono en 348/350. Hallazgo: sin la reducción del plugin
  (`LocalizationMaxPixels` 0, imagen completa) ubica 98 % (159/162) contra 81 % (132/162) con la reducción actual (TEO-A, C y F pasan de
  17–25 % a ~100 %), pero 4–5× más lento y con algunas ubicaciones sueltas de 1–6 m: falta confirmar que estén bien puestas antes de cambiar la app.
  4 de 285 ubicaciones con los 11 mapas caen en otro edificio (EIC-B → 152192, confianza 12–15).
- Tiempo de la imagen completa **en el teléfono** (28-oct, Xiaomi 14, fuera de la app: el `libPosePlugin.so` arm64 del `.aar` llamado
  desde un ejecutable del NDK por `adb shell`, 64 imágenes de las grabaciones del S25 y del 15T con los mapas que buscó cada una; la
  reducida reproduce a la app en 64/64): reducida mediana 2,3 s (p90 3,3); completa mediana 7,7 s (p90 14,7, máx. 20), 4,6× por imagen.
  La completa tarda más cuando **no** ubica (mediana 11,2 s contra 5,4 s). Ubica 25/64 contra 15/64 (gana TEO-A, C, E y EIC-A) y
  sube la confianza mediana de 22 a 47. Con las mismas imágenes, el S25 en su app tardó 0,77× lo del Xiaomi 14 y el 15T 1,7×: la
  completa daría ~6 s en el S25 y ~16 s en el 15T. Los registros viejos del Xiaomi 14 dentro de la app no son más lentos que la prueba
  por consola. Lo único que se mide aquí es el tiempo: falta ver si las ubicaciones de la completa caen en el lugar correcto.
  Resoluciones intermedias (misma prueba, las 4 en una corrida con el orden rotado por imagen; batería 30→33 °C):

  | `LocalizationMaxPixels` | Ubica | Mediana | p90 | × la actual | Gana sobre la actual |
  |---|---|---|---|---|---|
  | 960·720 (1109×624, la actual) | 15/64 | 2,6 s | 3,6 s | 1 | — |
  | 1280·720 | 17/64 | 3,4 s | 5,0 s | 1,38 | TEO-E |
  | 1440·810 | 20/64 | 4,2 s | 7,5 s | 2,03 | TEO-A, TEO-E |
  | 0 (1920×1080) | 25/64 | 8,5 s | 14,9 s | 4,55 | TEO-A, C, E, EIC-A |

  No pierde ninguna imagen que ubique la actual. Misma imagen y mismo mapa: las poses quedan a 0,05–0,16 m (mediana) de la completa,
  máx. 0,44 m. Una ubicación de la completa (EIC-A) quedó a 4,4 m de las demás de su punto. Con confianza ≥ 20: 9, 10, 10 y 15.
- Rama `limpieza/auditoria`: se borraron el reemplazo 3D, la oclusión, el botón «Vista», las pruebas A/B de la 1.3.6, la
  brújula, ARCore Extensions, iOS/ARKit y datos sin uso. **Próximos pasos: `Docs/PLAN-2026-10-26.md`.**
- Prueba del 26-oct (nublado, 1.3.6): apuntando a una cara **mapeada**, 67–80 % de los intentos ubican. Lo que falla es
  la cobertura (cada edificio tiene ~1 cara mapeada), la arquitectura (antes, un espacio por mapa; ahora uno por edificio, sin probar en terreno) y la velocidad en el Xiaomi 15T
  (25069PTEBG, Dimensity 8400-Ultra: 4–5× más lento que el S25; la causa —calor, cámara, chip— no está medida). El efecto del sol es probable pero no está medido.

## Entorno

| | |
|---|---|
| Unity | 6000.6.0f1, URP (`ARBackgroundRendererFeature` en `URP-Performant-Renderer.asset`) |
| AR | AR Foundation 6.5, ARCore 6.5, Immersal Core 2.4.0 (paquete git) |
| Build | Android, IL2CPP + ARM64, APK suelto `Builds/Android/AncoRAPaseo.apk`, paquete `com.ancora.ucnar.paseo` |
| Repo | `github.com/Gael-OC/AncoRA` (**público**: nunca subir claves ni tokens) |

Teléfonos: Xiaomi 14 (`dd5da89f`), Galaxy S25 (`RFCY11HW8QT`), Xiaomi 25069PTEBG = Xiaomi 15T de Gael (MediaTek Dimensity 8400-Ultra). Solo uno conectado a la
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
| Teología | `152259`, `152693` (desde 1.3.7) | frente NO y costado NE (`152693` sin probar en terreno; su registro con el dron tiene una segunda solución a 1,4 m) |

Cajas desde el dron: `Escenas/<Edificio>/` tiene la nube georreferenciada (`.ply` + `-georef.json`), el registro de cada
mapa (`<id>-registro.json`, rígido, escala 1) y `caja-dron.json`. Scripts: `Tools/AlinearDron.ps1` (RealityScan → nube con
escala del GPS), `Tools/RegistroDron/registrar_mapa.js`, `caja_dron.js`, `mapa_contra_mapa.js`. Cada vuelo trae su propio
sesgo de GPS (hasta ~4 m), así que dos edificios de vuelos distintos no comparten marco exacto.

**Marco del campus (28-oct).** `Imgs/2026-10-28/` (234 fotos originales copiadas por adb, 2 vuelos: 15:33 oblicuo a 13–22 m y
17:44 grilla + órbitas a 19–44 m) → un solo proyecto de RealityScan: 234/234 alineadas, 0,56 px, escala del GPS 0,947, residuo
0,47 m (`Escenas/Campus/Campus.ply` + `-georef.json`). La escala coincide con las nubes viejas a 1 % y con los mapas a 0,5–3 %.
- Registrar un mapa a ciegas contra la nube del campus **no sirve**: se desliza a otra fachada (152192 dio 33 % contra 74 %).
  `recortar_nube.js` corta un edificio; `registrar_campus.js` parte del registro viejo de cada mapa compuesto con nube contra
  nube y solo afina (todos los mapas de la app quedan a 54–89 %, igual o mejor que antes).
- `Escenas/Campus/campus.json` = transformación nube del edificio → campus, elegida con el consenso de los mapas (ver su
  `fuente`; los registros de cada mapa contra el campus están en `Escenas/Campus/<id>-registro.json`);
  `campus_edificios.js --escribir` pone `campus` en cada `edificio.json` y `verificar_campus.js Escenas/Campus/Campus.ply`
  comprueba de punta a punta que cada mapa, con la transformación que le da la escena, cae sobre la nube (hoy 54–89 % a
  < 25 cm; 152242 36 %). Desacuerdos entre mapas de un mismo
  edificio: 0,04–0,3 m en CB y X1, ~1° en Teología, 2° / 1,5 m en EIC (EIC usa nube contra nube). Las cajas quedan a 3–4 m de
  su `gps` (el sesgo de los vuelos viejos).

## Cómo funciona la ubicación

- `PaseoLocalization` reemplaza a `DeviceLocalization`: cada intento llama `Core.LocalizeImage(n, ids, …)` solo con los
  mapas que elige `PaseoSearchPlan` (edificio a la vista, luego el más cercano por GPS, ciclo 0,1,0,2,0,todos; sin GPS,
  todos). Un id sin *handle* hace fallar todo el intento. Una línea `INTENTO_PASEO` por intento.
- `PaseoPoseProcessor` + `PaseoPoseFilter` en cada XR Space (uno solo en modo campus; si no, uno por edificio): mide en la caja
  del edificio del mapa que ubicó, deja solo el giro vertical, ignora >5° de
  inclinación, muestra con la primera ubicación («Confirmando») y confirma con una segunda a ≤0,6 m; umbral que crece con lo
  caminado; promedio ponderado por confianza. Una línea `UBICACION_PASEO` por ubicación.
- Si ARCore pierde el tracking se ocultan las cajas y se reinician los filtros.
- `PaseoPace`: un intento cada 0,5 s buscando, cada 2 s mirando un edificio ya ubicado.

## Modo Grabar (banco de pruebas)

- `PaseoRecorder` (apagado al abrir la app; solo si `Debug.isDebugBuild`). `PaseoLocalization.Localize` copia el buffer del SDK **antes**
  de `Core.LocalizeImage` (`PaseoFrame.Copy`, dentro del `using` de la imagen) y lo entrega tras `AttemptFinished`; cualquier fallo
  de la grabación solo pierde esa grabación (hay pruebas: la imagen se libera una vez en todos los caminos).
- Carpeta: `registro/grabaciones/<sesión>/` (`NNNN-<edificio>-<punto>-<m>m.png` + `intentos.jsonl`). «Compartir registro» no las lleva;
  «Copiar grabaciones a Descargas» las deja en `Descargas/AncoRA/grabaciones/`; `Tools/BajarRegistro.ps1 -Grabaciones` las baja por adb.
- Topes: 1 GB por sesión, se detiene si quedan <400 MB libres, como máximo 3 escrituras pendientes (si el disco va lento se omiten
  imágenes, no se frena la búsqueda). Selección de punto: `PaseoRecordSelection` (CB/EIC/TEO/X1 × A–F × 15/30/50 m × luz).
- `Backend` de `PaseoLocalization` permite probar `Localize` entero sin mapas ni plugin.

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
- Cambio de significado de algo en `PlayerPrefs`: subir el prefijo (hoy `AncoRA.Paseo.v3.`: una pose por edificio, en el marco
  de su XR Space, que en modo campus es el del campus) o migrar.
- `GpsFix` no debe exigir edad ≥ 0 (el reloj del GPS va adelantado).
- Artefactos que aparecen tras abrir o compilar y no se commitean: `preloadedAssets` en `ProjectSettings.asset`,
  `URP-Performant.asset`, `GvhProjectSettings.xml`, `AndroidResolverDependencies.xml`, los `OpenXR*` de `Assets/XR` (OpenXR
  llega como dependencia de Immersal; el proyecto no lo usa).

## Reglas

- Un solo `ARSession`/`XROrigin`/cámara; sin `ServerLocalization` ni token en código, escena, logs o Git.
- No declarar «alineado» sin medirlo en el sitio. Separar: mapa cargado / SDK obtuvo pose / caja físicamente alineada.
- Las capturas para mapas se hacen **caminando**, no girando en un lugar (las de 1–5 m de recorrido no sirven).
- No agregar funciones nuevas (modelos, efectos, pruebas A/B) antes de medir lo básico con datos repetibles.
