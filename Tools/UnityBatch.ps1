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

$text = if (Test-Path $log) { Get-Content -Encoding UTF8 $log } else { @() }
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
