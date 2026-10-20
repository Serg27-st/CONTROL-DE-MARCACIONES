param(
    [string]$Setup = (Join-Path $PSScriptRoot '..\..\artifacts\local-installer\installer\ControlMarcacion-Setup.exe'),
    [string]$Resultado = (Join-Path $PSScriptRoot '..\..\artifacts\local-installer\manifest\CICLO_ELEVADO_RESULTADO.json')
)

$ErrorActionPreference = 'Stop'
$identidad = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identidad)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Este ensayo debe ejecutarse elevado mediante UAC.'
}

$setupResuelto = (Resolve-Path -LiteralPath $Setup).Path
$scriptCiclo = Join-Path $PSScriptRoot 'validar-ciclo-elevado.ps1'
$app = 'C:\Program Files\ControlMarcacion'
$programData = 'C:\ProgramData\ControlMarcacion'
$marca = Get-Date -Format 'yyyyMMdd-HHmmss'
$evidenciaOriginal = "C:\ProgramData\ControlMarcacion-evidencia-$marca"
$originalApartada = "C:\ProgramData\ControlMarcacion-original-apartado-$marca"
$datosEnsayo = "C:\ProgramData\ControlMarcacion-ensayo-$marca"
$dbRelativa = 'data\ControlMarcacion.db'
$hashEsperado = '3448B2B2024F710BBCD0471D86F00F079F425E8CADC7C1FDE4105E7C93DC9B1C'
$tamanoEsperado = 6873088L
$cicloValido = $false
$originalMovida = $false
$copiaVerificada = $false
$hashOriginal = $null
$hashCopia = $null
$hashEnsayo = $null

function Ejecutar-Y-Verificar([string]$archivo, [string[]]$argumentos) {
    $proceso = Start-Process -FilePath $archivo -ArgumentList $argumentos -Wait -PassThru
    if ($proceso.ExitCode -ne 0) {
        throw "$archivo terminó con código $($proceso.ExitCode)."
    }
}

try {
    $procesosPrevios = @(Get-Process -Name ControlMarcacion -ErrorAction SilentlyContinue)
    $procesosPrevios | Stop-Process -Force -ErrorAction SilentlyContinue
    $procesosPrevios | Wait-Process -ErrorAction SilentlyContinue

    $dbOriginal = Join-Path $programData $dbRelativa
    if (-not (Test-Path -LiteralPath $dbOriginal)) {
        throw "No existe la base original esperada: $dbOriginal"
    }

    $archivoOriginal = Get-Item -LiteralPath $dbOriginal
    $hashOriginal = (Get-FileHash -Algorithm SHA256 -LiteralPath $dbOriginal).Hash
    if ($archivoOriginal.Length -ne $tamanoEsperado -or $hashOriginal -ne $hashEsperado) {
        throw "La base original no coincide con la evidencia autorizada. Tamaño=$($archivoOriginal.Length); SHA256=$hashOriginal"
    }

    Copy-Item -LiteralPath $programData -Destination $evidenciaOriginal -Recurse
    $dbCopia = Join-Path $evidenciaOriginal $dbRelativa
    if (-not (Test-Path -LiteralPath $dbCopia)) {
        throw 'La copia de preservación no contiene la base esperada.'
    }
    $archivoCopia = Get-Item -LiteralPath $dbCopia
    $hashCopia = (Get-FileHash -Algorithm SHA256 -LiteralPath $dbCopia).Hash
    if ($archivoCopia.Length -ne $tamanoEsperado -or $hashCopia -ne $hashOriginal) {
        throw "Falló la verificación de la copia de preservación. Tamaño=$($archivoCopia.Length); SHA256=$hashCopia"
    }
    $copiaVerificada = $true

    $uninstaller = Join-Path $app 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller) {
        Ejecutar-Y-Verificar $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
    }
    if (Test-Path -LiteralPath $app) {
        throw 'La desinstalación previa no retiró Program Files.'
    }

    Move-Item -LiteralPath $programData -Destination $originalApartada
    $originalMovida = $true

    & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptCiclo `
        -Setup $setupResuelto -Resultado $Resultado
    if ($LASTEXITCODE -ne 0) { throw 'El ciclo elevado limpio falló.' }
    $cicloValido = $true
}
finally {
    $procesosEnsayo = @(Get-Process -Name ControlMarcacion -ErrorAction SilentlyContinue)
    $procesosEnsayo | Stop-Process -Force -ErrorAction SilentlyContinue
    $procesosEnsayo | Wait-Process -ErrorAction SilentlyContinue

    if (Test-Path -LiteralPath $programData) {
        $dbEnsayo = Join-Path $programData $dbRelativa
        if (Test-Path -LiteralPath $dbEnsayo) {
            $hashEnsayo = (Get-FileHash -Algorithm SHA256 -LiteralPath $dbEnsayo).Hash
        }
        Move-Item -LiteralPath $programData -Destination $datosEnsayo
    }

    if ($originalMovida -and (Test-Path -LiteralPath $originalApartada)) {
        Move-Item -LiteralPath $originalApartada -Destination $programData
    }

    $dbRestaurada = Join-Path $programData $dbRelativa
    if ($originalMovida) {
        if (-not (Test-Path -LiteralPath $dbRestaurada)) {
            throw 'La restauración no devolvió la base original.'
        }
        $archivoRestaurado = Get-Item -LiteralPath $dbRestaurada
        $hashRestaurado = (Get-FileHash -Algorithm SHA256 -LiteralPath $dbRestaurada).Hash
        if ($archivoRestaurado.Length -ne $tamanoEsperado -or $hashRestaurado -ne $hashEsperado) {
            throw "La base restaurada no coincide con la original. Tamaño=$($archivoRestaurado.Length); SHA256=$hashRestaurado"
        }
    }

    # Solo después de haber apartado y restaurado la base deja la versión nueva instalada.
    if ($originalMovida) {
        Ejecutar-Y-Verificar $setupResuelto @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-')
    }

    $resumen = [ordered]@{
        Estado = if ($cicloValido) { 'VALIDO' } else { 'FALLO' }
        CopiaPreservacion = $evidenciaOriginal
        CopiaPreservacionVerificada = $copiaVerificada
        HashDbCopiaPreservacion = $hashCopia
        DatosOriginalesRestaurados = $originalMovida -and (Test-Path -LiteralPath $programData)
        HashDbOriginalAntes = $hashOriginal
        HashDbOriginalDespues = if (Test-Path -LiteralPath (Join-Path $programData $dbRelativa)) {
            (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $programData $dbRelativa)).Hash
        } else { $null }
        HashDbEnsayo = $hashEnsayo
        Fecha = (Get-Date).ToUniversalTime().ToString('o')
    }
    $resumen | ConvertTo-Json | Set-Content `
        -LiteralPath (Join-Path (Split-Path $Resultado) 'CICLO_REAL_PRESERVACION.json') `
        -Encoding utf8

    if ($cicloValido -and (Test-Path -LiteralPath $datosEnsayo)) {
        Remove-Item -LiteralPath $datosEnsayo -Recurse -Force
    }
}

if (-not $cicloValido) { exit 1 }
