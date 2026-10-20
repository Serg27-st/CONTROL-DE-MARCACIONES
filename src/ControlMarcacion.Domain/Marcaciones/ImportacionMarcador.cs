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

    /// <summary>Equipo del que se extrajo el archivo.</summary>
    /// <remarks>
    /// Lo declara quien carga, porque el archivo no siempre lo dice: el TXT del G3 no trae
    /// ninguna columna con el aparato de origen. Queda registrado aquí para que "¿esta
    /// marcación de dónde salió?" tenga respuesta completa —qué equipo, qué archivo, quién
    /// lo subió— y no solo el nombre del archivo.
    /// </remarks>
    public long EquipoId { get; set; }

    public Catalogos.EquipoMarcador? Equipo { get; set; }

    /// <summary>Hash SHA-256 del contenido, para detectar recargas del mismo archivo.</summary>
    public string HashContenido { get; set; } = null!;

    /// <summary>
    /// Serie del aparato que traía el archivo —en la cabecera del respaldo o en el nombre
    /// del attlog—, o nula si no traía ninguna.
    /// </summary>
    public string? SerieArchivo { get; set; }

    /// <summary>Cómo se comprobó que el archivo salió del equipo declarado.</summary>
    /// <remarks>
    /// El aviso de la pantalla de resultado se pierde; esto queda. Al investigar meses
    /// después un día mal atribuido, hay que poder ver qué cargas nunca se verificaron y
    /// cuál enseñó la serie a su equipo.
    /// </remarks>
    public OrigenCarga OrigenVerificado { get; set; }

    public DateTime FechaCarga { get; set; }

    public string CargadoPorUsuario { get; set; } = null!;

    public int FilasLeidas { get; set; }

    public int FilasInsertadas { get; set; }

    /// <summary>Duplicadas ignoradas: ya existían. No son un error.</summary>
    public int FilasDuplicadas { get; set; }

    /// <summary>Rechazadas por dato inválido, corrupto o no interpretable.</summary>
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
/// Qué respalda que un archivo salió del equipo que declaró quien lo cargó.
/// </summary>
public enum OrigenCarga
{
    /// <summary>
    /// El archivo no traía serie —se renombró a mano—: se aceptó con el equipo declarado,
    /// sin nada que lo contrastara. También es el valor de las cargas anteriores a esta
    /// comprobación.
    /// </summary>
    NoVerificado = 0,

    /// <summary>La serie de la cabecera del respaldo binario es la del equipo.</summary>
    PorContenido,

    /// <summary>La serie del nombre del archivo (<c>SERIE_attlog</c>) es la del equipo.</summary>
    PorNombre,

    /// <summary>
    /// El equipo no tenía serie y la tomó de este archivo. Nada la contrastó: si más adelante
    /// otra carga choca con esa serie, esta es la primera que hay que revisar.
    /// </summary>
    SerieAprendida
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
