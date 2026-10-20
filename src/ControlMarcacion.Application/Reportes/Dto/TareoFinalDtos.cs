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

    /// <summary>
    /// Columna <c>CENTRO</c>: código del centro SAP (<c>Centro.Codigo</c>, el
    /// <c>CO_AREA</c> del archivo de GCA) al que pertenece el centro de costo asignado al
    /// trabajador en Personal. Ejemplo: <c>066G</c>.
    /// </summary>
    /// <remarks>
    /// Las tres columnas de CECO —<see cref="Centro"/>, <see cref="CentroCosto"/> y
    /// <see cref="DescripcionCeco"/>— salen siempre de la misma fila del catálogo real
    /// (<c>CENTROS DE COSTO POR EMPRESA.xlsx</c>), nunca de fuentes distintas. Si el
    /// trabajador todavía no tiene centro de costo asignado, las tres salen vacías: ya no
    /// se aproximan con Área/Sección, porque <see cref="Area"/> es ahora una columna
    /// propia y mezclarlas daría un CECO falso. Ver <c>docs/04-formato-tareo-final.md</c>.
    /// </remarks>
    public string? Centro { get; init; }

    /// <summary>
    /// Columna <c>CENTRO DE COSTO</c>: código del centro de costo SAP
    /// (<c>CentroCosto.Codigo</c>, el <c>CO_SECC</c> del archivo de GCA).
    /// Ejemplo: <c>PE21400303</c>.
    /// </summary>
    public string? CentroCosto { get; init; }

    /// <summary>
    /// Columna <c>DESCRIP. CECO</c>: descripción del centro de costo
    /// (<c>CentroCosto.Descripcion</c>, el <c>DE_SECC</c> del archivo de GCA).
    /// Ejemplo: <c>Manufactura Detergentes Lurín</c>.
    /// </summary>
    public string? DescripcionCeco { get; init; }

    /// <summary>
    /// Columna <c>AREA</c>: área interna de trabajo del operario (<c>Area.Nombre</c>):
    /// <c>ENVASADO</c>, <c>LAVAVAJILLAS</c>, <c>PROCESOS</c>…
    /// </summary>
    /// <remarks>
    /// Es un dato por trabajador y **no se deriva del centro de costo**: en el archivo de
    /// GCA el CECO <c>PE21420300</c> aparece con tres áreas distintas. Por eso Área sigue
    /// siendo un campo propio de Personal y convive con el CECO, en vez de haber quedado
    /// reemplazada por él.
    /// </remarks>
    public string? Area { get; init; }

    public required DateOnly FechaIngreso { get; init; }

    public DateOnly? FechaCese { get; init; }

    public string? Observacion { get; init; }

    // ---------- Movimiento de personal, en días ----------

    public int DiasFalta { get; init; }

    // No hay DiasSuspension: el sistema no tiene el concepto y nadie lo calculaba, pero la
    // propiedad existía, salía siempre en cero y se sumaba en la fila TOTAL, lo que la hacía
    // parecer calculada. La columna SUSPENSION del tareo final se conserva —es el formato de
    // GCA— y ahora va vacía. Ver ExportadorReportesExcel.SuspensionSinDato.

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

    // ---------- Montos calculados por el sistema (AI y AN) ----------

    /// <summary>
    /// Días asistidos (marcación real de entrada y salida, en estado Puntual o Tardanza)
    /// × el valor de movilidad diaria vigente en el periodo.
    /// </summary>
    public decimal MontoMovilidad { get; init; }

    /// <summary>
    /// 10 % de la RMV vigente, monto fijo mensual si <c>Trabajador.TieneHijos</c>. No se
    /// prorratea por días trabajados: así se modela legalmente en Perú (Ley 25129).
    /// </summary>
    public decimal MontoAsignacionFamiliar { get; init; }

    // ---------- Reintegros y descuentos manuales (bloque AO-BF) ----------

    /// <remarks>
    /// Los 18 campos siguientes vienen de <c>ReintegroTareo</c>, cargado a mano por
    /// planilla desde su propia pantalla (correcciones de periodos anteriores y conceptos
    /// de dinero que el sistema no calcula). En cero cuando no se cargó ninguno para el
    /// trabajador y el periodo — igual que en el archivo modelo de GCA.
    /// </remarks>
    public decimal ReintegroHorasNormales { get; init; }

    public decimal ReintegroHorasExtra25 { get; init; }

    public decimal ReintegroHorasExtra35 { get; init; }

    public decimal ReintegroHorasNoLaborable { get; init; }

    public decimal ReintegroMontoMovilidad { get; init; }

    public decimal ReintegroHorasNocturnas { get; init; }

    public decimal ReintegroHorasExtra25Nocturnas { get; init; }

    public decimal ReintegroHorasExtra35Nocturnas { get; init; }

    public decimal ReintegroHorasNoLaborableNocturnas { get; init; }

    public decimal ReintegroViatico { get; init; }

    public int ReintegroDiasDescansoMedico { get; init; }

    public int ReintegroDiasSubsidioMaternidad { get; init; }

    public int ReintegroDiasSubsidioAccidente { get; init; }

    public int ReintegroDiasLicenciaPaternidad { get; init; }

    public decimal ReintegroLicenciaConGoce { get; init; }

    public decimal ReintegroAsignacionFamiliar { get; init; }

    public decimal ReintegroAfecto { get; init; }

    public decimal ReintegroDescuentoPagoIndebido { get; init; }

    private static decimal AHoras(int minutos) => Math.Round(minutos / 60m, 2);
}

/// <summary>Una fila de la hoja <c>Descanso Medico</c>.</summary>
/// <remarks>
/// Contingencia, diagnóstico y centro de atención salen de la propia solicitud, que los
/// exige en los tipos subsidiados. Los dos primeros viajan ya traducidos al texto exacto
/// del archivo de GCA (<c>ENFERMEDAD COMUN</c>, <c>ESSALUD</c>…), no como enum: esta hoja
/// se pega en la plantilla que planilla ya tiene y otra grafía le rompe los filtros.
///
/// Solo quedan vacíos en las solicitudes registradas antes de que existieran estos campos.
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
