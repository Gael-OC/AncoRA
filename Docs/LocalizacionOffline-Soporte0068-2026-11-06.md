# Soporte físico de 0068 y refinamiento de aristas — continuación del 6-nov-2026

**Trabajo ejecutado en PC, después del ensayo de 78: revisión de fotos completas y tracks, diagnóstico de distribución/profundidad/paralaje, prueba SIFT dirigida y refinamiento manual de aristas en originales.** No se resolvió la pose de 0068 ni se aceptó una reconstrucción. No se volvió a ejecutar el banco SfM, no se alteraron las 71/78 ni sus cámaras y no se aplicó escala.

Salidas nuevas bajo `Tools/LocalizacionOffline/salida/`:

- `20261106-soporte0068-02/`: fotos completas, índices superpuestos, diez láminas de tracks, cobertura efectiva de 0066, diagnósticos cuantitativos y SIFT dirigido.
- `20261106-soporte0068-independiente-02/`: revisión visual independiente Terra, láminas de los tracks baseline/radial e informe. Su primera versión tenía el error tipográfico kp661 por kp561; se conserva junto a la corrección.
- `20261106-aristas-refinamiento-02/`: nueve filas multivista de las cuatro aristas, ocho PNG únicos por hash, segmentos crudos/orientados y recortes. La referencia de H–I y vuelta/frente comparte PNG.

## Identidad estructural y posición puntual son comprobaciones distintas

Se revisaron completas 143744/0066–0070, 142623/0078 y 143857/0039–0049–0078, además de recortes originales de sus asociaciones. En las fotos orientadas hay ranuras estrechas superior e inferior junto a ventanas largas con alero. El poste tapa parte de 0068. Los ejemplos examinados son compatibles con la misma ranura y el mismo montante; **no se demostró alternancia entre niveles en esos ejemplos**. Esto no certifica todos los tracks.

Índices de selección78: 46=143744/0068, 74=143744/0066, 8=142623/0078, 56/57/60=143857/0039/0049/0078. Los índices son de XFeat salvo indicación expresa; no se transfieren a ALIKED.

| Observación de 0068 | Enlaces revisados | Evidencia y límite |
|---|---|---|
| kp138 | 0066 kp704; 142623/0078 kp37; 143857/0039 kp142, /0049 kp399, /0078 kp235 | Próximo a unión inferior de montante y marco de la ranura superior. Candidato estructural más específico; no demuestra coincidencia subpíxel ni precisión física. |
| kp60 | 0066 kp7; 142623/0078 kp336 en radial; 143857/0039 kp1493, /0049 kp327, /0078 kp360 | Cerca de una transición claro/oscuro que llega al montante. Puede ser sombra/iluminación: no se distingue un vértice geométrico inequívoco. No aceptarlo como ancla física puntual por su repetibilidad. |
| kp584 | Baseline: 142623/0078 kp49 y 143857/0078 kp238. Radial: 142623/0078 kp1261 y 143857/0049 kp1112, además de /0087 kp1377 | Misma zona de montante liso, con cambio de posición. kp49→1261 dista 9,375 px en la misma foto. Sin marca única no se certifica cuál corresponde a kp584, ni se declara falso todo el track. |
| kp980 | 142623/0078 kp1188; 143857/0078 kp1824; 0066 kp1508 en radial | Montante sin detalle puntual inequívoco. Baseline incluye también 0068 kp1731, unos 5,6 px separado: mismo centro de cámara, no vista independiente. |
| kp1102 / kp1151 | 0066 kp1834 / kp1335, y material antiguo | Cerca del marco inferior/vidrio de la ranura superior. Más observaciones sobre ese borde no aportan por sí solas profundidad independiente. |

También se inspeccionaron ejemplos de la ranura inferior (kp362 y kp561) y bordes lisos (kp396, kp1837). La identidad puntual permanece incierta donde solo hay arista, sombra, vidrio o textura no distintiva. Se conservan las asociaciones; **no hay evidencia suficiente para una corrección v7 que suprima enlaces concretos como físicamente falsos**. No se impone identidad a partir de poses ni de un residuo menor.

## Cobertura realmente permitida por las máscaras

Los cinco tracks de 0068 que incorporan 0066 están en la **ranura superior**. Baseline usa kp60/138/181/1102/1151; las dos radiales, kp60/138/980/1102/1151.

La máscara final78 de 0066 termina en **v=1055,45 px orientados**, antes de la ranura inferior visible. No podía reforzar ese nivel con sus observaciones físicas correspondientes. Es una limitación del ensayo con exclusión conservadora de primer plano, **no prueba de que falte la fotografía inferior en el material recibido**. No se amplió la máscara ni se relajaron umbrales.

`cobertura-0066-v2.json` sigue el par 46–74: XFeat pasa de 594 matches brutos a 10 de fachada/identidad, todos en la zona de ranura superior; ALIKED pasa de 557 a 21, siete en esa zona y catorce en otras partes del muro. Ninguno conserva matches de la zona de ranura inferior de 0068 hacia una observación elegible de 0066. Los grandes totales brutos incluyen otras regiones y no equivalen a soporte de fachada válido.

Las cajas de diagnóstico de 0068 son solo ubicación en imagen orientada: superior u=900–1080/v=630–770, inferior u=890–1080/v=1090–1230. No son nuevas anotaciones de identidad ni filtros. La primera versión `cobertura-0066.json` dividía por v=900 y llamaba «lower_strip» también al muro intermedio; **usar v2 para distinguir ranuras**. El nombre preliminar `aliked-0068-0066-nivel-distinto.jpg` corresponde a una hipótesis descartada: esos tres pares están en muro intermedio, no en niveles de ventanas diferentes; la misma lámina se identifica correctamente como `aliked-tres-pares-muro.jpg`.

## Distribución, profundidad y paralaje condicionales

`cuantitativo-v2/soporte.json` cuenta puntos únicos por reconstrucción y conserva cada observación/índice. Verifica que las coordenadas del modelo, menos 0,5, reproducen los NPZ originales. Los IDs 3D locales no se comparan entre modelos.

| XFeat78 | Observaciones / puntos únicos | Zona superior / inferior / otro muro | Profundidad estimada p10–p90, unidades arbitrarias | Espesor/extensión por SVD 3D |
|---|---:|---:|---:|---:|
| Baseline | 38 / 37 | 22 / 14 / 1 | 2,535–2,698 | 0,0929 |
| Radial | 38 / 38 | 25 / 13 / 0 | 2,672–2,751 | 0,0333 |
| Réplica radial | 39 / 39 | 25 / 13 / 1 | 2,560–2,652 | 0,0336 |

El cociente SVD describe la nube estimada centrada, sin ajuste físico de planos. No son metros ni prueba independiente de planitud. La envolvente convexa ocupa 4,54/2,86/5,10 % de la imagen y atraviesa zonas vacías; no es porcentaje de fachada realmente bien apoyada.

Paralaje mediano estimado entre 0068 y 0066 sobre sus cinco puntos: **25,16° / 4,14° / 3,00°**. Hacia 142623/0078: **19,88° / 46,16° / 44,58°**; hacia 143857/0078: **45,23° / 15,09° / 16,84°**. Estas diferencias dependen de las mismas cámaras/asociaciones discutidas: **un ángulo grande no prueba buena profundidad física**. No hay base para atribuir el fallo únicamente a focal, falta de imágenes o poco movimiento.

Como diagnóstico local adicional se calculó el Jacobiano de pose con puntos 3D y K/D fijos, tras normalizar traslación por profundidad mediana. Su razón entre valores singulares extremos es 94,4/256,1/209,7. Indica sensibilidad anisótropa bajo ese modelo, ignora incertidumbre del mapa/asociaciones y **no selecciona una óptica/réplica ni representa covarianza de precisión**. El autocontrol comprueba invariancia a escala y un ángulo sintético conocido.

## Hipótesis nueva comprobada: textura de muro con SIFT

Se reutilizó `caracteristicas.Features('sift', 1920, 4096)` (RootSIFT, NN mutuo y ratio 0,8) en las seis fotos anteriores. Proceso separado, red bloqueada, sin SfM posterior. Mismas máscaras78, imágenes crudas y resultados en un dominio de índices nuevo: **no se mezclaron con XFeat/ALIKED ni se añadieron al mapa**.

| Par desde 0068 | Matches brutos / fachada | Inliers homografía diagnóstica |
|---|---:|---:|
| 0066 | 299 / 9 | 8 |
| 142623/0078 | 53 / 8 | 5 |
| 143857/0039 | 44 / 0 | 0 |
| 143857/0049 | 52 / 5 | 0 |
| 143857/0078 | 72 / 7 | 5 |

Los dos puntos del muro hacia 0066 situados lejos de las ranuras están en **la misma junta horizontal lisa** (v≈669–671); se inspeccionaron recortes, no muestran vértices únicos nuevos repartidos en altura. **Resultado negativo: esta prueba no ofrece el apoyo distribuido buscado.** La H cruda con RANSAC a 3 px es diagnóstico 2D, ignora no linealidad radial y no demuestra pose ni identidad; sus inliers no se convierten en controles 3D. No se elige ni descarta SIFT como localizador final por esta prueba.

## Aristas interiores y límites de esta continuación

Terra revisó originales y propuso correcciones de trazos: C–D en 143857/0116 conserva solo el tramo inferior visible del lado derecho; R–S en 142828/0078 tiene el borde izquierdo oblicuo; H–I en la referencia 143744/0116 sigue un límite oscuro con ambigüedad de marco. Las demás propuestas se conservan cuando parecían plausibles. El agente principal revisó además las completas anotadas C–D/142828-0011 y H–I/143744-0116.

`segmentos-refinados.json` guarda las nueve filas, coordenadas crudas/orientadas y hashes. La banda manual ±8 px **no es incertidumbre estadística ni una reducción demostrada respecto al ±20 anterior**; puede ser mayor en bordes ocluidos/ambiguos. No hay nueva confirmación del usuario. Los extremos solo delimitan tramos visibles, no puntos homólogos ni altura de cinta. No se triangularon líneas con cámaras no aceptadas, no se eligieron puntos 3D vecinos y no se aplicó escala. C–D sigue siendo el único ajuste; las tres reservas no intervinieron.

Continúan pendientes correspondencias puntuales físicamente distintivas y distribuidas para 0068, junto con comprobación de las otras asociaciones conflictivas del mapa. Antes de ampliar nada, revisar en los originales existentes los extremos estructurales de ambas ranuras y separar montantes, vidrio y transiciones de sombra; cualquier cambio de identidad o cobertura será una condición explícita nueva, manteniendo 71/78. El resultado actual no justifica repetir todo el banco ni aflojar máscaras para registrar 0069/0070.

## Reproducción y comprobación

Desde la raíz, para repetir solo el análisis cuantitativo en **otra ruta nueva**:

```sh
Tools/LocalizacionOffline/.venv/bin/python Tools/LocalizacionOffline/experimentos/soporte_0068_20261106.py --source Tools/LocalizacionOffline/salida/20261106-ensayo-78 --output Tools/LocalizacionOffline/salida/otra-revision-soporte0068
```

El comando rechaza sobrescritura. Se conservaron los scripts, variantes preliminares y resultados negativos. **20 pruebas Python aprobadas de nuevo**, además del autocontrol del nuevo diagnóstico y comprobación real de coordenadas contra NPZ. El grafo local se actualiza tras este código; sigue siendo parcial por formatos sin extractor. La auditoría final de conservación se guarda en `20261106-soporte0068-02/auditoria-cierre.json`.

La app sigue con Immersal. El mapa permite investigar correspondencias y sensibilidad a escala arbitraria; no acredita 10–20 cm. Para probar el motor propio faltan geometría/identidad aceptables, aristas 3D y escala con C–D/reservas, contrato óptico compatible en importación/triangulación/PnP/proyección o rectificación coherente, consulta separada en PC, comparación de los cuatro candidatos y puerto/medición Android. Multicuadro añade conversión óptica y sincronía real pendientes. **No se necesita otro envío de Gael para continuar la revisión en PC.**
