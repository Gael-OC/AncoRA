# Prueba de oclusión y cajas — 1.5.3

Base: demo 1.5.2. Gael confirmó que el mapa que funciona bien es **152693**, no 152692. La 1.5.3 usa código Android 28 y conserva los mapas, sus registros y el filtro de pose.

## Comparar oclusión en terreno

1. Abre la app, toca **Comenzar**, abre **Equipo** con cinco toques arriba a la izquierda y elige **152693** en Mapa de Teología. Si cambias de mapa, vuelve a tocar Comenzar.
2. Espera a que aparezcan los edificios. Sin moverte de ese punto, abre **Oclusión · prueba**.
3. **Sin** reproduce la presentación sin oclusión. Es el modo inicial cuando no hay una elección guardada.
4. Prueba **Cercana**, inicialmente a **5 m**. Las opciones **3 / 5 / 8 m** limitan la distancia desde la cámara de los obstáculos aceptados. Si las barandas están más lejos que el límite, no recortarán el modelo.
5. Desplaza suavemente el teléfono de lado para que ARCore estime profundidad. Compara el contorno de barandas y postes, los huecos entre barras y la estabilidad al mover la cámara.
6. **Normal** permite comparar con la profundidad completa suavizada de ARCore, sin el límite cercano ni el filtro por confianza.

El cambio de oclusión no recarga la escena ni reinicia deliberadamente Immersal. Modo y alcance se conservan en el teléfono. Los teléfonos sin Depth siguen mostrando el contenido sin oclusión; Equipo muestra si falta soporte, tracking o profundidad.

### Qué intenta resolver Cercana

La profundidad completa rellena regiones sin medición y puede desdibujar contornos. La prueba cercana usa las imágenes CPU de **Raw Depth y su confianza**, emparejadas por tamaño y timestamp. Conserva únicamente profundidad entre 0,2 m y el alcance elegido, con confianza **≥128/255**. Publica una textura de muestreo puntual, sin interpolar ni expandir la silueta. Un píxel recorta el modelo solo si está al menos 25 cm por delante de su superficie.

La profundidad real automática del fondo está desactivada en Cercana (`NoOcclusion`), de modo que no vuelve a recortar por debajo del filtro. El shader de los modelos y los caminantes descarta los fragmentos aceptados por la máscara; no cambia vértices, escala ni pose. Si faltan imágenes emparejadas, están desfasadas más de 0,5 s o se pierde tracking, no se aplica la máscara cercana de ese frame.

**Limitación:** Raw Depth no cubre todos los píxeles y las barandas delgadas pueden quedar incompletas o parpadear. El filtro intenta reducir recortes falsos; no garantiza contornos perfectos. Si no alcanza para una toma fija, el siguiente paso sería medir/modelar la baranda y usar geometría de oclusión alineada al campus, en vez de seguir expandiendo una máscara incierta.

Referencia: [ARCore Raw Depth](https://developers.google.com/ar/develop/unity-arf/depth/raw-depth), más el código instalado de AR Foundation/ARCore 6.5.0.

## Ajustar las cajas para el video

En **Equipo → Cajas para la demo**:

- **Mostrar cajas** dibuja los bordes de las cajas sobre los modelos, sin relleno encima de las fachadas. Se pueden ocultar para grabar solo los modelos; la elección se guarda en el teléfono.
- Se pueden elegir Ciencias Básicas, Teología y X1. **EIC se mantiene oculto**, según lo indicado por Gael, y no se ofrece para ajustar en esta demo.
- Elige el edificio antes de ajustar. La selección manual permanece fija aunque apuntes hacia otro edificio; **Automático** recupera la selección según el edificio a la vista.
- En **Ajuste de caja**, mueve en los tres ejes, gira y cambia ancho, alto y fondo. Los pasos grueso/medio/fino permiten acercarse y afinar. El modelo y el parque siguen la posición y el tamaño de su caja.
- Los ajustes de pose y tamaño reutilizan el guardado por edificio de la app. **Copiar valores** permite recuperar los valores para hornearlos después; **Restaurar escena** borra los ajustes guardados del teléfono, con la confirmación existente de dos toques.

Las cajas son anotaciones para comprobar el encaje y siguen dibujando sus bordes sobre el modelo y la imagen real. Para evaluar el contorno de la oclusión, ocúltalas. No se muestran antes de localizar ni durante una pérdida de tracking.

## Validación

- **505 pruebas EditMode aprobadas, sin omitidas**, ejecutadas con gráficos. Incluyen el filtrado con filas/píxeles de distinto stride, confianza baja, profundidad faltante, distancia, selección manual de edificio, redimensión y visibilidad de las cajas.
- Una prueba renderiza el shader real con profundidad sintética: comprueba el recorte de primer plano, la conservación del modelo donde faltan datos, la rotación de coordenadas de imagen y la vuelta al modelo completo al apagarlo. Imágenes: `Builds/Oclusion/Shader-cercana.png` y `Shader-sin.png`.
- Resultados: `Builds/Oclusion/Pruebas-EditMode.xml`.
- `Prepare` y `CheckNativeMapsInEditor` completados: escena y configuración válidas, diez mapas cargados. Comparación por nombre de los transforms de los diez mapas y las cuatro cajas: poses y escalas idénticas a las de la escena anterior.
- **APK compilado y verificado:** `Builds/Android/AncoRAPaseo-1.5.3-oclusion.apk`, también `AncoRAPaseo.apk`. Versión 1.5.3, código 28, paquete `com.ancora.ucnar.paseo`, mínimo API 26, ARM64; integridad ZIP y firma APK v2 correctas. Tamaño: 79.282.588 bytes (75,6 MiB). La 1.5.2 permanece en `AncoRAPaseo-1.5.2-demo.apk`.
- No había teléfono conectado por ADB; no se instaló ni se validó captura de profundidad real.
- **Pendiente:** prueba física con las barandas/poste y rendimiento en el teléfono. Las pruebas sintéticas y la compilación no demuestran precisión del Depth ni alineación en terreno.
