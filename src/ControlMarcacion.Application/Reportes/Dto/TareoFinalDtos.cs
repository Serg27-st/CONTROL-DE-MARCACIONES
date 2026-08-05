namespace ControlMarcacion.Application.Reportes.Dto;

/// <summary>
/// El tareo final tal como lo entrega GCA: un libro con tres hojas.
/// </summary>
/// <remarks>
/// Réplica del archivo modelo <c>TAREO NOVIEMBRE 2024_PLANTA 1.xls</c>. El original tiene
/// además una hoja <c>Plantilla</c> con la grilla día por día, y la hoja <c>Tareo</c> es una
/// proyección por <c>VLOOKUP</c> contra ella. Aquí no hace falta: el motor ya calculó los
/// totales, así que se escriben como valores.
///
/// La estructura de columnas está documentada en <c>docs/04-formato-tareo-final.md</c>.
/// </remarks>
public sealed record TareoFinalDto(
    string RazonSocial,
    string NombreMes,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    IReadOnlyList<FilaTareoFinalDto> Tareo,
    IReadOnlyList<FilaDescansoMedicoDto> DescansosMedicos,
    IReadOnlyList<FilaVacacionesDto> Vacaciones,
    IReadOnlyList<FilaLicenciaDto> Licencias);

/// <summary>
/// Una fila de la hoja <c>Tareo</c>: un trabajador con sus totales del periodo.
/// </summary>
/// <remarks>
/// Los tiempos viajan en <b>minutos enteros</b> y se convierten a horas decimales recién al
/// escribir la celda, que es como los espera planilla.
///
/// Los tramos se reparten de forma <b>disjunta</b> para que nada se cuente dos veces:
/// <list type="bullet">
///   <item>Ordinarias, 25 % y 35 % cuentan solo los días laborables.</item>
///   <item>El 100 % cuenta todo lo trabajado en día no laborable —domingo o feriado—, que
///   por eso no aparece en los tres tramos anteriores.</item>
///   <item>La nocturnidad es una <b>dimensión aparte</b>: los minutos nocturnos son un
///   subconjunto de los tramos anteriores, no una quinta categoría que se sume.</item>
/// </list>
/// ⚠️ Este reparto está pendiente de confirmación con GCA: en el archivo modelo hay filas
/// donde el 25 % nocturno supera al 25 % diurno, lo que sugiere que ellos los tratan como
/// categorías excluyentes. Ver <c>docs/04-formato-tareo-final.md</c>, §5.
/// </remarks>
public sealed record FilaTareoFinalDto
{
    public required int Numero { get; init; }

    public required string Dni { get; init; }

    public required string ApellidosYNombres { get; init; }

    public required string Planta { get; init; }

    /// <summary>Centro de costo. Vacío hasta que exista el catálogo CECO de Intradevco.</summary>
    public string? Ceco { get; init; }

    public string? DescripcionCeco { get; init; }

    public string? CentroCosto { get; init; }

    public required DateOnly FechaIngreso { get; init; }

    public DateOnly? FechaCese { get; init; }

    public string? Observacion { get; init; }

    // ---------- Movimiento de personal, en días ----------

    public int DiasFalta { get; init; }

    public int DiasSuspension { get; init; }

    public int DiasFaltaJustificada { get; init; }

    /// <summary>Faltas más faltas justificadas, como la columna <c>TOTAL FALTAS</c>.</summary>
    public int DiasTotalFaltas => DiasFalta + DiasFaltaJustificada;

    public int DiasDescansoMedico { get; init; }

    public int DiasVacaciones { get; init; }

    public int DiasLicenciaSinGoce { get; init; }

    public int DiasLicenciaConGoce { get; init; }

    public int DiasLicenciaPaternidad { get; init; }

    public int DiasSubsidioMaternidad { get; init; }

    public int DiasSubsidioEnfermedad { get; init; }

    public int DiasPermiso { get; init; }

    public int MinutosTardanza { get; init; }

    // ---------- Resumen de horas ----------

    public int MinutosOrdinarios { get; init; }

    public int MinutosExtra25 { get; init; }

    public int MinutosExtra35 { get; init; }

    /// <summary>Todo lo trabajado en domingo o feriado: va al 100 %.</summary>
    public int MinutosNoLaborable { get; init; }

    /// <summary>Base de la bonificación nocturna: minutos en franja 22:00–06:00.</summary>
    public int MinutosNocturnos { get; init; }

    public int MinutosExtra25Nocturnos { get; init; }

    public int MinutosExtra35Nocturnos { get; init; }

    public int MinutosNoLaborableNocturnos { get; init; }

    public decimal HorasTardanza => AHoras(MinutosTardanza);

    public decimal HorasOrdinarias => AHoras(MinutosOrdinarios);

    public decimal HorasExtra25 => AHoras(MinutosExtra25);

    public decimal HorasExtra35 => AHoras(MinutosExtra35);

    public decimal HorasNoLaborable => AHoras(MinutosNoLaborable);

    public decimal HorasNocturnas => AHoras(MinutosNocturnos);

    public decimal HorasExtra25Nocturnas => AHoras(MinutosExtra25Nocturnos);

    public decimal HorasExtra35Nocturnas => AHoras(MinutosExtra35Nocturnos);

    public decimal HorasNoLaborableNocturnas => AHoras(MinutosNoLaborableNocturnos);

    private static decimal AHoras(int minutos) => Math.Round(minutos / 60m, 2);
}

/// <summary>Una fila de la hoja <c>Descanso Medico</c>.</summary>
/// <remarks>
/// Contingencia, diagnóstico y centro de atención no existen todavía en el modelo de datos:
/// <c>SolicitudAusencia</c> solo guarda un sustento en texto libre. Salen vacíos hasta que
/// se agreguen esos campos.
/// </remarks>
public sealed record FilaDescansoMedicoDto(
    string Mes,
    string RazonSocial,
    string Empresa,
    string Dni,
    string ApellidosYNombres,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    int DiasEnElMes,
    string? Contingencia,
    string? Diagnostico,
    string? CentroAtencion,
    string Observacion);

/// <summary>
/// Una fila del bloque <c>VACACIONES</c>.
/// </summary>
/// <remarks>
/// El modelo reparte los días entre el mes del periodo y el siguiente, porque un descanso
/// que arranca el 25 se paga partido entre dos planillas.
/// </remarks>
public sealed record FilaVacacionesDto(
    int Numero,
    string Dni,
    string Nombre,
    int Dias,
    DateOnly FechaInicio,
    DateOnly FechaTermino,
    int DiasMesPeriodo,
    int DiasMesSiguiente,
    DateOnly FechaRetorno);

/// <summary>Una fila del bloque <c>LICENCIAS</c>.</summary>
public sealed record FilaLicenciaDto(
    string Mes,
    string Dni,
    string ApellidosYNombres,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    int DiasEnElMes,
    string Contingencia,
    string? Diagnostico,
    string? CentroAtencion,
    string? Observacion);
