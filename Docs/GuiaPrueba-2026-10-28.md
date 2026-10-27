# AncoRA Paseo 1.3.8 — guía de prueba en terreno (28-oct)

APK: `Builds/Android/AncoRAPaseo.apk` (build de desarrollo, código Android 20). SHA-256:
`5f20d896d72c97f3965e55e4214f4a642467ea166a06f8b955dfd1e8e23ed658`. Nada de esto está probado en un teléfono: esta guía
sirve para medirlo. (La guía `GuiaPrueba-2026-10-26.md` es de la 1.3.6 y quedó vieja.)

## Qué cambió desde la 1.3.7

1. **Arreglo del 27-oct.** El filtro de «cámara al suelo» de ayer dejaba la app sin intentos después del primer aviso (no
   liberaba la imagen de la cámara). Ahora sí la libera. Es lo primero que hay que confirmar: tras apuntar un rato al suelo,
   la app **debe seguir buscando**.
2. **Mensaje en pantalla:** con la cámara más de 1,5 s apuntando al suelo, la tarjeta dice «Levanta el teléfono».
3. **Búsqueda por edificio, sin «todos».** Antes cada ciclo de 6 intentos buscaba en los 8 mapas a la vez: en el Xiaomi
   25069PTEBG eso tardaba 13 s de media (hasta 23 s) y era la mitad del tiempo de búsqueda. Ahora el ciclo es
   0,1,0,2,0,3 (por cercanía GPS) y, sin GPS, los edificios van de uno en uno.
4. **11 mapas** (antes 8). Nuevos: `152794` (X1), `152796` (Teología), `152795` (EIC). Quedaron fuera `152793`, `152797`
   y `152798`: sus registros contra el dron se contradijeron o el mapa es muy chico (ver `Docs/PLAN-2026-10-26.md`).
   - `152794` y `152796` quedaron confirmados con dos métodos (dron y mapa-contra-mapa, a 0,2–0,3 m).
   - `152795` (EIC) es **provisional**: su registro es ambiguo (la caja podría estar corrida 2–3 m).

## Antes de salir

1. Pasar el APK al teléfono y comprobar el SHA-256. Si la instalación falla con «conflicto con la app existente»,
   desinstalar la app y volver a instalar.
2. Abrir la app una vez en casa, aceptar cámara y ubicación.

## Prueba (20 min)

Reglas: teléfono a la altura del pecho, quieto 30–40 s en cada punto, a 20–35 m del edificio. Anota la hora y el clima.

| Bloque | Dónde | Qué anotar |
|---|---|---|
| A. Seguir buscando | Apunta 5 s al suelo, luego levanta al edificio | ¿Salió «Levanta el teléfono»? ¿Después sigue buscando? (esto es lo del arreglo) |
| B. Caras ya conocidas | Ciencias Básicas frente SO, X1 frente SE, Teología frente NO, a 20 y 35 m | ¿Aparece la caja? ¿en cuántos segundos? |
| C. Mapas nuevos | X1: cara que cubre `152794` (la que mira a la zona de `x1nuevo2`); Teología: la de `152796` y la NE (`152693`); EIC: la de `152795` | ¿Qué mapa ubica? ¿La caja queda sobre el edificio? ¿Cuánto corrida? |
| D. Caminar | Ubica uno, camina ~30 m alrededor sin perderlo de vista y vuelve a mirar | ¿La caja se mantiene? |
| E. Luz | Repetir B en otra hora si se puede (mediodía vs. tarde) | Aciertos por hora |

Al terminar: hoja del equipo → **Compartir registro** y enviarlo.

## Qué reviso yo en el registro

- Errores `Could not acquire camera image`: deben ser 5 al arrancar y **cero después**.
- `Intento omitido` seguido de más `INTENTO_PASEO`.
- Segundos por intento por edificio (en el Xiaomi eran 3–5 s con 2–3 mapas, 13 s con los 8).
- Qué mapas ubicaron (`152693`, `152794`, `152796`, `152795`) y si las cajas de un mismo edificio coinciden entre mapas.
