using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Marcaciones;

/// <summary>
/// Registro de una carga de archivo TXT extraído por USB del marcador ZKTeco G3.
/// </summary>
/// <remarks>
/// Existe para que RRHH pueda responder "¿esta marcación de dónde salió?" sin adivinar.
/// El hash del contenido permite además avisar "este archivo ya se cargó el 12/07 a las
/// 14:30" <b>antes</b> de procesarlo, en vez de descubrirlo por los duplicados rechazados.
/// </remarks>
public class ImportacionMarcador : EntidadBase
{
    public string NombreArchivo { get; set; } = null!;

    /// <summary>Hash SHA-256 del contenido, para detectar recargas del mismo archivo.</summary>
    public string HashContenido { get; set; } = null!;

    public DateTime FechaCarga { get; set; }

    public string CargadoPorUsuario { get; set; } = null!;

    public int FilasLeidas { get; set; }

    public int FilasInsertadas { get; set; }

    /// <summary>Duplicadas ignoradas: ya existían. No son un error.</summary>
    public int FilasDuplicadas { get; set; }

    /// <summary>Rechazadas por dato inválido, ID desconocido o periodo cerrado.</summary>
    public int FilasRechazadas { get; set; }

    /// <summary>
    /// Detalle de los rechazos, con número de línea y motivo. Se muestra al usuario:
    /// un "se cargó correctamente" que oculta 40 filas rechazadas es un fallo grave.
    /// </summary>
    public ICollection<ErrorImportacion> Errores { get; set; } = [];
}

/// <summary>
/// Fila que no pudo importarse, con su motivo.
/// </summary>
public class ErrorImportacion : EntidadBase
{
    public long ImportacionId { get; set; }

    public int NumeroLinea { get; set; }

    public string? ContenidoLinea { get; set; }

    public string Motivo { get; set; } = null!;
}
