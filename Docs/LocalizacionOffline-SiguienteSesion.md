# AncoRA — encargo vigente para la siguiente sesión

**Pausa y respaldo autorizado (6-nov):** código y documentos en la rama `codex/respaldo-localizacion-offline-20261106`, sin integrar los dos commits remotos ni cambiar el estado local. Las capturas/resultados/pesos/entornos ignorados permanecen solo en este computador: conservarlos. [Alcance del respaldo y cómo retomar](LocalizacionOffline-RespaldoGitHub-2026-11-06.md).

Actualizado al cierre del **6-nov-2026**. Este encargo reemplaza al [del 5-nov, conservado íntegro](LocalizacionOffline-SiguienteSesion-2026-11-05-Historico.md). El punto de entrada es [Estado-y-Plan](LocalizacionOffline-Estado-y-Plan.md); el [plan original](LocalizacionOffline-PlanOriginal.md) permanece sin cambios.

**El siguiente chat debe ejecutar trabajo en PC, no repetir solamente este resumen o volver a hacer el inventario general.** Los ensayos de 78 vistas y de los 25 históricos todavía no se ejecutaron. No confundir preparación con resultado.

## Estado del entorno y límites que siguen vigentes

- Repositorio: `/Users/gael/Documents/GitHub/AncoRA`. Comprobar el estado al comenzar. Al cierre: rama `localizacion-offline`, HEAD `f84256fe0bbccd1acc00fce508c5a06a1039886b`, referencia local remota dos commits por delante y muchos cambios sin commit.
- **Conservar todo. No reset, restore/revert de cambios, cambio de rama, worktree, commit, push ni pull.** No integrar de golpe los dos commits remotos; se solapan con cambios locales. `nicowo` ya es ancestro.
- Usar salidas nuevas. Conservar originales, selecciones, características, anotaciones, bases, modelos, resultados negativos y procedencia. Los datos/pesos/entorno/salidas están ignorados por Git y existen en este computador; no asumir que aparecen al clonar el remoto.
- Entorno existente: `Tools/LocalizacionOffline/.venv/bin/python`, `.deps/`, `.weights/` y `salida/cache/`. La última revisión no necesitó instalar nada. Matching PyTorch y SfM/COLMAP deben correr en procesos separados por OpenMP. Esperar a que termine el matching antes de reconstruir; no reutilizar bases parciales/existentes.
- Gael autorizó subagentes **6.1 Terra** para revisar si hacen falta. Pueden revisar identidad, cambios y resultados; su revisión no acredita precisión física ni reemplaza comprobar los artefactos.
- Unity CLI/internet pueden usarse si una tarea concreta lo necesita. **No recompilar APK ni usar adb por rutina.** Primero ejecutar lo independiente en PC; pedir instalación adicional solo si realmente falta una dependencia necesaria.
- No usar poses Unity, GPS, etiquetas de distancia o Immersal como verdad de terreno. Mantener consultas/tablero fuera del mapa y de su ajuste. No retirar cámaras ni elegir una réplica para mejorar una cifra.

## Lectura de inicio

1. `CLAUDE.md`, `Docs/LocalizacionOffline-Estado-y-Plan.md` y este archivo.
2. `Docs/LocalizacionOffline-OrigenImagenes-2026-11-06.md`: procedencia actual, importador corregido y material preparado.
3. `Docs/LocalizacionOffline-IdentidadTeologia-2026-11-06.md`: correcciones, ensayos negativos, v6 y réplica inestable.
4. `Tools/LocalizacionOffline/README.md` y los scripts concretos que se usarán.
5. Antes de cámara/escala: `Docs/LocalizacionOffline-OpticaTeologia-2026-11-06.md`, `Docs/LocalizacionOffline-CalibracionCasa2-2026-11-05.md` y `Docs/LocalizacionOffline-Medidas-2026-11-06.md`.
6. Consultar `Docs/LocalizacionOffline-PlanOriginal.md` para los objetivos, y `Docs/LocalizacionOffline-RevisionRamas-2026-11-05.md` si se considera alguna pieza remota.

`LocalizacionOffline-Continuacion.md` y el encargo antiguo del 5-nov son historia: no reiniciar sus instrucciones de captura ni su selección preferida de motor.

## Qué ya se ejecutó

**Objetivo intacto:** ubicación completamente offline a 10–25 m, barrido de hasta 5 s y alineación física de 10–20 cm. No demostrado. La app conserva Immersal; no hay un motor nuevo integrado ni ganador elegido entre SIFT, SIFT+LightGlue, ALIKED+LightGlue y XFeat+LighterGlue.

**Datos:** 71 vistas seleccionadas de 580 PNG de construcción de Teología de la tarde del 5-nov, cinco recorridos: tres del Xiaomi 14 y dos del 15T. Primero fueron 65 ordinales y después seis intermedias. Los originales proceden de `Descargas/ancora ultimas grabacoines`; se usan copias conservadas y hashes. Las otras 509 no fueron eliminadas. `Descargas/Ancora grabaciones` contiene material anterior de la mañana ya revisado, fuera de las 71 actuales.

**Óptica:** calibración candidata estable del 15T con 487 PNG de tablero. Radial k1 de la segunda serie es la base y Brown la sensibilidad; valores exactos en `salida/20261105-calibracion-casa-2/ajuste/modelos.json`. Se ejecutaron 16 condiciones ópticas nuevas antes de las de identidad. SfM admite PINHOLE/OPENCV/FULL_OPENCV con K/D fijas, sobre PNG y keypoints crudos. El +0,5 se aplica conjuntamente a keypoints y centro principal al entrar a COLMAP. No se rectificaron imágenes; reutilizar sus características es compatible. No transferir esta calibración al Xiaomi 14, Samsung ni a históricos del 15T automáticamente. No hace falta repetir otra serie general de tablero del 15T.

**Identidad:** 34 reconstrucciones exploratorias y cuatro réplicas finales, filtros por ventanas/caras, pruebas de cuarentena y grafos; resultados y fuentes conservados. La v6 mejora el acuerdo entre métodos tras anotar la ventana corta lateral de `142828/0111`, sin retirar esa cámara: el máximo entre XFeat/ALIKED baja de ~94–98° en v5 a ~3,5° en las primeras corridas v6. No demuestra cuál pose es físicamente correcta.

Primeras corridas v6: XFeat 64 cámaras / 6.933 puntos baseline o 6.913 radial; ALIKED 61 / 2.749 o 2.758. Aún hay tracks incompatibles mediante desconocidos y sensibilidad óptica hasta 6,10° en XFeat. **Tres réplicas reproducen la geometría, pero XFeat radial encuentra otra solución:** `143744/0001` cambia 14,64 % del diámetro de centros y `/0068` hasta 3,75°, con iguales entradas/semilla. Conservar ambas; no escoger por reproyección.

Aislar todos los componentes ambiguos del grafo evita conflictos conocidos, pero elimina la mayor parte de las conexiones y fragmenta el edificio en mapas pequeños. Es un **resultado negativo**, no una solución a adoptar ni un motivo para sumar componentes como mapa. Las máscaras viejas de fachada también incluyen algunos objetos/fondos ajenos; desconocido no significa falso ni correcto. No repetir todos estos ensayos sin una hipótesis nueva.

**Importación histórica corregida:** el lector ignoraba `intentos (1).jsonl`. Ahora inventario y auditoría comparten lectura de copias numeradas, deduplicación y rechazo de metadatos contradictorios; la auditoría incluye todos los roles para no introducir consultas en construcción. 350 PNG del 28-oct tienen metadatos; cero huérfanos. Se conservan 20 referencias a PNG ausentes de CB, no bloqueantes para Teología. Última suite: **20 pruebas Python aprobadas**.

Se recuperaron 25 imágenes de Teología que no estaban en el conjunto histórico usado: 24 `TEO-A-15m` Samsung de `20261028-113656` y una `TEO-E-15m` 15T de `20261028-115641`. **Rol sin asignar; no se han añadido a construcción ni consultas.** Los JPG de los registros comprobados son 270×480: apoyo visual, no sustitutos automáticos de PNG calibrados.

## Entradas precisas para continuar

Todas las rutas de la tabla parten de `Tools/LocalizacionOffline/`.

| Entrada | Uso |
|---|---|
| `salida/20261106-identidad-teologia/{xfeat,aliked}-{baseline,radial_k1}-pares-v6/` | Modelos v6 y sus selecciones/condiciones, características, matches, bases y diagnósticos |
| `salida/20261106-identidad-teologia/{xfeat,aliked}-{baseline,radial_k1}-replica-v6/` | Réplicas; especialmente XFeat radial inestable |
| `salida/20261106-identidad-teologia/comparaciones-cierre/`, `resumen-reconstrucciones.json`, `resumen-comparaciones.json` | Comparaciones y todos los componentes conservados; el mayor por número de cámaras es solo una regla de informe |
| `salida/20261106-identidad-teologia/revision-puentes/` | Fotos, crops, caminos por índices y manifiesto de enlaces físicamente incompatibles |
| `experimentos/teologia-identidades-v6-20261106.json` | Anotaciones de identidad actuales; v1–v5 conservadas, no sobrescribirlas |
| `experimentos/teologia-71-regiones-20261105.json` | Regiones de fachada de las 71; parciales, no verdad semántica completa |
| `salida/20261105-revision-camaras/ampliado-{xfeat,aliked}/` | Características y matching aprendido de 71 antes del filtro de fachada/identidad; revisar firmas y coordenadas antes de reutilizar |
| `salida/20261105-recorridos/TEO-disponibles.json` y `originales/` | Los 580 PNG y exclusiones originales; conservar consultas por sesión/hash |
| `salida/20261106-origen-imagenes/siete-vistas-intermedias-candidatas.json` | Siete PNG de construcción preparados/verificados; aún sin matching/reconstrucción |
| `salida/20261106-origen-imagenes/teologia-historicas-25-por-clasificar.json` | 25 históricos recuperados, rol sin asignar |
| `salida/20261106-origen-imagenes/historicas-inventario-corregido.json`, `historicas-todos-jsonl.json`, `comprobacion.json` | Metadatos recuperados, procedencia y verificación de Descargas |
| `salida/20261106-identidad-teologia/aristas-interiores-confirmadas.json` y `aristas-interiores-propuestas.jpg` | Cuatro bordes interiores confirmados por Gael, con coordenadas crudas; la imagen conserva su nombre de borrador |
| `salida/20261105-calibracion-casa-2/ajuste/modelos.json` | K/D exactas del 15T; radial de segunda como candidato simple |

Reutilizar `sfm_aprendido.py`, `sfm_diagnostico.py`, `experimentos/conectar_recorridos.py`, `identidad_teologia_20261106.py` y herramientas conservadas antes de crear otra cadena. No confundir firmas/índices de XFeat y ALIKED.

## Trabajo prioritario: ensayo dirigido de 71 a 78 vistas

1. Comparar las dos soluciones radiales v6 de `143744/0001` y `/0068`: tracks compartidos/cambiados, identidad física, profundidad/paralaje y distribución de apoyo. Revisar las fotos antes de atribuirlo solo a focal, movimiento o ausencia de material. Conservar las cámaras en entradas e informar cuáles se registran.
2. Abrir las siete candidatas del recorrido `construccion-20261105-143744-192263eb`: **0002, 0003, 0004, 0066, 0067, 0069 y 0070**. Están dentro de los 580 PNG existentes. Comprobar continuidad visual y crear anotaciones propias de fachada/identidad, dejando incierto como desconocido. Mantener las 71 anotaciones v6 anteriores sin cambios dentro del ensayo ampliado, salvo que una corrección visual se documente como condición aparte.
3. Preparar una selección explícita de 78 si las siete son aptas; no elegirlas por éxito de reconstrucción. Si alguna no aporta cobertura, documentarlo con foto/razón y conservar el ensayo, sin retirarla para mejorar un indicador. Las 71 siguen como baseline.
4. Extraer/matchear las siete por método y calcular cruzados compatibles. Exhaustivo 78 = 3.003 pares; frente a 71 son **21 entre nuevas + 497 cruzados = 518 pares adicionales por método**, si se conserva ese grafo. Reutilizar pares anteriores solo tras comprobar hashes, firmas, coordenadas e índices; no reutilizar bases geométricas como si el cambio de entradas no afectara la verificación.
5. Mantener compatibles imagen, K, distorsión y coordenadas en todo el conjunto. **En la condición radial, las siete del 15T deben recibir el mismo candidato de K/D que las anteriores de esa configuración; no mezclar 71 calibradas con siete priors registrados dentro de la misma comparación.** Baseline y radial siguen separados, y Xiaomi 14 conserva su configuración. Si se rectifica alguna imagen, no reutilizar características/correspondencias incompatibles.
6. Reconstruir en bases/salidas nuevas con condiciones documentadas; comparar XFeat/ALIKED y baseline/radial sobre las mismas vistas. Hacer comprobaciones/repeticiones pertinentes para la ambigüedad observada, no repetir todo el banco por rutina. Informar también cámaras no registradas, componentes solapados, pérdida de apoyo y resultados negativos.

**Entregable:** ensayo nuevo realmente ejecutado, evidencia por cámara/enlace y decisión sobre si disminuye o persiste la ambigüedad. La ausencia de conflictos conocidos o un buen residuo no bastan para aceptar precisión física. No adoptar la poda de componentes gigantes ni un orden greedy de enlaces solo porque dé mejores poses.

## Trabajo independiente: revisar los 25 históricos recuperados

- Ver imágenes completas, metadatos, hashes y configuración; comprobar duplicados/solapamientos con los datos antiguos y la aportación visual concreta. No unirlos todos de golpe con el candidato principal.
- Fijar su función antes de matching/ajuste, conservando las particiones existentes por **sesiones completas y hash**. La imagen 15T E pertenece a una sesión histórica parcialmente usada antes: no presentarla como consulta independiente de esa sesión. La sesión Samsung A comparte carpeta/sesión con otras etiquetas de edificio; revisar el conjunto.
- No suponer que una imagen vieja del 15T comparte foco, recorte, orientación o calibración con casa. Mantener cámaras/configuraciones separadas. Los nombres de archivo no proporcionan K, tiempo, pose ni rol.
- Crear una sensibilidad histórica separada solo si una carencia concreta y la inspección justifican su incorporación. Los JPG de registro pueden ayudar a identificar detalles, pero no se les aplica K de los PNG sin comprobar su transformación.

**Entregable:** función documentada, evidencia de utilidad o límite de cada grupo y, si corresponde, ensayo nuevo separado. No llamar prueba final ciega a datos ya inspeccionados.

## Medidas: confirmación vigente y paso métrico pendiente

Gael confirmó ventanas y **las cuatro líneas verdes** de la lámina: anchos izquierda→derecha entre bordes internos, sin hormigón. No volver a pedirle identificar esas mismas ventanas por rutina.

| ID | Valor | Uso fijado antes de escala |
|---|---:|---|
| INTERIOR-C-D | 2,70 m | Única longitud de ajuste exploratorio, baja junto a la puerta |
| INTERIOR-R-S | 6,20 m | Reserva exploratoria, ventana larga baja |
| INTERIOR-H-I | 2,70 m | Reserva exploratoria, primera baja a la derecha de la esquina en panel 3 |
| INTERIOR-VUELTA-FRENTE | 1,95 m | Reserva exploratoria, baja a la izquierda de esa esquina |

Los círculos históricos de hormigón/aleros no equivalen a estos bordes. Se explicitó el cambio de INTERIOR-C-D a ajuste; nunca se presentará después como validación independiente. Las reservas son de desarrollo, ya conocidas, no una evaluación final ciega. **A–B/E–F no se midieron por altura y G está descartado: no pedir alcanzarlos.**

No se calculó ni aplicó escala. Los segmentos actuales identifican aristas, no altura de cinta ni puntos XYZ. Falta localizar las mismas aristas en varias vistas y reconstruirlas con cámaras aceptables, sin escoger puntos 3D cercanos por conveniencia. Incertidumbre/repeticiones/altura siguen no informadas; no inventarlas ni afirmar 10–20 cm por tener decimales. Completar la asociación 2D/3D en PC y pedir una aclaración física solo cuando sea concreta y necesaria. Ajustar solo C–D y evaluar las tres reservas después; no usar reservas para elegir silenciosamente modelo, escala o umbrales.

## Después: localización real e integración

Con mapa, cámaras y escala aceptables, adaptar la ruta de importación/triangulación/PnP/proyección para imagen cruda y K/D compatibles (incluida salida −0,5 desde COLMAP), o rectificar y reextraer de forma coherente. **El cambio ejecutado hasta hoy admite distorsión en SfM; el importador/localizador/multicuadro existente todavía exige PINHOLE rectificado/D=0.** No darle modelos arbitrarios con `meters-per-unit=1` para aparentar prueba métrica.

Después ejecutar localización de una imagen/sesión reservada en PC, comparar los cuatro candidatos de extremo a extremo y portar/medir en Android los que se justifiquen. No se ha elegido XFeat por eficiencia ni por una exportación ONNX; extractor exportado no equivale a matcher, mapa, PnP y motor completo.

Multicuadro requiere además validar conversión óptica y sincronía real: `timestamp_pose_sensor` copia tiempo de cámara y no demuestra cuándo se muestreó Unity. El barrido de casa de las 22:54:49 sigue discrepando ~6° con la nueva calibración. No aceptar la bandera del `alinear-arcore` remoto: hay un contraejemplo de sincronía no identificable documentado. La app y colocación de contenido requieren su propia integración/validación; Unity CLI no reemplaza comprobar los datos físicos.

## Cierre de la siguiente sesión

Actualizar Estado-y-Plan y el encargo vigente con resultados nuevos y límites. Explicar en palabras simples: qué se ejecutó y cambió, qué ocurrió con las dos cámaras dudosas, si las siete/los históricos ayudaron, qué permite el mapa, qué falta para la app y si se necesita algo concreto de Gael. Distinguir herramienta/sintético, estabilidad interna y precisión física. Conservar hashes/fuentes y todos los ensayos. No detenerse en otro plan general mientras haya trabajo independiente autorizado por ejecutar.
