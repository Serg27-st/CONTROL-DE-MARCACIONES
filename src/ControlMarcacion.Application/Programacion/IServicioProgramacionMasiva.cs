using ControlMarcacion.Application.Programacion.Dto;

namespace ControlMarcacion.Application.Programacion;

public interface IServicioProgramacionMasiva
{
    Task<PrevisualizacionMasiva> PrevisualizarAsync(
        SolicitudProgramacionMasiva solicitud,
        CancellationToken cancelacion = default);

    Task<ResultadoProgramacionMasiva> ConfirmarAsync(
        SolicitudProgramacionMasiva solicitud,
        string hashPrevisualizado,
        string usuario,
        CancellationToken cancelacion = default);
}
