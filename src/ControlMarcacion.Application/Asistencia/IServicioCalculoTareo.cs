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
    string? Observacion)
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
    /// Filas por página. Un periodo mensual son decenas de miles de filas
    /// (trabajadores × días) y volcarlas de una vez tumba el navegador.
    /// </summary>
    public int TamanioPagina { get; init; } = 50;
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

    Task<ResultadoPaginado<TareoDiarioDto>> ListarAsync(
        FiltroTareo filtro,
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
