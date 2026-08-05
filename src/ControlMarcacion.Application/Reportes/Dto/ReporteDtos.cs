using ControlMarcacion.Domain.Asistencia;

namespace ControlMarcacion.Application.Reportes.Dto;

/// <summary>Filtro común a todos los reportes.</summary>
public sealed record FiltroReporte
{
    public required long PeriodoId { get; init; }

    public long? EmpresaId { get; init; }

    public long? PlantaId { get; init; }

    public long? AreaId { get; init; }

    public long? SeccionId { get; init; }
}

/// <summary>
/// Fila del resumen mensual: un trabajador, con sus totales del periodo.
/// </summary>
/// <remarks>
/// Es el insumo para planilla, no la boleta: entrega minutos y conteos de días, nunca
/// montos. El sistema no conoce ninguna remuneración.
/// </remarks>
public sealed record ResumenTareoDto(
    long TrabajadorId,
    string Trabajador,
    string? IdMarcador,
    string Empresa,
    string Planta,
    string Area,
    string Seccion,
    string Puesto,
    int DiasPuntual,
    int DiasTardanza,
    int DiasFalta,
    int DiasAusenciaJustificada,
    int DiasIncompleto,
    int DiasDescansoTrabajado,
    int DiasFeriadoTrabajado,
    int MinutosOrdinarios,
    int MinutosExtra25,
    int MinutosExtra35,
    int MinutosNocturnos,
    int MinutosTardanzaAcumulados)
{
    public decimal HorasOrdinarias => Math.Round(MinutosOrdinarios / 60m, 2);

    public decimal HorasExtra25 => Math.Round(MinutosExtra25 / 60m, 2);

    public decimal HorasExtra35 => Math.Round(MinutosExtra35 / 60m, 2);

    public decimal HorasNocturnas => Math.Round(MinutosNocturnos / 60m, 2);

    public decimal HorasTardanza => Math.Round(MinutosTardanzaAcumulados / 60m, 2);

    public int DiasNoLaborableTrabajado => DiasDescansoTrabajado + DiasFeriadoTrabajado;
}

/// <summary>
/// Fila del reporte de excepciones: un día que necesita revisión.
/// </summary>
/// <remarks>
/// Solo entran los estados que son una anomalía sobre la que alguien debe actuar
/// (falta, tardanza, ausencia justificada, marcación incompleta). Los días de descanso,
/// feriado o sin vínculo no son excepciones: son lo esperado, y meterlos aquí obligaría
/// a RRHH a descartar cientos de filas irrelevantes cada mes.
/// </remarks>
public sealed record ExcepcionDto(
    long TrabajadorId,
    string Trabajador,
    string? IdMarcador,
    DateOnly Fecha,
    string? Turno,
    DateTime? EntradaReal,
    DateTime? SalidaReal,
    EstadoTareo Estado,
    int MinutosTardanza,
    int MinutosSalidaAnticipada,
    string? Observacion);

/// <summary>
/// Encabezado que acompaña toda exportación: qué periodo, con qué filtros, quién y cuándo.
/// </summary>
/// <remarks>
/// Un reporte sin esta información es un número sin procedencia: nadie puede auditar
/// después de dónde salió, ni si el periodo seguía abierto cuando se generó.
/// </remarks>
public sealed record MetadatosReporte(
    string PeriodoCodigo,
    string PeriodoNombre,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    bool PeriodoAbierto,
    string FiltrosDescripcion,
    DateTime GeneradoEn,
    string GeneradoPor);
