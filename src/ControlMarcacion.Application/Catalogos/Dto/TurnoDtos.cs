using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Domain.Catalogos;

namespace ControlMarcacion.Application.Catalogos.Dto;

/// <summary>
/// Fila del listado de turnos.
/// </summary>
public sealed record TurnoListaDto(
    long Id,
    string Codigo,
    string Nombre,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    bool CruzaMedianoche,
    int MinutosRefrigerio,
    int MinutosTolerancia,
    int MinutosAnticipacionEntrada,
    int MinutosAnticipacionSalida,
    int MinutosJornadaOrdinaria,
    DiasSemana DiasAplicables,
    IReadOnlyList<string> PlantasHabilitadas,
    IReadOnlyList<long> PlantasHabilitadasIds,
    bool Activo,
    int TrabajadoresAsignados)
{
    /// <summary>Duración bruta del turno, sin descontar el refrigerio.</summary>
    public int DuracionBrutaMinutos => CruzaMedianoche
        ? (int)(TimeSpan.FromDays(1) - HoraInicio.ToTimeSpan() + HoraFin.ToTimeSpan()).TotalMinutes
        : (int)(HoraFin.ToTimeSpan() - HoraInicio.ToTimeSpan()).TotalMinutes;

    public int DuracionEfectivaMinutos => DuracionBrutaMinutos - MinutosRefrigerio;

    /// <summary>Hora desde la que una marcación cuenta como entrada de este turno.</summary>
    public TimeOnly InicioVentanaEntrada => HoraInicio.AddMinutes(-MinutosAnticipacionEntrada);

    /// <summary>Hora desde la que una marcación cuenta como salida de este turno.</summary>
    public TimeOnly InicioVentanaSalida => HoraFin.AddMinutes(-MinutosAnticipacionSalida);

    /// <summary>Hora límite para llegar sin tardanza.</summary>
    public TimeOnly LimiteTolerancia => HoraInicio.AddMinutes(MinutosTolerancia);

    /// <summary>Aplica a todas las plantas cuando no declara ninguna.</summary>
    public bool AplicaATodasLasPlantas => PlantasHabilitadas.Count == 0;
}

/// <summary>
/// Alta y edición de un turno.
/// </summary>
public sealed record GuardarTurnoRequest
{
    public long? Id { get; init; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(10, MinimumLength = 1, ErrorMessage = "El código no puede superar los 10 caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 80 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "La hora de inicio es obligatoria.")]
    public TimeOnly HoraInicio { get; init; }

    [Required(ErrorMessage = "La hora de fin es obligatoria.")]
    public TimeOnly HoraFin { get; init; }

    [Range(0, 240, ErrorMessage = "El refrigerio debe estar entre 0 y 240 minutos.")]
    public int MinutosRefrigerio { get; init; }

    [Range(0, 120, ErrorMessage = "La tolerancia debe estar entre 0 y 120 minutos.")]
    public int MinutosTolerancia { get; init; }

    /// <summary>
    /// Minutos antes del inicio en que una marcación ya cuenta como entrada.
    /// </summary>
    [Range(0, 240, ErrorMessage = "La anticipación de entrada debe estar entre 0 y 240 minutos.")]
    public int MinutosAnticipacionEntrada { get; init; } = 15;

    /// <summary>
    /// Minutos antes del fin en que una marcación ya cuenta como salida.
    /// </summary>
    [Range(0, 240, ErrorMessage = "La anticipación de salida debe estar entre 0 y 240 minutos.")]
    public int MinutosAnticipacionSalida { get; init; } = 5;

    [Range(1, 1440, ErrorMessage = "La jornada ordinaria debe estar entre 1 y 1440 minutos.")]
    public int MinutosJornadaOrdinaria { get; init; } = 480;

    public DiasSemana DiasAplicables { get; init; } = DiasSemana.LunesASabado;

    /// <summary>
    /// Plantas donde el turno está habilitado. Vacío significa todas.
    /// </summary>
    public IReadOnlyList<long> PlantasHabilitadas { get; init; } = [];

    public bool Activo { get; init; } = true;
}
