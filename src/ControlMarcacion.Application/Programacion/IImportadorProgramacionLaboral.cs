using ControlMarcacion.Application.Programacion.Dto;

namespace ControlMarcacion.Application.Programacion;

public interface IImportadorProgramacionLaboral
{
    byte[] GenerarPlantillaExcel();

    Task<ResultadoPrevisualizacionProgramacion> PrevisualizarAsync(
        Stream contenido,
        string nombreArchivo,
        IReadOnlyCollection<long>? plantasAutorizadas,
        CancellationToken cancelacion = default);

    Task<ResultadoConfirmacionProgramacion> ConfirmarAsync(
        Stream contenido,
        string nombreArchivo,
        string huellaEsperada,
        string usuario,
        IReadOnlyCollection<long>? plantasAutorizadas,
        CancellationToken cancelacion = default);
}
