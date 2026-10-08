# Paseo virtual — plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Una escena Android que carga los mapas Immersal de los edificios de la escuela (un XR Space por mapa) y muestra la caja y el nombre de cada edificio al ubicarse, con panel de ajuste en terreno y automatización de Editor.

**Architecture:** La lógica que decide (configuración, ajuste, visibilidad, texto de estado, guardado, colocación inicial) va en clases C# chicas probadas en EditMode. Los MonoBehaviour (`PaseoTour`, `PaseoMapContent`, `PaseoLabel`, `PaseoHud`, `PaseoFieldAdjust`) solo conectan esas clases con el SDK y la pantalla. `PaseoSetup` (Editor) arma la escena desde `Assets/AncoRA/Paseo/<Edificio>/edificio.json`, la valida y compila; una prueba de humo lo ejercita con mapas de ejemplo del SDK.

**Tech Stack:** Unity 6000.6.0f1, URP, AR Foundation 6.5 + ARCore, Immersal Core 2.4.0, Unity Test Framework (EditMode, NUnit) sin asmdef, `JsonUtility`, IMGUI, `TextMesh`.

**Spec:** `docs/superpowers/specs/2026-10-08-paseo-virtual-design.md`

## Global Constraints

- Unity **6000.6.0f1**; Immersal Core **2.4.0** (se verifica la versión antes de usar `SimpleSample`).
- Solo Android: IL2CPP, ARM64, OpenGLES3, `allowUnsafeCode`; APK suelto. No tocar la configuración de iOS.
- Un solo `ARSession`, `XROrigin`, cámara e `ImmersalSDK`; un `DeviceLocalization`; **ningún `ServerLocalization` ni token** (nunca en código, escena, logs ni Git).
- **Un XR Space por mapa**; el `XR Map` en identidad dentro de su espacio; `ProcessPoses = false` y sin `PoseFilter`/`PoseSmoother`.
- Runtime en `namespace AncorRA.AR` (`Assets/AncoRA/Scripts/Paseo/`); Editor en `namespace AncorRA.Editor` (`Assets/AncoRA/Editor/Paseo/`, mismo patrón que `BuildAndroid` y los pilotos); pruebas en `namespace AncorRA.Tests` (`Assets/AncoRA/Editor/Tests/`).
- Textos en pantalla y logs en **español**; comentarios en **inglés**. Prefijo de log `[AncoRA Paseo]`.
- IMGUI y `TextMesh` usan la fuente por defecto de Unity: **solo caracteres Latin-1** (á, é, ñ, «», ·). Nada de ✓ ni otros símbolos (por eso el banner dice «ubicado» y no ✓ como en el ejemplo de la spec).
- IMGUI con `GUI.*` posicional (no `GUILayout`), dentro de `GuiSafeArea.Rect`.
- JSON con `JsonUtility` y clases `[Serializable]` con campos públicos en español (mismo patrón que el `EdificioFieldAdjustmentData` existente).
- `PlayerPrefs` con prefijo `AncoRA.Paseo.v1.`.
- Datos: `Assets/AncoRA/Paseo/<Edificio>/edificio.json` + `.bytes`; carpetas que empiezan con `_` no son edificios. Escena: `Assets/Scenes/PaseoIngenieria.unity`.
- Build de **desarrollo**, applicationId `com.ancora.ucnar.paseo`, nombre «AncoRA Paseo», salida `Builds/Android/AncoRAPaseo.apk`; se restauran id y nombre aunque falle.
- Unity se usa **en serie** y con el Editor cerrado. No instalar ni abrir la app por adb sin que el usuario lo pida.
- Commits en español, terminados en `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. No commitear los archivos que Unity regenera (`.vscode/settings.json`, `Assets/Plugins/Android/*`, `Assets/Settings/URP-Performant.asset`, `ProjectSettings/GvhProjectSettings.xml`, `ProjectSettings/Packages/...`, `Assets/XR/Settings/OpenXR Editor Settings.asset*`): usar siempre `git add <rutas>` explícitas.

## Review Focus

1. **Dos resultados en el mismo ciclo para dos mapas del mismo edificio** → manda el que se marcó último, de forma determinista (prueba en Task 4).
2. **Un `.bytes` en la carpeta de un edificio que no está en `edificio.json`** → aviso explícito, no se ignora en silencio (Task 2).
3. **Un ajuste de campo que nombra un edificio o mapa que no existe** → no se aplica nada y se listan los problemas (Task 3).
4. **Se pierde el tracking y vuelve** → ninguna caja reaparece hasta que su mapa localice de nuevo (Task 4).
5. **Cámara mirando al suelo en la primera localización** → la caja inicial queda en una pose finita, sin NaN (Task 6).

---

## Cómo correr Unity en este plan

Todas las verificaciones usan `Tools/UnityBatch.ps1` (Task 1), con el Editor de Unity **cerrado**, desde la raíz del repo y en
PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoConfigTests
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode compile
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSmokeTest.Run
```

Cada corrida tarda 1–3 minutos. Con errores de compilación Unity no genera resultados de pruebas: el script lista las
líneas `error CS…`.

---

### Task 1: Herramienta para correr Unity en batch

**Files:**
- Create: `Tools/UnityBatch.ps1`

**Interfaces:**
- Produces: `Tools/UnityBatch.ps1 -Mode compile|tests|method [-Filter <fixture>] [-Method <Clase.Metodo>]`; código de salida = el de Unity (0 = OK); logs y resultados en `Logs/batch/` (ignorado por git).

- [ ] **Step 1: Escribir el script**

```powershell
<#
.SYNOPSIS
  Runs Unity in batch mode on this project and summarizes the outcome: compile errors, EditMode test results, or the
  log lines of an -executeMethod. The Unity Editor must be closed.
#>
param(
    [Parameter(Mandatory = $true)][ValidateSet('compile', 'tests', 'method')][string]$Mode,
    [string]$Method,
    [string]$Filter
)
$ErrorActionPreference = 'Stop'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
$project = Split-Path -Parent $PSScriptRoot

if (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue) {
    Write-Host 'Unity ya está abierto; ciérralo antes de usar el modo batch.'
    exit 10
}

$outDir = Join-Path $project 'Logs\batch'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$log = Join-Path $outDir "$Mode-$stamp.log"
$unityArgs = @('-batchmode', '-nographics', '-projectPath', "`"$project`"", '-logFile', "`"$log`"")
$results = $null
switch ($Mode) {
    'compile' { $unityArgs += '-quit' }
    'method' {
        if (-not $Method) { throw 'Falta -Method.' }
        $unityArgs += @('-quit', '-executeMethod', $Method)
    }
    'tests' {
        $results = Join-Path $outDir "tests-$stamp.xml"
        $unityArgs += @('-runTests', '-testPlatform', 'EditMode', '-testResults', "`"$results`"")
        if ($Filter) { $unityArgs += @('-testFilter', $Filter) }
    }
}

$process = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru
$null = $process.Handle   # keeps ExitCode readable after the process ends
$process.WaitForExit()
# Unity can leave helper processes running for a moment after the main one returns.
while (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue) { Start-Sleep -Seconds 2 }
$code = $process.ExitCode
Write-Host "Unity terminó con código $code. Log: $log"

$text = if (Test-Path $log) { Get-Content $log } else { @() }
$compileErrors = $text | Select-String -Pattern 'error CS\d+' | ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique
if ($compileErrors) {
    Write-Host 'ERRORES DE COMPILACIÓN:'
    $compileErrors | ForEach-Object { Write-Host "  $_" }
}
if ($Mode -eq 'method') {
    $text | Select-String -Pattern '\[AncoRA|Exception' | ForEach-Object { Write-Host "  $($_.Line.Trim())" }
}
if ($Mode -eq 'tests') {
    if ($results -and (Test-Path $results)) {
        [xml]$xml = Get-Content $results
        $run = $xml.'test-run'
        Write-Host "Pruebas: $($run.result) | total $($run.total), pasaron $($run.passed), fallaron $($run.failed)"
        foreach ($case in $xml.SelectNodes("//test-case[@result='Failed']")) {
            $message = $case.SelectSingleNode('failure/message')
            Write-Host "  FALLÓ $($case.fullname): $(if ($message) { $message.InnerText.Trim() })"
        }
    }
    else {
        Write-Host 'No se generó el archivo de resultados (¿errores de compilación?).'
    }
}
exit $code
```

- [ ] **Step 2: Verificar la compilación**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode compile`
Expected: `Unity terminó con código 0`, sin `ERRORES DE COMPILACIÓN`.

- [ ] **Step 3: Verificar el modo de pruebas (aún sin pruebas)**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests`
Expected: código 0 y sin `ERRORES DE COMPILACIÓN`. Con cero pruebas Unity puede informar `total 0` o no generar el
archivo de resultados; las dos salidas son correctas aquí (lo que se comprueba es que el script lanza Unity, espera y resume).

- [ ] **Step 4: Commit**

```bash
git add Tools/UnityBatch.ps1
git commit -m "Agregar script para correr Unity en batch

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `PaseoConfig` — leer y validar `edificio.json`

**Files:**
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoConfig.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoConfigTests.cs`

**Interfaces:**
- Produces:
  - `[Serializable] class PaseoBuildingConfig { string nombre; float[] tamano; bool solido; List<PaseoMapConfig> mapas; }`
  - `[Serializable] class PaseoMapConfig { int id; string archivo; PaseoBoxPose caja; }`
  - `[Serializable] class PaseoBoxPose { float[] posicion; float giro; bool colocada; }`
  - `class PaseoValidation { List<string> Errors; List<string> Warnings; bool Ok; }`
  - `static class PaseoConfig { const string FileName = "edificio.json"; float[] DefaultSize; PaseoBuildingConfig Parse(string json); string Serialize(PaseoBuildingConfig c); int? IdFromFileName(string file); PaseoValidation Validate(string buildingId, PaseoBuildingConfig c, ICollection<string> filesInFolder, bool rejectSampleIds); List<string> ValidateTour(IEnumerable<KeyValuePair<string, PaseoBuildingConfig>> buildings); }`

- [ ] **Step 1: Escribir las pruebas**

```csharp
using System;
using System.Collections.Generic;
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoConfigTests
    {
        const string Full = @"{
  ""nombre"": ""Ciencias Básicas"",
  ""tamano"": [30, 10, 15],
  ""solido"": true,
  ""mapas"": [
    { ""id"": 152192, ""archivo"": ""152192-csbasicasgael.bytes"",
      ""caja"": { ""posicion"": [1, 2, 3], ""giro"": 45, ""colocada"": true } },
    { ""id"": 152196, ""archivo"": ""152196-csbasicasgael2.bytes"" }
  ]
}";

        static readonly string[] Files = { "152192-csbasicasgael.bytes", "152196-csbasicasgael2.bytes", "edificio.json" };

        [Test]
        public void Parse_ReadsAllFields()
        {
            var c = PaseoConfig.Parse(Full);
            Assert.AreEqual("Ciencias Básicas", c.nombre);
            CollectionAssert.AreEqual(new[] { 30f, 10f, 15f }, c.tamano);
            Assert.IsTrue(c.solido);
            Assert.AreEqual(2, c.mapas.Count);
            Assert.AreEqual(152192, c.mapas[0].id);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, c.mapas[0].caja.posicion);
            Assert.AreEqual(45f, c.mapas[0].caja.giro);
            Assert.IsTrue(c.mapas[0].caja.colocada);
        }

        [Test]
        public void Parse_FillsDefaultsForMissingSizeAndBox()
        {
            var c = PaseoConfig.Parse(@"{ ""nombre"": ""X1"", ""mapas"": [ { ""id"": 152198, ""archivo"": ""152198-x1gael.bytes"" } ] }");
            CollectionAssert.AreEqual(PaseoConfig.DefaultSize, c.tamano);
            Assert.IsNotNull(c.mapas[0].caja);
            Assert.AreEqual(3, c.mapas[0].caja.posicion.Length);
            Assert.IsFalse(c.mapas[0].caja.colocada);
        }

        [Test]
        public void Parse_MissingMapsGivesEmptyList()
        {
            var c = PaseoConfig.Parse(@"{ ""nombre"": ""Teología"" }");
            Assert.IsNotNull(c.mapas);
            Assert.AreEqual(0, c.mapas.Count);
        }

        [Test]
        public void Parse_MalformedJsonThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => PaseoConfig.Parse("{ nombre: "));
            Assert.Throws<FormatException>(() => PaseoConfig.Parse("   "));
        }

        [Test]
        public void SerializeThenParse_KeepsAccentsAndValues()
        {
            var c = PaseoConfig.Parse(Full);
            var again = PaseoConfig.Parse(PaseoConfig.Serialize(c));
            Assert.AreEqual("Ciencias Básicas", again.nombre);
            Assert.AreEqual(152196, again.mapas[1].id);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, again.mapas[0].caja.posicion);
        }

        [Test]
        public void IdFromFileName_ParsesLeadingNumber()
        {
            Assert.AreEqual(152192, PaseoConfig.IdFromFileName("152192-csbasicasgael.bytes"));
            Assert.IsNull(PaseoConfig.IdFromFileName("csbasicas.bytes"));
            Assert.IsNull(PaseoConfig.IdFromFileName("152192-csbasicasgael.json"));
        }

        [Test]
        public void Validate_AcceptsAGoodBuilding()
        {
            var v = PaseoConfig.Validate("CienciasBasicas", PaseoConfig.Parse(Full), Files, rejectSampleIds: true);
            Assert.IsTrue(v.Ok, string.Join("\n", v.Errors));
            Assert.AreEqual(0, v.Warnings.Count, string.Join("\n", v.Warnings));
        }

        [Test]
        public void Validate_ReportsMissingFile()
        {
            var v = PaseoConfig.Validate("CienciasBasicas", PaseoConfig.Parse(Full), new[] { "152192-csbasicasgael.bytes" }, true);
            Assert.IsFalse(v.Ok);
            StringAssert.Contains("152196-csbasicasgael2.bytes", string.Join("\n", v.Errors));
        }

        [Test]
        public void Validate_ReportsIdThatDoesNotMatchFile()
        {
            var c = PaseoConfig.Parse(Full);
            c.mapas[1].id = 999;
            var v = PaseoConfig.Validate("CienciasBasicas", c, Files, true);
            StringAssert.Contains("es del mapa 152196", string.Join("\n", v.Errors));
        }

        [Test]
        public void Validate_RejectsSdkSampleIdsOnlyWhenAsked()
        {
            var c = PaseoConfig.Parse(@"{ ""nombre"": ""A"", ""mapas"": [ { ""id"": 90687, ""archivo"": ""90687-SampleMapA.bytes"" } ] }");
            var files = new[] { "90687-SampleMapA.bytes" };
            Assert.IsFalse(PaseoConfig.Validate("A", c, files, rejectSampleIds: true).Ok);
            Assert.IsTrue(PaseoConfig.Validate("A", c, files, rejectSampleIds: false).Ok);
        }

        [Test]
        public void Validate_ReportsBadSizeAndDuplicateMap()
        {
            var c = PaseoConfig.Parse(Full);
            c.tamano = new[] { 20f, 0f };
            c.mapas[1].id = 152192;
            c.mapas[1].archivo = "152192-csbasicasgael.bytes";
            var errors = string.Join("\n", PaseoConfig.Validate("CienciasBasicas", c, Files, true).Errors);
            StringAssert.Contains("tamano", errors);
            StringAssert.Contains("repetido", errors);
        }

        [Test]
        public void Validate_WarnsAboutUnlistedBytesAndEmptyBuilding()
        {
            var c = PaseoConfig.Parse(@"{ ""nombre"": ""Teología"" }");
            var v = PaseoConfig.Validate("Teologia", c, new[] { "152195-Teologianicowo.bytes" }, true);
            Assert.IsTrue(v.Ok);
            var warnings = string.Join("\n", v.Warnings);
            StringAssert.Contains("no tiene mapas", warnings);
            StringAssert.Contains("152195-Teologianicowo.bytes", warnings);
        }

        [Test]
        public void ValidateTour_ReportsMapUsedByTwoBuildings()
        {
            var a = PaseoConfig.Parse(@"{ ""nombre"": ""A"", ""mapas"": [ { ""id"": 1, ""archivo"": ""1-a.bytes"" } ] }");
            var b = PaseoConfig.Parse(@"{ ""nombre"": ""B"", ""mapas"": [ { ""id"": 1, ""archivo"": ""1-a.bytes"" } ] }");
            var errors = PaseoConfig.ValidateTour(new Dictionary<string, PaseoBuildingConfig> { ["A"] = a, ["B"] = b });
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("mapa 1", errors[0]);
        }
    }
}
```

- [ ] **Step 2: Correr las pruebas y ver que fallan**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoConfigTests`
Expected: `ERRORES DE COMPILACIÓN` con `CS0246` (`PaseoConfig` / `PaseoBuildingConfig` no existen).

- [ ] **Step 3: Implementar**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// edificio.json of one building of the tour. Field names are Spanish because the team edits these files by hand,
    /// and they match the JSON keys one to one (JsonUtility).
    /// </summary>
    [Serializable]
    public sealed class PaseoBuildingConfig
    {
        public string nombre;
        public float[] tamano = { 20f, 8f, 12f };
        public bool solido;
        public List<PaseoMapConfig> mapas = new();
    }

    [Serializable]
    public sealed class PaseoMapConfig
    {
        public int id;
        public string archivo;
        public PaseoBoxPose caja = new();
    }

    /// <summary>Box pose in the frame of its map (= its XR Space, since the XR Map sits at identity).</summary>
    [Serializable]
    public sealed class PaseoBoxPose
    {
        public float[] posicion = { 0f, 0f, 12f };
        public float giro;
        public bool colocada;
    }

    public sealed class PaseoValidation
    {
        public readonly List<string> Errors = new();
        public readonly List<string> Warnings = new();
        public bool Ok => Errors.Count == 0;
    }

    public static class PaseoConfig
    {
        public const string FileName = "edificio.json";

        // A filler value, not a measurement: the team sizes each box on site.
        public static readonly float[] DefaultSize = { 20f, 8f, 12f };

        static readonly HashSet<int> SampleMapIds = new() { 90687, 90688, 90689, 90690 };
        static readonly Regex MapFilePattern = new(@"^(\d+)-[^\\/]+\.bytes$");

        public static PaseoBuildingConfig Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("edificio.json está vacío.");
            PaseoBuildingConfig config;
            try
            {
                config = JsonUtility.FromJson<PaseoBuildingConfig>(json);
            }
            catch (ArgumentException e)
            {
                throw new FormatException($"edificio.json no es JSON válido: {e.Message}");
            }
            if (config == null)
                throw new FormatException("edificio.json no es un objeto JSON.");

            // JsonUtility leaves absent arrays and objects null or empty; normalise so callers never check.
            if (config.tamano == null || config.tamano.Length == 0)
                config.tamano = (float[])DefaultSize.Clone();
            config.mapas ??= new List<PaseoMapConfig>();
            foreach (var map in config.mapas)
            {
                map.caja ??= new PaseoBoxPose();
                if (map.caja.posicion == null || map.caja.posicion.Length == 0)
                    map.caja.posicion = new[] { 0f, 0f, 12f };
            }
            return config;
        }

        public static string Serialize(PaseoBuildingConfig config) => JsonUtility.ToJson(config, true);

        public static int? IdFromFileName(string file)
        {
            var match = MapFilePattern.Match(file ?? "");
            return match.Success && int.TryParse(match.Groups[1].Value, out int id) ? id : null;
        }

        public static PaseoValidation Validate(string buildingId, PaseoBuildingConfig config, ICollection<string> filesInFolder,
            bool rejectSampleIds)
        {
            var result = new PaseoValidation();
            string where = $"Edificio {buildingId}";
            if (string.IsNullOrWhiteSpace(config.nombre))
                result.Errors.Add($"{where}: falta «nombre».");
            if (config.tamano == null || config.tamano.Length != 3 || config.tamano.Any(v => !IsFinite(v) || v < 0.1f))
                result.Errors.Add($"{where}: «tamano» debe tener tres medidas (ancho, alto, fondo) de al menos 0,1 m.");
            if (config.mapas.Count == 0)
                result.Warnings.Add($"{where}: no tiene mapas todavía; se omite de la escena.");

            var seen = new HashSet<int>();
            foreach (var map in config.mapas)
            {
                string label = $"{where}, mapa {map.id}";
                if (!seen.Add(map.id))
                    result.Errors.Add($"{label}: está repetido.");
                if (rejectSampleIds && SampleMapIds.Contains(map.id))
                    result.Errors.Add($"{label}: es un mapa de ejemplo del SDK, no del campus.");
                if (map.caja.posicion.Length != 3 || map.caja.posicion.Any(v => !IsFinite(v)) || !IsFinite(map.caja.giro))
                    result.Errors.Add($"{label}: «caja.posicion» debe tener tres números y «giro» un número.");
                if (string.IsNullOrWhiteSpace(map.archivo))
                {
                    result.Errors.Add($"{label}: falta «archivo».");
                    continue;
                }
                if (!filesInFolder.Contains(map.archivo))
                    result.Errors.Add($"{label}: no existe {map.archivo} en la carpeta.");
                int? fileId = IdFromFileName(map.archivo);
                if (fileId == null)
                    result.Errors.Add($"{label}: «{map.archivo}» no tiene la forma <id>-<nombre>.bytes.");
                else if (fileId.Value != map.id)
                    result.Errors.Add($"{label}: el archivo {map.archivo} es del mapa {fileId.Value}.");
            }

            foreach (string file in filesInFolder)
                if (file.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase) && config.mapas.All(m => m.archivo != file))
                    result.Warnings.Add($"{where}: {file} está en la carpeta pero no en edificio.json; no se usa.");
            return result;
        }

        public static List<string> ValidateTour(IEnumerable<KeyValuePair<string, PaseoBuildingConfig>> buildings)
        {
            var errors = new List<string>();
            var owner = new Dictionary<int, string>();
            foreach (var pair in buildings)
                foreach (var map in pair.Value.mapas)
                {
                    if (owner.TryGetValue(map.id, out string other) && other != pair.Key)
                        errors.Add($"El mapa {map.id} aparece en {other} y en {pair.Key}.");
                    else
                        owner[map.id] = pair.Key;
                }
            return errors;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
```

- [ ] **Step 4: Correr las pruebas y ver que pasan**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoConfigTests`
Expected: `Pruebas: Passed | total 13, pasaron 13, fallaron 0`. Si `Parse_FillsDefaultsForMissingSizeAndBox` falla, revisar la normalización de `Parse`, no la prueba.

- [ ] **Step 5: Commit**

```bash
git add Assets/AncoRA/Scripts/Paseo Assets/AncoRA/Scripts/Paseo.meta Assets/AncoRA/Editor/Tests Assets/AncoRA/Editor/Tests.meta
git commit -m "Agregar lectura y validacion de edificio.json del paseo

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `PaseoAdjustment` — JSON de «Copiar valores» y cómo se aplica

**Files:**
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoAdjustment.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoAdjustmentTests.cs`

**Interfaces:**
- Consumes: `PaseoBuildingConfig`, `PaseoMapConfig`, `PaseoBoxPose`, `PaseoConfig.Parse` (Task 2).
- Produces:
  - `[Serializable] class PaseoAdjustment { string generado; string build; List<PaseoAdjustmentBuilding> edificios; List<string> ApplyTo(IDictionary<string, PaseoBuildingConfig> configs); }`
  - `[Serializable] class PaseoAdjustmentBuilding { string id; float[] tamano; bool solido; List<PaseoAdjustmentMap> mapas; }`
  - `[Serializable] class PaseoAdjustmentMap { int id; float[] posicion; float giro; bool colocada; }`
  - `ApplyTo` es todo o nada: si devuelve problemas, no modificó nada.

- [ ] **Step 1: Escribir las pruebas**

```csharp
using System.Collections.Generic;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoAdjustmentTests
    {
        static Dictionary<string, PaseoBuildingConfig> Tour() => new()
        {
            ["CienciasBasicas"] = PaseoConfig.Parse(@"{ ""nombre"": ""Ciencias Básicas"", ""mapas"": [
                { ""id"": 152192, ""archivo"": ""152192-csbasicasgael.bytes"" },
                { ""id"": 152196, ""archivo"": ""152196-csbasicasgael2.bytes"" } ] }"),
            ["X1"] = PaseoConfig.Parse(@"{ ""nombre"": ""X1"", ""mapas"": [ { ""id"": 152198, ""archivo"": ""152198-x1gael.bytes"" } ] }")
        };

        static PaseoAdjustment Adjustment(string building, int mapId, bool placed) => new()
        {
            generado = "2026-10-08T12:00:00",
            edificios = new List<PaseoAdjustmentBuilding>
            {
                new()
                {
                    id = building, tamano = new[] { 30f, 10f, 15f }, solido = true,
                    mapas = new List<PaseoAdjustmentMap> { new() { id = mapId, posicion = new[] { 1f, 2f, 3f }, giro = 45f, colocada = placed } }
                }
            }
        };

        [Test]
        public void ApplyTo_UpdatesPlacedMapAndBuildingSize()
        {
            var tour = Tour();
            var problems = Adjustment("CienciasBasicas", 152196, placed: true).ApplyTo(tour);
            Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
            var building = tour["CienciasBasicas"];
            CollectionAssert.AreEqual(new[] { 30f, 10f, 15f }, building.tamano);
            Assert.IsTrue(building.solido);
            var box = building.mapas[1].caja;
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, box.posicion);
            Assert.AreEqual(45f, box.giro);
            Assert.IsTrue(box.colocada);
            Assert.IsFalse(building.mapas[0].caja.colocada, "the other map of the building was not adjusted");
        }

        [Test]
        public void ApplyTo_IgnoresPoseOfMapsTheTeamDidNotPlace()
        {
            var tour = Tour();
            Adjustment("X1", 152198, placed: false).ApplyTo(tour);
            var box = tour["X1"].mapas[0].caja;
            Assert.IsFalse(box.colocada);
            CollectionAssert.AreEqual(new[] { 0f, 0f, 12f }, box.posicion);
            CollectionAssert.AreEqual(new[] { 30f, 10f, 15f }, tour["X1"].tamano, "size is still applied");
        }

        [Test]
        public void ApplyTo_UnknownMapChangesNothing()
        {
            var tour = Tour();
            var adjustment = Adjustment("X1", 152198, true);
            adjustment.edificios[0].mapas.Add(new PaseoAdjustmentMap { id = 777, posicion = new[] { 0f, 0f, 0f }, colocada = true });
            var problems = adjustment.ApplyTo(tour);
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("777", problems[0]);
            Assert.IsFalse(tour["X1"].mapas[0].caja.colocada, "nothing is applied when there are problems");
            CollectionAssert.AreEqual(PaseoConfig.DefaultSize, tour["X1"].tamano);
        }

        [Test]
        public void ApplyTo_UnknownBuildingIsAProblem()
        {
            var problems = Adjustment("Teologia", 152195, true).ApplyTo(Tour());
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("Teologia", problems[0]);
        }

        [Test]
        public void ApplyTo_RejectsBadPositionAndSize()
        {
            var adjustment = Adjustment("X1", 152198, true);
            adjustment.edificios[0].mapas[0].posicion = new[] { 1f, float.NaN, 3f };
            adjustment.edificios[0].tamano = new[] { 30f, 10f };
            var problems = adjustment.ApplyTo(Tour());
            Assert.AreEqual(2, problems.Count, string.Join("\n", problems));
        }

        [Test]
        public void JsonRoundTrip_KeepsValues()
        {
            string json = JsonUtility.ToJson(Adjustment("X1", 152198, true));
            var back = JsonUtility.FromJson<PaseoAdjustment>(json);
            Assert.AreEqual("X1", back.edificios[0].id);
            Assert.AreEqual(152198, back.edificios[0].mapas[0].id);
            Assert.AreEqual(45f, back.edificios[0].mapas[0].giro);
        }
    }
}
```

- [ ] **Step 2: Correr y ver que fallan**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoAdjustmentTests`
Expected: `CS0246` por `PaseoAdjustment`.

- [ ] **Step 3: Implementar**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace AncorRA.AR
{
    /// <summary>
    /// What "Copiar valores" writes on the phone and PaseoSetup.ApplyFieldAdjustment reads in the Editor. Field names are
    /// Spanish and match the JSON keys (JsonUtility).
    /// </summary>
    [Serializable]
    public sealed class PaseoAdjustment
    {
        public string generado;
        public string build;
        public List<PaseoAdjustmentBuilding> edificios = new();

        /// <summary>
        /// Writes the adjustment into the building configs. All or nothing: when it returns problems, nothing changed.
        /// Size and fill always apply to a known building; a map pose applies only if the team placed it.
        /// </summary>
        public List<string> ApplyTo(IDictionary<string, PaseoBuildingConfig> configs)
        {
            var problems = new List<string>();
            var buildings = edificios ?? new List<PaseoAdjustmentBuilding>();
            foreach (var building in buildings)
            {
                if (building.id == null || !configs.TryGetValue(building.id, out var config))
                {
                    problems.Add($"El ajuste trae el edificio «{building.id}», que no tiene carpeta en el paseo.");
                    continue;
                }
                if (building.tamano == null || building.tamano.Length != 3 || building.tamano.Any(v => !IsFinite(v) || v < 0.1f))
                    problems.Add($"Edificio {building.id}: «tamano» inválido en el ajuste.");
                foreach (var map in building.mapas ?? new List<PaseoAdjustmentMap>())
                {
                    if (config.mapas.All(m => m.id != map.id))
                        problems.Add($"Edificio {building.id}: el mapa {map.id} no está en su edificio.json.");
                    else if (map.colocada && (map.posicion == null || map.posicion.Length != 3 ||
                                              map.posicion.Any(v => !IsFinite(v)) || !IsFinite(map.giro)))
                        problems.Add($"Edificio {building.id}, mapa {map.id}: posición o giro inválidos en el ajuste.");
                }
            }
            if (problems.Count > 0)
                return problems;

            foreach (var building in buildings)
            {
                var config = configs[building.id];
                config.tamano = (float[])building.tamano.Clone();
                config.solido = building.solido;
                foreach (var map in building.mapas ?? new List<PaseoAdjustmentMap>())
                {
                    if (!map.colocada)
                        continue;
                    var box = config.mapas.First(m => m.id == map.id).caja;
                    box.posicion = (float[])map.posicion.Clone();
                    box.giro = map.giro;
                    box.colocada = true;
                }
            }
            return problems;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class PaseoAdjustmentBuilding
    {
        public string id;
        public float[] tamano;
        public bool solido;
        public List<PaseoAdjustmentMap> mapas = new();
    }

    [Serializable]
    public sealed class PaseoAdjustmentMap
    {
        public int id;
        public float[] posicion;
        public float giro;
        public bool colocada;
    }
}
```

- [ ] **Step 4: Correr y ver que pasan**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoAdjustmentTests`
Expected: `total 6, pasaron 6, fallaron 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/AncoRA/Scripts/Paseo/PaseoAdjustment.cs Assets/AncoRA/Scripts/Paseo/PaseoAdjustment.cs.meta Assets/AncoRA/Editor/Tests/PaseoAdjustmentTests.cs Assets/AncoRA/Editor/Tests/PaseoAdjustmentTests.cs.meta
git commit -m "Agregar el ajuste de campo del paseo y su aplicacion a edificio.json

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `PaseoVisibility` — qué caja se ve

**Files:**
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoVisibility.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoVisibilityTests.cs`

**Interfaces:**
- Produces: `sealed class PaseoVisibility { const float HeldAfterSeconds = 10f; IReadOnlyList<string> Buildings; void AddMap(int mapId, string buildingId); bool MarkLocated(int mapId, float time); void LoseTracking(); bool IsLocated(int mapId); int VisibleMap(string buildingId) /* -1 = ninguna */; bool IsHeld(int mapId, float now); IEnumerable<int> MapsOf(string buildingId); }`

- [ ] **Step 1: Escribir las pruebas**

```csharp
using System;
using System.Linq;
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoVisibilityTests
    {
        static PaseoVisibility Tour()
        {
            var v = new PaseoVisibility();
            v.AddMap(152192, "CienciasBasicas");
            v.AddMap(152196, "CienciasBasicas");
            v.AddMap(152198, "X1");
            return v;
        }

        [Test]
        public void NothingVisibleBeforeAnyLocalization()
        {
            var v = Tour();
            Assert.AreEqual(-1, v.VisibleMap("CienciasBasicas"));
            Assert.AreEqual(-1, v.VisibleMap("X1"));
        }

        [Test]
        public void LocatedMapBecomesVisible()
        {
            var v = Tour();
            Assert.IsTrue(v.MarkLocated(152198, 1f));
            Assert.AreEqual(152198, v.VisibleMap("X1"));
            Assert.AreEqual(-1, v.VisibleMap("CienciasBasicas"));
        }

        [Test]
        public void TwoBuildingsCanBeVisibleAtOnce()
        {
            var v = Tour();
            v.MarkLocated(152198, 1f);
            v.MarkLocated(152192, 2f);
            Assert.AreEqual(152198, v.VisibleMap("X1"));
            Assert.AreEqual(152192, v.VisibleMap("CienciasBasicas"));
        }

        [Test]
        public void LatestMapOfABuildingWins()
        {
            var v = Tour();
            v.MarkLocated(152192, 1f);
            v.MarkLocated(152196, 2f);
            Assert.AreEqual(152196, v.VisibleMap("CienciasBasicas"));
            v.MarkLocated(152192, 3f);
            Assert.AreEqual(152192, v.VisibleMap("CienciasBasicas"));
        }

        [Test]
        public void SameCycleTieGoesToTheOneMarkedLast()
        {
            var v = Tour();
            v.MarkLocated(152196, 5f);
            v.MarkLocated(152192, 5f);
            Assert.AreEqual(152192, v.VisibleMap("CienciasBasicas"));
        }

        [Test]
        public void LosingTrackingHidesEverythingUntilANewLocalization()
        {
            var v = Tour();
            v.MarkLocated(152192, 1f);
            v.MarkLocated(152198, 1f);
            v.LoseTracking();
            Assert.AreEqual(-1, v.VisibleMap("CienciasBasicas"));
            Assert.AreEqual(-1, v.VisibleMap("X1"));
            v.MarkLocated(152198, 2f);
            Assert.AreEqual(152198, v.VisibleMap("X1"));
            Assert.AreEqual(-1, v.VisibleMap("CienciasBasicas"), "a building does not come back on another map's localization");
        }

        [Test]
        public void HeldAfterTenSecondsWithoutANewSuccess()
        {
            var v = Tour();
            v.MarkLocated(152198, 1f);
            Assert.IsFalse(v.IsHeld(152198, 10.9f));
            Assert.IsTrue(v.IsHeld(152198, 11f));
            v.MarkLocated(152198, 11f);
            Assert.IsFalse(v.IsHeld(152198, 11.5f));
        }

        [Test]
        public void UnknownMapIsIgnored()
        {
            var v = Tour();
            Assert.IsFalse(v.MarkLocated(999, 1f));
            Assert.IsFalse(v.IsLocated(999));
        }

        [Test]
        public void BuildingsKeepInsertionOrderAndRepeatedMapThrows()
        {
            var v = Tour();
            CollectionAssert.AreEqual(new[] { "CienciasBasicas", "X1" }, v.Buildings.ToArray());
            CollectionAssert.AreEquivalent(new[] { 152192, 152196 }, v.MapsOf("CienciasBasicas").ToArray());
            Assert.Throws<ArgumentException>(() => v.AddMap(152198, "X1"));
        }
    }
}
```

- [ ] **Step 2: Correr y ver que fallan**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoVisibilityTests`
Expected: `CS0246` por `PaseoVisibility`.

- [ ] **Step 3: Implementar**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace AncorRA.AR
{
    /// <summary>
    /// Decides which box is shown. Every map has its own XR Space, so a located map stays valid until the phone loses
    /// tracking. Per building, the map that located last wins; a sequence number (not the time) breaks ties, so two
    /// results in the same localization cycle are still ordered.
    /// </summary>
    public sealed class PaseoVisibility
    {
        public const float HeldAfterSeconds = 10f;

        sealed class Entry
        {
            public string Building;
            public bool Located;
            public long Sequence;
            public float LastSuccess;
        }

        readonly Dictionary<int, Entry> maps = new();
        readonly List<string> buildings = new();
        long sequence;

        public IReadOnlyList<string> Buildings => buildings;

        public void AddMap(int mapId, string buildingId)
        {
            if (maps.ContainsKey(mapId))
                throw new ArgumentException($"El mapa {mapId} está repetido en el paseo.");
            maps[mapId] = new Entry { Building = buildingId };
            if (!buildings.Contains(buildingId))
                buildings.Add(buildingId);
        }

        /// <summary>Call when the map's XR Space has received the pose of a successful localization.</summary>
        public bool MarkLocated(int mapId, float time)
        {
            if (!maps.TryGetValue(mapId, out var entry))
                return false;
            entry.Located = true;
            entry.Sequence = ++sequence;
            entry.LastSuccess = time;
            return true;
        }

        /// <summary>Losing the phone's own tracking invalidates every placed space: each map must localize again.</summary>
        public void LoseTracking()
        {
            foreach (var entry in maps.Values)
                entry.Located = false;
        }

        public bool IsLocated(int mapId) => maps.TryGetValue(mapId, out var entry) && entry.Located;

        public int VisibleMap(string buildingId)
        {
            int best = -1;
            long bestSequence = -1;
            foreach (var pair in maps)
                if (pair.Value.Located && pair.Value.Building == buildingId && pair.Value.Sequence > bestSequence)
                {
                    best = pair.Key;
                    bestSequence = pair.Value.Sequence;
                }
            return best;
        }

        public bool IsHeld(int mapId, float now) =>
            maps.TryGetValue(mapId, out var entry) && entry.Located && now - entry.LastSuccess >= HeldAfterSeconds;

        public IEnumerable<int> MapsOf(string buildingId) =>
            maps.Where(pair => pair.Value.Building == buildingId).Select(pair => pair.Key);
    }
}
```

- [ ] **Step 4: Correr y ver que pasan**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoVisibilityTests`
Expected: `total 9, pasaron 9, fallaron 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/AncoRA/Scripts/Paseo/PaseoVisibility.cs Assets/AncoRA/Scripts/Paseo/PaseoVisibility.cs.meta Assets/AncoRA/Editor/Tests/PaseoVisibilityTests.cs Assets/AncoRA/Editor/Tests/PaseoVisibilityTests.cs.meta
git commit -m "Agregar las reglas de visibilidad de las cajas del paseo

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `PaseoStatusText` — el aviso de arriba

**Files:**
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoStatusText.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoStatusTextTests.cs`

**Interfaces:**
- Produces:
  - `enum PaseoStatusLevel { Waiting, Ok, Error }`
  - `enum PaseoBuildingState { Searching, Located, Held, MapFailed }`
  - `readonly struct PaseoBuildingStatus { string Name; PaseoBuildingState State; PaseoBuildingStatus(string name, PaseoBuildingState state); }`
  - `struct PaseoStatusInputs { string Error; bool ArUnsupported; bool SdkReady; float SecondsSinceStart; string MapStillLoading; bool Tracking; IReadOnlyList<PaseoBuildingStatus> Buildings; }`
  - `static class PaseoStatusText { const float SlowStartSeconds = 15f; string Describe(PaseoStatusInputs s, out PaseoStatusLevel level); }`

- [ ] **Step 1: Escribir las pruebas**

```csharp
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoStatusTextTests
    {
        static PaseoStatusInputs Ready(params PaseoBuildingStatus[] buildings) => new()
        {
            SdkReady = true, Tracking = true, SecondsSinceStart = 3f, Buildings = buildings
        };

        static string Describe(PaseoStatusInputs s, out PaseoStatusLevel level) => PaseoStatusText.Describe(s, out level);

        [Test]
        public void ErrorWinsOverEverything()
        {
            var s = Ready();
            s.Error = "SDK no quedó listo";
            Assert.AreEqual("Error: SDK no quedó listo", Describe(s, out var level));
            Assert.AreEqual(PaseoStatusLevel.Error, level);
        }

        [Test]
        public void UnsupportedPhone()
        {
            var s = Ready();
            s.ArUnsupported = true;
            StringAssert.Contains("no es compatible", Describe(s, out var level));
            Assert.AreEqual(PaseoStatusLevel.Error, level);
        }

        [Test]
        public void StartingThenSlowStart()
        {
            var s = new PaseoStatusInputs { SdkReady = false, SecondsSinceStart = 2f };
            Assert.AreEqual("Iniciando...", Describe(s, out var level));
            Assert.AreEqual(PaseoStatusLevel.Waiting, level);
            s.SecondsSinceStart = 16f;
            StringAssert.Contains("no arrancó", Describe(s, out level));
            Assert.AreEqual(PaseoStatusLevel.Error, level);
        }

        [Test]
        public void LoadingMap()
        {
            var s = Ready();
            s.MapStillLoading = "152192 (Ciencias Básicas)";
            Assert.AreEqual("Cargando mapa 152192 (Ciencias Básicas)...", Describe(s, out _));
        }

        [Test]
        public void NotTrackingAsksToMoveSlowly()
        {
            var s = Ready();
            s.Tracking = false;
            StringAssert.Contains("Mueve el teléfono despacio", Describe(s, out _));
        }

        [Test]
        public void NothingLocatedYet()
        {
            var s = Ready(new PaseoBuildingStatus("X1", PaseoBuildingState.Searching));
            Assert.AreEqual("Apunta a un edificio del paseo.", Describe(s, out var level));
            Assert.AreEqual(PaseoStatusLevel.Waiting, level);
            s.SecondsSinceStart = 20f;
            Assert.AreEqual("Acércate a un edificio del paseo y muévete despacio.", Describe(s, out _));
        }

        [Test]
        public void ListsEveryBuildingOnceOneIsLocated()
        {
            var s = Ready(
                new PaseoBuildingStatus("Ciencias Básicas", PaseoBuildingState.Located),
                new PaseoBuildingStatus("X1", PaseoBuildingState.Held),
                new PaseoBuildingStatus("EIC", PaseoBuildingState.Searching),
                new PaseoBuildingStatus("Teología", PaseoBuildingState.MapFailed));
            Assert.AreEqual(
                "Ciencias Básicas: ubicado · X1: ubicado (mantenido) · EIC: buscando... · Teología: el mapa no cargó",
                Describe(s, out var level));
            Assert.AreEqual(PaseoStatusLevel.Ok, level);
        }

        [Test]
        public void NullBuildingListIsTreatedAsEmpty()
        {
            var s = Ready();
            s.Buildings = null;
            Assert.AreEqual("Apunta a un edificio del paseo.", Describe(s, out _));
        }
    }
}
```

- [ ] **Step 2: Correr y ver que fallan**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoStatusTextTests`
Expected: `CS0246` por `PaseoStatusInputs`.

- [ ] **Step 3: Implementar**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace AncorRA.AR
{
    public enum PaseoStatusLevel { Waiting, Ok, Error }

    public enum PaseoBuildingState { Searching, Located, Held, MapFailed }

    public readonly struct PaseoBuildingStatus
    {
        public readonly string Name;
        public readonly PaseoBuildingState State;

        public PaseoBuildingStatus(string name, PaseoBuildingState state)
        {
            Name = name;
            State = state;
        }
    }

    public struct PaseoStatusInputs
    {
        public string Error;
        public bool ArUnsupported;
        public bool SdkReady;
        public float SecondsSinceStart;
        public string MapStillLoading;
        public bool Tracking;
        public IReadOnlyList<PaseoBuildingStatus> Buildings;
    }

    /// <summary>
    /// The always-visible banner, in plain Spanish. Only Latin-1 characters: IMGUI's default font on Android has no
    /// check marks or other symbols.
    /// </summary>
    public static class PaseoStatusText
    {
        public const float SlowStartSeconds = 15f;

        public static string Describe(PaseoStatusInputs s, out PaseoStatusLevel level)
        {
            if (!string.IsNullOrEmpty(s.Error))
            {
                level = PaseoStatusLevel.Error;
                return "Error: " + s.Error;
            }
            if (s.ArUnsupported)
            {
                level = PaseoStatusLevel.Error;
                return "Este teléfono no es compatible con realidad aumentada (ARCore).";
            }
            if (!s.SdkReady)
            {
                if (s.SecondsSinceStart > SlowStartSeconds)
                {
                    level = PaseoStatusLevel.Error;
                    return "El sistema de ubicación no arrancó. Cierra y vuelve a abrir la app.";
                }
                level = PaseoStatusLevel.Waiting;
                return "Iniciando...";
            }
            level = PaseoStatusLevel.Waiting;
            if (!string.IsNullOrEmpty(s.MapStillLoading))
                return $"Cargando mapa {s.MapStillLoading}...";
            if (!s.Tracking)
                return "Mueve el teléfono despacio para que la cámara reconozca el entorno.";

            var buildings = s.Buildings ?? Array.Empty<PaseoBuildingStatus>();
            if (!buildings.Any(b => b.State == PaseoBuildingState.Located || b.State == PaseoBuildingState.Held))
                return s.SecondsSinceStart > SlowStartSeconds
                    ? "Acércate a un edificio del paseo y muévete despacio."
                    : "Apunta a un edificio del paseo.";

            level = PaseoStatusLevel.Ok;
            return string.Join(" · ", buildings.Select(b => b.State switch
            {
                PaseoBuildingState.Located => $"{b.Name}: ubicado",
                PaseoBuildingState.Held => $"{b.Name}: ubicado (mantenido)",
                PaseoBuildingState.MapFailed => $"{b.Name}: el mapa no cargó",
                _ => $"{b.Name}: buscando..."
            }));
        }
    }
}
```

- [ ] **Step 4: Correr y ver que pasan**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoStatusTextTests`
Expected: `total 8, pasaron 8, fallaron 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/AncoRA/Scripts/Paseo/PaseoStatusText.cs Assets/AncoRA/Scripts/Paseo/PaseoStatusText.cs.meta Assets/AncoRA/Editor/Tests/PaseoStatusTextTests.cs Assets/AncoRA/Editor/Tests/PaseoStatusTextTests.cs.meta
git commit -m "Agregar el texto de estado del paseo para varios edificios

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `PaseoBoxStore` y `PaseoPlacement` — guardado en el teléfono y caja inicial

**Files:**
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoBoxStore.cs`
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoPlacement.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoBoxStoreTests.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoPlacementTests.cs`

**Interfaces:**
- Produces:
  - `sealed class PaseoBoxStore { const string DefaultPrefix = "AncoRA.Paseo.v1."; PaseoBoxStore(string keyPrefix = DefaultPrefix); bool TryLoadPose(int mapId, out Vector3 position, out float yaw); void SavePose(int mapId, Vector3 position, float yaw); bool TryLoadSize(string buildingId, out Vector3 size, out bool solid); void SaveSize(string buildingId, Vector3 size, bool solid); void Clear(IEnumerable<int> mapIds, IEnumerable<string> buildingIds); }`
  - `static class PaseoPlacement { const float DistanceMeters = 12f; const float BaseBelowCameraMeters = 1.5f; void InitialPose(Vector3 cameraPosition, Vector3 cameraForward, Vector3 sizeMeters, Vector3 spacePosition, Quaternion spaceRotation, out Vector3 localPosition, out float localYaw); }` — la caja tiene el pivote en el centro; la cara +Z (la de la X) queda mirando a la cámara, a `DistanceMeters`, con la base `BaseBelowCameraMeters` bajo la cámara.

- [ ] **Step 1: Escribir las pruebas**

`Assets/AncoRA/Editor/Tests/PaseoBoxStoreTests.cs`:

```csharp
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoBoxStoreTests
    {
        const string Prefix = "AncoRA.Test.Paseo.";
        PaseoBoxStore store;

        [SetUp]
        public void SetUp() => store = new PaseoBoxStore(Prefix);

        [TearDown]
        public void TearDown() => store.Clear(new[] { 1, 2 }, new[] { "A" });

        [Test]
        public void PoseRoundTrip()
        {
            Assert.IsFalse(store.TryLoadPose(1, out _, out _));
            store.SavePose(1, new Vector3(1f, 2f, 3f), 45f);
            Assert.IsTrue(store.TryLoadPose(1, out var position, out float yaw));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), position);
            Assert.AreEqual(45f, yaw);
            Assert.IsFalse(store.TryLoadPose(2, out _, out _), "poses are per map");
        }

        [Test]
        public void SizeRoundTrip()
        {
            Assert.IsFalse(store.TryLoadSize("A", out _, out _));
            store.SaveSize("A", new Vector3(30f, 10f, 15f), true);
            Assert.IsTrue(store.TryLoadSize("A", out var size, out bool solid));
            Assert.AreEqual(new Vector3(30f, 10f, 15f), size);
            Assert.IsTrue(solid);
        }

        [Test]
        public void ClearForgetsEverything()
        {
            store.SavePose(1, Vector3.one, 1f);
            store.SaveSize("A", Vector3.one, false);
            store.Clear(new[] { 1 }, new[] { "A" });
            Assert.IsFalse(store.TryLoadPose(1, out _, out _));
            Assert.IsFalse(store.TryLoadSize("A", out _, out _));
        }
    }
}
```

`Assets/AncoRA/Editor/Tests/PaseoPlacementTests.cs`:

```csharp
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoPlacementTests
    {
        static readonly Vector3 Size = new(20f, 8f, 12f);

        static void AssertClose(Vector3 expected, Vector3 actual) =>
            Assert.Less(Vector3.Distance(expected, actual), 1e-3f, $"esperado {expected}, obtenido {actual}");

        [Test]
        public void SpaceAtIdentity_BoxAheadWithFrontFacingCamera()
        {
            PaseoPlacement.InitialPose(new Vector3(0f, 1.6f, 0f), Vector3.forward, Size, Vector3.zero, Quaternion.identity,
                out var position, out float yaw);
            // Front face at 12 m, so the centre is 12 + 6 ahead; base 1.5 m below the camera, so the centre is 4 m above it.
            AssertClose(new Vector3(0f, 4.1f, 18f), position);
            Assert.AreEqual(180f, yaw, 1e-3f);
        }

        [Test]
        public void RotatedAndMovedSpace_ResultIsLocalToTheSpace()
        {
            PaseoPlacement.InitialPose(new Vector3(0f, 1.6f, 0f), Vector3.forward, Size,
                new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), out var position, out float yaw);
            AssertClose(new Vector3(-18f, 4.1f, -10f), position);
            Assert.AreEqual(90f, yaw, 1e-3f);
        }

        [Test]
        public void PitchedCameraUsesItsHeadingOnly()
        {
            var pitchedDown = Quaternion.Euler(40f, 0f, 0f) * Vector3.forward;
            PaseoPlacement.InitialPose(new Vector3(0f, 1.6f, 0f), pitchedDown, Size, Vector3.zero, Quaternion.identity,
                out var position, out _);
            AssertClose(new Vector3(0f, 4.1f, 18f), position);
        }

        [Test]
        public void CameraLookingStraightDown_GivesAFinitePose()
        {
            PaseoPlacement.InitialPose(new Vector3(0f, 1.6f, 0f), Vector3.down, Size, Vector3.zero, Quaternion.identity,
                out var position, out float yaw);
            Assert.IsFalse(float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) || float.IsNaN(yaw));
            Assert.Greater(position.magnitude, 10f);
        }
    }
}
```

- [ ] **Step 2: Correr y ver que fallan**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter "AncorRA.Tests.PaseoBoxStoreTests;AncorRA.Tests.PaseoPlacementTests"`
Expected: `CS0246` por `PaseoBoxStore` y `PaseoPlacement`.

- [ ] **Step 3: Implementar `PaseoBoxStore`**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Box values the team adjusted on this phone. A pose only means something inside its own map, so it is stored per
    /// map; size and fill belong to the building and are shared by its maps. If the meaning of a value changes, bump the
    /// prefix (v1 → v2) instead of reusing it silently.
    /// </summary>
    public sealed class PaseoBoxStore
    {
        public const string DefaultPrefix = "AncoRA.Paseo.v1.";

        readonly string prefix;

        public PaseoBoxStore(string keyPrefix = DefaultPrefix) => prefix = keyPrefix;

        public bool TryLoadPose(int mapId, out Vector3 position, out float yaw)
        {
            string key = prefix + mapId;
            if (PlayerPrefs.GetInt(key + ".Set", 0) != 1)
            {
                position = default;
                yaw = 0f;
                return false;
            }
            position = new Vector3(PlayerPrefs.GetFloat(key + ".X"), PlayerPrefs.GetFloat(key + ".Y"), PlayerPrefs.GetFloat(key + ".Z"));
            yaw = PlayerPrefs.GetFloat(key + ".Yaw");
            return true;
        }

        public void SavePose(int mapId, Vector3 position, float yaw)
        {
            string key = prefix + mapId;
            PlayerPrefs.SetFloat(key + ".X", position.x);
            PlayerPrefs.SetFloat(key + ".Y", position.y);
            PlayerPrefs.SetFloat(key + ".Z", position.z);
            PlayerPrefs.SetFloat(key + ".Yaw", yaw);
            PlayerPrefs.SetInt(key + ".Set", 1);
            PlayerPrefs.Save();
        }

        public bool TryLoadSize(string buildingId, out Vector3 size, out bool solid)
        {
            string key = prefix + buildingId;
            if (PlayerPrefs.GetInt(key + ".SizeSet", 0) != 1)
            {
                size = default;
                solid = false;
                return false;
            }
            size = new Vector3(PlayerPrefs.GetFloat(key + ".W"), PlayerPrefs.GetFloat(key + ".H"), PlayerPrefs.GetFloat(key + ".D"));
            solid = PlayerPrefs.GetInt(key + ".Solid", 0) == 1;
            return true;
        }

        public void SaveSize(string buildingId, Vector3 size, bool solid)
        {
            string key = prefix + buildingId;
            PlayerPrefs.SetFloat(key + ".W", size.x);
            PlayerPrefs.SetFloat(key + ".H", size.y);
            PlayerPrefs.SetFloat(key + ".D", size.z);
            PlayerPrefs.SetInt(key + ".Solid", solid ? 1 : 0);
            PlayerPrefs.SetInt(key + ".SizeSet", 1);
            PlayerPrefs.Save();
        }

        public void Clear(IEnumerable<int> mapIds, IEnumerable<string> buildingIds)
        {
            foreach (int mapId in mapIds)
                foreach (string suffix in new[] { ".X", ".Y", ".Z", ".Yaw", ".Set" })
                    PlayerPrefs.DeleteKey(prefix + mapId + suffix);
            foreach (string buildingId in buildingIds)
                foreach (string suffix in new[] { ".W", ".H", ".D", ".Solid", ".SizeSet" })
                    PlayerPrefs.DeleteKey(prefix + buildingId + suffix);
            PlayerPrefs.Save();
        }
    }
}
```

- [ ] **Step 4: Implementar `PaseoPlacement`**

```csharp
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Where a box that nobody placed yet appears the first time its map localizes: in front of the camera, on the
    /// ground, facing it, so the team can see it and move it with the panel. Not a measurement.
    /// </summary>
    public static class PaseoPlacement
    {
        public const float DistanceMeters = 12f;
        public const float BaseBelowCameraMeters = 1.5f;

        public static void InitialPose(Vector3 cameraPosition, Vector3 cameraForward, Vector3 sizeMeters,
            Vector3 spacePosition, Quaternion spaceRotation, out Vector3 localPosition, out float localYaw)
        {
            // Heading only: a building never tilts with the phone. Looking straight down has no heading; use world forward.
            var heading = new Vector3(cameraForward.x, 0f, cameraForward.z);
            heading = heading.sqrMagnitude < 1e-6f ? Vector3.forward : heading.normalized;

            // The box pivot is its centre: push it half a depth past the front face and lift it half a height.
            var center = cameraPosition + heading * (DistanceMeters + sizeMeters.z * 0.5f)
                         + Vector3.up * (sizeMeters.y * 0.5f - BaseBelowCameraMeters);
            // +Z is the front face (the one with the X); it must face the camera.
            var worldRotation = Quaternion.LookRotation(-heading, Vector3.up);

            var toLocal = Quaternion.Inverse(spaceRotation);
            localPosition = toLocal * (center - spacePosition);
            localYaw = (toLocal * worldRotation).eulerAngles.y;
        }
    }
}
```

- [ ] **Step 5: Correr y ver que pasan**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter "AncorRA.Tests.PaseoBoxStoreTests;AncorRA.Tests.PaseoPlacementTests"`
Expected: `total 7, pasaron 7, fallaron 0`.

- [ ] **Step 6: Commit**

```bash
git add Assets/AncoRA/Scripts/Paseo/PaseoBoxStore.cs Assets/AncoRA/Scripts/Paseo/PaseoBoxStore.cs.meta Assets/AncoRA/Scripts/Paseo/PaseoPlacement.cs Assets/AncoRA/Scripts/Paseo/PaseoPlacement.cs.meta Assets/AncoRA/Editor/Tests/PaseoBoxStoreTests.cs Assets/AncoRA/Editor/Tests/PaseoBoxStoreTests.cs.meta Assets/AncoRA/Editor/Tests/PaseoPlacementTests.cs Assets/AncoRA/Editor/Tests/PaseoPlacementTests.cs.meta
git commit -m "Agregar el guardado de cajas y la colocacion inicial del paseo

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Componentes de la escena (`PaseoLabel`, `PaseoMapContent`, `PaseoTour`, `PaseoHud`, `PaseoFieldAdjust`)

Estos MonoBehaviour solo conectan las clases probadas con el SDK y la pantalla. Aquí se verifica que compilan; la
prueba de humo de la Task 8 los arma en una escena y comprueba su cableado.

**Files:**
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoLabel.cs`
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoMapContent.cs`
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoTour.cs`
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoHud.cs`
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoFieldAdjust.cs`

**Interfaces:**
- Consumes: `PaseoVisibility`, `PaseoStatusText`/`PaseoStatusInputs`/`PaseoBuildingStatus`/`PaseoBuildingState`, `PaseoBoxStore`, `PaseoPlacement`, `PaseoAdjustment*` (Tasks 3–6); `EdificioFacadeFrame` (`WidthMeters`, `HeightMeters`, `DepthMeters`, `Solid`, `PlacedByTeam`, `HasSolidMaterial`, `IsBox`, `SetSize`, `SetSolid`, `MarkPlaced`, `SetVisible`); `GuiSafeArea.Rect`; Immersal `ImmersalSDK`, `Localizer`, `XRSpace`, `XRMap`, `MapManager`, `Core`.
- Produces (los usa la Task 8):
  - `PaseoLabel : MonoBehaviour` (requiere `TextMesh`): `void Show(string text, float boxHeight)`, `void SetVisible(bool visible)`.
  - `PaseoMapContent : MonoBehaviour`: `void Configure(int mapId, string buildingId, string buildingName, EdificioFacadeFrame box, PaseoLabel label)`; `int MapId`; `string BuildingId`; `string BuildingName`; `EdificioFacadeFrame Box`; `PaseoLabel Label`; `bool Placed`; `Vector3 LocalPosition`; `float LocalYaw`; `Vector3 SizeMeters`; `void SetPose(Vector3 localPosition, float yaw, bool placed)`; `void SetSize(Vector3 size, bool solid)`; `void SetVisible(bool visible)`.
  - `PaseoTour : MonoBehaviour`: `void Configure(ImmersalSDK sdk, Localizer localizer, PaseoMapContent[] contents)`; `ImmersalSDK Sdk`; `Localizer Localizer`; `IReadOnlyList<PaseoMapContent> Contents`; `string Error`; `bool SdkReady`; `float SecondsSinceStart`; `string FirstMapStillLoading`; `IReadOnlyList<PaseoBuildingStatus> BuildingStatuses()`; `string DescribeMaps()`; `PaseoMapContent EditableContent`; `void SaveEdit(PaseoMapContent content, Vector3 position, float yaw, Vector3 size, bool solid)`; `void ForgetSavedAndReload()`; `PaseoAdjustment BuildAdjustment()`.
  - `PaseoHud : MonoBehaviour`: `void Configure(PaseoTour tour)`; `PaseoTour Tour`; `bool HudVisible`.
  - `PaseoFieldAdjust : MonoBehaviour`: `void Configure(PaseoTour tour, PaseoHud hud)`; `PaseoTour Tour`; `PaseoHud Hud`.

- [ ] **Step 1: `PaseoLabel`**

```csharp
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Building name floating over its box. Turns only around Y to face the camera, like a sign. Uses a legacy TextMesh
    /// with Unity's built-in font because the project has no TextMeshPro resources imported.
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    public sealed class PaseoLabel : MonoBehaviour
    {
        const float GapAboveBoxMeters = 1.5f;

        TextMesh textMesh;
        MeshRenderer meshRenderer;

        void Awake() => Init();

        void Init()
        {
            if (textMesh == null)
                textMesh = GetComponent<TextMesh>();
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();
        }

        /// <summary>Sets the text and sits the label just above a box of the given height (box pivot = centre).</summary>
        public void Show(string text, float boxHeight)
        {
            Init();
            textMesh.text = text;
            transform.localPosition = new Vector3(0f, boxHeight * 0.5f + GapAboveBoxMeters, 0f);
        }

        public void SetVisible(bool visible)
        {
            Init();
            meshRenderer.enabled = visible;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || meshRenderer == null || !meshRenderer.enabled)
                return;
            // TextMesh reads correctly when the camera looks along the label's +Z.
            var away = transform.position - cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f)
                return;
            transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }
    }
}
```

- [ ] **Step 2: `PaseoMapContent`**

```csharp
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The content of one map: the building box and its name, child of that map's XR Space. Its local pose is in the
    /// map's frame (the XR Map sits at identity inside its space).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaseoMapContent : MonoBehaviour
    {
        [SerializeField] int mapId;
        [SerializeField] string buildingId;
        [SerializeField] string buildingName;
        [SerializeField] EdificioFacadeFrame box;
        [SerializeField] PaseoLabel label;

        public int MapId => mapId;
        public string BuildingId => buildingId;
        public string BuildingName => buildingName;
        public EdificioFacadeFrame Box => box;
        public PaseoLabel Label => label;
        public bool Placed => box != null && box.PlacedByTeam;
        public Vector3 LocalPosition => transform.localPosition;
        public float LocalYaw => transform.localEulerAngles.y;
        public Vector3 SizeMeters => new(box.WidthMeters, box.HeightMeters, box.DepthMeters);

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(int map, string building, string displayName, EdificioFacadeFrame frame, PaseoLabel nameLabel)
        {
            mapId = map;
            buildingId = building;
            buildingName = displayName;
            box = frame;
            label = nameLabel;
            RefreshLabel();
        }

        public void SetPose(Vector3 localPosition, float yaw, bool placed)
        {
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            box.MarkPlaced(placed);
            RefreshLabel();
        }

        public void SetSize(Vector3 size, bool solid)
        {
            box.SetSize(size.x, size.y, size.z);
            box.SetSolid(solid);
            RefreshLabel();
        }

        public void SetVisible(bool visible)
        {
            box.SetVisible(visible);
            label.SetVisible(visible);
        }

        void RefreshLabel()
        {
            if (label != null && box != null)
                label.Show(box.PlacedByTeam ? buildingName : buildingName + " (sin colocar)", box.HeightMeters);
        }
    }
}
```

- [ ] **Step 3: `PaseoTour`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Immersal;
using Immersal.XR;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.AR
{
    /// <summary>
    /// Runs the tour. Every map has its own XR Space, so a localization moves only the space of its map and the boxes
    /// of other buildings stay where their last localization left them. A box is shown only after its space received
    /// the pose: the result event fires before the SDK moves the space, and showing earlier would flash the box at the
    /// world origin. None of this proves the box is physically on the building.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class PaseoTour : MonoBehaviour
    {
        const string Tag = "[AncoRA Paseo]";
        const float MinChangeMeters = 0.002f;
        const float MinChangeDegrees = 0.05f;
        // A re-localization that lands on (almost) the same pose does not move the space measurably.
        const float AcceptWithoutMoveSeconds = 0.5f;
        const float SdkStartTimeoutSeconds = 15f;

        [SerializeField] ImmersalSDK sdk;
        [SerializeField] Localizer localizer;
        [SerializeField] PaseoMapContent[] contents = Array.Empty<PaseoMapContent>();

        sealed class MapRuntime
        {
            public PaseoMapContent Content;
            public XRSpace Space;
            public bool Loaded;
            public bool LoadFailed;
            public int Attempts;
            public int Successes;
            public bool Waiting;
            public float WaitingSince;
            public bool SpaceEverMoved;
            public bool InitialPlacementPending;
            public bool Shown;
            public Vector3 LastPosition;
            public Quaternion LastRotation;
        }

        readonly Dictionary<int, MapRuntime> maps = new();
        readonly PaseoVisibility visibility = new();
        PaseoBoxStore store;
        float sceneStart;
        string error = "";
        bool reportedTimeout;
        bool wasTracking;
        int lastLocatedMapId = -1;

        public ImmersalSDK Sdk => sdk;
        public Localizer Localizer => localizer;
        public IReadOnlyList<PaseoMapContent> Contents => contents;
        public string Error => error;
        public bool SdkReady => sdk != null && sdk.IsReady;
        public float SecondsSinceStart => Time.realtimeSinceStartup - sceneStart;

        public string FirstMapStillLoading
        {
            get
            {
                var pending = maps.Values.FirstOrDefault(m => !m.Loaded && !m.LoadFailed);
                return pending == null ? null : $"{pending.Content.MapId} ({pending.Content.BuildingName})";
            }
        }

        /// <summary>The box the team can edit: the one of the map that located last, while it is shown.</summary>
        public PaseoMapContent EditableContent =>
            lastLocatedMapId >= 0 && maps.TryGetValue(lastLocatedMapId, out var runtime) && runtime.Shown ? runtime.Content : null;

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(ImmersalSDK immersal, Localizer immersalLocalizer, PaseoMapContent[] mapContents)
        {
            sdk = immersal;
            localizer = immersalLocalizer;
            contents = mapContents;
        }

        void Awake()
        {
            // The development console pops up on every error and covers the adjust bar; errors still reach logcat.
            Debug.developerConsoleVisible = false;
            sceneStart = Time.realtimeSinceStartup;
            XRMapVisualization.pointCloudVisible = false;
            store = new PaseoBoxStore();

            foreach (var content in contents)
            {
                var space = content.GetComponentInParent<XRSpace>();
                var runtime = new MapRuntime
                {
                    Content = content,
                    Space = space,
                    LastPosition = space.transform.position,
                    LastRotation = space.transform.rotation
                };
                maps[content.MapId] = runtime;
                visibility.AddMap(content.MapId, content.BuildingId);
                ApplyStored(runtime);
                content.SetVisible(false);
            }

            if (MapManager.MapRegisteredAndLoaded == null)
                MapManager.MapRegisteredAndLoaded = new UnityEvent<int>();
            MapManager.MapRegisteredAndLoaded.AddListener(OnMapLoaded);
            if (localizer != null)
                localizer.OnLocalizationResult.AddListener(OnLocalizationResult);

            Debug.Log($"{Tag} Inicio del paseo; versión {Application.version}, build {Application.buildGUID}, SDK {ImmersalSDK.sdkVersion}; " +
                      $"mapas [{string.Join(", ", contents.Select(c => $"{c.MapId} {c.BuildingId}"))}]; sin token embebido.");
        }

        void OnDestroy()
        {
            MapManager.MapRegisteredAndLoaded?.RemoveListener(OnMapLoaded);
            if (localizer != null)
                localizer.OnLocalizationResult.RemoveListener(OnLocalizationResult);
        }

        // Phone values win over the scene: the team adjusted them on site after the scene was baked.
        void ApplyStored(MapRuntime runtime)
        {
            var content = runtime.Content;
            if (store.TryLoadSize(content.BuildingId, out var size, out bool solid))
                content.SetSize(size, solid);
            if (store.TryLoadPose(content.MapId, out var position, out float yaw))
                content.SetPose(position, yaw, placed: true);
            else
                runtime.InitialPlacementPending = !content.Placed;
        }

        void OnMapLoaded(int id)
        {
            if (!maps.TryGetValue(id, out var runtime))
                return;
            // This event follows Core.LoadMap; an imported TextAsset alone is not proof of loading.
            int points = Core.GetPointCloudSize(id);
            runtime.Loaded = points > 0;
            runtime.LoadFailed = !runtime.Loaded;
            if (runtime.Loaded)
                Debug.Log($"{Tag} Mapa {id} ({runtime.Content.BuildingName}) cargado en el plugin: {points} puntos.");
            else
                Debug.LogError($"{Tag} ERROR: el mapa {id} ({runtime.Content.BuildingName}) se cargó sin puntos ({points}); ese edificio no podrá ubicarse.");
        }

        void OnLocalizationResult(ILocalizationResults results)
        {
            bool tracking = ARSession.state == ARSessionState.SessionTracking;
            foreach (var result in results.Results)
            {
                if (!maps.TryGetValue(result.MapId, out var runtime))
                    continue;
                runtime.Attempts++;
                if (!result.Success)
                    continue;
                runtime.Successes++;
                if (!runtime.Loaded || !tracking || runtime.Waiting)
                    continue;
                runtime.Waiting = true;
                runtime.WaitingSince = Time.realtimeSinceStartup;
            }
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            bool tracking = ARSession.state == ARSessionState.SessionTracking;
            if (wasTracking && !tracking)
            {
                visibility.LoseTracking();
                Debug.Log($"{Tag} El teléfono perdió el tracking: se ocultan las cajas hasta que cada mapa vuelva a localizar.");
            }
            wasTracking = tracking;

            foreach (var runtime in maps.Values)
            {
                var t = runtime.Space.transform;
                bool moved = Vector3.Distance(t.position, runtime.LastPosition) >= MinChangeMeters ||
                             Quaternion.Angle(t.rotation, runtime.LastRotation) >= MinChangeDegrees;
                if (moved)
                {
                    runtime.SpaceEverMoved = true;
                    runtime.LastPosition = t.position;
                    runtime.LastRotation = t.rotation;
                }
                if (!runtime.Waiting)
                    continue;
                bool settled = moved || (runtime.SpaceEverMoved && now - runtime.WaitingSince >= AcceptWithoutMoveSeconds);
                if (!settled)
                    continue;
                runtime.Waiting = false;
                if (!tracking)
                    continue;

                int id = runtime.Content.MapId;
                bool wasLocated = visibility.IsLocated(id);
                visibility.MarkLocated(id, now);
                lastLocatedMapId = id;
                if (runtime.InitialPlacementPending)
                    PlaceInitially(runtime);
                if (!wasLocated)
                    Debug.Log($"{Tag} {runtime.Content.BuildingName} ubicado con el mapa {id} (t={now - sceneStart:F1}s). Alineación física NO verificada.");
            }

            foreach (var runtime in maps.Values)
            {
                bool show = tracking && visibility.VisibleMap(runtime.Content.BuildingId) == runtime.Content.MapId;
                if (show == runtime.Shown)
                    continue;
                runtime.Shown = show;
                runtime.Content.SetVisible(show);
                Debug.Log(show
                    ? $"{Tag} Se muestra {runtime.Content.BuildingName} con el mapa {runtime.Content.MapId}."
                    : $"{Tag} Se oculta la caja del mapa {runtime.Content.MapId} ({runtime.Content.BuildingName}).");
            }

            CheckSdkStart();
        }

        void PlaceInitially(MapRuntime runtime)
        {
            var cam = Camera.main;
            if (cam == null)
                return;
            var content = runtime.Content;
            var space = runtime.Space.transform;
            PaseoPlacement.InitialPose(cam.transform.position, cam.transform.forward, content.SizeMeters,
                space.position, space.rotation, out var position, out float yaw);
            content.SetPose(position, yaw, placed: false);
            runtime.InitialPlacementPending = false;
            Debug.Log($"{Tag} La caja del mapa {content.MapId} ({content.BuildingName}) no estaba colocada: se puso a " +
                      $"{PaseoPlacement.DistanceMeters} m delante de la cámara. Ajustarla con el panel del equipo.");
        }

        void CheckSdkStart()
        {
            if (reportedTimeout || SdkReady || SecondsSinceStart < SdkStartTimeoutSeconds)
                return;
            reportedTimeout = true;
            error = "El SDK no quedó listo en 15 s; revisar el log de inicio.";
            Debug.LogError($"{Tag} {error}");
        }

        public IReadOnlyList<PaseoBuildingStatus> BuildingStatuses()
        {
            float now = Time.realtimeSinceStartup;
            var list = new List<PaseoBuildingStatus>();
            foreach (string building in visibility.Buildings)
            {
                var own = maps.Values.Where(m => m.Content.BuildingId == building).ToList();
                int visible = visibility.VisibleMap(building);
                var state = own.All(m => m.LoadFailed) ? PaseoBuildingState.MapFailed
                    : visible < 0 ? PaseoBuildingState.Searching
                    : visibility.IsHeld(visible, now) ? PaseoBuildingState.Held
                    : PaseoBuildingState.Located;
                list.Add(new PaseoBuildingStatus(own[0].Content.BuildingName, state));
            }
            return list;
        }

        public string DescribeMaps()
        {
            var text = new StringBuilder();
            foreach (var runtime in maps.Values)
            {
                var c = runtime.Content;
                string load = runtime.Loaded ? "cargado" : runtime.LoadFailed ? "NO CARGÓ" : "cargando";
                text.AppendLine($"Mapa {c.MapId} {c.BuildingName}: {load} | intentos/éxitos {runtime.Attempts}/{runtime.Successes} | " +
                                $"{(visibility.IsLocated(c.MapId) ? "ubicado" : "sin ubicar")} | {(runtime.Shown ? "visible" : "oculto")} | " +
                                $"{(c.Placed ? "caja colocada" : "CAJA SIN COLOCAR")}");
            }
            return text.ToString().TrimEnd();
        }

        public void SaveEdit(PaseoMapContent content, Vector3 position, float yaw, Vector3 size, bool solid)
        {
            content.SetPose(position, yaw, placed: true);
            store.SavePose(content.MapId, position, yaw);
            foreach (var other in contents)
                if (other.BuildingId == content.BuildingId)
                    other.SetSize(size, solid);
            store.SaveSize(content.BuildingId, size, solid);
            if (maps.TryGetValue(content.MapId, out var runtime))
                runtime.InitialPlacementPending = false;
        }

        /// <summary>Forgets every value saved on this phone and reloads, so the scene values apply again.</summary>
        public void ForgetSavedAndReload()
        {
            store.Clear(contents.Select(c => c.MapId), contents.Select(c => c.BuildingId).Distinct());
            Debug.Log($"{Tag} Ajustes del teléfono borrados; se recarga la escena con los valores horneados.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public PaseoAdjustment BuildAdjustment()
        {
            var adjustment = new PaseoAdjustment { generado = DateTime.Now.ToString("s"), build = Application.buildGUID };
            foreach (var group in contents.GroupBy(c => c.BuildingId))
            {
                var size = group.First().SizeMeters;
                var building = new PaseoAdjustmentBuilding
                {
                    id = group.Key,
                    tamano = new[] { size.x, size.y, size.z },
                    solido = group.First().Box.Solid
                };
                foreach (var content in group)
                {
                    var p = content.LocalPosition;
                    building.mapas.Add(new PaseoAdjustmentMap
                    {
                        id = content.MapId, posicion = new[] { p.x, p.y, p.z }, giro = content.LocalYaw, colocada = content.Placed
                    });
                }
                adjustment.edificios.Add(building);
            }
            return adjustment;
        }
    }
}
```

- [ ] **Step 4: `PaseoHud`**

```csharp
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.AR
{
    /// <summary>
    /// Always-visible status banner for everyone, plus a detailed team HUD toggled with five taps on the top-left corner.
    /// Drawn inside the safe area (camera cutout and rounded corners).
    /// </summary>
    public sealed class PaseoHud : MonoBehaviour
    {
        const float TapWindowSeconds = 1.5f;

        [SerializeField] PaseoTour tour;

        bool hudVisible;
        int teamTaps;
        float lastTapTime;

        public PaseoTour Tour => tour;
        public bool HudVisible => hudVisible;

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(PaseoTour paseoTour) => tour = paseoTour;

        void OnGUI()
        {
            if (tour == null)
                return;
            var safe = GuiSafeArea.Rect;
            if (GUI.Button(new Rect(safe.x, safe.y, 120, 120), GUIContent.none, GUIStyle.none))
            {
                teamTaps = Time.realtimeSinceStartup - lastTapTime < TapWindowSeconds ? teamTaps + 1 : 1;
                lastTapTime = Time.realtimeSinceStartup;
                if (teamTaps >= 5)
                {
                    hudVisible = !hudVisible;
                    teamTaps = 0;
                }
            }

            float top = DrawBanner(safe) + 12f;
            if (!hudVisible)
                return;

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                fontSize = Mathf.Max(16, Screen.width / 52)
            };
            string text = $"AncoRA · Paseo (EQUIPO) | v{Application.version} | ARSession: {ARSession.state} | " +
                          $"SDK: {(tour.SdkReady ? "listo" : "esperando")}\n{tour.DescribeMaps()}\nAlineación física: NO verificada";
            if (!string.IsNullOrEmpty(tour.Error))
                text += $"\nERROR: {tour.Error}";
            float width = Mathf.Min(safe.width, 950);
            float height = style.CalcHeight(new GUIContent(text), width) + 12;
            GUI.Box(new Rect(safe.x, top, width, height), text, style);
        }

        float DrawBanner(Rect safe)
        {
            string message = PaseoStatusText.Describe(new PaseoStatusInputs
            {
                Error = tour.Error,
                ArUnsupported = ARSession.state == ARSessionState.Unsupported,
                SdkReady = tour.SdkReady,
                SecondsSinceStart = tour.SecondsSinceStart,
                MapStillLoading = tour.FirstMapStillLoading,
                Tracking = ARSession.state == ARSessionState.SessionTracking,
                Buildings = tour.BuildingStatuses()
            }, out var level);

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(20, Screen.width / 34)
            };
            style.normal.textColor = level == PaseoStatusLevel.Ok ? new Color(0.55f, 1f, 0.6f)
                : level == PaseoStatusLevel.Error ? new Color(1f, 0.55f, 0.55f)
                : new Color(1f, 0.93f, 0.55f);
            var content = new GUIContent("AncoRA · " + message);
            float height = style.CalcHeight(content, safe.width) + 12f;
            GUI.Box(new Rect(safe.x, safe.y, safe.width, height), content, style);
            return safe.y + height;
        }
    }
}
```

- [ ] **Step 5: `PaseoFieldAdjust`**

```csharp
using System;
using System.IO;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Team-only bottom bar (visible with the team HUD) to place the box of the map that located last. Every change is
    /// saved on the phone at once; "Copiar valores" exports all boxes for PaseoSetup.ApplyFieldAdjustment.
    /// </summary>
    public sealed class PaseoFieldAdjust : MonoBehaviour
    {
        const string Tag = "[AncoRA Paseo]";
        const float RestoreConfirmSeconds = 3f;
        static readonly float[] PositionSteps = { 1f, 0.25f, 0.05f };
        static readonly float[] AngleSteps = { 5f, 1f, 0.2f };
        static readonly float[] SizeSteps = { 2f, 0.5f, 0.1f };
        static readonly string[] StepNames = { "grueso", "medio", "fino" };

        [SerializeField] PaseoTour tour;
        [SerializeField] PaseoHud hud;

        bool open;
        int stepIndex = 1;
        int editingMapId = -1;
        Vector3 position;
        Vector3 size;
        float yaw;
        bool solid;
        string toast = "";
        float toastUntil;
        float restoreArmedUntil;

        public PaseoTour Tour => tour;
        public PaseoHud Hud => hud;

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(PaseoTour paseoTour, PaseoHud paseoHud)
        {
            tour = paseoTour;
            hud = paseoHud;
        }

        void Read(PaseoMapContent content)
        {
            editingMapId = content.MapId;
            position = content.LocalPosition;
            yaw = content.LocalYaw;
            size = content.SizeMeters;
            solid = content.Box.Solid;
        }

        void OnGUI()
        {
            if (tour == null || hud == null || !hud.HudVisible)
                return;
            var target = tour.EditableContent;
            if (target == null)
                editingMapId = -1;
            else if (target.MapId != editingMapId)
                Read(target);

            var safe = GuiSafeArea.Rect;
            float width = safe.width;
            float row = Mathf.Max(56f, Screen.height / 26f);
            int font = Mathf.Max(16, (int)(row * 0.42f));
            var button = new GUIStyle(GUI.skin.button) { fontSize = font };
            var label = new GUIStyle(GUI.skin.label) { fontSize = font, alignment = TextAnchor.MiddleLeft };
            var value = new GUIStyle(GUI.skin.label) { fontSize = font, alignment = TextAnchor.MiddleCenter };
            var wrapped = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, font - 4), alignment = TextAnchor.MiddleLeft, wordWrap = true };
            label.normal.textColor = value.normal.textColor = wrapped.normal.textColor = Color.white;

            // Rows: header, then (when open) a title line, seven steppers and the fill toggle, then the footer.
            int rows = !open ? 0 : target == null ? 1 : 9;
            float height = row * (1 + rows) + (open ? row : 0f);
            float top = safe.height - height;
            GUI.BeginGroup(safe);
            if (open)
                GUI.Box(new Rect(0, top, width, height), GUIContent.none);

            float third = width / 3f;
            if (GUI.Button(new Rect(0, top, third, row), "Ajuste: Caja", button))
                open = !open;
            if (open && GUI.Button(new Rect(third, top, third, row), $"Paso: {StepNames[stepIndex]}", button))
                stepIndex = (stepIndex + 1) % StepNames.Length;
            if (open && GUI.Button(new Rect(2 * third, top, third, row), "Cerrar", button))
                open = false;

            float y = top + row;
            if (open)
            {
                if (target == null)
                {
                    GUI.Label(new Rect(8, y, width - 16, row), "Ubícate con un edificio para ajustar su caja.", wrapped);
                    y += row;
                }
                else
                {
                    GUI.Label(new Rect(8, y, width - 16, row),
                        $"Editando: {target.BuildingName} (mapa {target.MapId}){(target.Placed ? "" : " - SIN COLOCAR")}", wrapped);
                    y += row;
                    float p = PositionSteps[stepIndex], a = AngleSteps[stepIndex], s = SizeSteps[stepIndex];
                    bool changed = false;
                    changed |= Stepper(ref y, row, width, "X (m)", ref position.x, p, "F2", label, value, button);
                    changed |= Stepper(ref y, row, width, "Y (m)", ref position.y, p, "F2", label, value, button);
                    changed |= Stepper(ref y, row, width, "Z (m)", ref position.z, p, "F2", label, value, button);
                    changed |= Stepper(ref y, row, width, "Giro (°)", ref yaw, a, "F1", label, value, button);
                    changed |= Stepper(ref y, row, width, "Ancho (m)", ref size.x, s, "F2", label, value, button);
                    changed |= Stepper(ref y, row, width, "Alto (m)", ref size.y, s, "F2", label, value, button);
                    changed |= Stepper(ref y, row, width, "Fondo (m)", ref size.z, s, "F2", label, value, button);
                    GUI.Label(new Rect(8, y, width * 0.34f, row), "Relleno", label);
                    if (target.Box.HasSolidMaterial &&
                        GUI.Button(new Rect(width * 0.36f, y, width * 0.64f, row), solid ? "Sólido (tapa el edificio)" : "Transparente", button))
                    {
                        solid = !solid;
                        changed = true;
                    }
                    y += row;
                    size = Vector3.Max(size, Vector3.one * 0.5f);
                    if (changed)
                        tour.SaveEdit(target, position, yaw, size, solid);
                }

                if (GUI.Button(new Rect(0, y, third, row), "Copiar valores", button))
                    CopyValues();
                bool armed = Time.realtimeSinceStartup < restoreArmedUntil;
                if (GUI.Button(new Rect(third, y, third, row), armed ? "Tocar otra vez" : "Restaurar escena", button))
                {
                    if (armed)
                        tour.ForgetSavedAndReload();
                    else
                    {
                        restoreArmedUntil = Time.realtimeSinceStartup + RestoreConfirmSeconds;
                        Show("Borra lo guardado en este teléfono");
                    }
                }
                if (Time.realtimeSinceStartup < toastUntil)
                    GUI.Label(new Rect(2 * third + 8, y, third - 8, row), toast, wrapped);
            }
            GUI.EndGroup();
        }

        static bool Stepper(ref float y, float row, float width, string name, ref float current, float step, string format,
            GUIStyle label, GUIStyle value, GUIStyle button)
        {
            bool changed = false;
            GUI.Label(new Rect(8, y, width * 0.34f, row), name, label);
            if (GUI.Button(new Rect(width * 0.36f, y, width * 0.2f, row), "-", button))
            {
                current -= step;
                changed = true;
            }
            GUI.Label(new Rect(width * 0.56f, y, width * 0.24f, row), current.ToString(format), value);
            if (GUI.Button(new Rect(width * 0.8f, y, width * 0.2f, row), "+", button))
            {
                current += step;
                changed = true;
            }
            y += row;
            return changed;
        }

        void CopyValues()
        {
            string json = JsonUtility.ToJson(tour.BuildAdjustment());
            GUIUtility.systemCopyBuffer = json;
            Debug.Log($"{Tag} AJUSTE_PASEO {json}");
            try
            {
                string path = Path.Combine(Application.persistentDataPath, $"paseo-ajuste-campo-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.WriteAllText(path, json);
                Debug.Log($"{Tag} Ajuste guardado en {path}. Pegarlo en Assets/AncoRA/Paseo/ajuste-campo.json y correr ApplyFieldAdjustment.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} No se pudo guardar el ajuste en disco: {e.Message}");
            }
            Show("Copiado al portapapeles y al log");
        }

        void Show(string message)
        {
            toast = message;
            toastUntil = Time.realtimeSinceStartup + 4f;
        }
    }
}
```

- [ ] **Step 6: Compilar**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode compile`
Expected: código 0 y sin `ERRORES DE COMPILACIÓN`.

- [ ] **Step 7: Correr todas las pruebas (nada se rompió)**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests`
Expected: `total 43, pasaron 43, fallaron 0`.

- [ ] **Step 8: Commit**

```bash
git add Assets/AncoRA/Scripts/Paseo/PaseoLabel.cs Assets/AncoRA/Scripts/Paseo/PaseoLabel.cs.meta Assets/AncoRA/Scripts/Paseo/PaseoMapContent.cs Assets/AncoRA/Scripts/Paseo/PaseoMapContent.cs.meta Assets/AncoRA/Scripts/Paseo/PaseoTour.cs Assets/AncoRA/Scripts/Paseo/PaseoTour.cs.meta Assets/AncoRA/Scripts/Paseo/PaseoHud.cs Assets/AncoRA/Scripts/Paseo/PaseoHud.cs.meta Assets/AncoRA/Scripts/Paseo/PaseoFieldAdjust.cs Assets/AncoRA/Scripts/Paseo/PaseoFieldAdjust.cs.meta
git commit -m "Agregar los componentes de escena del paseo

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `PaseoSetup` (CheckInputs, Prepare, Validate, carga nativa) y prueba de humo

**Files:**
- Create: `Assets/AncoRA/Editor/Paseo/PaseoSetup.cs`
- Create: `Assets/AncoRA/Editor/Paseo/PaseoSmokeTest.cs`

**Interfaces:**
- Consumes: todo lo de las Tasks 2–7.
- Produces:
  - `internal sealed class PaseoPaths { string DataRoot = "Assets/AncoRA/Paseo"; string Scene = "Assets/Scenes/PaseoIngenieria.unity"; bool RejectSampleIds = true; string MaterialsFolder; string AdjustmentFile; }`
  - `public static class PaseoSetup { CheckInputs(); Prepare(); Validate(); CheckNativeMapsInEditor(); internal LoadTour(PaseoPaths, out List<string> errors, out List<string> warnings); internal CheckInputsCore/PrepareCore/ValidateCore/CheckNativeMapsCore(PaseoPaths); }` — menús `AncoRA/Paseo/…`.
  - `public static class PaseoSmokeTest { Run(); }`

- [ ] **Step 1: Escribir la prueba de humo (falla porque `PaseoSetup` no existe)**

```csharp
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using AncorRA.AR;
using Immersal.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AncorRA.Editor
{
    /// <summary>
    /// End-to-end check of the Editor automation with the SDK sample maps (only the .bytes, like the real campus maps):
    /// two buildings, one of them with two maps. Works in a temporary folder and scene that it deletes, and restores the
    /// Build Settings scene list. Proves the scene wiring, not localization.
    /// </summary>
    public static class PaseoSmokeTest
    {
        const string Root = "Assets/AncoRA/_PruebaHumoPaseo";
        const string Tag = "[AncoRA Paseo][PRUEBA DE HUMO]";

        [MenuItem("AncoRA/Paseo/Prueba de humo con mapas de ejemplo del SDK")]
        public static void Run()
        {
            var paths = new PaseoPaths { DataRoot = Root, Scene = Root + "/PruebaHumoPaseo.unity", RejectSampleIds = false };
            var originalScenes = EditorBuildSettings.scenes;
            try
            {
                CreateSampleTour();
                PaseoSetup.CheckInputsCore(paths);
                PaseoSetup.PrepareCore(paths);
                PaseoSetup.ValidateCore(paths);
                PaseoSetup.CheckNativeMapsCore(paths);
                AssertOneSpacePerMap();
                Debug.Log($"{Tag} OK: Prepare, Validate y carga nativa con 3 mapas en 3 XR Spaces (2 edificios). No prueba localización.");
            }
            finally
            {
                EditorBuildSettings.scenes = originalScenes;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(Root);
                if (Directory.Exists(Root))
                    Directory.Delete(Root, true);
                AssetDatabase.Refresh();
            }
        }

        static void CreateSampleTour()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.immersal.core")
                          ?? throw new InvalidOperationException("Falta Immersal Core.");
            string source = Path.Combine(package.resolvedPath, "Samples~", "Core", "Map Data");
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
            WriteBuilding("EdificioA", "Edificio A", source, "90687-SampleMapA.bytes");
            WriteBuilding("EdificioB", "Edificio B", source, "90689-SampleMapB.bytes", "90690-SampleMapC.bytes");
            AssetDatabase.Refresh();
        }

        static void WriteBuilding(string id, string name, string source, params string[] files)
        {
            string folder = Path.Combine(Root, id);
            Directory.CreateDirectory(folder);
            var config = new PaseoBuildingConfig { nombre = name };
            foreach (string file in files)
            {
                File.Copy(Path.Combine(source, file), Path.Combine(folder, file));
                config.mapas.Add(new PaseoMapConfig { id = PaseoConfig.IdFromFileName(file).Value, archivo = file });
            }
            File.WriteAllText(Path.Combine(folder, PaseoConfig.FileName), PaseoConfig.Serialize(config));
        }

        // The core claim of the design: ImmersalSDK registers each map with its nearest parent ISceneUpdateable, so each
        // map must find its own, distinct XR Space.
        static void AssertOneSpacePerMap()
        {
            var maps = Object.FindObjectsByType<XRMap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (maps.Length != 3)
                throw new InvalidOperationException($"Se esperaban 3 XR Map y hay {maps.Length}.");
            var parents = maps.Select(m => m.transform.GetComponentInParent<ISceneUpdateable>(true)).ToArray();
            if (parents.Any(p => p == null) || parents.Distinct().Count() != 3)
                throw new InvalidOperationException("Cada XR Map debe registrarse con su propio XR Space.");
            var contents = Object.FindObjectsByType<PaseoMapContent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (contents.Count(c => c.BuildingId == "EdificioB") != 2 || contents.Count(c => c.BuildingId == "EdificioA") != 1)
                throw new InvalidOperationException("EdificioB debe tener dos cajas (una por mapa) y EdificioA una.");
            foreach (var content in contents)
                if (content.GetComponentInParent<XRSpace>().GetComponentInChildren<XRMap>(true).mapId != content.MapId)
                    throw new InvalidOperationException($"La caja del mapa {content.MapId} no está en el XR Space de su mapa.");
        }
    }
}
#endif
```

- [ ] **Step 2: Compilar y ver que falla**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode compile`
Expected: `CS0246` por `PaseoPaths` / `PaseoSetup`.

- [ ] **Step 3: Implementar `PaseoSetup` (sin ajuste de campo ni build; llegan en la Task 9)**

```csharp
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AncorRA.AR;
using Immersal;
using Immersal.XR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
using Object = UnityEngine.Object;

namespace AncorRA.Editor
{
    internal sealed class PaseoPaths
    {
        public string DataRoot = "Assets/AncoRA/Paseo";
        public string Scene = "Assets/Scenes/PaseoIngenieria.unity";
        // Off only for the smoke test, which uses the SDK sample maps.
        public bool RejectSampleIds = true;
        public string MaterialsFolder => DataRoot + "/_Materiales";
        public string AdjustmentFile => DataRoot + "/ajuste-campo.json";
    }

    /// <summary>
    /// Editor automation of the tour. Builds the scene from Assets/AncoRA/Paseo/&lt;Edificio&gt;/edificio.json: one XR
    /// Space per map, each with its XR Map at identity and the building box. edificio.json is the source of truth: Prepare
    /// rebuilds the scene from it every time.
    /// </summary>
    public static class PaseoSetup
    {
        const string Tag = "[AncoRA Paseo]";
        const string SampleScenePath = "Samples~/Core/Scenes/SimpleSample.unity";
        static readonly PaseoPaths Real = new();

        // ---------------------------------------------------------------- inputs

        [MenuItem("AncoRA/Paseo/Comprobar datos de entrada")]
        public static void CheckInputs() => CheckInputsCore(Real);

        internal static void CheckInputsCore(PaseoPaths paths)
        {
            var tour = LoadTour(paths, out var errors, out var warnings);
            foreach (var pair in tour)
                Debug.Log($"{Tag} {pair.Key} «{pair.Value.nombre}»: {(pair.Value.mapas.Count == 0 ? "sin mapas" : string.Join(", ", pair.Value.mapas.Select(m => $"{m.id} ({(m.caja.colocada ? "caja colocada" : "caja sin colocar")})")))}");
            foreach (string warning in warnings)
                Debug.LogWarning($"{Tag} AVISO: {warning}");
            if (errors.Count > 0)
                throw new InvalidOperationException("Datos del paseo con errores:\n" + string.Join("\n", errors));
            Debug.Log($"{Tag} Datos de entrada OK.");
        }

        internal static SortedDictionary<string, PaseoBuildingConfig> LoadTour(PaseoPaths paths, out List<string> errors, out List<string> warnings)
        {
            errors = new List<string>();
            warnings = new List<string>();
            var tour = new SortedDictionary<string, PaseoBuildingConfig>(StringComparer.Ordinal);
            if (!Directory.Exists(paths.DataRoot))
            {
                errors.Add($"No existe {paths.DataRoot}.");
                return tour;
            }
            foreach (string folder in Directory.GetDirectories(paths.DataRoot).OrderBy(d => d, StringComparer.Ordinal))
            {
                string id = Path.GetFileName(folder);
                if (id.StartsWith("_"))
                    continue;
                string file = Path.Combine(folder, PaseoConfig.FileName);
                if (!File.Exists(file))
                {
                    errors.Add($"Edificio {id}: falta {PaseoConfig.FileName}.");
                    continue;
                }
                PaseoBuildingConfig config;
                try
                {
                    config = PaseoConfig.Parse(File.ReadAllText(file));
                }
                catch (FormatException e)
                {
                    errors.Add($"Edificio {id}: {e.Message}");
                    continue;
                }
                var files = Directory.GetFiles(folder).Select(Path.GetFileName).ToList();
                var validation = PaseoConfig.Validate(id, config, files, paths.RejectSampleIds);
                errors.AddRange(validation.Errors);
                warnings.AddRange(validation.Warnings);
                tour[id] = config;
            }
            errors.AddRange(PaseoConfig.ValidateTour(tour));
            if (errors.Count == 0 && tour.Values.All(c => c.mapas.Count == 0))
                errors.Add("Ningún edificio tiene mapas.");
            return tour;
        }

        static string BytesPath(PaseoPaths paths, string buildingId, PaseoMapConfig map) => $"{paths.DataRoot}/{buildingId}/{map.archivo}";

        // ---------------------------------------------------------------- prepare

        [MenuItem("AncoRA/Paseo/Preparar escena del paseo")]
        public static void Prepare() => PrepareCore(Real);

        internal static void PrepareCore(PaseoPaths paths)
        {
            var tour = LoadTour(paths, out var errors, out _);
            if (errors.Count > 0)
                throw new InvalidOperationException("Datos del paseo con errores:\n" + string.Join("\n", errors));
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.immersal.core");
            if (package == null || package.version != "2.4.0")
                throw new InvalidOperationException("Se necesita Immersal Core 2.4.0 para copiar SimpleSample.");

            EnsureAndroidSettings();
            AssetDatabase.Refresh();
            foreach (var pair in tour)
                foreach (var map in pair.Value.mapas)
                    if (AssetDatabase.LoadAssetAtPath<TextAsset>(BytesPath(paths, pair.Key, map)) == null)
                        throw new InvalidOperationException($"Unity no importó {BytesPath(paths, pair.Key, map)} como TextAsset.");

            // Same base as the earlier pilots: the official SimpleSample copied into a new scene.
            Directory.CreateDirectory(Path.GetDirectoryName(paths.Scene));
            if (File.Exists(paths.Scene))
                AssetDatabase.DeleteAsset(paths.Scene);
            File.Copy(Path.Combine(package.resolvedPath, SampleScenePath), paths.Scene);
            AssetDatabase.ImportAsset(paths.Scene);
            var scene = EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
            Lightmapping.lightingSettings = null;

            foreach (string unwanted in new[] { "Canvas", "EventSystem", "Directional Light" })
            {
                var go = GameObject.Find(unwanted);
                if (go != null)
                    Object.DestroyImmediate(go);
            }

            var sdk = Object.FindAnyObjectByType<ImmersalSDK>() ?? throw new InvalidOperationException("SimpleSample no contiene ImmersalSDK.");
            PrefabUtility.UnpackPrefabInstance(sdk.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            sdk.developerToken = "";
            var sdkFields = new SerializedObject(sdk);
            // ImmersalLogger.LoggingLevel.Verbose (1): session start, registered maps and each localization reach logcat.
            sdkFields.FindProperty("m_LoggingLevel").intValue = 1;
            sdkFields.ApplyModifiedPropertiesWithoutUndo();
            var localizer = sdk.GetComponentInChildren<Localizer>(true);
            var device = sdk.GetComponentInChildren<DeviceLocalization>(true);
            var server = sdk.GetComponentInChildren<ServerLocalization>(true);
            if (server != null)
                Object.DestroyImmediate(server.gameObject);
            var methods = new SerializedObject(localizer).FindProperty("m_LocalizationMethodObjects");
            methods.arraySize = 1;
            methods.GetArrayElementAtIndex(0).objectReferenceValue = device;
            methods.serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var origin = Object.FindAnyObjectByType<XROrigin>();
            origin.CameraYOffset = 0f;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;

            var templateSpace = Object.FindAnyObjectByType<XRSpace>() ?? throw new InvalidOperationException("SimpleSample no contiene XR Space.");
            if (PrefabUtility.IsPartOfPrefabInstance(templateSpace))
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(templateSpace), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // Apply each pose directly; the sample smoother starts at the world origin.
            templateSpace.ProcessPoses = false;
            var spaceFields = new SerializedObject(templateSpace);
            spaceFields.FindProperty("m_DataProcessors").arraySize = 0;
            spaceFields.ApplyModifiedPropertiesWithoutUndo();
            foreach (string name in new[] { "PoseFilter", "PoseSmoother" })
            {
                var child = templateSpace.transform.Find(name);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }
            var templateMap = templateSpace.GetComponentInChildren<XRMap>(true) ?? Object.FindAnyObjectByType<XRMap>()
                              ?? throw new InvalidOperationException("SimpleSample no contiene un XR Map.");
            if (templateMap.Visualization != null)
                templateMap.RemoveVisualization();
            templateMap.transform.SetParent(templateSpace.transform, false);

            var border = CreateMaterial(paths.MaterialsFolder + "/PaseoCajaBorde.mat", new Color(1f, 0.9f, 0.1f, 1f), transparent: false);
            var fill = CreateMaterial(paths.MaterialsFolder + "/PaseoCajaRelleno.mat", new Color(1f, 0.45f, 0.05f, 0.28f), transparent: true);
            var solid = CreateMaterial(paths.MaterialsFolder + "/PaseoCajaSolida.mat", new Color(0.62f, 0.25f, 0.03f, 1f), transparent: false);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("Paseo");
            var contents = new List<PaseoMapContent>();
            foreach (var pair in tour)
            {
                if (pair.Value.mapas.Count == 0)
                    continue;
                var buildingObject = new GameObject(pair.Key);
                buildingObject.transform.SetParent(root.transform, false);
                foreach (var mapConfig in pair.Value.mapas)
                {
                    var spaceObject = Object.Instantiate(templateSpace.gameObject, buildingObject.transform);
                    spaceObject.name = $"XR Space {mapConfig.id}";
                    var map = spaceObject.GetComponentInChildren<XRMap>(true);
                    ConfigureMap(map, AssetDatabase.LoadAssetAtPath<TextAsset>(BytesPath(paths, pair.Key, mapConfig)), mapConfig.id, device);
                    contents.Add(CreateContent(spaceObject.transform, pair.Key, pair.Value, mapConfig, border, fill, solid, font));
                }
            }
            Object.DestroyImmediate(templateSpace.gameObject);

            var tourComponent = root.AddComponent<PaseoTour>();
            tourComponent.Configure(sdk, localizer, contents.ToArray());
            var hud = root.AddComponent<PaseoHud>();
            hud.Configure(tourComponent);
            root.AddComponent<PaseoFieldAdjust>().Configure(tourComponent, hud);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(paths.Scene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Escena creada: {paths.Scene} con {contents.Count} mapa(s) en {contents.Select(c => c.BuildingId).Distinct().Count()} edificio(s), un XR Space por mapa. Nada de esto está medido en terreno.");
        }

        static void ConfigureMap(XRMap map, TextAsset bytes, int expectedId, DeviceLocalization device)
        {
            // Configure takes id and name from the file name; without -metadata.json the SDK warns and keeps identity alignment.
            map.Configure(bytes);
            map.gameObject.name = $"XR Map {map.mapId}-{map.mapName}";
            map.LocalizationMethod = device;
            map.MapOptions = new List<IMapOption>
            {
                new MapLoadingOption { m_SerializedDataSource = (int)MapDataSource.Embed, DownloadVisualizationAtRuntime = false }
            };
            map.SerializeMapOptions();
            // The map frame is the space frame: the box poses in edificio.json rely on it.
            map.transform.localPosition = Vector3.zero;
            map.transform.localRotation = Quaternion.identity;
            map.transform.localScale = Vector3.one;
            if (map.mapId != expectedId)
                throw new InvalidOperationException($"El XR Map quedó con ID {map.mapId} en vez de {expectedId}.");
        }

        static PaseoMapContent CreateContent(Transform space, string buildingId, PaseoBuildingConfig building, PaseoMapConfig mapConfig,
            Material border, Material fill, Material solid, Font font)
        {
            var boxObject = new GameObject("Caja");
            boxObject.transform.SetParent(space, false);
            var box = boxObject.AddComponent<EdificioFacadeFrame>();
            box.Configure(building.tamano[0], building.tamano[1], building.tamano[2], border, fill, solid, building.solido);

            var labelObject = new GameObject("Nombre");
            labelObject.transform.SetParent(boxObject.transform, false);
            var text = labelObject.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 96;
            text.characterSize = 0.12f;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
            labelObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            var label = labelObject.AddComponent<PaseoLabel>();

            var content = boxObject.AddComponent<PaseoMapContent>();
            content.Configure(mapConfig.id, buildingId, building.nombre, box, label);
            var p = mapConfig.caja.posicion;
            content.SetPose(new Vector3(p[0], p[1], p[2]), mapConfig.caja.giro, mapConfig.caja.colocada);
            return content;
        }

        static Material CreateMaterial(string path, Color color, bool transparent)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? throw new InvalidOperationException("Falta el shader URP/Unlit.");
            var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Cull", 0f); // visible from both sides
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            // CreateAsset replaces an existing asset at the same path.
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void EnsureAndroidSettings()
        {
            PlayerSettings.allowUnsafeCode = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        }

        // ---------------------------------------------------------------- validate

        [MenuItem("AncoRA/Paseo/Validar escena del paseo")]
        public static void Validate() => ValidateCore(Real);

        internal static void ValidateCore(PaseoPaths paths)
        {
            var tour = LoadTour(paths, out var errors, out var warnings);
            ValidateProjectSettings(errors);
            if (!File.Exists(paths.Scene))
                errors.Add($"Falta la escena {paths.Scene}; correr Prepare.");
            else
                ValidateScene(paths, tour, errors, warnings);
            var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (enabled.Length != 1 || enabled[0] != paths.Scene)
                errors.Add($"Build Settings debe tener solo {paths.Scene} habilitada.");

            foreach (string warning in warnings)
                Debug.LogWarning($"{Tag} AVISO: {warning}");
            if (errors.Count > 0)
                throw new InvalidOperationException("Escena del paseo inválida:\n" + string.Join("\n", errors));
            Debug.Log($"{Tag} Validación de datos, escena y proyecto: OK. No verifica localización ni alineación física.");
        }

        static void ValidateProjectSettings(List<string> errors)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.immersal.core");
            if (package == null || package.version != "2.4.0")
                errors.Add($"Immersal Core debe ser 2.4.0 (actual: {package?.version ?? "ausente"}).");
            if (!PlayerSettings.allowUnsafeCode ||
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP ||
                PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                errors.Add("Android debe usar IL2CPP, ARM64 y código unsafe.");

            var xrAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/XR/XRGeneralSettings.asset");
            if (xrAssets.OfType<XRGeneralSettings>().Count() != 4)
                errors.Add("XRGeneralSettings debe tener exactamente cuatro entradas (sin duplicados de OneDrive).");
            var android = xrAssets.OfType<XRManagerSettings>().Where(m => m.name == "Android Providers").ToArray();
            if (android.Length != 1 || android[0].activeLoaders.Count != 1 || android[0].activeLoaders[0].GetType().Name != "ARCoreLoader")
                errors.Add("XR: Android debe tener solo ARCoreLoader (sin OpenXR).");
        }

        static void ValidateScene(PaseoPaths paths, SortedDictionary<string, PaseoBuildingConfig> tour, List<string> errors, List<string> warnings)
        {
            EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
            var all = FindObjectsInactive.Include;
            var none = FindObjectsSortMode.None;
            if (Object.FindObjectsByType<ARSession>(all, none).Length != 1 ||
                Object.FindObjectsByType<XROrigin>(all, none).Length != 1 ||
                Object.FindObjectsByType<ARCameraManager>(all, none).Length != 1 ||
                Object.FindObjectsByType<Camera>(all, none).Length != 1)
                errors.Add("Debe haber una sola ARSession, XROrigin y cámara AR.");
            if (Object.FindObjectsByType<ServerLocalization>(all, none).Length != 0 ||
                Object.FindObjectsByType<DeviceLocalization>(all, none).Length != 1)
                errors.Add("Debe haber un único DeviceLocalization y ningún ServerLocalization.");

            var sdks = Object.FindObjectsByType<ImmersalSDK>(all, none);
            if (sdks.Length != 1 || !string.IsNullOrEmpty(sdks[0].developerToken) ||
                sdks[0].Localizer.AvailableLocalizationMethods.Length != 1 ||
                sdks[0].Localizer.AvailableLocalizationMethods[0] is not DeviceLocalization)
                errors.Add("SDK: debe haber uno, sin token serializado y con DeviceLocalization como único método.");

            var maps = Object.FindObjectsByType<XRMap>(all, none);
            var spaces = Object.FindObjectsByType<XRSpace>(all, none);
            var contents = Object.FindObjectsByType<PaseoMapContent>(all, none);
            var expected = tour.SelectMany(pair => pair.Value.mapas.Select(m => (building: pair.Key, config: pair.Value, map: m))).ToList();
            if (maps.Length != expected.Count || spaces.Length != expected.Count || contents.Length != expected.Count)
                errors.Add($"Se esperaban {expected.Count} mapas, XR Spaces y cajas; hay {maps.Length}, {spaces.Length} y {contents.Length}. Correr Prepare.");

            foreach (var (building, config, mapConfig) in expected)
            {
                string label = $"{building}, mapa {mapConfig.id}";
                var map = maps.FirstOrDefault(m => m.mapId == mapConfig.id);
                if (map == null)
                {
                    errors.Add($"{label}: no hay XR Map con ese ID.");
                    continue;
                }
                var space = map.GetComponentInParent<XRSpace>();
                var options = map.MapOptions.OfType<MapLoadingOption>().SingleOrDefault();
                if (space == null || space.GetComponentsInChildren<XRMap>(true).Length != 1)
                    errors.Add($"{label}: el XR Map debe estar solo dentro de su propio XR Space.");
                else if (space.ProcessPoses)
                    errors.Add($"{label}: su XR Space debe aplicar la pose directamente (ProcessPoses apagado).");
                if (map.mapFile == null || AssetDatabase.GetAssetPath(map.mapFile) != BytesPath(paths, building, mapConfig) ||
                    map.LocalizationMethod is not DeviceLocalization || options == null ||
                    options.m_SerializedDataSource != (int)MapDataSource.Embed || options.DownloadVisualizationAtRuntime)
                    errors.Add($"{label}: debe usar su .bytes embebido y DeviceLocalization.");
                if (map.transform.localPosition != Vector3.zero || map.transform.localRotation != Quaternion.identity ||
                    map.transform.localScale != Vector3.one)
                    errors.Add($"{label}: el XR Map debe quedar en identidad dentro de su XR Space.");

                var content = contents.FirstOrDefault(c => c.MapId == mapConfig.id);
                if (content == null || space == null || content.GetComponentInParent<XRSpace>() != space)
                {
                    errors.Add($"{label}: falta su caja dentro de su XR Space.");
                    continue;
                }
                if (content.BuildingId != building || content.BuildingName != config.nombre)
                    errors.Add($"{label}: la caja tiene edificio/nombre distintos a edificio.json.");
                var box = content.Box;
                var renderer = box != null ? box.GetComponent<MeshRenderer>() : null;
                if (box == null || !box.IsBox || !box.HasSolidMaterial || renderer == null || renderer.sharedMaterials.Length != 2 ||
                    renderer.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name != "Universal Render Pipeline/Unlit"))
                    errors.Add($"{label}: la caja necesita sus tres materiales URP/Unlit.");
                var text = content.Label != null ? content.Label.GetComponent<TextMesh>() : null;
                if (text == null || text.font == null || content.Label.GetComponent<MeshRenderer>().sharedMaterial == null)
                    errors.Add($"{label}: falta el letrero con fuente y material.");
                if (!mapConfig.caja.colocada)
                    warnings.Add($"{label}: caja sin colocar; aparecerá delante de la cámara hasta ajustarla en terreno.");
            }

            var tours = Object.FindObjectsByType<PaseoTour>(all, none);
            if (tours.Length != 1 || tours[0].Sdk == null || tours[0].Localizer == null ||
                tours[0].Contents.Count != contents.Length || tours[0].Contents.Any(c => c == null))
                errors.Add("Debe haber un único PaseoTour con SDK, Localizer y todas las cajas asignadas.");
            var huds = Object.FindObjectsByType<PaseoHud>(all, none);
            var adjusters = Object.FindObjectsByType<PaseoFieldAdjust>(all, none);
            if (huds.Length != 1 || tours.Length != 1 || huds[0].Tour != tours[0])
                errors.Add("Debe haber un único PaseoHud conectado al PaseoTour.");
            if (adjusters.Length != 1 || huds.Length != 1 || adjusters[0].Tour == null || adjusters[0].Hud != huds[0])
                errors.Add("Debe haber un único panel de ajuste conectado al PaseoTour y al PaseoHud.");
        }

        [MenuItem("AncoRA/Paseo/Comprobar carga nativa de los mapas")]
        public static void CheckNativeMapsInEditor() => CheckNativeMapsCore(Real);

        internal static void CheckNativeMapsCore(PaseoPaths paths)
        {
            ValidateCore(paths);
            foreach (var map in Object.FindObjectsByType<XRMap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                int handle = Core.LoadMap(map.mapId, map.mapFile.bytes);
                try
                {
                    int points = Core.GetPointCloudSize(map.mapId);
                    if (handle < 0 || points <= 0)
                        throw new InvalidOperationException($"El plugin no cargó el mapa {map.mapId}: handle={handle}, puntos={points}.");
                    Debug.Log($"{Tag} Plugin del Editor cargó el mapa {map.mapId}: {points} puntos. Android pendiente de dispositivo.");
                }
                finally
                {
                    Core.FreeMap(map.mapId);
                }
            }
        }
    }
}
#endif
```

- [ ] **Step 4: Correr la prueba de humo**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSmokeTest.Run`
Expected: código 0, una línea `[AncoRA Paseo][PRUEBA DE HUMO] OK: ...`, tres líneas `Plugin del Editor cargó el mapa ...`, avisos de «caja sin colocar» (normales) y ninguna `Exception`. Además, `git status` no debe mostrar `Assets/AncoRA/_PruebaHumoPaseo` ni cambios en `ProjectSettings/EditorBuildSettings.asset`.

Si falla por la estructura de `SimpleSample` (por ejemplo, el XR Map no está bajo el XR Space), corregir `PrepareCore`, no la prueba.

- [ ] **Step 5: Correr todas las pruebas**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests`
Expected: `total 43, pasaron 43, fallaron 0`.

- [ ] **Step 6: Commit**

```bash
git add Assets/AncoRA/Editor/Paseo Assets/AncoRA/Editor/Paseo.meta
git commit -m "Agregar la preparacion y validacion de la escena del paseo con prueba de humo

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Ajuste de campo hacia la escena y build de Android

**Files:**
- Modify: `Assets/AncoRA/Editor/Paseo/PaseoSetup.cs` (agregar la sección `field adjustment` y `build` al final de la clase)
- Modify: `Assets/AncoRA/Editor/Paseo/PaseoSmokeTest.cs` (agregar la comprobación del ajuste)

**Interfaces:**
- Consumes: `PaseoAdjustment.ApplyTo` (Task 3), `LoadTour`, `PrepareCore`, `ValidateCore` (Task 8).
- Produces: `PaseoSetup.ApplyFieldAdjustment()`, `internal ApplyFieldAdjustmentCore(PaseoPaths)`, `PaseoSetup.BuildAndroid()`.

- [ ] **Step 1: Extender la prueba de humo (falla porque `ApplyFieldAdjustmentCore` no existe)**

En `PaseoSmokeTest.Run`, después de `AssertOneSpacePerMap();` agregar `ApplyAdjustmentAndCheck(paths);`, y agregar a la clase:

```csharp
        static void ApplyAdjustmentAndCheck(PaseoPaths paths)
        {
            var adjustment = new PaseoAdjustment { generado = "prueba" };
            adjustment.edificios.Add(new PaseoAdjustmentBuilding
            {
                id = "EdificioB", tamano = new[] { 30f, 10f, 15f }, solido = true,
                mapas =
                {
                    new PaseoAdjustmentMap { id = 90689, posicion = new[] { 1f, 2f, 3f }, giro = 45f, colocada = true },
                    new PaseoAdjustmentMap { id = 90690, posicion = new[] { 9f, 9f, 9f }, giro = 90f, colocada = false }
                }
            });
            File.WriteAllText(paths.AdjustmentFile, JsonUtility.ToJson(adjustment));
            AssetDatabase.Refresh();

            PaseoSetup.ApplyFieldAdjustmentCore(paths);

            var contents = Object.FindObjectsByType<PaseoMapContent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var placed = contents.Single(c => c.MapId == 90689);
            var untouched = contents.Single(c => c.MapId == 90690);
            if (Vector3.Distance(placed.LocalPosition, new Vector3(1f, 2f, 3f)) > 1e-3f || Mathf.Abs(Mathf.DeltaAngle(placed.LocalYaw, 45f)) > 1e-2f || !placed.Placed)
                throw new InvalidOperationException($"La caja 90689 no quedó en (1,2,3) / 45° colocada: {placed.LocalPosition} / {placed.LocalYaw}.");
            if (untouched.Placed || Vector3.Distance(untouched.LocalPosition, new Vector3(9f, 9f, 9f)) < 1e-3f)
                throw new InvalidOperationException("La caja 90690 no se ajustó en terreno y no debía moverse.");
            if (Mathf.Abs(untouched.SizeMeters.x - 30f) > 1e-3f || !untouched.Box.Solid)
                throw new InvalidOperationException("El tamaño y el relleno son del edificio: también debían llegar a la caja 90690.");
            var config = PaseoConfig.Parse(File.ReadAllText(Path.Combine(paths.DataRoot, "EdificioB", PaseoConfig.FileName)));
            if (!config.mapas.Single(m => m.id == 90689).caja.colocada)
                throw new InvalidOperationException("edificio.json de EdificioB no guardó la caja colocada.");
            Debug.Log($"{Tag} Ajuste de campo aplicado a edificio.json y a la escena.");
        }
```

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode compile`
Expected: `CS0117` (`PaseoSetup` no contiene `ApplyFieldAdjustmentCore`).

- [ ] **Step 2: Implementar ajuste y build en `PaseoSetup`**

Agregar al inicio del archivo `using UnityEditor.Build.Reporting;` y estas constantes dentro de la clase, junto a `Tag`:

```csharp
        const string ApplicationId = "com.ancora.ucnar.paseo";
        const string ProductName = "AncoRA Paseo";
        const string AndroidApk = "Builds/Android/AncoRAPaseo.apk";
```

Y al final de la clase:

```csharp
        // ---------------------------------------------------------------- field adjustment

        [MenuItem("AncoRA/Paseo/Aplicar ajuste de campo desde ajuste-campo.json")]
        public static void ApplyFieldAdjustment() => ApplyFieldAdjustmentCore(Real);

        internal static void ApplyFieldAdjustmentCore(PaseoPaths paths)
        {
            if (!File.Exists(paths.AdjustmentFile))
                throw new FileNotFoundException($"Pegar el JSON de «Copiar valores» en {paths.AdjustmentFile}.", paths.AdjustmentFile);
            PaseoAdjustment adjustment;
            try
            {
                adjustment = JsonUtility.FromJson<PaseoAdjustment>(File.ReadAllText(paths.AdjustmentFile));
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException($"{paths.AdjustmentFile} no es JSON válido: {e.Message}");
            }
            if (adjustment?.edificios == null || adjustment.edificios.Count == 0)
                throw new InvalidOperationException($"{paths.AdjustmentFile} no trae edificios.");

            var tour = LoadTour(paths, out var errors, out _);
            if (errors.Count > 0)
                throw new InvalidOperationException("Datos del paseo con errores:\n" + string.Join("\n", errors));
            var problems = adjustment.ApplyTo(tour);
            if (problems.Count > 0)
                throw new InvalidOperationException("El ajuste no se aplicó (no se cambió nada):\n" + string.Join("\n", problems));

            foreach (var building in adjustment.edificios)
                File.WriteAllText(Path.Combine(paths.DataRoot, building.id, PaseoConfig.FileName), PaseoConfig.Serialize(tour[building.id]));
            AssetDatabase.Refresh();
            Debug.Log($"{Tag} Ajuste de campo del {adjustment.generado} (build {adjustment.build}) escrito en edificio.json; se rearma la escena.");
            PrepareCore(paths);
        }

        // ---------------------------------------------------------------- build

        [MenuItem("AncoRA/Paseo/Compilar APK de Android")]
        public static void BuildAndroid()
        {
            // Validate first: with broken data or scene this stops here and no APK is produced.
            ValidateCore(Real);
            string originalId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string originalName = PlayerSettings.productName;
            try
            {
                EnsureAndroidSettings();
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
                PlayerSettings.productName = ProductName;
                EditorUserBuildSettings.buildAppBundle = false;
                Directory.CreateDirectory(Path.GetDirectoryName(AndroidApk));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Real.Scene },
                    locationPathName = AndroidApk,
                    target = BuildTarget.Android,
                    // Development: Debug.Log reaches logcat (release builds did not show it).
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Build Android: {report.summary.result} ({report.summary.totalErrors} errores).");
                long bytes = new FileInfo(AndroidApk).Length;
                Debug.Log($"{Tag} APK listo: {AndroidApk} ({bytes / 1048576f:F1} MB en disco). Un build correcto NO demuestra precisión en terreno.");
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, originalId);
                PlayerSettings.productName = originalName;
                AssetDatabase.SaveAssets();
            }
        }
```

- [ ] **Step 3: Correr la prueba de humo**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSmokeTest.Run`
Expected: código 0, `Ajuste de campo aplicado a edificio.json y a la escena.` y `PRUEBA DE HUMO] OK`. Sin cambios residuales en `git status`.

- [ ] **Step 4: Commit**

```bash
git add Assets/AncoRA/Editor/Paseo/PaseoSetup.cs Assets/AncoRA/Editor/Paseo/PaseoSmokeTest.cs
git commit -m "Agregar el ajuste de campo hacia la escena y el build de Android del paseo

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Retirar los scripts de los pilotos que el paseo reemplaza

**Files:**
- Delete: `Assets/AncoRA/Scripts/ImmersalEdificioPilotDiagnostics.cs` (+ `.meta`)
- Delete: `Assets/AncoRA/Scripts/ImmersalEdificioFieldAdjust.cs` (+ `.meta`)
- Delete: `Assets/AncoRA/Scripts/TeologiaMapSelector.cs` (+ `.meta`)
- Delete: `Assets/AncoRA/Scripts/TeologiaBoxStore.cs` (+ `.meta`)
- Delete: `Assets/AncoRA/Scripts/EdificioStatusText.cs` (+ `.meta`)
- Delete: `Assets/AncoRA/Scripts/ImmersalMapAlignment.cs` (+ `.meta`)

Se conservan `EdificioFacadeFrame`, `GuiSafeArea` y `PipelineMaterials`.

- [ ] **Step 1: Confirmar que nada del paseo los usa**

Run (Bash): `grep -rlwE "ImmersalEdificioPilotDiagnostics|ImmersalEdificioFieldAdjust|EdificioFieldAdjustmentData|TeologiaMapSelector|TeologiaMode|TeologiaBoxStore|EdificioStatusText|EdificioStatusLevel|ImmersalMapAlignment" Assets --include=*.cs --include=*.unity`
Expected: solo los seis archivos que se van a borrar (se referencian entre ellos). Si aparece otro archivo, detenerse y revisar.

- [ ] **Step 2: Borrar**

```bash
git rm -q Assets/AncoRA/Scripts/ImmersalEdificioPilotDiagnostics.cs Assets/AncoRA/Scripts/ImmersalEdificioPilotDiagnostics.cs.meta Assets/AncoRA/Scripts/ImmersalEdificioFieldAdjust.cs Assets/AncoRA/Scripts/ImmersalEdificioFieldAdjust.cs.meta Assets/AncoRA/Scripts/TeologiaMapSelector.cs Assets/AncoRA/Scripts/TeologiaMapSelector.cs.meta Assets/AncoRA/Scripts/TeologiaBoxStore.cs Assets/AncoRA/Scripts/TeologiaBoxStore.cs.meta Assets/AncoRA/Scripts/EdificioStatusText.cs Assets/AncoRA/Scripts/EdificioStatusText.cs.meta Assets/AncoRA/Scripts/ImmersalMapAlignment.cs Assets/AncoRA/Scripts/ImmersalMapAlignment.cs.meta
```

- [ ] **Step 3: Compilar, pruebas y prueba de humo**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests`
Expected: `total 43, pasaron 43, fallaron 0`.
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSmokeTest.Run`
Expected: código 0 y `PRUEBA DE HUMO] OK`.

- [ ] **Step 4: Commit**

```bash
git commit -m "Retirar los scripts de los pilotos reemplazados por el paseo

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Datos reales, escena del paseo, APK y documentación

**Files:**
- Create: `Assets/AncoRA/Paseo/CienciasBasicas/152192-csbasicasgael.bytes`, `152196-csbasicasgael2.bytes`, `edificio.json`
- Create: `Assets/AncoRA/Paseo/X1/152198-x1gael.bytes`, `edificio.json`
- Create: `Assets/AncoRA/Paseo/EIC/152199-eicgael.bytes`, `edificio.json`
- Create: `Assets/AncoRA/Paseo/Teologia/edificio.json`
- Create (generados): `Assets/Scenes/PaseoIngenieria.unity`, `Assets/AncoRA/Paseo/_Materiales/*.mat`
- Modify: `ProjectSettings/EditorBuildSettings.asset` (lo escribe `Prepare`)
- Modify: `CLAUDE.md`

- [ ] **Step 1: Copiar los mapas** (se copian, los originales quedan en Descargas)

```bash
mkdir -p Assets/AncoRA/Paseo/CienciasBasicas Assets/AncoRA/Paseo/X1 Assets/AncoRA/Paseo/EIC Assets/AncoRA/Paseo/Teologia
cp "/c/Users/nicol/Downloads/Mapas/152192-csbasicasgael.bytes" "/c/Users/nicol/Downloads/Mapas/152196-csbasicasgael2.bytes" Assets/AncoRA/Paseo/CienciasBasicas/
cp "/c/Users/nicol/Downloads/Mapas/152198-x1gael.bytes" Assets/AncoRA/Paseo/X1/
cp "/c/Users/nicol/Downloads/Mapas/152199-eicgael.bytes" Assets/AncoRA/Paseo/EIC/
cmp "/c/Users/nicol/Downloads/Mapas/152198-x1gael.bytes" Assets/AncoRA/Paseo/X1/152198-x1gael.bytes && echo copia-ok
```

Expected: `copia-ok`.

- [ ] **Step 2: Escribir los `edificio.json`**

`Assets/AncoRA/Paseo/CienciasBasicas/edificio.json`:

```json
{
  "nombre": "Ciencias Básicas",
  "tamano": [20, 8, 12],
  "solido": false,
  "mapas": [
    { "id": 152192, "archivo": "152192-csbasicasgael.bytes", "caja": { "posicion": [0, 0, 12], "giro": 0, "colocada": false } },
    { "id": 152196, "archivo": "152196-csbasicasgael2.bytes", "caja": { "posicion": [0, 0, 12], "giro": 0, "colocada": false } }
  ]
}
```

`Assets/AncoRA/Paseo/X1/edificio.json`:

```json
{
  "nombre": "X1",
  "tamano": [20, 8, 12],
  "solido": false,
  "mapas": [
    { "id": 152198, "archivo": "152198-x1gael.bytes", "caja": { "posicion": [0, 0, 12], "giro": 0, "colocada": false } }
  ]
}
```

`Assets/AncoRA/Paseo/EIC/edificio.json` (nombre visible provisional, pendiente del usuario):

```json
{
  "nombre": "EIC",
  "tamano": [20, 8, 12],
  "solido": false,
  "mapas": [
    { "id": 152199, "archivo": "152199-eicgael.bytes", "caja": { "posicion": [0, 0, 12], "giro": 0, "colocada": false } }
  ]
}
```

`Assets/AncoRA/Paseo/Teologia/edificio.json` (se completa cuando llegue su mapa):

```json
{
  "nombre": "Teología",
  "tamano": [20, 8, 12],
  "solido": false,
  "mapas": []
}
```

- [ ] **Step 3: Comprobar los datos**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.CheckInputs`
Expected: código 0, las cuatro líneas de edificios, el aviso `Teologia: no tiene mapas todavía` y `Datos de entrada OK`.

- [ ] **Step 4: Preparar la escena**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.Prepare`
Expected: código 0 y `Escena creada: Assets/Scenes/PaseoIngenieria.unity con 4 mapa(s) en 3 edificio(s)`.

- [ ] **Step 5: Validar y comprobar la carga nativa de los cuatro mapas**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.CheckNativeMapsInEditor`
Expected: código 0, `Validación ... OK`, cuatro líneas `Plugin del Editor cargó el mapa 15219x: N puntos` (con N > 0), y cuatro avisos de caja sin colocar.

- [ ] **Step 6: Compilar el APK**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.BuildAndroid`
Expected: código 0 y `APK listo: Builds/Android/AncoRAPaseo.apk (... MB en disco)`. Después revisar `git diff ProjectSettings/ProjectSettings.asset`: el applicationId debe seguir siendo `com.ancora.ucnar` (se restauró).

- [ ] **Step 7: Actualizar `CLAUDE.md`**

Reemplazar la sección `## Paseo virtual (en diseño, desde 2026-10-08)` completa por:

```markdown
## Paseo virtual (desde 2026-10-08)

Paseo **en el campus** por los edificios de la escuela de ingeniería. Una escena carga los mapas Immersal de todos los
edificios con **un XR Space por mapa**: ubicarse con un mapa mueve solo su espacio, así que se pueden ver dos edificios a la
vez, cada uno en su lugar. Por edificio se muestra la caja del mapa que localizó último. Diseño:
`docs/superpowers/specs/2026-10-08-paseo-virtual-design.md`; plan: `docs/superpowers/plans/2026-10-08-paseo-virtual.md`.

| Edificio (carpeta) | Nombre visible | Mapas |
|---|---|---|
| `CienciasBasicas` | Ciencias Básicas | `152192-csbasicasgael`, `152196-csbasicasgael2` |
| `EIC` | EIC *(provisional)* | `152199-eicgael` |
| `X1` | X1 | `152198-x1gael` |
| `Teologia` | Teología | sin mapa todavía |

**Datos:** `Assets/AncoRA/Paseo/<Edificio>/edificio.json` + `.bytes` (metadata y `.ply` opcionales). `edificio.json` manda:
`Prepare` rearma la escena desde cero cada vez. Agregar un edificio = carpeta nueva + `Prepare`.

**Código:** runtime en `Assets/AncoRA/Scripts/Paseo/` (`PaseoTour`, `PaseoMapContent`, `PaseoLabel`, `PaseoHud`,
`PaseoFieldAdjust` + lógica pura `PaseoConfig`, `PaseoAdjustment`, `PaseoVisibility`, `PaseoStatusText`, `PaseoBoxStore`,
`PaseoPlacement`); Editor en `Assets/AncoRA/Editor/Paseo/`; pruebas EditMode en `Assets/AncoRA/Editor/Tests/`.

**Comandos** (Editor cerrado; `Tools/UnityBatch.ps1` espera a Unity y resume el resultado):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSmokeTest.Run
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.Prepare
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.CheckNativeMapsInEditor
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.BuildAndroid
```

APK de desarrollo: `Builds/Android/AncoRAPaseo.apk`, paquete `com.ancora.ucnar.paseo`, «AncoRA Paseo».

**En terreno:** cada caja sin colocar aparece a 12 m delante de la cámara con «(sin colocar)». Panel del equipo: 5 toques
arriba a la izquierda → «Ajuste: Caja» (edita la caja del último mapa ubicado; se guarda en el teléfono con prefijo
`AncoRA.Paseo.v1.`). «Copiar valores» → pegar en `Assets/AncoRA/Paseo/ajuste-campo.json` →
`PaseoSetup.ApplyFieldAdjustment` (escribe `edificio.json` y rearma la escena). Ciencias Básicas se coloca una vez en cada
uno de sus dos mapas.

**Pendientes del paseo:**
- [ ] Prueba en terreno: cada edificio localiza; con dos a la vista, ubicarse con el segundo **no mueve** el primero;
  Ciencias Básicas cambia de mapa sin duplicar la caja; nombre legible desde la distancia de observación.
- [ ] Colocar las cajas en terreno y hornearlas con `ApplyFieldAdjustment`.
- [ ] Nombre visible definitivo de EIC.
- [ ] Mapa de Teología.
```

En la tabla de *Código que quedó* de la sección de pilotos, dejar solo `EdificioFacadeFrame.cs`, `GuiSafeArea.cs` y `PipelineMaterials.cs`, y cambiar el párrafo siguiente por: «El resto se retiró: el paseo lo reemplaza (ver *Paseo virtual*).». En `### Menús del Editor` reemplazar el texto por: «`AncoRA/Paseo/…`: comprobar datos, preparar escena, validar, carga nativa, aplicar ajuste de campo, compilar APK y prueba de humo. Todos son `public static` para llamarlos con `-executeMethod`.». En el bloque `### Compilar`, cambiar `BuildAndroid.Build` por `AncorRA.Editor.PaseoSetup.BuildAndroid` y la frase que sigue por «Usar `Tools/UnityBatch.ps1` (ver *Paseo virtual*), que espera a que Unity termine.».

- [ ] **Step 8: Commit**

```bash
git add Assets/AncoRA/Paseo Assets/AncoRA/Paseo.meta Assets/Scenes Assets/Scenes.meta ProjectSettings/EditorBuildSettings.asset CLAUDE.md
git status --short
git commit -m "Agregar los mapas y la escena del paseo virtual

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

Antes del commit, revisar que `git status --short` no tenga staged nada de la lista de archivos regenerados (Global Constraints). Si `ProjectSettings/ProjectSettings.asset` cambió, revisar el diff: commitearlo solo si el cambio es de `EnsureAndroidSettings` (unsafe/IL2CPP/ARM64/GLES3), nunca el applicationId.

- [ ] **Step 9: Entregar al usuario**

No instalar ni abrir la app. Informar la ruta del APK y la lista de prueba en terreno de `CLAUDE.md`, y preguntar si se instala por `adb install` (con `adb devices` antes).
