# Parque animado sobre los edificios (1.5.0) — diseño y plan

Fecha: 2026-10-29. Estado: **sin probar en terreno**. El usuario pidió saltarse la lluvia de ideas: todo lo que sigue son
decisiones de Claude salvo lo marcado como pedido.

## Intención

Pedido: animaciones encima y alrededor de cada edificio del paseo AR, estilo parque, con modelos low-poly y personajes de
bloques (estilo Roblox, **sin nada de su marca**) que caminen, salten, saluden y bailen. Debe aparecer animado sobre la caja de
cada edificio en el teléfono y poder prenderse y apagarse.

Por qué ahora: la ubicación ya es usable en el modo campus (1.4.x); esto es lo primero que se ve más allá de la caja amarilla.

## Metas / no-metas / anti-metas

- Metas: un «parque» por edificio, anclado a su caja, animado por código; interruptor visible para cualquiera; datos en
  `edificio.json`; `Prepare` lo arma y `Validate` lo comprueba; pruebas EditMode de la lógica pura.
- No-metas: reemplazar la caja por el edificio antiguo (sigue postergado); oclusión con el edificio real; sombras; sonido;
  interacción con toques.
- Anti-metas: assets con marca o de licencia dudosa; materiales o mallas creados en runtime sin liberar; tocar la ubicación
  (filtro, búsqueda, mapas); bajar los FPS de forma notable (tope ~250 renderers en todo el campus).

## Decisiones (de Claude)

1. **Modelos hechos con primitivas, sin assets externos.** Cubos, esferas y cilindros integrados de Unity
   (`Resources.GetBuiltinResource<Mesh>("Cube.fbx")`, etc.). Cero licencias, cero descargas, todo reproducible por `Prepare`.
   Kenney/Quaternius (CC0) quedan como opción futura si se quieren modelos más ricos.
2. **La escena guarda la jerarquía; el runtime solo mueve transforms.** `Prepare` crea, bajo cada `Caja …`, un hijo
   `Parque` con el componente `PaseoPark`, sus personajes (cada uno: raíz + cuerpo + cabeza + 2 brazos + 2 piernas, con pivote
   en hombros y caderas) y sus adornos (árboles, átomo, bandera, bola de disco). No se crea ninguna malla ni material en runtime,
   así que no hay nada que liberar en `OnDestroy`.
3. **Materiales:** un shader nuevo `AncoRA/Paseo/Bloques` (color plano + luz fija semi-Lambert sobre la normal; las caras del
   cubo integrado tienen normales por cara, así que se ve facetado tipo low-poly; ZTest LEqual, ZWrite On, cola
   `Transparent+10` para dibujarse **después** del relleno translúcido de la caja, que tiene ZTest Always y si no los teñiría).
   Una paleta fija de ~10 materiales `Assets/AncoRA/Paseo/_Materiales/Bloques/PaseoBloque-<color>.mat` creados por `Prepare`.
   No hay luces en la escena (Prepare borra la Directional Light), por eso la luz va en el shader.
4. **Anclaje:** el `Parque` es hijo de la caja (marco local: centro de la caja, X ancho, Y alto, Z fondo; +Z es la cara
   «frente» de `EdificioFacadeFrame`). Las posiciones del diseño se guardan **normalizadas** (fracción del ancho/fondo, techo o
   suelo), así que si el equipo cambia el tamaño de la caja en terreno (`PaseoMapContent.SetSize`) el parque se reacomoda con
   `PaseoPark.Relayout(size)`. Techo = y +alto/2, suelo = y −alto/2.
5. **Escala:** personaje de 2,7 m de alto (1,5× una persona) para que se lea desde 15–50 m; `parque.escala` en
   `edificio.json` lo cambia (por defecto 1,5; rango 0,5–4).
6. **Encendido/apagado:** botón redondo «Parque» junto al «?» de la vista de cámara (para todos, no solo el equipo).
   Se guarda en `PlayerPrefs` `AncoRA.Paseo.v3.Parque` (1 por defecto = encendido); es una clave nueva, no cambia el
   significado de las existentes, así que no hace falta subir el prefijo. El parque se ve solo si la caja se ve **y** el
   interruptor está encendido (se desactiva el GameObject `Parque`, lo que también detiene su `Update`). Log:
   `PARQUE encendido` / `PARQUE apagado`. La caja amarilla se sigue viendo (es la referencia de alineación).
7. **Animaciones por código** (lógica pura en `PaseoBlockMotion`, probada en EditMode): `Caminar` (piernas y brazos en
   oposición, recorre un circuito), `Saltar` (parábola cada 1,2 s con agache), `Saludar` (brazo derecho arriba oscilando),
   `Bailar` (balanceo de cadera, brazos alternados, giro). Cada personaje quieto alterna su repertorio cada ~6 s
   (`PaseoParkSchedule`), con desfase por personaje para que no vayan sincronizados.
8. **Recorridos:** circuito rectangular con esquinas, metido hacia adentro del borde del techo (margen 1,2 m) o por fuera de la
   caja en el suelo (a 1,5 m). Muestreo por distancia recorrida a 1,4 m/s (`PaseoParkPath`).
9. **Qué se ve en cada edificio** (`parque.tema` en `edificio.json`):

   | Edificio | Tema | Techo | Suelo |
   |---|---|---|---|
   | Ciencias Básicas | `ciencias` | 3 personajes que alternan bailar/saltar/saludar; un átomo flotando (núcleo + 3 electrones en órbita) | 2 caminando alrededor, 2 árboles |
   | X1 | `pista` | techo largo y bajo: 2 trotando por el borde (pista), 2 saltando, una bandera | 1 caminando, 2 árboles |
   | Teología | `saludo` | 4 en fila en el borde del frente (+Z) saludando, 1 bailando | 1 caminando, 2 árboles |
   | EIC | `baile` | 5 en ronda bailando bajo una bola de disco que gira | 1 caminando, 1 árbol |

   La cara +Z puede no ser la de la vereda: es una suposición; `parque.frente` (`+z`, `-z`, `+x`, `-x`, por defecto `+z`)
   permite girarlo sin tocar código.
10. **Colores** deterministas: semilla = hash estable del id del edificio (no `string.GetHashCode`, que cambia entre
    plataformas). Mismo edificio, mismos colores en todas las corridas.

## Datos

```json
"parque": { "tema": "ciencias", "escala": 1.5, "frente": "+z" }
```

Sin `parque`, el edificio no lleva parque. `PaseoConfig.Validate`: tema desconocido, escala fuera de 0,5–4 o frente inválido →
error.

## Plan

### Tarea A — lógica pura + pruebas (Sonnet, TDD)

Archivos nuevos en `Assets/AncoRA/Scripts/Paseo/` y pruebas en `Assets/AncoRA/Editor/Tests/`:

- `PaseoParkConfig` (`[Serializable]` con `tema`, `escala = 1.5f`, `frente = "+z"`) agregado como `parque` a
  `PaseoBuildingConfig`; validación en `PaseoConfig.Validate`.
- `PaseoParkLayout.Build(PaseoParkConfig, Vector3 boxSize, string buildingId)` → `PaseoParkPlan` con listas de
  `PaseoActorSpot` (superficie techo/suelo, posición normalizada, giro, repertorio, recorrido, color, desfase) y
  `PaseoPropSpot` (tipo, posición normalizada, escala). `PaseoParkLayout.Resolve(spot, size)` → posición local en metros.
  Determinista; todo dentro del techo (con margen) o fuera de la caja en el suelo.
- `PaseoParkPath`: circuito rectangular (inset/outset), largo, `Sample(distancia)` → posición + giro, continuo y periódico.
- `PaseoBlockMotion.Evaluate(PaseoMove, t, phase)` → `PaseoBlockPose` (giro de hombros/caderas/cabeza, alto del cuerpo, giro
  del cuerpo). Periódico, salto ≥ 0, saludo con brazo arriba, caminar con piernas en oposición.
- `PaseoParkSchedule.MoveAt(repertorio, t, phase)`.
- `PaseoStableHash.Of(string)` (FNV-1a).

### Tarea B — vista, escena, interruptor (Sonnet; depende de A)

- Shader `Assets/AncoRA/Paseo/_Materiales/PaseoBloques.shader` (`AncoRA/Paseo/Bloques`).
- `PaseoPark` (MonoBehaviour en el hijo `Parque`): referencias serializadas a los transforms de cada personaje/adorno,
  `Configure(...)` desde el editor, `Relayout(size)`, `Update` que anima con `PaseoBlockMotion`/`PaseoParkPath`.
- `PaseoMapContent`: referencia a `PaseoPark` (puede ser null), `SetVisible` y `SetSize` lo propagan, `SetParkEnabled(bool)`.
- `PaseoTour`: `ParkEnabled` (lee/guarda `AncoRA.Paseo.v3.Parque`), lo aplica a todos los edificios, log `PARQUE …`.
- `PaseoCameraView`: botón «Parque» (clase de encendido/apagado) con evento; `PaseoUi` lo conecta al tour. Estilo en
  `Paseo.uss`.
- `PaseoSetup.Prepare`: crea materiales de la paleta y la jerarquía del parque para cada edificio con `parque`.
  `PaseoSetup.Validate`: parque presente sí y solo sí hay `parque`, tema y cantidad de personajes/adornos iguales al plan,
  materiales con el shader `AncoRA/Paseo/Bloques`, sin mallas generadas (solo integradas).
- `parque` en los 4 `edificio.json`.

### Tarea C — verificación (yo)

Pruebas → `Prepare` → `Validate` (`CheckNativeMapsInEditor`) → `BuildAndroid` con `Tools/UnityBatch.ps1`; subir versión a
1.5.0 (código 26); CLAUDE.md al día. En el teléfono: falta que el usuario lo vea en terreno.
