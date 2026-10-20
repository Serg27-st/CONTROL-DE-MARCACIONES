param(
    [Parameter(Mandatory = $true)]
    [string]$Publish
)

$ErrorActionPreference = 'Stop'
$publishResuelto = (Resolve-Path -LiteralPath $Publish).Path
$exe = Join-Path $publishResuelto 'ControlMarcacion.Web.exe'
$temporal = Join-Path $env:TEMP ("cm-portable-" + [guid]::NewGuid().ToString('N'))
$runtime = Join-Path $temporal 'runtime'
$db = Join-Path $temporal 'data\ControlMarcacion.db'
$logs = Join-Path $temporal 'logs'
New-Item -ItemType Directory -Path $runtime | Out-Null

$preferido = 5000
$ocupado = $null
try {
    $ocupado = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, $preferido)
    $ocupado.Start()
} catch [System.Net.Sockets.SocketException] {
    if ($_.Exception.SocketErrorCode -ne [System.Net.Sockets.SocketError]::AddressAlreadyInUse) { throw }
    $ocupado = $null
}

$argumentos = @(
    "--Despliegue:PuertoPreferido=$preferido",
    '--Despliegue:AbrirNavegador=false',
    '--Despliegue:ExigirHttps=false',
    "--Despliegue:RutaBaseDatos=$db",
    "--Despliegue:RutaRuntime=$runtime",
    "--Despliegue:RutaLogs=$logs",
    '--Seguridad:AdminCorreo=',
    '--Seguridad:AdminClave='
)

$env:Seguridad__AdminCorreo = $null
$env:Seguridad__AdminClave = $null
$pidServidor = $null
$exitoso = $false

try {
    Start-Process -FilePath $exe -ArgumentList $argumentos -WorkingDirectory $publishResuelto -WindowStyle Hidden | Out-Null
    $descriptor = $null
    $estado = $null
    $respuestaSetup = $null
    foreach ($intento in 1..100) {
        Start-Sleep -Milliseconds 500
        $descriptor = Get-ChildItem -LiteralPath $runtime -Filter 'instancia*.json' -File -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
        if ($descriptor -and (Test-Path -LiteralPath $descriptor)) {
            $estado = Get-Content -Raw -LiteralPath $descriptor | ConvertFrom-Json
            try {
                $respuestaSetup = Invoke-WebRequest "http://127.0.0.1:$($estado.Puerto)/setup" -UseBasicParsing -SessionVariable sesion -TimeoutSec 2
                if ($respuestaSetup.StatusCode -eq 200 -and $respuestaSetup.Content -match 'Crear coordinador') { break }
            } catch { }
        }
    }
    if (-not $respuestaSetup -or $respuestaSetup.StatusCode -ne 200) { throw 'FIRST RUN: /setup no estuvo disponible.' }
    if ([int]$estado.Puerto -eq $preferido) { throw 'PUERTO OCUPADO: no ocurrio el fallback.' }
    $pidServidor = [int]$estado.Pid

    $vivo = Invoke-WebRequest "http://127.0.0.1:$($estado.Puerto)/vivo" -UseBasicParsing -TimeoutSec 3
    if ($vivo.StatusCode -ne 200 -or $vivo.Content -notmatch 'ControlMarcaciones-OK') { throw 'LIVENESS invalido.' }

    $tokenMatch = [regex]::Match($respuestaSetup.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $tokenMatch.Success) { throw 'El wizard no emitio token antiforgery.' }
    $token = [System.Net.WebUtility]::HtmlDecode($tokenMatch.Groups[1].Value)
    $correo = "portable-$([guid]::NewGuid().ToString('N'))@example.invalid"
    $clave = "Portable-$([guid]::NewGuid().ToString('N').Substring(0,12))-Aa9"
    Invoke-WebRequest "http://127.0.0.1:$($estado.Puerto)/setup" -UseBasicParsing -WebSession $sesion -Method Post -Body @{
        __RequestVerificationToken = $token
        NombreCompleto = 'Coordinador portable temporal'
        Correo = $correo
        Clave = $clave
        ConfirmacionClave = $clave
    } -TimeoutSec 15 | Out-Null

    $listo = $null
    foreach ($intento in 1..40) {
        Start-Sleep -Milliseconds 250
        try {
            $listo = Invoke-WebRequest "http://127.0.0.1:$($estado.Puerto)/estado" -UseBasicParsing -TimeoutSec 2
            if ($listo.StatusCode -eq 200 -and $listo.Content -match 'ControlMarcaciones-OK;LISTO') { break }
        } catch { }
    }
    if (-not $listo -or $listo.StatusCode -ne 200) { throw 'SETUP: el sistema no quedo LISTO.' }

    $segundo = Start-Process -FilePath $exe -ArgumentList $argumentos -WorkingDirectory $publishResuelto -PassThru -WindowStyle Hidden
    if (-not $segundo.WaitForExit(20000)) { throw 'DOBLE INSTANCIA: el segundo proceso no termino.' }
    $estadoDespues = Get-Content -Raw -LiteralPath $descriptor | ConvertFrom-Json
    if ([int]$estadoDespues.Pid -ne $pidServidor) { throw 'DOBLE INSTANCIA: reemplazo al servidor.' }

    $direccionLan = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -ne '127.0.0.1' -and $_.IPAddress -notlike '169.254.*' } |
        Select-Object -First 1 -ExpandProperty IPAddress
    if ($direccionLan) {
        $lanRespondio = $false
        try {
            Invoke-WebRequest "http://$direccionLan`:$($estado.Puerto)/vivo" -UseBasicParsing -TimeoutSec 2 -NoProxy | Out-Null
            $lanRespondio = $true
        } catch { }
        if ($lanRespondio) { throw 'LAN: el endpoint respondio fuera de loopback.' }
    }

    Stop-Process -Id $pidServidor -Force
    Wait-Process -Id $pidServidor -ErrorAction SilentlyContinue
    $pidServidor = $null
    if ($ocupado) { $ocupado.Stop(); $ocupado = $null }

    Start-Process -FilePath $exe -ArgumentList $argumentos -WorkingDirectory $publishResuelto -WindowStyle Hidden | Out-Null
    $listoReinicio = $null
    foreach ($intento in 1..100) {
        Start-Sleep -Milliseconds 500
        if (Test-Path -LiteralPath $descriptor) {
            $estadoReinicio = Get-Content -Raw -LiteralPath $descriptor | ConvertFrom-Json
            try {
                $listoReinicio = Invoke-WebRequest "http://127.0.0.1:$($estadoReinicio.Puerto)/estado" -UseBasicParsing -TimeoutSec 2
                if ($listoReinicio.StatusCode -eq 200 -and $listoReinicio.Content -match 'LISTO') { break }
            } catch { }
        }
    }
    if (-not $listoReinicio -or $listoReinicio.StatusCode -ne 200) { throw 'RESTART/PERSISTENCIA: la DB configurada no quedo LISTA.' }
    $pidServidor = [int]$estadoReinicio.Pid
    $exitoso = $true
    Write-Output "VALIDO firstRun=SETUP ready=OK preferido=$preferido fallback=$($estado.Puerto) dobleInstancia=OK persistencia=OK lan=LOOPBACK restart=OK"
}
finally {
    if ($ocupado) { $ocupado.Stop() }
    if ($pidServidor) { Stop-Process -Id $pidServidor -Force -ErrorAction SilentlyContinue }
    Get-CimInstance Win32_Process -Filter "Name='ControlMarcacion.Web.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like "*$temporal*" } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    $env:Seguridad__AdminCorreo = $null
    $env:Seguridad__AdminClave = $null
    if ($exitoso) {
        Remove-Item -LiteralPath $temporal -Recurse -Force -ErrorAction SilentlyContinue
    } else {
        Write-Warning "Diagnostico conservado en $temporal"
    }
}
