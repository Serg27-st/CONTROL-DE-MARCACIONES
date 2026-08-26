using System.Globalization;
using ClosedXML.Excel;
using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Infrastructure.Personal;

/// <summary>
/// Lee la primera hoja de un libro de Excel como una matriz de texto.
/// </summary>
/// <remarks>
/// GCA trabaja en Excel: tanto el padrón de personal como el resumen de fin de mes salen de
/// su planilla en <c>.xlsx</c>. Obligar a "guardar como CSV" antes de cada carga es un paso
/// manual que se olvida, y que además rompe los documentos con ceros a la izquierda cuando
/// Excel decide que son números.
///
/// Devuelve <b>texto</b>, igual que <see cref="LectorCsv"/>, para que la carga masiva no
/// sepa de dónde vinieron las filas y siga aplicando las mismas reglas a las dos rutas.
///
/// Dos conversiones merecen explicación:
/// <list type="bullet">
///   <item><b>Fechas</b>: Excel las guarda como número de serie. Se emiten como
///   <c>dd/MM/yyyy</c>, que es uno de los formatos que el lector de la carga acepta;
///   dejarlas como "45678" haría fallar todas las filas.</item>
///   <item><b>Números</b>: se emiten con cultura invariante y sin separador de miles, para
///   que un sueldo no llegue como "1,200.00" en una máquina y "1.200,00" en otra.</item>
/// </list>
/// </remarks>
internal static class LectorExcel
{
    /// <summary>Filas máximas de un padrón antes de tratarlo como archivo anómalo (anti-DoS).</summary>
    private const int MaxFilas = 50_000;

    /// <summary>Columnas máximas: el padrón tiene decenas de columnas, no cientos.</summary>
    private const int MaxColumnas = 200;

    /// <summary>Los dos primeros bytes de un .xlsx: es un ZIP, y todo ZIP empieza por "PK".</summary>
    public static bool EsLibroExcel(Stream contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        if (contenido.Length < 2)
        {
            return false;
        }

        var posicion = contenido.Position;
        contenido.Position = 0;

        var primero = contenido.ReadByte();
        var segundo = contenido.ReadByte();

        contenido.Position = posicion;

        return primero == 'P' && segundo == 'K';
    }

    public static IReadOnlyList<string[]> Leer(Stream contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        contenido.Position = 0;

        using var libro = new XLWorkbook(contenido);

        var hoja = libro.Worksheets.FirstOrDefault()
            ?? throw new ReglaNegocioException("El archivo de Excel no tiene ninguna hoja.");

        var usado = hoja.RangeUsed();

        if (usado is null)
        {
            return [];
        }

        var ultimaColumna = usado.LastColumn().ColumnNumber();
        var ultimaFila = usado.LastRow().RowNumber();

        // Tope defensivo antes de recorrer celda por celda. Un padrón de personal son cientos o
        // pocos miles de filas, no millones; un .xlsx es un ZIP y uno malicioso —o corrupto—
        // puede declarar un rango enorme que, al materializarlo en una lista de arreglos de
        // texto, agota la memoria de la única PC. La subida ya está acotada a 5 MB en el
        // controlador; esto acota el otro extremo, el del rango ya abierto.
        if (ultimaFila > MaxFilas || ultimaColumna > MaxColumnas)
        {
            throw new ReglaNegocioException(
                $"El archivo declara un rango demasiado grande ({ultimaFila:N0} fila(s) × " +
                $"{ultimaColumna} columna(s)); el máximo es {MaxFilas:N0} × {MaxColumnas}. " +
                "Revise que sea el archivo del padrón y no otro.");
        }

        var filas = new List<string[]>();

        foreach (var fila in usado.RowsUsed())
        {
            var campos = new string[ultimaColumna];
            var hayAlgo = false;

            for (var c = 1; c <= ultimaColumna; c++)
            {
                var texto = Texto(fila.Cell(c));
                campos[c - 1] = texto;

                if (texto.Length > 0)
                {
                    hayAlgo = true;
                }
            }

            // Una fila totalmente vacía en medio del listado no es un trabajador sin datos:
            // es una fila que alguien dejó ahí. Incluirla generaría un error por cada una.
            if (hayAlgo)
            {
                filas.Add(campos);
            }
        }

        return filas;
    }

    private static string Texto(IXLCell celda)
    {
        if (celda.IsEmpty())
        {
            return string.Empty;
        }

        if (celda.DataType == XLDataType.DateTime && celda.Value.IsDateTime)
        {
            return celda.Value.GetDateTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        if (celda.DataType == XLDataType.Number && celda.Value.IsNumber)
        {
            return celda.Value.GetNumber().ToString("0.####", CultureInfo.InvariantCulture);
        }

        return celda.GetFormattedString().Trim();
    }
}
