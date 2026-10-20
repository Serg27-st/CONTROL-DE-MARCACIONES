using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>
/// Contratos Fase 6.2 para RQ-003 y RQ-006. Describen el resultado requerido,
/// aunque la implementación todavía use reparto ordinario y anticipación configurada.
/// </summary>
public sealed class DomingoYSalidaAnticipadaAcceptanceTests
{
    private static readonly DateOnly Domingo = new(2026, 7, 5);
    private static readonly DateOnly Lunes = new(2026, 7, 6);

    [Fact]
    public void DOM_01_domingo_de_07_00_a_09_00_es_integramente_extra_100()
    {
        var resultado = CalcularDomingo(TurnoDiurno(), new TimeOnly(7, 0), new TimeOnly(9, 0));

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.DescansoSemanal);
        resultado.MinutosTrabajados.ShouldBe(120);
        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
        MinutosExtra100(resultado).ShouldBe(120);
    }

    [Fact]
    public void DOM_02_domingo_de_07_00_a_17_22_es_integramente_extra_100()
    {
        var resultado = CalcularDomingo(TurnoDiurno(), new TimeOnly(7, 0), new TimeOnly(17, 22));

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.DescansoSemanal);
        resultado.MinutosTrabajados.ShouldBe(622);
        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
        MinutosExtra100(resultado).ShouldBe(622);
    }

    [Fact]
    public void DOM_03_domingo_nocturno_conserva_nocturnidad_sin_generar_ordinarios()
    {
        var resultado = CalcularDomingo(TurnoNocturno(), new TimeOnly(22, 0), new TimeOnly(6, 0), salidaDiaSiguiente: true);

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.DescansoSemanal);
        resultado.MinutosTrabajados.ShouldBe(480);
        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
        MinutosExtra100(resultado).ShouldBe(480);
        resultado.MinutosNocturnos.ShouldBe(480);
    }

    [Theory]
    [InlineData(23, 0)]
    [InlineData(22, 0)]
    [InlineData(21, 1)]
    [InlineData(17, 5)]
    [InlineData(0, 22)]
    public void SA_00_04_salida_anticipada_de_planilla_no_aplica_tolerancia(
        int minutoSalida,
        int anticipacionEsperada)
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Lunes,
            Turno = TurnoDiurno(),
            Marcaciones =
            [
                Lunes.ToDateTime(new TimeOnly(7, 0)),
                Lunes.ToDateTime(new TimeOnly(17, minutoSalida))
            ]
        });

        resultado.MinutosSalidaAnticipada.ShouldBe(anticipacionEsperada);
    }

    private static int MinutosExtra100(ResultadoCalculo resultado) =>
        resultado.TipoDiaNoLaborable == TipoDiaNoLaborable.DescansoSemanal
            ? resultado.MinutosTrabajados
            : 0;

    private static ResultadoCalculo CalcularDomingo(
        Turno turno,
        TimeOnly entrada,
        TimeOnly salida,
        bool salidaDiaSiguiente = false) =>
        CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Domingo,
            Turno = turno,
            Marcaciones =
            [
                Domingo.ToDateTime(entrada),
                Domingo.AddDays(salidaDiaSiguiente ? 1 : 0).ToDateTime(salida)
            ]
        });

    private static Turno TurnoDiurno() => new()
    {
        Id = 901,
        Codigo = "DIU-F62",
        Nombre = "Diurno Fase 6.2",
        HoraInicio = new TimeOnly(7, 0),
        HoraFin = new TimeOnly(17, 22),
        MinutosRefrigerio = 0,
        MinutosTolerancia = 5,
        MinutosAnticipacionSalida = 5,
        MinutosJornadaOrdinaria = 622,
        DiasAplicables = DiasSemana.LunesASabado
    };

    private static Turno TurnoNocturno() => new()
    {
        Id = 902,
        Codigo = "NOC-F62",
        Nombre = "Nocturno Fase 6.2",
        HoraInicio = new TimeOnly(22, 0),
        HoraFin = new TimeOnly(6, 0),
        MinutosRefrigerio = 0,
        MinutosTolerancia = 5,
        MinutosAnticipacionSalida = 5,
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = DiasSemana.LunesASabado
    };
}
