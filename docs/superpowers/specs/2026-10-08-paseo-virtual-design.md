# Paseo virtual por la escuela de ingeniería — diseño

Fecha: 2026-10-08 · Estado: **para revisión**

## 1. Objetivo

Un paseo **en el campus**, con el teléfono, por los edificios de la escuela de ingeniería UCN. Frente a cada edificio
el teléfono se ubica con el mapa Immersal de ese edificio y muestra **el volumen del edificio (una caja 3D) y su
nombre**. Si desde un punto se ven dos edificios, se ven las dos cajas, cada una en su lugar.

**Lo pedido por el usuario:** paseo por los edificios de la escuela; mapas nuevos hechos desde cero con Immersal; que
la app cargue todos los mapas y muestre los edificios; contenido de la primera versión = volumen + nombre (opción A);
Ciencias Básicas usa sus dos mapas.

**Supuestos:** una sola app y una sola escena, sin menú para elegir edificio; la persona no calibra nada; el equipo
coloca las cajas una vez en terreno y los valores quedan horneados en la escena para repartir; solo Android.

**Criterio de éxito de esta versión:** en terreno, cada edificio con mapa muestra su caja y su nombre al ubicarse; con
dos edificios a la vista, ubicarse con el segundo **no mueve** la caja del primero; el equipo puede colocar las cajas en
el teléfono y llevar esos valores a la escena sin editar nada a mano.

## 2. Edificios y mapas

| Edificio (id) | Nombre visible | Mapas | Estado |
|---|---|---|---|
| `CienciasBasicas` | Ciencias Básicas | `152192-csbasicasgael`, `152196-csbasicasgael2` | `.bytes` recibidos |
| `X1` | X1 | `152198-x1gael` | `.bytes` recibido |
| `EIC` | EIC *(provisional)* | `152199-eicgael` | `.bytes` recibido |
| `Teologia` | Teología | — | **sin mapa todavía**; entra al agregar su carpeta |

Solo llegaron los `.bytes`. `-metadata.json` y `-sparse.ply` son **opcionales**: si están, el `.ply` sirve para una
caja inicial mejor (ver §7); el sistema funciona sin ellos.

Las capturas crudas (`Descargas\Mapas\*.zip`, ~1,4 GB de fotos) **no entran al proyecto**.

## 3. Decisión central: un XR Space por mapa

Verificado en el SDK 2.4.0 (código, no en teléfono):

- `ImmersalSDK.RegisterAndLoadMaps` registra cada `XR Map` activo con el `ISceneUpdateable` (XR Space) **padre más
  cercano** (`ImmersalSDK.cs:317`).
- Al localizar, `SceneUpdater` calcula la pose y llama a `entry.SceneParent.SceneUpdate`, que mueve **solo ese XR
  Space** dentro del mundo de ARCore (`XRSpace.cs:100`). El mundo de ARCore no se mueve.

Por eso **cada mapa tiene su propio XR Space**. Ubicarse con un mapa mueve solo su espacio; las cajas de los demás
quedan donde las dejó su última localización.

**Edificios con varios mapas (Ciencias Básicas).** Cada mapa tiene su espacio y su propia pose de caja; el tamaño y
el nombre son del edificio y se comparten. Se muestra **la caja del mapa de ese edificio que localizó último** y se
ocultan las demás del mismo edificio. Así se usan los dos mapas (más cobertura) sin un paso explícito de alineación:
colocar la caja una vez en cada mapa **es** la alineación entre ellos (equivale a `DeriveAlignment` de la demo de
Teología, sin recargar la escena ni elegir modo). El costo: la caja se coloca dos veces en terreno, y si las dos
colocaciones difieren, la caja salta esa diferencia al cambiar de mapa.

Se descartaron: un solo XR Space con mapas alineados a mano (frágil, hay que medir y rehacer con cada mapa nuevo) y
anclas ARCore propias (reimplementa lo que el SDK ya hace).

## 4. Escena

`Assets/Scenes/PaseoIngenieria.unity`, generada por `PaseoSetup.Prepare` a partir del `SimpleSample` oficial (como
los pilotos):

```
AR Session
XR Origin (cámara AR)
ImmersalSDK                    (DeviceLocalization; sin ServerLocalization, sin token)
Paseo                          (PaseoTour, PaseoFieldAdjust, PaseoHud)
  CienciasBasicas
    XR Space 152192            (XRSpace)
      XR Map 152192            (XRMap → 152192-csbasicasgael.bytes)
      Caja                     (EdificioFacadeFrame en modo caja, PaseoMapContent)
        Nombre                 (PaseoLabel)
    XR Space 152196
      XR Map 152196
      Caja / Nombre
  EIC
    XR Space 152199 …
  X1
    XR Space 152198 …
```

La caja y el nombre son hijos del XR Space (no del XR Map), y su pose local está en el marco del mapa: el `XR Map`
queda en identidad dentro de su espacio, así que marco del espacio = marco del mapa.

## 5. Componentes

Todo en `namespace AncorRA.AR`, `Assets/AncoRA/Scripts/Paseo/` (runtime) y `Assets/AncoRA/Editor/Paseo/` (Editor).

| Unidad | Responsabilidad | Depende de |
|---|---|---|
| `PaseoVisibility` *(C# puro)* | Dado el estado de cada mapa (edificio, localizó con pose aplicada, instante del último éxito) y si ARCore está en tracking, decide qué caja se ve. Sin Unity: se prueba en EditMode. | — |
| `PaseoTour` | Escucha `OnLocalizationResult`, detecta cuándo cada XR Space recibió la pose, alimenta `PaseoVisibility` y aplica el resultado a cada `PaseoMapContent`. Expone el estado por edificio para el HUD y el panel. | `PaseoVisibility`, SDK |
| `PaseoMapContent` | Por mapa: id de mapa, id de edificio, referencia a la caja y al nombre. Aplica pose y tamaño; muestra/oculta. | `EdificioFacadeFrame`, `PaseoLabel` |
| `PaseoLabel` | Texto 3D (TextMeshPro) sobre la caja, gira solo en Y para mirar a la cámara. | TMP |
| `PaseoBoxStore` | `PlayerPrefs` con prefijo `AncoRA.Paseo.v1.`: pose por mapa (`<mapId>.Pos/Yaw`), tamaño y relleno por edificio (`<edificio>.Size/Solid`). Reemplaza a `TeologiaBoxStore`. | — |
| `PaseoFieldAdjust` | Panel del equipo (IMGUI dentro de `GuiSafeArea.Rect`). Edita la caja visible del **último mapa localizado**: posición X/Y/Z, giro Y, ancho, alto, fondo, sólido. Guarda en `PaseoBoxStore`. «Copiar valores» → JSON (§8). «Restaurar escena» borra lo guardado. | `PaseoTour`, `PaseoBoxStore` |
| `PaseoHud` | Banner siempre visible con el estado por edificio y HUD detallado del equipo (5 toques arriba a la izquierda). | `PaseoTour`, `GuiSafeArea` |
| `PaseoStatusText` *(C# puro)* | Texto del banner a partir del estado (reemplaza a `EdificioStatusText` para varios edificios). Prueba en EditMode. | — |
| `PaseoConfig` *(C# puro)* | Lee y escribe `edificio.json`; valida. Lo usan el Editor y las pruebas. | Newtonsoft.Json (dependencia de Immersal) |
| `PaseoSetup` *(Editor)* | `CheckInputs`, `Prepare`, `Validate`, `ApplyFieldAdjustment`, `BuildAndroid`; menús `AncoRA/Paseo/…`, todos `public static`. | `PaseoConfig` |

Se reutilizan sin cambios `EdificioFacadeFrame`, `GuiSafeArea` y `PipelineMaterials`. Al terminar se borran
`ImmersalEdificioPilotDiagnostics`, `ImmersalEdificioFieldAdjust`, `TeologiaMapSelector`, `TeologiaBoxStore`,
`EdificioStatusText` e `ImmersalMapAlignment` (este último ya no hace falta: cada mapa queda en identidad dentro de su
espacio), si nada más los usa.

## 6. Comportamiento en ejecución

**Arranque.** El SDK registra los cuatro mapas, cada uno con su espacio. Todas las cajas empiezan ocultas. Banner:
«Cargando mapas…» → «Apunta a un edificio».

**Localización de un mapa M (edificio E).** `PaseoTour` anota el éxito de M solo si ARCore está en tracking y el mapa
está cargado. Espera a que el XR Space de M cambie de pose en ese frame o el siguiente (el evento llega **antes** de que
`XRSpace` se mueva; mostrar antes dejaría la caja un instante en el origen). Desde ese momento M está «ubicado».

**Reglas de visibilidad (`PaseoVisibility`):**

1. Si ARCore no está en tracking, se ocultan todas y todos los mapas pierden «ubicado» (sus espacios ya no valen).
2. Por cada edificio, entre sus mapas «ubicados», se elige el de **último éxito más reciente**; se muestra su caja y se
   ocultan las de los demás mapas de ese edificio.
3. Un mapa «ubicado» sigue así aunque Immersal no vuelva a localizar (la caja se mantiene con el tracking de ARCore);
   el HUD lo marca como «mantenido» después de 10 s sin un nuevo éxito.
4. No hay límite de edificios visibles a la vez.

**Banner** (ejemplo): «Ciencias Básicas ✓ · X1 ✓ · EIC: buscando…». Si nada se ubica en 15 s: «Acércate a un edificio
del paseo y muévete despacio».

## 7. Colocación de las cajas

Orden de prioridad para la pose de la caja de un mapa:

1. Lo guardado en el teléfono (`PaseoBoxStore`), si el equipo la ajustó.
2. Lo horneado en `edificio.json` / escena con `colocada: true`.
3. Una pose inicial calculada al primer «ubicado» de ese mapa: **12 m delante de la cámara** en horizontal, base 1,5 m
   bajo la cámara, de frente a ella; convertida al marco del mapa. El nombre muestra «(sin colocar)» para que el equipo
   sepa que falta ajustarla. No se guarda hasta que el equipo la toca.

Tamaño por defecto si `edificio.json` no trae uno: 20 × 8 × 12 m (es un valor de relleno, no una medida). El `.ply`,
cuando exista, puede dar suelo, giro y centro (`Tools/EstimarCajaPly.js`); su uso queda fuera de esta versión.

## 8. Datos y persistencia

**Carpetas:** `Assets/AncoRA/Paseo/<Edificio>/` con los `.bytes` (y opcionales `-metadata.json`, `-sparse.ply`) y un
`edificio.json`:

```json
{
  "nombre": "Ciencias Básicas",
  "tamano": [20, 8, 12],
  "solido": false,
  "mapas": [
    { "id": 152192, "archivo": "152192-csbasicasgael.bytes",
      "caja": { "posicion": [0, 0, 12], "giro": 0, "colocada": false } },
    { "id": 152196, "archivo": "152196-csbasicasgael2.bytes",
      "caja": { "posicion": [0, 0, 12], "giro": 0, "colocada": false } }
  ]
}
```

El orden de los edificios en la escena es alfabético por id de carpeta.

**«Copiar valores»** deja en portapapeles, log (`AJUSTE_PASEO`) y `persistentDataPath` un JSON con, por edificio,
tamaño, sólido y la caja de cada mapa. `PaseoSetup.ApplyFieldAdjustment` lee
`Assets/AncoRA/Paseo/ajuste-campo.json`, actualiza cada `edificio.json` (marcando `colocada: true` solo en los mapas
que el equipo ajustó) y vuelve a correr `Prepare`.

**Teléfono:** prefijo `AncoRA.Paseo.v1.`. Si cambia el significado de un valor, subir a `v2` o migrar.

## 9. Automatización de Editor (`PaseoSetup`)

- **`CheckInputs`**: lista edificios y mapas; error si una carpeta no tiene `edificio.json`, si un `archivo` no existe,
  si el id del nombre de archivo no coincide con `id`, si hay ids repetidos o si un id es de los ejemplos del SDK
  (90687–90690). Código de salida 1 con la lista de faltantes.
- **`Prepare`**: `CheckInputs`, luego crea/reemplaza `PaseoIngenieria.unity` desde `SimpleSample` con la jerarquía de
  §4, asigna cada `.bytes` a su `XR Map`, quita `PoseFilter`/`PoseSmoother` y deja `ProcessPoses = false` en cada
  espacio (como los pilotos; el filtro de pose es un pendiente aparte), y deja la escena como única en Build Settings.
- **`Validate`**: abre la escena y comprueba un `XR Map` por `XR Space`, que cada mapa coincida con su `edificio.json`,
  un solo `ARSession`/`XROrigin`/`ImmersalSDK`, `DeviceLocalization` presente y `ServerLocalization` ausente, ningún
  token en la escena, y que la escena esté en Build Settings.
- **`ApplyFieldAdjustment`**: §8.
- **`BuildAndroid`**: `Validate`, luego build de **desarrollo** (`BuildOptions.Development`, para ver los logs en
  logcat) con applicationId `com.ancora.ucnar.paseo`, nombre «AncoRA Paseo», salida
  `Builds/Android/AncoRAPaseo.apk`. Restaura applicationId y nombre al terminar, aunque falle.

## 10. Errores

- Un `.bytes` que no carga en el plugin nativo: el log y el HUD lo dicen por id; los demás edificios siguen.
- Un edificio sin mapas en `edificio.json`: `CheckInputs` lo informa y `Prepare` lo omite (no aborta), para poder dejar
  la carpeta de Teología preparada antes de tener su mapa.
- SDK que no queda listo en 15 s o AR no soportado: banner con el error, como en los pilotos.
- Nada se oculta en silencio: cada cambio de visibilidad por edificio se loguea con el mapa que manda.

## 11. Pruebas

**En el PC (batch):**
- EditMode: `PaseoVisibility` (casos: un mapa; dos edificios a la vez; dos mapas del mismo edificio y el más reciente
  manda; pérdida de tracking oculta todo; «mantenido» tras 10 s), `PaseoStatusText`, `PaseoConfig` (lectura,
  validación, ids repetidos, archivo que no coincide).
- Prueba de humo `PaseoSmokeTest.Run`: con dos mapas de ejemplo del SDK en una carpeta temporal, corre `Prepare` y
  `Validate`, comprueba que hay dos XR Spaces con un mapa cada uno y que la jerarquía es la de §4; borra lo temporal.
- `Prepare` + `Validate` con los mapas reales y carga nativa de los cuatro `.bytes` en el Editor.

**En terreno (Android, lo que la prueba de PC no demuestra):**
1. Cada edificio localiza y muestra su caja (anotar tiempo hasta el primer «ubicado»).
2. **Con dos edificios a la vista: ubicarse con uno, luego con el otro; la caja del primero no se mueve.**
3. Ciencias Básicas: localizar con cada uno de sus mapas; la caja cambia de mapa sin duplicarse.
4. Colocar las cajas con el panel, «Copiar valores», `ApplyFieldAdjustment`, nuevo build: las cajas aparecen colocadas
   sin ajuste.

Separar siempre: **mapa cargado** / **SDK obtuvo pose** / **caja físicamente sobre el edificio**.

## 12. Fuera de alcance de esta versión

Fichas informativas, modelos 3D y oclusión; filtro de pose; usar el `.ply` para la caja inicial; iOS; navegación entre
edificios; quitar el paquete ARCore Extensions.

## 13. Abiertos

- Nombre visible definitivo de **EIC**.
- Mapa de **Teología** (se agrega como carpeta nueva + `Prepare`).
- `-metadata.json` y `-sparse.ply` de los mapas nuevos (opcionales).
