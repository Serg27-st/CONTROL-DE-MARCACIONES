[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $InputPath,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,

    [string] $ExpectedSha256
)

$ErrorActionPreference = 'Stop'
$Invariant = [System.Globalization.CultureInfo]::InvariantCulture

function Read-UInt32LittleEndian {
    param([byte[]] $Bytes, [int] $Offset)

    if (-not [BitConverter]::IsLittleEndian) {
        $copy = [byte[]]$Bytes[$Offset..($Offset + 3)]
        [Array]::Reverse($copy)
        return [BitConverter]::ToUInt32($copy, 0)
    }

    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Convert-ZkTimestamp {
    param([uint32] $Value)

    $work = [uint64]$Value
    $second = [int]($work % 60); $work = [math]::Floor($work / 60)
    $minute = [int]($work % 60); $work = [math]::Floor($work / 60)
    $hour = [int]($work % 24); $work = [math]::Floor($work / 24)
    $day = [int]($work % 31) + 1; $work = [math]::Floor($work / 31)
    $month = [int]($work % 12) + 1; $work = [math]::Floor($work / 12)
    $year = [int]$work + 2000

    try {
        return [datetime]::new($year, $month, $day, $hour, $minute, $second, [DateTimeKind]::Unspecified)
    }
    catch {
        return $null
    }
}

function Convert-ToHex {
    param([byte[]] $Bytes, [int] $Offset, [int] $Length)

    return [Convert]::ToHexString([byte[]]$Bytes[$Offset..($Offset + $Length - 1)])
}

function Get-Distribution {
    param([object[]] $Rows, [string] $Property)

    return @(
        $Rows |
            Group-Object -Property $Property |
            Sort-Object -Property Name |
            ForEach-Object {
                [ordered]@{
                    Valor = [string]$_.Name
                    Cantidad = $_.Count
                }
            }
    )
}

$resolvedInput = (Resolve-Path -LiteralPath $InputPath).Path
$bytes = [IO.File]::ReadAllBytes($resolvedInput)
$sha256 = (Get-FileHash -LiteralPath $resolvedInput -Algorithm SHA256).Hash.ToUpperInvariant()

if ($ExpectedSha256 -and $sha256 -ne $ExpectedSha256.ToUpperInvariant()) {
    throw "SHA-256 inesperado. Esperado: $ExpectedSha256. Real: $sha256."
}

$cursor = 0
$seriesLength = [int]$bytes[$cursor++]
$series = [Text.Encoding]::ASCII.GetString($bytes, $cursor, $seriesLength)
$cursor += $seriesLength
$modelLength = [int]$bytes[$cursor++]
$model = [Text.Encoding]::ASCII.GetString($bytes, $cursor, $modelLength)
$cursor += $modelLength
$recordSize = [int](Read-UInt32LittleEndian $bytes $cursor)
$cursor += 4
$fieldCount = [int]$bytes[$cursor++]

$fieldDescriptors = @()
$fieldWidths = @()
for ($fieldIndex = 0; $fieldIndex -lt $fieldCount; $fieldIndex++) {
    $descriptorType = [int]$bytes[$cursor]
    $width = [int]$bytes[$cursor + 1]
    $reserved = [int]$bytes[$cursor + 2]
    $fieldDescriptors += [ordered]@{
        Indice = $fieldIndex
        Tipo = ('0x{0:X2}' -f $descriptorType)
        Ancho = $width
        Reservado = ('0x{0:X2}' -f $reserved)
    }
    $fieldWidths += $width
    $cursor += 3
}

$headerSize = $cursor
$declaredBytes = ($fieldWidths | Measure-Object -Sum).Sum
$slack = $recordSize - $declaredBytes
$bodyBytes = $bytes.Length - $headerSize
$physicalRecordCount = [math]::Floor($bodyBytes / $recordSize)
$footerOrRemainderBytes = $bodyBytes % $recordSize

if ($recordSize -le 0 -or $fieldCount -le 0 -or $slack -lt 0 -or $physicalRecordCount -lt 1) {
    throw 'La cabecera no describe un cuerpo de registros auditable.'
}

$timestampRelativeOffset = $declaredBytes - 4
$minimumPlausible = [math]::Max(1, [math]::Ceiling($physicalRecordCount * 0.9))
$layoutCandidates = @()
$selectedDisplacement = $null

foreach ($alignedOnly in @($true, $false)) {
    $passCandidates = @()
    for ($displacement = 0; $displacement -le $slack; $displacement++) {
        $absoluteTimestampInRecord = $displacement + $timestampRelativeOffset
        if ($alignedOnly -and ($absoluteTimestampInRecord % 4) -ne 0) {
            continue
        }

        $valid = 0
        for ($recordIndex = 0; $recordIndex -lt $physicalRecordCount; $recordIndex++) {
            $position = $headerSize + ($recordIndex * $recordSize) + $absoluteTimestampInRecord
            $rawTimestamp = Read-UInt32LittleEndian $bytes $position
            $date = Convert-ZkTimestamp $rawTimestamp
            if ($null -ne $date -and $date.Year -ge 2015 -and $date -le [datetime]::Now.AddYears(1)) {
                $valid++
            }
        }

        $candidate = [pscustomobject]@{
            Pasada = if ($alignedOnly) { 'ALINEADA' } else { 'SIN_RESTRICCION' }
            Desplazamiento = $displacement
            TimestampEnRegistro = $absoluteTimestampInRecord
            FechasPlausibles = $valid
            CumpleUmbral = $valid -ge $minimumPlausible
        }
        $layoutCandidates += $candidate
        $passCandidates += $candidate
    }

    $best = $passCandidates |
        Where-Object CumpleUmbral |
        Sort-Object @{ Expression = 'FechasPlausibles'; Descending = $true }, @{ Expression = 'Desplazamiento'; Descending = $false } |
        Select-Object -First 1

    if ($null -ne $best) {
        $selectedDisplacement = [int]$best.Desplazamiento
        break
    }
}

if ($null -eq $selectedDisplacement) {
    throw 'No se encontró un layout con al menos 90 % de timestamps plausibles.'
}

$fieldRelativeOffsets = @()
$relative = $selectedDisplacement
foreach ($width in $fieldWidths) {
    $fieldRelativeOffsets += $relative
    $relative += $width
}

$maximumAgeBoundary = [datetime]::Now.AddDays(-400)
$baseRows = @()
for ($recordIndex = 0; $recordIndex -lt $physicalRecordCount; $recordIndex++) {
    $number = $recordIndex + 1
    $recordOffset = $headerSize + ($recordIndex * $recordSize)
    $rawRecord = Convert-ToHex $bytes $recordOffset $recordSize

    $idOffset = $recordOffset + $fieldRelativeOffsets[0]
    $id = Read-UInt32LittleEndian $bytes $idOffset
    $idBytes = Convert-ToHex $bytes $idOffset $fieldWidths[0]
    $auxiliary = Read-UInt32LittleEndian $bytes ($recordOffset + $fieldRelativeOffsets[1])
    $verification = [int]$bytes[$recordOffset + $fieldRelativeOffsets[2]]
    $type = [int]$bytes[$recordOffset + $fieldRelativeOffsets[3]]
    $eventLow = [int]$bytes[$recordOffset + $fieldRelativeOffsets[4]]
    $eventHigh = [int]$bytes[$recordOffset + $fieldRelativeOffsets[5]]
    $eventCode = $eventLow + ($eventHigh * 256)
    $timestampOffset = $recordOffset + $fieldRelativeOffsets[6]
    $timestamp = Read-UInt32LittleEndian $bytes $timestampOffset
    $timestampBytes = Convert-ToHex $bytes $timestampOffset 4
    $date = Convert-ZkTimestamp $timestamp

    $timestampState = if ($timestamp -eq 0) {
        'CERO'
    }
    elseif ($null -eq $date) {
        'IMPOSIBLE'
    }
    elseif ($date.Year -lt 2015 -or $date -gt [datetime]::Now.AddYears(1)) {
        'FUERA_RANGO_LAYOUT'
    }
    else {
        'VALIDO'
    }

    $status = 'ACEPTADO'
    $reason = ''
    if ($null -eq $date) {
        $status = 'RECHAZADO'
        $reason = 'La fecha y hora del registro no es una fecha real.'
    }
    elseif ($id -eq 0) {
        $status = 'RECHAZADO'
        $reason = 'El registro no tiene identificador de marcador: no corresponde a ninguna persona y el equipo lo escribió para sí mismo.'
    }
    elseif ($date -lt $maximumAgeBoundary) {
        $status = 'RECHAZADO'
        $reason = ('La fecha {0:dd/MM/yyyy} supera la antigüedad máxima de 400 días. Verifique la fecha configurada en el marcador.' -f $date)
    }

    $classification = if ($status -eq 'ACEPTADO') {
        'MARCACION_ACEPTADA'
    }
    elseif ($null -eq $date) {
        'TIMESTAMP_INVALIDO'
    }
    elseif ($id -eq 0) {
        'EVENTO_NO_MARCACION'
    }
    else {
        'NO_DETERMINADO'
    }

    $baseRows += [pscustomobject][ordered]@{
        IndiceFisico = $number
        Offset = $recordOffset
        RawHex = $rawRecord
        TamanoRegistro = $recordSize
        Primeros8Bytes = $rawRecord.Substring(0, 16)
        EstadoLector = $status
        IdRaw = $idBytes
        IdDecodificado = $id
        CampoAuxiliar = $auxiliary
        ModoVerificacion = $verification
        Tipo = $type
        CodigoEvento = $eventCode
        CodigoEventoLow = $eventLow
        CodigoEventoHigh = $eventHigh
        TimestampRaw = $timestampBytes
        TimestampDecimal = $timestamp
        FechaHoraSiDecodificable = if ($null -eq $date) { '' } else { $date.ToString('yyyy-MM-dd HH:mm:ss', $Invariant) }
        EstadoTimestamp = $timestampState
        MotivoRechazo = $reason
        Clasificacion = $classification
        LayoutIntentado = ('REG{0}/BLOQUE@{1}/ID@{2}/TS@{3}/LE' -f $recordSize, $selectedDisplacement, $fieldRelativeOffsets[0], $fieldRelativeOffsets[6])
        PatronEstructural = ('REG{0};PAD0={1};ID={2};AUX={3};VER={4};TIPO={5};EVENTO={6};PADFINAL={7}' -f $recordSize, $rawRecord.Substring(0, 16), $idBytes, $auxiliary, $verification, $type, $eventCode, $rawRecord.Substring($rawRecord.Length - 8, 8))
    }
}

$rows = @()
for ($i = 0; $i -lt $baseRows.Count; $i++) {
    $row = $baseRows[$i]
    $previous = if ($i -gt 0) { $baseRows[$i - 1] } else { $null }
    $next = if ($i + 1 -lt $baseRows.Count) { $baseRows[$i + 1] } else { $null }

    $rows += [pscustomobject][ordered]@{
        IndiceFisico = $row.IndiceFisico
        Offset = $row.Offset
        RawHex = $row.RawHex
        TamanoRegistro = $row.TamanoRegistro
        Primeros8Bytes = $row.Primeros8Bytes
        EstadoLector = $row.EstadoLector
        IdRaw = $row.IdRaw
        IdDecodificado = $row.IdDecodificado
        CampoAuxiliar = $row.CampoAuxiliar
        ModoVerificacion = $row.ModoVerificacion
        Tipo = $row.Tipo
        CodigoEvento = $row.CodigoEvento
        CodigoEventoLow = $row.CodigoEventoLow
        CodigoEventoHigh = $row.CodigoEventoHigh
        TimestampRaw = $row.TimestampRaw
        TimestampDecimal = $row.TimestampDecimal
        FechaHoraSiDecodificable = $row.FechaHoraSiDecodificable
        EstadoTimestamp = $row.EstadoTimestamp
        MotivoRechazo = $row.MotivoRechazo
        Clasificacion = $row.Clasificacion
        LayoutIntentado = $row.LayoutIntentado
        PatronEstructural = $row.PatronEstructural
        AnteriorIndice = if ($null -eq $previous) { '' } else { $previous.IndiceFisico }
        AnteriorId = if ($null -eq $previous) { '' } else { $previous.IdDecodificado }
        AnteriorEvento = if ($null -eq $previous) { '' } else { $previous.CodigoEvento }
        AnteriorFechaHora = if ($null -eq $previous) { '' } else { $previous.FechaHoraSiDecodificable }
        SiguienteIndice = if ($null -eq $next) { '' } else { $next.IndiceFisico }
        SiguienteId = if ($null -eq $next) { '' } else { $next.IdDecodificado }
        SiguienteEvento = if ($null -eq $next) { '' } else { $next.CodigoEvento }
        SiguienteFechaHora = if ($null -eq $next) { '' } else { $next.FechaHoraSiDecodificable }
    }
}

$accepted = @($rows | Where-Object EstadoLector -eq 'ACEPTADO')
$rejected = @($rows | Where-Object EstadoLector -eq 'RECHAZADO')

$middleStart = [math]::Max(0, [math]::Floor(($accepted.Count - 10) / 2))
$sampleAccepted = [ordered]@{
    CriterioMuestraMedia = "Diez aceptados consecutivos en el orden físico, desde el índice cero floor((N-10)/2) = $middleStart."
    Primeros10 = @($accepted | Select-Object -First 10)
    Medios10 = @($accepted | Select-Object -Skip $middleStart -First 10)
    Ultimos10 = @($accepted | Select-Object -Last 10)
}

$rejectedWithAcceptedPrevious = @($rejected | Where-Object { $_.AnteriorId -and [uint32]$_.AnteriorId -ne 0 }).Count
$rejectedWithAcceptedNext = @($rejected | Where-Object { $_.SiguienteId -and [uint32]$_.SiguienteId -ne 0 }).Count
$consecutiveRejected = 0
for ($i = 1; $i -lt $baseRows.Count; $i++) {
    if ($baseRows[$i - 1].EstadoLector -eq 'RECHAZADO' -and $baseRows[$i].EstadoLector -eq 'RECHAZADO') {
        $consecutiveRejected++
    }
}

$summary = [ordered]@{
    GeneradoUtc = [datetime]::UtcNow.ToString('o', $Invariant)
    Herramienta = 'tools/diagnostico-bkdat-rejects.ps1'
    Fuente = [ordered]@{
        Ruta = $resolvedInput
        TamanoBytes = $bytes.Length
        SHA256 = $sha256
        SHA256Esperado = $ExpectedSha256
        SHA256Verificado = (-not $ExpectedSha256) -or ($sha256 -eq $ExpectedSha256.ToUpperInvariant())
    }
    Cabecera = [ordered]@{
        Serie = $series
        Modelo = $model
        TamanoCabecera = $headerSize
        TamanoRegistro = $recordSize
        CantidadCampos = $fieldCount
        Descriptores = $fieldDescriptors
        BytesDeclarados = $declaredBytes
        HolguraPorRegistro = $slack
    }
    Layout = [ordered]@{
        DesplazamientoSeleccionado = $selectedDisplacement
        OffsetsRelativosCampos = $fieldRelativeOffsets
        Formato = ('REG{0}/BLOQUE@{1}/ID@{2}/TS@{3}/LE' -f $recordSize, $selectedDisplacement, $fieldRelativeOffsets[0], $fieldRelativeOffsets[6])
        UmbralFechasPlausibles = $minimumPlausible
        Candidatos = $layoutCandidates
    }
    Cuerpo = [ordered]@{
        Bytes = $bodyBytes
        RegistrosFisicosCompletos = $physicalRecordCount
        BytesFooterORemanente = $footerOrRemainderBytes
        Formula = "$bodyBytes / $recordSize = $physicalRecordCount, remainder $footerOrRemainderBytes"
    }
    Resultado = [ordered]@{
        TotalFisicos = $rows.Count
        Aceptados = $accepted.Count
        Rechazados = $rejected.Count
        IdsAceptadosDistintos = @($accepted.IdDecodificado | Sort-Object -Unique).Count
        FechaMinAceptada = ($accepted | Select-Object -First 1).FechaHoraSiDecodificable
        FechaMaxAceptada = ($accepted | Select-Object -Last 1).FechaHoraSiDecodificable
        MotivosRechazo = Get-Distribution $rejected 'MotivoRechazo'
        ClasificacionesRechazo = Get-Distribution $rejected 'Clasificacion'
        EstadosTimestampRechazados = Get-Distribution $rejected 'EstadoTimestamp'
        Primeros8BytesRechazados = Get-Distribution $rejected 'Primeros8Bytes'
        IdsRawRechazados = Get-Distribution $rejected 'IdRaw'
        CamposAuxiliaresRechazados = Get-Distribution $rejected 'CampoAuxiliar'
        TamanosRechazados = Get-Distribution $rejected 'TamanoRegistro'
        PatronesEstructuralesRechazados = Get-Distribution $rejected 'PatronEstructural'
        TimestampsRawRechazados = Get-Distribution $rejected 'TimestampRaw'
        EventosAceptados = Get-Distribution $accepted 'CodigoEvento'
        EventosRechazados = Get-Distribution $rejected 'CodigoEvento'
        TiposAceptados = Get-Distribution $accepted 'Tipo'
        TiposRechazados = Get-Distribution $rejected 'Tipo'
        ModosVerificacionAceptados = Get-Distribution $accepted 'ModoVerificacion'
        ModosVerificacionRechazados = Get-Distribution $rejected 'ModoVerificacion'
        RechazadosConAnteriorAceptado = $rejectedWithAcceptedPrevious
        RechazadosConSiguienteAceptado = $rejectedWithAcceptedNext
        ParesConsecutivosDeRechazados = $consecutiveRejected
    }
    MuestraAceptados = $sampleAccepted
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$rows | Export-Csv -LiteralPath (Join-Path $OutputDirectory 'all-records.csv') -NoTypeInformation -Encoding utf8
$accepted | Export-Csv -LiteralPath (Join-Path $OutputDirectory 'accepted.csv') -NoTypeInformation -Encoding utf8
$rejected | Export-Csv -LiteralPath (Join-Path $OutputDirectory 'rejected.csv') -NoTypeInformation -Encoding utf8
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding utf8

[pscustomobject]@{
    Input = $resolvedInput
    SHA256 = $sha256
    HeaderBytes = $headerSize
    RecordSize = $recordSize
    PhysicalRecords = $rows.Count
    Accepted = $accepted.Count
    Rejected = $rejected.Count
    FooterOrRemainderBytes = $footerOrRemainderBytes
    OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
} | Format-List
