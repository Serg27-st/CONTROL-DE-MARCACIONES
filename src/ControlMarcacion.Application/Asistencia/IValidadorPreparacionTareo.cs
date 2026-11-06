namespace ControlMarcacion.Application.Asistencia;

/// <summary>Estado técnico de la configuración necesaria para calcular tareo.</summary>
public enum EstadoPreparacionTareo
{
    Listo = 1,
    Incompleto = 2,
    Conflicto = 3
}

/// <summary>Operación para la que se evalúa la preparación del periodo.</summary>
public enum OperacionPreparacionTareo
{
    ConsultaHistorica = 1,
    Calculo = 2,
    Recalculo = 3,
    Cierre = 4
}

public enum EstadoRequisitoPreparacion
{
    Cumplido = 1,
    Pendiente = 2,
    Conflicto = 3,
    NoAplica = 4
}

/// <summary>Problema estable, explicable y accionable encontrado por el verificador.</summary>
public sealed record IncidenciaPreparacionTareo(
    string Codigo,
    EstadoRequisitoPreparacion Estado,
    string Explicacion,
    string AccionRecomendada,
    string? Referencia = null);

public sealed record RequisitoPreparacionTareo(
    string Codigo,
    string Nombre,
    EstadoRequisitoPreparacion Estado,
    string Explicacion);

/// <summary>Resultado de un trabajador para un día concreto.</summary>
public sealed record PreparacionDiaTareo(
    DateOnly Fecha,
    EstadoPreparacionTareo Estado,
    long? EmpresaId,
    long? PlantaId,
    string? Planta,
    long? EquipoId,
    string? IdMarcador,
    long? TurnoId,
    bool EsDescansoProgramado,
    IReadOnlyList<RequisitoPreparacionTareo> Requisitos,
    IReadOnlyList<IncidenciaPreparacionTareo> Incidencias);

/// <summary>
/// Resultado por intervalo. Conserva el detalle diario para no resolver un mes entero con
/// la configuración del primer día.
/// </summary>
public sealed record PreparacionTrabajadorTareo(
    long TrabajadorId,
    string Documento,
    string Trabajador,
    DateOnly Desde,
    DateOnly Hasta,
    EstadoPreparacionTareo Estado,
    int DiasListos,
    int DiasIncompletos,
    int DiasConConflicto,
    IReadOnlyList<PreparacionDiaTareo> Dias,
    IReadOnlyList<IncidenciaPreparacionTareo> Incidencias);

public sealed record FiltroPreparacionTareo
{
    public long? PeriodoId { get; init; }

    public DateOnly Desde { get; init; }

    public DateOnly Hasta { get; init; }

    public long? PlantaId { get; init; }

    public EstadoPreparacionTareo? Estado { get; init; }

    public IReadOnlyCollection<long>? TrabajadoresId { get; init; }

    public OperacionPreparacionTareo Operacion { get; init; } = OperacionPreparacionTareo.ConsultaHistorica;
}

/// <summary>
/// Diagnostica configuración. Es deliberadamente de solo lectura: estar listo no aprueba
/// la planilla y estar incompleto no autoriza a alterar el alcance del recálculo.
/// </summary>
public interface IValidadorPreparacionTareo
{
    Task<PreparacionTrabajadorTareo> EvaluarFechaAsync(
        long trabajadorId,
        DateOnly fecha,
        long? periodoId = null,
        OperacionPreparacionTareo operacion = OperacionPreparacionTareo.ConsultaHistorica,
        CancellationToken cancelacion = default);

    Task<PreparacionTrabajadorTareo> EvaluarIntervaloAsync(
        long trabajadorId,
        DateOnly desde,
        DateOnly hasta,
        long? periodoId = null,
        OperacionPreparacionTareo operacion = OperacionPreparacionTareo.ConsultaHistorica,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<PreparacionTrabajadorTareo>> EvaluarAutorizadosAsync(
        FiltroPreparacionTareo filtro,
        CancellationToken cancelacion = default);
}
