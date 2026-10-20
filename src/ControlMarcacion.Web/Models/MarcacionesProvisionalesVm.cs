using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.MarcacionesProvisionales.Dto;

namespace ControlMarcacion.Web.Models;

public sealed class MarcacionesProvisionalesVm
{
    public IReadOnlyList<EquipoOpcionDto> Equipos { get; init; } = [];

    public long? EquipoId { get; init; }
    public DateOnly? FechaInicio { get; init; }
    public DateOnly? FechaFin { get; init; }

    public ResultadoConsultaProvisional? Resultado { get; init; }

    public IReadOnlyList<ImportacionProvisionalListaDto> Historial { get; init; } = [];
}
