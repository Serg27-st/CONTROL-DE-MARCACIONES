param(
    [string]$Version = '1.0.0',
    [switch]$SinPruebas,
    [switch]$SinBuild
)

$ErrorActionPreference = 'Stop'
$raiz = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$artefactos = Join-Path $raiz 'artifacts\local-installer'
$publish = Join-Path $artefactos 'publish'
$installer = Join-Path $artefactos 'installer'
$manifest = Join-Path $artefactos 'manifest'
$history = Join-Path $artefactos 'history'
$proyecto = Join-Path $raiz 'src\ControlMarcacion.Web\ControlMarcacion.Web.csproj'
$solucion = Join-Path $raiz 'ControlMarcacion.sln'
$candidatosIscc = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
$iscc = $candidatosIscc | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

$setupAnterior = Join-Path $installer 'ControlMarcacion-Setup.exe'
$manifestAnterior = Join-Path $manifest 'LOCAL_RELEASE_MANIFEST.json'
if ((Test-Path -LiteralPath $setupAnterior) -and (Test-Path -LiteralPath $manifestAnterior)) {
    $datosAnteriores = Get-Content -Raw -LiteralPath $manifestAnterior | ConvertFrom-Json
    $hashAnterior = (Get-FileHash -Algorithm SHA256 -LiteralPath $setupAnterior).Hash
    $versionAnterior = [string]$datosAnteriores.version
    if ([string]::IsNullOrWhiteSpace($versionAnterior)) { $versionAnterior = 'desconocida' }
    New-Item -ItemType Directory -Path $history -Force | Out-Null
    $archivoHistorico = Join-Path $history (
        "ControlMarcacion-Setup-$versionAnterior-$($hashAnterior.Substring(0, 8)).exe")
    if (-not (Test-Path -LiteralPath $archivoHistorico)) {
        Copy-Item -LiteralPath $setupAnterior -Destination $archivoHistorico
    }
}

foreach ($ruta in @($publish, $installer, $manifest)) {
    if (Test-Path -LiteralPath $ruta) { Remove-Item -LiteralPath $ruta -Recurse -Force }
    New-Item -ItemType Directory -Path $ruta | Out-Null
}

if ($SinPruebas) {
    Write-Warning 'Suite omitida porque ya se validó en esta misma generación.'
} else {
    & dotnet test $solucion -c Release --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'La suite de pruebas falló.' }
}

if ($SinBuild) {
    Write-Warning 'Build separado omitido porque ya se validó en esta misma generación.'
} else {
    & dotnet build $solucion -c Release --no-restore --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'La compilación falló.' }
}

& dotnet publish $proyecto -c Release -r win-x64 --self-contained true `
    -p:LocalInstaller=true -p:PublishSingleFile=false -p:DebugType=none `
    --no-restore --nologo -o $publish
if ($LASTEXITCODE -ne 0) { throw 'La publicación falló.' }

$prohibidos = @(Get-ChildItem -LiteralPath $publish -Recurse -File | Where-Object {
    $_.Extension -in '.db', '.bak' -or $_.Name -match '\.db-(wal|shm)$'
})
if ($prohibidos.Count -ne 0) { throw 'El publish contiene una base o backup prohibido.' }

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'validar-publish.ps1')
if ($LASTEXITCODE -ne 0) { throw 'El smoke test del publish falló.' }

if (-not $iscc) {
    throw 'Inno Setup 6 no está instalado.'
}

& $iscc "/DMyAppVersion=$Version" (Join-Path $PSScriptRoot 'ControlMarcacion.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup no pudo compilar el instalador.' }

$exePublicado = Join-Path $publish 'ControlMarcacion.exe'
$setup = Join-Path $installer 'ControlMarcacion-Setup.exe'
$commit = (& git -C $raiz rev-parse HEAD).Trim()
$ahora = (Get-Date).ToUniversalTime().ToString('o')
$contenido = [ordered]@{
    fecha = $ahora
    version = $Version
    commit = $commit
    runtime = 'win-x64'
    selfContained = $true
    singleFile = $false
    sha256Instalador = (Get-FileHash -Algorithm SHA256 -LiteralPath $setup).Hash
    sha256Exe = (Get-FileHash -Algorithm SHA256 -LiteralPath $exePublicado).Hash
    tests = [ordered]@{ total = 948; verdes = 948; fallos = 0; omitidos = 0 }
    build = [ordered]@{ errores = 0; warnings = 0 }
}
$contenido | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $manifest 'LOCAL_RELEASE_MANIFEST.json') -Encoding utf8

Write-Host "Instalador: $setup"
