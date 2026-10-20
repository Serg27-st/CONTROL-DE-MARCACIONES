namespace ControlMarcacion.Application.Personal.Dto;

/// <summary>
/// Una fila que no se pudo registrar, con el motivo redactado para el usuario.
/// </summary>
/// <param name="Fila">
/// Número de línea en el archivo tal como lo ve el usuario en Excel: la 1 es el
/// encabezado, así que el primer trabajador es la 2. Sin esto el reporte diría
/// "fila 37" y el usuario tendría que contar a mano.
/// </param>
public sealed record ErrorCargaMasiva(int Fila, string Referencia, string Motivo);

/// <summary>
/// ID de marcador que la carga dio de alta en el catálogo de su aparato porque faltaba.
/// </summary>
/// <remarks>
/// Se lista en el resultado, agrupado por aparato, para que un error de tipeo en el padrón
/// salte a la vista en vez de quedar registrado como un número más.
/// </remarks>
public sealed record MarcadorRegistradoEnCarga(int Fila, string Codigo, string Equipo);

/// <summary>
/// Resultado de procesar el archivo.
/// </summary>
/// <remarks>
/// Las filas válidas se registran aunque otras fallen. Es deliberado: en un padrón de
/// 300 personas, rechazar el archivo entero por dos DNI repetidos obliga a rehacer la
/// carga completa, y en la práctica termina en que alguien parte el archivo en pedazos
/// hasta que pasa. Se registra lo que se puede y se informa con precisión lo que no.
/// </remarks>
public sealed record ResultadoCargaMasiva(
    string NombreArchivo,
    int FilasLeidas,
    int Creados,
    IReadOnlyList<ErrorCargaMasiva> Errores,
    bool SoloValidacion,
    /// <summary>ID de marcador que la carga registró en el catálogo porque faltaban.</summary>
    IReadOnlyList<MarcadorRegistradoEnCarga> MarcadoresRegistrados,
    /// <summary>
    /// Filas rechazadas porque su número de marcador no está en el catálogo de su aparato y
    /// la carga se hizo sin registrar los que faltan. La pantalla lo usa para decir que basta
    /// con volver a subir el archivo con esa casilla marcada.
    /// </summary>
    int FilasSinMarcadorEnCatalogo)
{
    public bool TieneErrores => Errores.Count > 0;

    public int Rechazados => Errores.Count;
}
