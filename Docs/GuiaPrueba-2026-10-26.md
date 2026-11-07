# AncoRA Paseo 1.3.6 — guía de prueba en terreno

Versión de pruebas, rama `mejoras/sol-y-anclaje`, código Android `18`, paquete `com.ancora.ucnar.paseo`. **Build de desarrollo,
con depuración**: tiene pruebas y botones que no existen en la versión final.

Release: https://github.com/Gael-OC/AncoRA/releases/tag/paseo-2026-10-26
Descarga directa del APK: https://github.com/Gael-OC/AncoRA/releases/download/paseo-2026-10-26/AncoRAPaseo.apk
SHA-256 del APK: `37067e65cd96a436d364130eb0bba9c62a0a47364127e1cfff0aa4aaf3158bd1`

Nada de esto está probado todavía en un teléfono. Esta guía sirve para medirlo, no para confirmar que funciona.

Antes de empezar, lee la sección 1 entera. Las cosas que más tiempo hacen perder son el cable y los ajustes de caja.

---

## 0. Qué cambió desde 1.3.5

- **Pruebas A/B contra el sol** (en «Pruebas de detección» de la hoja del equipo, todas apagadas por defecto):
  - **Contraste:** `normal`, `realzado` (CLAHE antes de la búsqueda) o `alterno`.
  - **Solver:** `normal`, `gravedad` (solver con la gravedad de ARCore) o `alterno`.
  - **Prior:** `no` o `sí`. Con `sí`, si el edificio buscado ya está ubicado, la app busca solo su mapa usando la posición
    anterior. Si falla, el intento siguiente usa la búsqueda normal.
  - En modo `alterno`, la prueba cambia de un intento al siguiente, para comparar con la misma luz.
- **Filtro de pose con recorrido:** el umbral de aceptación crece con lo caminado (1 m + 3 cm por metro, tope 2,5 m), y el
  promedio olvida lo ubicado lejos (8 m). `UBICACION_PASEO` agrega `umbral=` y `recorrido=`.
- Sigue incluido lo de 1.3.5: botón «Vista», X1 corregido en terreno, búsqueda por rumbo de la cámara y «Brújula: sí / no».

Lo que todavía **no** se ha medido en terreno: todo lo de la sección 0, y la brújula desde 1.3.5 (los ejes de los sensores y
si la búsqueda por rumbo ubica más seguido).

---

## 1. Antes de salir

- [ ] Teléfono cargado (mínimo 60 %) y cable de datos que sirva para adb (no solo de carga).
- [ ] Computador con adb (`platform-tools`) y Node.js.
- [ ] Ubicación del teléfono encendida y la app con permiso de ubicación.
- [ ] Hora anotada de la prueba. El sol cambia mucho la ubicación: Teología y EIC estaban en sombra por la mañana, en
  la prueba del 23-oct (ver Notas).
- [ ] Anotar en papel el lugar de cada prueba: a qué distancia y de qué lado de cada edificio estabas parado.
- [ ] Desinstalar lo viejo solo si hace falta (ver 2.3). **Primero exportar los ajustes de caja** con «Copiar valores».

---

## 2. Instalar

### 2.1 Con cable (recomendado)

1. Conecta el teléfono y acepta el aviso «¿Permitir depuración USB?» (marcar «Confiar siempre»).
2. En el teléfono, el modo USB debe ser «Transferencia de archivos», no «Solo cargar».
3. Confirma el serial:

```bash
adb devices -l
```

Debe salir una línea con el serial seguido de `device`. Si sale `unauthorized`, falta aceptar el aviso del paso 1.

4. Instala (reemplaza `<serial>`):

```bash
adb -s <serial> install -r Builds/Android/AncoRAPaseo.apk
```

El APK local es el mismo que el del release. Si lo bajaste del release, usa esa ruta.

5. Verifica la versión instalada:

```bash
adb -s <serial> shell dumpsys package com.ancora.ucnar.paseo | grep -E "versionName|versionCode"
```

Debe decir `versionName=1.3.6` y `versionCode=18`.

**No abras la app desde adb.** Ábrela tú desde el teléfono.

### 2.2 Sin cable (desde GitHub)

1. En el teléfono, abre el enlace de descarga directa de arriba.
2. Android pide permitir «instalar apps desconocidas» para el navegador o la app de archivos. Acéptalo.
3. Abre el archivo descargado e instala.
4. Compara el SHA-256 si quieres asegurarte de que la descarga está bien.

### 2.3 Si sale `INSTALL_FAILED_UPDATE_INCOMPATIBLE`

Es una versión firmada con otra clave (otro computador). Hay que desinstalar, y eso **borra los ajustes de caja** del
teléfono. Antes:
1. Abre la hoja del equipo (5 toques arriba a la izquierda) y toca «Copiar valores». Guarda el texto.
2. Desinstala: `adb -s <serial> uninstall com.ancora.ucnar.paseo` (pide permiso antes, según las reglas del proyecto).
3. Instala de nuevo.

Si `uninstall` responde `DELETE_FAILED_INTERNAL_ERROR`, suele ser cosmético: verifica con
`adb -s <serial> shell dumpsys package com.ancora.ucnar.paseo | grep firstInstallTime`.

---

## 3. Primera apertura

1. Abre la app desde el teléfono.
2. Toca **«Comenzar»**. Acepta **cámara** y después **ubicación**.
3. Si no das ubicación, la app funciona, pero sin la búsqueda por GPS. Anótalo.
4. Espera la pantalla de la cámara. Si aparece «El SDK no quedó listo», cierra la app y vuelve a abrirla.

---

## 4. Revisar el estado (antes de empezar)

1. Abre la **hoja del equipo**: 5 toques rápidos arriba a la izquierda.
2. En **«Estado»** debe aparecer `GPS ±N m`, con N menor que 50. Si dice `-`, el GPS no tiene posición todavía; espera o sal
   a un lugar abierto.
3. Revisa que la lista de mapas muestre los 7 mapas cargados.
4. Cierra la hoja otra vez con 5 toques arriba a la izquierda.

---

## 5. Cajas y vistas: cada edificio

Para cada edificio (Ciencias Básicas, X1, Teología, EIC): párate a **15–25 m** frente a la fachada que se capturó.

Cada edificio tiene una caja amarilla (la caja del mapa) y, si tiene reemplazo, un objeto. Revisa en este orden:

1. **«Vista: Caja»** (es como abre la app en builds de desarrollo): la imagen real con la caja amarilla encima.
   - ¿La caja calza con el edificio real?
   - ¿Se corre hacia algún lado? Anota cuánto y hacia dónde.
2. **«Vista: Fondo»**: solo el cielo y el suelo pintados sobre el edificio.
   - ¿El edificio desaparece limpio o se asoman pedazos?
3. **«Vista: Objeto»**: el objeto que reemplaza el edificio (cubo en Teología, esfera en Ciencias Básicas, cilindro en X1).
   - ¿El objeto está en el lugar del edificio?

Para cambiar de vista, toca el botón **«Vista»** (bajo el «?», arriba a la derecha). Cada toque pasa a la siguiente.

Anota para cada edificio:
- Segundos hasta que aparece la caja (cronómetro en la mano).
- ¿Quedó sobre el edificio real? (sí / no / parcialmente, y dónde).

**EIC** no tiene reemplazo: solo muestra su caja. Revísala igual en «Vista: Caja».

**X1** tiene la caja corregida en terreno el 23-oct. Esta es la prueba más importante de la caja: confirma que calza.

---

## 6. Brújula: sí / no

Con el teléfono **parado** (como cuando filmas, no inclinado hacia abajo ni acostado). Hazlo frente a cada edificio:

1. En «Pruebas de detección» deja **«Brújula: sí»**. Apunta unos 10 segundos al edificio. Anota los segundos hasta la caja.
2. Cambia a **«Brújula: no»** desde el mismo lugar. Apunta otros 10 segundos. Anota los segundos.
3. A unos 10 m de un edificio, gira despacio sobre ti mismo una vuelta completa. Esto sirve para comprobar que la brújula
   gira con la cámara.
4. Anota si hay fierros, autos o motores cerca (afectan la brújula).

En el registro, `DEPURACION_POSE` trae `norte` (rumbo de la cámara), `rumbo` (de la cámara AR) y `cajas` (ángulo a cada caja).
Si `norte` menos `rumbo` cambia al girar, los ejes de la brújula están mal. Eso se revisa con el analizador (sección 10).

---

## 7. Pruebas de detección (1 minuto cada una, en el mismo lugar)

Todas están en la hoja del equipo, en «Pruebas de detección». Elige un edificio que cueste ubicar (Teología o EIC por la
mañana). Cada cambio queda con hora en el registro (`DEPURACION_AJUSTE`), así que después se separan los tramos.

| Prueba | Opciones | Qué mirar |
|---|---|---|
| **Imagen** | «reducida a 960 × 720» / «completa» | Si la completa ubica más, y cuánto tarda más cada intento |
| **Búsqueda** | «un edificio por intento» / «todos los mapas» | Segundos hasta la caja |
| **Oclusión** (sobre X1) | «sí» / «no» | Con «sí», las mesas y sillas delante deben tapar el objeto. Anota si se asoman pedazos del edificio real o si algo cercano no tapa el objeto |
| **Brújula** | «sí» / «no» | Ver sección 6 |
| **Contraste** | `normal` / `realzado` / `alterno` | Aciertos y `realce_ms` (cuánto cuesta el realce) |
| **Solver** | `normal` / `gravedad` / `alterno` | Aciertos y `rmse` |
| **Prior** | «no» / «sí» | Solo tiene efecto con un edificio ya ubicado. Anota si con «sí» los intentos ubican más seguido |

Para contraste, solver y prior:

1. Párate en un lugar fijo, con el edificio a la vista.
2. Deja la prueba en `normal` (o `no`) durante **1 minuto** y anota el número de aciertos (cuántas veces apareció la caja)
   y el tiempo hasta la primera.
3. Cambia a la siguiente opción y repite lo mismo, sin moverte.
4. Si la prueba es `alterno`, deja 2 minutos: el modo `alterno` cambia de intento a intento.

Las pruebas vuelven a lo normal al cerrar y abrir la app. Antes de cerrarla, comparte el registro (sección 10).

---

## 8. Filtro de pose con recorrido

Esta es la prueba del filtro que sigue lo caminado:

1. Ubica un edificio (la caja tiene que aparecer y quedar en su lugar).
2. Camina **30 a 40 m** alrededor del edificio, sin perder de vista la fachada.
3. Vuelve a mirar la caja. ¿Sigue sobre el edificio? Anota si se corrió y cuánto.
4. En el registro, busca las líneas `UBICACION_PASEO`. Revisa `umbral=` (crece con lo caminado) y `recorrido=` (cuánto
   caminaste desde la última ubicación usada). Si las ubicaciones correctas salen como `lejos` después de caminar, el
   umbral está muy bajo.

---

## 9. Calor y batería

- Usa la app **10 minutos seguidos**, buscando edificios.
- Anota la batería al empezar y al terminar.
- En el registro, `ESTADO_PASEO` cada 10 s trae `bateria`, `termico` (3 = el teléfono ya baja el rendimiento) y `margen`.
- Si el teléfono calienta mucho o `termico` llega a 3, anótalo: el ritmo de búsqueda puede ser demasiado alto.

---

## 10. Registro: compartir, bajar y analizar

### 10.1 Compartir desde el teléfono (sin cable)

En la hoja del equipo, toca **«Compartir registro»**. La app arma un zip con las 3 últimas sesiones, sus fotos de depuración
y los datos del teléfono, y lo guarda en **Descargas/AncoRA**. Abre el menú de compartir para mandarlo (por ejemplo por WhatsApp,
como documento). El nombre es `AncoRA-registro-<modelo>-<fecha-hora>.zip`.

### 10.2 Bajar con cable (Mac)

```bash
mkdir -p Registros
adb -s <serial> pull /sdcard/Android/data/com.ancora.ucnar.paseo/files/registro ./Registros/
```

Esto crea la carpeta `Registros/registro/` (`Registros/` está ignorada por git). Cada sesión es un archivo `paseo-*.log`.

### 10.3 Analizar

```bash
node Tools/AnalizarRegistro.js Registros/registro/<paseo-*.log>
```

Opcionalmente, con CSV:

```bash
node Tools/AnalizarRegistro.js Registros/registro/<paseo-*.log> --csv=Registros/<nombre>.csv
```

El analizador resume: intentos por edificio buscado y motivo, aciertos por prueba de detección, veredictos del filtro,
la primera caja de cada edificio, batería, temperatura, FPS, cambios de tracking y, desde 1.3.5, la diferencia entre
`norte` y el rumbo AR.

### 10.4 Líneas que importan

- `INTENTO_PASEO`: edificio buscado, `motivo`, `seg`, `resultado`, `conf`, `contraste`, `solver`, `prior`, `realce_ms`,
  `rmse`, `rumbo` y `rumbo_dif`.
- `UBICACION_PASEO`: veredicto del filtro (`fijada`, `aceptada`, `lejos`, `reanclada`, `inclinada`, `esperando`), `umbral`,
  `recorrido`.
- `DEPURACION_AJUSTE`: cuándo cambió cada prueba.
- `DEPURACION_POSE`: cada segundo, cámara, `norte`, `rumbo`, `brujula`, `campo_ut`, `cajas`.
- `DEPURACION_UI`: cuándo cambiaste la vista (`vista=caja|fondo|objeto`).
- `ESTADO_PASEO`: batería, temperatura y GPS cada 10 s.

---

## 11. Reportar

Por cada prueba, anota:

- Fecha, hora y lugar (a qué distancia y de qué lado del edificio).
- Modelo del teléfono (Xiaomi 14 o Galaxy S25) y si era el de release o el de desarrollo.
- Por edificio: segundos hasta la caja, si quedó en su lugar, y desde dónde.
- Por prueba de detección: aciertos en 1 minuto y primer acierto.
- Brújula: si `norte` y `rumbo` cambian juntos al girar.
- Filtro con recorrido: si la caja se mantuvo y cuánto se corrió.
- Calor y batería después de 10 minutos.
- El zip o el archivo del registro.

---

## Notas

- **Sol:** el 23-oct, Teología (0 de 10) y EIC (0 de 9) no ubicaron por la mañana, cuando estaban en sombra y a contraluz.
  X1, con sol de frente, ubicó 3 de 6 en la última sesión. Anota la hora siempre.
- **Firma:** si el APK cambia de computador, hay que desinstalar (sección 2.3). Eso borra los ajustes de caja.
- **Vista:** los builds de desarrollo arrancan en «Vista: Caja». Un build de release sin el botón arranca en «Objeto».
- **Lo que no se ha probado:** el prior con un solo mapa, el costo de `alterno`, y si el filtro con recorrido mejora la
  caja después de caminar. Todo eso es para medir en esta prueba.
