# Diagnóstico inicial de correspondencias offline

Actualización del 5-nov-2026: [auditoría de los nuevos registros, 996 comparaciones y ensayo de reconstrucción](LocalizacionOffline-Registros-2026-11-05.md).
Los números siguientes conservan el ensayo inicial del 4-nov y no se mezclan con el conjunto nuevo.

Equipo de ejecución: Darwin arm64; CPU, sin aceleración móvil.

Configuración: lado máximo 1024 px, hasta 2048 características.

**Estos pares no miden pose 3D, precisión en centímetros ni tiempo de inicio en Android.**
La etiqueta ‘mismo edificio’ tampoco garantiza solapamiento entre las vistas.

| Método | Pares del mismo edificio / otros | Mediana inliers mismo / otros | Extracción consulta (s) | Comparación (s) |
|---|---:|---:|---:|---:|
| sift | 12 / 12 | 5.500 / 4.000 | 0.033 | 0.018 |
| sift-lightglue | 12 / 12 | 7.000 / 5.000 | 0.031 | 0.552 |
| aliked-lightglue | 12 / 12 | 7.500 / 4.500 | 0.681 | 0.186 |
| xfeat-lighterglue | 12 / 12 | 12.500 / 0.000 | 0.074 | 0.144 |

## Límites

- Ensayo diagnóstico sobre imágenes estáticas de sesiones distintas.
- Los tiempos de extracción guardados en caché son el cómputo original; no la lectura de la caché.
- Una homografía puede ajustar coincidencias falsas: sus inliers no acreditan una ubicación.
- Falta mapa abierto registrado, calibración óptica y controles de fachada independientes.
- No se elige ganador ni se declara cumplido el objetivo de 10–20 cm con este ensayo.
