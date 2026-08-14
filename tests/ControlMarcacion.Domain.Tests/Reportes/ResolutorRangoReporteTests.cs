using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Reportes;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Reportes;

/// <summary>
/// El resolutor del alcance temporal de un reporte.
/// </summary>
/// <remarks>
/// Función pura: fechas literales, sin base de datos ni reloj. Es donde vive la validación
/// de los cuatro alcances, y por eso se prueba aquí y no a través de una pantalla.
/// </remarks>
public class ResolutorRangoReporteTests
{
    // ------------------------------------------------------------------
    // Periodo
    // ------------------------------------------------------------------

    [Fact]
    public void Un_periodo_produce_su_rango_y_conserva_su_id()
    {
        var rango = ResolutorRangoReporte.DePeriodo(
            7, "2026-07", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        rango.Desde.ShouldBe(new DateOnly(2026, 7, 1));
        rango.Hasta.ShouldBe(new DateOnly(2026, 7, 31));
        rango.Dias.ShouldBe(31);

        // El id sobrevive porque el tareo final y el estado abierto/cerrado lo necesitan:
        // son cosas del periodo, no del rango de fechas.
        rango.PeriodoId.ShouldBe(7);
        rango.Etiqueta.ShouldBe("Periodo 2026-07 · 01/07/2026 al 31/07/2026");
    }

    // ------------------------------------------------------------------
    // Mes
    // ------------------------------------------------------------------

    [Fact]
    public void Un_mes_cubre_del_primero_al_ultimo_dia()
    {
        var rango = ResolutorRangoReporte.DeMes(2026, 2);

        rango.Desde.ShouldBe(new DateOnly(2026, 2, 1));
        rango.Hasta.ShouldBe(new DateOnly(2026, 2, 28));
        rango.Etiqueta.ShouldBe("Mes de Febrero 2026");
    }

    /// <summary>El último día sale del calendario, no de un 28 ni un 30 fijos.</summary>
    [Fact]
    public void Febrero_de_un_anio_bisiesto_llega_hasta_el_29()
    {
        ResolutorRangoReporte.DeMes(2028, 2).Hasta.ShouldBe(new DateOnly(2028, 2, 29));
    }

    /// <summary>Un alcance por mes no arrastra periodo: no es un periodo de planilla.</summary>
    [Fact]
    public void Un_mes_no_tiene_periodo_asociado()
    {
        ResolutorRangoReporte.DeMes(2026, 7).PeriodoId.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Un_mes_fuera_de_1_a_12_se_rechaza(int mes)
    {
        Should.Throw<ReglaNegocioException>(() => ResolutorRangoReporte.DeMes(2026, mes));
    }

    [Fact]
    public void Un_anio_fuera_del_rango_del_sistema_se_rechaza()
    {
        Should.Throw<ReglaNegocioException>(() => ResolutorRangoReporte.DeMes(1998, 7));
    }

    [Fact]
    public void Un_mes_sin_elegir_pide_elegirlo_en_vez_de_reventar()
    {
        var error = Should.Throw<ReglaNegocioException>(() => ResolutorRangoReporte.DeMes(2026, null));

        error.Message.ShouldContain("Elija el mes");
    }

    // ------------------------------------------------------------------
    // Día
    // ------------------------------------------------------------------

    [Fact]
    public void Un_dia_produce_un_rango_de_un_solo_dia()
    {
        var rango = ResolutorRangoReporte.DeDia(new DateOnly(2026, 7, 15));

        rango.Desde.ShouldBe(new DateOnly(2026, 7, 15));
        rango.Hasta.ShouldBe(new DateOnly(2026, 7, 15));
        rango.Dias.ShouldBe(1);
        rango.Etiqueta.ShouldBe("Día 15/07/2026");
    }

    /// <summary>Un día suelto se nombra con su fecha, sin el "a" del rango.</summary>
    [Fact]
    public void El_sufijo_de_archivo_de_un_dia_es_una_sola_fecha()
    {
        ResolutorRangoReporte.DeDia(new DateOnly(2026, 7, 15)).Sufijo.ShouldBe("2026-07-15");
    }

    // ------------------------------------------------------------------
    // Rango libre
    // ------------------------------------------------------------------

    [Fact]
    public void Un_rango_libre_conserva_sus_extremos_y_los_cuenta_inclusive()
    {
        var rango = ResolutorRangoReporte.DeRango(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15));

        rango.Dias.ShouldBe(15);
        rango.Etiqueta.ShouldBe("Del 01/07/2026 al 15/07/2026");
        rango.Sufijo.ShouldBe("2026-07-01_a_2026-07-15");
    }

    [Fact]
    public void Un_rango_al_reves_se_rechaza()
    {
        var error = Should.Throw<ReglaNegocioException>(() => ResolutorRangoReporte.DeRango(
            new DateOnly(2026, 7, 31), new DateOnly(2026, 7, 1)));

        error.Message.ShouldContain("no puede ser anterior");
    }

    [Fact]
    public void Un_rango_de_un_solo_dia_es_valido()
    {
        var dia = new DateOnly(2026, 7, 15);

        ResolutorRangoReporte.DeRango(dia, dia).Dias.ShouldBe(1);
    }

    /// <summary>El tope es inclusivo: 366 días pasan, 367 no.</summary>
    [Fact]
    public void El_rango_maximo_entra_justo()
    {
        var desde = new DateOnly(2026, 1, 1);
        var hasta = desde.AddDays(ResolutorRangoReporte.MaximoDias - 1);

        ResolutorRangoReporte.DeRango(desde, hasta).Dias.ShouldBe(ResolutorRangoReporte.MaximoDias);
    }

    [Fact]
    public void Un_rango_mas_largo_que_el_tope_se_rechaza_diciendo_cuanto_pidio()
    {
        var desde = new DateOnly(2026, 1, 1);
        var hasta = desde.AddDays(ResolutorRangoReporte.MaximoDias);

        var error = Should.Throw<ReglaNegocioException>(
            () => ResolutorRangoReporte.DeRango(desde, hasta));

        error.Message.ShouldContain("367 días");
        error.Message.ShouldContain("periodo por periodo");
    }

    [Fact]
    public void Un_rango_a_medio_llenar_pide_las_dos_fechas()
    {
        Should.Throw<ReglaNegocioException>(
            () => ResolutorRangoReporte.DeRango(new DateOnly(2026, 7, 1), null));
    }

    // ------------------------------------------------------------------

    [Fact]
    public void Contiene_responde_por_los_extremos_inclusive()
    {
        var rango = ResolutorRangoReporte.DeRango(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        rango.Contiene(new DateOnly(2026, 7, 1)).ShouldBeTrue();
        rango.Contiene(new DateOnly(2026, 7, 31)).ShouldBeTrue();
        rango.Contiene(new DateOnly(2026, 6, 30)).ShouldBeFalse();
        rango.Contiene(new DateOnly(2026, 8, 1)).ShouldBeFalse();
    }
}
