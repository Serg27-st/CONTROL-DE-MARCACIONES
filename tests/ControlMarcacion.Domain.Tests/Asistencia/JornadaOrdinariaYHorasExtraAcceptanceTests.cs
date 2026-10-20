using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>
/// R-P0-03 / R-P0-03.1: turnos regulares DIU 07:00–17:22 y NOC 19:00–05:22, ordinario hasta el
/// fin teórico, umbral de 15 min para horas extra de lunes a viernes, sábado íntegramente
/// extra y domingo íntegramente al 100 % (ACCEPTANCE_RULES VIG-HE-01..03, VIG-DOM-01).
/// </summary>
public sealed class JornadaOrdinariaYHorasExtraAcceptanceTests
{
    private static readonly DateOnly Lunes = new(2026, 7, 6);
    private static readonly DateOnly Sabado = new(2026, 7, 4);
    private static readonly DateOnly Domingo = new(2026, 7, 5);

    [Theory]
    [InlineData(17, 22, 0)]
    [InlineData(17, 23, 0)]
    [InlineData(17, 30, 0)]
    [InlineData(17, 36, 0)]
    [InlineData(17, 37, 15)]
    [InlineData(17, 38, 16)]
    [InlineData(18, 22, 60)]
    [InlineData(19, 22, 120)]
    public void HE_DIU_00_07_umbral_de_15_minutos_tras_las_17_22(int hora, int minuto, int heEsperadas)
    {
        var resultado = Calcular(Lunes, Diurno(), new TimeOnly(7, 0), Lunes.ToDateTime(new TimeOnly(hora, minuto)));

        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtraTotales.ShouldBe(heEsperadas);
        resultado.MinutosExtra25.ShouldBe(heEsperadas);
        resultado.MinutosExtra35.ShouldBe(0);
        resultado.MinutosSalidaAnticipada.ShouldBe(0);
    }

    [Theory]
    [InlineData(5, 22, 0)]
    [InlineData(5, 23, 0)]
    [InlineData(5, 36, 0)]
    [InlineData(5, 37, 15)]
    [InlineData(5, 38, 16)]
    [InlineData(6, 22, 60)]
    public void HE_NOC_00_05_umbral_de_15_minutos_tras_las_05_22(int hora, int minuto, int heEsperadas)
    {
        var resultado = Calcular(
            Lunes, Nocturno(), new TimeOnly(19, 0), Lunes.AddDays(1).ToDateTime(new TimeOnly(hora, minuto)));

        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtraTotales.ShouldBe(heEsperadas);
        resultado.MinutosExtra25.ShouldBe(heEsperadas);
        resultado.MinutosSalidaAnticipada.ShouldBe(0);
    }

    [Fact]
    public void DIU_CANONICO_07_00_a_17_22_sin_tardanza_ni_salida_anticipada_ni_extra()
    {
        var resultado = Calcular(Lunes, Diurno(), new TimeOnly(7, 0), Lunes.ToDateTime(new TimeOnly(17, 22)));

        resultado.Estado.ShouldBe(EstadoTareo.Puntual);
        resultado.MinutosTardanza.ShouldBe(0);
        resultado.MinutosSalidaAnticipada.ShouldBe(0);
        resultado.MinutosExtraTotales.ShouldBe(0);
        resultado.MinutosOrdinarios.ShouldBe(622);
    }

    [Fact]
    public void NOC_CANONICO_19_00_a_05_22_sin_tardanza_ni_salida_anticipada_ni_extra_con_nocturnidad()
    {
        var resultado = Calcular(
            Lunes, Nocturno(), new TimeOnly(19, 0), Lunes.AddDays(1).ToDateTime(new TimeOnly(5, 22)));

        resultado.Estado.ShouldBe(EstadoTareo.Puntual);
        resultado.MinutosTardanza.ShouldBe(0);
        resultado.MinutosSalidaAnticipada.ShouldBe(0);
        resultado.MinutosExtraTotales.ShouldBe(0);
        resultado.MinutosOrdinarios.ShouldBe(622);

        // 22:00–05:22: la nocturnidad es otra dimensión y convive con cero horas extra.
        resultado.MinutosNocturnos.ShouldBe(442);
    }

    [Fact]
    public void HE_LV_tardanza_diurna_no_corre_el_corte()
    {
        var resultado = Calcular(Lunes, Diurno(), new TimeOnly(7, 10), Lunes.ToDateTime(new TimeOnly(17, 37)));

        resultado.MinutosTardanza.ShouldBe(10);
        resultado.MinutosOrdinarios.ShouldBe(612);
        resultado.MinutosExtraTotales.ShouldBe(15);
    }

    [Fact]
    public void HE_LV_tardanza_diurna_sin_umbral_alcanzado_no_genera_extra()
    {
        var resultado = Calcular(Lunes, Diurno(), new TimeOnly(7, 10), Lunes.ToDateTime(new TimeOnly(17, 32)));

        resultado.MinutosTardanza.ShouldBe(10);
        resultado.MinutosOrdinarios.ShouldBe(612);
        resultado.MinutosExtraTotales.ShouldBe(0);
    }

    [Fact]
    public void HE_LV_tardanza_nocturna_no_corre_el_corte()
    {
        var resultado = Calcular(
            Lunes, Nocturno(), new TimeOnly(19, 10), Lunes.AddDays(1).ToDateTime(new TimeOnly(5, 37)));

        resultado.MinutosTardanza.ShouldBe(10);
        resultado.MinutosOrdinarios.ShouldBe(612);
        resultado.MinutosExtraTotales.ShouldBe(15);
    }

    [Theory]
    [InlineData(6, 45, 7, 0)]
    [InlineData(18, 45, 19, 0)]
    public void HE_LV_llegar_antes_no_genera_extra(int horaEntrada, int minutoEntrada, int horaInicio, int minutoInicio)
    {
        var turno = horaInicio == 7 ? Diurno() : Nocturno();
        var salida = horaInicio == 7
            ? Lunes.ToDateTime(new TimeOnly(17, 22))
            : Lunes.AddDays(1).ToDateTime(new TimeOnly(5, 22));

        var resultado = Calcular(Lunes, turno, new TimeOnly(horaEntrada, minutoEntrada), salida);

        resultado.MinutosTardanza.ShouldBe(0);
        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtraTotales.ShouldBe(0);
        turno.HoraInicio.ShouldBe(new TimeOnly(horaInicio, minutoInicio));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    public void TAR_NOC_tardanza_nocturna_sin_tolerancia(int minutosTarde, int tardanzaEsperada)
    {
        var resultado = Calcular(
            Lunes, Nocturno(), new TimeOnly(19, minutosTarde), Lunes.AddDays(1).ToDateTime(new TimeOnly(5, 22)));

        resultado.MinutosTardanza.ShouldBe(tardanzaEsperada);
    }

    [Theory]
    [InlineData(22, 0)]
    [InlineData(21, 1)]
    [InlineData(17, 5)]
    public void SA_NOC_salida_anticipada_nocturna_sin_tolerancia(int minutoSalida, int anticipacionEsperada)
    {
        var resultado = Calcular(
            Lunes, Nocturno(), new TimeOnly(19, 0), Lunes.AddDays(1).ToDateTime(new TimeOnly(5, minutoSalida)));

        resultado.MinutosSalidaAnticipada.ShouldBe(anticipacionEsperada);
    }

    [Theory]
    [InlineData(8, 0, 60, 0)]
    [InlineData(9, 0, 120, 0)]
    [InlineData(9, 1, 120, 1)]
    [InlineData(12, 0, 120, 180)]
    [InlineData(7, 10, 10, 0)]
    public void HE_SAB_sabado_es_integramente_extra_sin_umbral(
        int horaSalida,
        int minutoSalida,
        int extra25Esperado,
        int extra35Esperado)
    {
        var resultado = Calcular(Sabado, Diurno(), new TimeOnly(7, 0), Sabado.ToDateTime(new TimeOnly(horaSalida, minutoSalida)));

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Ninguno);
        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtra25.ShouldBe(extra25Esperado);
        resultado.MinutosExtra35.ShouldBe(extra35Esperado);
    }

    [Fact]
    public void DOM_04_domingo_con_turno_dominical_tambien_va_integramente_al_100()
    {
        var turnoDominical = new Turno
        {
            Id = 4,
            Codigo = "DOM1",
            Nombre = "Dominical 1",
            HoraInicio = new TimeOnly(7, 0),
            HoraFin = new TimeOnly(15, 0),
            MinutosJornadaOrdinaria = 480,
            DiasAplicables = DiasSemana.Domingo
        };

        var resultado = Calcular(Domingo, turnoDominical, new TimeOnly(7, 0), Domingo.ToDateTime(new TimeOnly(15, 0)));

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.DescansoSemanal);
        resultado.MinutosTrabajados.ShouldBe(480);
        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
    }

    [Fact]
    public void DOM_05_domingo_no_aplica_umbral_desde_el_primer_minuto()
    {
        var resultado = Calcular(Domingo, Diurno(), new TimeOnly(7, 0), Domingo.ToDateTime(new TimeOnly(7, 10)));

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.DescansoSemanal);
        resultado.MinutosTrabajados.ShouldBe(10);
        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtraTotales.ShouldBe(0);
    }

    private static ResultadoCalculo Calcular(DateOnly fecha, Turno turno, TimeOnly entrada, DateTime salida) =>
        CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = fecha,
            Turno = turno,
            Marcaciones = [fecha.ToDateTime(entrada), salida]
        });

    private static Turno Diurno() => new()
    {
        Id = 1,
        Codigo = "DIU",
        Nombre = "Diurno",
        HoraInicio = new TimeOnly(7, 0),
        HoraFin = new TimeOnly(17, 22),
        MinutosRefrigerio = 0,
        MinutosTolerancia = 5,
        MinutosAnticipacionSalida = 5,
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = DiasSemana.LunesASabado
    };

    private static Turno Nocturno() => new()
    {
        Id = 2,
        Codigo = "NOC",
        Nombre = "Nocturno",
        HoraInicio = new TimeOnly(19, 0),
        HoraFin = new TimeOnly(5, 22),
        MinutosRefrigerio = 0,
        MinutosTolerancia = 5,
        MinutosAnticipacionSalida = 5,
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = DiasSemana.LunesASabado
    };
}
