# Aligns one building's drone photos in RealityScan without its UI and exports what the registration scripts need.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\AlinearDron.ps1 -Edificio X1 -Fotos .\Imgs\X1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\AlinearDron.ps1 -Edificio Teologia -Proyecto TeologiaAlto -Fotos .\Imgs\Teologia
#   Several folders (several flights) need -Command, because -File passes "a,b" as one string:
#   powershell -NoProfile -ExecutionPolicy Bypass -Command "& .\Tools\AlinearDron.ps1 -Edificio EIC -Proyecto EIC2 -Fotos '.\Imgs\EIC','.\Imgs\EIC-2026-10-09'"
#
# In Escenas\<Edificio>\ (only the first two are committed):
#   <Proyecto>.ply            tie points georeferenced with the drone GPS: x east, y north, z up, metres
#   <Proyecto>-georef.json    the transform, per-flight GPS corrections and camera/GPS residuals
#   <Proyecto>.rsproj         RealityScan project (+ its data folder <Proyecto>\)
#   rs-<Proyecto>\            <Proyecto>-rs.ply (raw export), reporte.html, progreso.txt, colmap\ (camera poses), errores\
#
# RealityScan's own scale is not reliable here (DJI Fly exports keep the GPS only in XMP), so scale and vertical come
# from Tools\RegistroDron\georef_dron.js, which fits the reconstructed cameras to the XMP GPS. Several flights can be
# mixed: each gets its own GPS offset. The PLY and COLMAP formats are RealityScan's current export settings (the last
# ones used in its UI: ASCII PLY with colour, COLMAP text).
param(
    [Parameter(Mandatory = $true)][string]$Edificio,
    [Parameter(Mandatory = $true)][string[]]$Fotos,
    [string]$Proyecto = $Edificio,
    [string]$RealityScan = 'C:\Program Files\Epic Games\RealityScan_2.2\RealityScan.exe'
)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "Escenas\$Edificio"
$aux = Join-Path $out "rs-$Proyecto"
$project = Join-Path $out "$Proyecto.rsproj"
$ply = Join-Path $out "$Proyecto.ply"
$rawPly = Join-Path $aux "$Proyecto-rs.ply"
$georef = Join-Path $out "$Proyecto-georef.json"
$report = Join-Path $aux 'reporte.html'
$colmap = Join-Path $aux "colmap\$Proyecto.txt"
if (Test-Path $project) { throw "Ya existe $project. Bórralo o ábrelo en RealityScan; este script no lo pisa." }
New-Item -ItemType Directory -Force (Split-Path $colmap) | Out-Null
$photoDirs = @($Fotos | ForEach-Object { (Resolve-Path $_).Path })

$rsArgs = @('-headless', '-stdConsole', '-silent', (Join-Path $aux 'errores'), '-set', 'appQuitOnError=true',
    '-writeProgress', (Join-Path $aux 'progreso.txt'), '-newScene')
foreach ($d in $photoDirs) { $rsArgs += @('-addFolder', $d) }
$rsArgs += @(
    '-align', '-selectMaximalComponent',
    '-save', $project,
    '-exportSparsePointCloud', $rawPly,
    '-exportRegistration', $colmap,
    '-exportReport', $report, (Join-Path (Split-Path $RealityScan) 'Reports\SelectedComponent.html'),
    '-quit')
# Start-Process does not quote array elements, and the report template lives under "Program Files".
$argLine = ($rsArgs | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '

Write-Host "RealityScan: $Edificio/$Proyecto con $($photoDirs -join ', ')"
$start = Get-Date
$p = Start-Process -FilePath $RealityScan -ArgumentList $argLine -PassThru -Wait
Write-Host ("Terminó en {0:N1} min con código {1}" -f ((Get-Date) - $start).TotalMinutes, $p.ExitCode)

foreach ($file in @($project, $rawPly, $report)) {
    if (Test-Path $file) { Write-Host ("  OK     {0}  ({1:N1} MB)" -f $file, ((Get-Item $file).Length / 1MB)) }
    else { Write-Host "  FALTA  $file" }
}
if (Test-Path $report) {
    $text = (Get-Content $report -Raw -Encoding UTF8) -replace '(?s)<style.*?</style>', ' ' -replace '(?s)<script.*?</script>', ' ' -replace '<[^>]+>', ' ' -replace '\s+', ' '
    # The report follows RealityScan's UI language (Spanish or English here); print the lines that matter.
    foreach ($key in 'im\S+genes registradas|registered images', 'Conteo de puntos|Points'' count',
        'Mediana del error|Median of projection error', 'Error medio|Mean projection error') {
        if ($text -match "(($key)[^\d]*[\d.]+(\s*/\s*\d+)?)") { Write-Host "  $($Matches[1])" }
    }
}
$images = Join-Path (Split-Path $colmap) 'sparse\0\images.txt'
if ((Test-Path $images) -and (Test-Path $rawPly)) {
    Write-Host '  Georreferencia con el GPS del dron:'
    node (Join-Path $PSScriptRoot 'RegistroDron\georef_dron.js') $images $rawPly $ply $georef @photoDirs | ForEach-Object { "    $_" }
}
else { Write-Host "  FALTA  $images o ${rawPly}: sin poses y nube no se puede georreferenciar" }
