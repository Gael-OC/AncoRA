# Contexto Unity de AncoRA

<!-- unity-onboarding:generated:start -->

Última revisión: 2026-09-10, commit base `7d862a4`. El working tree contiene una migración amplia y
no confirmada desde la plantilla AR; sus eliminaciones y archivos sin seguimiento deben preservarse.

## Proyecto y plataformas

- Unity 6000.6.0f1.
- AR Foundation, ARCore y ARKit 6.5.0; XR Management 4.6.0.
- ARCore Extensions 1.56.0 está embebido en `Packages/`.
- URP 17.0.3 con `ARBackgroundRendererFeature` activo.
- Destinos del piloto: iOS con ARKit y Android con ARCore. Geospatial está habilitado para ambos.
- Código propio sin `.asmdef`: compila en `Assembly-CSharp`.

## Arranque y composición

La única escena habilitada es `Assets/Scenes/GeospatialPilot.unity`. Contiene `ARSession`,
`XROrigin`, cámara AR, `ARAnchorManager`, `AREarthManager`, `ARCoreExtensions` y
`GeospatialVpsProbe`, con referencias serializadas válidas al config Geospatial y al perfil del sitio.

Archivos principales:

- `Assets/AncoRA/Scripts/GeospatialVpsProbe.cs`: máquina de estados, diagnóstico, ancla y HUD IMGUI.
- `Assets/AncoRA/Scripts/GeospatialSiteProfile.cs`: coordenadas y geometría serializadas.
- `Assets/AncoRA/Scripts/HouseMeshBuilder.cs`: malla paramétrica con base en `y = 0`.
- `Assets/AncoRA/Scripts/PipelineMaterials.cs`: materiales URP creados en runtime.
- `Assets/Settings/HouseGeospatialSite.asset`: sitio provisional; no modificar coordenadas sin datos
  de terreno.
- `GEOSPATIAL_PILOT.md`: diagnóstico confirmado y protocolo de prueba.

## Estado del bug Geospatial

El export `Builds/iOS` contiene la implementación antigua: condiciona la solicitud Terrain a pose
H <= 1 m, V <= 2 m y yaw <= 1,5 grados, y falla tras 30 s. Esa máquina explica que el teléfono
mostrara VPS disponible sin crear el bloque. La fuente actual separa Earth Tracking, resolución
Terrain, Preview y candidato Final, conserva datos después del aviso temporal y ofrece una
previsualización forzada marcada como no confiable. El build de diagnóstico vigente debe mostrar
`geospatial-diagnostic-v3`: cada intento tiene un `runId`, la promesa VPS es observable/cancelable,
el GPS informa precisión/edad y el proxy usa URP Unlit.

No están confirmados aún en dispositivo: transición real de Earth Tracking, resultado Terrain,
alineación visual, precisión repetible ni deriva. Deben decidirse con el JSON/captura descritos en
`GEOSPATIAL_PILOT.md`; no cambiando coordenadas por intuición.

## Validación y herramientas

- Compilación y validador determinista con Unity CLI en batchmode: exit code 0 el 2026-09-10 para
  Android y iOS, sin errores ni warnings del código AncoRA. Al cambiar a Android, el paquete
  embebido de ARCore Extensions emite warnings de API obsoleta de Unity 6 que no pertenecen al
  piloto. El método es
  `AncorRA.Editor.GeospatialPilotValidation.ValidateFromCommandLine`.
- No hay assemblies de pruebas EditMode/PlayMode propios; el validador comprueba escena, referencias,
  perfil, configuración, shader, bounds de la malla y distancia/rumbo conocidos.
- No hay un Unity MCP disponible en esta sesión; la inspección se realizó desde archivos, logs y
  artefactos IL2CPP.
- El export Xcode y la prueba física quedan a cargo del operador; no confundir compilación con
  validación Geospatial en terreno.

## Restricciones

- Español en textos de UI/log; comentarios de código en inglés.
- No volver a Augmented Images, QR ni calibración obligatoria junto a la casa.
- Mantener Android e iOS.
- No registrar ni copiar credenciales en documentación o respuestas.

<!-- unity-onboarding:generated:end -->
