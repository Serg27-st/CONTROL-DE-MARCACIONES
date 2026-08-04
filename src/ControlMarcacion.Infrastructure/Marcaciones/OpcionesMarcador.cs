namespace ControlMarcacion.Infrastructure.Marcaciones;

/// <summary>
/// Plantilla de lectura del archivo del marcador. Se enlaza a la sección "Marcador"
/// de la configuración.
/// </summary>
/// <remarks>
/// Es configuración y no constantes porque el firmware del equipo puede cambiar el
/// separador o el formato de fecha, y eso no debería obligar a recompilar.
/// </remarks>
public sealed class OpcionesMarcador
{
    public const string Seccion = "Marcador";

    /// <summary>Separador de columnas. El G3 real usa tabulador.</summary>
    public string Separador { get; set; } = "\t";

    /// <summary>
    /// Página de códigos para leer el archivo. 1252 cubre los acentos del español
    /// si algún firmware llegara a incluir nombres.
    /// </summary>
    public int CodigoPagina { get; set; } = 1252;

    /// <summary>
    /// Formatos de fecha aceptados, en orden de intento. El archivo real usa el primero.
    /// </summary>
    public IList<string> FormatosFecha { get; set; } =
    [
        "yyyy-MM-dd HH:mm:ss",
        "dd/MM/yyyy HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "dd/MM/yyyy HH:mm"
    ];

    /// <summary>
    /// Dos marcaciones del mismo trabajador dentro de esta ventana se consideran la misma.
    /// </summary>
    /// <remarks>
    /// En el archivo real hay casos de doble fichada con 9 a 19 segundos de diferencia:
    /// el trabajador pasa el dedo dos veces porque no vio la confirmación. Sin esta
    /// ventana, esa segunda marca se tomaría como la salida y arruinaría el cálculo.
    /// </remarks>
    public int VentanaDeduplicacionSegundos { get; set; } = 60;

    /// <summary>
    /// Antigüedad máxima aceptada. Protege contra un equipo con la fecha mal configurada,
    /// que reportaría marcaciones del año 2000.
    /// </summary>
    public int DiasAntiguedadMaxima { get; set; } = 400;
}
