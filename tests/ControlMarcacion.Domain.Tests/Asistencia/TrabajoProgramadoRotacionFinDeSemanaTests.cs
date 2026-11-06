using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

public sealed class TrabajoProgramadoRotacionFinDeSemanaTests
{
    private static readonly DateOnly Sabado = new(2026, 10, 24);
    private static readonly DateOnly Domingo = new(2026, 10, 25);

    [Theory]
    [MemberData(nameof(DiasOrdinariosDeRotacion))]
    public void Trabajo_programado_en_fin_de_semana_es_jornada_ordinaria(DateOnly fecha)
    {
        var resultado = Calcular(
            fecha,
            TurnoDiurno(),
            fecha.ToDateTime(new TimeOnly(7, 0)),
            fecha.ToDateTime(new TimeOnly(16, 0)));

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Ninguno);
        resultado.MinutosTrabajados.ShouldBe(480);
        resultado.MinutosOrdinarios.ShouldBe(480);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
        resultado.MinutosExtra100.ShouldBe(0);
    }

    [Fact]
    public void Trabajo_programado_nocturno_del_sabado_conserva_la_jornada_del_dia_de_inicio()
    {
        var resultado = Calcular(
            Sabado,
            TurnoNocturno(),
            Sabado.ToDateTime(new TimeOnly(22, 0)),
            Domingo.ToDateTime(new TimeOnly(7, 0)));

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Ninguno);
        resultado.MinutosTrabajados.ShouldBe(480);
        resultado.MinutosOrdinarios.ShouldBe(480);
        resultado.MinutosExtraTotales.ShouldBe(0);
        resultado.MinutosExtra100.ShouldBe(0);
    }

    [Fact]
    public void Rotacion_industrial_lunes_a_sabado_conserva_el_sabado_como_sobretiempo()
    {
        var turnoIndustrial = TurnoDiurno();
        turnoIndustrial.DiasAplicables = DiasSemana.LunesASabado;

        var resultado = Calcular(
            Sabado,
            turnoIndustrial,
            Sabado.ToDateTime(new TimeOnly(7, 0)),
            Sabado.ToDateTime(new TimeOnly(16, 0)));

        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtraTotales.ShouldBe(480);
    }

    [Theory]
    [InlineData(14, 0)]
    [InlineData(15, 15)]
    public void Trabajo_programado_en_domingo_aplica_el_umbral_ordinario_de_sobretiempo(
        int exceso,
        int extraEsperada)
    {
        var resultado = Calcular(
            Domingo,
            TurnoDiurno(),
            Domingo.ToDateTime(new TimeOnly(7, 0)),
            Domingo.ToDateTime(new TimeOnly(16, 0)).AddMinutes(exceso));

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Ninguno);
        resultado.MinutosOrdinarios.ShouldBe(480);
        resultado.MinutosExtraTotales.ShouldBe(extraEsperada);
        resultado.MinutosExtra100.ShouldBe(0);
    }

    public static TheoryData<DateOnly> DiasOrdinariosDeRotacion => new()
    {
        Sabado,
        Domingo
    };

    private static ResultadoCalculo Calcular(
        DateOnly fecha,
        Turno turno,
        DateTime entrada,
        DateTime salida) =>
        CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = fecha,
            Turno = turno,
            EsTrabajoProgramadoEnRotacion = true,
            Marcaciones = [entrada, salida]
        });

    private static Turno TurnoDiurno() => new()
    {
        Id = 101,
        Codigo = "HOTEL-D",
        Nombre = "Hotel diurno",
        HoraInicio = new TimeOnly(7, 0),
        HoraFin = new TimeOnly(16, 0),
        MinutosRefrigerio = 60,
        MinutosTolerancia = 10,
        MinutosAnticipacionEntrada = 15,
        MinutosAnticipacionSalida = 5,
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = DiasSemana.Todos,
        Activo = true
    };

    private static Turno TurnoNocturno() => new()
    {
        Id = 102,
        Codigo = "HOTEL-N",
        Nombre = "Hotel nocturno",
        HoraInicio = new TimeOnly(22, 0),
        HoraFin = new TimeOnly(7, 0),
        MinutosRefrigerio = 60,
        MinutosTolerancia = 10,
        MinutosAnticipacionEntrada = 15,
        MinutosAnticipacionSalida = 5,
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = DiasSemana.Todos,
        Activo = true
    };
}
