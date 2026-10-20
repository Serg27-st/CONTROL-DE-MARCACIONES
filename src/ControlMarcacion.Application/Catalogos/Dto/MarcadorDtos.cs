using System.ComponentModel.DataAnnotations;

namespace ControlMarcacion.Application.Catalogos.Dto;

/// <summary>
/// Fila del catálogo de marcadores.
/// </summary>
public sealed record MarcadorListaDto(
    long Id,
    string Codigo,
    long EquipoId,
    string Equipo,
    long PlantaId,
    string Planta,
    string Empresa,
    string? Descripcion,
    bool Activo,
    /// <summary>Trabajadores con este ID asignado y sin fecha de fin.</summary>
    int AsignacionesVigentes,
    /// <summary>Asignaciones históricas, vigentes o ya cerradas. Bloquea la baja.</summary>
    int AsignacionesTotales,
    /// <summary>Quién tiene hoy el número («APELLIDOS, Nombres»). Nulo si está libre.</summary>
    string? AsignadoA = null)
{
    public bool EstaEnUso => AsignacionesTotales > 0;
}

/// <summary>
/// Ficha mínima de un marcador del catálogo, para validar una asignación desde Personal.
/// </summary>
public sealed record MarcadorCatalogoDto(
    long Id,
    string Codigo,
    long EquipoId,
    string Equipo,
    long PlantaId,
    string Planta,
    bool Activo);

/// <summary>
/// Filtros del catálogo de marcadores.
/// </summary>
public sealed record FiltroMarcadores
{
    public string? Busqueda { get; init; }

    public long? PlantaId { get; init; }

    public bool IncluirInactivos { get; init; }
}

/// <summary>
/// Alta y edición de un marcador.
/// </summary>
public sealed record GuardarMarcadorRequest
{
    public long? Id { get; init; }

    /// <summary>
    /// Código tal como lo escribe el equipo en el TXT, incluidos los ceros a la
    /// izquierda. Texto y no número, por eso mismo.
    /// </summary>
    [Required(ErrorMessage = "El código del marcador es obligatorio.")]
    [StringLength(30, MinimumLength = 1, ErrorMessage = "El código no puede superar los 30 caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    /// <summary>
    /// Equipo que emite este código.
    /// </summary>
    /// <remarks>
    /// Sustituye a la planta, que hasta el cambio de modelo se capturaba aquí: la planta
    /// del marcador es hoy una <b>copia derivada</b> de la del equipo y la escribe el
    /// servicio. Pedir las dos permitiría guardarlas en desacuerdo.
    /// </remarks>
    [Required(ErrorMessage = "Seleccione el equipo que emite este código.")]
    [Range(1, long.MaxValue, ErrorMessage = "Seleccione el equipo que emite este código.")]
    public long EquipoId { get; init; }

    [StringLength(150, ErrorMessage = "La descripción no puede superar los 150 caracteres.")]
    public string? Descripcion { get; init; }

    public bool Activo { get; init; } = true;
}

/// <summary>
/// Fila del catálogo de equipos.
/// </summary>
public sealed record EquipoListaDto(
    long Id,
    string Codigo,
    string Nombre,
    string? Serie,
    long PlantaId,
    string Planta,
    string Empresa,
    string? Descripcion,
    bool Activo,
    /// <summary>Marcadores activos que emite este equipo.</summary>
    int MarcadoresActivos,
    /// <summary>Marcadores registrados, activos o de baja. Bloquea el cambio de planta.</summary>
    int MarcadoresTotales,
    /// <summary>Archivos cargados desde este equipo. Bloquea la baja si hay alguno.</summary>
    int Importaciones)
{
    public bool EstaEnUso => MarcadoresTotales > 0 || Importaciones > 0;
}

/// <summary>
/// Filtros del catálogo de equipos.
/// </summary>
public sealed record FiltroEquipos
{
    public string? Busqueda { get; init; }

    public long? PlantaId { get; init; }

    public bool IncluirInactivos { get; init; }
}

/// <summary>
/// Alta y edición de un equipo marcador.
/// </summary>
public sealed record GuardarEquipoRequest
{
    public long? Id { get; init; }

    [Required(ErrorMessage = "El código del equipo es obligatorio.")]
    [StringLength(30, MinimumLength = 1, ErrorMessage = "El código no puede superar los 30 caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "El nombre del equipo es obligatorio.")]
    [StringLength(150, MinimumLength = 1, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>
    /// Serie que imprime el equipo. Opcional: se da de alta antes de ver un archivo suyo.
    /// </summary>
    /// <remarks>
    /// <b>Texto</b> y no número — las series mezclan letras y dígitos (<c>VGU6251600671</c>).
    /// </remarks>
    [StringLength(60, ErrorMessage = "La serie no puede superar los 60 caracteres.")]
    public string? Serie { get; init; }

    [Required(ErrorMessage = "Seleccione la planta donde está instalado el equipo.")]
    [Range(1, long.MaxValue, ErrorMessage = "Seleccione la planta donde está instalado el equipo.")]
    public long PlantaId { get; init; }

    [StringLength(150, ErrorMessage = "La descripción no puede superar los 150 caracteres.")]
    public string? Descripcion { get; init; }

    public bool Activo { get; init; } = true;
}

/// <summary>
/// Equipo para elegir en un desplegable, con su planta.
/// </summary>
/// <remarks>
/// Lleva la planta dentro para que la pantalla de carga pueda filtrar los marcadores al
/// elegirla, sin volver al servidor: son un puñado de aparatos y caben enteros en la
/// respuesta.
/// </remarks>
public sealed record EquipoOpcionDto(
    long Id,
    string Codigo,
    string Nombre,
    long PlantaId,
    string Planta);
