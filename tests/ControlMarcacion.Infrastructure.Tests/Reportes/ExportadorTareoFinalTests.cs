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

    private static MetadatosReporte Metadatos(EstadoCalculoTareo? calculo = null) => new(
        Alcance: "Periodo 2024-11 · 01/11/2024 al 30/11/2024",
        FechaInicio: Inicio,
        FechaFin: Fin,
        PeriodoCodigo: "2024-11",
        PeriodoNombre: "Noviembre 2024",
        PeriodoAbierto: false,
        FiltrosDescripcion: "Todos los trabajadores",
        GeneradoEn: new DateTime(2024, 12, 2, 9, 0, 0),
        GeneradoPor: "rrhh",
        Calculo: calculo ?? new EstadoCalculoTareo(
            CalculadoMasReciente: new DateTime(2024, 12, 1, 22, 0, 0),
            UltimoCambioRelevante: new DateTime(2024, 11, 30, 20, 0, 0),
            MotivoUltimoCambio: "una carga de marcaciones"));

    private static FilaTareoFinalDto Fila() => new()
    {
        Numero = 1,
        Dni = "07436907",
        ApellidosYNombres = "ACHO PIPA, MARSEBITH",
        Planta = "PLANTA 1",
        Centro = "066D",
        CentroCosto = "PE21400618",
        DescripcionCeco = "Envasado de Aerosoles/Insecticidas",
        Area = "AEROSOLES",
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

    private static XLWorkbook Exportar(TareoFinalDto tareo, MetadatosReporte? metadatos = null)
    {
        var bytes = new ExportadorReportesExcel().ExportarTareoFinal(metadatos ?? Metadatos(), tareo);
        return new XLWorkbook(new MemoryStream(bytes));
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Las tres hojas del modelo van primero y en su orden; la de trazabilidad, después.
    /// </summary>
    /// <remarks>
    /// El orden importa de verdad: planilla copia las tres primeras a su plantilla. Por eso
    /// "Trazabilidad" se agregó al final y no al principio — es aditiva y no corre nada.
    /// </remarks>
    [Fact]
    public void El_libro_trae_las_tres_hojas_del_modelo_y_en_ese_orden()
    {
        using var libro = Exportar(Tareo());

        libro.Worksheets.Select(h => h.Name).ShouldBe(
            ["Tareo", "Descanso Medico", "Vacaciones-Licencias", "Trazabilidad"]);
    }

    [Fact]
    public void La_hoja_de_trazabilidad_dice_de_donde_salio_el_libro()
    {
        using var libro = Exportar(Tareo());
        var hoja = libro.Worksheet("Trazabilidad");

        var etiquetas = Enumerable.Range(3, 10).Select(f => hoja.Cell(f, 1).GetString()).ToList();

        etiquetas.ShouldContain("Alcance");
        etiquetas.ShouldContain("Generado por");
        etiquetas.ShouldContain("Estado del periodo");

        hoja.Cell(4, 2).GetString().ShouldBe("Periodo 2024-11 · 01/11/2024 al 30/11/2024");
        hoja.Cell(12, 2).GetString().ShouldBe("rrhh");
    }

    /// <summary>
    /// Un tareo calculado antes del último cambio se declara desactualizado en el archivo.
    /// </summary>
    /// <remarks>
    /// Es la única señal: un Excel con cifras viejas se ve idéntico a uno al día, y una vez
    /// enviado por correo ya no hay pantalla que lo avise.
    /// </remarks>
    [Fact]
    public void Un_tareo_desactualizado_se_advierte_dentro_del_propio_archivo()
    {
        var desactualizado = new EstadoCalculoTareo(
            CalculadoMasReciente: new DateTime(2024, 11, 20, 10, 0, 0),
            UltimoCambioRelevante: new DateTime(2024, 11, 29, 18, 30, 0),
            MotivoUltimoCambio: "una carga de marcaciones");

        desactualizado.Desactualizado.ShouldBeTrue();

        using var libro = Exportar(Tareo(), Metadatos(desactualizado));
        var hoja = libro.Worksheet("Trazabilidad");

        var texto = Enumerable.Range(13, 6)
            .Select(f => hoja.Cell(f, 1).GetString())
            .First(t => t.Contains("Tareo desactualizado", StringComparison.Ordinal));

        texto.ShouldContain("20/11/2024 10:00");
        texto.ShouldContain("una carga de marcaciones");
        texto.ShouldContain("Recalcule el periodo");
    }

    [Fact]
    public void El_encabezado_lleva_razon_social_mes_y_rango_del_periodo()
    {
        using var libro = Exportar(Tareo());
        var hoja = libro.Worksheet("Tareo");

        hoja.Cell("A1").GetString().ShouldBe("CA OUTSOURCING SAC");
        hoja.Cell("A2").GetString().ShouldBe("TAREO MENSUAL (OPERARIOS)");
        hoja.Cell("A3").GetString().ShouldBe("MES: NOVIEMBRE");
        hoja.Cell("AF3").GetString().ShouldBe("DEL");
        hoja.Cell("AG3").GetDateTime().ShouldBe(new DateTime(2024, 11, 1));
        hoja.Cell("AJ3").GetDateTime().ShouldBe(new DateTime(2024, 11, 30));
    }

    /// <summary>
    /// Las columnas de días tienen que caer exactamente en M:Y. Planilla lee por posición.
    /// </summary>
    [Fact]
    public void Las_columnas_de_dias_estan_en_las_posiciones_del_modelo()
    {
        using var libro = Exportar(Tareo());
        var hoja = libro.Worksheet("Tareo");

        hoja.Cell("L6").GetString().ShouldBe("FALTAS");
        hoja.Cell("M6").GetString().ShouldBe("SUSPENSION");
        hoja.Cell("N6").GetString().ShouldBe("FALTAS JUS.");
        hoja.Cell("O6").GetString().ShouldBe("TOTAL FALTAS");
        hoja.Cell("P6").GetString().ShouldBe("DM");
        hoja.Cell("Q6").GetString().ShouldBe("VACACI.");
        hoja.Cell("W6").GetString().ShouldBe("PERMISOS");
        hoja.Cell("X6").GetString().ShouldBe("TARDANZA");
    }

    [Fact]
    public void Los_bloques_de_horas_conservan_los_titulos_y_los_tramos_del_modelo()
    {
        using var libro = Exportar(Tareo());
        var hoja = libro.Worksheet("Tareo");

        hoja.Cell("AD6").GetString().ShouldBe("HRS NORMALES");
        hoja.Cell("AE6").GetDouble().ShouldBe(0.25);
        hoja.Cell("AF6").GetDouble().ShouldBe(0.35);
        hoja.Cell("AG6").GetDouble().ShouldBe(1);

        hoja.Cell("AI6").GetString().ShouldBe("B.NOCT. (HORAS)");
        hoja.Cell("AJ6").GetDouble().ShouldBe(0.25);
        hoja.Cell("AK6").GetDouble().ShouldBe(0.35);
        hoja.Cell("AL6").GetDouble().ShouldBe(1);

        // Los tramos se muestran como porcentaje, no como 0.25 pelado.
        hoja.Cell("AE6").Style.NumberFormat.Format.ShouldBe("0%");
    }

    [Fact]
    public void Los_minutos_se_escriben_como_horas_decimales()
    {
        using var libro = Exportar(Tareo());
        var hoja = libro.Worksheet("Tareo");

        // 14 400 min = 240 h; 1 002 min = 16.70 h; 5 760 min = 96 h; 802 min = 13.37 h.
        hoja.Cell("AD7").GetDouble().ShouldBe(240);
        hoja.Cell("AE7").GetDouble().ShouldBe(16.70);
        hoja.Cell("AG7").GetDouble().ShouldBe(7.25);
        hoja.Cell("AI7").GetDouble().ShouldBe(96);
        hoja.Cell("AJ7").GetDouble().ShouldBe(13.37);
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

        hoja.Cell("L7").GetDouble().ShouldBe(2);
        hoja.Cell("N7").GetDouble().ShouldBe(1);
        hoja.Cell("O7").GetDouble().ShouldBe(3);
    }

    /// <summary>
    /// El bloque de llenado manual (Y-AC) se emite vacío, presente solo por título — igual
    /// que en el archivo de GCA. Y si el trabajador no tiene centro de costo asignado, las
    /// tres columnas de CECO salen vacías en vez de aproximarse con la jerarquía interna.
    /// </summary>
    [Fact]
    public void Las_columnas_sin_origen_real_se_emiten_presentes_pero_vacias()
    {
        var fila = Fila() with { Centro = null, CentroCosto = null, DescripcionCeco = null };

        using var libro = Exportar(Tareo([fila]));
        var hoja = libro.Worksheet("Tareo");

        // Presentes: el título existe y está en su posición.
        hoja.Cell("Y4").GetString().ShouldBe("Ausencias");

        // Vacías: no se inventa un dato que ningún catálogo real todavía resuelve.
        hoja.Cell("Y7").IsEmpty().ShouldBeTrue();
        hoja.Cell("E7").GetString().ShouldBe(string.Empty);
        hoja.Cell("F7").GetString().ShouldBe(string.Empty);
        hoja.Cell("G7").GetString().ShouldBe(string.Empty);

        // AREA no depende del CECO: sigue saliendo.
        hoja.Cell("H7").GetString().ShouldBe("AEROSOLES");
    }

    /// <summary>
    /// La columna SUSPENSION sale vacía, no en cero, y no corre las que van después.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No existe el concepto de suspensión en el sistema. Salía en cero y se sumaba en la
    /// fila TOTAL, lo que la hacía parecer calculada: con el formato de número el cero se
    /// pinta como «-», indistinguible de «esta persona no tuvo suspensiones». En el archivo
    /// que va a planilla eso es afirmar algo que nadie comprobó.
    /// </para>
    /// <para>
    /// La segunda mitad del test es la que de verdad importa: la columna <b>se conserva</b>
    /// en su posición. El bloque de días se escribe por índice desde la 12, así que sacarla
    /// correría todo lo que va después y dejaría cada total bajo el título equivocado, sin
    /// ningún error.
    /// </para>
    /// </remarks>
    [Fact]
    public void La_columna_suspension_sale_vacia_y_no_corre_a_las_demas()
    {
        var fila = Fila() with { DiasFalta = 2, DiasFaltaJustificada = 1, DiasVacaciones = 5 };

        using var libro = Exportar(Tareo([fila]));
        var hoja = libro.Worksheet("Tareo");

        // El título sigue ahí: es el formato de GCA.
        hoja.Cell("M6").GetString().ShouldBe("SUSPENSION");

        // Pero ni la fila ni el TOTAL afirman un cero que nadie calculó.
        hoja.Cell("M7").IsEmpty().ShouldBeTrue();

        // Y las vecinas siguen en su sitio, con su dato: nada se corrió.
        hoja.Cell("L7").GetDouble().ShouldBe(2);   // FALTAS
        hoja.Cell("N7").GetDouble().ShouldBe(1);   // FALTAS JUS.
        hoja.Cell("O7").GetDouble().ShouldBe(3);   // TOTAL FALTAS
        hoja.Cell("Q7").GetDouble().ShouldBe(5);   // VACACI.

        var total = hoja.Column(1).CellsUsed(c => c.GetString() == "TOTAL").Single().Address.RowNumber;

        hoja.Cell(total, 13).IsEmpty().ShouldBeTrue();
        hoja.Cell(total, 12).GetDouble().ShouldBe(2);
        hoja.Cell(total, 14).GetDouble().ShouldBe(1);
        hoja.Cell(total, 17).GetDouble().ShouldBe(5);
    }

    /// <summary>
    /// Bloque de identificación del formato vigente de GCA: CENTRO (E) / CENTRO DE COSTO (F) /
    /// DESCRIP. CECO (G) salen del catálogo real de SAP, y AREA (H) es el área interna.
    /// </summary>
    [Fact]
    public void Ceco_y_area_se_escriben_en_las_columnas_del_formato_de_gca()
    {
        var fila = Fila() with
        {
            Centro = "066G",
            CentroCosto = "PE21400303",
            DescripcionCeco = "Manufactura Detergentes Lurín",
            Area = "PROCESOS"
        };

        using var libro = Exportar(Tareo([fila]));
        var hoja = libro.Worksheet("Tareo");

        hoja.Cell("E4").GetString().ShouldBe("CENTRO");
        hoja.Cell("F4").GetString().ShouldBe("CENTRO DE COSTO");
        hoja.Cell("G4").GetString().ShouldBe("DESCRIP. CECO");
        hoja.Cell("H4").GetString().ShouldBe("AREA");

        hoja.Cell("E7").GetString().ShouldBe("066G");
        hoja.Cell("F7").GetString().ShouldBe("PE21400303");
        hoja.Cell("G7").GetString().ShouldBe("Manufactura Detergentes Lurín");
        hoja.Cell("H7").GetString().ShouldBe("PROCESOS");
    }

    /// <summary>Movilidad (AH) y asignación familiar (AM): montos calculados por el sistema.</summary>
    [Fact]
    public void Movilidad_y_asignacion_familiar_se_escriben_como_numero()
    {
        var fila = Fila() with { MontoMovilidad = 90m, MontoAsignacionFamiliar = 113m };

        using var libro = Exportar(Tareo([fila]));
        var hoja = libro.Worksheet("Tareo");

        hoja.Cell("AH7").GetDouble().ShouldBe(90);
        hoja.Cell("AH7").DataType.ShouldBe(XLDataType.Number);
        hoja.Cell("AM7").GetDouble().ShouldBe(113);
        hoja.Cell("AM7").DataType.ShouldBe(XLDataType.Number);
    }

    /// <summary>Las 18 columnas del bloque REINTEGROS (AO-BF), cargadas desde ReintegroTareo.</summary>
    [Fact]
    public void Los_reintegros_se_escriben_en_el_bloque_AO_BF()
    {
        var fila = Fila() with
        {
            ReintegroHorasNormales = 8m,
            ReintegroHorasExtra25 = 1m,
            ReintegroHorasExtra35 = 0.5m,
            ReintegroHorasNoLaborable = 2m,
            ReintegroMontoMovilidad = 15m,
            ReintegroHorasNocturnas = 3m,
            ReintegroHorasExtra25Nocturnas = 0.25m,
            ReintegroHorasExtra35Nocturnas = 0.1m,
            ReintegroHorasNoLaborableNocturnas = 0.2m,
            ReintegroViatico = 50m,
            ReintegroDiasDescansoMedico = 2,
            ReintegroDiasSubsidioMaternidad = 0,
            ReintegroDiasSubsidioAccidente = 1,
            ReintegroDiasLicenciaPaternidad = 4,
            ReintegroLicenciaConGoce = 120m,
            ReintegroAsignacionFamiliar = 113m,
            ReintegroAfecto = 30m,
            ReintegroDescuentoPagoIndebido = 25m
        };

        using var libro = Exportar(Tareo([fila]));
        var hoja = libro.Worksheet("Tareo");

        hoja.Cell("AN7").GetDouble().ShouldBe(8);
        hoja.Cell("AO7").GetDouble().ShouldBe(1);
        hoja.Cell("AP7").GetDouble().ShouldBe(0.5);
        hoja.Cell("AQ7").GetDouble().ShouldBe(2);
        hoja.Cell("AR7").GetDouble().ShouldBe(15);
        hoja.Cell("AS7").GetDouble().ShouldBe(3);
        hoja.Cell("AT7").GetDouble().ShouldBe(0.25);
        hoja.Cell("AU7").GetDouble().ShouldBe(0.1);
        hoja.Cell("AV7").GetDouble().ShouldBe(0.2);
        hoja.Cell("AW7").GetDouble().ShouldBe(50);
        hoja.Cell("AX7").GetDouble().ShouldBe(2);
        hoja.Cell("AY7").GetDouble().ShouldBe(0);
        hoja.Cell("AZ7").GetDouble().ShouldBe(1);
        hoja.Cell("BA7").GetDouble().ShouldBe(4);
        hoja.Cell("BB7").GetDouble().ShouldBe(120);
        hoja.Cell("BC7").GetDouble().ShouldBe(113);
        hoja.Cell("BD7").GetDouble().ShouldBe(30);
        hoja.Cell("BE7").GetDouble().ShouldBe(25);
    }

    /// <summary>Sin reintegros cargados, el bloque AO-BF sale en cero (se ve como "-"), no vacío.</summary>
    [Fact]
    public void Sin_reintegros_cargados_el_bloque_sale_en_cero()
    {
        using var libro = Exportar(Tareo());
        var hoja = libro.Worksheet("Tareo");

        hoja.Cell("AN7").IsEmpty().ShouldBeFalse();
        hoja.Cell("AN7").GetDouble().ShouldBe(0);
        hoja.Cell("BE7").GetDouble().ShouldBe(0);
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
    /// Las tres columnas clínicas —la brecha de datos que quedaba— se escriben con el texto
    /// exacto del archivo de GCA.
    /// </summary>
    /// <remarks>
    /// El texto importa: la hoja se pega en la plantilla que planilla ya tiene armada, y un
    /// "EsSalud" o un "Enfermedad Común" con otra grafía le rompe los filtros. Por eso se
    /// comprueba la cadena literal y no solo que la celda venga llena.
    /// </remarks>
    [Fact]
    public void El_detalle_clinico_se_escribe_con_el_texto_del_archivo_de_gca()
    {
        var descanso = new FilaDescansoMedicoDto(
            "NOVIEMBRE", "CA OUTSOURCING SAC", "PLANTA 1", "73616983",
            "TAIPE VARGAS, EMERSON", new DateOnly(2024, 11, 8), new DateOnly(2024, 11, 9),
            2, "ACCIDENTE LABORAL", "Fractura de radio distal", "ESSALUD", "DESCANSO MEDICO");

        using var libro = Exportar(Tareo(descansos: [descanso]));
        var hoja = libro.Worksheet("Descanso Medico");

        hoja.Cell("I4").GetString().ShouldBe("CONTINGENCIA");
        hoja.Cell("J4").GetString().ShouldBe("DIAGNÓSTICO");
        hoja.Cell("K4").GetString().ShouldBe("CENTRO DE ATENCIÓN");

        hoja.Cell("I5").GetString().ShouldBe("ACCIDENTE LABORAL");
        hoja.Cell("J5").GetString().ShouldBe("Fractura de radio distal");
        hoja.Cell("K5").GetString().ShouldBe("ESSALUD");
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

        libro.Worksheets.Count.ShouldBe(4);
        libro.Worksheet("Tareo").Cell("L6").GetString().ShouldBe("FALTAS");
    }
}
