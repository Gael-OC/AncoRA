# AncoRA Paseo 1.1.0 — prueba en campus

Versión de pruebas del 13 de octubre de 2026. Paquete Android `com.ancora.ucnar.paseo`, código de versión `2`.
El APK contiene nueve mapas y cuatro edificios. La localización y la alineación física en el Xiaomi 15T todavía requieren esta prueba.

## Qué cambió

- Se incorporan **152257 de Ciencias Básicas** y **152259 de Teología**, desde la carpeta compartida de Drive. Sus mapas cargan y sus registros contra el dron son suficientemente diferenciados para preparar una prueba.
- Las cajas nuevas se expresan en el marco de cada mapa. Ciencias Básicas conserva el ajuste realizado en terreno en 152192; las posiciones y tamaños de los siete mapas anteriores no cambian.
- Se mantiene el filtro de pose: requiere dos ubicaciones consistentes antes de mostrar una caja.
- **Depth es opcional.** Esta versión no usa oclusión; no hace falta un teléfono con ese soporte adicional. ARCore sigue siendo necesario.
- El HUD cuenta intentos y fallos globales de localización, y éxitos de cada mapa. Si el SDK arranca después del aviso de los 15 segundos, el error se limpia. Un teléfono sin ARCore muestra la causa de compatibilidad aunque el SDK también haya agotado su tiempo de inicio.

No se incorporan las capturas pequeñas ni los registros ambiguos. `152254`, `152255`, `152256`, `152237` y `152238` requieren comprobaciones adicionales; agregarlos sin una caja fiable podría provocar saltos entre mapas.

| Edificio | Mapas del APK | Caja, ancho × alto × fondo |
|---|---|---|
| Ciencias Básicas | 152192, 152196, **152257** | 19,9 × 7,4 × 10,7 m |
| X1 | 152198, 152200 | 27,6 × 4,5 × 14,0 m |
| Teología | 152195, 152241, **152259** | 22,3 × 8,3 × 10,7 m |
| EIC | 152242 | 15,1 × 9,3 × 18,7 m; únicamente el bloque |

## Instalar en cada teléfono

1. Usar un teléfono ARM64 con Android 14 o superior. Instalar/actualizar **Servicios de Google Play para RA** desde Play Store. Si no está disponible para el modelo, comprobar esa compatibilidad antes del recorrido.
2. Descargar el APK del release `paseo-2026-10-13` e instalarlo. Android puede pedir habilitar la instalación desde la app que lo descargó.
3. Abrir **AncoRA Paseo** manualmente y permitir la cámara. Immersal Mapper no es necesario para usarla; los mapas ya vienen dentro del APK.

El Xiaomi 15T no figura con ese nombre en la [lista oficial de ARCore consultada](https://developers.google.com/ar/devices); su funcionamiento exacto queda por comprobar en el equipo. Quitar Depth obligatorio elimina una restricción adicional, pero no sustituye ARCore. La app no necesita una API key Geospatial ni descargar mapas de Drive durante el recorrido.

Si una versión anterior se instaló desde otro computador, puede haber una firma diferente y Android rechazará la actualización. **No desinstalar antes de exportar los ajustes del teléfono:** la desinstalación los borra. En ese caso, abrir la versión anterior, activar el panel del equipo, usar «Copiar valores» y conservar su JSON antes de resolver la instalación.

## Comprobación inicial

Dar cinco toques rápidos en la esquina superior izquierda para mostrar el HUD del equipo. Comprobar:

- `v1.1.0` y `ARSession: SessionTracking`.
- `SDK: listo` y **nueve mapas cargados**.
- Ninguna línea `NO CARGÓ`, ninguna caja «SIN COLOCAR» ni error persistente de inicio.

Sin reconocimiento de edificios, la cámara debería mostrar el entorno y el banner pedir apuntar o acercarse. No usar como tasa por edificio el total de fallos globales: un intento sin coincidencia no identifica qué edificio estaba mirando.

## Recorrido

1. **Ciencias Básicas:** empezar desde el sector fotografiado. Caminar despacio con vistas solapadas de la fachada. Comprobar caja y nombre; recorrer varios puntos hasta observar 152192, 152196 y el nuevo 152257 en el HUD.
2. **X1:** repetir con 152198 y 152200. Anotar cualquier salto de posición o giro al cambiar el mapa visible.
3. **Teología:** repetir con 152195, 152241 y el nuevo 152259. Prestar atención a altura, giro y continuidad entre mapas.
4. **EIC:** comprobar el bloque y especialmente la altura respecto del suelo. El ala larga no está representada. Las capturas del otro lado y las fotos faltantes del dron siguen pendientes.

En cada parada registrar el tiempo hasta el primer éxito y hasta que aparezca la caja estable. El filtro exige dos poses consistentes; un único éxito puede no mostrarla.

Con dos edificios visibles, localizar uno y después el otro: el segundo no debe mover la caja del primero. En edificios con varios mapas debe verse una sola caja, sin duplicados ni saltos apreciables. Probar también llevar la app a segundo plano, volver, perder y recuperar tracking: las cajas se ocultan y reaparecen tras nuevas poses.

Mantener una sesión de unos diez minutos para observar fluidez y calentamiento. La prueba en computador no mide estos aspectos ni la precisión real.

## Ajustar y conservar resultados

Con el HUD abierto, tocar **«Ajuste: Caja»**. El panel edita el edificio del último mapa localizado. Guardar los ajustes relevantes, usar **«Copiar valores»** y compartir el JSON con el equipo. La corrección se lleva a los otros mapas ya colocados del mismo edificio.

Reiniciar la app y comprobar que el ajuste persiste. Para repartirlo en otro APK, colocar el JSON en `Assets/AncoRA/Paseo/ajuste-campo.json`, ejecutar `AncorRA.Editor.PaseoSetup.ApplyFieldAdjustment` y volver a validar/compilar. No pulsar «Restaurar escena» si se quiere conservar el ajuste: esa acción borra los valores del teléfono.

| Teléfono / Android | Edificio / ID visible | Lugar y luz | Tiempo hasta caja | Salto entre mapas / error visual | Resultado |
|---|---|---|---|---|---|
| | | | | | |

## Validación en computador

- Unity 6000.6.0f1: **76 pruebas EditMode aprobadas**, sin fallos ni omisiones.
- Escena: nueve mapas, nueve XR Spaces, nueve filtros y nueve cajas; datos y referencias verificados.
- Carga nativa: los nueve mapas cargan en el plugin del Editor.
- Prueba de humo: regeneración, validación, carga y transferencia de ajustes verificadas con mapas de ejemplo.
- Android: APK de desarrollo IL2CPP/ARM64, firmado y comprobado con herramientas Android; Depth no obligatorio.

Todos estos puntos se comprobaron antes de publicar el release. El APK verificado pesa 68,93 MiB, usa `minSdk 34` y `targetSdk 36`. La evidencia resumida del candidato se conserva en `Docs/AI/ValidacionRelease-2026-10-13.json`. No se afirma localización en el Xiaomi ni alineación física antes del recorrido.
