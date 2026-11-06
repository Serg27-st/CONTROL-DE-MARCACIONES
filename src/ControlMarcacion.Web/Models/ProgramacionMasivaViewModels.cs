using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Programacion.Dto;

namespace ControlMarcacion.Web.Models;

public sealed class ProgramacionMasivaAlcanceVm
{
    public long PlantaId { get; set; }
    public long TurnoId { get; set; }
    public DateOnly FechaEfectiva { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
    public IReadOnlyList<OpcionDto> Plantas { get; set; } = [];
    public IReadOnlyList<OpcionDto> Turnos { get; set; } = [];
}

public sealed class ProgramacionMasivaSeleccionVm
{
    public long PlantaId { get; set; }
    public long TurnoId { get; set; }
    public DateOnly FechaEfectiva { get; set; }
    public string PlantaNombre { get; set; } = "";
    public string TurnoNombre { get; set; } = "";

    public IReadOnlyList<TrabajadorSeleccionDto> Trabajadores { get; set; } = [];
    public HashSet<long> Seleccionados { get; set; } = [];
    public int TotalTrabajadores { get; set; }
    public int Pagina { get; set; } = 1;
    public int TamanioPagina { get; set; } = 50;
    public int TotalPaginas => TotalTrabajadores <= 0 ? 1 : (int)Math.Ceiling((double)TotalTrabajadores / TamanioPagina);
}

public sealed record TrabajadorSeleccionDto(
    long Id,
    string Documento,
    string ApellidosNombres,
    string? TurnoActual,
    string? CuadrillaActual);

public sealed class ProgramacionMasivaPrevisualizarVm
{
    public PrevisualizacionMasiva? Previsualizacion { get; set; }
    public long PlantaId { get; set; }
    public long TurnoId { get; set; }
    public DateOnly FechaEfectiva { get; set; }
    public string PlantaNombre { get; set; } = "";
    public string TurnoNombre { get; set; } = "";
    public int TotalSeleccionados { get; set; }
}

public sealed class ProgramacionMasivaResultadoVm
{
    public ResultadoProgramacionMasiva? Resultado { get; set; }
    public string PlantaNombre { get; set; } = "";
    public string TurnoNombre { get; set; } = "";
    public DateOnly FechaEfectiva { get; set; }
}
