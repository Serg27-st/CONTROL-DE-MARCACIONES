using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Web.Models;

public sealed class PreparacionTareoVm
{
    public long? PeriodoId { get; init; }

    public long? PlantaId { get; init; }

    public EstadoPreparacionTareo? Estado { get; init; }

    public OperacionPreparacionTareo Operacion { get; init; } = OperacionPreparacionTareo.ConsultaHistorica;

    public IReadOnlyList<PeriodoListaDto> Periodos { get; init; } = [];

    public IReadOnlyList<OpcionDto> Plantas { get; init; } = [];

    public IReadOnlyList<PreparacionTrabajadorTareo> Resultados { get; init; } = [];

    public PeriodoListaDto? Periodo => Periodos.FirstOrDefault(p => p.Id == PeriodoId);

    public int Listos => Resultados.Count(r => r.Estado == EstadoPreparacionTareo.Listo);

    public int Incompletos => Resultados.Count(r => r.Estado == EstadoPreparacionTareo.Incompleto);

    public int Conflictos => Resultados.Count(r => r.Estado == EstadoPreparacionTareo.Conflicto);
}
