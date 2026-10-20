# Instala el Control de Marcaciones en esta PC y lo deja arrancando con Windows.
# Ejecutar UNA VEZ, en PowerShell abierto como Administrador.
#
# No instala SQL Server ni .NET: el ejecutable lleva su propio runtime y la base es un
# archivo SQLite que la aplicación crea sola en el primer arranque.

$ErrorActionPreference = 'Stop'

$carpeta = 'C:\ControlMarcaciones'
$ejecutable = Join-Path $carpeta 'ControlMarcacion.Web.exe'
$tarea = 'ControlMarcaciones'
$url = 'http://127.0.0.1:5000'

if (-not (Test-Path $ejecutable)) {
    throw "No se encontró $ejecutable. Copia primero el contenido del paquete a $carpeta."
}

# Una base vacia sin bootstrap externo aborta el arranque de forma deliberada. Detener la
# instalacion antes de registrar la tarea evita dejar un proceso que nunca quedara listo.
$configuracion = Join-Path $carpeta 'appsettings.Production.json'
if ((Test-Path $configuracion) -and (Select-String -Path $configuracion -Pattern 'PENDIENTE-PONER-CONTRASENA-NUEVA' -Quiet)) {
    throw 'appsettings.Production.json todavia tiene la contrasena de relleno. Configura el bootstrap externo antes de instalar.'
}

# AbrirNavegador en false: esta tarea corre como SYSTEM, que no tiene escritorio
# interactivo. Abrir ahí un navegador no muestra nada a nadie y deja un proceso colgado en
# la sesión 0. El navegador lo abre el doble clic del usuario, no el arranque del sistema.
$accion = New-ScheduledTaskAction `
    -Execute $ejecutable `
    -Argument '--Despliegue:AbrirNavegador=false' `
    -WorkingDirectory $carpeta

$disparador = New-ScheduledTaskTrigger -AtStartup

# SYSTEM y no la cuenta de la persona: así el sistema queda escuchando desde que la PC
# enciende, sin esperar a que alguien inicie sesión, y no hay que guardar la contraseña de
# ninguna cuenta dentro de la tarea. SYSTEM ya puede escribir en C:\ProgramData, que es
# donde vive la base.
$principal = New-ScheduledTaskPrincipal `
    -UserId 'SYSTEM' `
    -LogonType ServiceAccount `
    -RunLevel Highest

# ExecutionTimeLimit en cero es deliberado: sin él, el Programador de tareas mata el
# proceso a los tres días y el sistema deja de responder sin que nadie toque nada.
# RestartCount levanta la aplicación si se cae, que es lo que haría un servicio.
$opciones = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit ([TimeSpan]::Zero)

Register-ScheduledTask `
    -TaskName $tarea `
    -Action $accion `
    -Trigger $disparador `
    -Principal $principal `
    -Settings $opciones `
    -Description "Sistema de Control de Marcaciones (GCA). Escucha en $url" `
    -Force | Out-Null

Write-Host "Tarea '$tarea' registrada." -ForegroundColor Green

Start-ScheduledTask -TaskName $tarea
Write-Host "Arrancando (el primer arranque crea la base y puede tardar)..." -ForegroundColor Cyan

# Kestrel prueba el puerto preferido y luego los consecutivos. La propia aplicacion registra
# el puerto que consiguió enlazar para que el instalador no tenga que adivinarlo.
$preferido = 5000
$configuracionJson = if (Test-Path $configuracion) { Get-Content $configuracion -Raw | ConvertFrom-Json } else { $null }
if ($configuracionJson -and $configuracionJson.Despliegue -and $configuracionJson.Despliegue.PuertoPreferido) {
    $preferido = [int]$configuracionJson.Despliegue.PuertoPreferido
}
$rutaPuertoActivo = 'C:\ProgramData\ControlMarcaciones\puerto-activo.txt'
if ($configuracionJson -and $configuracionJson.Despliegue -and $configuracionJson.Despliegue.RutaPuertoActivo) {
    $rutaPuertoActivo = [string]$configuracionJson.Despliegue.RutaPuertoActivo
}

$respondio = $false
$estaVivo = $false
$urlViva = $null
foreach ($intento in 1..12) {
    Start-Sleep -Seconds 5
    $puertos = @($preferido)
    if (Test-Path $rutaPuertoActivo) {
        $registrado = 0
        if ([int]::TryParse((Get-Content $rutaPuertoActivo -Raw).Trim(), [ref]$registrado)) {
            $puertos = @($registrado)
        }
    }
    foreach ($puerto in $puertos) {
        try {
            $liveness = Invoke-WebRequest -Uri "http://127.0.0.1:$puerto/vivo" -UseBasicParsing -TimeoutSec 3
            if ($liveness.StatusCode -eq 200 -and $liveness.Content -match 'ControlMarcaciones-OK') {
                $estaVivo = $true
                $urlViva = "http://127.0.0.1:$puerto"
            }
        } catch { }

        try {
            $r = Invoke-WebRequest -Uri "http://127.0.0.1:$puerto/estado" -UseBasicParsing -TimeoutSec 3
            if ($r.StatusCode -eq 200 -and
                $r.Content -match 'ControlMarcaciones-OK' -and
                $r.Content -match 'LISTO') {
                $url = "http://127.0.0.1:$puerto"
                Write-Host "La aplicacion esta operativamente LISTA en $url" -ForegroundColor Green
                if ($puerto -ne $preferido) {
                    Write-Warning "El puerto $preferido estaba ocupado; el sistema quedo en el $puerto."
                }
                $respondio = $true
                break
            }
        } catch { }
    }
    if ($respondio) { break }
    Write-Host "  esperando... ($intento/12)"
}

if ($respondio) {
    # El acceso directo apunta al EJECUTABLE, no a la dirección. Así un clic funciona en
    # los dos casos: si el sistema ya está corriendo abre la pestaña y sale, y si no
    # estuviera corriendo lo levanta y luego abre la pestaña. Un enlace a la URL a secas
    # no haría nada útil el día que la tarea no haya arrancado.
    $enlace = Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'Control de Marcaciones.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $acceso = $shell.CreateShortcut($enlace)
    $acceso.TargetPath = $ejecutable
    $acceso.WorkingDirectory = $carpeta
    $acceso.Description = 'Abre el Control de Marcaciones en el navegador'
    $acceso.Save()
    Write-Host "Acceso directo 'Control de Marcaciones' creado en el escritorio." -ForegroundColor Green
    Write-Host "Un clic ahi abre el sistema en el navegador." -ForegroundColor Green
} else {
    if ($estaVivo) {
        Write-Warning "La aplicacion esta viva en $urlViva, pero /estado no confirmo que este operativamente lista."
        Write-Warning 'Revisa migraciones, compatibilidad del esquema y configuracion bootstrap.'
    } else {
        Write-Warning "La aplicacion no respondio en $url."
    }
    Write-Warning "Ejecuta $ejecutable a mano desde una consola para ver el error."
}
