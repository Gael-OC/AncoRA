# Guía de captura — piloto de fachada del edificio de ingeniería

**Para el equipo, en terreno. El público nunca hace nada de esto:** abre la app, apunta, ve el marco.

Estado: **faltan todos los datos reales** (ver la lista exacta al final). Hasta que existan, la escena
del edificio no se crea y los builds del edificio se niegan a generarse. Ninguna cifra de esta guía es
una medición del sitio; los umbrales marcados como *criterio práctico* son heurísticas nuestras, no
requisitos de Immersal.

## 0. Qué se busca y qué no

- Se busca **un solo marco** de tamaño real, alineado con la fachada, estable al mover el teléfono.
- Plan gratuito de Immersal: varios mapas de hasta 100 imágenes; el SDK admite varios `XR Map` en una
  escena, pero el *stitching/alineación automáticos del portal son Enterprise*. Aquí se mantienen **dos
  mapas `.bytes` separados** y se alinean **a mano en Unity, una sola vez, por el equipo**.
- Varias PLY juntas **no** forman un mapa de localización. La PLY solo sirve para *ver* la alineación.
- La precisión milimétrica no es realista. Meta inicial: **20–30 cm** de error visual, a confirmar en
  terreno. La meta de **3–5 s** desde el toque del icono es un objetivo a medir, no un resultado.

## 1. Levantamiento físico (antes de mapear)

Herramientas: huincha larga o medidor láser, cinta de color, cámara del teléfono, libreta o la tabla de
abajo.

1. **Puntos de observación.** Recorrer el lugar y fijar los puntos desde donde la gente realmente verá
   la fachada (P1, P2, …). Para cada uno: distancia aproximada a la fachada, si hay veredas/calles
   transitadas, sol de frente o de espaldas. Si existen puntos razonables a ~15, ~25 y ~40 m, marcarlos;
   si no existen, **no inventarlos**.
2. **Dimensiones de la fachada.** Ancho total y alto (hasta la cornisa o el borde visible). Anotar cómo se
   midió (láser, huincha, plano) y la incertidumbre.
3. **Al menos tres detalles físicos permanentes** reconocibles en fotos y en Unity: esquina de un vano,
   junta de hormigón, columna, placa, borde de ventana, remate. Evitar carteles, plantas, mobiliario.
   - Definir un origen físico (p. ej. esquina inferior izquierda de la fachada mirándola de frente),
     el eje X a lo largo de la fachada y el eje Y hacia arriba.
   - Medir **todas las distancias entre pares** de detalles (horizontal, vertical y diagonal). Con tres
     detalles no alineados se fija escala y giro; conviene un cuarto para comprobar. Es preferible que dos
     de ellos estén separados **≥ 10 m** en horizontal (*criterio práctico*) porque el giro se estima mal
     con detalles cercanos.
4. Sacar una foto frontal de cada detalle con una regla o huincha visible.

| Detalle | Descripción | Coord. X / Y desde el origen (m) | Foto | Medido con |
|---|---|---|---|---|
| D1 | | | | |
| D2 | | | | |
| D3 | | | | |
| D4 (opcional) | | | | |

| Par | Distancia horizontal (m) | Vertical (m) | Diagonal (m) |
|---|---|---|---|
| D1–D2 | | | |
| D1–D3 | | | |
| D2–D3 | | | |

Además: **ancho y alto del marco** que se mostrará (normalmente el ancho×alto de la fachada o del
paño elegido) → van a `edificio-medidas.json` (sección 5).

## 2. Dos mapas piloto con Immersal Mapper

Usar un celular compatible con Immersal Mapper. Registrar modelo y SO.

| Mapa | Cubre | Regla |
|---|---|---|
| **A** | **Centro** de la fachada | Es la referencia: define el origen del XR Space. |
| **B** | Sector **contiguo** | Debe compartir una franja física con A. |

Reglas de captura:

- **Franja común.** Capturar A y B pasando por la **misma franja física** (con al menos dos de los
  detalles medidos dentro de ella). *Criterio práctico:* que la franja sea de varios metros de ancho y
  que ambos mapas la vean con buen solape, no solo en un borde.
- **Variedad.** Varias posiciones, distancias y ángulos, con paso lento y pausas; no una sola pasada.
  Mover el teléfono despacio, sin giros bruscos.
- **Presupuesto.** Cada mapa **< 100 imágenes** (límite del plan). *Criterio práctico:* apuntar a ~60–70
  por mapa para tener margen de repetir tomas deficientes sin pasar el límite.
- **Qué evitar como rasgos principales:** personas, vehículos, reflejos en vidrios, patrones repetitivos
  (rejas, ladrillos uniformes), vegetación que se mueve, carteles temporales, sombras duras que cambian.
  Capturar con luz pareja; anotar hora.
- **Distancias de uso real.** Incluir tomas desde las distancias reales de uso (por ejemplo 15, 25 y
  40 m **si son puntos existentes y razonables**). El equipo puede acercarse durante el levantamiento; el
  usuario final no deberá hacerlo.

## 3. Probar A y B **por separado** antes de tocar Unity

En Immersal Mapper, para cada mapa y **desde cada posición prevista** (P1, P2, …, y las distancias
15/25/40 m si existen): abrir directamente desde ahí, sin acercarse antes, y anotar si **relocaliza**, en
cuánto tiempo y si falla.

| Mapa | Punto / distancia | Arranque nº | ¿Relocaliza? | Tiempo (s) | Falla / observación |
|---|---|---|---|---|---|

**Si B no reconoce la fachada por sí solo desde los puntos requeridos, no se arregla alineándolo con A.**
Alinear en Unity solo coloca un mapa que ya funciona respecto de otro; no le da capacidad de
relocalizar. En ese caso hay que recapturar B (más imágenes útiles desde esas distancias), no forzar la
unión.

## 4. Exportar y registrar

Del portal, para **cada** mapa (A y B):

1. **ID** del mapa.
2. **`.bytes`** (el archivo con el que se localiza el teléfono).
3. **Metadata JSON** (`<id>-<nombre>-metadata.json`).
4. **PLY sparse** (`<id>-<nombre>-sparse.ply`) — solo visualización.

Registrar en una hoja: nombre, fecha, dispositivo, zona cubierta, nº de imágenes, puntos compartidos
(qué detalles físicos caen en la franja común) y resultado de la sección 3.

> **Nunca** pegar tokens ni claves del portal en código, escenas, logs publicados ni Git. El piloto va
> sin token (`developerToken` vacío, mapas embebidos).

## 5. Dónde dejar los archivos

```
Assets/AncoRA/ImmersalEdificio/
  MapaA/<idA>-<nombre>.bytes
  MapaA/<idA>-<nombre>-metadata.json
  MapaA/<idA>-<nombre>-sparse.ply
  MapaB/<idB>-<nombre>.bytes
  MapaB/<idB>-<nombre>-metadata.json
  MapaB/<idB>-<nombre>-sparse.ply
  edificio-medidas.json
```

`edificio-medidas.json` (valores **medidos**, en metros; los de abajo son solo el formato):

```json
{
  "frameWidthMeters": 0.0,
  "frameHeightMeters": 0.0,
  "note": "cómo y con qué se midió"
}
```

Los nombres de archivo deben quedar tal como los exporta Immersal. Comprobar que no falta nada:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -nographics -quit `
  -projectPath "C:\dev\AncoRA-immersal" `
  -executeMethod AncorRA.Editor.ImmersalEdificioPilotSetup.CheckInputs -logFile C:\dev\ancora-edificio-datos.log
```

Ver `IMMERSAL_EDIFICIO_PILOT.md` para el resto del flujo (Prepare, alineación A↔B, builds).

## 6. Dron DJI Mini 5 — para qué sirve y para qué no

- **Las fotos del DJI no se importan directamente en el flujo gratuito de Immersal Mapper.** Mapper crea
  el mapa con sus propias capturas desde el celular.
- La vía de Immersal para imágenes externas usa **otra API, metadatos de cámara y condiciones de licencia**
  que no debemos suponer disponibles en Free. Antes de intentarla hay que confirmarlo con Immersal.
- Uso realista del dron, **después o en paralelo**: fotos **oblicuas de las zonas altas** de la fachada y
  una reconstrucción geométrica externa (por ejemplo COLMAP u OpenDroneMap) para **medir alturas y
  verificar dimensiones/planos** del marco, escalando el modelo con las distancias del punto 1. Es una
  referencia geométrica, no un mapa de localización.
- Verificar normativa aérea local y autorización del recinto antes de volar.

## Lista exacta de lo que falta

**Archivos (12):**

- [ ] `MapaA/<idA>-<nombre>.bytes`, `-metadata.json`, `-sparse.ply`
- [ ] `MapaB/<idB>-<nombre>.bytes`, `-metadata.json`, `-sparse.ply`
- [ ] `edificio-medidas.json` con ancho y alto del marco

**Medidas y registros:**

- [ ] Ancho y alto de la fachada y del marco, con método e incertidumbre.
- [ ] ≥ 3 detalles permanentes con coordenadas X/Y y distancias entre pares (tabla 1), con fotos.
- [ ] Puntos de observación reales y su distancia a la fachada (¿existen 15/25/40 m?).
- [ ] Resultado de relocalizar A y B por separado desde cada punto (tabla 3).
- [ ] Ficha de cada mapa: ID, nombre, fecha, dispositivo, zona, nº de imágenes, puntos compartidos.
- [ ] Un iPhone y un Android disponibles para las pruebas de aceptación.

**No hay nada de esto en el repositorio hoy.** Los mapas `151649` (Fuente) y los de ejemplo del SDK no
son del edificio y el validador los rechaza.
