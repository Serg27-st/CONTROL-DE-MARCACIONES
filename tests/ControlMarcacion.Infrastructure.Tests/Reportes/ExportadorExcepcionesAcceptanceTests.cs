using ClosedXML.Excel;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Infrastructure.Reportes;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Reportes;

/// <summary>Contratos EXPQA-01..10 sobre el XLSX real generado por ClosedXML.</summary>
public sealed class ExportadorExcepcionesAcceptanceTests
{
    private static MetadatosReporte Metadatos() => new(
        "Piloto QA", new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 6),
        "QA-2026-11", "Piloto noviembre", true, "EMPRESA QA / UNIDAD A",
        new DateTime(2026, 11, 7, 9, 0, 0), "qa",
        new EstadoCalculoTareo(new DateTime(2026, 11, 6, 23, 0, 0), null, null));

    private static ExcepcionDto FilaCompleta() => new(
        1001, "ALAN QA", "1001", "EQ-QA", new DateOnly(2026, 11, 3), "DIU-QA",
        new DateTime(2026, 11, 3, 7, 10, 0),
        new DateTime(2026, 11, 3, 17, 22, 0),
        EstadoTareo.Tardanza, 10, 0, "Ingreso posterior al inicio teórico");

    private static XLWorkbook Exportar(params ExcepcionDto[] filas)
    {
        var bytes = new ExportadorReportesExcel().ExportarExcepciones(Metadatos(), filas);
        return new XLWorkbook(new MemoryStream(bytes));
    }

    [Fact]
    public void EXPQA_01_02_03_04_05_08_10_encabezados_y_celdas_corresponden_sin_desplazamiento()
    {
        using var libro = Exportar(FilaCompleta());
        var hoja = libro.Worksheet("Excepciones");

        string[] esperados =
        [
            "Trabajador", "ID marcador · equipo", "Fecha", "Turno", "Entrada", "Salida",
            "Estado", "Minutos tardanza", "Minutos salida anticipada", "Observación"
        ];
        for (var columna = 1; columna <= esperados.Length; columna++)
        {
            hoja.Cell(8, columna).GetString().ShouldBe(esperados[columna - 1]);
        }

        hoja.Cell("A9").GetString().ShouldBe("ALAN QA");
        hoja.Cell("B9").GetString().ShouldBe("1001 · EQ-QA");
        hoja.Cell("C9").GetDateTime().ShouldBe(new DateTime(2026, 11, 3));
        hoja.Cell("C9").DataType.ShouldBe(XLDataType.DateTime);
        hoja.Cell("C9").Style.DateFormat.Format.ShouldBe("dd/MM/yyyy");
        hoja.Cell("D9").GetString().ShouldBe("DIU-QA");
        hoja.Cell("E9").GetFormattedString().ShouldBe("07:10");
        hoja.Cell("F9").GetFormattedString().ShouldBe("17:22");
        hoja.Cell("G9").GetString().ShouldBe("Tardanza");
        hoja.Cell("H9").GetDouble().ShouldBe(10);
        hoja.Cell("I9").GetDouble().ShouldBe(0);
        hoja.Cell("J9").GetString().ShouldBe("Ingreso posterior al inicio teórico");
        hoja.Cell("K9").IsEmpty().ShouldBeTrue();
    }

    [Fact]
    public void EXPQA_06_07_campos_opcionales_y_multiples_filas_conservan_su_columna()
    {
        var incompleta = new ExcepcionDto(
            1002, "CARLOS QA", null, null, new DateOnly(2026, 11, 4), null,
            new DateTime(2026, 11, 4, 7, 0, 0), null,
            EstadoTareo.Incompleto, 0, 0, null);

        using var libro = Exportar(FilaCompleta(), incompleta);
        var hoja = libro.Worksheet("Excepciones");

        hoja.Cell("A10").GetString().ShouldBe("CARLOS QA");
        hoja.Cell("B10").GetString().ShouldBe("—");
        hoja.Cell("C10").GetDateTime().ShouldBe(new DateTime(2026, 11, 4));
        hoja.Cell("D10").GetString().ShouldBe("—");
        hoja.Cell("E10").GetFormattedString().ShouldBe("07:00");
        hoja.Cell("F10").GetString().ShouldBe("—");
        hoja.Cell("G10").GetString().ShouldBe("Marcación incompleta");
        hoja.Cell("J10").GetString().ShouldBe(string.Empty);
    }

    [Fact]
    public void EXPQA_09_otros_reportes_conservan_su_formato_publico()
    {
        var resumen = new ResumenTareoDto(
            1001, "ALAN QA", "1001", "EQ-QA", "EMPRESA QA", "UNIDAD A", null, null,
            "OPERARIO", 5, 0, 0, 0, 0, 0, 0, 3110, 23, 0, 0, 0, 0m, 0m);
        var bytes = new ExportadorReportesExcel().ExportarResumenTareo(Metadatos(), [resumen]);

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheet("Resumen de tareo");
        hoja.Cell("A8").GetString().ShouldBe("Trabajador");
        hoja.Cell("A9").GetString().ShouldBe("ALAN QA");
        hoja.Cell("N9").GetDouble().ShouldBe(Math.Round(3110 / 60d, 2));
    }
}
