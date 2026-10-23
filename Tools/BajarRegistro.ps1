<#
.SYNOPSIS
  Junta en Registros\ los registros de las sesiones del paseo (y las fotos de depuración) y resume la sesión más reciente
  con AnalizarRegistro.js. Sin -Archivo los baja del teléfono conectado por adb (no abre la app); con -Archivo abre el .zip
  que la app comparte («Compartir registro» en la hoja del equipo, por ejemplo por WhatsApp) o un .log suelto.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\BajarRegistro.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\BajarRegistro.ps1 -Serial dd5da89f -Todas
  powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\BajarRegistro.ps1 -Archivo "$env:USERPROFILE\Downloads\AncoRA-registro-23127PN0CC-20261021-1700.zip"
#>
param(
    [string]$Serial,
    # Un .zip compartido desde la app o un paseo-*.log, en vez de bajar por adb.
    [string]$Archivo,
    # Resume todas las sesiones, no solo la más reciente.
    [switch]$Todas,
    # La app de la que se bajan los registros: en esta rama, la de la prueba MultiSet (com.ancora.ucnar.paseo para la normal).
    [string]$Paquete = 'com.ancora.ucnar.paseo.multiset'
)
$ErrorActionPreference = 'Stop'
$adb = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
$project = Split-Path -Parent $PSScriptRoot
$remote = "/sdcard/Android/data/$Paquete/files/registro"
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

if ($Archivo) {
    if (-not (Test-Path $Archivo)) { Write-Host "No existe $Archivo."; exit 2 }
    $name = [System.IO.Path]::GetFileNameWithoutExtension($Archivo)
    $out = Join-Path $project "Registros\$stamp-$name"
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    if ($Archivo -like '*.zip') { Expand-Archive -Path $Archivo -DestinationPath $out -Force }
    else { Copy-Item $Archivo $out }
} else {
    # Only one phone is connected at a time and they are swapped often: never assume the serial.
    $devices = @(& $adb devices | Select-String -Pattern '^(\S+)\s+device$' | ForEach-Object { $_.Matches[0].Groups[1].Value })
    if (-not $Serial) {
        if ($devices.Count -ne 1) {
            Write-Host "Hay $($devices.Count) teléfonos conectados ($($devices -join ', ')); indicar uno con -Serial."
            exit 2
        }
        $Serial = $devices[0]
    } elseif ($devices -notcontains $Serial) {
        Write-Host "El teléfono $Serial no está conectado (conectados: $($devices -join ', '))."
        exit 2
    }
    $out = Join-Path $project "Registros\$stamp-$Serial"
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    & $adb -s $Serial pull $remote $out
    if ($LASTEXITCODE -ne 0) {
        Write-Host "adb pull falló (código $LASTEXITCODE). ¿Se abrió la app al menos una vez en este teléfono?"
        exit 3
    }
}

$logs = @(Get-ChildItem -Recurse -Path $out -Filter 'paseo-*.log' | Sort-Object Name)
$images = @(Get-ChildItem -Recurse -Path $out -Filter '*.jpg')
Write-Host "En $out : $($logs.Count) sesión(es), $($images.Count) foto(s) de depuración."
$device = Get-ChildItem -Recurse -Path $out -Filter 'telefono.txt' | Select-Object -First 1
if ($device) { Get-Content -Encoding UTF8 $device.FullName | ForEach-Object { Write-Host "  $_" } }
if ($logs.Count -eq 0) { exit 0 }
$toSummarise = if ($Todas) { $logs } else { @($logs[-1]) }
foreach ($log in $toSummarise) {
    Write-Host ''
    & node (Join-Path $PSScriptRoot 'AnalizarRegistro.js') $log.FullName "--csv=$($log.FullName -replace '\.log$', '-intentos.csv')"
}
