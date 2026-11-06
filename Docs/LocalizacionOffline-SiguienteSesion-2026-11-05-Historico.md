# AncoRA — encargo vigente para la siguiente sesión

Preparado el 5-nov-2026 después de la auditoría y reconstrucción de los recorridos de la tarde.
Gael pidió dejar el plan listo y continuar en un chat nuevo, avanzando todo lo posible para salir a probar la app.
**Este encargo es para ejecutar trabajo, no solo proponer otro plan.**

## Entorno y conservación

- Repositorio local: `/Users/gael/Documents/GitHub/AncoRA`, rama `localizacion-offline`.
- Hay cambios locales extensos, incluidos scripts y documentos sin commit. Inspeccionar estado y conservarlos.
  No restaurar a `f84256f`, no hacer reset, descartar cambios, cambiar de rama por rutina ni crear un worktree.
- No usar prefijo `codex/`, coautorías ni atribuciones al asistente. No hacer commits/push por rutina.
- Conservar originales y ensayos. Usar una salida nueva para los siguientes experimentos; no sobrescribir bases.
- No recompilar el APK sin una razón concreta. No instalar, abrir la app por adb, desinstalar ni borrar datos por rutina.

## Lecturas de inicio

1. `CLAUDE.md` y `Docs/LocalizacionOffline-Estado-y-Plan.md`.
2. `Docs/LocalizacionOffline-Recorridos-2026-11-05.md`: resultado final de esta sesión.
3. `Tools/LocalizacionOffline/README.md` y los scripts que se vayan a usar.
4. `Docs/LocalizacionOffline-PlanOriginal.md`: requisitos conservados.
5. Para comparar antecedentes, `Docs/LocalizacionOffline-Geometria-2026-11-05.md`.
   La guía `LocalizacionOffline-PruebaConstruccion.md` documenta el APK de captura ya usado; no exige repetir la salida.

Antes de modificar Unity, consultar el contexto y las instrucciones pertinentes del proyecto y seguir su validación.
`LocalizacionOffline-Continuacion.md` es un encargo histórico ya ejecutado; no reiniciarlo.

## Punto de partida comprobado

El objetivo sigue siendo localización visual **offline**, a 10–25 m, con barrido de hasta 5 s y alineación de 10–20 cm.
Todavía no se ha demostrado. La demo conserva Immersal; no se eligió el motor Android nuevo.

Hay 31 sesiones nuevas íntegras, 1.900 PNG únicos: 12 recorridos y 19 consultas. Teología aporta 580 imágenes de
construcción y 107 de consulta. Todo lo recibido está procesado; no esperar otras carpetas. Los dos barridos finales
de X1 citados solo en el log no bloquean Teología. Las exportaciones sueltas de Descargas cambiaron de ubicación
durante el análisis; usar las copias protegidas, no depender de esas rutas iniciales.

Salida principal: `Tools/LocalizacionOffline/salida/20261105-recorridos/` (ignorada por Git).

| Entrada o evidencia | Uso |
|---|---|
| `originales/`, `procedencia.json`, `zip-miembros.json`, `verificacion-fuentes.json` | Copias verificadas y procedencia; 2.392 archivos conservados, incluidos ZIP/logs |
| `auditoria.json`, `inventario.json`, `calidad-imagenes.json` | Auditoría real de sesiones, tiempos, intrínsecos e imágenes |
| `TEO-disponibles.json`, `TEO-seleccion-densa.json` | 580 vistas de construcción; selección fija de 65 vistas, 13 por sesión |
| `consultas.json`, `ambiguas.json` y exclusiones en cada manifiesto | Separación completa por sesión y hash; 510 consultas nuevas excluidas del mapa |
| `teo-denso-xfeat/`, `teo-denso-aliked/` | Matching completo sobre las mismas 65 vistas |
| `teo-denso-xfeat-fachada-v2/`, `teo-denso-aliked-fachada-v2/` | Candidatos finales filtrados por fachada; punto de partida principal |
| `consistencia-v2-65-xfeat-aliked-fachada.json` | Cámaras discrepantes entre métodos, con ancla explícita |
| `consistencia-v2-65-*-completo-fachada.json`, `consistencia-v2-35-65-fachada.json` | Sensibilidad a primer plano y densidad |
| `teo-union-xfeat-fachada-v2/`, `consistencia-v2-mascara-union.json` | Diagnóstico histórico inestable; mantener separado del candidato principal |
| `fuentes/`, `fuentes-sha256.json`, `auditoria-local-final.json`, `artefactos-finales-sha256.json` | Fuentes, entorno, verificaciones y hashes del cierre anterior |

Usar **v2**: la revisión ampliada detectó una lámpara/persona/tablero y otro poste que requerían exclusiones más
amplias. Se conservaron las máscaras/modelos preliminares y se repitieron las cinco reconstrucciones filtradas.
Máscaras finales en `Tools/LocalizacionOffline/experimentos/teologia-recorridos-densa-regiones-v2-20261105.json`.
No interpretar las salidas sin v2 como las cifras finales. Las regiones son manuales y parciales; arquitectura al
fondo no acredita la identidad del edificio de cada punto. Revisar imágenes antes de tratar CB/X1 como negativos puros.

Resultado denso final: XFeat conserva 58 cámaras y 5.824 puntos de fachada; ALIKED, 55 cámaras y 2.330 puntos.
Comparten 55 cámaras, con discrepancia mediana de centros de 0,445 % del diámetro. Hay mejora mayoritaria, pero quedan
casos concretos que impiden aceptar todas las cámaras. La unión histórica cambia unos 51° al corregir máscaras.
**Porcentajes y ángulos son estabilidad interna, no error físico ni centímetros.**

Casos iniciales a investigar, usando sus vecinas temporales originales:

- `construccion-20261105-143744-192263eb/0001-TEO-B-10m.png`: 13,51 % del diámetro y 5,96° entre métodos.
- `construccion-20261105-143744-192263eb/0011-TEO-B-10m.png`: discrepancia menor que también conviene revisar.
- `construccion-20261105-142828-6c2403ae/0011-TEO-C-15m.png` y `0020-TEO-C-15m.png`: este último cambia 4,45°.
- `0116-TEO-C-15m.png` de `142828`: 79,53° entre selecciones completas de 35/65 vistas; diagnóstico de muestreo.

Entorno y herramientas existentes: `Tools/LocalizacionOffline/.venv/bin/python`, `.deps/`, `.weights/` y
`salida/cache/`. Reutilizar `sfm_aprendido.py`, `sfm_diagnostico.py`, `auditar_recorridos.py` y los experimentos
existentes antes de crear herramientas. Matching PyTorch y SfM pycolmap van en procesos separados por OpenMP.
Esperar a que termine cada matching antes de reconstruir. Hay 15 pruebas Python aprobadas y 17 ensayos SfM completos;
eso verifica herramientas y ejecución, no precisión física. No repetir todo el banco sin una pregunta concreta.

## Trabajo a ejecutar y entregables

**1. Consolidar la geometría nueva.** Abrir los casos anteriores, revisar tracks, distribución, estructuras repetidas,
primer plano y vistas vecinas. Probar la hipótesis concreta de conexión/cámara con continuidad temporal, manteniendo
comparables los métodos y las imágenes. No retirar una cámara solo porque mejora un indicador. Conservar baseline,
decisión por cámara, selección, máscaras, parámetros, hashes y evidencia visual. No forzar la unión con históricas.
Entregar candidato reproducible, cobertura real declarada y discrepancias restantes, o el enlace exacto aún no resuelto.

**2. Avanzar toda validación de cámara y preparación de integración que permita el PC.** Revisar orientación del PNG,
intrínsecos registrados, distorsión/rectificación, tiempos y conversión sensor/Unity/OpenCV; no declarar validada una
pose solo por consistencia de metadatos. Trazar el flujo existente de mapa, PnP, secuencia, ARCore y colocación de
contenido para identificar el cambio mínimo que permitiría un prototipo Android. Preparar código/exportación o
comprobaciones sintéticas cuando un paso concreto lo justifique. Mantener explícito qué exige hardware o medidas.

**3. Preparar una visita útil sin pedir otra captura general.** Elegir detalles físicos permanentes y distinguibles
sobre fotografías existentes; identificar controles distribuidos y otros independientes reservados para comprobar.
Preparar una hoja con fotografías/identificadores, qué distancias medir, unidades, procedimiento, incertidumbre que
se necesita y campos vacíos para valores reales. Añadir protocolo de calibración/cámara si lo exige el diagnóstico,
teléfono/mode de captura y consultas independientes. No inventar dimensiones ni fijar cantidades sin justificar
cobertura y accesibilidad. La salida debe resolver una carencia precisa, no repetir los recorridos por rutina.

**4. Continuar hacia localización y Android cuando se cumplan los requisitos.** Solo con geometría, escala y cámaras
válidas ejecutar PnP/multicuadro reales y comparar una imagen con el barrido. Mantener SIFT, SIFT+LightGlue,
ALIKED+LightGlue y XFeat+LighterGlue abiertos como candidatos. Integrar y medir en Android lo que esté justificado;
ningún ganador en PC equivale al motor elegido. Si un control físico impide continuar, completar antes las tareas
independientes y dejar preparado el kit concreto para obtenerlo. Una primera visita puede medir/calibrar y todavía
no probar el nuevo localizador con precisión final.

Mantener escala arbitraria hasta disponer de controles. No usar etiquetas de distancia, poses Unity, GPS o Immersal
como verdad de terreno. No alimentar estos modelos a umbrales en metros ni al ajuste temporal métrico. No ajustar el
mapa con consultas y después presentar su resultado como prueba ciega. No incluir funciones de demo ajenas.

Para un APK nuevo debe existir una razón concreta: corrección comprobada, captura/instrumentación necesaria o
integración lista para verificar. Registrar qué prueba permite, validación realizada y SHA-256. El APK actual
`Builds/Android/AncoRA-Construccion-Offline.apk` es de captura, versión heredada 1.5.3 (28), no del nuevo localizador.
SHA-256 conservado: `ef655eaad4fc147e94d2385083e7db3d8a52dfb405a1f64b1707070a4b0fb543`.

Al terminar, actualizar Estado-y-Plan con resultados y siguientes pasos reales. Explicar primero el resultado en
lenguaje claro; distinguir herramienta implementada, prueba de software, geometría estimada y validación física.
Entregar instrucciones concretas de la próxima salida o del APK si está justificado y listo. No detenerse solo para
preguntar si se desea continuar trabajo ya autorizado; no simular los datos físicos que falten.
