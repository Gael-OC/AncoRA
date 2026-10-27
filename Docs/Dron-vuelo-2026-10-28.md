# Vuelo de dron para AncoRA (hoja para quien vuela)

Objetivo: fotos para armar **un solo modelo 3D** con los 4 edificios (Ciencias Básicas, EIC, X1, Teología) y el suelo entre
ellos. Con eso los 4 edificios quedan en un mismo marco (error ~0,2 m) y la app puede mostrar todos a la vez. Hoy cada edificio
sale de un vuelo distinto y cada vuelo trae su propio error de GPS (hasta ~4 m).

Antes de volar: confirmar los permisos de la universidad y las reglas de la DGAC para volar en el campus. Esta hoja no los
reemplaza.

## Reglas que no se pueden saltar

1. **Un solo día, una sola sesión de luz** (ojalá mañana nublada o con luz pareja). No mezclar con vuelos de otros días.
2. **GPS del dron encendido** y con señal fija antes de despegar. El GPS va dentro de cada foto y de ahí sale la escala.
3. **Fotos JPG originales**, sin editar, sin filtros, sin recortar, sin renombrar. Cámara en automático pero **sin zoom**
   (la misma lente en todo el vuelo) y sin modo HDR/«Hyperlapse».
4. **Una foto cada 2 s o por distancia**, no video.
5. **Mismo modelo de dron y cámara** en todos los vuelos del día.

## Qué volar, en este orden (si falta batería, parar antes del siguiente, no saltarse el orden)

| Orden | Pasada | Altura | Cámara | Cómo |
|---|---|---|---|---|
| 1 | **Grilla** sobre todo el sector (los 4 edificios y lo que hay entre ellos) | 40 m | hacia abajo (nadir) | 80 % de solape hacia adelante y 70 % al lado. Líneas paralelas |
| 2 | **Órbita** alrededor de cada edificio | 25–30 m | 45° de inclinación, mirando al edificio | Una vuelta completa por edificio, foto cada ~10° |
| 3 | **Pasada baja** frente a cada fachada | 8–10 m | horizontal o 10–20° hacia abajo | Lenta, a lo largo de la fachada. **Prioridad: EIC (el lado que falló), las partes de atrás de los 4 y el costado SO de X1** |

Orden de edificios para las órbitas y pasadas bajas: EIC primero (es el que menos datos tiene), luego X1, Teología y Ciencias
Básicas.

## Marcas para comprobar la escala

Si es posible, **4 a 6 marcas en el suelo** visibles desde arriba (cruces de tiza grandes o esquinas de baldosas), repartidas
por el sector. Medir con huincha la distancia entre dos pares y anotarla. Sirve para comprobar que el modelo no queda a
otra escala.

## Al terminar (y antes de irse del campus)

1. **Copiar todas las fotos a un computador y contar**: número de fotos por carpeta/vuelo.
2. **Revisar que ninguna pese 0 bytes** (la vez pasada llegaron 26 fotos vacías de EIC y no se notó hasta después). En Mac:
   `find <carpeta> -name '*.JPG' -size 0`. En Windows: ordenar por tamaño en el Explorador.
3. Anotar en un papel o mensaje: fecha, hora de inicio y fin, clima (nublado/sol), modelo del dron, y si hubo viento.
4. Guardar las fotos **sin tocar** (que no pasen por WhatsApp ni se comprimen).

## Qué pasa después (no lo hace quien vuela)

Las fotos se procesan juntas como **un solo proyecto** de RealityScan (en el PC de escritorio, `Tools/AlinearDron.ps1`).
Luego cada mapa de la app se registra contra esa nube (`registrar_mapa.js`) y se miden las cajas (`caja_dron.js`). Con eso se
puede poner los 4 edificios en un solo espacio.
