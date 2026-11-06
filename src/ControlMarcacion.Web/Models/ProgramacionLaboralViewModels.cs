using ControlMarcacion.Application.Programacion.Dto;

namespace ControlMarcacion.Web.Models;

public sealed class ImportarProgramacionVm
{
    public ResultadoPrevisualizacionProgramacion? Previsualizacion { get; init; }
    public ResultadoConfirmacionProgramacion? Confirmacion { get; init; }
    public string? TokenConfirmacion { get; init; }
    public bool PuedeConfirmar => Previsualizacion?.PuedeConfirmar == true
        && !string.IsNullOrWhiteSpace(TokenConfirmacion);
}
