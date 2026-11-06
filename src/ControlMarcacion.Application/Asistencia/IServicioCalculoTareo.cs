using ControlMarcacion.Application.Asistencia.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Asistencia;

namespace ControlMarcacion.Application.Asistencia;

/// <summary>Resumen de una corrida de cálculo.</summary>
public sealed record ResultadoRecalculo(
    long PeriodoId,
    string PeriodoCodigo,
    int TrabajadoresProcesados,
    int DiasCalculados,
    int DiasCreados,
    int DiasActualizados,
    IReadOnlyDictionary<EstadoTareo, int> PorEstado)
{
    public int Conteo(EstadoTareo estado) => PorEstado.TryGetValue(estado, out var n) ? n : 0;
}

/// <summary>Fila del tareo para mostrar en pantalla.</summary>
public sealed record TareoDiarioDto(
    long Id,
    long TrabajadorId,
    string Trabajador,
    string? IdMarcador,
    string? Equipo,
    DateOnly Fecha,
    string? Turno,
    DateTime? EntradaReal,
    DateTime? SalidaReal,
    int MinutosTrabajados,
    int MinutosOrdinarios,
    int MinutosExtra25,
    int MinutosExtra35,
    int MinutosNocturnos,
    int MinutosTardanza,
    int MinutosSalidaAnticipada,
    TipoDiaNoLaborable TipoDiaNoLaborable,
    EstadoTareo Estado,
    string? Observacion,
    TipoIncidencia? TipoIncidencia,
    bool RequiereValidacion,
    bool TieneDescansoSustitutorio,
    DateOnly? FechaDescansoSustitutorio)
{
    public bool EsDiaNoLaborable => TipoDiaNoLaborable != TipoDiaNoLaborable.Ninguno;
}

/// <summary>Filtro del listado del tareo.</summary>
public sealed record FiltroTareo
{
    public long? PeriodoId { get; init; }

    public long? TrabajadorId { get; init; }

    public EstadoTareo? Estado { get; init; }

    /// <summary>Texto libre sobre apellidos, nombres o documento del trabajador.</summary>
    public string? Busqueda { get; init; }

    public int Pagina { get; init; } = 1;

    /// <summary>
    /// <b>Trabajadores</b> por página, no filas.
    /// </summary>
    /// <remarks>
    /// La unidad de página es la persona y no el día: la pantalla se llama «detalle por
    /// trabajador y día», y paginar por filas partía el mes de alguien entre dos páginas
    /// —días 1 al 20 en una, 21 al 31 en la siguiente, con el nombre repetido encima de
    /// cada trozo—. Nadie lee medio mes de una persona.
    ///
    /// Es además lo que hace correctos los totales por trabajador sin consultar nada más:
    /// la página trae a esa persona <b>entera</b>, así que sumar sus días es sumar todo lo
    /// que le corresponde bajo el filtro activo.
    /// </remarks>
    public int TamanioPagina { get; init; } = 10;
}

/// <summary>
/// Los días de un trabajador dentro del periodo, ya agrupados.
/// </summary>
/// <remarks>
/// Los totales se calculan sobre <see cref="Dias"/> y eso es correcto <b>porque la
/// paginación es por trabajador</b>: la lista no es un trozo, es todo lo que esa persona
/// tiene bajo el filtro. Si algún día se volviera a paginar por filas, estos totales
/// pasarían a ser «lo que se ve» sin que nada lo delatara.
///
/// Cuando hay un filtro por estado, describen lo filtrado —doce faltas, no doce días— y
/// la vista los rotula en consecuencia.
/// </remarks>
public sealed record TareoDeTrabajador(
    long TrabajadorId,
    string Trabajador,
    string? IdMarcador,
    string? Equipo,
    IReadOnlyList<TareoDiarioDto> Dias)
{
    public int TotalDias => Dias.Count;

    public int Faltas => Dias.Count(d => d.Estado == EstadoTareo.Falta);

    public int Tardanzas => Dias.Count(d => d.Estado == EstadoTareo.Tardanza);

    public int MinutosTrabajados => Dias.Sum(d => d.MinutosTrabajados);

    public int MinutosExtra => Dias.Sum(d => d.MinutosExtra25 + d.MinutosExtra35);

    public int MinutosTardanza => Dias.Sum(d => d.MinutosTardanza);

    /// <summary>Primer y último día que trae, para rotular el tramo mostrado.</summary>
    public DateOnly? Desde => Dias.Count == 0 ? null : Dias[0].Fecha;

    public DateOnly? Hasta => Dias.Count == 0 ? null : Dias[^1].Fecha;
}

/// <summary>
/// Totales del tareo que coincide con un filtro, calculados en la base.
/// </summary>
/// <remarks>
/// Existe porque el listado <b>pagina</b>: sumar los minutos de las filas que llegaron a
/// la vista daría "el total de esta página", que no es ningún dato útil y además cambia al
/// pasar de página. Estos totales se calculan sobre el conjunto completo.
///
/// El reparto por estado ignora a propósito el estado del filtro: es lo que permite
/// mostrar "12 tardanzas, 3 faltas" como accesos para filtrar, en vez de un único número
/// que ya sabías porque tú mismo lo elegiste.
/// </remarks>
public sealed record ResumenTareo(
    IReadOnlyDictionary<EstadoTareo, int> PorEstado,
    int Dias,
    int Trabajadores,
    int MinutosOrdinarios,
    int MinutosExtra25,
    int MinutosExtra35,
    int MinutosNocturnos,
    int MinutosTardanza,
    int MinutosSalidaAnticipada)
{
    public static ResumenTareo Vacio { get; } =
        new(new Dictionary<EstadoTareo, int>(), 0, 0, 0, 0, 0, 0, 0, 0);

    public int MinutosExtra => MinutosExtra25 + MinutosExtra35;

    public int Conteo(EstadoTareo estado) => PorEstado.TryGetValue(estado, out var n) ? n : 0;

    public bool HayDatos => Dias > 0;
}

/// <summary>
/// Cálculo del tareo diario.
/// </summary>
/// <remarks>
/// El recálculo es <b>idempotente</b>: correrlo dos veces sobre el mismo periodo produce
/// exactamente el mismo resultado y no duplica filas. Lo garantiza el índice único
/// (trabajador, fecha) más la actualización en sitio de la fila existente.
///
/// Un periodo <b>cerrado</b> no se recalcula: sus tareos ya están congelados.
/// </remarks>
public interface IServicioCalculoTareo
{
    Task<ResultadoRecalculo> RecalcularPeriodoAsync(
        long periodoId,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Recalcula una única jornada después de aprobar una regularización puntual.
    /// Conserva las mismas ventanas y reglas del cálculo del periodo, pero solo persiste
    /// el día indicado del trabajador indicado.
    /// </summary>
    Task<ResultadoRecalculo> RecalcularJornadaAsync(
        long periodoId,
        long trabajadorId,
        DateOnly fecha,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// El tareo agrupado por trabajador, con una página de <b>personas</b> completas.
    /// </summary>
    /// <remarks>
    /// Ver <see cref="FiltroTareo.TamanioPagina"/>: la unidad de página es la persona, de
    /// modo que el mes de nadie se parte entre dos páginas.
    /// </remarks>
    Task<ResultadoPaginado<TareoDeTrabajador>> ListarPorTrabajadorAsync(
        FiltroTareo filtro,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Los días que esperan una decisión humana: la bandeja de incidencias.
    /// </summary>
    /// <remarks>
    /// Vive aquí y no en un módulo propio porque la incidencia es un <b>atributo del día de
    /// tareo</b>, no una entidad aparte: es Asistencia quien la calcula y quien la puede
    /// consultar sin cruzar el límite de nadie. Trae también el estado de la regularización
    /// que le corresponda, para que la bandeja distinga «hay que resolverlo» de «ya se pidió
    /// y falta autorizarlo».
    ///
    /// Pagina como todo listado del sistema: la tabla de tareos llega a decenas de miles de
    /// filas y un mes malo pone cientos en la bandeja.
    /// </remarks>
    Task<ResultadoPaginado<IncidenciaDto>> ListarIncidenciasAsync(
        FiltroIncidencias filtro,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Totales y reparto por estado del tareo que coincide con el filtro.
    /// </summary>
    /// <remarks>
    /// Se ignoran <see cref="FiltroTareo.Estado"/> y la paginación: el resumen describe
    /// todo lo que hay bajo el periodo y la búsqueda, para que los conteos por estado
    /// sirvan de acceso a filtrar y no cambien al pasar de página.
    /// </remarks>
    Task<ResumenTareo> ResumirAsync(
        FiltroTareo filtro,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Agrupa los minutos del periodo por concepto y les aplica la tasa vigente.
    /// </summary>
    /// <remarks>
    /// La tasa se resuelve <b>por la fecha del periodo</b>, no por la de hoy: recalcular
    /// un mes de hace medio año debe usar las tasas de entonces, o el recálculo reescribiría
    /// una planilla ya pagada.
    /// </remarks>
    Task<LiquidacionPeriodo?> LiquidarPeriodoAsync(
        long periodoId,
        CancellationToken cancelacion = default);
}
