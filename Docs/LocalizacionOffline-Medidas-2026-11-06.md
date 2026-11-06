# Teología — medidas informadas el 6-nov-2026

## Aclaraciones posteriores y partición antes de escala

Durante la sensibilidad de cámara del 6-nov, Gael confirmó las ventanas de la lámina y precisó: **anchos de izquierda a derecha entre bordes internos de la ventana, sin el hormigón**. Añadió una cuarta medida:

| ID | Identificación confirmada por Gael | Ancho informado | Papel para el siguiente ensayo métrico |
|---|---|---:|---|
| INTERIOR-C-D | Ventana baja junto a la puerta, panel 1, foto `143744/0011` | 2,70 m | Ajuste exploratorio condicionado a soporte 2D/3D |
| INTERIOR-H-I | Primera baja del lateral, panel 3, foto `143744/0116` | 2,70 m | Reservada |
| INTERIOR-R-S | Ventana larga baja, panel 2, foto `143744/0049` | 6,20 m | Reservada |
| INTERIOR-VUELTA-FRENTE | Baja a la izquierda de H–I, al otro lado de la esquina, panel 3, foto `143744/0116` | 1,95 m | Nueva reserva |

**Cambio explícito anterior a cualquier ajuste:** el nuevo control INTERIOR-C-D se destina a ajuste, aunque el C–D de la hoja antigua figuraba como reservado. No son automáticamente los mismos extremos. La longitud usada para ajustar no se presentará como comprobación independiente; las otras tres quedan fuera. Si C–D no tiene soporte suficiente, no se sustituirá silenciosamente por una reserva.

**Confirmación fotográfica posterior:** Gael revisó las cuatro fotos con líneas verdes y respondió «Sí, las cuatro coinciden». Se identifican los bordes interiores realmente medidos, sin hormigón. `salida/20261106-identidad-teologia/aristas-interiores-confirmadas.json` conserva segmentos en coordenadas de presentación y crudas, imagen/hash y esa respuesta; la lámina es `aristas-interiores-propuestas.jpg`. Los segmentos indican aristas, no altura de cinta ni puntos XYZ. H–I corresponde a la primera baja a la derecha de la esquina del panel 3; la de 1,95 m queda a la izquierda de esa esquina.

La altura exacta de la huincha, repeticiones, incertidumbre y correspondencia 3D de esas aristas siguen pendientes. Las fotos están registradas; el [nuevo filtro de identidad](LocalizacionOffline-IdentidadTeologia-2026-11-06.md) mejora la estabilidad de cámaras, pero aún hay asociaciones contradictorias. **No se calculó ni aplicó escala con ninguna medida ni se usaron reservas para elegir geometría.** No se pide alcanzar A–B/E–F/G ni repetir la calibración del 15T.

Evidencia actualizada en `Tools/LocalizacionOffline/salida/20261106-optica-teologia/medidas-asociacion-confirmada.json`; el registro inicial y `asociacion-medidas-pendiente.json` se conservan sin reescribirlos. [Resultado de los ensayos ópticos](LocalizacionOffline-OpticaTeologia-2026-11-06.md).

## Registro inicial conservado

Gael informó tres longitudes y aclaró: **«Medí por dentro, por el marco de las ventanas, desde donde empieza a donde termina»**. Se conservan sus etiquetas de ubicación, pero se crean identificadores nuevos porque no está confirmado que los extremos coincidan con los círculos de hormigón/aleros de la lámina original. Tampoco se reinterpretan como las esquinas exteriores del marco metálico solicitadas en la pauta posterior.

| ID nuevo | Etiqueta usada por Gael | Valor informado | Detalle aportado |
|---|---|---:|---|
| INTERIOR-R-S | R–S | 6,2 m | Largo horizontal de la ventana; medido por dentro del marco |
| INTERIOR-H-I | H–I | 2,70 m | Medido por dentro del marco; dirección/ventana exacta pendiente de identificación en foto |
| INTERIOR-C-D | C–D | 2,70 m | Medido por dentro del marco; ventana y esquinas exactas pendientes de identificación en foto |

No se aportaron segundas lecturas, graduación/instrumento realmente usado, incertidumbre ni confirmación de cinta recta entre extremos. Se mantienen como **no informados**, sin inventar una precisión por la cantidad de decimales. «Por dentro del marco» todavía no identifica por sí solo qué arista o esquina 3D se tocó: hay que marcar los extremos físicos antes de relacionarlos con tracks de la reconstrucción.

**No medidos:** A–B y E–F, porque estaban demasiado altos para Gael. G ya había sido descartado de esta visita. No se pide alcanzar esos puntos ni se trasladan sus dimensiones desde otras ventanas que parezcan iguales.

## Cómo se utilizarán

Son medidas potencialmente útiles para escala y comprobación de zonas del edificio. Para utilizarlas hay que asociar cada una a **su ventana y sus dos extremos interiores reales** en una foto, y después comprobar que esos mismos extremos pueden identificarse en las referencias y en la geometría 3D. Una foto general de la ventana más otra donde se señalen inicio/fin puede resolver la diferencia con la lámina original. Esto no exige medir hasta el techo ni repetir el tablero del 15T.

Los nombres R–S/H–I/C–D de la lámina anterior figuraban como controles reservados, mientras que la pauta posterior usaba otras esquinas y nombres AJ/RES. **No se reclasificaron silenciosamente ni se usó ninguna medida para ajustar.** La asignación de las nuevas longitudes interiores a ajuste/comprobación queda pendiente de resolver la correspondencia física, conservando la separación antes de calcular una escala. Si se usa una medida en un ensayo exploratorio de ajuste, se documentará como tal y no se presentará esa misma medida como comprobación independiente.

Los dos valores 2,70 m no prueban igualdad física de elementos distintos fuera de lo que se midió, ni permiten inferir A–B/E–F o alturas. El largo de 6,2 m no se reparte entre módulos o ventanas sin evidencia.

## Estado del proyecto tras recibirlas

- Tres longitudes físicas **informadas por el usuario**, conservadas con su descripción interior.
- Extremos 2D/3D, incertidumbre y repeticiones aún pendientes de documentación.
- **No se calculó ni aplicó escala, no se cambiaron cámaras ni se aceptó precisión métrica.** Teología sigue con escala arbitraria y cámaras discrepantes por resolver.
- Estas medidas del edificio no cambian la calibración del tablero ni validan sincronía Unity/cámara.

Registro estructurado en `Tools/LocalizacionOffline/salida/20261106-medidas-teologia/medidas-informadas.json`. No se incorporaron imágenes de calibración o consultas al mapa, ni se modificaron originales, código Unity, APK, rama o commits.
