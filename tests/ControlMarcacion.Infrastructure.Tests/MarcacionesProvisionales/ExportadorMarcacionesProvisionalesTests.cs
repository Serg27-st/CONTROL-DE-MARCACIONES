using ClosedXML.Excel;
using ControlMarcacion.Application.MarcacionesProvisionales.Dto;
using ControlMarcacion.Infrastructure.MarcacionesProvisionales;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.MarcacionesProvisionales;

public sealed class ExportadorMarcacionesProvisionalesTests
{
    private static readonly DateOnly Dia = new(2026, 7, 15);

    private readonly ExportadorMarcacionesProvisionales _exportador = new();

    [Fact]
    public void Genera_libro_con_dos_hojas()
    {
        var jornadas = CrearJornadas(1);

        var bytes = _exportador.Exportar(jornadas, "DET-AL - Detergentes A-L", Dia, Dia, "admin");

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        libro.Worksheets.Count.ShouldBe(2);
        libro.Worksheets.First().Name.ShouldBe("Marcaciones");
        libro.Worksheets.Last().Name.ShouldBe("Trazabilidad");
    }

    [Fact]
    public void Hoja_marcaciones_tiene_13_columnas_de_encabezado()
    {
        var jornadas = CrearJornadas(1);

        var bytes = _exportador.Exportar(jornadas, "DET-AL", Dia, Dia, "admin");

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheet("Marcaciones");
        hoja.Cell(1, 1).GetString().ShouldBe("Fecha");
        hoja.Cell(1, 13).GetString().ShouldBe("Observación");
        hoja.Cell(1, 14).IsEmpty().ShouldBeTrue();
    }

    [Fact]
    public void Escribe_datos_de_jornada_en_fila()
    {
        var jornada = new JornadaProvisionalDto(
            Dia, 1, "DET-AL", "14",
            100, "41300001", "GARCIA", "LOPEZ", "JUAN",
            new TimeOnly(7, 0), new TimeOnly(19, 0),
            "DIU", "Diurno 12h",
            "PLANTA DETERGENTES", "AEROSOLES", "PE21430001",
            null, 2, [new TimeOnly(7, 0), new TimeOnly(19, 0)]);

        var bytes = _exportador.Exportar([jornada], "DET-AL", Dia, Dia, "admin");

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheet("Marcaciones");
        hoja.Cell(2, 1).GetString().ShouldBe("15/07/2026");
        hoja.Cell(2, 2).GetString().ShouldBe("14");
        hoja.Cell(2, 3).GetString().ShouldBe("41300001");
        hoja.Cell(2, 4).GetString().ShouldBe("GARCIA");
        hoja.Cell(2, 7).GetString().ShouldBe("07:00");
        hoja.Cell(2, 8).GetString().ShouldBe("19:00");
        hoja.Cell(2, 9).GetString().ShouldBe("Diurno 12h");
        hoja.Cell(2, 10).GetString().ShouldBe("PLANTA DETERGENTES");
    }

    [Fact]
    public void Trabajador_sin_asignar_muestra_texto()
    {
        var jornada = new JornadaProvisionalDto(
            Dia, 1, "DET-AL", "99",
            null, null, null, null, null,
            new TimeOnly(7, 0), null,
            null, null, null, null, null,
            "ID de marcador no asignado", 1, [new TimeOnly(7, 0)]);

        var bytes = _exportador.Exportar([jornada], "DET-AL", Dia, Dia, "admin");

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheet("Marcaciones");
        hoja.Cell(2, 3).GetString().ShouldBe("Sin asignar");
        hoja.Cell(2, 4).GetString().ShouldBe("Sin asignar");
    }

    [Fact]
    public void Trazabilidad_incluye_equipo_y_rango()
    {
        var bytes = _exportador.Exportar([], "DET-AL - Detergentes A-L",
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "coordinador");

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheet("Trazabilidad");
        hoja.Cell(2, 2).GetString().ShouldBe("DET-AL - Detergentes A-L");
        hoja.Cell(3, 2).GetString().ShouldBe("01/07/2026 a 31/07/2026");
        hoja.Cell(5, 2).GetString().ShouldBe("coordinador");
    }

    [Fact]
    public void Trazabilidad_incluye_advertencias()
    {
        var advertencias = new List<string> { "El rango supera los 30 días." };

        var bytes = _exportador.Exportar([], "DET-AL", Dia, Dia, "admin", advertencias);

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheet("Trazabilidad");
        var encontrado = false;
        for (var fila = 1; fila <= 10; fila++)
        {
            if (hoja.Cell(fila, 1).GetString() == "El rango supera los 30 días.")
            {
                encontrado = true;
                break;
            }
        }
        encontrado.ShouldBeTrue();
    }

    [Fact]
    public void Ordena_por_fecha_planta_area_apellidos()
    {
        var j1 = Jornada("ZUNIGA", "RIOS", new DateOnly(2026, 7, 16), "PLANTA 1");
        var j2 = Jornada("ALVAREZ", "DIAZ", new DateOnly(2026, 7, 15), "PLANTA 1");
        var j3 = Jornada("BAUTISTA", "RUIZ", new DateOnly(2026, 7, 15), "PLANTA DETERGENTES");

        var bytes = _exportador.Exportar([j1, j2, j3], "DET-AL", new DateOnly(2026, 7, 15),
            new DateOnly(2026, 7, 16), "admin");

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheet("Marcaciones");
        hoja.Cell(2, 4).GetString().ShouldBe("ALVAREZ");
        hoja.Cell(3, 4).GetString().ShouldBe("BAUTISTA");
        hoja.Cell(4, 4).GetString().ShouldBe("ZUNIGA");
    }

    [Fact]
    public void Lista_vacia_genera_libro_valido()
    {
        var bytes = _exportador.Exportar([], "DET-AL", Dia, Dia, "admin");

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        libro.Worksheets.Count.ShouldBe(2);
        libro.Worksheet("Marcaciones").Cell(2, 1).IsEmpty().ShouldBeTrue();
    }

    [Fact]
    public void Texto_con_formula_queda_neutralizado()
    {
        var jornada = new JornadaProvisionalDto(
            Dia, 1, "DET-AL", "14",
            100, "41300001", "=HYPERLINK(\"http://malo\")", "LOPEZ", "JUAN",
            new TimeOnly(7, 0), new TimeOnly(19, 0),
            "DIU", "Diurno 12h",
            "PLANTA DETERGENTES", "+AREA", "CC",
            null, 2, [new TimeOnly(7, 0), new TimeOnly(19, 0)]);

        var bytes = _exportador.Exportar([jornada], "DET-AL", Dia, Dia, "admin");

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheet("Marcaciones");
        var celdaAp = hoja.Cell(2, 4);
        celdaAp.DataType.ShouldBe(ClosedXML.Excel.XLDataType.Text);
        celdaAp.GetString().ShouldContain("HYPERLINK");

        var celdaArea = hoja.Cell(2, 11);
        celdaArea.DataType.ShouldBe(ClosedXML.Excel.XLDataType.Text);
        celdaArea.GetString().ShouldContain("AREA");
    }

    [Fact]
    public void Area_y_centro_costo_en_columnas_correctas()
    {
        var jornada = new JornadaProvisionalDto(
            Dia, 1, "DET-AL", "14",
            100, "41300001", "GARCIA", "LOPEZ", "JUAN",
            new TimeOnly(7, 0), new TimeOnly(19, 0),
            "DIU", "Diurno 12h",
            "PLANTA DETERGENTES", "AEROSOLES", "PE21430001",
            null, 2, [new TimeOnly(7, 0), new TimeOnly(19, 0)]);

        var bytes = _exportador.Exportar([jornada], "DET-AL", Dia, Dia, "admin");

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheet("Marcaciones");
        hoja.Cell(1, 11).GetString().ShouldBe("Área");
        hoja.Cell(2, 11).GetString().ShouldBe("AEROSOLES");
        hoja.Cell(1, 12).GetString().ShouldBe("Centro de costo");
        hoja.Cell(2, 12).GetString().ShouldBe("PE21430001");
    }

    private static List<JornadaProvisionalDto> CrearJornadas(int cantidad)
    {
        var lista = new List<JornadaProvisionalDto>();
        for (var i = 0; i < cantidad; i++)
        {
            lista.Add(Jornada($"AP{i}", $"AM{i}", Dia, "PLANTA 1"));
        }
        return lista;
    }

    private static JornadaProvisionalDto Jornada(string ap, string am, DateOnly fecha, string planta) =>
        new(fecha, 1, "DET-AL", "14",
            100, "41300001", ap, am, "NOMBRES",
            new TimeOnly(7, 0), new TimeOnly(19, 0),
            "DIU", "Diurno 12h",
            planta, "AREA", "CC",
            null, 2, [new TimeOnly(7, 0), new TimeOnly(19, 0)]);
}
