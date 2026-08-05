using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Domain.Asistencia;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo de la pantalla de tareo.</summary>
public sealed class TareoVm
{
    public IReadOnlyList<PeriodoListaDto> Periodos { get; init; } = [];

    public IReadOnlyList<TareoDiarioDto> Tareos { get; init; } = [];

    public long? PeriodoId { get; init; }

    public EstadoTareo? Estado { get; init; }

    /// <summary>Minutos por concepto con la tasa vigente aplicada.</summary>
    public LiquidacionPeriodo? Liquidacion { get; init; }

    public PeriodoListaDto? PeriodoSeleccionado =>
        Periodos.FirstOrDefault(p => p.Id == PeriodoId);

    /// <summary>Conteo por estado, para el resumen de la cabecera.</summary>
    public IReadOnlyDictionary<EstadoTareo, int> Resumen =>
        Tareos.GroupBy(t => t.Estado).ToDictionary(g => g.Key, g => g.Count());

    public int MinutosExtraTotales =>
        Tareos.Sum(t => t.MinutosExtra25 + t.MinutosExtra35);

    public int MinutosTardanzaTotales => Tareos.Sum(t => t.MinutosTardanza);

    public int MinutosNocturnosTotales => Tareos.Sum(t => t.MinutosNocturnos);
}
