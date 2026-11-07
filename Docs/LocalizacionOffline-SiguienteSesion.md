# AncoRA — encargo vigente tras ejecutar el ensayo de 78 vistas

Actualizado el **6-nov-2026**, después de la revisión física dirigida de 0068, prueba SIFT acotada y refinamiento de aristas posteriores a las cinco reconstrucciones78. **El ensayo 71→78 ya se ejecutó: no repetirlo como tarea pendiente.** [Estado y plan](LocalizacionOffline-Estado-y-Plan.md), [resultados78](LocalizacionOffline-Ensayo78-2026-11-06.md), [nueva revisión física y aristas](LocalizacionOffline-Soporte0068-2026-11-06.md). Las versiones de inicio se conservan en `salida/20261106-ensayo-78/fuentes/LocalizacionOffline-SiguienteSesion-antes-78.md` y `salida/20261106-soporte0068-02/LocalizacionOffline-SiguienteSesion.md`, bajo `Tools/LocalizacionOffline/`; el [encargo del 5-nov](LocalizacionOffline-SiguienteSesion-2026-11-05-Historico.md) sigue como historia. El [plan original](LocalizacionOffline-PlanOriginal.md) no cambia.

## Entorno y restricciones vigentes

- Repo `/Users/gael/Documents/GitHub/AncoRA`; comprobar al empezar. Al cierre: `localizacion-offline`, HEAD `f84256fe0bbccd1acc00fce508c5a06a1039886b`, referencia remota dos commits por delante y muchos cambios locales sin commit.
- **Conservar todo: no reset/restore/revert, cambio de rama, worktree, commit, push ni pull.** No integrar automáticamente respaldo ni los dos commits remotos. [Respaldo remoto y alcance](LocalizacionOffline-RespaldoGitHub-2026-11-06.md). Capturas, resultados, pesos, entornos y grafo ignorados existen solo aquí; conservar originales y ensayos, incluidos negativos/preliminares.
- Usar siempre salidas nuevas y bases nuevas, sin reanudar bases parciales. Reutilizar `Tools/LocalizacionOffline/.venv/bin/python`, `.deps`, `.weights` y `salida/cache`. Matching PyTorch y SfM en procesos separados por OpenMP; esperar a que termine todo matching antes de reconstruir. No hace falta instalar nada para comenzar.
- Subagentes **6.1 Terra** autorizados para revisión. Sus revisiones no son validación física. Unity CLI/internet solo con tarea concreta; **no recompilar APK ni usar adb por rutina**.
- No usar GPS, Immersal, etiquetas de distancia ni poses Unity como verdad. Consultas y tablero fuera del mapa/ajuste por sesión y hash. No retirar cámaras ni escoger réplica/óptica para mejorar una cifra. No adoptar cuarentena/poda masiva que fragmentó el edificio.

## Lecturas de inicio

1. `CLAUDE.md`, Estado-y-Plan y este encargo.
2. `Docs/LocalizacionOffline-Ensayo78-2026-11-06.md`: nueva evidencia, resultado negativo de 0068 y reproducción.
3. `Docs/LocalizacionOffline-OrigenImagenes-2026-11-06.md` e `IdentidadTeologia-2026-11-06.md`: procedencia, importador y 71 v6; sus pendientes de siete/históricos fueron atendidos por el ensayo nuevo.
4. `Tools/LocalizacionOffline/README.md` y fuentes concretas usadas. Reutilizar herramientas existentes antes de crear otra cadena.
   Leer también `Docs/LocalizacionOffline-Soporte0068-2026-11-06.md`: revisión posterior ya ejecutada, límites de la máscara de 0066 y resultado negativo del SIFT dirigido.
5. Antes de cámara/escala: `OpticaTeologia-2026-11-06.md`, `CalibracionCasa2-2026-11-05.md` y `Medidas-2026-11-06.md`, todos bajo `Docs/LocalizacionOffline-…`.
6. `LocalizacionOffline-RevisionRamas-2026-11-05.md` solo si se considera alguna pieza remota. No aceptar su bandera temporal como prueba de sincronía.

## Qué se ejecutó y qué significa

**Objetivo pendiente:** localización offline a 10–25 m, barrido ≤5 s y alineación física 10–20 cm. La app conserva Immersal; no hay motor propio integrado ni ganador entre SIFT, SIFT+LightGlue, ALIKED+LightGlue y XFeat+LighterGlue.

**71 conservadas como baseline:** selección de 580 PNG de construcción de cinco recorridos de la tarde del 5-nov, tres Xiaomi 14 y dos 15T. Sus 71 anotaciones de fachada/identidad v6 se conservaron exactamente dentro de las 78. El ensayo añade las siete preseleccionadas de `143744`: 0002–0004, 0066–0067,0069–0070. No se eligieron por éxito. P/U1 identificada en las tres iniciales; en las otras cuatro la identidad de ranuras permanece desconocida. Se corrigieron oclusiones solo en máscaras nuevas antes de reconstruir, conservando versiones.

**Matching ejecutado por método:** 21 pares nuevos internos + 497 cruzados, 2.485 previos reutilizados compatibles, total 3.003. Firmas, hashes, coordenadas e índices comprobados; los 2.485 pares filtrados anteriores por método son numéricamente idénticos a v6. Las siete reciben la misma K/D candidata del 15T que las anteriores en radial; Xiaomi 14 intacto. No se rectifican PNG/keypoints.

**Cinco reconstrucciones desde bases nuevas:** XFeat baseline 69 cámaras/6.782 puntos, radial 69/7.055 y réplica radial 69/7.006; ALIKED baseline 66/3.007 y radial 66/3.052. Un componente retenido en cada una, no se suman componentes. Todos registran cinco nuevas (`0002,0003,0004,0066,0067`); ninguno `0069,0070`. Se conservan las 64/61 cámaras registradas previas de cada método. ALIKED sigue sin registrar `143744/0068`. Los modelos reales tienen K/D exactamente fijas como su selección. Hay avisos numéricos y conflictos conocidos en todos, conservados en logs e informes.

**0001 mejora localmente, sin aceptación:** dos radiales 78 difieren 0,219 % del diámetro/0,605°, frente a 14,64 % entre radiales 71; apoyo 166/169 observaciones, 151 índices comunes, 129 tracks completos iguales y 22 cambiados. 138/166 tracks de la primera radial contienen alguna de 0002/3/4. Dos ejecuciones próximas no excluyen otra solución ni prueban precisión física. Se conserva una asociación equivocada concreta de la réplica 71: kp19 incorpora ventanas distintas; no se eligió por eso todo el modelo inicial como correcto.

**0068 sigue abierta:** XFeat baseline/radial 78 difiere **11,667 % / 33,536°**; baseline 71→78 cambia 25,273°. Radial/réplica 78 difiere 0,729 % / 1,837°. Solo 38/38/39 observaciones, concentradas en dos o tres celdas; todas sin identidad conocida. Baseline/radial comparten 28 índices pero solo siete tracks completos iguales; radial/réplica comparten 36 y 29 tracks completos iguales. Solo cinco tracks de 0068 incluyen una nueva vista, siempre 0066 y además vistas antiguas. Añadir vecindad no aportó apoyo nuevo distribuido suficiente. No se atribuye el fallo solo a focal o falta de imágenes, ni se escoge radial por mejor residuo/repetibilidad.

**Históricos ya clasificados por separado:** 25 hashes únicos/no incluidos en los 182 candidatos anteriores, inspección completa y configuración registrada. Las 24 Samsung TEO-A pertenecen a una sesión de 51 PNG que incluye 27 CB y una referencia CB ya usada; la 15T E pertenece a 27 TEO, 26 candidatos previos/siete seleccionados SfM y otra referencia histórica. Función: diagnóstico/desarrollo por **sesiones completas**, sin cambiar particiones anteriores. Ninguna añadida a mapa/consultas, ninguna calibrada por transferencia de casa. No se ejecutó sensibilidad histórica porque no se identificó una carencia concreta que resuelvan. No son prueba final ciega.

**Aristas multivista avanzadas en PC:** se identificaron fotos candidatas de las mismas cuatro ventanas y propusieron segmentos 2D. Son trazos aproximados del agente, no extremos 3D equivalentes ni confirmación adicional del usuario. Hay oclusiones; faltan refinamiento y triangulación con cámaras aceptables. No se eligieron puntos 3D próximos por conveniencia ni se aplicó escala.

**Validación de herramienta:** 20 pruebas Python aprobadas de nuevo; comparación real de pares/índices, reproducción de anotaciones y auditoría de modelos/K/D. Graphify actualizado; cobertura parcial por formatos no soportados. Esto no valida precisión física.

## Entradas exactas para continuar

Rutas relativas a `Tools/LocalizacionOffline/`:

| Entrada | Contenido |
|---|---|
| `salida/20261106-soporte0068-02/` | Revisión posterior: completas/índices/tracks, `cuantitativo-v2/soporte.json`, `cobertura-0066-v2.json`, SIFT dirigido y sus negativos; versiones iniciales conservadas |
| `salida/20261106-soporte0068-independiente-02/REVISION.md` | Revisión visual independiente: kp138 unión estructural, kp60 posible sombra, kp584 montante sin ancla puntual; inicial con typo conservada |
| `salida/20261106-aristas-refinamiento-02/segmentos-refinados.json` | Nueve filas, ocho PNG únicos, trazos revisados y oclusiones; ±8 px manual no es precisión certificada ni extremos homólogos |
| `salida/20261106-ensayo-78/` | Selección78, máscaras/anotaciones nuevas y preliminares conservadas, cinco bases/modelos/logs, condiciones/fuentes |
| `salida/20261106-ensayo-78/resumen-resultados.json` y `comparaciones/` | Cámaras ausentes, apoyo por imagen, conflictos, diez comparaciones; no solo el máximo |
| `salida/20261106-ensayo-78/revision-tracks78/` | Tracks por `(imagen,índice)`, comparaciones 0001/0068, tablas de entrada/verificación y láminas; kp584 no basta para declarar falsedad física |
| `salida/20261106-ensayo-78/no-registradas-diagnostico.json` | 0069/0070: apoyo potencial en 3D final inferior a 30, pares brutos/filtrados/verificados y distribución |
| `salida/20261106-ensayo-78/observaciones-repetidas.json` | Tracks con más de un índice de la misma imagen, separaciones en píxeles; no confundir índices próximos con vistas desde centros independientes |
| `salida/20261106-78-auditoria-resultados/` | Revisión independiente de modelos, K/D, pares, comparaciones y avisos solver |
| `salida/20261106-78-revision-independiente/` | Revisión visual de máscaras y de fuentes; inicial/corregida conservadas |
| `salida/20261106-ambiguedad-v6-revision/` | Comparación de las dos radiales71 y enlace físico incorrecto kp19 |
| `salida/20261106-78-historicos-revision/` | 25 históricos, cinco láminas, 78 PNG de sus sesiones, metadatos, hashes y función |
| `salida/20261106-aristas-multivista-revision/asociaciones-propuestas.json` | Ventanas/aristas candidatas nuevas y trazos aproximados; no usar como triangulación ya validada |
| `salida/20261106-identidad-teologia/{xfeat,aliked}-{baseline,radial_k1}-pares-v6/` y `*-replica-v6/` | 71 baseline intactas y dos soluciones XFeat radiales |
| `experimentos/teologia-identidades-v6-20261106.json`, `teologia-71-regiones-20261105.json` | Anotaciones71 intactas; corregirlas solo como condición nueva explícita |
| `salida/20261105-calibracion-casa-2/ajuste/modelos.json` | K/D candidata exacta del 15T, radial segunda serie; Brown sensibilidad |
| `salida/20261106-identidad-teologia/aristas-interiores-confirmadas.json` | Cuatro ventanas y bordes interiores confirmados por Gael |

Orquestadores nuevos `experimentos/ensayo_78_20261106.py` y `analizar_78_20261106.py`; reutilizan `sfm_aprendido.py`, `sfm_diagnostico.py`, `conectar_recorridos.py` e `identidad_teologia_20261106.py`. No repetir todo el banco sin hipótesis nueva.

## Próximo trabajo concreto en PC

1. **Resolver el soporte físico de 0068 antes de escala, partiendo de la revisión ya ejecutada.** Se revisaron completas y recortes hacia 0066, 142623/0078 y 143857/0039–0049–0078. Los ejemplos son compatibles en ranura/montante; no se demostró confusión superior/inferior. Kp138 es candidato de unión estructural; kp60 puede seguir sombra; kp584 cambia de altura sobre arista lisa sin que se pueda elegir su homología puntual. Los cinco tracks nuevos solo refuerzan la ranura superior: la máscara de 0066 corta antes de la inferior visible. El SIFT dirigido a seis fotos con máscaras vigentes tampoco aportó distribución: 0–9 matches de fachada por par, puntos de muro sobre una misma junta lisa. No repetirlo como pendiente ni atribuir el fallo solo a focal/falta de fotografías. Próximo paso: revisar extremos estructurales de ambas ranuras en originales existentes y cadenas por desconocidos, diferenciando marco/vidrio/sombra. Si se corrige identidad/cobertura, condición nueva explícita con antes/después y todas las cámaras, sin reescribir 71/78 ni ampliar máscaras para forzar registro.
2. El diagnóstico final de 0069/0070 ya se ejecutó: 0069 aporta 21/26 índices 2D con algún apoyo 3D XFeat y 25/25 ALIKED; 0070, 0/0 XFeat y 28/27 ALIKED, todos por debajo de los 30 inliers mínimos de pose absoluta, con poca distribución. No es historia exacta del mapper ni prueba de identidad. Si se amplía soporte, identificar primero detalle/cobertura concreta con material existente y anotar sin aceptar primer plano; no relajar umbrales/máscaras para forzar registro ni pedir otra captura general.
3. El refinamiento manual en originales de nueve filas ya está ejecutado y conservado aparte; usar `segmentos-refinados.json` y `revision-principal.md`. Queda resolver especialmente ambigüedad del marco de H–I y oclusión de C–D en 143857/0116, validar mismas aristas multivista y reconstruir líneas/puntos con cámaras aceptables. La banda ±8 px no acredita menor incertidumbre que el borrador. No equiparar extremos de segmentos a puntos físicos homólogos ni ajustar métricamente porque la consulta/importación lo requiera.
4. Con geometría aceptable: escala solo C–D y evaluación posterior de las tres reservas exploratorias. Si C–D no tiene soporte, informar el bloqueo concreto; no absorber una reserva ni escoger óptica/modelo silenciosamente con sus valores.

## Medidas y paso posterior hacia la app

Gael confirmó las cuatro líneas verdes y ventanas: anchuras interiores sin hormigón. **No pedir otra identificación de las mismas por rutina.** C–D=2,70 m, ventana baja junto a puerta, es la única longitud de ajuste exploratorio. R–S=6,20 m, H–I=2,70 m y vuelta/frente=1,95 m siguen reservadas. Son controles de desarrollo conocidos, no evaluación final ciega. Altura de cinta/repeticiones/incertidumbre siguen no informadas; no inventarlas. A–B/E–F no medidos por altura y G descartado: no pedir alcanzarlos. No hace falta otro tablero general del 15T.

El mapa permite investigar tracks/pose a escala arbitraria; no ubicar físicamente a 10–20 cm. Para consulta real falta un mapa/cámaras/escala aceptables y adaptar importación/triangulación/PnP/proyección al contrato crudo K/D, incluida salida −0,5 desde COLMAP, o rectificar y reextraer coherentemente. El localizador existente sigue exigiendo PINHOLE rectificado/D=0: no alimentarlo con OPENCV crudo y `meters-per-unit=1` ficticio.

Después: consulta de una imagen/sesión separada en PC, comparación de los cuatro candidatos de extremo a extremo y puerto/medición Android de extractor/matcher/mapa/PnP/contenido. Una exportación de extractor no equivale a motor completo. Multicuadro exige además conversión óptica y **sincronía real**: timestamp pose copia cámara; el caso de casa 22:54:49 mantiene ~6° con nueva calibración. No aceptar bandera remota `alinear-arcore` ni Unity como prueba física.

## Cierre siguiente

Ejecutar trabajo concreto, conservar evidencia y negativos, actualizar Estado-y-Plan y este encargo. Explicar qué cambió, qué vistas ayudaron/no, estado de 0001/0068, alcance real del mapa y qué falta para app. No pedir material al usuario mientras haya comprobaciones independientes en PC; pedir aclaración física solo cuando sea concreta y necesaria. Actualmente **no se requiere otro envío de Gael para continuar el diagnóstico**.
