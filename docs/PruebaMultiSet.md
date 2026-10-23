# Prueba de MultiSet al lado de Immersal

Rama `prueba/multiset` (sale de `nicowo` 1.3.4). La app de prueba se llama **AncoRA MultiSet**
(`com.ancora.ucnar.paseo.multiset`) y se instala **al lado** de la app normal, sin pisar sus ajustes ni sus cajas.

Es el paseo de siempre, con Immersal funcionando igual que antes, más MultiSet consultando su nube en paralelo con la misma
cámara. Así se comparan los dos desde el mismo lugar y en el mismo momento:

1. **Rapidez:** cuánto tarda cada sistema desde «Comenzar» hasta su primera ubicación (`PRIMERA_UBICACION`).
2. **Acierto:** segundos y resultado de cada consulta (`INTENTO_MULTISET` frente a `INTENTO_PASEO`).
3. **Precisión relativa:** cuánto difiere la caja que pone MultiSet de la caja amarilla de Immersal (`dif_caja`, en metros).

En pantalla, la caja **cian** es donde MultiSet pone el edificio; la amarilla, donde lo pone Immersal.

**Lo que no está probado:** el SDK de MultiSet nunca corrió en un teléfono con este código (el Editor no ubica). La primera
salida a terreno es también la primera prueba de que autentica, consulta y calza con Immersal.

---

## Qué se necesita

- **Internet en el teléfono** (datos o Wi-Fi). MultiSet ubica en su nube: sin red no ubica, aunque Immersal siga funcionando.
- **Una cuenta de MultiSet.** El plan gratis trae 5 mapas, **1 000 consultas en total** (no por mes) y una marca de agua en
  pantalla. Alcanza para la prueba si se cuidan las consultas (ver *En terreno*).
- **Un Android con ARCore** para hacer el mapa (el Xiaomi 14 o el Galaxy S25 sirven; un iPhone con LiDAR da un mapa mejor).
- **adb** en el computador, para instalar la app y pasarle la configuración:
  `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`.

---

## 1. Cuenta y credenciales (esto lo haces tú)

1. Regístrate en el portal de desarrolladores: <https://developer.multiset.ai/>.
2. Entra a **Credentials** → **Create New**.
3. Ponle un nombre como `AncoRA prueba` y marca **solo el permiso Query** (consultas de ubicación). Write y Delete no
   hacen falta, y los permisos no se pueden cambiar después.
4. Copia el **Client ID** y el **Client Secret** en ese momento: el portal no vuelve a mostrar el secreto.

**Sobre la seguridad.** El secreto va a quedar guardado en el teléfono de prueba. MultiSet recomienda no ponerlo en apps
publicadas, así que para esta prueba:

- Usa una credencial creada solo para la prueba, con permiso Query únicamente.
- No la subas a git, no la pegues en el chat del equipo y no la dejes dentro de la carpeta del repositorio.
- Al terminar la prueba, desactívala o bórrala en el portal.

La app nunca escribe el secreto en el registro (si apareciera, sale como `***`), y el zip de «Compartir registro» no
incluye el archivo de configuración.

---

## 2. Hacer el mapa de un edificio

Conviene partir con **Teología**: su mapa Immersal (`152259`) es uno solo y registra claro contra el dron, así que la caja
amarilla es una buena referencia para comparar.

### Con el teléfono (app MultiSet)

1. Instala la app **MultiSet** de Google Play (`ai.multiset.maps`) o del App Store, y entra con la misma cuenta.
2. Escanea siguiendo estas reglas (las de MultiSet, más lo aprendido con Immersal):
   - **Camina; no gires en un mismo punto.** Es el mismo problema que dejó diminutos los mapas Immersal de Gael.
   - Haz **una pasada a lo largo de la fachada a 3–5 m**, despacio, con el teléfono horizontal.
   - Haz **otra pasada desde donde se para la gente a mirar el edificio (15–25 m)**, mirando hacia él y caminando de lado
     en arcos cortos.
   - Mira cada parte desde varios ángulos. Repetir el mismo punto de vista no agrega nada.
   - Evita que el vidrio, las superficies lisas o la gente que pasa ocupen el centro de la imagen.
   - Termina donde empezaste.
3. Súbelo y espera en el portal a que el mapa quede en estado **Active** (pasa por *Uploading* y *Pending*; si queda en
   *Failed* hay que escanear de nuevo).
4. Copia el **código del mapa** desde su ficha en el portal.

### Con una cámara 360 (si hay una)

MultiSet acepta el `.insv` crudo de una Insta360 X4/X5, o un `.mp4` 360 equirectangular de cualquier cámara (al menos
3840 × 1920). Para exteriores piden grabar en 8K a 30 fps, con la cámara sobre la cabeza, caminando a 1–1,5 m/s, y
terminar donde se empezó. Se sube desde el portal. El plan gratis procesa hasta 300 s de video 360.

### Pista GPS (opcional)

Si el mapa se **georreferencia** en el portal (MultiSet lo hace solo en exteriores con cielo despejado, o se alinea a mano
contra la imagen satelital), se puede encender `pistaGps` en la configuración para que la búsqueda vaya más directo. Con
un mapa sin georreferenciar, déjala en `false`.

---

## 3. Instalar la app y pasarle la configuración

1. Conecta el teléfono y confirma el serial (solo hay uno conectado a la vez):
   ```powershell
   adb devices
   ```
2. Instala el APK de la prueba (queda al lado de la app normal):
   ```powershell
   adb -s <serial> install -r C:\dev\AncoRA-multiset\Builds\Android\AncoRAMultiSet.apk
   ```
3. Abre **AncoRA MultiSet** una vez en el teléfono y ciérrala, para que se cree su carpeta.
4. En una carpeta **fuera del repositorio** (por ejemplo `Documentos`), crea `multiset.json` a partir de
   `docs/multiset.ejemplo.json`:
   ```json
   {
     "clientId": "tu Client ID",
     "clientSecret": "tu Client Secret",
     "codigo": "el código del mapa",
     "tipo": "map",
     "edificios": { "el código del mapa": "Teologia" },
     "pistaGps": false,
     "radioGps": 25,
     "maxConsultas": 120,
     "segundosTrasUbicar": 10
   }
   ```
   - `edificios` dice a qué edificio del paseo corresponde cada mapa de MultiSet. El nombre es el de la carpeta:
     `Teologia`, `X1`, `CienciasBasicas` o `EIC`.
   - `maxConsultas` es el tope por sesión; `segundosTrasUbicar`, cada cuánto consulta después de la primera ubicación.
   - Con varios edificios se arma un **MapSet** en el portal: `"tipo": "mapset"`, `codigo` = el del MapSet, y una línea por
     mapa en `edificios`.
5. Pásalo al teléfono:
   ```powershell
   adb -s <serial> push multiset.json /sdcard/Android/data/com.ancora.ucnar.paseo.multiset/files/multiset.json
   ```
6. Abre la app, toca «Comenzar» y abre la hoja del equipo (5 toques arriba a la izquierda). La fila de MultiSet debe pasar
   de «MultiSet: autenticando…» a **«MultiSet: autenticado · N consultas»**. Si dice «error», revisa el internet y las
   credenciales. Si dice «apagado», el archivo no está en la carpeta correcta o le falta algún campo; la razón exacta queda
   en el registro.

Para cambiar el mapa o el tope basta con editar `multiset.json` y volver a hacer el `push`: no hace falta otro APK.

---

## 4. En terreno

### Primero, una vez por teléfono: unir las dos cajas

MultiSet no sabe dónde está la caja del edificio dentro de su mapa. La aprende de Immersal la primera vez que los dos
ubican juntos:

1. Párate frente a Teología y espera a que **Immersal** muestre la caja amarilla y la tarjeta diga **«Ubicado»** (no
   «Confirmando»).
2. Con la caja amarilla a la vista, espera a que **MultiSet** ubique. En ese momento guarda la relación y aparece la caja
   **cian** encima de la amarilla.
3. Desde ahí, cada vez que MultiSet ubica dibuja su caja cian, **aunque Immersal todavía no haya ubicado**.

Si la caja amarilla estaba mal puesta, corrígela antes con el panel de ajuste, y después toca **«Olvidar relación
MultiSet»** en la hoja (pide un segundo toque) para que la relación se rehaga con la caja corregida.

### Las mediciones

Elige **3 puntos de observación** por edificio (donde se pararía un visitante, a 15–25 m) y repite **5 veces en cada
punto**:

1. Cierra la app del todo.
2. Párate en el punto, abre la app y toca «Comenzar».
3. Apunta al edificio y quédate quieto unos 30 s.
4. Mira si la caja cian y la amarilla coinciden. Si se separan, saca una captura de pantalla.

No hace falta anotar tiempos: el registro guarda cuándo ubicó cada sistema por primera vez.

### Cuidar las 1 000 consultas

- Cada sesión consulta como mucho `maxConsultas` (120 por defecto) y después se detiene; la hoja muestra «tope alcanzado».
- Antes de la primera ubicación consulta una tras otra (cada consulta tarda unos segundos); después, una cada
  `segundosTrasUbicar`.
- Al caminar entre un punto y otro, pausa con el botón **«MultiSet: sí»** de la hoja (pasa a «MultiSet: no»; otro toque
  lo reanuda).
- Las 15 sesiones de arriba gastan unas 100–300 consultas por edificio si ubica rápido. El portal muestra cuántas quedan.

### Al terminar

En la hoja del equipo, **«Compartir registro»** arma un zip (sin las credenciales) que se puede mandar por WhatsApp. O,
con el teléfono conectado:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\BajarRegistro.ps1 -Serial <serial> -Todas
```

(desde `C:\dev\AncoRA-multiset`; en esta rama baja por defecto los registros de AncoRA MultiSet). Para un zip compartido,
`-Archivo <ruta del zip>`.

---

## 5. Leer los resultados

```bash
node Tools/AnalizarRegistro.js Registros/<carpeta>/paseo-<fecha>.log
```

La sección **MultiSet** muestra las consultas, el % de acierto, los segundos con y sin coincidencia, la primera ubicación de
cada sistema por edificio y `dif_caja` (mediana y máximo). Las líneas del registro, por si se quieren mirar a mano:

| Línea | Qué dice |
|---|---|
| `MULTISET_ESTADO estado=… consultas=… t=…` | El estado del SDK: `esperando`, `autenticando`, `autenticado`, `error`, `sin-config`, `apagado` (en pausa) o `tope`. |
| `INTENTO_MULTISET n=… seg=… resultado=… conf=… aceptada=… edificio=… gps=… t=…` | Cada consulta: cuánto tardó, qué mapa encontró, con qué confianza y si se aceptó (≥ 0,3). |
| `UBICACION_MULTISET edificio=… relacion=nueva\|guardada\|sin-immersal dif_caja=… dist=… t=…` | Cada ubicación aceptada y cuánto se separa su caja de la de Immersal. |
| `PRIMERA_UBICACION sistema=immersal\|multiset edificio=… t=…` | La primera ubicación de cada sistema en la sesión, en segundos desde «Comenzar». |

**Cuándo convendría cambiarse a MultiSet** (umbrales míos, para discutir):

- Su primera ubicación llega claramente antes que la de Immersal desde los mismos puntos. Hoy Immersal tarda entre 9 y 33 s.
- Acierta más de la mitad de las consultas.
- `dif_caja` se mantiene bajo ~0,5–1 m: la caja cian queda sobre la amarilla.

Si MultiSet es más rápido pero `dif_caja` es grande, una de las dos cajas está corrida: compáralas con el edificio real en
las capturas antes de decidir.

---

## Otras cosas a tener en cuenta

- Cada consulta manda una imagen de la cámara a la nube de MultiSet, con lo que salga en ella (gente incluida).
- La latencia depende de la red del campus: una consulta tarda ~2 s según MultiSet, más el envío de la imagen.
- **Volver a compilar** (con el Editor de Unity cerrado), desde `C:\dev\AncoRA-multiset`:
  ```powershell
  powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.BuildAndroid
  ```
- Al terminar la prueba, desactiva la credencial en el portal y desinstala AncoRA MultiSet si ya no se usa (la app normal
  no se ve afectada).
