namespace ControlMarcacion.Application.Programacion.Dto;

public sealed record SolicitudProgramacionMasiva(
    long PlantaId,
    long TurnoId,
    DateOnly FechaEfectiva,
    IReadOnlyList<long> TrabajadorIds);

public sealed record PrevisualizacionMasiva(
    string Hash,
    int TotalSeleccionados,
    int Cambios,
    int SinCambio,
    int Advertencias,
    IReadOnlyList<FilaPrevisualizacion> Filas);

public sealed record FilaPrevisualizacion(
    long TrabajadorId,
    string Documento,
    string ApellidosNombres,
    string? TurnoActual,
    string? CuadrillaActual,
    string TurnoNuevo,
    DateOnly FechaEfectiva,
    EstadoCambio Estado,
    string? Observacion);

public enum EstadoCambio { Cambio, SinCambio, Advertencia }

public sealed record ResultadoProgramacionMasiva(
    int Procesados,
    int SinCambio,
    int Errores,
    IReadOnlyList<ErrorProgramacion> Detalle);

public sealed record ErrorProgramacion(
    long TrabajadorId,
    string ApellidosNombres,
    string Motivo);
