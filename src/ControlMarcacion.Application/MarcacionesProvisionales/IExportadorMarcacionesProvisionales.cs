using ControlMarcacion.Application.MarcacionesProvisionales.Dto;

namespace ControlMarcacion.Application.MarcacionesProvisionales;

public interface IExportadorMarcacionesProvisionales
{
    byte[] Exportar(
        IReadOnlyList<JornadaProvisionalDto> jornadas,
        string equipoDescripcion,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        string? usuario,
        IReadOnlyList<string>? advertencias = null);
}
