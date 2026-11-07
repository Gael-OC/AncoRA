# Grabador de campus — 7 de noviembre de 2026

APK: `Builds/Android/AncoRA-Grabacion-Campus.apk`. Nombre: **AncoRA Capturas**, versión 1.5.4 (29).
Se instala sobre AncoRA Paseo con el mismo identificador y firma de este Mac. **No desinstalar ni borrar datos** para resolver una firma incompatible: conservar grabaciones y ajustes primero.

## Antes de salir: prueba corta obligatoria

1. Instalar el APK y abrirlo manualmente. Pulsar **Abrir grabador**, permitir cámara y, si se desea, ubicación. Esperar **Cámara lista para grabar**. La ubicación es contexto; no se necesita Internet ni una coincidencia de Immersal.
2. Elegir **Prueba en casa** y la luz real. Dejar marcado **Video ARCore + fotos y metadatos**. Pulsar **Grabar esta toma**; esperar a que el contador muestre fotos guardadas antes de moverse. Caminar unos pasos despacio en una habitación con detalles.
3. Al terminar los 10 s, esperar el guardado. La toma debe aparecer completa. Abrir **Comprobar / reproducir una sesión**, elegirla y pulsar **Reproducir sesión**. La cámara debe mostrar la habitación grabada aunque el teléfono apunte ahora a otra parte. **Capturas** permite volver al panel y a cámara en vivo.
4. Pulsar **Copiar TODO a Descargas**. Esperar **Copia terminada** o **Todo estaba copiado**. En Archivos revisar `Descargas/AncoRA/grabaciones/`: una carpeta de toma con MP4, PNG, `intentos.jsonl`, `sesion.json`, `registro.log` y `sha256.txt`, más una carpeta `registros-…` con los registros de la app.
5. Si el video falla, conservar el mensaje. Puede desmarcarse el video y grabar solo PNG + metadatos; ese respaldo sirve para análisis en PC, pero **no sustituye la reproducción de sensores ARCore**. La exportación debe comprobarse también en ese caso.

La compilación y las pruebas de software no comprueban estos pasos en hardware. Gael instalará manualmente; no hubo un teléfono conectado durante la preparación.

## En Teología

### Orientación corregida por Gael, 7-nov

| Cara física | Hacia dónde mira | Referencias |
|---|---|---|
| Frente | X1 | Poste y ventanas largas de aproximadamente 6 m; ventana medida R–S de 6,20 m |
| Lado izquierdo | Ciencias Básicas | Puerta y ventana medida C–D de 2,70 m |
| Lado derecho | EIC | Tercera cara visible del edificio |

La pauta anterior confundía frente y lateral. **El APK 1.5.4 ya entregado conserva el rótulo incorrecto «Lateral · dos franjas de ventanas»: esa toma debe hacerse en el FRENTE hacia X1, donde están el poste y las ventanas largas.** Esta corrección documental no cambia el APK instalado ni exige repetir material que ya muestre la cara indicada.

Para los recorridos de ida y vuelta, conectar lado izquierdo (Ciencias Básicas) → frente (X1) → lado derecho (EIC), y regresar en sentido inverso. Si no cabe en 60 s, repetir el mismo tipo de toma como continuación, empezando un poco antes del final anterior. En cada esquina conservar elementos de ambas caras en las vistas de transición. Los detalles de ventana junto a puerta corresponden al lado izquierdo; los de ventana larga, al frente. Las otras ventanas medidas se reconocen por las referencias fotográficas confirmadas: no reinterpretar sus IDs históricos como orientaciones físicas nuevas.

El selector **Toma** contiene instrucciones y duración; **Siguiente toma** avanza sin borrar lo anterior. El panel muestra qué tomas están completas en el teléfono. Se puede repetir cualquiera y se crea otra carpeta. Las referencias de ventanas son de Teología; CB/X1/EIC están disponibles para recorridos y consultas equivalentes, con prioridad para completar Teología.

| Toma | Propósito | Duración máxima |
|---|---|---:|
| Prueba en casa | Comprobar captura, copia y reproducción; fuera del mapa | 10 s |
| Frente y esquina · ida | Construcción caminando desde la vereda | 60 s |
| Frente y esquina · vuelta | Construcción, otra trayectoria con solapamiento | 60 s |
| Lateral · dos franjas de ventanas | Apoyo dirigido a la fachada discutida, sin el poste delante | 60 s |
| Ventana junto a puerta | Bordes interiores de C–D = 2,70 m | 15 s |
| Primera ventana lateral | Bordes de H–I = 2,70 m y vuelta/frente = 1,95 m | 15 s |
| Ventana larga | Bordes interiores de R–S = 6,20 m | 15 s |
| Consultas 10 / 15 / 25 m | Tres sesiones nuevas, separadas de construcción | 5 s cada una |
| Movimiento y regreso | Desarrollo y estabilidad | 30 s |
| Otro edificio | Negativa: sin Teología visible | 10 s |
| Consulta reservada | Otro ángulo, fuera de ajuste/construcción | 10 s |

Son unos siete minutos de grabación; reservar más tiempo para desplazarse, medir, repetir y copiar. Las distancias del formulario son **aproximadas, solo contexto**: cambiar la seleccionada si corresponde. Los acercamientos de ventanas permiten 3/5 m y no se confunden con las consultas a 10–25 m. No cruzar barandas ni acceder a puntos altos para cumplir la pauta.

Mantener el teléfono vertical y sin zoom, cámara trasera. En construcción, caminar lentamente y conservar detalles comunes entre imágenes; incluir ambas caras al pasar la esquina. En consultas, quedarse en el punto y barrer suavemente. El contador empieza a medir la duración útil al llegar la primera imagen. **Detener captura y guardar** permite acabar antes. Si se pierde tracking o se pausa la app, se conserva la sesión como interrumpida: repetir ese tramo en otra toma.

En **Notas / medidas / punto de inicio**, escribir antes de pulsar **Grabar esta toma**: el texto se guarda al iniciar esa sesión. Registrar inicio/fin y cualquier desvío o sombra. Para los anchos: documentar borde interior exacto, altura de la cinta, instrumento y segunda lectura si se hace. C–D sigue siendo el único ajuste exploratorio; los otros tres anchos siguen reservados. La captura no cambia esa partición ni demuestra precisión de centímetros.

## Entrega: un solo paquete

Pulsar **Copiar TODO a Descargas** al terminar, conservando la app abierta hasta el mensaje final. Incluye todas las sesiones guardadas en el teléfono y los registros disponibles de aperturas anteriores. Las sesiones actuales también contienen una copia del registro al iniciar/finalizar. **No hace falta usar Compartir registro** para estas capturas.

Enviar/copiar al Mac la carpeta **`Descargas/AncoRA/grabaciones` completa**, con sus subcarpetas. Puede comprimirse como archivo, conservando los MP4 originales: no enviarlos como video de WhatsApp ni editarlos/recomprimirlos. No borrar originales del teléfono hasta comprobar recepción e integridad. Las huellas `sha256.txt` permiten comprobar que lo recibido coincide.

La copia no borra originales. Se verifica que los archivos ya copiados sigan en Descargas; si falta uno se vuelve a copiar. Una copia fallida no deja un archivo parcial publicado ni marca éxito. El modo de captura no ofrece un botón de borrado.

## Alcance técnico

- Grabación nativa del paquete ARCore 6.5.0 ya instalado, sin SDK o servicio nuevos. El MP4 guarda entrada de cámara/sensores, no la pantalla ni cajas renderizadas. La reproducción se ejecuta en Android; no convierte el Editor de Unity en un emulador de sensores.
- PNG R8 a resolución CPU original, sin giro/reducción, a objetivo de 2 imágenes/s en construcción y hasta 6/s en otras tomas. La cadencia puede ser menor y se informa de las omitidas. El grabador solicita la configuración disponible más cercana a 1920×1080; los metadatos conservan el tamaño e intrínsecos reales por imagen.
- `sesion.json`: rol, toma, notas, contexto, teléfono/build, inicio/fin con zona horaria, estado, conteos y cierre de video. `intentos.jsonl`: intrínsecos, pose Unity, tiempos, orientación/display, exposición si disponible y GPS si autorizado/disponible.
- La pose se lee en `LateUpdate`; el tiempo de lectura se registra aparte y `timestamp_pose_independiente=false` evita presentarlo como instante óptico medido. El video de sensores permitirá más investigación; **no declara resuelta la sincronía**. Las calibraciones candidatas no se aplican a las imágenes.
- Roles explícitos por fila: construcción, consulta, detalle, ensayo, desarrollo, negativa y reserva. Solo construcción usa `construccion-arcore`; consulta usa `barrido-arcore`. Los demás tipos son específicos y el auditor histórico los mantiene fuera de la selección automática de mapa/consultas.
- SDK/localizador de la demo suspendido en el modo de captura; sin cajas que haya que alinear antes de grabar. La demo normal conserva su flujo.
- Topes por toma: 1 GB entre video y PNG y parada con menos de 400 MB libres. No se evita que muchas tomas ocupen varios GB. Para copiar se requiere espacio adicional en el teléfono; conviene llegar con varios GB libres.
- Durante reproducción no se graban datos ni se exporta. ARCore puede estimar tracking distinto entre reproducciones. El grabador permite comprobar cámara/sensores; la superposición del nuevo motor sigue pendiente de mapa, contrato óptico e integración.

## Reproducir la compilación

Unity cerrado; una sola instancia. `AncorRA.Editor.PaseoSetup.BuildCampusRecorderAndroid` valida la escena y compila con `ANCORA_CAPTURE` solo para ese APK. Nombre/versión/código originales de Player Settings se restauran al terminar. No modifica los modelos, medidas, escena ni particiones del banco.

Validación y evidencia: `output/20261107-grabador-campus/`. Pruebas EditMode con gráficos: **521 aprobadas, cero fallos, cero omitidas**. Compilación Android terminada con código 0; APK de desarrollo **75,7 MiB / 79.363.796 bytes**, ARM64/IL2CPP, mínimo API 26 y objetivo 36. `aapt` confirma AncoRA Capturas 1.5.4 (29), identificador `com.ancora.ucnar.paseo`. Firma válida verificada con `apksigner`; CRC del ZIP correcto y bibliotecas ARCore nativas presentes. La rama compilada de `CaptureMode` se comprobó activa en el código IL2CPP generado. Player Settings restaurados a nombre AncoRA y versión 1.5.3 (28) tras compilar.

SHA-256 del APK: `fb117e06a768fb3458abdb8fee2fb8ae9b2a275bbbacaaf595e236c0a07363ae`.

Avisos de compilación: referencias duplicadas de herramientas del Editor y métodos grandes de TextMeshPro; sin errores C# ni fallos de build. No se instalaron paquetes ni se modificaron para silenciar avisos. **Instalación, grabación, reproducción y exportación en Android siguen pendientes de la prueba manual de Gael.** El MP4 y los conteos no prueban alineación física, sincronía óptica ni funcionamiento del nuevo motor offline.
