using ControlMarcacion.Application.Programacion.Dto;

namespace ControlMarcacion.Application.Programacion;

/// <summary>
/// Concilia jornadas programadas contra los catálogos y la historia laboral,
/// independientemente de su origen (Excel, interfaz web, copia de periodo anterior).
/// </summary>
public interface IConciliadorProgramacion
{
    Task<ResultadoPrevisualizacionProgramacion> PrevisualizarAsync(
        IReadOnlyList<EntradaProgramacion> entradas,
        string nombreOrigen,
        IReadOnlyCollection<long>? plantasAutorizadas,
        CancellationToken cancelacion = default);

    Task<ResultadoConfirmacionProgramacion> ConfirmarAsync(
        IReadOnlyList<EntradaProgramacion> entradas,
        string nombreOrigen,
        string huellaEsperada,
        string usuario,
        IReadOnlyCollection<long>? plantasAutorizadas,
        CancellationToken cancelacion = default);
}
