# Localización offline en Android — plan

Rama `localizacion-offline`. Complementa [LocalizacionOffline.md](LocalizacionOffline.md): ahí están el banco Python y
el criterio de aceptación; aquí, cómo llevar el candidato (XFeat) al teléfono. **Nada de esto está implementado en la app
ni medido en un teléfono.** Las cifras de tiempo son presupuestos, no resultados.

## Qué se probó ya

`Tools/LocalizacionOffline/exportar_onnx.py` exporta el extractor XFeat (commit fijo `e92685f`, `weights/xfeat.pt`) a
ONNX opset 17 con entrada gris 1×1×H×W fija. Medido en este PC (torch 2.15 CPU, onnxruntime CPU), contra el código original
de XFeat:

| Tamaño | .onnx | Diferencia máx. descriptores / puntos / fiabilidad |
|---|---|---|
| 640×480 | 2,65 MB | 1,6e-6 / 3,6e-6 / 2,0e-6 |
| 1024×576 | 2,65 MB | 3,7e-6 / 6,9e-6 / 4,8e-6 |

Los valores de salida llegan a 0,3–0,8, así que la diferencia es de punto flotante. Operadores del grafo: Add, AveragePool,
Clip, Concat, Constant, Conv, Div, Expand, InstanceNormalization, ReduceL2, Relu, Reshape, Resize, Shape, Sigmoid, Slice,
Softmax, Transpose (BatchNorm queda fundida en las convoluciones). Salidas: mapa denso de descriptores 64-d (H/8×W/8, ya
normalizado), mapa de probabilidad de punto a resolución completa y mapa de fiabilidad. NMS 5×5, umbral, top-k y muestreo
de descriptores quedan fuera del grafo (la salida de tamaño variable no se exporta bien) y se reescriben en el teléfono.
Esto valida solo la equivalencia numérica en PC; no dice nada de velocidad ni de operadores en Android.

## Opciones de motor de inferencia

| Opción | A favor | En contra / por verificar |
|---|---|---|
| **Unity Inference Engine** (`com.unity.ai.inference`, antes Sentis) | Es un paquete de Unity; sin código nativo propio. Última versión que encontré: 2.6.1 (2-abr-2026), con soporte oficial de opset hasta 25 ([changelog](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/changelog/CHANGELOG.html), [novedades](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/whats-new.html)). La [lista de operadores](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/supported-operators.html) cubre los 18 del grafo en CPU, GPUCompute y GPUPixel (Conv, InstanceNormalization, AveragePool, Resize, Softmax, ReduceL2, Clip, Expand…) | La página no dice si Resize bilineal con `half_pixel` está soportado (solo excluye `half_pixel_symmetric`); hay que cargar el modelo y comparar salida. Rendimiento en Adreno/Mali con GPUCompute y costo de leer el resultado a CPU: sin medir. Paquete y runtime viven dentro del ciclo de versiones de Unity |
| **ONNX Runtime Mobile** (.aar) con XNNPACK / NNAPI | Runtime maduro; XNNPACK es CPU ARM bien optimizada; la guía oficial recomienda medir latencia con `onnxruntime_perf_test` en el teléfono ([guía móvil](https://onnxruntime.ai/docs/tutorials/mobile), [ajuste de rendimiento](https://onnxruntime.ai/docs/performance/mobile-performance-tuning.html)) | Requiere puente JNI/plugin propio desde Unity y empaquetar el .aar. NNAPI: el material que encontré es de versiones ≤1.10; revisar estado actual antes de depender de ella. No encontré cifras publicadas de XFeat en Android (búsqueda del 5-nov) |
| **TFLite** | Delegados GPU/NNAPI conocidos | Hay que convertir ONNX → TFLite (otro paso que puede cambiar el modelo); sin ventaja demostrada sobre las dos anteriores. Descartada por ahora |

**Recomendación:** empezar con Unity Inference Engine (backend CPU y GPUCompute) porque el modelo ya exporta limpio, los
operadores están documentados y no hay compilación nativa. Criterio de salida: si la salida en el teléfono no coincide con
el .onnx de PC (tolerancia 1e-3) o el extractor pasa de ~1 s, cambiar a ONNX Runtime Mobile con XNNPACK. El .onnx es el mismo
para ambas, así que el riesgo de elegir mal es acotado.

**Emparejamiento y pose:** no usar LighterGlue al principio. Es imagen contra imagen (necesita puntos de una imagen de
referencia) y añade un modelo más. Primera versión: vecino más cercano mutuo con prueba de razón (ya existe en el banco para
SIFT) de los descriptores de la consulta contra los puntos 3D del edificio, ejecutado como trabajos Burst en C#. PnP + RANSAC
(P3P mínimo, luego refinamiento Levenberg-Marquardt de los inliers) implementado en C# con Burst y verificado con casos de oro
generados por `cv2.solvePnPRansac` del banco. OpenCV para Android queda como plan B solo si la implementación propia no
reproduce las poses del banco; trae un .aar grande y un puente que mantener. LighterGlue se evalúa después, emparejando
contra las imágenes de referencia más parecidas, si el vecino mutuo no alcanza la tasa de aciertos.

## Formato del mapa en el teléfono

Un archivo binario por edificio (`StreamingAssets` o descarga), sin dependencia de Immersal:

- Cabecera: versión del formato, firma del extractor (commit XFeat, SHA-256 de `xfeat.pt`, tamaño de entrada, top-k), edificio,
  transformación mapa → marco del edificio (el mismo que hoy usa `PaseoBuildingFrame`), metros por unidad.
- Cuerpo: N puntos con descriptor 64-d en float16 (128 B) + xyz en float32 (12 B) = 140 B por punto, ordenados por celda
  espacial para poder buscar solo en la vista esperada.
- Estimación (no medida): 50 000–150 000 puntos por edificio → 7–21 MB; cuatro edificios 28–84 MB. Un mapa con otra firma de
  extractor se rechaza al cargar, igual que hace el banco.

Se genera con `offline.py mapa --method xfeat-lighterglue` y un exportador nuevo (pendiente) que escriba este formato.

## Integración con la app

`PaseoLocalization` ya tiene un `Backend` intercambiable. Se añade un `OfflineBackend` que recibe la imagen gris y los
intrínsecos del frame (`PaseoFrame`) y devuelve la misma estructura de resultado (pose + confianza) que
`PaseoPoseProcessor`/`PaseoPoseFilter` ya filtran. Immersal sigue como respaldo: ajuste de campo (`PlayerPrefs`) para elegir
`Immersal` / `Offline` / `Offline y luego Immersal`, y modo Grabar guarda la misma imagen para ambos, de modo que se comparan
sobre las mismas entradas. `PaseoSearchPlan` sigue eligiendo el edificio por GPS; el backend offline solo carga el mapa de ese
edificio. Sin cambios en el filtro de pose ni en las cajas hasta que haya datos.

## Presupuesto de tiempo (objetivo ≤5 s hasta la primera ubicación correcta)

Presupuesto de partida por intento, con 2 intentos posibles dentro de los 5 s. Cada etapa se mide en el teléfono; ninguna cifra
es resultado.

| Etapa | Presupuesto |
|---|---|
| Copia y reducción de la imagen (gris 640×480 o 1024×576) | 0,10 s |
| Extractor (inferencia) | ≤ 1,0 s |
| NMS, top-k y muestreo de descriptores (1 024–2 048 puntos) | ≤ 0,15 s |
| Emparejamiento mutuo contra el edificio | ≤ 0,6 s |
| P3P + RANSAC + refinamiento | ≤ 0,25 s |
| Verificación y filtro de pose | ≤ 0,05 s |
| **Intento** | **≈ 2,2 s** |

El Xiaomi 15T es el caso difícil (calor, ver `CLAUDE.md`): si no cabe ahí, se reduce resolución o top-k antes de cambiar de motor.

## Criterio de aceptación

El mismo del [doc principal](LocalizacionOffline.md): por teléfono, ≥95 % de inicios correctos en ≤5 s, mediana del desfase
≤10 cm y p95 ≤20 cm, y cero aceptaciones falsas en al menos 1.000 secuencias negativas. Requiere el mapa abierto con escala
métrica y las medidas de control independientes que aún no existen. Los tiempos del Mac no cuentan.

## Secuencia de mediciones

1. **Equivalencia** (un teléfono, Xiaomi 14): cargar el .onnx en Inference Engine y comparar las tres salidas con las del PC
   sobre 10 imágenes reales de las grabaciones. Tolerancia 1e-3.
2. **Velocidad del extractor** en los tres teléfonos (Xiaomi 14, Galaxy S25, Xiaomi 15T): mediana y p90 de 50 inferencias, CPU y
   GPUCompute, 640×480 y 1024×576, con teléfono frío y tras 5 min de uso (calor). Si falla 1 o 2, repetir con ONNX Runtime + XNNPACK.
3. **Etapas sueltas**: emparejamiento y PnP+RANSAC en el teléfono con descriptores guardados del PC; las poses deben coincidir
   con las del banco.
4. **Intento completo** sobre las imágenes grabadas (reproducción en el teléfono), tasa de ubicación y tiempo por teléfono.
5. **Terreno**: Offline frente a Immersal sobre las mismas imágenes; luego el criterio de aceptación con secuencias de
   evaluación separadas de las de construcción y negativas.

## Pendiente

- Exportador del mapa al formato del teléfono y generación del mapa métrico (depende del modelo abierto del campus).
- Prueba de Resize bilineal en Inference Engine (única duda de operadores) y del orden de canales.
- Implementar `OfflineBackend`, matcher Burst y PnP+RANSAC; casos de oro desde el banco.
- Comparar 640×480 contra 1024×576 en tasa de ubicación (hoy solo se comprobó que ambos exportan).
