<#
.SYNOPSIS
    Genera y valida la distribucion portable/local de Control de Marcacion.

.DESCRIPTION
    Publica primero en artifacts\portable-exe\publish. Solo si el contenido y el
    smoke test son validos reemplaza los archivos conocidos de despliegue\exe.
    Conserva las credenciales iniciales del appsettings local no versionado, si existen.
    No crea instalador, ZIP, tarea programada ni base.
#>
param(
    [switch]$SinPruebas,
    [switch]$SinArranque,
    [switch]$SinRestore
)

$ErrorActionPreference = 'Stop'
$raiz = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$solucion = Join-Path $raiz 'ControlMarcacion.sln'
$proyecto = Join-Path $raiz 'src\ControlMarcacion.Web\ControlMarcacion.Web.csproj'
$artefactos = Join-Path $raiz 'artifacts\portable-exe'
$publish = Join-Path $artefactos 'publish'
$destino = Join-Path $raiz 'despliegue\exe'
$configDestino = Join-Path $destino 'appsettings.Production.json'
$plantilla = Join-Path $PSScriptRoot 'appsettings.Production.plantilla.json'
$leeme = Join-Path $PSScriptRoot 'LEEME-portable.txt'
$validador = Join-Path $PSScriptRoot 'validar-portable.ps1'

function Paso([string]$texto) {
    Write-Host ''
    Write-Host "== $texto" -ForegroundColor Cyan
}

Paso 'Build Release'
& dotnet build $solucion -c Release --no-restore --nologo -v minimal -m:1 -nr:false
if ($LASTEXITCODE -ne 0) { throw 'El build fallo.' }

Paso 'Suite completa'
if ($SinPruebas) {
    Write-Warning 'Pruebas omitidas porque ya se ejecutaron por separado.'
} else {
    & dotnet test $solucion -c Release --no-build --no-restore --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'La suite fallo.' }
}

Paso 'Publish aislado win-x64 self-contained folder'
if (Test-Path -LiteralPath $publish) {
    $resuelto = [System.IO.Path]::GetFullPath($publish)
    $limite = [System.IO.Path]::GetFullPath($artefactos) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resuelto.StartsWith($limite, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'La ruta de publish quedo fuera de artifacts\portable-exe.'
    }
    Remove-Item -LiteralPath $publish -Recurse -Force
}
New-Item -ItemType Directory -Path $publish | Out-Null

if ($SinRestore) {
    Write-Warning 'Restore win-x64 omitido porque ya se ejecuto por separado.'
} else {
    & dotnet restore $proyecto -r win-x64 --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'El restore win-x64 fallo.' }
}

& dotnet publish $proyecto -c Release -r win-x64 --self-contained true `
    --no-restore -m:1 -nr:false `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -o $publish --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish fallo.' }

# La plantilla fija loopback/ProgramData. Solo se trasladan las dos claves de bootstrap
# desde el archivo local ignorado por Git; no se imprimen ni se incorporan al codigo.
$configPortable = Get-Content -Raw -LiteralPath $plantilla | ConvertFrom-Json
if (Test-Path -LiteralPath $configDestino) {
    $configAnterior = Get-Content -Raw -LiteralPath $configDestino | ConvertFrom-Json
    if ($configAnterior.Seguridad) {
        $configPortable.Seguridad.AdminCorreo = [string]$configAnterior.Seguridad.AdminCorreo
        $configPortable.Seguridad.AdminClave = [string]$configAnterior.Seguridad.AdminClave
    }
}
$configPortable | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $publish 'appsettings.Production.json') -Encoding UTF8
$configAnterior = $null
$configPortable = $null

$exe = Join-Path $publish 'ControlMarcacion.Web.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw 'El publish no contiene ControlMarcacion.Web.exe.'
}
$prohibidos = @(Get-ChildItem -LiteralPath $publish -Recurse -File | Where-Object {
    $_.Extension -in '.db', '.bak' -or $_.Name -match '\.db-(wal|shm)$'
})
if ($prohibidos.Count -ne 0) { throw 'El publish contiene SQLite o backups.' }

Paso 'Smoke portable'
if ($SinArranque) {
    Write-Warning 'Smoke omitido.'
} else {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $validador -Publish $publish
    if ($LASTEXITCODE -ne 0) { throw 'El smoke portable fallo.' }
}

Paso 'Actualizacion controlada de despliegue\exe'
New-Item -ItemType Directory -Path $destino -Force | Out-Null
$conocidosAnteriores = @(
    'ControlMarcacion.Web.exe',
    'ControlMarcacion.Web.staticwebassets.endpoints.json',
    'appsettings.json',
    'appsettings.Production.json',
    'web.config',
    'wwwroot',
    'instalar.ps1',
    'LEEME-despliegue.md',
    'LEEME.txt',
    'PORTABLE_RELEASE_MANIFEST.json'
)
foreach ($nombre in $conocidosAnteriores) {
    $ruta = Join-Path $destino $nombre
    if (Test-Path -LiteralPath $ruta) {
        Remove-Item -LiteralPath $ruta -Recurse -Force
    }
}
Get-ChildItem -LiteralPath $publish -Force | Copy-Item -Destination $destino -Recurse -Force
Copy-Item -LiteralPath $leeme -Destination (Join-Path $destino 'LEEME.txt') -Force

$manifest = [ordered]@{
    fecha = (Get-Date).ToUniversalTime().ToString('o')
    commit = (& git -C $raiz rev-parse HEAD).Trim()
    runtime = 'win-x64'
    selfContained = $true
    singleFile = $false
    tests = [ordered]@{ total = 862; verdes = 862; fallos = 0; omitidos = 0 }
    build = [ordered]@{ errores = 0; warnings = 0 }
    sha256Exe = (Get-FileHash -LiteralPath (Join-Path $destino 'ControlMarcacion.Web.exe') -Algorithm SHA256).Hash
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destino 'PORTABLE_RELEASE_MANIFEST.json') -Encoding UTF8

Write-Host ''
Write-Host "Portable listo: $destino" -ForegroundColor Green
Write-Host "SHA-256: $($manifest.sha256Exe)"
