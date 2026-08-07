using ControlMarcacion.Domain.Asistencia;

namespace ControlMarcacion.Application.Reportes.Dto;

/// <summary>Filtro común a todos los reportes.</summary>
public sealed record FiltroReporte
{
    public required long PeriodoId { get; init; }

    public long? EmpresaId { get; init; }

    public long? PlantaId { get; init; }

    public long? CentroId { get; init; }

    public long? CentroCostoId { get; init; }
}

/// <summary>
/// Fila del resumen mensual: un trabajador, con sus totales del periodo.
/// </summary>
/// <remarks>
/// Es el insumo para planilla, no la boleta: entrega minutos y conteos de días. Las dos
/// excepciones son <see cref="MontoBonoNocturno"/> — el sistema no calcula la remuneración
/// completa del trabajador, pero ese concepto puntual (D.S. 007-2002-TR, art. 8) sí lo
/// resuelve porque depende pura y exclusivamente de datos que el sistema ya tiene:
/// franja nocturna trabajada, RMV, sobretasa y sueldo básico, todos con vigencia por fecha —
/// y <see cref="MontoMovilidad"/>, días asistidos por la tarifa vigente, mismo criterio que
/// usa el tareo final.
///
/// <see cref="Centro"/>/<see cref="CentroCosto"/> vienen del catálogo real de SAP asignado
/// en Personal (<c>Trabajador.CentroCostoId</c>); si el trabajador todavía no tiene uno
/// asignado, caen a la aproximación con Área/Sección — mismo criterio que
/// <c>ServicioReportes.ArmarFilasTareoAsync</c> usa para el tareo final.
/// </remarks>
public sealed record ResumenTareoDto(
    long TrabajadorId,
    string Trabajador,
    string? IdMarcador,
    string Empresa,
    string Planta,
    string? Centro,
    string? CentroCosto,
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
    int MinutosTardanzaAcumulados,
    decimal MontoBonoNocturno,
    decimal MontoMovilidad)
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
