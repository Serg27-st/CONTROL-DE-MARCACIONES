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

    /// <summary>
    /// La carga quedó sin efecto: sus marcaciones existen pero el tareo no las cuenta.
    /// </summary>
    /// <remarks>
    /// Es la alternativa a borrar, y existe porque borrar aquí está prohibido: las
    /// marcaciones crudas son el dato duro del sistema y son inmutables. Cuando alguien
    /// carga el archivo equivocado —una prueba, un mes repetido, un TXT de otra planta—,
    /// las filas no se pueden eliminar sin destruir la trazabilidad de "¿esta marcación de
    /// dónde salió?". Anular deja el archivo, sus filas y sus errores intactos y
    /// consultables, y solo le quita el efecto sobre el cálculo.
    ///
    /// Es reversible: reactivar la carga vuelve a incluir sus marcaciones. Lo que no se
    /// recupera nunca es lo borrado, y por eso no se borra.
    /// </remarks>
    public bool Anulada { get; set; }

    /// <summary>Por qué se anuló. Obligatorio al anular: una anulación sin motivo no se puede auditar.</summary>
    public string? MotivoAnulacion { get; set; }

    public DateTime? AnuladaEn { get; set; }

    public string? AnuladaPorUsuario { get; set; }

    /// <summary>
    /// Deja la carga sin efecto sobre el cálculo, conservando todas sus filas.
    /// </summary>
    public void Anular(string motivo, string usuario, DateTime momento)
    {
        if (Anulada)
        {
            throw new ReglaNegocioException(
                $"La carga ya se anuló el {AnuladaEn:dd/MM/yyyy HH:mm}.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ReglaNegocioException(
                "Indique por qué se anula la carga: es lo que explicará, meses después, " +
                "por qué esas marcaciones no cuentan.");
        }

        Anulada = true;
        MotivoAnulacion = motivo.Trim();
        AnuladaEn = momento;
        AnuladaPorUsuario = usuario;
        ModificadoPor = usuario;
    }

    /// <summary>Vuelve a incluir la carga en el cálculo.</summary>
    public void Reactivar(string usuario)
    {
        if (!Anulada)
        {
            throw new ReglaNegocioException("La carga no está anulada.");
        }

        Anulada = false;
        MotivoAnulacion = null;
        AnuladaEn = null;
        AnuladaPorUsuario = null;
        ModificadoPor = usuario;
    }
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
