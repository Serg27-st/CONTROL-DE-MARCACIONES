using ControlMarcacion.Application.MarcacionesProvisionales.Dto;

namespace ControlMarcacion.Application.MarcacionesProvisionales;

public interface IServicioConsultaMarcacionProvisional
{
    Task<ResultadoConsultaProvisional> ConsultarAsync(
        long equipoId,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        CancellationToken cancellationToken = default);
}
