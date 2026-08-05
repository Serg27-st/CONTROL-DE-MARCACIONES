using ClosedXML.Excel;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Infrastructure.Reportes;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Reportes;

/// <summary>
/// Verifica que el libro del tareo final tenga la estructura del archivo modelo de GCA
/// (<c>TAREO NOVIEMBRE 2024_PLANTA 1.xls</c>), celda por celda.
/// </summary>
/// <remarks>
/// Estos tests son deliberadamente literales sobre posiciones: el archivo lo consume el
/// área de planilla con un proceso armado alrededor de esas columnas. Mover una columna sin
/// avisar rompe su trabajo, y un test que solo comprobara "hay tres hojas" no lo detectaría.
///
/// La especificación está en <c>docs/04-formato-tareo-final.md</c>.
/// </remarks>
public class ExportadorTareoFinalTests
{
    private static readonly DateOnly Inicio = new(2024, 11, 1);
    private static readonly DateOnly Fin = new(2024, 11, 30);

    private static MetadatosReporte Metadatos() => new(
        "2024-11", "Noviembre 2024", Inicio, Fin, PeriodoAbierto: false,
        "Todos los trabajadores", new DateTime(2024, 12, 2, 9, 0, 0), "rrhh");

    private static FilaTareoFinalDto Fila() => new()
    {
        Numero = 1,
        Dni = "07436907",
        ApellidosYNombres = "ACHO PIPA, MARSEBITH",
        Planta = "PLANTA 1",
        DescripcionCeco = "Envasado de Aerosoles/Insecticidas",
        CentroCosto = "AEROSOLES",
        FechaIngreso = new DateOnly(2023, 1, 3),
        DiasFalta = 2,
        DiasFaltaJustificada = 1,
        DiasVacaciones = 5,
        MinutosTardanza = 33,
        MinutosOrdinarios = 14_400,
        MinutosExtra25 = 1_002,
        MinutosExtra35 = 0,
        MinutosNoLaborable = 435,
        MinutosNocturnos = 5_760,
        MinutosExtra25Nocturnos = 802,
        MinutosExtra35Nocturnos = 0,
        MinutosNoLaborableNocturnos = 120
    };

    private static TareoFinalDto Tareo(
        IReadOnlyList<FilaTareoFinalDto>? filas = null,
        IReadOnlyList<FilaDescansoMedicoDto>? descansos = null,
        IReadOnlyList<FilaVacacionesDto>? vacaciones = null,
        IReadOnlyList<FilaLicenciaDto>? licencias = null) =>
        new("CA OUTSOURCING SAC", "NOVIEMBRE", Inicio, Fin,
            filas ?? [Fila()], descansos ?? [], vacaciones ?? [], licencias ?? []);

    private static XLWorkbook Exportar(TareoFinalDto tareo)
    {
        var bytes = new ExportadorReportesExcel().ExportarTareoFinal(Metadatos(), tareo);
        return new XLWorkbook(new MemoryStream(bytes));
    }

    // ------------------------------------------------------------------

    [Fact]
    public void El_libro_trae_las_tres_hojas_del_modelo_y_en_ese_orden()
    {
        using var libro = Exportar(Tareo());

        libro.Worksheets.Select(h => h.Name).ShouldBe(
            ["Tareo", "Descanso Medico", "Vacaciones-Licencias"]);
    }

    [Fact]
    public void El_encabezado_lleva_razon_social_mes_y_rango_del_periodo()
    {
        using var libro = Exportar(Tareo());
        var hoja = libro.Worksheet("Tareo");

        hoja.Cell("A1").GetString().ShouldBe("CA OUTSOURCING SAC");
        hoja.Cell("A2").GetString().ShouldBe("TAREO MENSUAL (OPERARIOS)");
        hoja.Cell("A3").GetString().ShouldBe("MES: NOVIEMBRE");
        hoja.Cell("AG3").GetString().ShouldBe("DEL");
        hoja.Cell("AH3").GetDateTime().ShouldBe(new DateTime(2024, 11, 1));
        hoja.Cell("AK3").GetDateTime().ShouldBe(new DateTime(2024, 11, 30));
    }

    /// <summary>
    /// Las columnas de días tienen que caer exactamente en M:Y. Planilla lee por posición.
    /// </summary>
    [Fact]
    public void Las_columnas_de_dias_estan_en_las_posiciones_del_modelo()
    {
        using var libro = Exportar(Tareo());
        var hoja = libro.Worksheet("Tareo");

        hoja.Cell("M6").GetString().ShouldBe("FALTAS");
        hoja.Cell("N6").GetString().ShouldBe("SUSPENSION");
        hoja.Cell("O6").GetString().ShouldBe("FALTAS JUS.");
        hoja.Cell("P6").GetString().ShouldBe("TOTAL FALTAS");
        hoja.Cell("Q6").GetString().ShouldBe("DM");
        hoja.Cell("R6").GetString().ShouldBe("VACACI.");
        hoja.Cell("X6").GetString().ShouldBe("PERMISOS");
        hoja.Cell("Y6").GetString().ShouldBe("TARDANZA");
    }

    [Fact]
    public void Los_bloques_de_horas_conservan_los_titulos_y_los_tramos_del_modelo()
    {
        using var libro = Exportar(Tareo());
        var hoja = libro.Worksheet("Tareo");

        hoja.Cell("AE6").GetString().ShouldBe("HRS NORMALES");
        hoja.Cell("AF6").GetDouble().ShouldBe(0.25);
        hoja.Cell("AG6").GetDouble().ShouldBe(0.35);
        hoja.Cell("AH6").GetDouble().ShouldBe(1);

        hoja.Cell("AJ6").GetString().ShouldBe("B.NOCT. (HORAS)");
        hoja.Cell("AK6").GetDouble().ShouldBe(0.25);
        hoja.Cell("AL6").GetDouble().ShouldBe(0.35);
        hoja.Cell("AM6").GetDouble().ShouldBe(1);

        // Los tramos se muestran como porcentaje, no como 0.25 pelado.
        hoja.Cell("AF6").Style.NumberFormat.Format.ShouldBe("0%");
    }

    [Fact]
    public void Los_minutos_se_escriben_como_horas_decimales()
    {
        using var libro = Exportar(Tareo());
        var hoja = libro.Worksheet("Tareo");

        // 14 400 min = 240 h; 1 002 min = 16.70 h; 5 760 min = 96 h; 802 min = 13.37 h.
        hoja.Cell("AE7").GetDouble().ShouldBe(240);
        hoja.Cell("AF7").GetDouble().ShouldBe(16.70);
        hoja.Cell("AH7").GetDouble().ShouldBe(7.25);
        hoja.Cell("AJ7").GetDouble().ShouldBe(96);
        hoja.Cell("AK7").GetDouble().ShouldBe(13.37);
    }

    /// <summary>
    /// Un DNI con cero a la izquierda tiene que sobrevivir el viaje a Excel. Si la celda
    /// queda numérica, el cero se pierde y el cruce con planilla falla.
    /// </summary>
    [Fact]
    public void El_dni_se_escribe_como_texto_y_conserva_el_cero_inicial()
    {
        using var libro = Exportar(Tareo());
        var hoja = libro.Worksheet("Tareo");

        hoja.Cell("B7").GetString().ShouldBe("07436907");
        hoja.Cell("B7").DataType.ShouldBe(XLDataType.Text);
    }

    [Fact]
    public void El_total_de_faltas_suma_las_faltas_y_las_justificadas()
    {
        using var libro = Exportar(Tareo());
        var hoja = libro.Worksheet("Tareo");

        hoja.Cell("M7").GetDouble().ShouldBe(2);
        hoja.Cell("O7").GetDouble().ShouldBe(1);
        hoja.Cell("P7").GetDouble().ShouldBe(3);
    }

    [Fact]
    public void Las_columnas_sin_origen_se_emiten_presentes_pero_vacias()
    {
        using var libro = Exportar(Tareo());
        var hoja = libro.Worksheet("Tareo");

        // Presentes: el título existe y está en su posición.
        hoja.Cell("AN4").GetString().ShouldBe("ASIG. FAMILIAR");
        hoja.Cell("AX6").GetString().ShouldBe("VIATICO");
        hoja.Cell("BF6").GetString().ShouldBe("DESC. PAGO INDEBIDO");

        // Vacías: no se inventa un cero donde nadie calculó nada.
        hoja.Cell("AN7").IsEmpty().ShouldBeTrue();
        hoja.Cell("AX7").IsEmpty().ShouldBeTrue();
    }

    [Fact]
    public void La_hoja_de_descanso_medico_sigue_el_orden_de_columnas_del_modelo()
    {
        var descanso = new FilaDescansoMedicoDto(
            "NOVIEMBRE", "CA OUTSOURCING SAC", "PLANTA 1", "73616983",
            "TAIPE VARGAS, EMERSON", new DateOnly(2024, 11, 8), new DateOnly(2024, 11, 9),
            2, null, null, null, "DESCANSO MEDICO");

        using var libro = Exportar(Tareo(descansos: [descanso]));
        var hoja = libro.Worksheet("Descanso Medico");

        hoja.Cell("A4").GetString().ShouldBe("MES");
        hoja.Cell("B4").GetString().ShouldBe("RAZÓN SOCIAL");
        // El modelo trae "DIAS  EN EL MES" con doble espacio; se normaliza a uno solo.
        hoja.Cell("H4").GetString().ShouldBe("DIAS EN EL MES");
        hoja.Cell("I4").GetString().ShouldBe("CONTINGENCIA");
        hoja.Cell("L4").GetString().ShouldBe("OBSERVACION");

        hoja.Cell("D5").GetString().ShouldBe("73616983");
        hoja.Cell("H5").GetDouble().ShouldBe(2);
        hoja.Cell("L5").GetString().ShouldBe("DESCANSO MEDICO");
    }

    /// <summary>
    /// Las dos tablas van apiladas en una sola hoja, y el bloque de licencias arranca
    /// después del último registro de vacaciones — no en una fila fija.
    /// </summary>
    [Fact]
    public void Vacaciones_y_licencias_van_apiladas_en_la_misma_hoja()
    {
        var vacacion = new FilaVacacionesDto(
            1, "71518991", "HURTADO GUERRERO, ELCIRA", 30,
            new DateOnly(2024, 11, 18), new DateOnly(2024, 12, 17), 13, 17,
            new DateOnly(2024, 12, 18));

        var licencia = new FilaLicenciaDto(
            "NOVIEMBRE", "44692612", "AGUIRRE CRUZ, GLADYS",
            new DateOnly(2024, 11, 1), new DateOnly(2024, 11, 30), 30,
            "LICENCIA CON GOCE", null, null, null);

        using var libro = Exportar(Tareo(vacaciones: [vacacion], licencias: [licencia]));
        var hoja = libro.Worksheet("Vacaciones-Licencias");

        hoja.Cell("A1").GetString().ShouldBe("VACACIONES");
        hoja.Cell("A2").GetString().ShouldBe("N°");
        hoja.Cell("D2").GetString().ShouldBe("DIAS");

        // Las columnas de reparto se titulan con el mes: cambian en cada emisión.
        hoja.Cell("G2").GetString().ShouldBe("Noviembre");
        hoja.Cell("H2").GetString().ShouldBe("Diciembre");
        hoja.Cell("I2").GetString().ShouldBe("RETORNO");

        hoja.Cell("G3").GetDouble().ShouldBe(13);
        hoja.Cell("H3").GetDouble().ShouldBe(17);
        hoja.Cell("I3").GetDateTime().ShouldBe(new DateTime(2024, 12, 18));

        // Una vacación ocupa la fila 3, así que el bloque de licencias abre en la 5.
        hoja.Cell("A5").GetString().ShouldBe("LICENCIAS");
        hoja.Cell("A6").GetString().ShouldBe("MES");
        hoja.Cell("G6").GetString().ShouldBe("CONTINGENCIA");
        hoja.Cell("G7").GetString().ShouldBe("LICENCIA CON GOCE");
    }

    [Fact]
    public void Un_periodo_sin_trabajadores_igual_produce_el_libro_con_sus_encabezados()
    {
        using var libro = Exportar(Tareo(filas: []));

        libro.Worksheets.Count.ShouldBe(3);
        libro.Worksheet("Tareo").Cell("M6").GetString().ShouldBe("FALTAS");
    }
}
