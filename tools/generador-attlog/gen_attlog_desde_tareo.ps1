<#
    Genera un archivo attlog simulado del ZKTeco G3 a partir del tareo real de un mes.

    Por qué existe además de gen_attlog.py: aquel inventa una dotación de 50 personas desde
    cero. Este parte del "RESUMEN FIN DE MES" que entrega GCA, así que el archivo resultante
    reproduce la plantilla real —cuántos trabajaron, cuántos días cada uno, cuántas horas,
    quién rota a nocturno y en qué fechas exactas estuvo de vacaciones o con descanso
    médico—. Sirve para probar la ingesta y el motor contra un volumen y una forma realistas
    sin usar marcaciones reales de personas.

    ENTRADAS (las produce la extracción del Excel; no se versionan, llevan DNI):
      -Tareo      CSV con: dni;area;ceco;dias;horas;noct;ext25;ext35;ext100;faltas;dm;vac;
                  lsg;lcg;permisos;tardanza;cese;ingreso
                  (ingreso y cese acotan la vigencia: fuera de ella la marca no se puede
                  atribuir a nadie y la ingesta la rechaza)
      -Ausencias  CSV con: tipo;doc;inicio;fin   (fechas dd/MM/yyyy)

    SALIDAS:
      -Salida     el .txt con formato ZKTeco
      -Mapa       CSV dni;id_marcador, necesario para asignar el ID en Personal

    FORMATO DEL ZKTECO, verificado byte a byte contra el archivo real de producción:
      · seis campos separados por TABULADOR: id, fecha-hora, 1, 0, 1, 0
      · id alineado a la DERECHA en 9 caracteres, rellenado con espacios
      · fecha-hora "yyyy-MM-dd HH:mm:ss"
      · salto de línea LF, no CRLF, y la última línea NO lleva salto
      · orden cronológico estricto en todo el archivo, mezclando a todos los trabajadores
      · la cuarta columna (tipo de marcación) es SIEMPRE 0: el equipo no distingue entrada
        de salida, y de eso depende que el motor tenga que inferirlo

    Es determinista: la misma semilla produce el mismo archivo byte a byte.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Tareo,
    [Parameter(Mandatory)] [string] $Ausencias,
    [Parameter(Mandatory)] [string] $Salida,
    [Parameter(Mandatory)] [string] $SalidaMapa,
    [int] $Semilla = 20260731,
    [datetime] $Inicio = '2026-07-01',
    [datetime] $Fin = '2026-07-31'
)

$ErrorActionPreference = 'Stop'
$azar = New-Object System.Random($Semilla)

# Feriados de Fiestas Patrias 2026: martes 28 y miércoles 29.
$feriados = @([datetime]'2026-07-28', [datetime]'2026-07-29')

# El [string[]] NO es decorativo. Sin él, PowerShell pasa un object[] donde se espera
# string[], falla la resolución de sobrecarga y acaba llamando a la versión de UN solo
# formato con los tres concatenados en una cadena. TryParseExact devuelve false SIEMPRE, la
# función devuelve null en silencio y los filtros de ingreso y cese dejan de aplicarse sin
# que nada falle a la vista.
function ParseFecha([string] $texto) {
    if ([string]::IsNullOrWhiteSpace($texto)) { return $null }
    $f = [datetime]::MinValue
    $formatos = [string[]] @('dd/MM/yyyy', 'd/M/yyyy', 'yyyy-MM-dd')
    if ([datetime]::TryParseExact($texto.Trim(), $formatos, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref] $f)) { return $f }
    return $null
}

# --- Ausencias con fecha exacta, indexadas por documento -------------------
$bloqueados = @{}
foreach ($a in (Import-Csv $Ausencias -Delimiter ';')) {
    $desde = ParseFecha $a.inicio
    $hasta = ParseFecha $a.fin
    if (-not $desde -or -not $hasta) { continue }
    $doc = $a.doc.Trim()
    if (-not $bloqueados.ContainsKey($doc)) { $bloqueados[$doc] = New-Object 'System.Collections.Generic.HashSet[string]' }
    for ($d = $desde; $d -le $hasta; $d = $d.AddDays(1)) { [void] $bloqueados[$doc].Add($d.ToString('yyyy-MM-dd')) }
}

# --- Códigos de marcador ---------------------------------------------------
# Los cuatro últimos llevan ceros a la izquierda a propósito: es el caso que rompe todo
# sistema que trate el ID como número, y el archivo real de GCA lo contiene.
$filas = @(Import-Csv $Tareo -Delimiter ';')
$codigos = @{}
for ($i = 0; $i -lt $filas.Count; $i++) {
    $n = $i + 1
    $codigos[$filas[$i].dni] = if ($n -gt ($filas.Count - 4)) { '{0:0000}' -f $n } else { "$n" }
}

$marcaciones = New-Object System.Collections.Generic.List[object]

function Marca([string] $id, [datetime] $momento) {
    $marcaciones.Add([pscustomobject]@{ Id = $id; Momento = $momento })
}

foreach ($t in $filas) {
    $dias = [int][double] $t.dias
    if ($dias -le 0) { continue }

    $id = $codigos[$t.dni]
    $horasOrdinarias = [double] $t.horas
    $extras = [double] $t.ext25 + [double] $t.ext35 + [double] $t.ext100
    $presenciaTotal = $horasOrdinarias + $extras
    $horasPorDia = if ($dias -gt 0) { $presenciaTotal / $dias } else { 8 }
    if ($horasPorDia -lt 4) { $horasPorDia = 4 }
    if ($horasPorDia -gt 14) { $horasPorDia = 14 }

    # Proporción de la jornada que cayó en franja nocturna: decide si la persona es de
    # noche, de día, o rota a mitad de mes.
    $proporcionNocturna = if ($presenciaTotal -gt 0) { [double] $t.noct / $presenciaTotal } else { 0 }

    $cese = ParseFecha $t.cese

    # La fecha de ingreso importa tanto como la de cese: el ID de marcador se asigna al
    # trabajador CON VIGENCIA desde que entra, así que una marca anterior a su ingreso no
    # se puede atribuir a nadie y la ingesta la rechaza. Sin esta línea, los contratados a
    # mitad de mes generaban marcas huérfanas.
    $ingreso = ParseFecha $t.ingreso

    # --- Elegir qué días trabajó ------------------------------------------
    $candidatos = New-Object System.Collections.Generic.List[datetime]
    for ($d = $Inicio; $d -le $Fin; $d = $d.AddDays(1)) {
        if ($cese -and $d -gt $cese) { continue }
        if ($ingreso -and $d -lt $ingreso) { continue }
        if ($bloqueados.ContainsKey($t.dni) -and $bloqueados[$t.dni].Contains($d.ToString('yyyy-MM-dd'))) { continue }
        $candidatos.Add($d)
    }
    if ($candidatos.Count -eq 0) { continue }

    # Se descartan domingos primero y feriados después, solo si sobran días: quien tiene
    # horas al 100 % sí trabajó en día no laborable y debe conservarlos.
    $tieneCien = [double] $t.ext100 -gt 0
    $ordenados = $candidatos | Sort-Object @{
        Expression = {
            $penalizacion = 0
            if ($_.DayOfWeek -eq [DayOfWeek]::Sunday -and -not $tieneCien) { $penalizacion += 2 }
            if ($feriados -contains $_ -and -not $tieneCien) { $penalizacion += 2 }
            $penalizacion
        }
    }, @{ Expression = { $azar.Next() } }

    $trabajados = @($ordenados | Select-Object -First ([Math]::Min($dias, $ordenados.Count)) | Sort-Object)

    # --- Emitir las marcas de cada día ------------------------------------
    $tardanzasPendientes = [int][double] $t.tardanza
    $mitad = $Inicio.AddDays(15)

    foreach ($dia in $trabajados) {
        # Turno del día: fijo si la persona es claramente de día o de noche; si está en
        # medio, rota a mitad de mes, que es como rotan los obreros en planta.
        $esNocturno =
            if ($proporcionNocturna -ge 0.7) { $true }
            elseif ($proporcionNocturna -le 0.05) { $false }
            else { $dia -ge $mitad }

        # Un turno nocturno que empieza el día del cese sale a las 07:00 del día siguiente,
        # cuando la asignación del marcador ya cerró: esa marca de salida no se puede
        # atribuir a nadie y la ingesta la rechaza. No se programa esa última noche.
        #
        # ⚠️ Es una limitación real del sistema, no del archivo: la atribución mira la fecha
        # natural de la marca y no la jornada a la que pertenece. Aquí se esquiva para que
        # la simulación no genere ruido, pero con marcaciones reales el caso existe.
        if ($esNocturno -and $cese -and $dia.Date -eq $cese.Date) { continue }

        $horaBase = if ($esNocturno) { 19 } else { 7 }

        # Casi todos marcan unos minutos antes de su hora. Llegar antes no genera
        # sobretiempo —lo dice el D.S. 007-2002-TR— pero el marcador igual lo registra.
        $desfase = $azar.Next(-14, 3)

        if ($tardanzasPendientes -gt 0 -and $azar.NextDouble() -lt 0.35) {
            $desfase = $azar.Next(6, 40)
            $tardanzasPendientes--
        }

        $entrada = $dia.Date.AddHours($horaBase).AddMinutes($desfase).AddSeconds($azar.Next(0, 60))

        $duracion = $horasPorDia + ($azar.NextDouble() - 0.5)
        $horaSalida = $entrada.AddHours($duracion).AddMinutes($azar.Next(-6, 7))

        Marca $id $entrada

        # Marca impar: el trabajador olvidó marcar la salida. Pasa de verdad y es el caso
        # que obliga al módulo de justificaciones a existir.
        if ($azar.NextDouble() -lt 0.012) { continue }

        Marca $id $horaSalida

        # Doble pasada por el torniquete: dos marcas del mismo ID con segundos de
        # diferencia. La ingesta debe colapsarlas, no contarlas dos veces.
        if ($azar.NextDouble() -lt 0.010) {
            Marca $id $entrada.AddSeconds($azar.Next(4, 40))
        }
    }
}

# --- Escritura -------------------------------------------------------------
$orden = $marcaciones | Sort-Object Momento, Id
$sb = New-Object System.Text.StringBuilder
$primera = $true
foreach ($m in $orden) {
    if (-not $primera) { [void] $sb.Append("`n") }
    $primera = $false
    [void] $sb.Append(('{0,9}' -f $m.Id))
    [void] $sb.Append("`t")
    [void] $sb.Append($m.Momento.ToString('yyyy-MM-dd HH:mm:ss'))
    [void] $sb.Append("`t1`t0`t1`t0")
}

[System.IO.File]::WriteAllText($Salida, $sb.ToString(), (New-Object System.Text.ASCIIEncoding))

$sbMapa = New-Object System.Text.StringBuilder
[void] $sbMapa.AppendLine('numero_documento;id_marcador')
foreach ($t in $filas) { [void] $sbMapa.AppendLine("$($t.dni);$($codigos[$t.dni])") }
[System.IO.File]::WriteAllText($SalidaMapa, $sbMapa.ToString(), (New-Object System.Text.UTF8Encoding($false)))

[pscustomobject]@{
    Marcaciones      = $orden.Count
    Trabajadores     = ($orden | Select-Object -ExpandProperty Id -Unique).Count
    PrimeraMarca     = $orden[0].Momento
    UltimaMarca      = $orden[-1].Momento
    ArchivoBytes     = (Get-Item $Salida).Length
}
