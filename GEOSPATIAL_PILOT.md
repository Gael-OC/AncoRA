# ANCoRA — plan del piloto Geospatial

## Decisión técnica

La prueba anterior con una imagen de referencia no representa una experiencia vendible para este
caso. El cartel debe ocupar una parte importante de la cámara para detectarse por primera vez; a la
distancia desde la que se ve la casa completa ocupa muy pocos píxeles. Además, una pose estimada
desde un solo plano cercano amplifica los errores de posición y orientación al cubrir un volumen de
aproximadamente 18 metros.

El siguiente experimento debe usar **ARCore Geospatial + VPS** para localizar el teléfono desde el
punto de observación, y un ancla **Terrain** levantada una sola vez por el equipo para ubicar el
volumen de la casa. El cliente no debe calibrar, acercarse al cartel ni caminar hacia atrás.

Esto es un piloto, no una promesa de precisión. Street View hace plausible la cobertura visual, pero
la disponibilidad real se debe consultar con la API de VPS en la coordenada. La precisión que informa
el SDK tampoco reemplaza medir el error visual sobre la casa.

El volumen provisional usa una sola ancla en el centro para aislar el error de localización. Con una
huella de 18 x 10 m, las esquinas quedan a unos 10,3 m del centro, algo por encima de la recomendación
general de ARCore de mantener el contenido a menos de 8 m del ancla. No se agregan anclas por reflejo:
si el levantamiento confirma ese tamaño y la prueba muestra rotación en los extremos, la malla final
se dividirá en bloques con referencias cercanas y se medirá que las uniones no salten.

Referencias oficiales:

- [Comprobar disponibilidad de VPS](https://developers.google.com/ar/develop/unity-arf/geospatial/check-vps-availability)
- [Obtener y evaluar la pose Geospatial](https://developers.google.com/ar/develop/unity-arf/geospatial/obtain-device-pose)
- [Anclas Terrain, Rooftop y Streetscape](https://developers.google.com/ar/develop/unity-arf/geospatial/anchors)
- [Buenas prácticas y distancia a las anclas](https://developers.google.com/ar/develop/anchors)
- [Configurar Geospatial para Android](https://developers.google.com/ar/develop/unity-arf/geospatial/enable-android)

## Qué contiene este piloto

- Una escena mínima sin la plantilla Mobile AR ni el experimento de Augmented Images.
- Consulta de VPS antes de intentar ubicar contenido.
- Solicitud explícita de ubicación precisa.
- Espera de `EarthTrackingState.Tracking`, seguida inmediatamente por la resolución Terrain o
  Rooftop. La resolución del ancla no depende del umbral final de calidad.
- Tres niveles separados: diagnóstico H <= 20 m y yaw <= 25 grados; previsualización H <= 5 m,
  V <= 10 m y yaw <= 10 grados durante 1 s; candidato final H <= 1 m, V <= 2 m y yaw <= 1,5 grados
  durante 2 s.
- El plazo de 180 s solo levanta una advertencia: no borra la última pose ni detiene el diagnóstico.
- Modo forzado `POSE NO CONFIABLE` para mostrar el volumen una vez resuelta el ancla aunque la pose
  no alcance Preview.
- Volumen translúcido de 18 x 5 x 10 m, con techo y aristas, para medir alineación.
- HUD continuo con ARSession, EarthState, EarthTrackingState, H/V/yaw, estado de la promesa y del
  ancla, distancia/rumbo, flecha previa al volumen, controles de ajuste y JSON completo.
- Cada intento tiene `runNumber` y `runId` propios. `Reintentar` cancela las promesas pendientes y
  limpia pose, temporizadores, modo forzado y eventos del intento anterior.
- La consulta VPS expone `Pending`, `Done` o `Cancelled` y el tiempo transcurrido, sin imponer un
  timeout destructivo.
- El GPS previo a Earth informa precisión horizontal, antigüedad de la muestra y si está obsoleta.
- El volumen diagnóstico usa URP Unlit para no depender de luces ni de estimación lumínica.

La latitud y longitud copiadas por el HUD son la **posición de la cámara**. Sirven para registrar la
prueba; no deben pegarse como coordenada del centro de la casa.

## Preparación única del sitio

1. Obtener un pin exacto del punto de anclaje de la casa en Google Maps. Para el primer ensayo se
   recomienda un punto al nivel del suelo en el centro de la huella y un ancla Terrain.
2. Medir el rumbo de la fachada respecto del norte verdadero. No usar el rumbo instantáneo del
   teléfono.
3. Confirmar ancho, alto total y profundidad. Los 18 x 5 x 10 m actuales son provisionales.
4. Escribir esos datos en `Assets/Settings/HouseGeospatialSite.asset`.
5. Activar ARCore API en Google Cloud.
6. Configurar autenticación keyless Android para el paquete `com.ancora.ucnar` y el SHA-1 del
   certificado con que se firma el APK.
7. Restringir la credencial al paquete y certificado del proyecto.
8. Antes de una entrega externa, agregar el consentimiento de uso de datos exigido por ARCore y
   publicar/enlazar la política de privacidad del producto. El permiso nativo de ubicación no
   sustituye ese aviso.
9. En `Project Settings > Player > Other Settings`, usar `Active Input Handling = Both`: el pose
   driver usa Input System y `Input.location`/brújula necesitan el Input Manager legado.

Este levantamiento lo hace el equipo una vez por ubicación. No forma parte del flujo del cliente.

El APK de diagnóstico generado el 10-09-2026 está firmado con el certificado Android Debug cuya
huella SHA-1 es `1C:0C:66:D0:98:2D:DB:C9:1E:22:D5:26:09:F1:CF:1E:CC:F7:9C:A5`. Esta huella sirve
solo para probar ese APK. Al crear una firma de release hay que registrar también la huella del
keystore de producción y dejar de depender de la firma debug.

## Experiencia objetivo del cliente

1. La app confirma que está cerca del sitio configurado.
2. El usuario se ubica en un punto seguro desde el que se ve la estructura y apunta hacia ella.
3. La interfaz le pide mover el teléfono suavemente unos grados para obtener rasgos visuales; no le
   pide caminar hasta la casa.
4. El volumen aparece normalmente al alcanzar Preview. En el piloto, el operador puede forzarlo con
   una advertencia inequívoca para separar un problema de calidad de pose de uno de colocación.
5. Si la calidad no llega, la app sigue mostrando la última evidencia útil. Nunca presenta una pose
   forzada como anclaje aprobado.

## Diagnóstico del primer ensayo iOS

El build exportado en `Builds/iOS` contenía la máquina de estados antigua. El C++ generado por
IL2CPP conserva un timeout de 30 s y evalúa `PosePassesQuality` antes de ejecutar
`ResolveAnchorOnTerrainAsync`. Por tanto, el texto `VPS disponible` solo demostraba que la consulta
de cobertura había terminado: si H <= 1 m, V <= 2 m y yaw <= 1,5 grados no se mantenían durante la
ventana estable, nunca se solicitaba el Terrain Anchor y nunca se construía el volumen.

La revisión diagnóstica desacopla las etapas: primero exige únicamente Earth Tracking, luego resuelve
Terrain y después clasifica la pose como Diagnostic, Preview o Final. Esto permite observar por
separado tracking, resolución del servicio, colocación y render.

## Protocolo de terreno

Para el siguiente ensayo, verificar primero que el HUD diga `geospatial-diagnostic-v3`. En cada
equipo:

1. Con ubicación precisa habilitada y datos móviles activos, abrir desde el punto donde se ve la
   estructura completa. No acercarse a la casa.
2. Grabar la pantalla desde antes de abrir la app y mover el teléfono lentamente unos 30 grados a
   cada lado, manteniendo visibles rasgos del entorno usados por Street View.
3. Esperar hasta ver `EarthTrackingState: Tracking` o hasta 180 s. Copiar el JSON aun si nunca llega.
4. Si Tracking aparece, observar `Terrain: Pending` y esperar `Terrain: Success` o un error concreto.
5. Si Terrain es Success pero el bloque no aparece, pulsar `Mostrar bloque: POSE NO CONFIABLE` y
   copiar otro JSON. No ajustar todavía.
6. Con el bloque visible, comprobar la flecha y la línea `Ancla AR`: distancia, delta vertical y si
   queda delante o detrás de la cámara. Tomar una captura donde también se vea el HUD.
7. Solo entonces abrir `Ajustar bloque`; corregir X/Y/Z, giro y tamaño sin cambiar latitud/longitud a
   ciegas. Copiar `diagnóstico + ajuste` al terminar.
8. Mantener el teléfono 60 s, desplazarse unos 5 m lateralmente mirando la casa y registrar deriva.
9. Repetir 10 arranques fríos y desde uno o dos puntos cercanos permitidos.

Si falla, devolver el JSON completo de los pasos 3, 5 o 7 (según hasta dónde avanzó), una captura del
HUD y la grabación de pantalla. Los campos decisivos son `earthState`, `earthTrackingState`,
`horizontalAccuracy`, `verticalAccuracy`, `yawAccuracy`, `terrainAnchorStatus`,
`anchorPromiseState`, `anchorTrackingState`, `worldDistanceToAnchorMeters`,
`anchorVerticalOffsetMeters`, `anchorForwardDot`, `contentVisible`, `contentRendererIsVisible`, los
shaders y `events`. Para separar intentos, incluir también `runId`, `vpsPromiseState`,
`vpsCheckElapsedSeconds`, `gpsHorizontalAccuracyMeters`, `gpsAgeSeconds` y `gpsSampleStale`.

### Criterio de aprobación para el cubo

- Mediana de localización <= 10 s y percentil 95 <= 20 s.
- Al menos 9 de 10 arranques completan sin acercarse a la casa.
- Error visual máximo <= 0.5 m en las cuatro referencias de la fachada.
- Deriva <= 0.25 m durante 60 s y sin saltos perceptibles al desplazarse lateralmente.

Estos son objetivos de producto para la prueba, no garantías del SDK. Para “desaparecer” el edificio
la tolerancia probablemente tendrá que bajar a unos 0.2–0.3 m en el contorno visible.

## Árbol de decisión después de medir

### A. VPS + Terrain cumple

Se conserva esta arquitectura. Se reemplazan las medidas provisionales por un levantamiento preciso
y se crea un perfil versionado por sitio. Es el camino de menor fricción para el cliente.

### B. VPS localiza rápido, pero deja un sesgo repetible

Se mantiene VPS como referencia global y se agrega un refinamiento por sitio. El equipo registra una
corrección de posición y rumbo una sola vez, o ajusta el ancla contra Streetscape Geometry cuando la
fachada esté disponible. El cliente sigue sin calibrar.

### C. VPS no está disponible o el error cambia entre sesiones

No conviene insistir con GPS solo: el error métrico y de orientación no sirve para cubrir una casa.
El siguiente paso es un localizador visual propio del sitio:

- capturar muchas imágenes desde las zonas normales de observación;
- reconstruir puntos 3D y asociarlos a coordenadas conocidas;
- reconocer rasgos naturales de toda la fachada/cerro, no un cartel único;
- estimar la cámara con correspondencias 2D–3D y fusionarla con el tracking inercial de ARKit/ARCore;
- conservar VPS/GPS únicamente para elegir el mapa correcto y dar una pose inicial.

Esta alternativa usa más almacenamiento y cómputo, pero elimina el marcador físico y puede ofrecer
mejor alineación local. Solo se justifica si las mediciones demuestran que VPS no alcanza.

## Lo que no se recomienda

- GPS como ancla final: es útil para llegar al sitio, no para alinear bordes de un edificio.
- Cloud Anchor como reemplazo directo: alguien tendría que crear/actualizar la referencia in situ y
  no resuelve por sí solo la localización inicial desde lejos.
- Un cartel o QR gigante: condiciona el sitio y repite el problema actual.
- Mostrar el contenido apenas AR diga “Tracking”: hay que respetar las precisiones Geospatial y el
  resultado visual medido.
- Ajuste manual por cada cliente: es lento y no es reproducible.

## Camino hacia “desaparecer” la estructura

El cubo valida únicamente el registro espacial. La fase final necesita además:

1. una malla georreferenciada del cerro original, extendida más allá del contorno del edificio;
2. cámara/material del fondo coherente con iluminación y color del día;
3. una máscara/mesh de oclusión de la estructura;
4. preservación de personas, vegetación y objetos que pasen por delante;
5. pruebas de paralaje desde la zona de movimiento permitida.

La secuencia correcta es: precisión del ancla, volumen simple, medición de error, refinamiento si
hace falta y recién después reconstrucción visual del cerro.

## Estado actual

- Escena limpia y piloto Geospatial: listos.
- La revisión diagnóstica compila en Unity 6000.6.0f1. El export iOS `Builds/iOS_Diagnostic` anterior
  ya contiene la máquina desacoplada; hay que volver a exportar para incorporar el identificador v2
  y los campos de diagnóstico de posición/render agregados después.
- Posición inicial de prueba: ancla Terrain 22 m desde el panorama indicado, en rumbo 301,168°:
  `-29.96410422, -71.34963299`. La fachada queda orientada inicialmente hacia el punto de cámara
  (`121,1683°`). Es una estimación para encontrar el bloque, no un levantamiento definitivo.
- Dimensiones iniciales: 18 x 5 x 10 m. El HUD permite ajustar posición, giro y dimensiones en el
  teléfono, y copiar un JSON con el resultado para fijarlo después en el perfil del sitio.
- Coordenadas/rumbos/dimensiones definitivos del sitio: pendientes de la prueba visual o medición.
- Google Cloud/API: la credencial usada en el ensayo funcionó; no se atribuye el fallo a autorización.
- Validación física del VPS y del error: pendiente en terreno.
- iOS Support, Geospatial y ARKit están habilitados. Android conserva ARCore y el mismo flujo de
  diagnóstico.
