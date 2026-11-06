using System.Globalization;
using ClosedXML.Excel;
using ControlMarcacion.Application.MarcacionesProvisionales;
using ControlMarcacion.Application.MarcacionesProvisionales.Dto;
using ControlMarcacion.Infrastructure.Reportes;

namespace ControlMarcacion.Infrastructure.MarcacionesProvisionales;

internal sealed class ExportadorMarcacionesProvisionales : IExportadorMarcacionesProvisionales
{
    private static readonly string[] Columnas =
    [
        "Fecha",
        "ID marcador",
        "DNI",
        "Apellido paterno",
        "Apellido materno",
        "Nombres",
        "Hora ingreso",
        "Hora salida",
        "Turno",
        "Unidad",
        "Área",
        "Centro de costo",
        "Observación"
    ];

    public byte[] Exportar(
        IReadOnlyList<JornadaProvisionalDto> jornadas,
        string equipoDescripcion,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        string? usuario,
        IReadOnlyList<string>? advertencias = null)
    {
        using var libro = new XLWorkbook();

        var ordenadas = jornadas
            .OrderBy(j => j.Fecha)
            .ThenBy(j => j.Planta)
            .ThenBy(j => j.Area)
            .ThenBy(j => j.ApellidoPaterno)
            .ThenBy(j => j.ApellidoMaterno)
            .ThenBy(j => j.Nombres)
            .ToList();

        EscribirHojaDatos(libro, ordenadas);
        EscribirHojaTrazabilidad(libro, equipoDescripcion, fechaInicio, fechaFin,
            jornadas.Count, usuario, advertencias);

        using var flujo = new MemoryStream();
        libro.SaveAs(flujo);
        return flujo.ToArray();
    }

    private static void EscribirHojaDatos(XLWorkbook libro, List<JornadaProvisionalDto> jornadas)
    {
        var hoja = libro.Worksheets.Add("Marcaciones");

        for (var i = 0; i < Columnas.Length; i++)
        {
            var celda = hoja.Cell(1, i + 1);
            celda.Value = Columnas[i];
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
            celda.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        var fila = 2;
        foreach (var j in jornadas)
        {
            var col = 1;
            hoja.Cell(fila, col).Value = j.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            col++;
            hoja.Cell(fila, col++).Value = Sanear(j.IdMarcador);
            hoja.Cell(fila, col++).Value = Sanear(j.TrabajadorId.HasValue ? j.NumeroDocumento : "Sin asignar");
            hoja.Cell(fila, col++).Value = Sanear(j.TrabajadorId.HasValue ? j.ApellidoPaterno : "Sin asignar");
            hoja.Cell(fila, col++).Value = Sanear(j.TrabajadorId.HasValue ? j.ApellidoMaterno : "Sin asignar");
            hoja.Cell(fila, col++).Value = Sanear(j.TrabajadorId.HasValue ? j.Nombres : "Sin asignar");

            if (j.HoraIngreso.HasValue)
            {
                hoja.Cell(fila, col).Value = j.HoraIngreso.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
            }
            col++;

            if (j.HoraSalida.HasValue)
            {
                hoja.Cell(fila, col).Value = j.HoraSalida.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
            }
            col++;

            hoja.Cell(fila, col++).Value = Sanear(j.TurnoNombre);
            hoja.Cell(fila, col++).Value = Sanear(j.Planta);
            hoja.Cell(fila, col++).Value = Sanear(j.Area);
            hoja.Cell(fila, col++).Value = Sanear(j.CentroCosto);
            hoja.Cell(fila, col).Value = Sanear(j.Observacion);
            fila++;
        }

        hoja.RangeUsed()?.SetAutoFilter();
        hoja.SheetView.FreezeRows(1);
        hoja.Columns().AdjustToContents(1, Math.Min(fila, 100));
    }

    private static void EscribirHojaTrazabilidad(
        XLWorkbook libro,
        string equipoDescripcion,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        int totalJornadas,
        string? usuario,
        IReadOnlyList<string>? advertencias)
    {
        var hoja = libro.Worksheets.Add("Trazabilidad");
        var fila = 1;

        Rotulo(hoja, fila, "Fecha de generación", DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
        fila++;
        Rotulo(hoja, fila, "Equipo", equipoDescripcion);
        fila++;
        Rotulo(hoja, fila, "Rango consultado",
            string.Format(CultureInfo.InvariantCulture, "{0:dd/MM/yyyy} a {1:dd/MM/yyyy}", fechaInicio, fechaFin));
        fila++;
        Rotulo(hoja, fila, "Total de jornadas exportadas", totalJornadas.ToString(CultureInfo.InvariantCulture));
        fila++;

        if (!string.IsNullOrWhiteSpace(usuario))
        {
            Rotulo(hoja, fila, "Generado por", usuario);
            fila++;
        }

        if (advertencias is { Count: > 0 })
        {
            fila++;
            hoja.Cell(fila, 1).Value = "Advertencias";
            hoja.Cell(fila, 1).Style.Font.Bold = true;
            fila++;

            foreach (var adv in advertencias)
            {
                hoja.Cell(fila, 1).Value = Sanear(adv);
                fila++;
            }
        }

        hoja.Columns().AdjustToContents();
    }

    private static void Rotulo(IXLWorksheet hoja, int fila, string etiqueta, string valor)
    {
        hoja.Cell(fila, 1).Value = etiqueta;
        hoja.Cell(fila, 1).Style.Font.Bold = true;
        hoja.Cell(fila, 2).Value = valor;
    }

    private static string? Sanear(string? valor) => SanitizadorExcel.Neutralizar(valor);
}
