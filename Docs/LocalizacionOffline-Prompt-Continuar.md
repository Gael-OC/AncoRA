# Prompt para continuar en una sesión nueva

Preparado al cierre del 6-nov-2026. Copiar el bloque completo; el encargo detallado está en `LocalizacionOffline-SiguienteSesion.md` y las evidencias permanecen en el computador.

**Respaldo autorizado para la pausa:** [rama en GitHub](https://github.com/Gael-OC/AncoRA/tree/codex/respaldo-localizacion-offline-20261106). El estado local sigue sin commit en su rama original; el mismo prompt sirve en este computador. Conservar capturas, `salida/`, pesos y entornos ignorados. Un clon nuevo necesita además recuperar esos datos. [Detalle](LocalizacionOffline-RespaldoGitHub-2026-11-06.md).

```text
Continuemos la localización offline de AncoRA en:
/Users/gael/Documents/GitHub/AncoRA

Lee también Docs/LocalizacionOffline-RespaldoGitHub-2026-11-06.md: existe una instantánea remota de respaldo; no la integres automáticamente.

Comprueba el estado inicial. Estamos en localizacion-offline, con muchos cambios locales sin commit. Conserva todo: no hagas reset/restore, cambies de rama, crees worktree, hagas commit, push ni pull. Usa salidas nuevas y conserva originales y ensayos.

Lee primero CLAUDE.md, Docs/LocalizacionOffline-Estado-y-Plan.md y Docs/LocalizacionOffline-SiguienteSesion.md. Sigue el encargo vigente del 6-nov y sus lecturas/evidencias, especialmente OrigenImagenes e IdentidadTeologia. Ejecuta trabajo en PC, no entregues solamente otro resumen o plan.

Prioridad: investigar la ambigüedad de 143744/0001 y /0068 entre las dos reconstrucciones radiales v6. Ya están preparadas siete vistas de construcción para un ensayo dirigido de 71 a 78: 0002–0004, 0066–0067, 0069–0070. Verifica/anota su identidad, conserva las 71 como baseline, reutiliza solo características/pares compatibles y ejecuta la comparación en bases nuevas. No se ha reconstruido todavía con esas siete.

Revisa aparte las 25 imágenes históricas de Teología cuyos metadatos se recuperaron de intentos (1).jsonl. Están por clasificar: no añadirlas de golpe al mapa o a consultas ni transferirles automáticamente la calibración de casa. El importador/auditoría ya se corrigió y pasan 20 pruebas Python.

El mejor acuerdo entre métodos v6 no significa mapa validado: la réplica XFeat radial cambia una cámara ~14,64 % del diámetro. No eliminar cámaras ni elegir la réplica por una cifra mejor. Mantén K/distorsión/imagen coherentes por teléfono/configuración; la calibración candidata es solo del 15T y está en el archivo indicado en el encargo.

Las cuatro ventanas y sus bordes interiores ya fueron confirmados en fotos: C–D=2,70 m para ajuste exploratorio; R–S=6,20 m, H–I=2,70 m y la otra ventana=1,95 m reservadas. Falta asociación en varias vistas/3D; no se ha aplicado escala. No volver a pedir otra captura general, otro tablero del 15T ni puntos altos por rutina.

La app conserva Immersal; no hay motor propio integrado. No usar GPS, Immersal, etiquetas de distancia o poses Unity como verdad. Consultas/tablero siguen fuera del mapa/ajuste. La sincronía multicuadro no está validada; el timestamp de pose copia el de cámara. Los cuatro candidatos siguen abiertos.

Puedes usar subagentes 6.1 Terra para revisión. Reutiliza el entorno Python instalado; Unity CLI/internet solo si una tarea concreta lo justifica. No recompilar APK ni usar adb por rutina.

Avanza todo el trabajo independiente autorizado y actualiza Estado-y-Plan y el encargo con resultados reales. Al terminar explica qué cambió, si las imágenes adicionales ayudaron, qué permite el mapa, qué falta para probar el nuevo localizador en la app y qué necesitas de mí, si algo.
```
