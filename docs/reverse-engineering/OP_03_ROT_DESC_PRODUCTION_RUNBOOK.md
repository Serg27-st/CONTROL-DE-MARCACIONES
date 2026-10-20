# OP-03 — Runbook productivo ROT-DESC

Fecha de preparación: 2026-10-09  
Estado del documento: **PREPARADO**  
Estado de ejecución: **NO EJECUTADO**  
Migración productiva: **NO APLICADA**

Este documento es el procedimiento operativo final para desplegar
`20261007033130_DescansoProgramadoEnRotaciones`. Prepararlo no autoriza a ejecutar ninguno
de sus comandos sobre producción. La ejecución requiere una ventana aprobada, responsables
identificados y el llenado del acta de la sección 1.

## 0. Alcance, hechos verificados y límites

- Baseline aceptada: **807/807 pruebas verdes, 0 fallos, 0 omitidos**.
- `OP-02B = VALIDADO` y `R-WEB-ROT-DESC = RESUELTO`.
- La migración se validó sobre copia, no sobre producción.
- La aplicación productiva se instala normalmente como tarea programada
  `ControlMarcaciones`, ejecuta `ControlMarcacion.Web.exe` como `SYSTEM` y migra al arrancar.
  En OP-03 la migración se ejecuta deliberadamente por CLI, con la aplicación detenida, y
  solo después de validar la base se permite el arranque.
- Fuera de Development, el código resuelve la base con esta precedencia efectiva:
  argumento CLI `Despliegue:RutaBaseDatos`, variable de entorno de máquina
  `Despliegue__RutaBaseDatos`, `appsettings.<Entorno>.json`, `appsettings.json` y, por último,
  `%ProgramData%\ControlMarcaciones\ControlMarcacion.db`.
- La ruta **esperada**, si no existe override, es
  `C:\ProgramData\ControlMarcaciones\ControlMarcacion.db`. No se acepta como ruta real hasta
  ejecutar la resolución de la sección 3 y guardar la evidencia.
- OP-03A leyó en modo solo lectura el archivo candidato de ProgramData y confirmó que termina en
  `20260923140604_RotacionPorCuadrilla`. La ruta coincide con el default y evidencia anterior,
  pero no pudo ligarse a una tarea/proceso/configuración activos; durante la ventana sigue siendo
  obligatorio resolver la ruta efectiva antes de usarla.
- OP-03A ensayó con éxito las siete migraciones pendientes, desde
  `JustificacionUnicaPorDiaYLicenciaSinGoce` hasta ROT-DESC: exit 0, history 15,
  `integrity_check=ok`, FK 0 y conteos preexistentes preservados.
- La advertencia conocida de SQLite es obligatoria: la operación
  `PRAGMA foreign_keys = 0` de la migración no puede quedar completamente dentro de una
  transacción. El backup restaurable y la parada total de escrituras son controles duros.
- No se usa `Down` como rollback productivo. El rollback es restaurar el backup pre-migración.
- No se ejecutan migración, parada, backup ni smoke productivo durante la preparación de este
  documento.

### Bloqueante de artefacto detectado al preparar OP-03

El paquete actualmente presente en el repositorio **no es elegible**:

| Artefacto | Fecha observada | SHA-256 observado | Decisión |
|---|---|---|---|
| `despliegue/ControlMarcaciones.zip` | 2026-09-24 09:43:16 | `10120A42A30F5716E5F5EFFA8B580D14A9451A1D61D2B50E1DB697883A1AE994` | Rechazar: anterior a ROT-DESC |
| `despliegue/exe/ControlMarcacion.Web.exe` | 2026-09-24 09:42:51 | `8F4C025DED25D2471E3FB6AED9B13607B2B2E4FDD1F9DE7C3F612F080FBFB358` | Rechazar: anterior a ROT-DESC |

OP-03A generó un reemplazo congelado, todavía no aprobado para ventana por los bloqueantes de la
sección 15:

| Artefacto | SHA-256 | Estado |
|---|---|---|
| `artifacts/op03a-rot-desc-20261009/release/ControlMarcacion_ROT_DESC_20261009_op03a01.zip` | `29B8F78BD7041A6C82F649799DD034EBBE29352447DC98E3C7712532DCA4C1DE` | FROZEN; candidato |
| `ControlMarcacion.Web.exe` contenido | `0A5B17D93286A820AAFF710A06379A6F7A59FA31938FE5C5EA52542DEA2EEA2C` | build y arranque conformes |

El smoke autenticado del ZIP queda pendiente de una credencial autorizada. No sustituir esa
prueba con una cuenta creada informalmente.

## 1. Acta de ejecución y segregación de funciones

Completar antes de tocar producción. Un campo vacío equivale a `NO-GO`.

| Campo | Valor |
|---|---|
| Ticket/cambio aprobado | `<ID>` |
| Inicio autorizado (America/Lima) | `<YYYY-MM-DD HH:mm>` |
| Fin autorizado (America/Lima) | `<YYYY-MM-DD HH:mm>` |
| Operador de despliegue | `<nombre>` |
| Validador técnico independiente | `<nombre>` |
| Responsable funcional/payroll | `<nombre>` |
| Responsable que autoriza GO | `<nombre>` |
| Responsable que autoriza rollback | `<nombre>` |
| ZIP de release | `<ruta absoluta>` |
| SHA-256 esperado del ZIP | `<SHA256>` |
| SHA-256 esperado del EXE | `<SHA256>` |
| Commit/tag o manifiesto de fuente | `<identificador>` |
| Raíz de backup fuera de la carpeta productiva | `<ruta absoluta>` |
| Planta piloto | `<id y nombre>` |
| Cuadrilla piloto | `<id y código>` |
| Trabajador piloto | `<id y documento>` |
| Período abierto piloto | `<id y código>` |
| Cuenta COORDINADOR para smoke | `<usuario>` |
| Cuenta ADMINISTRADOR limitada solo a planta piloto | `<usuario>` |

La cuadrilla y el trabajador piloto deben estar aprobados para prueba, aislados de liquidación
real y sin otra programación que se solape. No se inventan datos directamente por SQL para el
smoke funcional. Si no existe un piloto seguro o no puede retirarse/regularizarse luego por el
mecanismo normal de la aplicación, se difiere el despliegue.

## 2. Convenciones y artefactos de evidencia

Todos los comandos siguientes son para una PowerShell elevada en la máquina productiva,
dentro de la ventana autorizada. No copiarlos parcialmente. Las variables se fijan una sola
vez y su salida se conserva.

```powershell
$ErrorActionPreference = 'Stop'
$taskName = 'ControlMarcaciones'
$migration = '20261007033130_DescansoProgramadoEnRotaciones'
$expectedLastApplied = '20260923140604_RotacionPorCuadrilla'
$repoRoot = '<RUTA_ABSOLUTA_DE_LA_FUENTE_CONGELADA>'
$releaseZip = '<RUTA_ABSOLUTA_DEL_ZIP_APROBADO>'
$releaseZipSha256 = '<SHA256_APROBADO>'
$releaseExeSha256 = '<SHA256_APROBADO>'
$approvedSourceManifest = '<RUTA_ABSOLUTA_source-manifest.json>'
$backupRoot = '<RUTA_ABSOLUTA_DE_BACKUP>'
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$evidenceRoot = Join-Path $repoRoot "artifacts\op03-rot-desc-$stamp"
$backupDir = Join-Path $backupRoot "op03-rot-desc-$stamp"
$preDir = Join-Path $evidenceRoot 'pre'
$postDir = Join-Path $evidenceRoot 'post'
$logsDir = Join-Path $evidenceRoot 'logs'
$uiDir = Join-Path $evidenceRoot 'ui'
$stageDir = Join-Path $env:TEMP "op03-rot-desc-$stamp"

New-Item -ItemType Directory -Path $evidenceRoot,$backupDir,$preDir,$postDir,$logsDir,$uiDir,$stageDir | Out-Null
Start-Transcript -LiteralPath (Join-Path $logsDir 'maintenance-transcript.txt') -Force
```

El directorio final obligatorio es `artifacts/op03-rot-desc-<fecha-hora>/`. No contiene
secretos, cookies, contraseñas ni el contenido de `appsettings.Production.json`; de ese archivo
solo se guarda ruta, tamaño, fecha y hash. El backup de aplicación y DB queda en `$backupDir`,
con acceso restringido.

Crear un registro temporal de hitos:

```powershell
$timeline = [System.Collections.Generic.List[object]]::new()
function Add-Hito([string]$nombre, [string]$resultado, [string]$detalle = '') {
    $timeline.Add([pscustomobject]@{
        Momento = (Get-Date).ToString('o')
        Hito = $nombre
        Resultado = $resultado
        Detalle = $detalle
    })
    $timeline | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $evidenceRoot 'timeline.json') -Encoding UTF8
}
Add-Hito 'Inicio mantenimiento' 'INICIADO'
```

## 3. Pre-check

### 3.1 Herramientas, paquete y fuente congelada

```powershell
Set-Location -LiteralPath $repoRoot
Get-Command dotnet,sqlite3,rg,Get-ScheduledTask | Format-List * |
    Out-File -LiteralPath (Join-Path $preDir 'tools.txt') -Encoding utf8

$zipHash = (Get-FileHash -LiteralPath $releaseZip -Algorithm SHA256).Hash
if ($zipHash -ne $releaseZipSha256) { throw 'NO-GO: SHA-256 del ZIP no coincide.' }
if ($zipHash -eq '10120A42A30F5716E5F5EFFA8B580D14A9451A1D61D2B50E1DB697883A1AE994') {
    throw 'NO-GO: se intentó usar el paquete antiguo anterior a ROT-DESC.'
}

$expectedSource = Get-Content -LiteralPath $approvedSourceManifest -Raw | ConvertFrom-Json
$currentPaths = @(@(rg --files src tests tools/despliegue) + 'ControlMarcacion.sln') |
    Sort-Object -Unique | ForEach-Object { $_ -replace '\\','/' }
$sourcePathDiff = Compare-Object @($expectedSource.Path | Sort-Object) $currentPaths
if ($sourcePathDiff) { throw 'NO-GO: cambió el inventario de la fuente congelada.' }
foreach ($entry in $expectedSource) {
    $sourcePath = Join-Path $repoRoot ($entry.Path -replace '/','\')
    if (-not (Test-Path -LiteralPath $sourcePath)) { throw "NO-GO: falta $($entry.Path)." }
    if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ne $entry.SHA256) {
        throw "NO-GO: cambió $($entry.Path)."
    }
}
git rev-parse HEAD | Set-Content -LiteralPath (Join-Path $preDir 'source-commit.txt')
git status --porcelain | Set-Content -LiteralPath (Join-Path $preDir 'source-status.txt')

dotnet build src/ControlMarcacion.Infrastructure/ControlMarcacion.Infrastructure.csproj `
  -c Release --no-restore --nologo 2>&1 |
  Tee-Object -FilePath (Join-Path $preDir 'infrastructure-release-build.txt')
if ($LASTEXITCODE -ne 0) { throw 'NO-GO: no se pudo compilar la fuente aprobada en Release.' }

dotnet ef migrations list `
  --project src/ControlMarcacion.Infrastructure `
  --startup-project src/ControlMarcacion.Infrastructure `
  --configuration Release `
  --no-build 2>&1 | Tee-Object -FilePath (Join-Path $preDir 'ef-migrations-list.txt')
if ($LASTEXITCODE -ne 0) { throw 'NO-GO: no se pudo enumerar migraciones desde la fuente aprobada.' }
if (-not (Select-String -LiteralPath (Join-Path $preDir 'ef-migrations-list.txt') -Pattern $migration -Quiet)) {
    throw 'NO-GO: la fuente aprobada no contiene la migración ROT-DESC.'
}
```

Guardar junto al cambio `RELEASE_MANIFEST.json`, su hash, el source manifest, el log 807/807 y
la evidencia de extracción/arranque de OP-03A. El árbol Git es dirty conocido: la identidad se
demuestra con el manifiesto de cada archivo, no con el commit por sí solo. No regenerar el
paquete durante la ventana.

### 3.2 Resolver la instancia y la base productiva real

Este bloque reproduce la precedencia de configuración relevante. No muestra valores secretos.

```powershell
$task = Get-ScheduledTask -TaskName $taskName -ErrorAction Stop
$taskInfo = Get-ScheduledTaskInfo -TaskName $taskName
$action = @($task.Actions)[0]
$exePath = [Environment]::ExpandEnvironmentVariables([string]$action.Execute)
$workDir = [Environment]::ExpandEnvironmentVariables([string]$action.WorkingDirectory)
$taskArgs = [string]$action.Arguments

if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) { throw "NO-GO: no existe $exePath" }
if (-not (Test-Path -LiteralPath $workDir -PathType Container)) { throw "NO-GO: no existe $workDir" }

$environmentName = [Environment]::GetEnvironmentVariable('DOTNET_ENVIRONMENT','Machine')
if ([string]::IsNullOrWhiteSpace($environmentName)) {
    $environmentName = [Environment]::GetEnvironmentVariable('ASPNETCORE_ENVIRONMENT','Machine')
}
if ([string]::IsNullOrWhiteSpace($environmentName)) { $environmentName = 'Production' }

$baseConfigPath = Join-Path $workDir 'appsettings.json'
$envConfigPath = Join-Path $workDir "appsettings.$environmentName.json"
$baseConfig = if (Test-Path -LiteralPath $baseConfigPath) {
    Get-Content -LiteralPath $baseConfigPath -Raw | ConvertFrom-Json
} else { $null }
$envConfig = if (Test-Path -LiteralPath $envConfigPath) {
    Get-Content -LiteralPath $envConfigPath -Raw | ConvertFrom-Json
} else { $null }

$cliMatch = [regex]::Match(
    $taskArgs,
    '(?:^|\s)--Despliegue:RutaBaseDatos(?:=|\s+)(?:"(?<q>[^"]+)"|(?<u>\S+))')
$cliDb = if ($cliMatch.Success) {
    if ($cliMatch.Groups['q'].Success) { $cliMatch.Groups['q'].Value } else { $cliMatch.Groups['u'].Value }
} else { $null }
$machineDb = [Environment]::GetEnvironmentVariable('Despliegue__RutaBaseDatos','Machine')
$envJsonDb = if ($envConfig) { [string]$envConfig.Despliegue.RutaBaseDatos } else { $null }
$baseJsonDb = if ($baseConfig) { [string]$baseConfig.Despliegue.RutaBaseDatos } else { $null }
$defaultDb = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'ControlMarcaciones\ControlMarcacion.db'

if (-not [string]::IsNullOrWhiteSpace($cliDb)) { $dbSource='CLI'; $dbRaw=$cliDb }
elseif (-not [string]::IsNullOrWhiteSpace($machineDb)) { $dbSource='MachineEnvironment'; $dbRaw=$machineDb }
elseif (-not [string]::IsNullOrWhiteSpace($envJsonDb)) { $dbSource=$envConfigPath; $dbRaw=$envJsonDb }
elseif (-not [string]::IsNullOrWhiteSpace($baseJsonDb)) { $dbSource=$baseConfigPath; $dbRaw=$baseJsonDb }
else { $dbSource='DefaultProgramData'; $dbRaw=$defaultDb }

if (-not [IO.Path]::IsPathRooted($dbRaw)) { $dbRaw = Join-Path $workDir $dbRaw }
$dbPath = [IO.Path]::GetFullPath($dbRaw)
$connection = "Data Source=$dbPath"
if (-not (Test-Path -LiteralPath $dbPath -PathType Leaf)) { throw "NO-GO: DB resuelta no existe: $dbPath" }

$configEvidence = foreach ($path in @($baseConfigPath,$envConfigPath)) {
    if (Test-Path -LiteralPath $path) {
        $item = Get-Item -LiteralPath $path
        [pscustomobject]@{ Path=$item.FullName; Length=$item.Length; LastWriteTimeUtc=$item.LastWriteTimeUtc; SHA256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
    }
}
$target = [pscustomobject]@{
    TaskName=$taskName; TaskState=[string]$task.State; LastTaskResult=$taskInfo.LastTaskResult
    Executable=$exePath; WorkingDirectory=$workDir; Arguments=$taskArgs
    Environment=$environmentName; DatabaseSource=$dbSource; DatabasePath=$dbPath
    ExpectedDefault=$defaultDb; ResolvedAt=(Get-Date).ToString('o')
    ExecutableSHA256=(Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash
    ConfigurationMetadata=$configEvidence
}
$target | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $preDir 'resolved-target.json') -Encoding UTF8
```

No guardar la configuración JSON completa porque contiene credenciales. El operador y el
validador deben revisar `resolved-target.json`, la tarea y el código de resolución. Si existe
más de una fuente no vacía con valores distintos, se registra el desvío y se declara `NO-GO`
aunque la precedencia parezca clara.

Registrar el proceso que atiende `/vivo` antes de detenerlo. `/vivo` identifica el proceso;
`/estado` se reserva para comprobar que además está operativamente listo:

```powershell
$candidateProcesses = Get-CimInstance Win32_Process | Where-Object {
    $_.Name -ieq 'ControlMarcacion.Web.exe' -or
    ($_.Name -ieq 'dotnet.exe' -and $_.CommandLine -like '*ControlMarcacion.Web*') -or
    ($_.Name -ieq 'iisexpress.exe' -and $_.CommandLine -like '*ControlMarcacion*')
} | Select-Object ProcessId,ParentProcessId,Name,ExecutablePath,CommandLine,CreationDate
$candidateProcesses | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $preDir 'processes-before-stop.json') -Encoding UTF8
```

Debe existir una sola instancia productiva coherente con la tarea. Si está disponible
Sysinternals Handle, guardar además `handle64.exe -accepteula $dbPath`; su ausencia no se
sustituye por una suposición. La evidencia mínima aceptable es tarea + PID + working directory
+ resolución de configuración + `/vivo` HTTP 200 con `ControlMarcaciones-OK`; cualquier contradicción es
`NO-GO`.

### 3.3 Ventana y cero escrituras

Antes de detener:

- anunciar el inicio de mantenimiento a usuarios de marcaciones, tareo y cuadrillas;
- bloquear imports, tareo, edición de cuadrillas, marcaciones manuales y cualquier escritura
  Web;
- confirmar que no hay importación ni recálculo en curso;
- registrar la hora y la aceptación del responsable funcional.

Detener la tarea y verificar la ausencia de procesos, sin matar procesos automáticamente:

```powershell
Add-Hito 'Parada solicitada' 'INICIADO'
Stop-ScheduledTask -TaskName $taskName
$deadline = (Get-Date).AddSeconds(60)
do {
    Start-Sleep -Seconds 2
    $state = (Get-ScheduledTask -TaskName $taskName).State
} until ($state -ne 'Running' -or (Get-Date) -ge $deadline)

$remaining = Get-CimInstance Win32_Process | Where-Object {
    $_.Name -ieq 'ControlMarcacion.Web.exe' -or
    ($_.CommandLine -and $_.CommandLine -like "*$workDir*")
}
if ($state -eq 'Running' -or $remaining) {
    $remaining | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $preDir 'processes-after-stop.json')
    throw 'NO-GO: la instancia no se detuvo limpiamente. No usar Stop-Process sin autorización.'
}
Add-Hito 'Aplicación detenida' 'OK'
```

Probar acceso exclusivo; esta transacción no modifica datos:

```powershell
& sqlite3 -bail $dbPath 'PRAGMA busy_timeout=0; BEGIN EXCLUSIVE; ROLLBACK;' 2>&1 |
    Tee-Object -FilePath (Join-Path $preDir 'exclusive-lock.txt')
if ($LASTEXITCODE -ne 0) { throw 'NO-GO: la DB continúa abierta por un escritor.' }
```

### 3.4 WAL/SHM, baseline física e integridad previa

Inventariar sidecars antes del checkpoint, ejecutar un checkpoint limpio y volver a
inventariar. No copiar un WAL activo.

```powershell
$walPath = "$dbPath-wal"
$shmPath = "$dbPath-shm"
Get-Item -LiteralPath $dbPath,$walPath,$shmPath -ErrorAction SilentlyContinue |
    Select-Object FullName,Length,LastWriteTimeUtc |
    Export-Csv -LiteralPath (Join-Path $preDir 'sqlite-files-before-checkpoint.csv') -NoTypeInformation -Encoding UTF8

& sqlite3 -bail $dbPath 'PRAGMA busy_timeout=5000; PRAGMA wal_checkpoint(TRUNCATE);' 2>&1 |
    Tee-Object -FilePath (Join-Path $preDir 'wal-checkpoint.txt')
if ($LASTEXITCODE -ne 0) { throw 'NO-GO: checkpoint WAL falló.' }

Get-Item -LiteralPath $dbPath,$walPath,$shmPath -ErrorAction SilentlyContinue |
    Select-Object FullName,Length,LastWriteTimeUtc |
    Export-Csv -LiteralPath (Join-Path $preDir 'sqlite-files-after-checkpoint.csv') -NoTypeInformation -Encoding UTF8

$dbItem = Get-Item -LiteralPath $dbPath
$dbBaseline = [pscustomobject]@{
    Path=$dbPath; Length=$dbItem.Length; LastWriteTimeUtc=$dbItem.LastWriteTimeUtc
    SHA256=(Get-FileHash -LiteralPath $dbPath -Algorithm SHA256).Hash
    CapturedAt=(Get-Date).ToString('o')
}
$dbBaseline | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $preDir 'database-baseline.json') -Encoding UTF8

& sqlite3 -bail $dbPath 'PRAGMA integrity_check;' |
    Set-Content -LiteralPath (Join-Path $preDir 'integrity-check.txt') -Encoding UTF8
& sqlite3 -bail -header -csv $dbPath 'PRAGMA foreign_key_check;' |
    Set-Content -LiteralPath (Join-Path $preDir 'foreign-key-check.csv') -Encoding UTF8

if ((Get-Content -LiteralPath (Join-Path $preDir 'integrity-check.txt') -Raw).Trim() -ne 'ok') {
    throw 'NO-GO: integrity_check previo distinto de ok.'
}
if ((Get-Item -LiteralPath (Join-Path $preDir 'foreign-key-check.csv')).Length -ne 0) {
    throw 'NO-GO: foreign_key_check previo devolvió filas.'
}
```

## 4. Backup

### 4.1 Backup lógico consistente y copia física de auditoría

`$backupRoot` no puede estar dentro de `$workDir`, `%ProgramData%\ControlMarcaciones` ni
`$repoRoot`. Se prefiere otro volumen o almacenamiento ya protegido. Si solo existe el mismo
volumen, el responsable de continuidad debe aceptar expresamente el riesgo en el acta.

```powershell
$backupDb = Join-Path $backupDir "ControlMarcacion_PRE_ROT_DESC_$stamp.db"
$backupApp = Join-Path $backupDir 'application-before'
$physicalDir = Join-Path $backupDir 'physical-set-after-checkpoint'
New-Item -ItemType Directory -Path $backupApp,$physicalDir | Out-Null

$backupCommand = '.backup "{0}"' -f ($backupDb -replace '\\','/')
& sqlite3 -bail $dbPath $backupCommand 2>&1 |
    Tee-Object -FilePath (Join-Path $logsDir 'sqlite-backup.txt')
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $backupDb)) {
    throw 'NO-GO: no se pudo crear el backup SQLite.'
}

Copy-Item -LiteralPath $dbPath -Destination (Join-Path $physicalDir (Split-Path $dbPath -Leaf))
foreach ($sidecar in @($walPath,$shmPath)) {
    if (Test-Path -LiteralPath $sidecar) {
        Copy-Item -LiteralPath $sidecar -Destination $physicalDir
    }
}

robocopy $workDir $backupApp /E /R:1 /W:1 /NFL /NDL /NP |
    Tee-Object -FilePath (Join-Path $logsDir 'application-backup-robocopy.txt')
if ($LASTEXITCODE -ge 8) { throw "NO-GO: backup de aplicación falló con robocopy $LASTEXITCODE." }
```

La copia `physical-set-after-checkpoint` es evidencia forense. El backup principal restaurable
es `$backupDb`, producido por la API de backup de SQLite y autocontenido.

### 4.2 Verificar el backup

```powershell
& sqlite3 -bail $backupDb 'PRAGMA integrity_check;' |
    Set-Content -LiteralPath (Join-Path $preDir 'backup-integrity-check.txt') -Encoding UTF8
& sqlite3 -bail -header -csv $backupDb 'PRAGMA foreign_key_check;' |
    Set-Content -LiteralPath (Join-Path $preDir 'backup-foreign-key-check.csv') -Encoding UTF8

if ((Get-Content -LiteralPath (Join-Path $preDir 'backup-integrity-check.txt') -Raw).Trim() -ne 'ok') {
    throw 'NO-GO: el backup no supera integrity_check.'
}
if ((Get-Item -LiteralPath (Join-Path $preDir 'backup-foreign-key-check.csv')).Length -ne 0) {
    throw 'NO-GO: el backup tiene violaciones FK.'
}

$backupManifest = Get-ChildItem -LiteralPath $backupDir -File -Recurse | ForEach-Object {
    [pscustomobject]@{
        RelativePath=$_.FullName.Substring($backupDir.Length).TrimStart('\')
        Length=$_.Length; LastWriteTimeUtc=$_.LastWriteTimeUtc
        SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
}
$backupManifest | Export-Csv -LiteralPath (Join-Path $evidenceRoot 'backup-manifest.csv') -NoTypeInformation -Encoding UTF8
Add-Hito 'Backup verificable' 'OK' $backupDb
```

El validador independiente debe abrir `backup-manifest.csv`, ubicar el `.db` principal y
firmar su SHA-256 en el acta antes de continuar.

## 5. Snapshot pre-migración

Usar una exportación determinista, siempre ordenada. El hash contractual de
`tramo_rotacion` cubre solo las columnas que la migración debe preservar.

```powershell
function Export-SqliteCsv([string]$database,[string]$sql,[string]$path) {
    $output = & sqlite3 -bail -header -csv $database $sql 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Consulta SQLite falló: $sql`n$output" }
    [IO.File]::WriteAllLines($path,[string[]]$output,(New-Object Text.UTF8Encoding($false)))
}

$preHistory = Join-Path $preDir 'ef-migrations-history.csv'
$preTramos = Join-Path $preDir 'tramo-rotacion-stable.csv'
Export-SqliteCsv $dbPath 'SELECT "MigrationId","ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' $preHistory
Export-SqliteCsv $dbPath 'SELECT "id","programacion_id","orden","turno_id","dias" FROM "tramo_rotacion" ORDER BY "id";' $preTramos
Export-SqliteCsv $dbPath 'SELECT * FROM "programacion_cuadrilla" ORDER BY "id";' (Join-Path $preDir 'programacion-cuadrilla.csv')
Export-SqliteCsv $dbPath 'SELECT * FROM "cuadrilla" ORDER BY "id";' (Join-Path $preDir 'cuadrilla.csv')
Export-SqliteCsv $dbPath @'
SELECT 'tramo_rotacion' AS tabla, COUNT(*) AS filas FROM tramo_rotacion
UNION ALL SELECT 'programacion_cuadrilla',COUNT(*) FROM programacion_cuadrilla
UNION ALL SELECT 'cuadrilla',COUNT(*) FROM cuadrilla
UNION ALL SELECT 'tareo_diario',COUNT(*) FROM tareo_diario;
'@ (Join-Path $preDir 'counts.csv')

$targetApplied = (& sqlite3 $dbPath "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\"='$migration';").Trim()
if ($targetApplied -ne '0') { throw 'NO-GO: ROT-DESC ya aparece aplicada; no reaplicar.' }
$expectedHistory = @(
    '20260817165631_EsquemaInicial',
    '20260910150246_EquipoMarcadorComoIdentidad',
    '20260921143551_PadronesPlanta18Set',
    '20260921162221_PuestosGenerales',
    '20260921185600_AtribucionDeMarcacionesAlCalcular',
    '20260922205054_GraciaDeSalidaPorTurno',
    '20260922205415_OrigenVerificadoDeLaCarga',
    '20260923140604_RotacionPorCuadrilla'
)
$actualHistory = @(& sqlite3 $dbPath 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";')
if (Compare-Object $expectedHistory $actualHistory) {
    throw 'NO-GO: el historial real difiere del baseline de ocho migraciones ensayado en OP-03A.'
}

$preSnapshotHash = (Get-FileHash -LiteralPath $preTramos -Algorithm SHA256).Hash
[pscustomobject]@{File=$preTramos;SHA256=$preSnapshotHash;CapturedAt=(Get-Date).ToString('o')} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $preDir 'snapshot-hash.json') -Encoding UTF8
Get-ChildItem -LiteralPath $preDir -File | ForEach-Object {
    [pscustomobject]@{Name=$_.Name;Length=$_.Length;SHA256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}
} | Export-Csv -LiteralPath (Join-Path $preDir 'manifest.csv') -NoTypeInformation -Encoding UTF8
Add-Hito 'Snapshot pre-migración' 'OK' $preSnapshotHash
```

## 6. Deploy

### 6.1 Preparar binarios sin arrancar

No ejecutar `instalar.ps1`: registra e inicia la tarea. Extraer el paquete aprobado en staging,
validar su EXE y preservar la configuración productiva existente.

```powershell
Expand-Archive -LiteralPath $releaseZip -DestinationPath $stageDir -Force
$stagedExe = Join-Path $stageDir 'ControlMarcacion.Web.exe'
if (-not (Test-Path -LiteralPath $stagedExe)) { throw 'NO-GO: el ZIP no contiene el EXE.' }
if ((Get-FileHash -LiteralPath $stagedExe -Algorithm SHA256).Hash -ne $releaseExeSha256) {
    throw 'NO-GO: SHA-256 del EXE nuevo no coincide.'
}
if (-not (Test-Path -LiteralPath (Join-Path $stageDir 'wwwroot\js\programacion-cuadrilla.js'))) {
    throw 'NO-GO: el paquete no contiene el script externo corregido de R-WEB.'
}

$productionConfig = Join-Path $workDir "appsettings.$environmentName.json"
$configHashBefore = if (Test-Path -LiteralPath $productionConfig) {
    (Get-FileHash -LiteralPath $productionConfig -Algorithm SHA256).Hash
} else { $null }

robocopy $stageDir $workDir /E /R:1 /W:1 /XF 'appsettings.Production.json' /NFL /NDL /NP |
    Tee-Object -FilePath (Join-Path $logsDir 'application-deploy-robocopy.txt')
if ($LASTEXITCODE -ge 8) { throw "NO-GO: deploy de binarios falló con robocopy $LASTEXITCODE." }

if ($configHashBefore -and (Get-FileHash -LiteralPath $productionConfig -Algorithm SHA256).Hash -ne $configHashBefore) {
    throw 'NO-GO: la configuración productiva cambió durante el deploy.'
}
if ((Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash -ne $releaseExeSha256) {
    throw 'NO-GO: el EXE desplegado no coincide con el aprobado.'
}
Add-Hito 'Binarios desplegados' 'OK' $releaseExeSha256
```

Revisar que `resolved-target.json` siga describiendo la misma tarea, working directory,
entorno y ruta de DB. No arrancar todavía: el EXE migra automáticamente al iniciar.

### 6.2 Aplicar la migración por el mecanismo validado

El comando se ejecuta desde la fuente congelada que produjo el paquete. La salida combinada y
el código de retorno son evidencia. La advertencia `PRAGMA foreign_keys = 0` se conserva en el
log; no se silencia ni se interpreta como transaccionalidad total.

```powershell
Set-Location -LiteralPath $repoRoot
$migrationLog = Join-Path $logsDir 'ef-database-update.txt'
$migrationStart = Get-Date
& dotnet ef database update $migration `
  --connection $connection `
  --project src/ControlMarcacion.Infrastructure `
  --startup-project src/ControlMarcacion.Infrastructure `
  --configuration Release `
  --no-build 2>&1 | Tee-Object -FilePath $migrationLog
$migrationExit = $LASTEXITCODE
$migrationEnd = Get-Date
[pscustomobject]@{
    Command="dotnet ef database update $migration --connection <ruta registrada> --project src/ControlMarcacion.Infrastructure --startup-project src/ControlMarcacion.Infrastructure --configuration Release --no-build"
    StartedAt=$migrationStart.ToString('o'); FinishedAt=$migrationEnd.ToString('o'); ExitCode=$migrationExit
    KnownWarning='PRAGMA foreign_keys = 0 no puede ejecutarse completamente dentro de una transacción'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logsDir 'migration-execution.json') -Encoding UTF8

if ($migrationExit -ne 0) {
    Add-Hito 'Migración' 'FALLO' "Exit $migrationExit"
    throw 'NO-GO: no arrancar la aplicación; ejecutar rollback por restore.'
}
Add-Hito 'Migración' 'OK'
```

## 7. Validation post-migración

### 7.1 Integridad, FK, historial y backfill

```powershell
& sqlite3 -bail $dbPath 'PRAGMA integrity_check;' |
    Set-Content -LiteralPath (Join-Path $postDir 'integrity-check.txt') -Encoding UTF8
& sqlite3 -bail -header -csv $dbPath 'PRAGMA foreign_key_check;' |
    Set-Content -LiteralPath (Join-Path $postDir 'foreign-key-check.csv') -Encoding UTF8
if ((Get-Content -LiteralPath (Join-Path $postDir 'integrity-check.txt') -Raw).Trim() -ne 'ok') {
    throw 'NO-GO: integrity_check post-migración falló; restaurar backup.'
}
if ((Get-Item -LiteralPath (Join-Path $postDir 'foreign-key-check.csv')).Length -ne 0) {
    throw 'NO-GO: foreign_key_check post-migración devolvió filas; restaurar backup.'
}

Export-SqliteCsv $dbPath 'SELECT "MigrationId","ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' (Join-Path $postDir 'ef-migrations-history.csv')
Export-SqliteCsv $dbPath 'SELECT "tipo",COUNT(*) AS filas FROM "tramo_rotacion" GROUP BY "tipo" ORDER BY "tipo";' (Join-Path $postDir 'tramos-by-type.csv')
Export-SqliteCsv $dbPath @'
SELECT 'tramo_rotacion' AS tabla, COUNT(*) AS filas FROM tramo_rotacion
UNION ALL SELECT 'programacion_cuadrilla',COUNT(*) FROM programacion_cuadrilla
UNION ALL SELECT 'cuadrilla',COUNT(*) FROM cuadrilla
UNION ALL SELECT 'tareo_diario',COUNT(*) FROM tareo_diario;
'@ (Join-Path $postDir 'counts-before-smoke.csv')

$historyCount = (& sqlite3 $dbPath "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\"='$migration';").Trim()
$restCount = (& sqlite3 $dbPath "SELECT COUNT(*) FROM \"tramo_rotacion\" WHERE \"tipo\"='Descanso';").Trim()
$badWorkCount = (& sqlite3 $dbPath "SELECT COUNT(*) FROM \"tramo_rotacion\" WHERE \"tipo\"<>'Trabajo' OR \"turno_id\" IS NULL;").Trim()
if ($historyCount -ne '1') { throw 'NO-GO: la migración no aparece exactamente una vez.' }
if ($restCount -ne '0') { throw 'NO-GO: el backfill creó descansos históricos.' }
if ($badWorkCount -ne '0') { throw 'NO-GO: algún tramo histórico no quedó Trabajo con TurnoId.' }
```

### 7.2 Comparación contractual del snapshot

```powershell
$postTramos = Join-Path $postDir 'tramo-rotacion-stable.csv'
Export-SqliteCsv $dbPath 'SELECT "id","programacion_id","orden","turno_id","dias" FROM "tramo_rotacion" ORDER BY "id";' $postTramos
$postSnapshotHash = (Get-FileHash -LiteralPath $postTramos -Algorithm SHA256).Hash
$snapshotDiff = Compare-Object (Get-Content -LiteralPath $preTramos) (Get-Content -LiteralPath $postTramos)
$snapshotDiff | Out-File -LiteralPath (Join-Path $postDir 'snapshot-diff.txt') -Encoding utf8
if ($postSnapshotHash -ne $preSnapshotHash -or $snapshotDiff) {
    throw 'NO-GO: cambió Id/ProgramacionId/Orden/TurnoId/Dias; restaurar backup.'
}
```

Además, `counts.csv` y `counts-before-smoke.csv` deben coincidir para las cuatro tablas. Antes
del smoke, la única diferencia de datos permitida es el historial de migración y el backfill
estructural de columnas nuevas.

### 7.3 Esquema y probes descartables de constraints

```powershell
& sqlite3 -bail $dbPath "SELECT sql FROM sqlite_master WHERE type='table' AND name IN ('tramo_rotacion','tareo_diario') ORDER BY name;" |
    Set-Content -LiteralPath (Join-Path $postDir 'schema-rot-desc.sql') -Encoding UTF8
$schemaText = Get-Content -LiteralPath (Join-Path $postDir 'schema-rot-desc.sql') -Raw
foreach ($constraint in @('CK_tramo_rotacion_tipo_turno','CK_tramo_rotacion_descanso_un_dia','CK_tramo_rotacion_dias_positivos','CK_tramo_rotacion_orden_positivo')) {
    if ($schemaText -notmatch [regex]::Escape($constraint)) { throw "NO-GO: falta $constraint" }
}
```

Para los probes debe existir al menos una planta activa y un turno activo. Cada conexión abre
una transacción propia; el caso válido hace `ROLLBACK`, y en un caso inválido el cierre de la
conexión revierte lo no confirmado. Después de cada caso se comprueba que no quedó el código
temporal.

```powershell
if ((& sqlite3 $dbPath 'SELECT COUNT(*) FROM planta WHERE activo=1;').Trim() -eq '0') { throw 'NO-GO: no hay planta para probes.' }
if ((& sqlite3 $dbPath 'SELECT COUNT(*) FROM turno WHERE activo=1;').Trim() -eq '0') { throw 'NO-GO: no hay turno para probes.' }
$probeCode = "OP03PROBE$stamp"
$probeSetup = @"
PRAGMA foreign_keys=ON;
BEGIN IMMEDIATE;
INSERT INTO cuadrilla(codigo,nombre,planta_id,creado_en,creado_por,activo)
SELECT '$probeCode','OP03 PROBE',id,CURRENT_TIMESTAMP,'OP03',1 FROM planta WHERE activo=1 ORDER BY id LIMIT 1;
INSERT INTO programacion_cuadrilla(cuadrilla_id,fecha_inicio,fecha_fin,fecha_ancla,creado_en,creado_por,activo)
SELECT id,'2099-01-01','2099-01-10','2099-01-01',CURRENT_TIMESTAMP,'OP03',1 FROM cuadrilla WHERE codigo='$probeCode';
"@
$programId = "(SELECT id FROM programacion_cuadrilla WHERE cuadrilla_id=(SELECT id FROM cuadrilla WHERE codigo='$probeCode'))"
$turnId = '(SELECT id FROM turno WHERE activo=1 ORDER BY id LIMIT 1)'

function Invoke-ConstraintProbe([string]$name,[string]$insert,[bool]$shouldPass) {
    $sql = $probeSetup + "`n" + $insert + "`nROLLBACK;"
    $log = Join-Path $postDir "probe-$name.txt"
    $result = & sqlite3 -bail $dbPath $sql 2>&1
    $exit = $LASTEXITCODE
    [IO.File]::WriteAllLines($log,[string[]]$result,(New-Object Text.UTF8Encoding($false)))
    if ($shouldPass -and $exit -ne 0) { throw "NO-GO: probe válido $name fue rechazado." }
    if (-not $shouldPass -and $exit -eq 0) { throw "NO-GO: probe inválido $name fue aceptado." }
    if ((& sqlite3 $dbPath "SELECT COUNT(*) FROM cuadrilla WHERE codigo='$probeCode';").Trim() -ne '0') {
        throw "NO-GO: probe $name dejó residuos; restaurar backup."
    }
}

Invoke-ConstraintProbe 'descanso-valido' "INSERT INTO tramo_rotacion(activo,creado_en,creado_por,dias,orden,programacion_id,tipo,turno_id) VALUES(1,CURRENT_TIMESTAMP,'OP03',1,1,$programId,'Descanso',NULL);" $true
Invoke-ConstraintProbe 'trabajo-sin-turno' "INSERT INTO tramo_rotacion(activo,creado_en,creado_por,dias,orden,programacion_id,tipo,turno_id) VALUES(1,CURRENT_TIMESTAMP,'OP03',1,1,$programId,'Trabajo',NULL);" $false
Invoke-ConstraintProbe 'descanso-con-turno' "INSERT INTO tramo_rotacion(activo,creado_en,creado_por,dias,orden,programacion_id,tipo,turno_id) VALUES(1,CURRENT_TIMESTAMP,'OP03',1,1,$programId,'Descanso',$turnId);" $false
Invoke-ConstraintProbe 'descanso-dos-dias' "INSERT INTO tramo_rotacion(activo,creado_en,creado_por,dias,orden,programacion_id,tipo,turno_id) VALUES(1,CURRENT_TIMESTAMP,'OP03',2,1,$programId,'Descanso',NULL);" $false
Add-Hito 'Validación DB post-migración' 'OK'
```

No continuar si cualquiera de estos controles falla. Ejecutar la sección 12 antes de arrancar.

## 8. Arranque controlado

```powershell
$startAt = Get-Date
Start-ScheduledTask -TaskName $taskName
$ports = @(5000,5080,5090,8080,8090)
if (Test-Path -LiteralPath $envConfigPath) {
    $urls = [string]$envConfig.Urls
    if (-not [string]::IsNullOrWhiteSpace($urls)) {
        $preferredPort = ([Uri]($urls.Split(';')[0])).Port
        $ports = @($preferredPort) + @($ports | Where-Object { $_ -ne $preferredPort })
    }
}
$appUrl = $null
$appViva = $null
foreach ($attempt in 1..12) {
    Start-Sleep -Seconds 5
    foreach ($port in $ports) {
        try {
            $liveness = Invoke-WebRequest -Uri "http://localhost:$port/vivo" -UseBasicParsing -TimeoutSec 3
            if ($liveness.StatusCode -eq 200 -and $liveness.Content -match 'ControlMarcaciones-OK') {
                $appViva = "http://localhost:$port"
            }
            $response = Invoke-WebRequest -Uri "http://localhost:$port/estado" -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -eq 200 -and
                $response.Content -match 'ControlMarcaciones-OK' -and
                $response.Content -match 'LISTO') { $appUrl="http://localhost:$port"; break }
        } catch { }
    }
    if ($appUrl) { break }
}
if (-not $appUrl) {
    if ($appViva) { throw "NO-GO: proceso vivo en $appViva, pero aplicación NO_LISTA; detener y restaurar backup." }
    throw 'NO-GO: la aplicación no respondió; detener y restaurar backup.'
}

$taskAfter = Get-ScheduledTask -TaskName $taskName
$taskInfoAfter = Get-ScheduledTaskInfo -TaskName $taskName
$processAfter = Get-CimInstance Win32_Process | Where-Object { $_.Name -ieq 'ControlMarcacion.Web.exe' } |
    Select-Object ProcessId,ParentProcessId,Name,ExecutablePath,CommandLine,CreationDate
[pscustomobject]@{
    StartedAt=$startAt.ToString('o'); Url=$appUrl; TaskState=[string]$taskAfter.State
    LastTaskResult=$taskInfoAfter.LastTaskResult; Processes=$processAfter
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $logsDir 'startup.json') -Encoding UTF8

Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-TaskScheduler/Operational';StartTime=$startAt} -ErrorAction SilentlyContinue |
    Where-Object { $_.Message -like "*$taskName*" } |
    Select-Object TimeCreated,Id,LevelDisplayName,Message |
    Export-Csv -LiteralPath (Join-Path $logsDir 'task-scheduler-startup.csv') -NoTypeInformation -Encoding UTF8
Add-Hito 'Arranque controlado' 'OK' $appUrl
```

La tarea programada actual no persiste `stdout/stderr`. La evidencia de arranque es estado de
tarea, PID, SHA del EXE, `/vivo`, `/estado`, eventos del Task Scheduler y ausencia de errores en el
smoke. No cambiar la definición de la tarea durante OP-03 para introducir logging improvisado.

## 9. Smoke autenticado

Toda evidencia visual debe guardarse en `$uiDir`, sin contraseñas, cookies ni tokens. Abrir las
herramientas del navegador y conservar consola y red para detectar CSP, JavaScript, 4xx/5xx y
peticiones inesperadas.

### 9.1 Login y UI

1. Autenticar con la cuenta `COORDINADOR` registrada en el acta: esperado `login OK`.
2. Abrir la cuadrilla piloto y el formulario de programación.
3. Seleccionar `Trabajo`: Turno habilitado y Días editable.
4. Seleccionar `Descanso`: Turno limpio/deshabilitado y Días fijado en 1.
5. Confirmar cero errores CSP/JavaScript/500 en consola y red.

### 9.2 POST válido y reapertura

Crear mediante la UI, sobre la cuadrilla piloto y una vigencia aprobada, esta secuencia:

```text
DIU ×2 → DESCANSO ×1 → NOC ×2
```

Esperado: POST exitoso, tres tramos exactos y GET posterior con la misma secuencia. Registrar el
ID de programación, capturas y esta consulta de evidencia:

```sql
SELECT p.id AS programacion_id,p.cuadrilla_id,p.fecha_inicio,p.fecha_fin,p.fecha_ancla,
       t.id AS tramo_id,t.orden,t.tipo,t.turno_id,t.dias
FROM programacion_cuadrilla p
JOIN tramo_rotacion t ON t.programacion_id=p.id
WHERE p.id=<PROGRAMACION_PILOTO_ID>
ORDER BY t.orden;
```

Exportarla a `ui/pilot-program.csv` con `Export-SqliteCsv`.

### 9.3 Requests inválidos

Usar una petición autenticada real al mismo endpoint, con antiforgery válido, pero sin alterar
el JavaScript desplegado. Puede repetirse el POST desde DevTools cambiando exclusivamente el
payload del formulario. Nunca registrar el token ni la cookie.

- `Descanso + TurnoId`: respuesta con error de validación, sin nueva programación/tramo.
- `Descanso + Dias=2`: respuesta con error de validación, sin nueva programación/tramo.

Capturar conteos de `programacion_cuadrilla` y `tramo_rotacion` inmediatamente antes y después
de cada intento. Deben ser idénticos. Una redirección exitosa o cualquier persistencia es
`NO-GO`.

### 9.4 Reportes y resolución de la rotación

1. En Reportes/Excepciones, el día piloto de descanso debe mostrar
   `DESCANSO PROGRAMADO`, no `SIN TURNO`.
2. Resolver y registrar:

| Día | Esperado |
|---|---|
| D1 | DIU |
| D2 | DIU |
| D3 | DescansoProgramado |
| D4 | NOC |
| D5 | NOC |
| D6 | SinProgramacion |

### 9.5 Casos funcionales controlados

Ejecutar solo sobre el trabajador, período y planta piloto aprobados, usando imports/marcaciones
por mecanismos normales y trazables. No insertar marcas ni tareo directamente por SQL.

| Caso | Datos | Esperado |
|---|---|---|
| NOC anterior | NOC 19:00 → 05:22; día siguiente Descanso | 05:22 ya reclamada, no abre trabajo en descanso |
| Descanso trabajado | 07:00 → 17:00 | 600 HE100; 0 ordinarias; 0 HE25; 0 HE35 |
| D+1 | 20:00 → 02:00 sin conflicto siguiente | 360 HE100 |
| Domingo | domingo + descanso + trabajo | HE100 una sola vez |
| Feriado ordinario | feriado + descanso + trabajo | clasificación `FeriadoTrabajado`; minutos físicos únicos |
| 1 de mayo | 1 mayo + descanso | clasificación especial conservada; no validar ni desbloquear valorización económica pendiente |

Los escenarios pueden ejecutarse en fechas piloto separadas dentro del período. Para cada uno,
guardar marcas de entrada, tareo resultante, reporte y consulta SQL. No reutilizar minutos de un
caso en otro. Si el calendario real del período no permite domingo, feriado o 1 de mayo sin
contaminar datos operativos, esos casos se aceptan con la evidencia OP-02B/automatizada y se
registran como `NO EJECUTADO EN PRODUCCIÓN POR SEGURIDAD`; no se falsifica el calendario.

### 9.6 Vigencia

Modificar por UI la programación piloto con una nueva vigencia aprobada. El tareo previamente
calculado debe pasar de `vigente` a `desactualizado`. Guardar el antes/después. No modificar una
programación real ni una vigencia cerrada de nómina.

## 10. Recalculation

El login smoke se hace como `COORDINADOR`, pero el recálculo **no**: el código concede a
`COORDINADOR` alcance sin restricción y recalcula todos los trabajadores activos del período.
Para acotar la operación a la planta piloto, usar una cuenta `ADMINISTRADOR` cuya asignación en
`usuario_planta` contenga exactamente esa planta.

### 10.1 Demostrar alcance antes de recalcular

```powershell
$adminUserId = '<ID_USUARIO_ADMIN_PILOTO>'
$pilotPlantId = '<ID_PLANTA_PILOTO>'
Export-SqliteCsv $dbPath "SELECT usuario_id,planta_id FROM usuario_planta WHERE usuario_id='$adminUserId' ORDER BY planta_id;" (Join-Path $uiDir 'recalc-user-scope.csv')
```

El archivo debe tener exactamente una fila y su `planta_id` debe ser `$pilotPlantId`. Si no,
`NO-GO`; no usar COORDINADOR como sustituto.

### 10.2 Primera ejecución

1. Autenticar con el `ADMINISTRADOR` acotado.
2. Confirmar que el período piloto está abierto.
3. Exportar a `recalc-before-1.csv` el tareo de trabajadores de esa planta y período, ordenado
   por trabajador y fecha; registrar conteo y SHA-256.
4. Ejecutar `Asistencia/Recalcular` solo para `<PERIODO_PILOTO_ID>`.
5. Registrar mensaje UI, trabajadores procesados, días creados/actualizados, errores, duración,
   conteo posterior y `recalc-after-1.csv`.
6. Verificar que no cambió tareo de otras plantas ni períodos mediante conteos/hashes de
   control.

La consulta de snapshot debe unir `tareo_diario → trabajador` y filtrar simultáneamente por
`periodo_id=<PERIODO_PILOTO_ID>` y `trabajador.planta_id=<ID_PLANTA_PILOTO>`. Usar `SELECT *`
ordenado solo como evidencia, no como mecanismo de escritura.

### 10.3 Segunda ejecución e idempotencia

Sin modificar programación, marcaciones, asignaciones ni justificaciones:

1. repetir exactamente el recálculo con la misma cuenta y período;
2. exportar `recalc-after-2.csv` con la misma consulta y orden;
3. comparar SHA-256 y `Compare-Object` entre `after-1` y `after-2`;
4. consultar duplicados por la clave de tareo `(trabajador_id,fecha)` dentro del período;
5. exigir cero duplicados, cero diferencias funcionales y cero filas fuera del alcance.

Una segunda ejecución que altera resultados sin cambio de entradas es `NO-GO`.

## 11. Go / No-Go

### GO

Reabrir operación solo si todos son verdaderos:

- paquete nuevo, fuente congelada y hashes aprobados;
- DB real resuelta sin desvío y una sola instancia identificada;
- cero escritores durante checkpoint, backup, migración y checks;
- backup restaurable con `integrity_check=ok`, cero FK y SHA registrado;
- migración `exit=0`, warning conocido preservado y ningún otro error;
- `integrity_check=ok`, cero FK y migración exactamente una vez;
- todos los tramos históricos `Trabajo`, cero descansos históricos y snapshot contractual idéntico;
- cuatro probes DB conformes y sin residuos;
- app inicia, `/vivo` identifica el proceso y `/estado` confirma `LISTO`; tarea y PID son coherentes;
- login COORDINADOR, UI, POST válido, reapertura, requests inválidos y reporte conformes;
- rotación piloto y casos seguros conformes, sin duplicar minutos;
- vigencia/desactualización conforme;
- recálculo acotado e idempotente, sin duplicados ni cambios fuera de alcance;
- cero errores críticos EF, SQLite, CSP, JavaScript o HTTP 500;
- responsable técnico y funcional firman GO.

```powershell
Add-Hito 'Decisión final' 'GO' '<responsables>'
Stop-Transcript
```

Solo después de GO se comunica fin de mantenimiento y se habilitan imports, tareo, edición y
escrituras Web.

### NO-GO

Cualquiera de estos eventos obliga a mantener cerrada la operación, detener la app y restaurar:

- ruta/configuración/proceso ambiguos o múltiples instancias;
- paquete/hashes/fuente no aprobados;
- escritor activo, checkpoint fallido o backup no verificable;
- `integrity_check != ok` o una fila de `foreign_key_check`;
- migración ausente, duplicada, incompleta o con salida distinta de cero;
- snapshot histórico alterado, pérdida de datos o backfill distinto del esperado;
- constraint inválido aceptado o válido rechazado;
- app/login/POST/reportes no funcionales;
- errores críticos EF/SQLite, CSP/JavaScript que impidan el flujo, o HTTP 500;
- recálculo sin alcance demostrable, no idempotente, duplicado o fuera de planta/período;
- falta de aprobación técnica/funcional.

No intentar reparar manualmente el esquema productivo durante la ventana. Registrar el fallo y
seguir el restore.

## 12. Rollback por restauración

### 12.1 Procedimiento principal

1. Declarar `NO-GO`, mantener el bloqueo de usuarios y anotar hora/causa.
2. Detener la tarea y comprobar cero procesos/escritores como en 3.3.
3. Copiar la DB fallida y sus sidecars a `$evidenceRoot\failed-state` para diagnóstico; no
   sobreescribir el backup.
4. Verificar otra vez el SHA-256 de `$backupDb` contra `backup-manifest.csv`.
5. Retirar la DB fallida y sidecars a una carpeta de cuarentena; no borrarlos.
6. Restaurar el `.db` autocontenido al `$dbPath` exacto.
7. No restaurar WAL/SHM de otro instante. Solo si el backup aprobado fue excepcionalmente un
   conjunto físico atómico, restaurar juntos su `.db`, `.db-wal` y `.db-shm`, todos con hashes
   del mismo manifiesto. La ruta normal de OP-03 usa `.backup` y no restaura sidecars.
8. Validar integridad, FK, historial y conteos antes de arrancar.
9. Restaurar binarios pre-deploy y preservar la configuración productiva.
10. Arrancar la tarea y ejecutar smoke mínimo de solo lectura.

Comandos exactos de reemplazo recuperable:

```powershell
Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
$failedDir = Join-Path $evidenceRoot 'failed-state'
New-Item -ItemType Directory -Path $failedDir -Force | Out-Null

foreach ($path in @($dbPath,$walPath,$shmPath)) {
    if (Test-Path -LiteralPath $path) {
        Move-Item -LiteralPath $path -Destination $failedDir
    }
}

$expectedBackupHash = (Import-Csv -LiteralPath (Join-Path $evidenceRoot 'backup-manifest.csv') |
    Where-Object { (Join-Path $backupDir $_.RelativePath) -eq $backupDb }).SHA256
if ((Get-FileHash -LiteralPath $backupDb -Algorithm SHA256).Hash -ne $expectedBackupHash) {
    throw 'STOP: el backup cambió; no restaurar y escalar a continuidad.'
}
Copy-Item -LiteralPath $backupDb -Destination $dbPath

& sqlite3 -bail $dbPath 'PRAGMA integrity_check;' |
    Set-Content -LiteralPath (Join-Path $evidenceRoot 'rollback-integrity-check.txt') -Encoding UTF8
& sqlite3 -bail -header -csv $dbPath 'PRAGMA foreign_key_check;' |
    Set-Content -LiteralPath (Join-Path $evidenceRoot 'rollback-foreign-key-check.csv') -Encoding UTF8
$rollbackHistory = (& sqlite3 $dbPath "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\"='$migration';").Trim()
if ((Get-Content -LiteralPath (Join-Path $evidenceRoot 'rollback-integrity-check.txt') -Raw).Trim() -ne 'ok') { throw 'Restore inválido: integrity_check.' }
if ((Get-Item -LiteralPath (Join-Path $evidenceRoot 'rollback-foreign-key-check.csv')).Length -ne 0) { throw 'Restore inválido: FK.' }
if ($rollbackHistory -ne '0') { throw 'Restore inválido: ROT-DESC aún aparece en history.' }

robocopy $backupApp $workDir /MIR /R:1 /W:1 /NFL /NDL /NP |
    Tee-Object -FilePath (Join-Path $logsDir 'application-rollback-robocopy.txt')
if ($LASTEXITCODE -ge 8) { throw "Rollback de aplicación falló con robocopy $LASTEXITCODE." }
Add-Hito 'Rollback restore' 'OK' $backupDb
```

`Move-Item` se usa para conservar el estado fallido. Antes de ejecutarlo, el operador debe
confirmar visualmente que `$dbPath`, `$walPath`, `$shmPath` y `$failedDir` coinciden con
`resolved-target.json` y el artifact root de esta ejecución.

### 12.2 Smoke post-rollback

Tras los checks:

1. iniciar `ControlMarcaciones`;
2. exigir `/vivo` con HTTP 200 y luego `/estado` con HTTP 200, `ControlMarcaciones-OK` y `LISTO`;
3. login;
4. consulta de trabajadores;
5. consulta de marcaciones;
6. consulta de tareo existente;
7. reporte de solo lectura;
8. confirmar conteos contra `pre/counts.csv` y migración ROT-DESC ausente.

No reabrir escrituras si falla algún punto. No ejecutar `Down`: después de crear datos
`Tipo=Descanso`, el esquema anterior no puede representarlos de forma segura.

## 13. Evidence y cierre

Conservar como mínimo:

```text
artifacts/op03-rot-desc-<fecha-hora>/
├── timeline.json
├── backup-manifest.csv
├── pre/
│   ├── resolved-target.json
│   ├── processes-before-stop.json
│   ├── database-baseline.json
│   ├── sqlite-files-before-checkpoint.csv
│   ├── sqlite-files-after-checkpoint.csv
│   ├── integrity-check.txt
│   ├── foreign-key-check.csv
│   ├── backup-integrity-check.txt
│   ├── backup-foreign-key-check.csv
│   ├── ef-migrations-history.csv
│   ├── tramo-rotacion-stable.csv
│   ├── programacion-cuadrilla.csv
│   ├── cuadrilla.csv
│   ├── counts.csv
│   └── manifest.csv
├── post/
│   ├── integrity-check.txt
│   ├── foreign-key-check.csv
│   ├── ef-migrations-history.csv
│   ├── tramos-by-type.csv
│   ├── tramo-rotacion-stable.csv
│   ├── snapshot-diff.txt
│   ├── schema-rot-desc.sql
│   ├── probe-*.txt
│   └── counts-before-smoke.csv
├── logs/
│   ├── maintenance-transcript.txt
│   ├── ef-database-update.txt
│   ├── migration-execution.json
│   ├── startup.json
│   └── task-scheduler-startup.csv
└── ui/
    ├── pilot-program.csv
    ├── recalc-user-scope.csv
    ├── recalc-before-1.csv
    ├── recalc-after-1.csv
    ├── recalc-after-2.csv
    └── capturas y exportes sin secretos
```

Agregar `final-manifest.csv` con tamaño y SHA-256 de todo el árbol de evidencia. No limpiar ni
sobrescribir estos artefactos. El backup se retiene según la política de continuidad y nunca se
borra como parte de este runbook.

```powershell
Get-ChildItem -LiteralPath $evidenceRoot -File -Recurse | Where-Object { $_.Name -ne 'final-manifest.csv' } |
    ForEach-Object {
        [pscustomobject]@{
            RelativePath=$_.FullName.Substring($evidenceRoot.Length).TrimStart('\')
            Length=$_.Length; LastWriteTimeUtc=$_.LastWriteTimeUtc
            SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    } | Export-Csv -LiteralPath (Join-Path $evidenceRoot 'final-manifest.csv') -NoTypeInformation -Encoding UTF8
```

## 14. Ficha final de ejecución

```text
OP-03 — Runbook productivo ROT-DESC

BASE PRODUCTIVA
Ruta esperada: C:\ProgramData\ControlMarcaciones\ControlMarcacion.db, solo sin override
Cómo verificar ruta real: sección 3.2; resolved-target.json; tarea + entorno + JSON + proceso

PRE-CHECK
Aplicación detenida: <SÍ/NO + hora>
Escrituras bloqueadas: <SÍ/NO + responsable>
Integrity: <ok/resultado>
FK: <0 filas/resultado>
Migration history: <target ausente; previous presente/resultado>
Snapshot: <ruta + SHA-256>

BACKUP
Ruta: <backupDb>
Archivos: <manifiesto>
SHA: <SHA-256>
Validación: <integrity ok + FK 0>

MIGRACIÓN
Comando: dotnet ef database update 20261007033130_DescansoProgramadoEnRotaciones ...
Migración: 20261007033130_DescansoProgramadoEnRotaciones
Warning conocido: PRAGMA foreign_keys=0 no completamente transaccional
Criterio éxito: exit=0 + checks post completos

POST-MIGRACIÓN
Integrity: <ok/resultado>
FK: <0 filas/resultado>
History: <exactamente 1/resultado>
Backfill: <Trabajo=N; Descanso=0>
Snapshot: <idéntico/diferencia>

ARRANQUE
Proceso: <tarea + PID + EXE SHA>
Logs: <rutas + errores críticos>

SMOKE
Login: <resultado>
UI Trabajo: <resultado>
UI Descanso: <resultado>
POST válido: <resultado>
POST inválido: <dos resultados; sin persistencia>
Reporte: <resultado>
Rotación: <D1..D6>
NOC anterior: <resultado>
D+1: <resultado>
Domingo: <resultado>
Feriado: <resultado>
1 mayo: <clasificación/aceptado con evidencia previa por seguridad>

VIGENCIA: <vigente → desactualizado>
RECÁLCULO: <planta/período; ejecución 1; ejecución 2; idempotencia>

GO CRITERIA: <CUMPLE/NO CUMPLE + firmas>

NO-GO CRITERIA: <NINGUNO/DETALLE>

ROLLBACK
Procedimiento: restauración, no Down
Backup: <ruta + SHA>
Checks: <integrity/FK/history/conteos>
Smoke: <resultado>

ARTEFACTOS: artifacts/op03-rot-desc-<fecha-hora>/

OP-03 RUNBOOK:
PREPARADO

OP-03 EJECUTADO:
NO

MIGRACIÓN PRODUCTIVA:
NO

¿Listo para programar ventana productiva?:
NO, hasta cerrar los bloqueantes de la sección 15

Bloqueantes:
<lista o NINGUNO>

Siguiente paso:
<acción aprobada>
```

## 15. Estado al cerrar esta intervención

```text
OP-03 RUNBOOK = PREPARADO
OP-03 EJECUTADO = NO
MIGRACIÓN PRODUCTIVA = NO
```

Bloqueantes actuales para programar la ventana:

1. confirmar desde la instancia/tarea/configuración real que la DB de ProgramData inspeccionada
   por OP-03A es la ruta efectiva;
2. cerrar el smoke autenticado del ZIP congelado con una credencial autorizada;
3. aprobar por negocio PlantaId 3 y confirmar el período 2, cuyo código `2026-09` y nombre
   `octubre` son inconsistentes;
4. designar personas concretas para Release, Backup, Migración, DB validation, Smoke, Negocio,
   GO/NO-GO y Rollback;
5. designar y probar el destino final de backup; el candidato local pasó write/read/delete y
   restore, pero comparte el volumen C: y `F:` no fue accesible;
6. disponer de una cuenta `ADMINISTRADOR` limitada exactamente a PlantaId 3 y probar sobre copia
   el flujo autenticado permitido/fuera de alcance. `COORDINADOR` no sirve porque es global.

Siguiente paso: cerrar esos seis prerrequisitos, someter el acta a aprobación y recién entonces
programar la ventana. Este documento no ejecuta producción ni autoriza la migración.
