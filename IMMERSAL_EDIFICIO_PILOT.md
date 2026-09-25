# Piloto Immersal — fachada del edificio de ingeniería

Rama `experiments/immersal-fuente-151649`. **Independiente del piloto Fuente**: la escena
`Assets/Scenes/ImmersalFuentePilot.unity`, su setup (`ImmersalFuentePilotSetup`) y su validador (que exige
el mapa 151649 y su cubo) no se modifican y siguen siendo referencia y respaldo. Si un texto antiguo
contradice la escena o el código actual, mandan estos últimos.

Antes de salir a terreno: `EDIFICIO_GUIA_CAPTURA.md`.

## Estado

| Pieza | Estado |
|---|---|
| Guía de captura y lista de faltantes | Hecha (`EDIFICIO_GUIA_CAPTURA.md`) |
| Mapas A y B reales | **Recibidos (2026-09-24):** A = `151686-G6` («lejos», 75 img, referencia), B = `151687-G6down` («cerca», 81 img), mismo edificio. Carga nativa en el Editor OK |
| Escena `Assets/Scenes/ImmersalEdificioPilot.unity` | Creada con `Prepare` y validada. B con **alineación ESTIMADA por nubes PLY (no verificada; giro y desnivel fiables, X/Z ambiguo unos metros)**; marco sin colocar |
| Scripts runtime y de Editor del edificio | Compilan; prueba de humo y `ValidateProject` OK (ver *Verificación por CLI*) |
| Medidas del marco | **ESTIMADAS de las nubes PLY (47 × 17 m; fachada = cara larga, confirmado por el equipo), no medidas**; `edificio-medidas.json` con `estimated: true` |
| Builds del edificio | **No generados** (falta alinear B y medir); `BuildAndroid` listo |
| Alineación A↔B, marco físicamente alineado, tiempos, iOS/Android | **Sin medir.** Requiere terreno |

## Cómo está diseñado (y por qué)

Base comprobada: el `SimpleSample` de Immersal Core 2.4.0, igual que la Fuente, con la estructura del
`MultimapSample` (XR Maps hermanos bajo un mismo XR Space).

```
AR Session, XR Origin > Camera Offset > Main Camera        (una sola sesión y cámara)
ImmersalSDK (Localizer > DeviceLocalization; sin ServerLocalization; developerToken vacío)
XR Space  (ProcessPoses = false: la pose del mapa se aplica directa, sin suavizador)
 ├─ XR Map A <id>-<nombre>   (+ ImmersalMapAlignment: referencia, identidad)   [PLY verde]
 ├─ XR Map B <id>-<nombre>   (+ ImmersalMapAlignment: pos/rot manual A↔B)     [PLY magenta]
 └─ Marco fachada (ajustar en Editor)   ← UN solo marco, hijo del XR Space, no de un mapa
Diagnóstico Edificio (solo equipo)
```

- Mapas **embebidos** (`Map data source = Embed`), sin descargas ni token. Las PLY se importan solo para
  ver la alineación en Scene View y **arrancan ocultas** en ejecución.
- El marco (`EdificioFacadeFrame`) tiene ancho/alto en metros reales y su posición/rotación se ajustan con
  el gizmo. Material URP/Unlit (borde opaco + relleno translúcido, doble cara). No depende de detectar
  superficies. +Z local es la normal hacia el público (flecha azul del gizmo).

### Qué hace realmente `XRMap` con la alineación (leído del código del SDK 2.4.0)

- `MapManager.RegisterMap` toma `localPosition/localRotation/localScale` del `XR Map` **una sola vez, al
  iniciar el SDK**, y esa pose es la relación mapa→espacio. Ahí vive la alineación manual.
- `XRMap.ApplyAlignment()` **reescribe ese transform** con la metadata del portal (identidad para mapas
  sin alinear). Lo llama el inspector de `XRMap` al bajar/leer metadata. `XRMap.Configure(TextAsset)` solo
  relee la metadata a `mapAlignment`, sin tocar el transform, pero el siguiente `ApplyAlignment` lo pisa.
- Por eso `ImmersalMapAlignment` guarda pos/rot **serializadas**, las reaplica en `Awake` (orden -2000,
  antes de que el SDK registre los mapas), `OnValidate` las restaura al cargar la escena y `Validate` falla si el transform no coincide con lo
  guardado. La metadata (lat/lon, escala visual de la PLY) **no** se toma como alineación física.
- Si en un mismo ciclo localizan A y B, el SDK aplica ambos resultados en orden y gana el último; tras una
  buena alineación ambos dan la misma pose. La diferencia entre ambos es justamente el error de alineación.

### Comportamiento en ejecución (`ImmersalEdificioPilotDiagnostics`)

- El marco **no se muestra** hasta: resultado de localización aceptado (mapa cargado en el plugin nativo) +
  `ARSession` en tracking + calidad SDK > 0 **+ XR Space ya recibió la pose de ese resultado** (así no
  aparece un instante en el origen). Sin filtros ni retardos adicionales.
- Si se pierde tracking o calidad, se oculta hasta una nueva localización.
- Registra en el log `[AncoRA Edificio]`: mapa que produjo cada pose, confianza y rmse, tiempo hasta la
  primera aceptación y hasta el primer marco en cámara, **cada cambio de pose (cm y °)** y cada **cambio
  de mapa** (`A→B`). Un salto > 0,5 m o > 5° se registra como `SALTO BRUSCO`. El salto en un cambio de mapa
  mide alineación A↔B más deriva; no se suaviza.
- HUD **solo para el equipo**: oculto por defecto; se abre/cierra con **5 toques rápidos en la esquina
  superior izquierda**. El público solo ve la cámara y el marco. Los permisos del sistema en el primer
  arranque no son una calibración pero sí afectan el tiempo de ese arranque.

## Flujo cuando lleguen los datos

Requisitos: Unity 6000.6.0f1, **Immersal Core 2.4.0** (ya en `manifest.json`), y para `ExportIos` el
módulo **iOS Build Support** y un Mac con Xcode para firmar y probar.

Ejecutar Unity **secuencialmente** (nunca dos instancias sobre el mismo proyecto). Windows:

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$m = "AncorRA.Editor.ImmersalEdificioPilotSetup"
# 1) ¿están todos los datos?                       (…CheckInputs)
# 2) crear la escena desde SimpleSample            (…Prepare)
# 3) validar escena/mapas/marco/loaders            (…Validate)
# 4) cargar ambos .bytes en el plugin del Editor   (…CheckNativeMapsInEditor)
# 5) APK / export iOS                              (…BuildAndroid / …ExportIos)
Start-Process $unity -Wait -ArgumentList '-batchmode','-nographics','-quit','-projectPath','C:\dev\AncoRA-immersal','-executeMethod',"$m.Prepare",'-logFile','C:\dev\ancora-edificio-prepare.log'
```

macOS (mismo patrón, ajustando el método):

```sh
UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -quit -projectPath "$PWD" \
  -executeMethod AncorRA.Editor.ImmersalEdificioPilotSetup.Validate -logFile /tmp/ancora-edificio-validar.log
```

Revisar **código de salida y log** (`[AncoRA Edificio]`). Cualquier excepción devuelve código 1.

| Método (`AncorRA.Editor.ImmersalEdificioPilotSetup.`) | Qué hace |
|---|---|
| `CheckInputs` | Lista exactamente qué datos faltan (carpetas, `.bytes`, metadata, PLY, medidas). |
| `Prepare` | Crea `ImmersalEdificioPilot.unity` **solo si están los datos** y la escena no existe (no pisa la alineación guardada). Rechaza los IDs de la Fuente y de los mapas de ejemplo del SDK. |
| `ValidateProject` | Solo ajustes: Immersal 2.4.0, IL2CPP, ARM64, GLES3/Metal, un único loader ARCore/ARKit (sin OpenXR). No necesita mapas. |
| `Validate` | Lo anterior + datos + escena: una `ARSession`/`XROrigin`/cámara, un `DeviceLocalization`, sin `ServerLocalization`, sin token, dos `XR Map` hermanos bajo el mismo `XR Space`, `Embed`, alineación intacta, un único marco hijo del `XR Space`, materiales URP. Avisa (no falla) si B o el marco siguen «sin ajustar». |
| `AddFieldAdjust` | Agrega (idempotente) el panel de ajuste de campo a una escena ya preparada. |
| `ApplyFieldAdjustment` | Aplica `ajuste-campo.json` (lo que copia el panel de la app) a B y al marco y los marca ajustados por el equipo. |
| `ApplyMeasurements` | Copia ancho/alto de `edificio-medidas.json` al marco de la escena existente sin tocar su posición ni rotación (usar cuando lleguen las medidas reales). |
| `ApplyEstimatedAlignment` | Escribe en B la alineación de `alineacion-estimada.json` (generada por `Tools/EstimarAlineacionPly.js`, vista superior con `Tools/VistaSuperiorAlineacion.js`). No la marca como ajustada por el equipo. |
| `CheckNativeMapsInEditor` | `Validate` + `Core.LoadMap` de ambos `.bytes` en el plugin nativo del Editor. |
| `BuildAndroid` | `Validate` y luego `Builds/Android/ImmersalEdificioPilot.apk` (IL2CPP, ARM64, APK suelto). |
| `ExportIos` | `Validate` y luego `Builds/iOS_ImmersalEdificioPilot/`. Abrir en Xcode, firmar, probar en un iPhone real. |
| `ImmersalEdificioSmokeTest.Run` | Prueba de humo **solo del automatismo** con los mapas de ejemplo del SDK en una carpeta/escena temporales que borra al terminar. No valida datos del edificio. |

Los builds usan un `applicationIdentifier` con sufijo `.edificio` y nombre «… Edificio», y lo restauran al
terminar, para no reemplazar la app de la Fuente en el teléfono. En iOS hay que registrar ese identificador
en la firma. No se toca la lista de escenas de Build Settings (se pasa la escena explícita).

Instalación Android: `adb devices` y confirmar el serial **antes de cada llamada**;
`adb -s <serial> install -r Builds/Android/ImmersalEdificioPilot.apk`. **No abrir la app por adb.** Pedir
permiso antes de cualquier desinstalación (borra datos guardados). iPhone: instalar desde Xcode.

## Alineación manual A↔B (una vez, por el equipo)

1. Abrir `ImmersalEdificioPilot.unity`. En Scene View se ven la PLY de A (verde) y la de B (magenta).
2. **No mover** `XR Space`, `XR Map A` ni las nubes. Seleccionar `XR Map B` y editar en su componente
   **`ImmersalMapAlignment`**: `Local Position` (m) y `Local Euler Angles` (°, el eje Y es el vertical). El
   transform se actualiza solo. **No** editar el Transform de `XR Map B` a mano y **no** pulsar en el
   inspector de `XRMap` los botones que reaplican la metadata: pisarían el ajuste (se restaura al recargar la escena y `Validate`
   lo detecta; corregir con el menú contextual del componente → *Aplicar alineación manual*).
3. Referencias: los ≥ 3 detalles físicos comunes de la franja compartida, identificables en ambas nubes.
   Hacer coincidir primero el giro (rotación sobre Y), luego la posición, y comprobar la **escala** con
   distancias reales entre detalles medidas en terreno (la PLY puede verse a escala correcta sin estar
   físicamente alineada).
4. Anotar en `Field Notes` los detalles usados, las distancias y la fecha; marcar `Adjusted By Team`.
5. Colocar el marco: mover `Marco fachada` (hijo de XR Space) sobre la fachada con las medidas reales,
   comprobar el ancho/alto en `EdificioFacadeFrame` y marcar `Placed By Team`.
6. Guardar la escena, ejecutar `Validate` (el log imprime pos/rot de B y del marco para el registro) y
   recompilar. Registrar esos números en la hoja de campo junto a la versión/build GUID.

`Placed By Team` y `Adjusted By Team` solo indican que el equipo hizo el ajuste; **no** prueban alineación.


## Ajuste de campo desde la app (solo equipo)

Para afinar la alineación A↔B y el marco **en el lugar**, sin recompilar, el HUD del equipo trae un panel de
ajuste. El público no lo ve: primero hay que abrir el HUD con **5 toques rápidos en la esquina superior
izquierda**; recién entonces aparece abajo el botón **Ajuste: Mapa B / Ajuste: Marco**.

- **Mapa B:** posición X/Y/Z (m) y giro Y (°) de B respecto de A. Como el SDK lee la pose de B una sola vez al
  arrancar, el panel actualiza también la relación que guarda el SDK (`MapEntry.Relation`); el efecto se ve
  **en la siguiente localización que venga de B** (un par de segundos), no al instante. Con **Mostrar nubes
  PLY** se ve también la nube de B moverse.
- **Marco:** posición X/Y/Z (m) y giro Y (°) dentro del `XR Space` (= ejes del mapa A), más ancho y alto (m).
  Estos cambios se ven al instante.
- **Paso:** grueso / medio / fino (posición 1 · 0,25 · 0,05 m; giro 5 · 1 · 0,2°; tamaño 2 · 0,5 · 0,1 m).
- **Copiar valores:** copia un JSON al portapapeles, lo imprime en el log (`AJUSTE_CAMPO {...}`) y lo guarda en
  `Application.persistentDataPath`. Incluye también el mapa que localizó, los cambios de mapa y el último salto.
- **Restaurar escena:** vuelve a los valores de la escena. **No hay persistencia entre arranques** a propósito:
  un desplazamiento recordado en silencio taparía un valor de escena equivocado.

Cómo se usa: 1) mirar la fachada desde un punto de A y ver dónde cae el marco; 2) ir a un punto donde localice B
y comprobar si el marco **salta** al cambiar de mapa (el HUD muestra el último salto en cm y °); 3) ajustar B
hasta que el salto sea mínimo y el marco coincida con la fachada real; 4) ajustar el marco (posición, giro, ancho,
alto); 5) **Copiar valores**.

Aplicarlo desde el Editor: guardar el JSON pegado como
`Assets/AncoRA/ImmersalEdificio/ajuste-campo.json` y ejecutar
`AncorRA.Editor.ImmersalEdificioPilotSetup.ApplyFieldAdjustment` (o darme el JSON y lo aplico yo). Escribe la
alineación de B y el marco en la escena y los marca *ajustados por el equipo* (`Adjusted By Team`,
`Placed By Team`), lo que dice **quién** lo hizo, no que esté verificado contra medidas físicas. Los valores
en pantalla sirven para comparar, no reemplazan medir detalles reales de la fachada.

## Protocolo de aceptación

Distinguir siempre tres resultados, en columnas separadas: **① mapa cargado** (log «cargado en plugin
local»), **② SDK obtuvo pose** (log «SDK devolvió localización», mapa + confianza), **③ marco físicamente
alineado** (solo por medición en el sitio). Grabar pantalla **desde antes de tocar el icono** (el reloj de
la escena no incluye el arranque de Unity). Probar en al menos **un Android y un iPhone**, **desde ambos
lados de la frontera A/B**, a distintas horas y en arranques en frío (cerrar la app del todo).

| Dispositivo / SO / build | Punto y distancia | Mapa que localizó (① ② ③) | Tiempo icono→marco (s) | Éxitos / arranques en frío | Error visual en 4 detalles (cm) D1 · D2 · D3 · D4 | Deriva 60 s quieto (cm) | Saltos al mover lateral / al cambiar de mapa (cm, °) | Hora, vídeo, observaciones |
|---|---|---|---|---|---|---|---|---|
| Android / … | P1 (lado A) | A | | / | | | | |
| Android / … | P2 (lado B) | B | | / | | | | |
| Android / … | frontera A/B | | | / | | | | |
| iPhone / … | P1 (lado A) | A | | / | | | | |
| iPhone / … | P2 (lado B) | B | | / | | | | |
| iPhone / … | frontera A/B | | | / | | | | |

- Error visual: en cada uno de **cuatro detalles distribuidos por la fachada**, distancia entre el borde del
  marco/detalle virtual y el detalle físico, medida sobre la fachada (con la regla de la foto, no en
  píxeles). Objetivo inicial 20–30 cm, **sujeto a prueba física**.
- Tiempo: objetivo 3–5 s desde el toque del icono. Se mide, no se garantiza.
- Si la unión manual no mantiene un marco estable o el tiempo no cumple: **presentar la evidencia y
  recomendar el siguiente experimento** (p. ej. recapturar B con más solape, mapa único más grande,
  tercer mapa en la franja, o evaluar otro método). No esconderlo con una calibración del usuario.
- Nada de esto autoriza a afirmar precisión milimétrica ni paridad iOS/Android a partir de compilar.

## Verificación por CLI (comprobado vs. pendiente)

**Comprobado (Unity 6000.6.0f1, Windows, 2026-09-24)**

- Lectura del código de Immersal Core 2.4.0 (`XRMap`, `MapManager`, `SceneUpdater`, `XRSpace`,
  `ImmersalSession`) para el comportamiento de alineación y el orden de eventos descrito arriba.
- Los scripts nuevos **compilan**. Para compilar el paquete local `com.google.ar.core.arfoundation.extensions`
  hace falta el módulo *iOS Build Support* de Unity (su Editor usa `UnityEditor.iOS.Xcode` sin guarda); con el
  módulo instalado el proyecto compila.
- `ImmersalEdificioSmokeTest.Run` (**exit 0**), con los mapas A/B de ejemplo del SDK en una escena temporal
  que se borra sola. Comprobó: `Prepare` y `Validate` se detienen sin datos; los IDs de ejemplo se rechazan
  en modo estricto; `Prepare` crea la escena con dos `XR Map` hermanos bajo el mismo `XR Space` y un marco
  hijo del `XR Space`; `Prepare` no sobrescribe una escena existente; la alineación manual de B sobrevive a
  `ApplyAlignment()` y a recargar la escena (`OnValidate` la reaplica; en ejecución lo hace `Awake`).
- `ValidateProject` (**exit 0**): Immersal 2.4.0, IL2CPP, ARM64, GLES3/Metal, un loader por plataforma.
- Sobre el proyecto real, `CheckInputs`, `Validate` y `BuildAndroid` terminan con **exit 1** y la lista exacta
  de datos que faltan; **no se generó ningún APK**.

**No comprobado (pendiente)**

- `Prepare` y `Validate` con los datos reales del edificio, `CheckNativeMapsInEditor` y un `BuildAndroid`
  completo: no hay mapas A/B ni medidas. La prueba de humo prueba el automatismo, no ningún dato del edificio.
- Cargar el mapa en el plugin nativo en un teléfono, y todo lo de terreno: localización con mapas reales,
  alineación A↔B, tiempo hasta el marco, error visual, deriva y saltos.
