# Procedencia de las 71 vistas y revisión de Descargas — 6-nov-2026

Las **71 vistas son una selección de los 580 PNG de construcción de Teología de la tarde del 5-nov**, todos procedentes de `/Users/gael/Downloads/ancora ultimas grabacoines/`. Se verificó la ruta original y SHA-256 de cada una; no son fotografías distintas generadas por las reconstrucciones ni todo el material disponible.

| Carpeta de construcción | Teléfono | PNG disponibles | Seleccionados |
|---|---|---:|---:|
| `AncoRA/grabaciones/construccion-20261105-142623-6336f9e6` | Xiaomi 14 | 116 | 13 |
| `AncoRA/grabaciones/construccion-20261105-142828-6c2403ae` | Xiaomi 14 | 116 | 15 |
| `AncoRA/grabaciones/construccion-20261105-143053-41f02d6d` | Xiaomi 14 | 116 | 15 |
| `grabaciones/construccion-20261105-143744-192263eb` | Xiaomi 15T | 116 | 15 |
| `grabaciones/construccion-20261105-143857-256706aa` | Xiaomi 15T | 116 | 13 |
| Total | | **580** | **71** |

Primero se seleccionaron 13 ordinales por recorrido = 65 vistas. Después se añadieron seis intermedias para investigar cámaras concretas: dos de `142828`, dos de `143053` y dos de `143744`. Las **509 restantes no se borraron**; simplemente quedaron fuera de ese ensayo controlado. No todas aportan información nueva o son aptas automáticamente, pero están conservadas para ampliar las vecindades necesarias.

## Carpetas comprobadas ahora

| Ruta bajo Descargas | Imágenes presentes | Uso/estado |
|---|---|---|
| `ancora ultimas grabacoines` | 1.900 PNG 1920×1080 y 431 JPG 270×480 | Ya conservadas por hash. Los PNG incluyen varios edificios, construcción y consultas; de aquí salen las 71 |
| `Ancora grabaciones` | 136 PNG 1920×1080 y 179 JPG 270×480 | Ya analizadas y conservadas. Material de la mañana del 5-nov: reconstrucciones históricas/diagnóstico y consultas separadas; no pertenece a las 71 actuales |
| `grabaciones` | 350 PNG 1920×1080 del 28-oct | Históricas, separadas. Ahora se verificó la cobertura completa de sus metadatos, incluyendo copias JSONL numeradas |
| Cuatro exportaciones raíz `AncoRA-registro-Xiaomi-25069PTEBG-20261027/29-…` | 72 JPG 270×480 | Diagnóstico visual adicional, fuera de los manifiestos comparados. No son originales PNG calibrados; no entran automáticamente al mapa |

Los ZIP raíz del 29-oct contienen los mismos JPG de sus carpetas; el ZIP del 23-oct no contiene imágenes. Los JPG pueden ayudar a revisar encuadres/identidad, pero su tamaño y transformación difieren de los PNG originales: no se les asigna la K de estos últimos por conveniencia.

## Omisión encontrada y corregida en los históricos

El importador leía solo `intentos.jsonl` e ignoraba `intentos (1).jsonl`. En tres carpetas del 28-oct esos archivos adicionales contienen metadatos legítimos para **26 PNG que parecían huérfanos**. Se compararon filas completas: hay 78 filas solapadas idénticas y cero conflictos. Ahora se reconocen las copias numeradas y se deduplican los solapamientos, conservando procedencia. Se rechazan metadatos contradictorios para la misma imagen.

`datos.recording_groups` comparte esta lectura entre inventario y auditoría. La auditoría analiza los roles del conjunto de archivos, para que una copia adicional no introduzca consultas inadvertidamente en construcción. Una prueba nueva comprueba recuperación, duplicados, rol ambiguo ante construcción/consulta mezcladas y rechazo de K contradictoria. **20 pruebas Python aprobadas.**

Resultado del inventario corregido: **350 PNG con metadatos, ninguno huérfano**; Teología 153, X1 85, EIC 59 y CB 53. Hay además **20 referencias de metadatos a PNG ausentes, todas de CB**, informadas explícitamente mediante `skip_missing=True`; no se inventaron esos archivos ni se ocultaron sus nombres. No bloquean la investigación de Teología.

De los 26 PNG recuperados, **25 son de Teología y uno de X1**. Los 25 de Teología no estaban en los 182 candidatos del SfM histórico usado antes:

- 24 imágenes `TEO-A-15m`, Samsung, sesión `20261028-113656`.
- Una imagen `TEO-E-15m`, Xiaomi 15T, sesión `20261028-115641`.

No son nuevas capturas: son material existente recuperado correctamente para este banco. Se guardan en `teologia-historicas-25-por-clasificar.json` con **rol sin asignar**. Antes de incorporarlas, se comprobará su utilidad visual, la partición por sesiones completas y su configuración óptica. **La calibración de casa no se transfiere automáticamente a esos históricos.** No se alteran las reservas existentes ni se llama a estos registros evaluación ciega.

## Próximo ensayo concreto preparado

Se preparó y verificó por hash `siete-vistas-intermedias-candidatas.json`: imágenes **0002, 0003, 0004, 0066, 0067, 0069 y 0070** del recorrido de construcción `143744`. Están dentro de los 580 PNG existentes y rodean las dos vistas aún problemáticas `0001` y `0068`. Se conservaron las exclusiones de consultas y no se seleccionaron por éxito geométrico.

**Esto es preparación, no una reconstrucción nueva:** faltan sus anotaciones propias de fachada/identidad, comprobar que aportan continuidad y ejecutar el matching/SfM en otra salida. Si se añaden las siete, el ensayo tendrá 78 vistas; las 71 anteriores se conservan. Los 25 históricos recuperados se revisarán aparte, sin unir de golpe todo el material.

Después: resolver/comprobar los enlaces y la sensibilidad de esas cámaras → identificar en varias vistas las cuatro aristas interiores ya confirmadas → ajustar escala solo con INTERIOR-C-D y comprobar las tres reservas exploratorias → ejecutar localización de una imagen reservada con óptica compatible → comparar candidatos e integrar/medir Android. Las medidas no entraron en la selección de imágenes ni en el ajuste de las cámaras.

Salida de esta revisión: `Tools/LocalizacionOffline/salida/20261106-origen-imagenes/`, con procedencia de las 71, imágenes/dimensiones/hashes por carpeta, todos los JSONL históricos, omisión inicial conservada, inventario corregido, candidatos y pruebas. **Sin modificación de originales, reconstrucciones existentes, APK, rama o commits.**
