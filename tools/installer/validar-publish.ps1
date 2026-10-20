$ErrorActionPreference = 'Stop'
$raiz = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exe = Join-Path $raiz 'artifacts\local-installer\publish\ControlMarcacion.exe'
$temporal = Join-Path $env:TEMP ("cm-local-installer-" + [guid]::NewGuid().ToString('N'))
$runtime = Join-Path $temporal 'runtime'
$db = Join-Path $temporal 'data\ControlMarcacion.db'
$logs = Join-Path $temporal 'logs'
New-Item -ItemType Directory -Path $runtime | Out-Null

$puertoPrueba = 15000
$ocupado = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, $puertoPrueba)
$ocupado.Start()
$preferido = $ocupado.LocalEndpoint.Port
$argumentos = @(
    "--Despliegue:PuertoPreferido=$preferido",
    '--Despliegue:AbrirNavegador=false',
    '--Despliegue:ExigirHttps=false',
    "--Despliegue:RutaBaseDatos=$db",
    "--Despliegue:RutaRuntime=$runtime",
    "--Despliegue:RutaLogs=$logs"
)

$env:Seguridad__AdminCorreo = $null
$env:Seguridad__AdminClave = $null
$pidServidor = $null
$exitoso = $false

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

try {
    $primero = Start-Process -FilePath $exe -ArgumentList $argumentos -WorkingDirectory (Split-Path $exe) -PassThru -WindowStyle Hidden
    $descriptor = $null
    foreach ($intento in 1..80) {
        Start-Sleep -Milliseconds 500
        $descriptor = Get-ChildItem -LiteralPath $runtime -Filter 'instancia*.json' -File -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
        if ($descriptor -and (Test-Path $descriptor)) {
            $estado = Get-Content $descriptor -Raw | ConvertFrom-Json
            try {
                $r = Invoke-WebRequest "http://127.0.0.1:$($estado.Puerto)/setup" -UseBasicParsing -SessionVariable sesion -TimeoutSec 2
                if ($r.StatusCode -eq 200 -and $r.Content -match 'Crear coordinador') { break }
            } catch { }
        }
    }
    if (-not $estado -or -not $r -or $r.StatusCode -ne 200) { throw 'El publish no llegó a /setup.' }
    if ([int]$estado.Puerto -eq $preferido) { throw 'No ocurrió el fallback con el puerto ocupado.' }
    $pidServidor = [int]$estado.Pid

    $segundoEnSetup = Start-Process -FilePath $exe -ArgumentList $argumentos -WorkingDirectory (Split-Path $exe) -PassThru -WindowStyle Hidden
    if (-not $segundoEnSetup.WaitForExit(20000)) { throw 'La segunda instancia no terminó durante SetupRequired.' }
    $estadoDespuesSetup = Get-Content $descriptor -Raw | ConvertFrom-Json
    if ([int]$estadoDespuesSetup.Pid -ne $pidServidor) {
        throw 'La segunda instancia reemplazó al servidor durante SetupRequired.'
    }

    $tokenMatch = [regex]::Match($r.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $tokenMatch.Success) { throw 'El wizard no emitió token antiforgery.' }
    $token = [System.Net.WebUtility]::HtmlDecode($tokenMatch.Groups[1].Value)
    $correo = "smoke-$([guid]::NewGuid().ToString('N'))@example.invalid"
    $clave = "Smoke-$([guid]::NewGuid().ToString('N').Substring(0,12))-Aa9"
    Invoke-WebRequest "http://127.0.0.1:$($estado.Puerto)/setup" -UseBasicParsing -WebSession $sesion -Method Post -Body @{
        __RequestVerificationToken = $token
        NombreCompleto = 'Coordinador smoke'
        Correo = $correo
        Clave = $clave
        ConfirmacionClave = $clave
    } -TimeoutSec 15 | Out-Null
    $r = $null
    foreach ($intento in 1..40) {
        Start-Sleep -Milliseconds 250
        try {
            $r = Invoke-WebRequest "http://127.0.0.1:$($estado.Puerto)/estado" -UseBasicParsing -TimeoutSec 2
            if ($r.StatusCode -eq 200 -and $r.Content -match 'ControlMarcaciones-OK;LISTO') { break }
        } catch { }
    }
    if (-not $r -or $r.StatusCode -ne 200) { throw 'El publish no quedó LISTO tras /setup.' }

    $segundo = Start-Process -FilePath $exe -ArgumentList $argumentos -WorkingDirectory (Split-Path $exe) -PassThru -WindowStyle Hidden
    if (-not $segundo.WaitForExit(20000)) { throw 'La segunda instancia no terminó.' }
    $estadoDespues = Get-Content $descriptor -Raw | ConvertFrom-Json
    if ([int]$estadoDespues.Pid -ne $pidServidor) { throw 'La segunda instancia reemplazó al servidor.' }

    $direccionLan = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -ne '127.0.0.1' -and $_.IPAddress -notlike '169.254.*' } |
        Select-Object -First 1 -ExpandProperty IPAddress
    if ($direccionLan) {
        $lanRespondio = $false
        try {
            Invoke-WebRequest "http://$direccionLan`:$($estado.Puerto)/vivo" -UseBasicParsing -TimeoutSec 2 | Out-Null
            $lanRespondio = $true
        } catch { }
        if ($lanRespondio) { throw 'El endpoint respondió mediante una dirección LAN.' }
    }

    Stop-Process -Id $pidServidor -Force
    Wait-Process -Id $pidServidor -ErrorAction SilentlyContinue
    $pidServidor = $null

    $archivosConClave = @(Get-ChildItem -LiteralPath $temporal -Recurse -File | Where-Object {
        Contiene-TextoPlano $_.FullName $clave
    })
    if ($archivosConClave.Count -ne 0) {
        throw 'La clave temporal quedó persistida en texto plano dentro del ensayo portable.'
    }

    $ocupado.Stop()

    $env:Seguridad__AdminCorreo = $null
    $env:Seguridad__AdminClave = $null
    $reinicio = Start-Process -FilePath $exe -ArgumentList $argumentos -WorkingDirectory (Split-Path $exe) -PassThru -WindowStyle Hidden
    foreach ($intento in 1..80) {
        Start-Sleep -Milliseconds 500
        if (Test-Path $descriptor) {
            $estadoReinicio = Get-Content $descriptor -Raw | ConvertFrom-Json
            try {
                $r2 = Invoke-WebRequest "http://127.0.0.1:$($estadoReinicio.Puerto)/estado" -UseBasicParsing -TimeoutSec 2
                if ($r2.StatusCode -eq 200) { break }
            } catch { }
        }
    }
    if (-not $r2 -or $r2.StatusCode -ne 200) { throw 'El reinicio con la DB persistente no quedó LISTO.' }
    $pidServidor = [int]$estadoReinicio.Pid
    $exitoso = $true
    Write-Output "VALIDO puertoPreferido=$preferido puertoFallback=$($estado.Puerto) singleSetup=OK singleReady=OK plaintext=NO pid=$pidServidor"
}
finally {
    $ocupado.Stop()
    if ($pidServidor) { Stop-Process -Id $pidServidor -Force -ErrorAction SilentlyContinue }
    Get-CimInstance Win32_Process -Filter "Name='ControlMarcacion.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like "*$temporal*" } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    $env:Seguridad__AdminCorreo = $null
    $env:Seguridad__AdminClave = $null
    if ($exitoso) {
        Remove-Item -LiteralPath $temporal -Recurse -Force -ErrorAction SilentlyContinue
    } else {
        Write-Warning "Diagnóstico conservado en $temporal"
    }
}
