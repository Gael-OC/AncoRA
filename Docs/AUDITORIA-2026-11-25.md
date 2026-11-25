# Auditoría de la app (2026-11-25, versión 1.6.2)

Revisión de solo lectura del código de la rama `nicowo` en cuatro áreas: ubicación, rendimiento gráfico, interfaz y
grabación/build. No se cambió código. Cada punto dice cómo se comprobó: **verificado** (leído en el código o en los
ajustes, y en el paquete de Immersal cuando aplica), **probable** (camino de código leído, no visto en el teléfono) o
**a medir** (depende del teléfono).

Ordenado por lo que más afecta a la demo del 7-dic.

## A. Pueden arruinar la demo

| # | Qué pasa | Dónde | Estado | Arreglo |
|---|---|---|---|---|
| A1 | **La ubicación muere sin aviso** si `PaseoLocalization.Localize` lanza una excepción: el `Localizer` de Immersal la relanza al leer `.Result`, `m_IsLocalizing` queda en `true` y `ImmersalSession` se detiene («Stopping session»). Nada la vuelve a arrancar: la caja queda congelada hasta cerrar la app. Poco probable, pero sin salida. | `PaseoLocalization.cs:140, 180-185` (eventos sin try/catch); `Localizer.cs:235, 276`; `ImmersalSession.cs:266-270` | verificado | try/catch alrededor de todo `Localize` y de cada evento, devolviendo un resultado vacío. Vigilante en `PaseoTour.Update`: con el SDK listo y más de 15 s sin intento, `StartSession()`. |
| A2 | **Un resultado que cruza una pérdida de tracking** (empezó antes y llegó después) entra al filtro con el marco viejo de ARCore y lo fija; las ubicaciones buenas que siguen se rechazan como «lejos» hasta un re-anclaje. En el 15T un intento dura 5–15 s, así que la ventana es real. | `PaseoTour.cs:920, 935-942`; `PaseoPoseProcessor.cs:79` | probable | Contador que sube al perder tracking; `Localize` descarta el resultado si cambió. No lanzar intentos sin tracking. |
| A3 | **La búsqueda nunca para.** Ya ubicado, sigue un intento cada 2 s, y en el 15T los intentos de ~7 s se encadenan sin pausa: CPU al 100 % toda la demo, calor y estrangulamiento (la causa sospechada de que el 15T sea 4–5× más lento). | `PaseoPace.cs:18-28`; `PaseoTour.cs:1029-1051` | verificado | Con la caja confirmada (≥ 3 aceptadas) y a la vista, un intento cada 8–10 s; volver a rápido ante «lejos», movimiento grande o pérdida de tracking. |
| A4 | **Primera ubicación más lenta de lo necesario:** con un solo mapa buscado sigue activo el umbral de 30 de confianza pensado contra falsos entre edificios; entre 40 y 53 % de las ubicaciones buenas quedan «pendiente» esperando una segunda. | `PaseoPoseProcessor.cs:76`; `PaseoResolution.cs:42` | verificado (la regla); a medir (el efecto) | Con un solo edificio buscado, umbral 20 o ninguno. Comprobarlo antes con `PaseoReplay` sobre las grabaciones de Teología. |
| A5 | **1440×810 por defecto en todos los teléfonos.** En el 15T son ~7 s de mediana por intento. La prueba del teléfono existe pero es manual. | `PaseoResolution.cs:18` | verificado | Sin código: correr «Probar este teléfono» en cada teléfono de la demo antes del 7-dic. |
| A6 | **«Restaurar escena» recarga sin esperar el intento nativo en curso**: Immersal libera los mapas mientras un hilo puede estar dentro de `LocalizeImage`. Posible cierre brusco. | `PaseoTour.cs:1186-1190` (`ChangeDemoMap` sí espera) | probable | Usar la misma pausa y espera de `ChangeDemoMap`. |

## B. Lo ven los visitantes

| # | Qué pasa | Dónde | Estado | Arreglo |
|---|---|---|---|---|
| B1 | Si el SDK tarda más de 15 s en arrancar, sale **«Error: El SDK no quedó listo en 15 s; revisar el log de inicio.»** en rojo. El mensaje pensado para visitantes («Cierra y vuelve a abrir la app») nunca se muestra porque `Error` se evalúa antes. | `PaseoTour.cs:1109`; `PaseoStatusText.cs:49-59` | verificado | Mostrar el texto amable; el detalle técnico solo al log. |
| B2 | La píldora de estado dice **«Ciencias Básicas: ubicado · EIC: ubicado · X1: ubicado · Teología: ubicado»**, incluido EIC (oculto) y edificios que no se buscan; ocupa varias líneas. | `PaseoTour.cs:1113-1127` | verificado | Listar solo edificios con caja visible, o solo «Teología: ubicado». |
| B3 | La línea de actividad dice «Buscando en todos los edificios…» antes del primer intento; ya ubicado sigue contando «N imágenes sin coincidencia» bajo la píldora verde; y muestra jerga del equipo («descartada: movería la caja 3,4 m», «confianza 18», «Cargando mapa 152259»). | `PaseoActivity.cs:50, 74-81`; `PaseoTour.cs:625`; `PaseoStatusText.cs:187` | verificado | Ocultarla cuando el edificio buscado ya está ubicado, «Buscando…» genérico, y la jerga solo en la hoja del equipo. |
| B4 | **Botones chicos:** segmentos de época de 34 px, «Parque» de 30 px y «?» de 36 px sobre una referencia de 390 px de ancho (~6 mm en el teléfono; lo recomendado es ~9 mm). Los visitantes van a fallar. | `Paseo.uss:30, 34, 49`; `PaseoPanel.asset` | verificado | 44–48 px. |
| B5 | La bienvenida dice «Camina al punto que te indicamos», pero la app no indica ninguno; además está fuera del área segura (el botón puede quedar bajo la barra de gestos). | `PaseoWelcomeView.cs:153` | verificado (texto); probable (área segura) | «Párate en la vereda frente a Teología y apunta a la fachada»; moverla dentro de `GuiSafeArea`. |
| B6 | La app abre en **1950**: cámara en blanco y negro con rayas mientras todavía busca, sin edificio encima. | `PaseoEpoch.cs:76` | verificado | Empezar en Actualidad o aplicar el filtro solo con un edificio a la vista. |
| B7 | **Ajustes que sobreviven entre sesiones** (época, cajas de diagnóstico, oclusión): si quedaron puestos de una prueba, la demo abre así. «Restaurar escena» solo borra poses y tamaños. | `PaseoTour.cs:362-366`; `PaseoOcclusion.cs:37-39` | verificado | Lista de revisión antes de la demo, o reiniciarlos al abrir. |
| B8 | Marca «Development Build» y splash de Unity: el APK siempre se compila en modo desarrollo. | `PaseoSetup.cs:953`; `ProjectSettings.asset` `m_ShowUnitySplashScreen: 1` | verificado (ajuste); a ver en el teléfono (marca) | Decisión: un APK de demo sin `Development` pierde la hoja del equipo y Grabar (dependen de `Debug.isDebugBuild`); el registro de sesión sigue. |
| B9 | Cinco toques seguidos en la esquina abren la hoja del equipo; un niño puede abrirla. | `PaseoTapGesture.cs`; `Paseo.uss:70` | probable | Exigir los 5 toques en menos de 3 s o una pulsación larga. |

## C. Rendimiento

Los teléfonos ya van a ~30 fps (registros de octubre: `RENDIMIENTO fps=30`), así que no hace falta limitar los cuadros por segundo.

| # | Qué | Dónde | Estado | Beneficio |
|---|---|---|---|---|
| C1 | URP copia la profundidad en cada cuadro y nada la usa. | `URP-Performant.asset:22` `m_RequireDepthTexture: 1` | verificado | 0,3–0,8 ms de GPU por cuadro (estimado). Probar con la oclusión Normal. |
| C2 | La película de 1950/1980 se redibuja entera cada cuadro, y la deriva de las rayas depende de los cuadros, no del tiempo. | `PaseoFilmOverlay.cs:44-60` | verificado | Redibujar a 24 Hz: 1,25–2,5× menos costo de esa capa. |
| C3 | Tabla de color de 16 puntos con filtros fuertes (blanco y negro, sepia): posible banding en el cielo. | `URP-Performant.asset:80` | probable | Subir a 32, costo despreciable. |
| C4 | El shader de modelos y parque usa `Cull Off` y `discard` siempre: pierde el descarte temprano de profundidad en un modelo que cubre media pantalla. | `PaseoBloques.shader` | probable | `Cull Back` fuera de las transiciones; `discard` de oclusión solo con oclusión. 10–25 % del costo de esos fragmentos (estimado). |
| C5 | GPS encendido (precisión 5 m) sin uso cuando se busca un solo edificio. | `PaseoTour.cs:787-797` | verificado | Batería. |
| C6 | Renderizado multihilo apagado en Android desde el primer commit. | `ProjectSettings.asset:496-497` | verificado (ajuste); a medir | Probar `Android: 1` y comparar `DEPURACION_RENDIMIENTO`. |
| C7 | Ráfaga inicial: los intentos descartados por mirar al suelo igual piden la imagen cada cuadro. | `PaseoLocalization.cs:108-127` | probable | Saltar antes de pedir la imagen. |

## D. Pruebas de terreno (no la demo)

- **«Copiar grabaciones a Descargas» no revisa el espacio libre** y duplica hasta 1 GB por sesión. Con el disco lleno deja de escribirse el registro de sesión para siempre (`PaseoRecordings.cs:107-167`, `PaseoSessionLog.cs:142`). Arreglo: exigir espacio libre suficiente antes de copiar.
- **Las grabaciones nunca se rotan** entre sesiones; solo protege el mínimo de 400 MB libres. Antes de la demo: copiar y «Borrar grabaciones» en cada teléfono.
- **«Borrar grabaciones» corre en el hilo principal**: puede congelar la app varios segundos (`PaseoUi.cs:621-638`).
- Si falla la carga del mapa de la demo, la búsqueda cae a **todos** los mapas cargados (~13 s por intento en el 15T) (`PaseoLocalization.cs:252-264`).
- El zip de «Compartir registro» se escribe en el hilo principal y los zips temporales no se borran (`PaseoShare.cs:133-163`).

## E. Detalles menores

- Cambiar de época a mitad de la transición: el filtro y el modelo saltan (`PaseoEpochFilter.cs:39`, `PaseoMapContent.cs:175`).
- El modelo de 2000 ya traía 0,2 m por lado horneados; con el margen de la 1.6.2 queda a ~0,6 m por lado, contra 0,4 m de 1950/1980. Solo tapa un poco más.
- `PaseoPark` llama a `SetActive(false)` en cada cuadro a un personaje ya oculto (inofensivo).
- `Packages/manifest.json` incluye Input System y OpenXR sin usarlos.
- El APK pesa 86 MB: `libil2cpp` 18 MB, `libunity` 16 MB (variante de desarrollo), mapas ~31 MB, `Resources/PaseoPrueba` 5,8 MB (imágenes de la prueba del teléfono, siempre incluidas).

## Descartado al verificar

- «Sin tope de cuadros por segundo → 120 Hz»: los registros del teléfono muestran 30 fps.
- Fugas de materiales o mallas, normales invertidas en los modelos, problemas de SRP batching, imágenes de cámara sin liberar, eventos sin desuscribir, claves de `PlayerPrefs` fuera del prefijo `v3`: revisados, sin problema.

## Propuesta para el 7-dic

**Paquete demo (~1 día, sin tocar los mapas):** A1, A3, A6, B1–B7, C1, C2, C5.

**Por decidir:**
- A4 (umbral), después de probarlo con `PaseoReplay` sobre las grabaciones de Teología.
- A2 (pérdida de tracking).
- B8 (APK de demo sin modo desarrollo).

**Sin código:** A5 (prueba del teléfono en cada teléfono), B7 y D (revisión y limpieza de los teléfonos antes de la demo).
