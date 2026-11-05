# Continuar la localización offline

Decisión del 5-nov-2026: comprobar geometría 3D de fachada con los datos existentes antes de integrar un motor en Android.
XFeat+LighterGlue es el candidato principal; ALIKED+LightGlue, la comparación. Gael confirmó que no tiene más carpetas.

## Prompt para el siguiente trabajo

```text
Continuemos la localización offline de AncoRA en /Users/gael/Documents/GitHub/AncoRA,
rama localizacion-offline. No uses prefijo codex/ ni coautorías o atribuciones al asistente.
Conserva los cambios locales ajenos: hay trabajo de demo/oclusión fuera de los commits offline.

Lee CLAUDE.md, Docs/LocalizacionOffline.md, Docs/LocalizacionOffline-Resultados.md,
Docs/LocalizacionOffline-Registros-2026-11-05.md y Tools/LocalizacionOffline/README.md.
Docs/AI/UnityProjectContext.md describe además la demo local; sus cambios no están todos publicados.

Lo entregado es todo el material disponible. No esperes siete carpetas que aparecen en los logs
ni el JSONL de Teología J: no existen entre los archivos que tengo. Usa las copias verificadas de
Tools/LocalizacionOffline/salida/20261105/ y las grabaciones históricas ya disponibles.
Los originales, pesos, entornos, cachés y resultados locales están ignorados por Git, no en el remoto.
Entorno: Tools/LocalizacionOffline/.venv; pesos y código preparados en .weights y .deps.

Objetivo de esta etapa: intentar una reconstrucción abierta solo desde imágenes que sostenga
la fachada de Teología, usando XFeat+LighterGlue como candidato principal y ALIKED+LightGlue
como comparación. El ensayo SIFT anterior registró 27 imágenes y 1.238 puntos principalmente
en vegetación; su reproyección de 0,314 px no lo convierte en un mapa útil de fachada.

Usa A/B Samsung y sesiones históricas de Teología como construcción; selecciona vistas desde
ese conjunto. Mantén D Samsung y E Xiaomi como consultas separadas. Teología J tiene 28 PNG
sin calibración ni tiempos: sirve solo para comparación 2D. La evaluación ya fue explorada;
no la presentes como prueba final ciega. No mezcles consultas en el mapa ni ajustes umbrales
con ellas para luego afirmar éxito. Conserva los scripts y resultados anteriores.

Reutiliza las herramientas existentes. Integra correspondencias aprendidas con SfM si hace
falta, con una prueba pertinente de esa integración. Evalúa tracks, paralaje, reproyección,
cobertura sobre fachada y consistencia, identificando vegetación y estructuras repetidas.
Si reconstruyes sin controles físicos, declara escala arbitraria y no pases ese modelo al
localizador con umbrales en metros. No conviertas poses Unity crudas en cámaras ópticas
validadas ni uses Immersal como verdad de terreno. No inventes datos de Teología J.

Avanza con lo disponible sin repetir la investigación inicial ni recompilar el APK.
Entrega el resultado geométrico, sus límites y una decisión: continuar con un mapa métrico,
o justificar con evidencia la trayectoria de captura de construcción que falta.
Solo después de validar geometría, escala y calibración corresponde medir PnP/multicuadro
y portar a Android. El objetivo de 10–20 cm, 10–25 m y arranque ≤5 s sigue sin demostrarse.
```

La rama publicada contiene el banco Python, captura de barridos y documentación offline. Los cambios locales de modelos,
oclusión, escena, selección de mapa de demo y ajustes de parque se mantienen fuera de esos commits.
