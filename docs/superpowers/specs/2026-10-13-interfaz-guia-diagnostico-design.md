# Interfaz nueva, guía para encontrar el edificio y diagnóstico del equipo — diseño

Fecha: 2026-10-13 · Estado: **para revisión**

## 1. Objetivo

Que AncoRA Paseo se sienta como una app bien hecha (estilo iOS: limpia, con tarjetas y animaciones), que **guíe a la
persona hasta el punto desde donde cada edificio se reconoce**, y que el equipo pueda ver **dónde funciona y dónde falla
la ubicación** para decidir dónde capturar mapas nuevos.

**Lo pedido por el usuario:** opción B (la interfaz actual bien hecha + bienvenida + tarjeta del edificio); estilo de app
de Apple; en la cámara, indicaciones animadas de acercarse, alejarse o hacia dónde ir hasta que el edificio cargue bien;
herramientas y registros más claros para que el equipo vea qué mejorar y dónde faltan mapas; permiso de ubicación (GPS)
aprobado (opción A); «haz todo».

**Supuestos (corregibles):** colores neutros tipo iOS con acento azul y el naranja de las cajas; logo de texto «AncoRA»;
la bienvenida sale en cada apertura y se salta con «Comenzar»; la descripción de cada edificio la escribe el equipo; solo
Android; la fuente es la de Unity salvo que se vea mal (entonces Inter, licencia OFL, con permiso para descargarla).

**Criterio de éxito:**
- Ninguna ventana gris de Unity (IMGUI) queda a la vista, ni del público ni del equipo.
- Una persona que abre la app por primera vez llega, guiada por la pantalla, a ver una caja ubicada sin que nadie le
  explique nada.
- En cada momento la pantalla dice una sola cosa que hacer, y esa cosa es la que más ayuda a ubicarse.
- Después de un recorrido, el equipo abre un mapa con todos los intentos (aciertos y fallas sobre los edificios) y sabe
  dónde capturar el próximo mapa.

## 2. Decisión técnica: UI Toolkit

Se reemplaza la interfaz IMGUI (`PaseoHud`, `PaseoFieldAdjust`) y el texto 3D (`PaseoLabel`, `TextMesh` con la fuente
básica) por **UI Toolkit** (UXML + USS + C#), incluido en Unity 6 (módulo `uielements`, ya en el proyecto).

- Estilos tipo CSS: bordes redondeados, transiciones de opacidad, posición, escala y giro; texto nítido; escala con la
  pantalla (`PanelSettings` en modo «scale with screen size»).
- Sin desenfoque de fondo: las tarjetas van claras y casi opacas sobre la cámara.
- La lógica sigue en clases puras con pruebas EditMode; las vistas solo leen estado y animan.
- Descartadas: uGUI + TextMeshPro (más recursos: sprites para bordes, importar TMP, animaciones a mano) e IMGUI con estilos
  (no llega al aspecto pedido).

El área segura (cámara perforada y esquinas redondeadas) sigue saliendo de `GuiSafeArea.Rect`, convertida a unidades del
panel.

## 3. Lo que ve el público

**Bienvenida.** Logo de texto, subtítulo «Paseo por la Escuela de Ingeniería» y tres pasos con ícono: camina al punto que
te indicamos, apunta al edificio sin apuro, míralo en realidad aumentada. Botón «Comenzar». Debajo: «Usa la cámara y tu
ubicación». Al tocar «Comenzar» se pide primero la cámara y después la ubicación, cada una precedida por una línea que
explica para qué sirve; recién entonces se enciende la `ARSession` (el SDK arranca solo gracias al reintento de 1.1.4).
Si niega la ubicación, la app sigue con la guía de cámara sola. Un botón «?» discreto en la cámara vuelve a mostrarla.

**Cámara.** Tres capas, siempre dentro del área segura:
- **Píldora de estado** arriba (ícono + texto corto): «Buscando edificios», «Reconociendo Teología», «Teología ubicado».
- **Tarjeta de guía** abajo con la indicación del momento (§4) y su animación.
- **Etiquetas de nombre**: una píldora con el nombre sobre cada caja visible, proyectada a la pantalla cada frame (se
  oculta si la caja queda detrás de la cámara o fuera de cuadro). Reemplaza el `TextMesh` 3D.

**Tarjeta del edificio.** Cuando un edificio se ubica, la tarjeta de guía se convierte en su tarjeta: nombre, chip
«Ubicado» y la descripción de `edificio.json` (si falta, solo el nombre). Sube con una animación corta. Si hay dos
edificios ubicados, muestra el que está más al centro de la pantalla.

## 4. La guía

### Señales

| Señal | De dónde sale | Disponible |
|---|---|---|
| Estado del tracking de ARCore | `ARSession.state` | siempre |
| Inclinación de la cámara (mirar al cielo o al suelo) | pose de la cámara AR | siempre |
| Velocidad de giro y de desplazamiento | pose de la cámara AR, promedio de 0,5 s | siempre |
| Luz | estimación de luz de ARCore (brillo medio) | si ARCore la entrega |
| Posición GPS y su precisión | `Input.location` | con permiso de ubicación |
| Rumbo de la cámara respecto del norte | ver *Rumbo* abajo | con ubicación |
| Edificios ubicados y su pose | `PaseoTour` | siempre |

### Decisión (`PaseoGuide`, lógica pura)

Gana la primera regla que se cumple; la pantalla muestra una sola indicación.

| # | Condición | Indicación | Animación |
|---|---|---|---|
| 1 | Sin tracking | «Mueve el teléfono despacio para empezar» | teléfono que se balancea |
| 2 | Hay un edificio ubicado y en pantalla | tarjeta del edificio | la tarjeta sube |
| 3 | Moviéndose rápido (giro > 90°/s o desplazamiento > 1,5 m/s) | «Más despacio» | ícono que se frena |
| 4 | Cámara al cielo (> 50° hacia arriba) o al suelo (> 35° hacia abajo) | «Apunta al edificio» | flecha arriba/abajo |
| 5 | Poca luz (brillo medio de ARCore < 0,15) | «Hay poca luz; busca la fachada iluminada» | — |
| 6 | Sin GPS (negado o sin señal) | «Apunta a un edificio del paseo» | recuadro de escaneo |
| 7 | Lejos del edificio objetivo (> distancia máxima + 15 m) | «Teología · 35 m» y «camina hacia la flecha» | flecha que gira hacia el punto |
| 8 | Del lado equivocado: el teléfono está a más de 75° del frente, visto desde el centro de la caja | «Rodea hacia el frente de Teología» | flecha |
| 9 | Más lejos que la distancia máxima | «Acércate unos 8 m» | chevrones que avanzan |
| 10 | Más cerca que la distancia mínima | «Aléjate unos 5 m» | chevrones que retroceden |
| 11 | En rango pero mirando a más de 25° del edificio | «Gira hacia Teología» | flecha |
| 12 | En rango y mirándolo | «Reconociendo Teología» + «que la fachada llene el recuadro» | recuadro que respira |

**Edificio objetivo:** el más cercano (por distancia a su punto de observación) que no está ubicado, dentro de 200 m.
Mientras uno está ubicado, la tarjeta del edificio manda; debajo, un chip pequeño «Siguiente: X1 · 60 m».

Los umbrales (50°, 35°, velocidad, 15 m, distancias) son una primera estimación: se calibran con el registro de intentos
(§6).

### Rumbo

Antes de ubicar el primer edificio, el rumbo sale de combinar el giro de la cámara AR (preciso y estable, pero sin norte)
con un desfase al norte estimado de la brújula, promediado en el tiempo. Con el teléfono vertical la brújula de Android
puede ser poco confiable: **la fase 2 empieza con una prueba en el Xiaomi** para elegir entre brújula y rumbo por
desplazamiento (comparar unos metros caminados en GPS contra los mismos metros en ARCore).

**Después del primer edificio ubicado, el rumbo y la posición son exactos**: el registro del mapa contra la nube del dron
(`<id>-registro.json`) dice cómo está ese mapa respecto del este y del norte, y la nube del dron está georreferenciada. Desde
ahí las flechas hacia los demás edificios no dependen de la brújula ni del GPS del teléfono; solo arrastran el sesgo de GPS
entre vuelos del dron (hasta ~4 m, documentado en CLAUDE.md).

## 5. Datos nuevos en `edificio.json`

```json
{
  "nombre": "Teología",
  "descripcion": "Texto corto escrito por el equipo.",
  "georef": { "lat0": -29.963997, "lon0": -71.349217 },
  "punto": { "centro": [7.10, -6.38], "frente": 302.7, "distancia": [12, 30] },
  "mapas": [
    {
      "id": 152259, "archivo": "152259-teologianuevo1.bytes",
      "caja": { "posicion": [-9.21, 0.97, -9.09], "giro": 66.66, "colocada": true },
      "enu": { "yaw": -123.91, "t": [-5.58, 1.67, -3.81] }
    }
  ]
}
```

- `georef`: origen (latitud, longitud) de la nube del dron contra la que se registraron los mapas del edificio.
- `punto.centro`: centro de la caja en metros este y norte de ese origen; `frente`: rumbo (grados desde el norte, en sentido
  horario) hacia donde mira la cara frontal (la de la X). En el marco del dron leído como Unity (x = este, z = norte) es el
  mismo giro de Unity: Teología tiene −57,26°, o sea 302,7°; `distancia`: rango, en metros desde el centro de la caja, donde se reconoce bien.
  El punto de observación es `centro + frente × promedio(distancia)`.
- `enu` por mapa: el registro mapa → nube del dron (giro + traslación, como en `<id>-registro.json`).

Todo lo calcula una herramienta nueva, `Tools/RegistroDron/punto_edificio.js`, desde `edificio.json` (la caja vigente,
incluidas las correcciones de terreno), los `<id>-registro.json` y el `-georef.json` de la nube. La distancia inicial es la
que hace caber la fachada frontal en la cámara vertical (~50° horizontales), acotada a 10–30 m; el equipo la ajusta a mano
o con el mapa de fallas. `descripcion`, `georef`, `punto` y `enu` son opcionales: `Validate` avisa si faltan y la guía cae a
las reglas sin GPS (§4, regla 6). `PaseoConfig` los lee y los valida.

## 6. Lo que ve el equipo

**Panel del equipo** (5 toques arriba a la izquierda, como hoy): una hoja que sube desde abajo, con tres secciones:
- **Estado:** SDK, tracking, GPS (precisión), y por mapa: cargado, último veredicto del filtro, confianza, usadas/vistas.
- **Métricas de la sesión:** intentos, % de aciertos (últimos 2 min y total), tiempo desde que se apunta a un edificio hasta
  que aparece su caja.
- **Ajuste de caja:** los mismos controles de hoy pero en ejes de la caja («a lo largo», «hacia el edificio», «arriba»,
  giro, ancho, alto, fondo), con pasos grueso/medio/fino; «Copiar valores», «Restaurar escena» y «Exportar intentos».

**Registro de intentos.** Cada intento de ubicación queda como una línea JSON en
`Application.persistentDataPath/intentos/<fecha-hora>.jsonl`. La primera línea describe la sesión (versión, build, mapas,
edificios con `georef` y `punto`). Cada intento guarda: hora, segundos desde el inicio, resultado (mapa o falla), veredicto
del filtro, confianza, inclinación, cuánto movería la caja, distancia a la caja, GPS (latitud, longitud, precisión,
antigüedad), rumbo y su fuente, inclinación y velocidad de la cámara, estado de tracking y, si hay un edificio ubicado, la
posición del teléfono en el marco de ese edificio (exacta). Los datos no salen del teléfono: el equipo los baja con
`adb pull`. «Exportar intentos» cierra el archivo y muestra la ruta y el comando.

**Mapa de fallas.** `Tools/MapaFallas/index.html`, una página local sin internet: se le arrastran uno o más `.jsonl` y
dibuja, en metros, las huellas de los edificios (desde `punto` y el tamaño de la caja), los puntos de observación y cada
intento (verde: usado por el filtro; ámbar: descartado; rojo: sin coincidencia), con filtros por edificio, mapa y hora y,
por edificio, intentos, % de aciertos y mediana del tiempo hasta la caja.

## 7. Unidades de código

| Unidad | Tipo | Hace |
|---|---|---|
| `PaseoGeo` | pura | latitud/longitud ↔ metros este/norte, distancia, rumbo |
| `PaseoHeading` | pura | rumbo al norte a partir del giro AR + muestras de brújula o de desplazamiento; exacto con un edificio ubicado |
| `PaseoGuide` | pura | entradas (§4) → una indicación (tipo, edificio, metros, ángulo relativo) |
| `PaseoAttemptRecord` | pura | un intento → línea JSON; cabecera de sesión |
| `PaseoSessionStats` | pura | aciertos, tiempos hasta la caja |
| `PaseoConfig` (existente) | pura | lee y valida `descripcion`, `georef`, `punto`, `enu` |
| `PaseoStatusText` (existente) | pura | texto de la píldora de estado |
| `PaseoLocationSource` | MonoBehaviour | permisos de cámara/ubicación, `Input.location`, brújula |
| `PaseoCameraSense` | MonoBehaviour | inclinación, velocidad y luz desde ARFoundation |
| `PaseoAttemptRecorder` | MonoBehaviour | escucha los resultados de ubicación y escribe el `.jsonl` |
| `PaseoUi` | MonoBehaviour | `UIDocument` raíz, área segura, cambio entre bienvenida y cámara |
| `PaseoWelcomeView`, `PaseoCameraView`, `PaseoBuildingCardView`, `PaseoLabelsView`, `PaseoTeamSheet` | vistas | dibujan y animan; sin lógica de decisión |

Salen `PaseoHud`, `PaseoFieldAdjust` y `PaseoLabel`. Los UXML, USS y `PanelSettings` viven en
`Assets/AncoRA/Paseo/_Ui/`. `PaseoSetup.Prepare` arma la interfaz en la escena (y deja la `ARSession` apagada hasta
«Comenzar»); `Validate` la exige.

## 8. Fases

Cada fase termina con pruebas en verde, `Validate` OK, un APK con versión nueva y revisión en el Xiaomi con capturas por adb.

1. **Interfaz (1.2.0):** UI Toolkit, bienvenida con permisos, píldora de estado, etiquetas de nombre, tarjeta del
   edificio, panel del equipo con ajuste en ejes de la caja. Salen las tres piezas IMGUI/TextMesh.
2. **Guía (1.3.0):** prueba del rumbo en el Xiaomi, `PaseoGeo`, `PaseoHeading`, `PaseoGuide`, fuentes de ubicación y
   cámara, datos nuevos con `punto_edificio.js`, animaciones de la tarjeta de guía.
3. **Diagnóstico (1.4.0):** registro de intentos, métricas en el panel, exportar, `Tools/MapaFallas`.

## 9. Pruebas

- EditMode: `PaseoGeo` (distancias y rumbos conocidos en el campus), `PaseoHeading`, `PaseoGuide` (una prueba por regla y
  por prioridad entre reglas), `PaseoAttemptRecord`, `PaseoSessionStats`, lectura de los campos nuevos en `PaseoConfig`.
- `punto_edificio.js`: se comprueba recalculando un valor conocido (como la transferencia de cajas del 13-oct).
- `Validate`: interfaz presente y conectada, sin componentes IMGUI, datos nuevos coherentes.
- En el teléfono: capturas por adb de cada pantalla y de cada indicación de la guía; el registro de un recorrido real.

## 10. Riesgos

- **Brújula con el teléfono vertical** (§4, *Rumbo*): se decide con datos en la fase 2.
- **GPS junto a edificios:** 5–10 m de error; la guía usa rangos amplios y el GPS solo hasta que se ubica el primer edificio.
- **Fuente:** si la de Unity se ve mal, se necesita permiso para descargar Inter.
- **Rendimiento:** UI Toolkit y la proyección de etiquetas cada frame; se mide en el Xiaomi.
- **Permiso de ubicación negado:** la app funciona igual, sin flechas ni distancias.

## 11. Fuera de alcance

Desenfoque de fondo; iOS; compartir el registro desde el teléfono (se usa adb); mapa o lista de edificios (opción C);
cambios en cómo se dibujan las cajas AR; información de edificios más allá de una descripción corta.
