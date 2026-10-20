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
    int MinutosGraciaSalida,
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

    /// <summary>Hora límite para salir sin que cuente como sobretiempo.</summary>
    public TimeOnly LimiteGraciaSalida => HoraFin.AddMinutes(MinutosGraciaSalida);

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

    /// <summary>
    /// Minutos después del fin en que salir todavía no genera sobretiempo. Cero: todo
    /// minuto posterior al fin es sobretiempo.
    /// </summary>
    /// <remarks>
    /// El tope es bajo a propósito: la gracia cubre la cola frente al lector, no una
    /// política para dejar de pagar sobretiempo.
    /// </remarks>
    [Range(0, 30, ErrorMessage = "La gracia de salida debe estar entre 0 y 30 minutos.")]
    public int MinutosGraciaSalida { get; init; }

    /// <summary>
    /// Desde qué fecha rige el horario que se está guardando. Vacío: desde hoy.
    /// </summary>
    /// <remarks>
    /// El horario del turno tiene vigencia por fecha, así que cambiarlo no reescribe los
    /// meses anteriores: se cierra la versión que regía y se abre otra. Esta fecha es la
    /// frontera entre las dos, y sirve para anotar el cambio <b>el día en que ocurrió</b>
    /// cuando se registra tarde. Una fecha igual o anterior al inicio de la versión vigente
    /// la corrige en sitio: el horario estaba mal desde el principio.
    /// </remarks>
    public DateOnly? RigeDesde { get; init; }

    [Range(1, 1440,ErrorMessage = "La jornada ordinaria debe estar entre 1 y 1440 minutos.")]
    public int MinutosJornadaOrdinaria { get; init; } = 480;

    public DiasSemana DiasAplicables { get; init; } = DiasSemana.LunesASabado;

    /// <summary>
    /// Plantas donde el turno está habilitado. Vacío significa todas.
    /// </summary>
    public IReadOnlyList<long> PlantasHabilitadas { get; init; } = [];

    public bool Activo { get; init; } = true;
}
