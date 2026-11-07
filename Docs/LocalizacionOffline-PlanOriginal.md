# AncoRA offline: priorizar confiabilidad a 10–25 metros

## Recomendación actualizada

**Elegiría una arquitectura de localización visual sobre un mapa 3D propio, usando varias imágenes y el movimiento de ARCore.** Dentro de esa arquitectura compararía métodos clásicos y neuronales sin dar por ganador a ninguno.

Con tus respuestas, los requisitos quedan así:

- Distancia de uso: **10–25 m**.
- Inicio: se acepta un **barrido breve de hasta unos 5 segundos**.
- Alineación: objetivo de **10–20 cm sobre referencias visibles de la fachada**.
- Procesamiento del visitante completamente local; mapas preparados en nuestro PC.
- Marcadores como respaldo.
- Complejidad y mantenimiento asumibles por el equipo.

Esto cambia mi recomendación anterior: **XFeat deja de ser el favorito por ser ligero.** Primero buscaremos confiabilidad y precisión; después comprobaremos qué candidatos caben en el teléfono.

## Por qué esta arquitectura tiene más sentido

El barrido permite combinar detalles de distintas imágenes y comprobar que todos describen una misma ubicación compatible con el movimiento del teléfono. También permite recuperar información cuando una ventana, árbol o persona dificulta una imagen.

Hay evidencia relevante para tu aclaración: un trabajo sobre localización secuencial encontró que, al aprovechar movimiento y varias imágenes, **SIFT podía competir con características aprendidas más modernas en sus experimentos**. Por tanto, no corresponde elegir redes basándose únicamente en comparaciones de fotografías aisladas. [Investigación original](https://research.chalmers.se/publication/522731/file/522731_Fulltext.pdf).

LaMAR también estudia localización y mapeo con dispositivos AR y secuencias reales. Es una referencia útil para diseñar nuestra evaluación, aunque su código de investigación no constituye un SDK Android listo para instalar. [LaMAR](https://github.com/microsoft/lamar-benchmark).

Mi evaluación para AncoRA es:

| Alternativa | Papel recomendado |
|---|---|
| **Características clásicas + geometría + varias imágenes** | Candidato serio. Puede ganar si alcanza la precisión y confiabilidad requeridas. |
| **Características neuronales + geometría + varias imágenes** | Candidato serio. Puede aportar correspondencias más robustas ante cambios de apariencia. |
| **Red que aprende las coordenadas del campus —SCR—** | Segunda línea experimental; aún debemos resolver despliegue móvil y licencias de sus componentes. |
| **SLAM propio completo** | Reservarlo para el caso en que las mediciones identifiquen al seguimiento de ARCore como limitación. |
| **Marcadores medidos** | Respaldo y ayuda de calibración; no garantizan por sí solos la alineación posterior del edificio completo. |
| **SDK comercial offline** | Reduce integración, pero conserva una dependencia que ustedes quieren eliminar. |

**Mi apuesta principal sigue siendo combinar reconocimiento y geometría. Que el reconocimiento sea neuronal o clásico lo decidirán nuestras pruebas.**

## El mapa y la calibración tienen tanta importancia como el algoritmo

Acercarnos a 10–25 m favorece disponer de más detalle visual, pero no elimina los errores de escala, geometría o registro.

Por ejemplo, **0,5° de error angular producen aproximadamente 22 cm de desplazamiento a 25 m**. Una coincidencia visual aparentemente convincente puede resultar insuficiente para el objetivo elegido.

Hay que medir por separado:

1. **Geometría y escala del mapa:** dónde están realmente los puntos de las fachadas.
2. **Localización:** dónde calcula el sistema que está el teléfono.
3. **Contenido virtual:** si su tamaño y colocación representan correctamente el edificio.

El campus actual es una base aprovechable, pero todavía no es una referencia independiente validada a 10–20 cm. El archivo de georreferenciación registra unos 47 cm de residuo mediano respecto del GPS del dron; **eso no equivale directamente al error local del modelo**. [Datos del campus](/Users/gael/Documents/GitHub/AncoRA/Escenas/Campus/Campus-georef.json).

Antes de atribuir una caja corrida al localizador, tendremos que comprobar esos tres componentes.

## Comparación e implementación propuestas

**1. Preparar referencias y capturas propias.** Comenzar con Teología y EIC, que permiten contrastar un caso conocido con otro problemático, y validar después los cuatro edificios. Capturar barridos a 10, 15, 20 y 25 m, desde distintos ángulos y con sol, sombra y nublado.

Las grabaciones anteriores sirven para una primera comparación, pero fueron mayormente estáticas. El nuevo banco debe guardar secuencias con imágenes, intrínsecos, orientación, marcas temporales y poses relativas de ARCore.

**2. Comparar cuatro candidatos sobre el mismo material:**

| Candidato | Qué permite comprobar |
|---|---|
| **SIFT + comparación clásica** | Referencia sin redes. |
| **SIFT + LightGlue** | Cuánto aporta aprender las correspondencias manteniendo el extractor clásico. |
| **ALIKED + LightGlue** | Una cadena de extracción y comparación neuronal. |
| **XFeat + LighterGlue** | Una alternativa neuronal orientada a eficiencia. |

LightGlue dispone de modelos para distintos extractores; las combinaciones deben utilizar pesos compatibles. [LightGlue](https://github.com/cvg/LightGlue), [XFeat y LighterGlue](https://github.com/verlab/accelerated_features).

Cada extractor tendrá sus propios descriptores y asociaciones con puntos 3D. Compartirán fotografías, marco métrico y evaluación; no reutilizaremos descriptores incompatibles entre métodos.

**3. Integrar la información temporal.** Seleccionar imágenes útiles del barrido, verificar correspondencias geométricamente y refinar una transformación del campus compatible con las poses relativas de ARCore. Compararemos esta solución con resolver cada imagen individualmente para medir la mejora real.

**4. Validar en Android antes de elegir.** Llevar los candidatos que superen la evaluación geométrica a Galaxy S25, Xiaomi 14 y Xiaomi 15T. Medir captura, reconocimiento, comparación y cálculo de posición completos. Una demostración rápida en PC no decidirá el motor.

La interfaz interna entregará transformación del campus, instante de captura, calidad geométrica y estado de localización, sin tipos de Immersal. Cuando falte evidencia suficiente, solicitará otro barrido o el marcador de respaldo.

## Criterios para elegir y declarar que funciona

Propongo estos umbrales de aceptación; **son objetivos del ensayo, no resultados obtenidos**:

| Aspecto | Criterio |
|---|---|
| Primera ubicación | ≥95 % de los inicios correctamente ubicados en ≤5 s por teléfono, incluyendo captura y procesamiento desde que AR está listo. |
| Alineación | Desfase mediano ≤10 cm y percentil 95 ≤20 cm sobre referencias de fachada verificadas. |
| Falsas ubicaciones | Cero aceptaciones incorrectas en al menos 1.000 secuencias negativas de evaluación. |
| Estabilidad | Mantener la alineación al mover el teléfono; comprobar recuperación tras tapar la cámara o perder seguimiento. |
| Independencia | Funcionar desde una instalación de prueba sin conexión y reconstruir mapas con herramientas conservadas por el equipo. |

La evaluación separará sesiones de construcción, ajuste y prueba final, y publicará resultados por edificio, distancia, iluminación y teléfono. Las referencias físicas deberán tener incertidumbre suficientemente menor que los 20 cm que queremos comprobar.

Entre los candidatos que cumplan los requisitos, elegiremos el que tenga **mejor confiabilidad en las condiciones difíciles**; después compararemos alineación, latencia y consumo. Si gana SIFT, utilizaremos SIFT. Si gana una cadena neuronal, tenemos capacidad para mantenerla. Si ninguno cumple, revisaremos los fallos medidos antes de ampliar la solución a otro enfoque.
