# Revisión de ramas y contraste con el plan offline — 5-nov-2026

Revisión focalizada de lectura, con el árbol local sin commit conservado. Se comparó el plan original, el estado actual, las dos entregas del tablero y los commits disponibles. Se consultó GitHub mediante el conector para comprobar las puntas remotas, sin fetch, checkout, merge, rebase, pull, worktree, commit o push. No se abrió Unity ni se recompiló/instaló ningún APK.

## Conclusión operativa

Seguimos en **geometría y validación de cámara/escala**, antes de una localización real aceptada y su integración Android. La calibración del 15T ya produjo candidatos útiles y estables para ensayos de PC; no hace falta seguir repitiendo el tablero. Falta aplicar correctamente esa óptica al flujo de imágenes y comprobar el edificio con medidas físicas. La [jornada del 6-nov](LocalizacionOffline-Jornada-2026-11-06.md) se adapta al equipo confirmado: teléfonos y cinta, sin exigir medir techo/aleros inaccesibles.

La prioridad no es convertir más modelos a Android ni sustituir el localizador antes de tener un mapa comprobable. Se reutilizarán las piezas nuevas que ayuden a ese orden de trabajo.

## Qué rama tiene realmente los cambios

| Referencia | Punta comprobada | Relación con el trabajo local |
|---|---|---|
| `nicowo` local y GitHub | `d92cfe5fb534b63e1c7d11cb869dc39796eda119`, 23-oct | GitHub confirma identidad con la referencia local. Es ancestro de HEAD: sus commits ya están en la historia actual |
| HEAD local `localizacion-offline` | `f84256fe0bbccd1acc00fce508c5a06a1039886b` | Más los cambios extensos sin commit, incluidos captura de construcción, calibración e informes |
| GitHub `localizacion-offline` | `e333ceefabc66399e88c30ac9c4d87de7fc59eb3`, 5-nov | Dos commits por delante de HEAD: `1282d61` y `e333cee`. Su contenido no está íntegramente aplicado al árbol local |

Los commits recientes del colaborador relevantes para el banco están publicados en `localizacion-offline`, **no como novedades de `nicowo`**. Esta distinción evita intentar integrar otra vez una rama antigua o atribuir los cambios locales al remoto.

## Lo que aportó nicowo y sigue siendo útil

- [`d92cfe5`](https://github.com/Gael-OC/AncoRA/commit/d92cfe5fb534b63e1c7d11cb869dc39796eda119): exportación de una nube de mapa suelta, análisis de cobertura y candidato Teología `152693`. El mapa se incorporó después en la historia de la app; no es una novedad pendiente de traer. La nube/registro Immersal puede ayudar a ubicar visualmente sectores, **sin usarla como verdad física ni como descriptores del mapa abierto**.
- [`bbb1f2e`](https://github.com/Gael-OC/AncoRA/commit/bbb1f2e55679bd4086369af78e000aa2e61a0de8): página de cobertura. Sigue disponible para entender qué se fotografió y las fachadas; no acredita precisión métrica del nuevo mapa.
- [`0eee8a8`](https://github.com/Gael-OC/AncoRA/commit/0eee8a8dd8c3867e0ac2fafd410ad5110df6a42b): búsqueda por rumbo y ajuste de X1 en terreno. Son antecedentes, algunos reemplazados por cambios posteriores; `ec21d58` rehizo X1 desde el edificio del dron y descartó el ajuste inflado. No recuperar la caja antigua ni introducir brújula/GPS como referencia de calibración offline.
- [`0969a73`](https://github.com/Gael-OC/AncoRA/commit/0969a73a56eb5466a57177a635abbbabf032a734): modos de vista de la antigua demo. El flujo de reemplazos cambió después. No resuelve cámara/escala/sincronía ni necesita reintroducirse para la salida de mañana.

No hay un choque nuevo pendiente con `nicowo` por commits no integrados. La presencia de una función en aquella versión no obliga a restaurarla sobre la arquitectura actual.

## Los dos commits remotos nuevos

[`1282d61`](https://github.com/Gael-OC/AncoRA/commit/1282d6177ea0e61245f8d94085795778ec9bd973), «Integridad de capturas, recorrido de construcción, mapa del dron y XFeat en ONNX»:

| Pieza | Aporte | Decisión respecto del plan |
|---|---|---|
| `PaseoRecordManifest`, finalización de escrituras y comprobación al copiar | Manifiestos SHA-256 y conteo final después de terminar escrituras; evita confiar solo en el contador al detener | **Aprovechable.** Integración selectiva posterior con pruebas; nuestras carpetas sin manifiesto siguen siendo utilizables mediante auditoría PNG/JSONL/hash |
| Recorrido 60 s / ~2 imágenes/s y `PaseoTravelTracker` | Se solapa con el recorrido ya añadido localmente; agrega distancia estimada por Unity | Conservar un solo flujo de captura. El recorrido estimado no es longitud física ni referencia para dar escala al edificio |
| `importar-realityscan` | Importa cámaras PINHOLE y georreferencia del dron; conserva `alignment_verified:false` y `ground_truth:null` | Útil como herramienta de importación si existen cámaras/fotos válidas. Su escala procedente de GPS no sustituye controles de cinta ni acredita 10–20 cm. No abre una dependencia nueva de material del PC del dron |
| `alinear-arcore` | Busca Sim(3), ejes y desfase entre SfM y ARCore | **Solo diagnóstico por ahora.** Su etiqueta de validación es demasiado fuerte; hay contraejemplo reproducido más abajo |
| `exportar_onnx.py` | Exporta el extractor XFeat, deja selección de puntos/muestreo y otros pasos fuera del grafo | Avance reutilizable para un ensayo de equivalencia/rendimiento futuro. No implementa matcher, mapa, PnP, secuencia ni colocación del contenido |
| `LocalizacionOffline-Android.md` | Propone XFeat, matcher por vecino mutuo y P3P/RANSAC propio | Es una propuesta, no trabajo Android terminado. No elegir XFeat ni omitir LightGlue por comodidad: el plan mantiene cuatro candidatos. No hacer de un solver propio un requisito antes de comparar lo que ya funciona en PC |

[`e333cee`](https://github.com/Gael-OC/AncoRA/commit/e333ceefabc66399e88c30ac9c4d87de7fc59eb3) regenera `PaseoIngenieria.unity` por una discrepancia de EIC. No aporta un localizador ni prueba óptica. La escena local tiene otros cambios sin commit; no reemplazarla por la escena generada de otra versión.

El mensaje del primer commit informa 516 pruebas EditMode y 24 Python; no se presentan aquí como verificadas de nuevo. En esta revisión se ejecutaron **10 pruebas Python seleccionadas de importación y alineación** sobre una copia de ese commit: todas pasan. No se ejecutaron Unity, build, ONNX ni hardware. El APK local de construcción mantiene SHA-256 `ef655eaad4fc147e94d2385083e7db3d8a52dfb405a1f64b1707070a4b0fb543`.

## Hallazgos que afectan la decisión

### R1 — Prioridad alta antes de usar el nuevo validador: sincronía no identificable puede quedar «validada» (confirmado en sintético)

En `alineacion.py:318–423`, `align_arcore` estima desfase y Sim(3) con las mismas cámaras y acepta por residuos internos, número de vistas, extensión del recorrido y error angular. En `offline.py:198–215` puede marcar `optical_pose_validated=true` en el inventario. No exige comprobación independiente del instante real de pose ni que el desfase sea identificable.

Se probó el código remoto **sin modificarlo** con un movimiento circular uniforme, con >1 m de extensión, variación lateral suficiente y un desfase simulado real de **+30 ms**. Las 41 hipótesis temporales de −100 a +100 ms pueden absorberse en otra similitud: todas ajustan posiciones con RMS <1,1e−15 m. La función elige **−35 ms**, 65 ms distinto del valor simulado, y devuelve **`pose_optica_validada:true`**. Esto no es fallo de precisión de ARCore medido en hardware: demuestra que el criterio de aceptación del ajuste no acredita por sí solo tiempo físico ni observabilidad.

Evidencia reproducible: `revision-2/caso_tiempo_no_observable.py` y `.json`, dentro de la salida de revisión. Ejecutarlo no aplica el commit a la rama activa. Las diez pruebas originales seleccionadas pasan, pero no cubren este caso. El contador de escrituras/manifiesto nuevo tampoco corrige los tiempos: el `PaseoSequenceCapture` remoto todavía escribe `PoseSensorTimestamp = frameTimestamp` y lee Unity en `LateUpdate`.

**Acción antes de integrarlo como puerta de aceptación:** conservar el resultado como consistencia/hipótesis, comprobar observabilidad y límites de búsqueda, separar ajuste/comprobación e instrumentar/validar el instante de pose. No hacer que la etiqueta cambie a verdadero solo porque un ajuste a las propias poses dé menos de 5 cm. Validar con movimiento y referencias temporales independientes; hasta entonces no habilitar multicuadro por este rótulo.

### R2 — Riesgo de integración alto, solapamientos confirmados; no es un merge ejecutado

Se hizo una previsualización textual de tres vías en archivos copiados, usando HEAD como base, árbol local como lado local y `e333cee` como remoto. **No se tocó el índice ni se aplicó ningún cambio.** Hay nueve archivos modificados por ambos lados. Seis muestran conflictos textuales en esa prueba: `PaseoSequenceCapture.cs`, `PaseoTour.cs`, `PaseoUi.cs`, `Ui/PaseoRecordPanel.cs`, `PaseoIngenieria.unity` y `Docs/LocalizacionOffline.md`. `PaseoRecorder.cs`, su prueba de panel y el README no muestran conflicto textual en esa previsualización, lo cual no prueba compatibilidad semántica ni compilación.

Los conflictos coinciden con dos implementaciones del recorrido/controles de grabación y la regeneración masiva de la escena. El informe por archivo está en `revision-2/solapamientos.json`; las copias y vistas `.preview` son solo evidencia, nunca fuentes para Unity. Recomendación: portar primero los manifiestos/finalización útiles sobre el flujo local y validarlos; resolver la escena a partir de la configuración vigente cuando exista una integración autorizada y comprobable. No cambiar de rama o hacer pull sobre este árbol para salir mañana.

### R3 — Prioridad media: el documento Android adelanta decisiones que el plan dejó abiertas (confirmado en texto)

El documento nuevo propone una primera cadena XFeat + vecino mutuo, deja LighterGlue para después y plantea P3P/RANSAC en C#/Burst. Son ensayos posibles, pero todavía no comparan SIFT, SIFT+LightGlue, ALIKED+LightGlue y XFeat+LighterGlue con mapa/cámara aceptados, ni están medidos en el teléfono. No reemplazar la evaluación por ese diseño, ni interpretar equivalencia de un extractor ONNX como localización completa. Mantener el exportador como pieza aprovechable, con puerta independiente de equivalencia y rendimiento, sin instalar paquetes o desarrollar un solver por rutina.

## Secuencia acordada por el alcance de este trabajo

1. Para el 6-nov: obtener longitudes AJ/RES accesibles y fotos inequívocas con teléfonos/cinta; dejar explícitas zonas no medibles. No más recorridos generales ni otra calibración completa del 15T.
2. En PC: incorporar la óptica candidata del 15T de forma compatible en una **sensibilidad nueva** de geometría, con distorsión/rectificación y K consistentes; no cambiar globalmente los priors de otros teléfonos. Revisar tracks/cámaras ambiguas y correspondencia de extremos físicos.
3. Ajustar escala con AJ y evaluar RES. Con cinta el alcance inicial será escala y distancias locales; no declarar XYZ completo ni exactitud en techo/sectores sin medición. Si los puntos no están reconstruidos, identificar esa carencia antes de otra captura.
4. Probar pose de una imagen con mapa/cámara aceptables y consultas separadas; añadir multicuadro solo al comprobar conversión óptica/tiempo. El tablero de las 22:54:49 sigue como diagnóstico de movimiento, no como verdad.
5. Comparar candidatos y preparar Android con los que superen las comprobaciones. Aprovechar manifiestos y exportación como piezas; no integrar de golpe la escena remota ni el rótulo de validación temporal.

## Evidencia y límites de la revisión

Salida nueva `Tools/LocalizacionOffline/salida/20261105-plan-y-ramas/revision-2/`: instantánea de los archivos remotos seleccionados, solapamientos de tres vías, contraejemplo sintético, resultados y hashes. La salida parcial de la primera previsualización se conserva separada: el comando Git devuelve el número de bloques conflictivos; la revisión completa posterior manejó correctamente ese resultado.

Contexto comprobado: Unity 6000.6.0f1, AR Foundation/ARCore 6.5, Immersal 2.4; no hay Inference Engine instalado en el manifiesto local. `PaseoLocalization.Backend` sigue apuntando a `Core.LocalizeImage`; los scripts de geometría aún usan PINHOLE y PnP sin distorsión. La calibración candidata de casa no se ha aplicado automáticamente a esos modelos. No se evaluó calidad completa de ONNX, runtime Android, UI/escena en Editor ni compatibilidad de una integración real. Las recomendaciones sobre esas piezas son decisiones de alcance y lectura del código, no mediciones de rendimiento.
