using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Solicitudes;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Solicitudes;

/// <summary>
/// Pruebas de los topes de días por tipo de ausencia.
/// </summary>
/// <remarks>
/// El validador es una función pura: no toca base de datos. Los días ya acumulados se los
/// entrega quien llama (el servicio, con un SUM sobre las solicitudes existentes), así que
/// aquí se pueden probar todos los casos límite con números literales.
///
/// Los tipos de ausencia se construyen igual que están sembrados en <c>DatosSemilla</c>:
/// si el seed cambia un nombre o un tope, estos tests dejan de reflejar la realidad y hay
/// que actualizarlos a propósito, no por accidente.
/// </remarks>
public class ValidadorLimitesSolicitudTests
{
    // Tipos tal como están en el catálogo sembrado.
    private static TipoAusencia Vacaciones() => new()
    {
        Id = 1,
        Codigo = "VAC",
        Nombre = "Vacaciones",
        Comportamiento = ComportamientoAusencia.JustificadaRemunerada
    };

    private static TipoAusencia DescansoMedico() => new()
    {
        Id = 2,
        Codigo = "DM",
        Nombre = "Descanso médico",
        Comportamiento = ComportamientoAusencia.Subsidiada,
        MaximoDiasPorAnioCalendario = 20
    };

    private static TipoAusencia LicenciaConGoce() => new()
    {
        Id = 3,
        Codigo = "LCG",
        Nombre = "Licencia con goce",
        Comportamiento = ComportamientoAusencia.JustificadaRemunerada
    };

    private static TipoAusencia LicenciaPaternal() => new()
    {
        Id = 6,
        Codigo = "LPAT",
        Nombre = "Licencia paternal",
        Comportamiento = ComportamientoAusencia.JustificadaRemunerada,
        MaximoDiasPorEvento = 10
    };

    private static TipoAusencia LicenciaMaternidad() => new()
    {
        Id = 7,
        Codigo = "LMAT",
        Nombre = "Licencia por maternidad",
        Comportamiento = ComportamientoAusencia.JustificadaRemunerada,
        MaximoDiasPorEvento = 98
    };

    // ------------------------------------------------------------------
    // Tope por evento: licencia paternal (10) y maternidad (98)
    // ------------------------------------------------------------------

    [Fact]
    public void Licencia_paternal_de_exactamente_diez_dias_se_acepta()
    {
        // Del 1 al 10 de marzo son 10 días: el tope se alcanza, no se excede.
        var error = ValidadorLimitesSolicitud.Validar(
            LicenciaPaternal(),
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 10),
            diasYaAcumulados: 0,
            subtipoVacaciones: null,
            periodoVacacional: null);

        error.ShouldBeNull();
    }

    [Fact]
    public void Licencia_paternal_de_once_dias_se_rechaza()
    {
        var error = ValidadorLimitesSolicitud.Validar(
            LicenciaPaternal(),
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 11),
            diasYaAcumulados: 0,
            subtipoVacaciones: null,
            periodoVacacional: null);

        error.ShouldBe("La licencia paternal no puede exceder 10 días. Se solicitaron 11 días.");
    }

    [Fact]
    public void El_tope_por_evento_ignora_los_dias_ya_acumulados()
    {
        // La licencia paternal se agota por evento, no por año: un segundo hijo en el mismo
        // año da derecho a otros diez días. Si el tope mirara el acumulado, el sistema
        // negaría un derecho que la ley sí reconoce.
        var error = ValidadorLimitesSolicitud.Validar(
            LicenciaPaternal(),
            new DateOnly(2026, 11, 2),
            new DateOnly(2026, 11, 11),
            diasYaAcumulados: 10,
            subtipoVacaciones: null,
            periodoVacacional: null);

        error.ShouldBeNull();
    }

    [Fact]
    public void Licencia_por_maternidad_de_exactamente_98_dias_se_acepta()
    {
        // Del 1 de marzo al 6 de junio de 2026: 31 + 30 + 31 + 6 = 98 días.
        var error = ValidadorLimitesSolicitud.Validar(
            LicenciaMaternidad(),
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 6, 6),
            diasYaAcumulados: 0,
            subtipoVacaciones: null,
            periodoVacacional: null);

        error.ShouldBeNull();
    }

    [Fact]
    public void Licencia_por_maternidad_de_99_dias_se_rechaza()
    {
        var error = ValidadorLimitesSolicitud.Validar(
            LicenciaMaternidad(),
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 6, 7),
            diasYaAcumulados: 0,
            subtipoVacaciones: null,
            periodoVacacional: null);

        error.ShouldBe(
            "La licencia por maternidad no puede exceder 98 días (14 semanas). " +
            "Se solicitaron 99 días.");
    }

    [Fact]
    public void Un_solo_dia_cuenta_como_un_dia_no_como_cero()
    {
        // fechaFin == fechaInicio es un día, no una diferencia de cero: si el cálculo
        // restara sin sumar uno, todos los topes quedarían corridos en un día.
        var error = ValidadorLimitesSolicitud.Validar(
            LicenciaPaternal(),
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 1),
            diasYaAcumulados: 0,
            subtipoVacaciones: null,
            periodoVacacional: null);

        error.ShouldBeNull();
    }

    // ------------------------------------------------------------------
    // Tope por año calendario: descanso médico (20)
    // ------------------------------------------------------------------

    [Fact]
    public void Descanso_medico_de_diecinueve_acumulados_mas_uno_llega_justo_al_tope()
    {
        var error = ValidadorLimitesSolicitud.Validar(
            DescansoMedico(),
            new DateOnly(2026, 5, 10),
            new DateOnly(2026, 5, 10),
            diasYaAcumulados: 19,
            subtipoVacaciones: null,
            periodoVacacional: null);

        error.ShouldBeNull();
    }

    [Fact]
    public void Descanso_medico_de_veinte_acumulados_mas_uno_pasa_el_tope()
    {
        var error = ValidadorLimitesSolicitud.Validar(
            DescansoMedico(),
            new DateOnly(2026, 5, 10),
            new DateOnly(2026, 5, 10),
            diasYaAcumulados: 20,
            subtipoVacaciones: null,
            periodoVacacional: null);

        error.ShouldBe(
            "No se puede registrar: el trabajador ya acumula 20 día(s) de descanso médico " +
            "en el año 2026. El máximo permitido es 20 días por año. Los días solicitados (1) " +
            "exceden el límite en 1 día(s).");
    }

    [Fact]
    public void El_exceso_del_tope_anual_se_informa_con_el_numero_exacto_de_dias()
    {
        // 15 acumulados + 10 solicitados = 25: se pasa en 5.
        var error = ValidadorLimitesSolicitud.Validar(
            DescansoMedico(),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 10),
            diasYaAcumulados: 15,
            subtipoVacaciones: null,
            periodoVacacional: null);

        error.ShouldBe(
            "No se puede registrar: el trabajador ya acumula 15 día(s) de descanso médico " +
            "en el año 2026. El máximo permitido es 20 días por año. Los días solicitados (10) " +
            "exceden el límite en 5 día(s).");
    }

    [Fact]
    public void El_ano_del_mensaje_es_el_de_la_fecha_de_inicio()
    {
        // Un descanso que arranca el 28 de diciembre y termina en enero se imputa al año
        // en que empezó: es el año cuyo acumulado se consultó.
        var error = ValidadorLimitesSolicitud.Validar(
            DescansoMedico(),
            new DateOnly(2025, 12, 28),
            new DateOnly(2026, 1, 5),
            diasYaAcumulados: 20,
            subtipoVacaciones: null,
            periodoVacacional: null);

        error.ShouldNotBeNull();
        error.ShouldContain("en el año 2025.");
    }

    [Fact]
    public void Sin_dias_acumulados_un_descanso_medico_de_veinte_dias_se_acepta()
    {
        // Del 1 al 20 de julio son exactamente 20 días.
        var error = ValidadorLimitesSolicitud.Validar(
            DescansoMedico(),
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 20),
            diasYaAcumulados: 0,
            subtipoVacaciones: null,
            periodoVacacional: null);

        error.ShouldBeNull();
    }

    // ------------------------------------------------------------------
    // Vacaciones: gozadas (30) y venta (15), por periodo vacacional
    // ------------------------------------------------------------------

    [Fact]
    public void Venta_de_vacaciones_justo_en_quince_dias_se_acepta()
    {
        // 10 ya vendidos + 5 solicitados (del 5 al 9 de enero) = 15.
        var error = ValidadorLimitesSolicitud.Validar(
            Vacaciones(),
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 9),
            diasYaAcumulados: 10,
            subtipoVacaciones: "VENTA",
            periodoVacacional: "2025-2026");

        error.ShouldBeNull();
    }

    [Fact]
    public void Venta_de_vacaciones_que_pasa_de_quince_dias_se_rechaza()
    {
        // 10 ya vendidos + 6 solicitados = 16.
        var error = ValidadorLimitesSolicitud.Validar(
            Vacaciones(),
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 10),
            diasYaAcumulados: 10,
            subtipoVacaciones: "VENTA",
            periodoVacacional: "2025-2026");

        error.ShouldBe(
            "No se puede registrar: el trabajador ya tiene 10 día(s) de venta de vacaciones " +
            "en el periodo 2025-2026. El máximo es 15 días por periodo.");
    }

    [Fact]
    public void Vacaciones_gozadas_justo_en_treinta_dias_se_aceptan()
    {
        // 15 ya gozados + 15 solicitados (del 1 al 15 de febrero) = 30.
        var error = ValidadorLimitesSolicitud.Validar(
            Vacaciones(),
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 15),
            diasYaAcumulados: 15,
            subtipoVacaciones: "GOZADAS",
            periodoVacacional: "2025-2026");

        error.ShouldBeNull();
    }

    [Fact]
    public void Vacaciones_gozadas_que_pasan_de_treinta_dias_se_rechazan()
    {
        // 30 ya gozados + 1 solicitado = 31.
        var error = ValidadorLimitesSolicitud.Validar(
            Vacaciones(),
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 1),
            diasYaAcumulados: 30,
            subtipoVacaciones: "GOZADAS",
            periodoVacacional: "2025-2026");

        error.ShouldBe(
            "No se puede registrar: el trabajador ya tiene 30 día(s) de vacaciones gozadas " +
            "en el periodo 2025-2026. El máximo es 30 días por periodo.");
    }

    [Fact]
    public void Los_topes_de_venta_y_de_goce_se_cuentan_por_separado()
    {
        // Quien ya vendió 15 días todavía puede gozar 15: son dos cupos distintos del mismo
        // periodo, y el llamador consulta el acumulado de cada subtipo por separado.
        var error = ValidadorLimitesSolicitud.Validar(
            Vacaciones(),
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 15),
            diasYaAcumulados: 0,
            subtipoVacaciones: "GOZADAS",
            periodoVacacional: "2025-2026");

        error.ShouldBeNull();
    }

    [Fact]
    public void El_subtipo_de_vacaciones_no_distingue_mayusculas()
    {
        var error = ValidadorLimitesSolicitud.Validar(
            Vacaciones(),
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 10),
            diasYaAcumulados: 10,
            subtipoVacaciones: " venta ",
            periodoVacacional: "2025-2026");

        error.ShouldNotBeNull();
        error.ShouldContain("venta de vacaciones");
    }

    // ------------------------------------------------------------------
    // Vacaciones: datos obligatorios y formato del periodo
    // ------------------------------------------------------------------

    [Fact]
    public void Vacaciones_sin_subtipo_se_rechazan()
    {
        // Sin subtipo no hay tope que aplicar: dejarlo pasar sería una vía silenciosa para
        // saltarse los límites de 15 y 30 días.
        var error = ValidadorLimitesSolicitud.Validar(
            Vacaciones(),
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 9),
            diasYaAcumulados: 0,
            subtipoVacaciones: null,
            periodoVacacional: "2025-2026");

        error.ShouldBe(
            "Debe indicar si las vacaciones son gozadas o venta de vacaciones.");
    }

    [Fact]
    public void Un_subtipo_desconocido_se_rechaza()
    {
        var error = ValidadorLimitesSolicitud.Validar(
            Vacaciones(),
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 9),
            diasYaAcumulados: 0,
            subtipoVacaciones: "ADELANTO",
            periodoVacacional: "2025-2026");

        error.ShouldBe(
            "El subtipo de vacaciones 'ADELANTO' no es válido: debe ser 'GOZADAS' o 'VENTA'.");
    }

    [Fact]
    public void Vacaciones_sin_periodo_vacacional_se_rechazan()
    {
        var error = ValidadorLimitesSolicitud.Validar(
            Vacaciones(),
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 9),
            diasYaAcumulados: 0,
            subtipoVacaciones: "GOZADAS",
            periodoVacacional: "   ");

        error.ShouldBe(
            "Debe indicar el periodo vacacional al que se imputan estos días " +
            "(formato AAAA-AAAA, por ejemplo 2025-2026).");
    }

    [Fact]
    public void Un_periodo_con_anios_no_consecutivos_se_rechaza()
    {
        // La expresión regular del formulario acepta 2025-2027: el segundo año consecutivo
        // solo se puede comprobar aquí.
        var error = ValidadorLimitesSolicitud.Validar(
            Vacaciones(),
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 9),
            diasYaAcumulados: 0,
            subtipoVacaciones: "GOZADAS",
            periodoVacacional: "2025-2027");

        error.ShouldBe(
            "El periodo vacacional '2025-2027' no es válido: el segundo año debe ser el " +
            "siguiente del primero (por ejemplo, 2025-2026).");
    }

    [Theory]
    [InlineData("2025/2026")]
    [InlineData("25-26")]
    [InlineData("2025-20261")]
    [InlineData("abcd-efgh")]
    [InlineData("2025 -2026")]
    public void Un_periodo_con_formato_invalido_se_rechaza(string periodo)
    {
        var error = ValidadorLimitesSolicitud.Validar(
            Vacaciones(),
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 9),
            diasYaAcumulados: 0,
            subtipoVacaciones: "GOZADAS",
            periodoVacacional: periodo);

        error.ShouldNotBeNull();
        error.ShouldContain("formato AAAA-AAAA");
    }

    // ------------------------------------------------------------------
    // Tipos sin tope
    // ------------------------------------------------------------------

    [Fact]
    public void Un_tipo_sin_topes_nunca_devuelve_error()
    {
        // Licencia con goce: el catálogo no le puso tope, así que ni el rango ni el
        // acumulado la limitan.
        var error = ValidadorLimitesSolicitud.Validar(
            LicenciaConGoce(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            diasYaAcumulados: 1000,
            subtipoVacaciones: null,
            periodoVacacional: null);

        error.ShouldBeNull();
    }

    [Fact]
    public void Un_tipo_sin_topes_ignora_el_subtipo_de_vacaciones()
    {
        // Los campos de vacaciones solo aplican al tipo VAC. Que lleguen con basura desde
        // un formulario mal armado no puede bloquear una licencia con goce.
        var error = ValidadorLimitesSolicitud.Validar(
            LicenciaConGoce(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 5),
            diasYaAcumulados: 0,
            subtipoVacaciones: "ADELANTO",
            periodoVacacional: "no es un periodo");

        error.ShouldBeNull();
    }

    [Fact]
    public void Un_descanso_medico_ignora_el_subtipo_de_vacaciones()
    {
        var error = ValidadorLimitesSolicitud.Validar(
            DescansoMedico(),
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 3),
            diasYaAcumulados: 5,
            subtipoVacaciones: "VENTA",
            periodoVacacional: "2025-2026");

        error.ShouldBeNull();
    }

    [Fact]
    public void Vacaciones_dentro_de_los_topes_devuelven_null()
    {
        var error = ValidadorLimitesSolicitud.Validar(
            Vacaciones(),
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 9),
            diasYaAcumulados: 0,
            subtipoVacaciones: "GOZADAS",
            periodoVacacional: "2025-2026");

        error.ShouldBeNull();
    }
}
