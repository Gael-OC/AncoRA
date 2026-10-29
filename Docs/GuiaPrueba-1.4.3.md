# AncoRA Paseo 1.4.3 — guía de prueba en terreno

**1.4.3** = la 1.4.2 de Nico (1440×810 por defecto, selector «Resolución de la imagen» y «Probar este teléfono») más: el umbral de
primera ubicación según el tamaño de la imagen, un ciclo de búsqueda mejor dirigido y la brújula guardada en las grabaciones.
Mapas y cajas no cambian. APK: `Builds/Android/AncoRAPaseo.apk` (versión 1.4.3, código 25).

## Qué cambió respecto de la 1.4.2

- **Umbral de primera ubicación por tamaño** (`PaseoResolution.SingleLockConfidence`): 20 a 960×720, **30 a 1440×810**, 45 completa. Con
  una imagen más grande, las ubicaciones falsas entre edificios salen con más confianza (EIC-B contra un mapa de Ciencias Básicas: 15 →
  26 → 31 → 43). Una primera ubicación bajo el umbral espera una segunda que coincida (`veredicto=pendiente`). Con 1440×810 y el umbral
  viejo (20), 2 de 5 falsas habrían pasado.
- **Búsqueda por edificio**: el ciclo por rango GPS es `0,1,0,1,0,1,2,0,1,3` (antes `0,1,0,2,0,3`). En las grabaciones del 28-oct el edificio
  apuntado era el más cercano por GPS solo el 40 % de las veces y el segundo el 55 %. Simulado: el edificio correcto se busca en 50 % de los
  intentos (antes 34 %); la mediana de espera no cambia, el p90 hasta que aparece la caja baja de 32 s a 22 s (0,69 MP) y de 72 s a 41 s
  (1,23 MP). Todo a ~15 m. En el registro, `busca=` y `motivo=` dicen a qué edificio fue cada intento.
- **Brújula, solo para medir**: con Grabar encendido, cada línea de `intentos.jsonl` trae `brujula` (norte real y magnético, precisión, campo en
  µT). Nada de la app la usa. Un filtro por radio de GPS no sirvió (a 11–31 m del apuntado hay 2,9 de 4 edificios en rango), así que falta el
  rumbo: `node Tools/AnalizarBrujula.js <carpeta de grabaciones>` compara la brújula con el giro de la cámara de AR, mira si el campo está
  clavado y cuenta cuántas veces rumbo + GPS aciertan el edificio.

## Prueba (30–40 min)

Reglas de siempre: teléfono a la altura del pecho, quieto 30–40 s por punto. Anotar hora, clima y teléfono.

1. **Primero «Probar este teléfono»** en cada teléfono (hoja → Resolución de la imagen) y anotar qué tamaño elige y cuánto tardó.
2. **Grabar encendido** en toda la prueba (edificio, punto, distancia y luz). Las grabaciones guardan siempre la imagen completa, así que cualquier
   tamaño se puede reproducir después en el computador (`PaseoReplay -replayPixels 691200,1166400,0`).
3. **Distancia 10–20 m**, que es lo más lejos que permite el campus. Un punto **desde cada extremo del pasillo mirando a EIC y a Ciencias
   Básicas** (donde el GPS pone primero a otro edificio), anotando hacia dónde se mira. Usar los dos teléfonos: el Xiaomi 25069PTEBG tenía la
   brújula pegada el 26-oct.
4. **Un tamaño por pasada** en el mismo punto (960×720, 1440×810, completa), 40 s cada uno, para comparar segundos por intento (`px=` y `seg=`).
5. **Puntos**: los difíciles (Teología A, C y F; EIC A y B) y dos que ya ubican (X1 frente SE, Ciencias Básicas frente SO).
6. **Falsas**: en EIC, mirar si aparece una caja corrida o saltan las 4 cajas; anotar el tamaño.
7. Lo de la 1.4.1 (`Docs/GuiaPrueba-1.4.1.md`): las 4 cajas aparecen y cuánto se sale cada una sin volver a ubicar.

Al terminar: hoja → Compartir registro y Copiar grabaciones a Descargas (con la grabación apagada).
