using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Reportes.Dto;

namespace ControlMarcacion.Web.Models;

/// <summary>Filtros comunes a las pantallas de reportes.</summary>
public sealed class FiltroReporteVm
{
    public long? PeriodoId { get; init; }

    public long? EmpresaId { get; init; }

    public long? PlantaId { get; init; }

    public long? CentroId { get; init; }

    public long? CentroCostoId { get; init; }
}

/// <summary>Modelo del resumen mensual de tareo.</summary>
public sealed class ResumenTareoVm
{
    public FiltroReporteVm Filtro { get; init; } = new();

    public IReadOnlyList<PeriodoListaDto> Periodos { get; init; } = [];

    public IReadOnlyList<OpcionDto> Empresas { get; init; } = [];

    public IReadOnlyList<OpcionDto> Plantas { get; init; } = [];

    public IReadOnlyList<OpcionDto> Centros { get; init; } = [];

    public IReadOnlyList<OpcionDto> CentrosCosto { get; init; } = [];

    public IReadOnlyList<ResumenTareoDto> Filas { get; init; } = [];

    public bool HayPeriodoSeleccionado => Filtro.PeriodoId is not null;
}

/// <summary>Modelo del reporte de excepciones.</summary>
public sealed class ExcepcionesVm
{
    public FiltroReporteVm Filtro { get; init; } = new();

    public IReadOnlyList<PeriodoListaDto> Periodos { get; init; } = [];

    public IReadOnlyList<OpcionDto> Empresas { get; init; } = [];

    public IReadOnlyList<OpcionDto> Plantas { get; init; } = [];

    public IReadOnlyList<OpcionDto> Centros { get; init; } = [];

    public IReadOnlyList<OpcionDto> CentrosCosto { get; init; } = [];

    public IReadOnlyList<ExcepcionDto> Filas { get; init; } = [];

    public bool HayPeriodoSeleccionado => Filtro.PeriodoId is not null;
}
