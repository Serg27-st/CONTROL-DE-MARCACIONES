param(
    [string]$Setup = (Join-Path $PSScriptRoot '..\..\artifacts\local-installer\installer\ControlMarcacion-Setup.exe'),
    [string]$Resultado = (Join-Path $PSScriptRoot '..\..\artifacts\local-installer\manifest\CICLO_ELEVADO_RESULTADO.json')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
trap {
    [ordered]@{
        Estado = 'FALLO'
        Error = $_.Exception.Message
        Fecha = (Get-Date).ToUniversalTime().ToString('o')
    } | ConvertTo-Json | Set-Content -LiteralPath $Resultado -Encoding utf8
    exit 1
}

$identidad = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identidad)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Este ensayo debe ejecutarse elevado mediante UAC.'
}

$setupResuelto = (Resolve-Path -LiteralPath $Setup).Path
$app = 'C:\Program Files\ControlMarcacion'
$exe = Join-Path $app 'ControlMarcacion.exe'
$uninstaller = Join-Path $app 'unins000.exe'
$programData = 'C:\ProgramData\ControlMarcacion'
$db = Join-Path $programData 'data\ControlMarcacion.db'
$runtime = Join-Path $programData 'runtime'
$logs = Join-Path $programData 'logs'
$nombreAcceso = 'Control de Marcaci' + [char]0x00f3 + 'n'
$menu = "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\$nombreAcceso\$nombreAcceso.lnk"
$escritorio = "C:\Users\Public\Desktop\$nombreAcceso.lnk"
$logInno = 'C:\Windows\Temp\ControlMarcacion-Inno.log'
$argumentosSetup = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', "/LOG=$logInno")

if ((Test-Path -LiteralPath $app) -or (Test-Path -LiteralPath $programData)) {
    throw 'El ensayo exige estado limpio: ya existe Program Files o ProgramData de ControlMarcacion.'
}

function Ejecutar-Y-Verificar([string]$archivo, [string[]]$argumentos) {
    $proceso = Start-Process -FilePath $archivo -ArgumentList $argumentos -Wait -PassThru
    if ($proceso.ExitCode -ne 0) { throw "$archivo terminó con código $($proceso.ExitCode)." }
}

function Verificar-Instalacion([bool]$exigirDb = $true) {
    foreach ($ruta in @($exe, $uninstaller, $programData, $menu, $escritorio)) {
        if (-not (Test-Path -LiteralPath $ruta)) { throw "Falta después de instalar: $ruta" }
    }
    if ($exigirDb -and -not (Test-Path -LiteralPath $db)) {
        throw "Falta después del primer arranque: $db"
    }
}

function Consultar-Estado($estado) {
    $cliente = New-Object System.Net.Http.HttpClient
    try {
        $respuesta = $cliente.GetAsync("http://127.0.0.1:$($estado.Puerto)/estado").GetAwaiter().GetResult()
        [pscustomobject]@{
            Codigo = [int]$respuesta.StatusCode
            Cuerpo = $respuesta.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        }
    }
    finally {
        $cliente.Dispose()
    }
}

function Iniciar-Y-ObtenerDescriptor([string]$lanzador = $escritorio) {
    $env:Seguridad__AdminCorreo = $null
    $env:Seguridad__AdminClave = $null
    Start-Process -FilePath $lanzador -WorkingDirectory $app | Out-Null
    foreach ($intento in 1..120) {
        Start-Sleep -Milliseconds 500
        $descriptor = Get-ChildItem -LiteralPath $runtime -Filter 'instancia-*.json' -File -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($descriptor) {
            $estado = Get-Content -Raw -LiteralPath $descriptor.FullName | ConvertFrom-Json
            $procesoDescriptor = Get-Process -Id ([int]$estado.Pid) -ErrorAction SilentlyContinue
            if (-not $procesoDescriptor -or $procesoDescriptor.ProcessName -ne 'ControlMarcacion') {
                continue
            }
            try {
                $respuesta = Invoke-WebRequest "http://127.0.0.1:$($estado.Puerto)/vivo" -UseBasicParsing -TimeoutSec 2
                if ($respuesta.StatusCode -eq 200 -and $respuesta.Content -match 'ControlMarcaciones-OK') {
                    return $estado
                }
            } catch { }
        }
    }
    throw 'La aplicación instalada no llegó a /vivo.'
}

function Esperar-SetupRequired($estado) {
    foreach ($intento in 1..120) {
        Start-Sleep -Milliseconds 500
        try {
            $respuesta = Consultar-Estado $estado
            if ($respuesta.Codigo -eq 503 -and $respuesta.Cuerpo -match 'CONFIGURACION_INICIAL_REQUERIDA') {
                return
            }
        } catch { }
    }
    throw 'La aplicación instalada no llegó a CONFIGURACION_INICIAL_REQUERIDA.'
}

function Esperar-Listo($estado) {
    foreach ($intento in 1..120) {
        Start-Sleep -Milliseconds 500
        try {
            $respuesta = Consultar-Estado $estado
            if ($respuesta.Codigo -eq 200 -and $respuesta.Cuerpo -match 'ControlMarcaciones-OK;LISTO') {
                return
            }
        } catch { }
    }
    throw 'La aplicación instalada no llegó a LISTO.'
}

function Validar-Setup-Bloqueado($estado) {
    $respuesta = Invoke-WebRequest "http://127.0.0.1:$($estado.Puerto)/setup" -UseBasicParsing -TimeoutSec 5
    if ($respuesta.StatusCode -ne 200 -or $respuesta.BaseResponse.ResponseUri.AbsolutePath -ne '/Cuenta/Ingresar') {
        throw 'FIRST-RUN-04: /setup siguió reutilizable después de crear el coordinador.'
    }
}

function Validar-Login($estado, [string]$correo, [string]$clave) {
    $url = "http://127.0.0.1:$($estado.Puerto)"
    $pagina = Invoke-WebRequest "$url/Cuenta/Ingresar" -UseBasicParsing -SessionVariable sesionLogin -TimeoutSec 5
    $coincidencia = [regex]::Match(
        $pagina.Content,
        'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $coincidencia.Success) { throw 'El login no emitió token antiforgery.' }
    $token = [System.Net.WebUtility]::HtmlDecode($coincidencia.Groups[1].Value)
    $inicio = Invoke-WebRequest "$url/Cuenta/Ingresar" -UseBasicParsing -WebSession $sesionLogin -Method Post -Body @{
        __RequestVerificationToken = $token
        Correo = $correo
        Clave = $clave
        Recordarme = 'false'
    } -TimeoutSec 15
    if ($inicio.StatusCode -ne 200 -or $inicio.BaseResponse.ResponseUri.AbsolutePath -eq '/Cuenta/Ingresar') {
        throw 'No fue posible iniciar sesión con el coordinador del ensayo.'
    }
}

function Completar-Wizard-Y-Login($estado) {
    $url = "http://127.0.0.1:$($estado.Puerto)"
    $pagina = Invoke-WebRequest "$url/setup" -UseBasicParsing -SessionVariable sesion -TimeoutSec 5
    if ($pagina.StatusCode -ne 200 -or $pagina.Content -notmatch 'Crear coordinador') {
        throw 'FIRST-RUN-02: /setup no quedó disponible.'
    }

    $coincidencia = [regex]::Match(
        $pagina.Content,
        'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $coincidencia.Success) { throw 'El wizard no emitió token antiforgery.' }
    $token = [System.Net.WebUtility]::HtmlDecode($coincidencia.Groups[1].Value)
    $correo = "first-run-$([guid]::NewGuid().ToString('N'))@example.invalid"
    $clave = "FirstRun-$([guid]::NewGuid().ToString('N').Substring(0,12))-Aa9"
    $login = Invoke-WebRequest "$url/setup" -UseBasicParsing -WebSession $sesion -Method Post -Body @{
        __RequestVerificationToken = $token
        NombreCompleto = 'Coordinador de ensayo'
        Correo = $correo
        Clave = $clave
        ConfirmacionClave = $clave
    } -TimeoutSec 15
    if ($login.StatusCode -ne 200 -or $login.BaseResponse.ResponseUri.AbsolutePath -ne '/Cuenta/Ingresar') {
        throw 'FIRST-RUN-03: el wizard no terminó en la pantalla de login.'
    }

    Esperar-Listo $estado
    Validar-Setup-Bloqueado $estado
    Validar-Login $estado $correo $clave
    [pscustomobject]@{ Correo = $correo; Clave = $clave }
}

function Validar-Single-Instance($estado) {
    $pidOriginal = [int]$estado.Pid
    Start-Process -FilePath $escritorio | Out-Null
    Start-Sleep -Seconds 3
    $descriptor = Get-ChildItem -LiteralPath $runtime -Filter 'instancia-*.json' -File |
        Select-Object -First 1
    $estadoPosterior = Get-Content -Raw -LiteralPath $descriptor.FullName | ConvertFrom-Json
    if ([int]$estadoPosterior.Pid -ne $pidOriginal) {
        throw 'La segunda ejecución reemplazó al servidor original.'
    }
    if (@(Get-Process -Name ControlMarcacion -ErrorAction SilentlyContinue).Count -ne 1) {
        throw 'La segunda ejecución dejó más de un servidor local.'
    }
}

function Contiene-TextoPlano([string]$ruta, [string]$texto) {
    $contenido = [System.IO.File]::ReadAllBytes($ruta)
    foreach ($codificacion in @([Text.Encoding]::UTF8, [Text.Encoding]::Unicode)) {
        $buscado = $codificacion.GetBytes($texto)
        for ($inicio = 0; $inicio -le $contenido.Length - $buscado.Length; $inicio++) {
            $coincide = $true
            for ($indice = 0; $indice -lt $buscado.Length; $indice++) {
                if ($contenido[$inicio + $indice] -ne $buscado[$indice]) {
                    $coincide = $false
                    break
                }
            }
            if ($coincide) { return $true }
        }
    }
    return $false
}

function Detener-Instancia($estado) {
    Stop-Process -Id ([int]$estado.Pid) -Force
    Wait-Process -Id ([int]$estado.Pid) -ErrorAction SilentlyContinue
}

Ejecutar-Y-Verificar $setupResuelto $argumentosSetup
Verificar-Instalacion $false
if (Test-Path -LiteralPath $db) {
    throw 'La instalación limpia creó una DB antes del primer arranque.'
}
$estadoInicial = Iniciar-Y-ObtenerDescriptor $escritorio
Esperar-SetupRequired $estadoInicial
Verificar-Instalacion
$credencial = Completar-Wizard-Y-Login $estadoInicial
Detener-Instancia $estadoInicial

$estadoSegundo = Iniciar-Y-ObtenerDescriptor $escritorio
Esperar-Listo $estadoSegundo
Validar-Setup-Bloqueado $estadoSegundo
Validar-Login $estadoSegundo $credencial.Correo $credencial.Clave
Validar-Single-Instance $estadoSegundo
Detener-Instancia $estadoSegundo

$archivosConClave = @(Get-ChildItem -LiteralPath $programData -Recurse -File | Where-Object {
    Contiene-TextoPlano $_.FullName $credencial.Clave
})
if ($archivosConClave.Count -ne 0) {
    throw 'La clave temporal quedó persistida en texto plano.'
}

$hashDb = (Get-FileHash -Algorithm SHA256 -LiteralPath $db).Hash
Ejecutar-Y-Verificar $setupResuelto $argumentosSetup
Verificar-Instalacion
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $db).Hash -ne $hashDb) {
    throw 'La reinstalación reemplazó o modificó la DB antes de iniciar.'
}
$estadoReinstalado = Iniciar-Y-ObtenerDescriptor $escritorio
Esperar-Listo $estadoReinstalado
Validar-Setup-Bloqueado $estadoReinstalado
Validar-Login $estadoReinstalado $credencial.Correo $credencial.Clave
Detener-Instancia $estadoReinstalado

Ejecutar-Y-Verificar $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
foreach ($ruta in @($exe, $menu, $escritorio)) {
    if (Test-Path -LiteralPath $ruta) { throw "La desinstalación no retiró: $ruta" }
}
if (-not (Test-Path -LiteralPath $programData)) { throw 'La desinstalación eliminó ProgramData.' }
if (-not (Test-Path -LiteralPath $db)) { throw 'La desinstalación eliminó la DB.' }
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $db).Hash -ne $hashDb) {
    throw 'La desinstalación modificó la DB.'
}

Ejecutar-Y-Verificar $setupResuelto $argumentosSetup
Verificar-Instalacion
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $db).Hash -ne $hashDb) {
    throw 'La reinstalación posterior no reutilizó la DB existente.'
}
$estadoFinal = Iniciar-Y-ObtenerDescriptor $escritorio
Esperar-Listo $estadoFinal
Validar-Setup-Bloqueado $estadoFinal
Validar-Login $estadoFinal $credencial.Correo $credencial.Clave

$textoLogs = (Get-ChildItem -LiteralPath $logs -Filter 'ControlMarcacion-*.log' -File |
    ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"
if ($textoLogs -match 'CredencialesBootstrapFaltantesException|Unhandled exception') {
    throw 'Los logs del ciclo contienen una excepción de bootstrap o no controlada.'
}
if ($textoLogs.Contains($credencial.Clave)) {
    throw 'Los logs del ciclo contienen la clave temporal en texto plano.'
}
if ($textoLogs -notmatch 'requiere configuraci.n inicial' -or $textoLogs -notmatch 'operativamente LISTO') {
    throw 'Los logs no contienen evidencia de SetupRequired y Ready.'
}

$resultadoFinal = [ordered]@{
    Estado = 'VALIDO'
    Uac = 'APROBADO'
    Instalacion = 'OK'
    PrimerArranque = 'OK'
    VivoAntesSetup = 200
    EstadoAntesSetup = 'CONFIGURACION_INICIAL_REQUERIDA'
    UsuarioInicial = 1
    RolInicial = 'COORDINADOR'
    PasswordPlaintext = 'NO'
    EstadoDespuesSetup = 'LISTO'
    SetupReutilizable = 'NO'
    Login = 'OK'
    SegundoArranque = 'OK'
    SingleInstance = 'OK'
    Reinstalacion = 'OK'
    Desinstalacion = 'OK'
    ReinstalacionPosterior = 'OK'
    DbPreservada = 'OK'
    ProgramDataPreservado = 'OK'
    LogsBootstrapException = 'NO'
    LogsSecrets = 'NO'
    PuertoFinal = $estadoFinal.Puerto
    PidFinal = $estadoFinal.Pid
}
$resultadoFinal | ConvertTo-Json | Set-Content -LiteralPath $Resultado -Encoding utf8
$resultadoFinal | ConvertTo-Json
