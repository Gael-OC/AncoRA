# AncoRA Paseo 1.4.0 — guía de prueba en terreno (antes del viernes 30-oct)

Dos APK, mismo paquete y misma firma (se instala uno encima del otro con `adb install -r`, sin desinstalar):

| APK | Qué es |
|---|---|
| `Builds/Android/AncoRAPaseo.apk` (= `AncoRAPaseo-1.4.0-campus.apk`) | **Modo campus**: un solo espacio con los 11 mapas y las 4 cajas |
| `Builds/Android/AncoRAPaseo-1.4.0-por-edificio.apk` | Respaldo: igual que la 1.3.9 (un espacio por edificio), con versión 1.4.0 |

SHA-256: ver el final de esta guía. Los APK de este computador (PC de escritorio) están firmados con su keystore: si el teléfono
tiene una versión hecha en el Mac, `INSTALL_FAILED_UPDATE_INCOMPATIBLE` → desinstalar primero (los ajustes guardados se
pierden igual: la 1.4.0 usa el prefijo `v3` y no lee los de antes).

## Qué cambió desde la 1.3.9

1. **Los 4 edificios en un solo marco.** El vuelo del dron del 28-oct (234 fotos, un solo modelo de RealityScan) puso las nubes
   viejas de cada edificio en un marco común. Ubicarse con **cualquier** mapa muestra **las 4 cajas**, cada una en su edificio,
   aunque ese edificio no tenga mapa de la cara que se mira. Comprobado en el computador: los 11 mapas, con la transformación
   que usa la escena, caen sobre la nube del dron (54–89 % de puntos a < 25 cm). **No está medido en terreno.**
2. **Primera ubicación más exigente.** Como una ubicación mala movería las 4 cajas, una primera ubicación con confianza < 20
   no se muestra sola: espera una segunda que coincida (≤ 0,6 m). En el registro sale `veredicto=pendiente`. En los registros
   del 21–23 oct, 6 de cada 10 primeras ubicaciones tenían confianza < 20: puede tardar unos segundos más en aparecer.
3. Cada ubicación se juzga en la caja del edificio cuyo mapa ubicó (antes, en la única caja del espacio).
4. `AndroidMinSdkVersion` 26 (de la 1.3.9, sin probar aún en Android 12).

## Prueba (30 min, con luz pareja si se puede)

Reglas de siempre: teléfono a la altura del pecho, quieto 30–40 s por punto, a 15–35 m. Anotar hora, clima y teléfono.

1. **Arranque.** Abrir la app, esperar «listo». En el registro (hoja del equipo → Compartir registro) la línea de inicio
   debe decir `modo campus (un solo XR Space para todos los edificios)`.
2. **Ubicar en una cara con mapa** (la mejor: X1 frente SE, o Ciencias Básicas frente SO, o Teología frente NO).
   - ¿Aparecen las 4 cajas? ¿Cuánto tardó?
   - **Lo importante:** mirar cada uno de los otros 3 edificios **sin que se vuelva a ubicar** y anotar cuánto se sale su caja
     (de lado, adelante/atrás, arriba/abajo, giro), con una foto de la pantalla por edificio. Si se sale más de ~1 m, anotar
     cuál y hacia dónde: indica qué edificio quedó mal en el marco común (sobre todo EIC, el de registro más débil).
3. **Caminar** de un edificio a otro (40–80 m) sin tapar la cámara. Las cajas deben seguir en su lugar (ARCore deriva ~1–3 %
   de lo caminado). Al llegar, apuntar a una cara con mapa y ver si la caja se corrige y cuánto salta.
4. **Repetir el paso 2 empezando por otro edificio.** Si las cajas quedan distintas según con qué edificio se ubicó, el marco
   común tiene un error entre esos dos.
5. **Si el modo campus sale mal** (cajas de otros edificios corridas varios metros): instalar
   `AncoRAPaseo-1.4.0-por-edificio.apk`, que se comporta como la 1.3.9, y avisar con las fotos.

## Para corregir después

- Una caja mal puesta en el marco común se corrige con el panel del equipo («Ajuste: Caja» edita la caja **que se está
  mirando**) → «Copiar valores» → `Assets/AncoRA/Paseo/ajuste-campo.json` → `PaseoSetup.ApplyFieldAdjustment`. En modo
  campus el ajuste mueve solo ese edificio y no toca a los demás.
- Para volver a un espacio por edificio sin el APK de respaldo: borrar `campus` de los cuatro `edificio.json` y correr
  `Prepare` + `BuildAndroid`.

## SHA-256

- `AncoRAPaseo.apk` = `AncoRAPaseo-1.4.0-campus.apk`: `62687b47364d4aedd7d50d27f8867115613371f4f43886986984f6346584d7b3`
- `AncoRAPaseo-1.4.0-por-edificio.apk`: `0ee363e682e66e25e15d1a573ae186016d9266d9c126f272a4d94f36dff3f064`

Los dos: `com.ancora.ucnar.paseo`, versión 1.4.0, código 22, Android 8 o más nuevo (SDK 26), build de desarrollo, firmados en
el PC de escritorio.

