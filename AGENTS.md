# AncoRA: reglas para agentes

## Objetivo y alcance

Mantener separadas la versión **DEV** (trabajo del equipo) y la versión **NORMAL**
(producto para el usuario). La versión normal debe sentirse terminada: mostrar solo
contenido y funciones completos, sin herramientas internas ni experimentos visibles.
Estas reglas aplican a todos los agentes que modifiquen este repositorio.

Leer también `CLAUDE.md` para el runbook y verificar el estado actual en el código.
Las notas históricas de demos no convierten una función en apta para NORMAL. Ante
una diferencia, estas reglas rigen la separación DEV/NORMAL; la instrucción explícita
del usuario tiene prioridad.

## Estado comprobado al 25-nov-2026

- `PaseoSetup.BuildAndroid` y las variantes de captura pasan por `BuildAndroidTo`,
  que usa siempre `BuildOptions.Development`. **Todavía no existe una compilación
  NORMAL.** El APK habitual `Builds/Android/AncoRAPaseo.apk` es de desarrollo.
- `PaseoDebug.Enabled` usa `Debug.isDebugBuild` y `PaseoRecorder.Available` depende
  de esa condición. Sin embargo, `PaseoUi` crea la hoja Equipo y su gesto de acceso
  sin comprobar el perfil. Quitar `BuildOptions.Development` por sí solo no basta.
- La demo y el grabador dedicado usan `com.ancora.ucnar.paseo`; instalar uno puede
  reemplazar al otro. `ANCORA_CAPTURE` se aplica solo al build del grabador.
- Estas instrucciones definen el contrato a implementar. **Editar documentación
  no significa que la separación técnica ya exista.** No presentar el APK actual
  como producto final ni documentar comandos de release como si ya funcionaran.

## Qué pertenece a cada versión

| Aspecto | DEV | NORMAL |
| --- | --- | --- |
| Interfaz | Herramientas del equipo y experimentos identificados | Recorrido completo con textos y controles listos |
| Diagnóstico | Equipo, mapas/IDs, confianza, tiempos, resolución de prueba, registros detallados | Mensajes claros de carga, permisos, búsqueda y recuperación, sin métricas internas |
| Ajustes de campo | Cajas, bordes, pose, escala, selección manual y restauración de ajustes | Configuración validada, sin controles de calibración para el visitante |
| Captura técnica | Fotos para localización, datasets ARCore, grabación y exportación del equipo | Sin captura técnica ni procesos de diagnóstico en segundo plano |
| Contenido | Maquetas, edificios/épocas en prueba y comparaciones | Solo edificios, modelos y épocas validados para esa entrega |

Una función de foto o video pensada para el usuario puede entrar a NORMAL cuando
esté completa y validada; no reutilizar por ello el flujo técnico del grabador.

## Reglas al implementar cambios

1. **Toda herramienta técnica o función incompleta nueva queda en DEV por
   defecto.** Mantener una selección explícita de funciones/contenido habilitados
   para NORMAL; si falta configuración o no se reconoce el perfil, no habilitar
   experimentos. No usar la rama, un nombre de APK o un `PlayerPrefs` como único
   selector del perfil.
2. Centralizar la decisión de perfil. Reutilizar `PaseoDebug.Enabled` para las
   capacidades de diagnóstico y usar `UNITY_EDITOR` / `DEVELOPMENT_BUILD` donde
   corresponda excluir código. Evitar condiciones distintas y contradictorias
   repartidas por la interfaz. `ANCORA_CAPTURE` es exclusivo de captura DEV y debe
   rechazarse al compilar NORMAL.
3. En NORMAL no crear ni conectar la hoja Equipo, sus gestos, paneles de prueba,
   ajustes de pose/tamaño, capturas técnicas o exportadores. Tampoco iniciar sus
   tareas, suscripciones, escrituras ni permisos exclusivos de esas herramientas.
   Un menú secreto, un botón invisible o CSS no son una separación suficiente.
4. Conservar las herramientas útiles de DEV al limpiar el producto. Compartir la
   lógica de localización y las vistas terminadas; evitar dos copias completas de
   la app que después diverjan. Mantener herramientas de Editor bajo `Editor/`.
5. No mostrar en NORMAL botones sin acción, secciones vacías, «próximamente»,
   datos de ejemplo, cajas de depuración ni modelos provisionales como contenido
   final. Si un edificio o una época no están listos, excluirlos de sus selectores
   y del flujo normal. No cambiar silenciosamente a otra época o maqueta para
   simular que el contenido existe. No presentar un modelo genérico como una
   reconstrucción histórica verificada.
6. Mantener estados reales de permisos denegados, sin ubicación, carga y error,
   con una explicación y una salida útil para el usuario. Nunca ocultar un fallo
   necesario ni inventar éxito para que la app parezca terminada. NORMAL no debe
   requerir que el visitante abra Equipo o calibre manualmente para completar el
   recorrido previsto.
7. `edificio.json` y los generadores son la fuente de la escena. Cualquier
   separación de contenido debe sobrevivir a `PaseoSetup.Prepare`; no resolverla
   solo desactivando objetos a mano en `PaseoIngenieria.unity`. No alterar mapas,
   transformaciones o alineaciones medidas para cambiar la presentación.

## Contrato de compilación e instalación

Al implementar los perfiles, ofrecer entradas explícitas para ambos en
`PaseoSetup` y en el runbook. Convención de destino:

| Perfil | Nombre instalado | Identificador Android | Salida |
| --- | --- | --- | --- |
| DEV | AncoRA Paseo DEV | `com.ancora.ucnar.paseo.dev` | `Builds/Android/Dev/AncoRA-Paseo-Dev.apk` |
| NORMAL | AncoRA Paseo | `com.ancora.ucnar.paseo` | `Builds/Android/Release/AncoRA-Paseo.apk` |

- DEV usa `BuildOptions.Development` y una indicación DEV visible para el equipo.
  NORMAL se compila sin `Development`, `AllowDebugging`, `ConnectWithProfiler` ni
  símbolos de captura/experimentos. No convertir `BuildAndroid` silenciosamente en
  release mientras siga siendo el comando habitual de las pruebas.
- Los perfiles deben poder coexistir en el teléfono, con datos, `PlayerPrefs`,
  grabaciones y exportaciones separados. El grabador dedicado debe usar también
  una identidad DEV propia y una salida inequívoca; cambiar solo el nombre del
  archivo no separa instalaciones. No escribir pruebas en carpetas de NORMAL.
- Aplicar nombre, identificador, símbolos y demás opciones del perfil durante el
  build, y restaurar en `finally` toda configuración global modificada. No dejar
  `ANCORA_CAPTURE` ni opciones de depuración persistentes en el proyecto.
- Validar el perfil antes de generar el APK y detener NORMAL si hay opciones DEV,
  contenido provisional habilitado o falta la configuración de release. Evitar
  empaquetar recursos exclusivos de prueba/captura, incluidos los que Unity agrega
  automáticamente desde `Resources` o `StreamingAssets`.
- La transición desde el paquete compartido actual debe preservar los datos del
  usuario. No desinstalar, borrar grabaciones ni reiniciar ajustes para separar
  perfiles sin autorización explícita. Identificar siempre perfil y ruta del APK
  antes de instalar; comprobar `adb devices` como indica el runbook.

## Flujo de trabajo y criterio de entrega

- Revisar rama, cambios locales y upstream antes de actualizar Git. Preferir
  `git pull --ff-only`; conservar el trabajo ajeno y resolver una divergencia sin
  `reset --hard`, borrados ni force push. No cambiar de rama ni fusionar trabajo
  experimental a la estable por el solo hecho de pedir un pull.
- Para trabajo nuevo que necesite rama, usar `codex/<tarea>`. DEV/NORMAL son
  perfiles de la misma base; una rama no sustituye la separación de compilación.
  No asumir que `main` o la rama actual ya representan un producto validado.
- Comprobar el efecto de cada cambio sobre **ambos perfiles**. Para separación de
  perfiles, probar que NORMAL no construye menús/gestos internos, no inicia
  capturas y no recupera ajustes DEV; comprobar que DEV conserva sus herramientas.
  Al cambiar código, ejecutar las pruebas pertinentes del runbook. Para un cambio
  solo de documentación, revisar coherencia y diff; no hace falta compilar Unity.
- Antes de entregar un APK NORMAL, validar escena y datos, carga nativa de mapas y
  compilación del perfil. Orden de referencia: EditMode → `Prepare` → `Validate`
  → `CheckNativeMapsInEditor` → build explícito del perfil. Una sola instancia de
  Unity 6000.6.0f1; cerrar el Editor antes del modo batch.
- Revisar visualmente NORMAL y recorrer inicio, permisos, búsqueda, contenido,
  cambio de época y recuperación de errores en un Android compatible. Comprobar
  además instalación limpia, coexistencia y reapertura sin ajustes DEV. Un build
  correcto o una pose de Immersal no demuestran alineación física ni rendimiento
  en terreno. Si falta esa evidencia, informar «pendiente de validación».
- Promover una función a NORMAL solo con flujo completo, contenido definitivo,
  validación pertinente y evidencia registrada. Las solicitudes explícitas del
  usuario pueden cambiar el alcance de una entrega; registrar esa decisión sin
  presentar limitaciones conocidas como verificaciones realizadas.
- Al cerrar una tarea, indicar qué cambió en DEV, qué cambió en NORMAL, qué se
  verificó y qué queda pendiente. Si solo se escribieron reglas, decirlo.

## Graphify

For codebase exploration, use the local knowledge graph when it helps locate related code:
`.graphify-venv/bin/graphify query "<question>" --budget 1000` (or `explain` / `path`).
Verify findings in the source; use normal search when the graph is incomplete or stale.
After code changes, refresh it with `.graphify-venv/bin/graphify update .`.
For the installed Graphify skill, prepend `.graphify-venv/bin` to `PATH`.

The environment and graph are local and ignored by Git. On macOS/Linux, recreate them with
`uv venv .graphify-venv`, `uv pip install --python .graphify-venv/bin/python graphifyy==0.9.103`,
then `.graphify-venv/bin/graphify extract . --code-only`. This indexes code locally,
without a semantic pass over documents or an API key. On Windows, use the corresponding
executables in `.graphify-venv/Scripts/`.
