# Próxima prueba útil de AncoRA — construcción de Teología

**Objetivo:** obtener una cadena de vistas que permita conectar las partes de fachada que quedaron ambiguas.
Las pruebas anteriores de Gael ya se hicieron offline. No se atribuye la inestabilidad geométrica a falta de Internet.

APK: `Builds/Android/AncoRA-Construccion-Offline.apk`. El APK de capturas anterior se conserva.
Este build añade un recorrido de construcción; todavía no integra XFeat/ALIKED como localizador Android.
Las cajas de la demo siguen correspondiendo al localizador actual y no son el resultado del nuevo experimento.

## Qué cambia en el grabador

- «Grabar recorrido de construcción (60 s)»: hasta 60 segundos, objetivo de 2 imágenes por segundo a resolución
  original. La cadencia real puede ser menor. Se usa una sola carpeta y época de tracking por recorrido.
- La hoja Equipo se cierra para ver la cámara; aparece tiempo, imágenes guardadas, omitidas y botón «Detener captura
  y guardar». Se puede detener antes del límite. Se pausa el localizador durante la captura y el guardado.
- Tras detener, espera las escrituras pendientes antes de permitir copiar. Se muestra el resultado y se vuelve a Equipo.
  Una última escritura fallida se informa aunque la captura ya se haya detenido.
- Se conservan los barridos de cinco segundos para consultas. Los recorridos se distinguen por carpeta `construccion-…`
  y `tipo_captura: construccion-arcore`; los barridos conservan `barrido-…` y `barrido-arcore`.
- Pérdida de tracking, pausa, espacio insuficiente o errores detienen la captura. Las poses siguen declaradas como
  no validadas ópticamente. Ni el recorrido ni su etiqueta de distancia acreditan escala métrica.

## Antes de ir al campus: una comprobación corta

1. Instalar el APK y abrir AncoRA Paseo manualmente. Usa el mismo identificador que la demo: instalar encima conserva
   los ajustes si coincide la firma. Si Android informa firma incompatible, no desinstalar ni borrar datos: conservar
   las grabaciones y comunicar el mensaje para resolverlo.
2. Puedes mantener modo avión. Pulsar Comenzar, permitir cámara y esperar seguimiento AR.
3. Abrir Equipo con cinco toques en la esquina superior izquierda; entrar a Grabar. Dejar apagado el interruptor de
   grabación por intentos. Elegir edificio, punto, distancia y luz para habilitar la captura.
4. Pulsar el recorrido, grabar unos segundos y detener. Comprobar que el contador avanzó, acabó el guardado y no hay
   error. Copiar a Descargas y revisar que la carpeta contiene imágenes y `intentos.jsonl`.

Esa comprobación en casa solo prueba captura y exportación: no sirve para reconstruir Teología.

## En Teología: dos recorridos y tres consultas

Teléfonos disponibles: **Xiaomi 14 para construir** y **Xiaomi 15T para las tres consultas de prueba**.
No hace falta Samsung ni recompilar el APK. Se separan los roles para comprobar después el reconocimiento desde
otro teléfono. No hay que sincronizar los dos teléfonos ni hacer coincidir sus orígenes ARCore.

**No esperar a que se alinee la caja.** Este modo graba datos y no alinea el edificio. Solo requiere seguimiento
de ARCore; el propio botón informa si aún falta. Al pulsarlo puede aparecer «Preparando» mientras termina un intento
anterior. Empezar a caminar cuando aparezca «grabando» y el contador haya guardado al menos una imagen.
El localizador se pausa durante la captura; una caja que siga visible no es una comprobación de esta prueba.

En construcción se mueve **la persona con el teléfono**. Sujetarlo vertical, a la altura del pecho, sin cambiar zoom.
Caminar aproximadamente un paso corto por segundo; girar suavemente el teléfono solo para mantener la fachada en
imagen. Girar solo el teléfono desde una posición fija no sustituye este recorrido.

**Inicio/final:** no hay un punto geográfico obligatorio ni se necesita una alineación previa. Desde la vereda,
escoger un extremo accesible de la fachada, apuntar hacia el edificio y caminar paralelo hasta el otro extremo.
Si se puede continuar junto a la esquina, hacerlo manteniendo ambas caras visibles en las vistas de transición.
No se presupone que todos los lados sean accesibles: no cruzar barandas ni abandonar la vereda para completar la ruta.

1. **Xiaomi 14, construcción A, ida:** seleccionar TEO, punto A, luz real y distancia de referencia del recorrido. Esa distancia
   es una etiqueta de contexto, no una medida constante ni un control de escala. Pulsar «Grabar recorrido de construcción».
   Caminar lentamente por la vereda mirando la fachada, con detalles compartidos entre vistas sucesivas. Cubrir el
   frente y las posiciones intermedias que conectan las vistas A/B anteriores. Al pasar la esquina, incluir detalles
   de ambas caras simultáneamente. Mantener fachada y aleros en imagen; evitar llenar el encuadre de suelo/arbustos.
2. Detener al terminar, o dejar que finalice a los 60 s. Si falta recorrido, comenzar otro desde prácticamente la misma
   posición para mantener el solapamiento. Si se interrumpe el tracking, guardar esa carpeta y repetir el tramo; no
   continuar suponiendo que se conservó el mismo origen.
3. **Xiaomi 14, construcción B, vuelta:** elegir punto B y repetir en sentido inverso, procurando una posición lateral distinta
   si la vereda lo permite. Usar únicamente zonas accesibles, sin cruzar barandas. El recorrido completo es importante;
   no limitarse a girar sobre un punto ni a unos pocos pasos delante de la misma ventana.
4. **Xiaomi 15T, consultas C/D/E:** elegir tres posiciones de la misma vereda: hacia un extremo de la fachada (C),
   frente a su zona central (D) y hacia el otro extremo o esquina accesible (E). En cada una quedarse quieto y usar
   «Grabar barrido de 5 segundos», moviendo suavemente la cámara por una parte de la fachada. Para estos barridos
   no caminar. Elegir explícitamente la distancia de referencia correspondiente; el valor automático del punto no
   es una medición. Estas consultas no se incorporarán al mapa para conseguir éxito. Pueden grabarse mientras el
   otro teléfono hace construcción, procurando no taparse mutuamente la fachada.
5. Esperar que termine el guardado, pulsar «Copiar grabaciones a Descargas» y esperar el final de la copia sin fallos.

No se exige medir ahora todos los teléfonos, luces, edificios y distancias del ensayo final. Esta prueba resuelve
primero la conexión geométrica de un edificio. Para aceptar después un mapa métrico harán falta controles físicos
medidos y comprobaciones de cámara; esta sesión por sí sola no demostrará precisión en centímetros.

## Qué enviar

### Aprovechar la misma salida: Ciencias Básicas y X1

Orden de prioridad: **Teología → Ciencias Básicas → X1**. Antes de pasar al siguiente, comprobar que avanzaron los
contadores, terminó el guardado y la copia no informa fallos. La falta de Samsung no limita este protocolo.

Si hay tiempo para los tres, repetir exactamente el reparto de teléfonos y las cinco capturas por edificio:

| Edificio elegido en la app | Xiaomi 14: construcción | Xiaomi 15T: consultas separadas |
|---|---|---|
| TEO | A ida, B vuelta, hasta 60 s cada una | C extremo, D centro, E otro extremo/esquina, 5 s cada una |
| CB | A ida, B vuelta, hasta 60 s cada una | C extremo, D centro, E otro extremo/esquina, 5 s cada una |
| X1 | A ida, B vuelta, hasta 60 s cada una | C extremo, D centro, E otro extremo/esquina, 5 s cada una |

Cambiar el edificio seleccionado **antes** de grabar; las letras se reutilizan dentro de cada edificio. Elegir la
distancia de referencia de cada captura explícitamente, sin asumir que el valor automático del punto es correcto.
Usar la fachada desde cuya vereda se pretende utilizar la app y el recorrido accesible más completo que la muestre.
Mantener cámara, zoom y encuadre de fachada como en Teología. No hace falta repetir todas las distancias y luces ahora.

Son **15 carpetas principales** si cada recorrido cabe en una captura: seis de construcción y nueve de consulta.
Conservar además continuaciones e interrupciones. Si no alcanza el tiempo, la sesión completa de Teología tiene
prioridad sobre dejar tres edificios a medias.

Las capturas adicionales permitirán intentar mapas de CB/X1 y diagnosticar confusiones entre edificios cuando haya
geometría válida. Una consulta de CB/X1 solo puede tratarse como negativa frente a un mapa de Teología si no contiene
también detalles mapeados de Teología; revisar el encuadre y anotar si aparece otro edificio. No se consideran por sí
solas una prueba final ciega ni sustituyen controles físicos. El análisis seguirá cerrando primero el piloto de Teología.

Comprimir las **carpetas completas** de esta sesión dentro de `Descargas/AncoRA/grabaciones/`: recorridos
`construccion-…` y consultas `barrido-…`, cada uno con imágenes e `intentos.jsonl`. No enviar solo fotos sueltas.
«Compartir registro» es un envío distinto y no incluye esas imágenes; enviar también ese registro si hubo interrupciones.
Añadir teléfono usado y una descripción breve del recorrido; una foto indicando inicio/final también ayuda.
Si cada recorrido de construcción cabe en 60 s, lo esperado son cinco carpetas principales: dos `construccion-…`
del Xiaomi 14 y tres `barrido-…` del Xiaomi 15T. Conservar también continuaciones o capturas interrumpidas si las hubo.

## Qué se hará con esa prueba

1. Auditar imágenes/filas, tiempos, seguimiento, cadencia y separación entre construcción y consultas.
2. Seleccionar vistas de los recorridos y reconstruir con XFeat; contrastar con ALIKED. Comprobar fachada,
   vegetación, ventanas repetidas y estabilidad de la conexión entre sectores.
3. Comparar con los resultados anteriores. Una mejora debe sostenerse al quitar el primer plano y cambiar condiciones.
4. Si la geometría se sostiene, preparar controles de escala/calibración y la evaluación real de localización.
   Si no, identificar el enlace concreto que falla antes de pedir una repetición general.

La compilación y las pruebas de software no validan la cámara ni la interfaz en el teléfono. La comprobación corta
de arriba es la primera validación de este nuevo modo en hardware; no se ha demostrado aún 10–20 cm ni arranque ≤5 s.

## Validación del APK entregado, 5-nov-2026

- Unity 6000.6.0f1, EditMode `AncorRA.Tests`, sin gráficos: **514 aprobadas, cero fallos, una omitida**. La omitida
  verifica recorte de fragmentos de oclusión y ya requería ejecución con gráficos. Se comprobaron bloqueo de copia y
  selección durante captura/guardado, parada con escrituras pendientes, error del último guardado después de detener,
  metadatos de ambos modos y restauración del localizador tras rechazar cualquiera de las dos capturas.
- Build Android completado con código 0 mediante `AncorRA.Editor.PaseoSetup.BuildConstructionCaptureAndroid`.
  APK de desarrollo, ARM64, IL2CPP, mínimo Android API 26, objetivo API 36, **92,7 MiB**.
- Paquete `com.ancora.ucnar.paseo`, nombre visible AncoRA Paseo, versión heredada 1.5.3 (28). El botón de recorrido
  identifica este build; no se cambió la versión de la demo. Firma verificada y coincidente con el APK de capturas
  anterior de este Mac; una instalación que venga firmada desde otro computador puede diferir.
- CRC del ZIP válido y presencia del modo de construcción comprobada en el binario. SHA-256:
  `ef655eaad4fc147e94d2385083e7db3d8a52dfb405a1f64b1707070a4b0fb543`.
- Sin errores de compilación C#. Avisos de Unity sobre un asset de pruebas en un paquete inmutable y compilación
  de métodos grandes de TextMeshPro, como en el build anterior; no se modificaron esos paquetes para silenciarlos.
- Se conservaron escena, configuración y cambios locales ajenos. Logs y XML:
  `Tools/LocalizacionOffline/salida/20261105/captura-construccion/` (ignorados por Git).
- No se instaló ni se ensayó este nuevo modo en un teléfono durante la preparación del APK. Cadencia, cámara,
  interfaz, seguimiento y exportación Android deben confirmarse con la comprobación corta antes del recorrido.
