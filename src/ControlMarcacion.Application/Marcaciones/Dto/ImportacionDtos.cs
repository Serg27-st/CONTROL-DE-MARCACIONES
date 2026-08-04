namespace ControlMarcacion.Application.Marcaciones.Dto;

/// <summary>
/// Una marcación tal como viene del archivo, antes de resolverse contra el padrón.
/// </summary>
/// <remarks>
/// <see cref="IdMarcador"/> es <b>texto</b>: el archivo real trae identificadores como
/// "0014" junto a "14", y convertirlos a número los haría colisionar.
/// </remarks>
public sealed record MarcacionLeida(
    string IdMarcador,
    DateTime FechaHora,
    string? ModoVerificacion,
    string? TipoMarcacion,
    int NumeroLinea,
    string LineaOriginal);

/// <summary>Una línea que no se pudo interpretar.</summary>
public sealed record LineaRechazada(int NumeroLinea, string ContenidoLinea, string Motivo);

/// <summary>Resultado de leer el archivo, sin tocar todavía la base de datos.</summary>
public sealed record ResultadoLectura(
    IReadOnlyList<MarcacionLeida> Marcaciones,
    IReadOnlyList<LineaRechazada> Rechazadas)
{
    public int TotalLineas => Marcaciones.Count + Rechazadas.Count;
}

/// <summary>
/// Identificador del marcador que aparece en el archivo pero no está asignado a nadie.
/// </summary>
/// <remarks>
/// No es un error del archivo: es un trabajador que falta registrar, o una asignación
/// cuya vigencia no cubre esas fechas. Se reporta aparte para que RRHH sepa exactamente
/// a quién dar de alta.
/// </remarks>
public sealed record IdMarcadorSinAsignar(
    string IdMarcador,
    int Marcaciones,
    DateOnly PrimeraFecha,
    DateOnly UltimaFecha);

/// <summary>Resultado completo de una importación ya persistida.</summary>
public sealed record ResultadoImportacion(
    long ImportacionId,
    string NombreArchivo,
    int FilasLeidas,
    int FilasInsertadas,
    int FilasDuplicadas,
    int FilasRechazadas,
    IReadOnlyList<LineaRechazada> Rechazadas,
    IReadOnlyList<IdMarcadorSinAsignar> IdsSinAsignar)
{
    public bool HuboProblemas => FilasRechazadas > 0 || IdsSinAsignar.Count > 0;
}

/// <summary>Aviso de que el mismo archivo ya se cargó antes.</summary>
public sealed record ImportacionPrevia(
    long ImportacionId,
    string NombreArchivo,
    DateTime FechaCarga,
    string CargadoPorUsuario,
    int FilasInsertadas);

/// <summary>Fila del historial de importaciones.</summary>
public sealed record ImportacionHistorialDto(
    long Id,
    string NombreArchivo,
    DateTime FechaCarga,
    string CargadoPorUsuario,
    int FilasLeidas,
    int FilasInsertadas,
    int FilasDuplicadas,
    int FilasRechazadas);
