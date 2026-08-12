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

    Task<IReadOnlyList<TareoDiarioDto>> ListarAsync(
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
