# Localización offline — implementación inicial

**Estado actual y orden para continuar:** [Estado y plan de trabajo](LocalizacionOffline-Estado-y-Plan.md).
Este documento conserva la implementación y sus validaciones por fecha; «implementado» no significa validado físicamente.

Rama `localizacion-offline`. Diseño: 10–25 m, barrido inicial de hasta 5 s, objetivo de alineación de fachada de 10–20 cm.

## Ya implementado

- Banco Python reproducible con cuatro métodos: SIFT, SIFT+LightGlue, ALIKED+LightGlue y XFeat+LighterGlue.
- Importación de las grabaciones sin confundir etiquetas/respuestas de Immersal con verdad de terreno.
- Triangulación desde cámaras métricas registradas, localización PnP/RANSAC y refinamiento de secuencias calibradas.
- Índices por método, parámetros, versiones, pesos y hash de la imagen; consultas separadas de las sesiones de construcción.
- Captura Android de barridos desde AR Foundation, independiente del ciclo de intentos. Pausa y restaura Immersal durante
  el barrido. Imágenes R8 originales, intrínsecos, poses Unity, tiempos de sensor/pose y matriz display.
- Distancias de captura 10/15/20/25 m. Las grabaciones históricas mantienen los metros explícitos de sus nombres.

La escena de demo sigue funcionando con Immersal. **Aún no hay localizador alternativo Android ni mapa abierto del campus
validado métricamente.** El banco y el grabador son el primer entregable necesario para decidir con datos.

## Registrar nuevos barridos

Para la construcción conectada de Teología usar el nuevo modo de recorrido:
[APK y prueba concreta](LocalizacionOffline-PruebaConstruccion.md). La pauta siguiente corresponde a consultas de 5 s.

Compilar con el menú `AncoRA/Paseo/Compilar APK de capturas offline`, o ejecutar
`AncorRA.Editor.PaseoSetup.BuildOfflineCaptureAndroid` por Unity batch. Salida separada:
`Builds/Android/AncoRA-Capturas-Offline.apk`; no reemplaza `AncoRAPaseo.apk` en el computador.
Usa el mismo identificador Android que la demo: instalar encima conserva los ajustes solo con la misma firma.
No desinstalar para resolver una firma distinta sin conservar primero datos y ajustes.

1. Abrir la app y esperar a que ARCore tenga seguimiento.
2. Abrir Equipo (cinco toques en la esquina superior izquierda) y Grabar.
3. Elegir edificio, punto A–F, distancia real y luz. Nuevos puntos: A/B a 10 m, C/D a 15 m, E a 20 m y F a 25 m.
4. Dejar apagada la grabación por intentos. Pulsar **Grabar barrido de 5 segundos**. Esperar al intento anterior si estaba
   procesando; después barrer suavemente la fachada. La captura busca hasta 6 imágenes/s y puede omitir imágenes si el
   teléfono o disco no alcanzan ese ritmo. No se exige caminar ni cruzar obstáculos.
5. Mantener la app abierta hasta que terminen las escrituras. Copiar grabaciones a Descargas con el botón existente.

Los barridos se guardan como carpetas `barrido-...` bajo las grabaciones existentes. Pausar la app o perder tracking
interrumpe el barrido. Usar otra captura al volver, no mezclar los dos orígenes. No hacer marcas permanentes en el campus.

## Datos disponibles en este Mac

**Actualización 5-nov-2026:** 136 PNG nuevos de Teología: cuatro barridos completos (108 imágenes, Samsung S25 y
Xiaomi 15T) y 28 imágenes de «Teología J» sin JSONL. Cadencia real de los completos: 5,37–5,39 imágenes/s, sin huecos
largos ni interrupciones registradas durante esas capturas. Los logs señalan siete carpetas Samsung adicionales no
incluidas en el material disponible; Gael confirmó que no tiene más carpetas ni metadatos que recuperar.
Se ejecutaron 996 comparaciones con los cuatro métodos y un ensayo SfM sin escala métrica;
el modelo SIFT obtenido se concentra en vegetación cercana, no valida la fachada. XFeat mejora las correspondencias
entre sesiones, pero aún no se elige motor Android. La siguiente prueba será reconstrucción con XFeat y comparación
ALIKED sobre los datos existentes, manteniendo las consultas separadas. Informe y particiones:
[Registros del 5-nov](LocalizacionOffline-Registros-2026-11-05.md). El importador ahora avisa de imágenes sin metadatos;
11 pruebas Python aprobadas. No se modificó ni recompiló la app.

Se encontraron 344 filas históricas en Descargas: 324 imágenes presentes de 12 sesiones y 20 fotos ausentes de otras tres.
Se importaron con exclusión explícita. Capturas principalmente estáticas, a unos 15 m y nublado. No cubren el nuevo ensayo
de secuencias, luz y distancias. Informe ejecutado: [LocalizacionOffline-Resultados.md](LocalizacionOffline-Resultados.md).
Los JPEG exportados junto a los logs son miniaturas de depuración, reducidas y giradas por `PaseoDebugImages`; no se
mezclaron con los PNG originales que conservan los intrínsecos de cámara.

## Para completar la elección del motor

1. Obtener cámaras/imagenes de una reconstrucción abierta y medidas de control independientes. Registrar el modelo en
   metros y comprobar escala, geometría de fachadas y colocación del contenido virtual por separado.
2. Validar extrínsecos/orientación de la imagen cruda y sincronía de las poses en cada teléfono. Los registros no declaran
   calibración óptica validada por el simple hecho de contener una pose Unity.
3. Capturar barridos separados para construcción, ajuste y evaluación; comparar por teléfono, edificio, distancia y luz.
4. Llevar los candidatos geométricamente válidos a Android (biblioteca nativa/motor de inferencia) y medir el proceso
   completo. Los tiempos del Mac no son tiempos Android.
5. Aceptación propuesta: ≥95 % de inicios correctos en ≤5 s por teléfono, mediana del desfase ≤10 cm y p95 ≤20 cm,
   y cero aceptaciones falsas en al menos 1.000 secuencias negativas. Son objetivos, no resultados actuales.

Comandos, formatos y dependencias: [README del banco](../Tools/LocalizacionOffline/README.md).

## Validación del 4-nov-2026

- Unity 6000.6.0f1, EditMode `AncorRA.Tests`: 508 aprobadas, 0 fallidas, 1 ignorada previamente porque verifica
  recorte de fragmentos con gráficos y la ejecución fue `-nographics`.
  Se comprobó la restauración del estado previo del localizador cuando un barrido es rechazado.
- Python 3.12, `unittest discover`: 10 aprobadas. Incluyen geometría sintética, mapa → consulta separada, rechazo de
  mezcla de sesiones y rechazo de mapas con otro extractor/código/pesos. No constituyen una prueba de precisión física.
- Cuatro métodos ejecutados sin red: comparación de 24 pares por método a 1024 px/2048 características; comprobación
  final de dos pares por método tras cerrar la implementación. Entorno y pesos preparados en carpetas ignoradas por Git.
- APK de desarrollo compilado con `BuildOfflineCaptureAndroid`: 92,6 MiB, ARM64, IL2CPP, Android mínimo 26 y objetivo 36;
  identificador `com.ancora.ucnar.paseo`, versión heredada 1.5.3 (28). No se cambió la versión de la demo ni sus ajustes.
- El build emitió avisos sobre un asset de pruebas de ARCore en paquetes inmutables y sobre funciones grandes de
  TextMeshPro. Terminó con código 0; no se editaron esos paquetes para silenciar los avisos.

**Pendiente de validar en teléfono:** acceso a la imagen CPU, cadencia real, sincronía/orientación de cámara, pérdida de
tracking, pausa/reanudación y exportación del barrido. No se instaló este APK ni se ensayó el barrido en hardware durante
esta validación. La sustitución de Immersal y el objetivo de alineación siguen pendientes de mapa y evaluación reales.

## Publicación aislada, 5-nov-2026

La rama incorpora solo banco Python, captura ARCore y documentación offline sobre `f41cf00`. Los cambios locales de
demo, modelos, oclusión, parque, mapas de demo y escena se conservaron fuera del commit. En archivos compartidos se
seleccionaron únicamente los cambios de captura; el trabajo local completo conserva además sus interbloqueos de demo.

Validación de la versión seleccionada en una copia aislada: **482 pruebas EditMode `AncorRA.Tests` aprobadas,
0 fallidas, 0 omitidas**, Unity 6000.6.0f1, `-batchmode -nographics`; sin errores de compilación C#.
Las 11 pruebas Python también pasan sobre esa copia. Este total no incluye las pruebas de las funciones de demo que
permanecen locales. No se reconstruyó el APK ni se atribuye a esta publicación una validación nueva en Android.

Lo aportado es todo el material disponible; no se espera recuperar otras carpetas. Decisión y prompt de continuación:
[LocalizacionOffline-Continuacion.md](LocalizacionOffline-Continuacion.md).

## Reconstrucción aprendida, 5-nov-2026

Se ejecutó la continuación con A/B nuevos y las cinco sesiones históricas de Teología: 49 vistas seleccionadas de
182 disponibles, comparadas con XFeat+LighterGlue y ALIKED+LightGlue a 1024 px. XFeat solo fachada reconstruye 48 vistas
y 2.771 puntos, con un núcleo parcial que coincide mejor entre métodos. El conjunto global cambia decenas de grados
al quitar primer plano o cambiar a 1440 px; **no se acepta como mapa métrico**. ALIKED solo fachada produce dos
componentes separados con soporte 3D. D/E nuevos y J no participaron en la construcción.

Se conservan modelos, tracks, diagnósticos de cobertura/vegetación y comparaciones de estabilidad, con escala
arbitraria explícita. Hay 13 pruebas Python aprobadas, incluida la integración de correspondencias externas con SfM.
No se modificó ni recompiló Unity. Resultado, límites y trayectoria de construcción propuesta:
[Geometría de Teología](LocalizacionOffline-Geometria-2026-11-05.md).
