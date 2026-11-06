using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ControlMarcacion.Infrastructure.Personal;

namespace ControlMarcacion.Infrastructure.Programacion;

/// <summary>
/// Adaptador exclusivo de programación laboral para fechas OpenXML <c>t="d"</c>.
/// ClosedXML 0.105.1 no materializa fechas ISO con fracciones y sufijo Z; se normalizan
/// en una copia en memoria antes de delegar al lector común. Las celdas nativas numéricas,
/// los textos y el archivo recibido permanecen intactos.
/// </summary>
internal static class LectorExcelProgramacion
{
    private static readonly string[] FormatosFechaHoraIso =
    [
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"
    ];

    public static IReadOnlyList<string[]> Leer(Stream contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        using var normalizado = NormalizarFechasOpenXml(contenido);
        return LectorExcel.Leer(normalizado);
    }

    private static MemoryStream NormalizarFechasOpenXml(Stream contenido)
    {
        contenido.Position = 0;
        using var origen = new ZipArchive(contenido, ZipArchiveMode.Read, leaveOpen: true);
        var salida = new MemoryStream();

        using (var destino = new ZipArchive(salida, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entrada in origen.Entries)
            {
                var nueva = destino.CreateEntry(entrada.FullName, CompressionLevel.Optimal);
                nueva.LastWriteTime = entrada.LastWriteTime;

                using var lectura = entrada.Open();
                using var escritura = nueva.Open();

                if (EsHoja(entrada.FullName))
                {
                    var documento = XDocument.Load(lectura, LoadOptions.PreserveWhitespace);
                    NormalizarCeldasFecha(documento);
                    documento.Save(escritura, SaveOptions.DisableFormatting);
                }
                else
                {
                    lectura.CopyTo(escritura);
                }
            }
        }

        salida.Position = 0;
        return salida;
    }

    private static bool EsHoja(string ruta) =>
        ruta.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)
        && ruta.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private static void NormalizarCeldasFecha(XDocument documento)
    {
        foreach (var celda in documento.Descendants().Where(e => e.Name.LocalName == "c"
                     && string.Equals((string?)e.Attribute("t"), "d", StringComparison.OrdinalIgnoreCase)))
        {
            var valor = celda.Elements().FirstOrDefault(e => e.Name.LocalName == "v")?.Value ?? string.Empty;
            var texto = NormalizarFechaIso(valor);

            celda.SetAttributeValue("t", "inlineStr");
            celda.Elements().Where(e => e.Name.LocalName is "v" or "is").Remove();
            var espacio = celda.Name.Namespace;
            celda.Add(new XElement(espacio + "is", new XElement(espacio + "t", texto)));
        }
    }

    private static string NormalizarFechaIso(string valor)
    {
        if (DateOnly.TryParseExact(
                valor,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var fecha))
        {
            return fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (DateTimeOffset.TryParseExact(
                valor,
                FormatosFechaHoraIso,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var fechaHora)
            && fechaHora.TimeOfDay == TimeSpan.Zero)
        {
            return DateOnly.FromDateTime(fechaHora.DateTime)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        // Se conserva el valor para que el validador de la fila informe columna, valor y
        // formatos aceptados, en lugar de dejar que ClosedXML arroje FormatException.
        return valor;
    }
}
