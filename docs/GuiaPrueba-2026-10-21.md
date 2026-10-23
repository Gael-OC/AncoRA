# AncoRA Paseo 1.3.4 — prueba de detección, reemplazo y oclusión

Versión de pruebas del 21 de octubre de 2026, rama `nicowo`. Paquete Android `com.ancora.ucnar.paseo`, código de versión
`16`. Build de desarrollo, con depuración. Nada de esto está probado en terreno todavía.

## Qué cambia desde 1.2.2 (hasta 1.3.4)

- **Busca un edificio por intento.** Antes cada intento buscaba en los 7 mapas, y uno sin coincidencia tardaba ~10 s. Ahora
  la app elige el edificio con el GPS: primero el que ya está ubicado a la vista, después el más cercano. Cada seis
  intentos busca en todos los mapas, por si el GPS se equivoca. Sin GPS busca en todos, como antes.
- **La caja aparece con la primera coincidencia.** La tarjeta dice «Confirmando» hasta que una segunda coincidencia la
  confirma; ahí pasa a «Ubicado».
- **Aviso de búsqueda** bajo la píldora: «Buscando Teología · 3 imágenes sin coincidencia»; en verde cada coincidencia, y
  en ámbar las que el filtro descartó, con el motivo.
- **Depuración** (solo en esta build de desarrollo): más líneas en el registro de la sesión y dos pruebas de detección en
  la hoja del equipo.
- **Reemplazo del edificio** (1.3.1): al ubicar Teología, Ciencias Básicas o X1, el edificio real desaparece de la pantalla
  (se pinta cielo y suelo encima) y en su lugar aparece un objeto: un cubo que gira en Teología, una esfera en Ciencias
  Básicas y un cilindro en X1. EIC sigue con su caja amarilla. Donde la caja quede corta se puede asomar el edificio real.
- **Compartir el registro** (1.3.2): botón «Compartir registro» en la hoja del equipo (ver más abajo), fotos de lo que ve
  la cámara cada 3 s y más líneas de depuración.
- **Oclusión** (1.3.3): con la profundidad de ARCore, lo real que está delante (mesas, sillas, gente) tapa el objeto virtual
  y el cielo pintado. El edificio reemplazado no vuelve a aparecer: solo cuenta como «delante» lo que está a más de 3 m por
  delante del edificio. En un teléfono sin Depth API se ve como antes. En «Pruebas de detección» hay un interruptor
  «Oclusión: sí / no».
- **Modo de ajuste** (1.3.3): con la hoja del equipo abierta, los edificios con reemplazo muestran su caja amarilla en vez
  del objeto, para alinearla contra el edificio real con «Ajuste de caja». Al cerrar la hoja vuelve la vista elegida
  (desde 1.3.4, ver «Vista»).
- **Botón «Vista»** (1.3.4): bajo el «?», arriba a la derecha, cicla cómo se dibujan los edificios con reemplazo para
  revisarlos por partes: «Vista: Caja» (la imagen real con la caja amarilla encima; es como abre la app), «Vista: Fondo»
  (solo el cielo y el suelo pintados sobre el edificio, sin objeto ni caja) y «Vista: Objeto» (el fondo y el objeto, como
  en 1.3.3). Después vuelve a «Caja». EIC, que no tiene reemplazo, siempre muestra su caja. Con la hoja del equipo abierta
  se ve la caja en cualquier vista. Cada cambio queda en el registro como `DEPURACION_UI vista=caja|fondo|objeto`.

## Instalar

```bash
adb devices
adb -s <serial> install -r AncoRA-Paseo-2026-10-21-4.apk
```

Si Android rechaza la actualización (`INSTALL_FAILED_UPDATE_INCOMPATIBLE`), la versión instalada viene de otro
computador. Antes de desinstalar, abrir la app, ir a la hoja del equipo y tocar «Copiar valores», porque la
desinstalación borra los ajustes de caja del teléfono.

Al abrir: «Comenzar» y aceptar **cámara y ubicación**. Sin ubicación la app funciona, pero sin la búsqueda por GPS.

## Recorrido sugerido (unos 15 minutos)

1. Con la ubicación del teléfono encendida, abrir la hoja del equipo (5 toques arriba a la izquierda). En «Estado» debe
   decir `GPS ±N m`, con N bajo 50.
2. Para cada edificio (Ciencias Básicas, X1, Teología, EIC): pararse a 15–25 m frente a la fachada que se capturó, apuntar
   sin apuro y anotar cuántos segundos pasan hasta que aparece la caja y si queda sobre el edificio. Revisar cada edificio
   con reemplazo primero en «Vista: Caja» (¿la caja calza con el edificio real?), después en «Vista: Fondo» (¿el edificio
   desaparece limpio, sin pedazos que se asomen?) y al final en «Vista: Objeto».
3. **Pruebas de detección**, frente a un edificio que cueste ubicar. Cada una, 1 minuto en el mismo lugar:
   - «Búsqueda: un edificio por intento» y después «Búsqueda: todos los mapas».
   - «Imagen: reducida a 960 × 720» y después «Imagen: completa». La completa ve más detalle de una fachada lejana, pero
     cada intento tarda más.

   Cada cambio queda anotado en el registro con su hora, así después se separan los tramos.
4. **Oclusión**, frente a X1, donde hay mesas y sillas delante: con «Vista: Objeto» (o «Fondo»), comparar «Oclusión: sí» y
   «no». Con «sí», las mesas y sillas deben quedar por delante del cielo pintado y del cilindro. Anotar si se asoman pedazos del
   edificio real (habría que subir la `holgura`) o si algo cercano no tapa el objeto (habría que bajarla).
5. **Ajustar la caja de X1**: con la hoja del equipo abierta se ve la caja amarilla. Con «Ajuste de caja», moverla y girarla
   hasta que calce con el edificio real, tocar «Copiar valores» y después «Compartir registro». El ajuste viaja en el zip.
6. Usar la app 10 minutos seguidos, para medir calor y batería.

## Compartir el registro (sin cable, desde 1.3.2)

Al terminar, en la hoja del equipo tocar **«Compartir registro»**. La app arma
`AncoRA-registro-<modelo>-<fecha-hora>.zip` con las 3 últimas sesiones, sus fotos de depuración y los datos del teléfono, lo
guarda en **Descargas/AncoRA** y abre el menú de compartir: elegir WhatsApp y mandarlo como documento. Sirve para probar
con varios teléfonos a la vez: cada zip dice de qué modelo es.

En el computador:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\BajarRegistro.ps1 -Archivo "<ruta del zip>"
```

## Bajar el registro con cable

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\BajarRegistro.ps1
```

Equivale a `adb -s <serial> pull /sdcard/Android/data/com.ancora.ucnar.paseo/files/registro`, y además resume la sesión.

Un archivo por sesión. Las líneas que importan:

- `INTENTO_PASEO`: qué edificio buscó y por qué (`motivo=gps`, `a-la-vista`, `orden` o `todos`), cuánto tardó (`seg`) y qué
  encontró.
- `DEPURACION_INTENTO` (mismo `n`): tamaño de la imagen, dónde estaba la cámara, el tracking, el edificio a la vista y el
  GPS crudo.
- `UBICACION_PASEO`: qué hizo el filtro con cada coincidencia.
- `DEPURACION_TRACKING`, `DEPURACION_CAMARA`, `DEPURACION_GPS`, `DEPURACION_AJUSTE`: cambios de tracking, configuración de
  la cámara, estado del GPS y las pruebas de la hoja.
- `ESTADO_PASEO` cada 10 s: batería, temperatura (`termico`, `margen`), intentos, fallos y precisión del GPS.
- Desde 1.3.2: `DEPURACION_POSE` (la cámara cada segundo y las cajas a la vista), `DEPURACION_RENDIMIENTO`,
  `DEPURACION_CAJA`, `DEPURACION_UI`, `DEPURACION_APP` y `DEPURACION_FOTO`, más una foto chica de lo que veía la cámara cada
  3 s en `imagenes/`.

## Qué reportar

Por edificio: segundos hasta la caja, si quedó en su lugar y desde dónde se probó. Y, sobre todo, el archivo del registro.
