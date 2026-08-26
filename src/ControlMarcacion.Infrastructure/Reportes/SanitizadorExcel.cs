namespace ControlMarcacion.Infrastructure.Reportes;

/// <summary>
/// Neutraliza la inyección de fórmulas en las celdas de texto de un export.
/// </summary>
/// <remarks>
/// Excel, LibreOffice y Google Sheets interpretan como <b>fórmula</b> cualquier celda cuyo
/// texto empiece por <c>= + - @</c> (o por tabulador / retorno de carro). Un nombre de
/// trabajador como <c>=HYPERLINK("http://malo","clic")</c> —que puede entrar por la carga
/// masiva de personal desde un CSV/XLSX— se ejecutaría al abrir el archivo en la máquina de
/// planilla.
///
/// Hoy <b>no es explotable</b> por esta vía: ClosedXML (API tipada, ≥0.97) escribe los strings
/// como texto, no como fórmula, así que un <c>=…</c> sale literal. Esto es defensa en
/// profundidad —y sí protege si el archivo se convierte a CSV aguas abajo, donde ya no hay tipo
/// que valga—: antepone un apóstrofo, que Excel muestra como texto y no imprime.
///
/// Solo se aplica a texto libre de origen humano (nombres, observaciones, diagnóstico, áreas…),
/// nunca a los títulos de columna ni a los números o fechas, que no pueden empezar por esos
/// caracteres.
/// </remarks>
internal static class SanitizadorExcel
{
    private static readonly char[] Peligrosos = ['=', '+', '-', '@', '\t', '\r', '\n'];

    /// <summary>
    /// Devuelve el valor tal cual, o con un apóstrofo delante si empieza por un carácter que
    /// dispararía una fórmula. Null y vacío pasan sin tocar.
    /// </summary>
    public static string? Neutralizar(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return valor;
        }

        return Array.IndexOf(Peligrosos, valor[0]) >= 0 ? "'" + valor : valor;
    }
}
