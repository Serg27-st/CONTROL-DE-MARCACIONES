using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>Contratos HEQA-01..12 de la remediación R-QA-E2E-004-A.</summary>
public sealed class PoliticaEconomicaQaAcceptanceTests
{
    private static readonly DateOnly Lunes = new(2026, 11, 2);

    private static Turno Diu(int refrigerio = 0, int ordinarios = 622) => new()
    {
        Id = 901,
        Codigo = "DIU-QA",
        Nombre = "Diurno QA",
        HoraInicio = new TimeOnly(7, 0),
        HoraFin = new TimeOnly(17, 22),
        MinutosRefrigerio = refrigerio,
        MinutosJornadaOrdinaria = ordinarios,
        DiasAplicables = DiasSemana.Todos
    };

    private static Turno TurnoQa(
        long id,
        string codigo,
        TimeOnly inicio,
        TimeOnly fin,
        int refrigerio,
        int ordinarios,
        DiasSemana dias) => new()
    {
        Id = id,
        Codigo = codigo,
        Nombre = codigo,
        HoraInicio = inicio,
        HoraFin = fin,
        MinutosRefrigerio = refrigerio,
        MinutosJornadaOrdinaria = ordinarios,
        DiasAplicables = dias
    };

    private static EntradaCalculo Entrada(
        Turno? turno,
        DateOnly fecha,
        DateTime entrada,
        DateTime salida,
        bool rotacion = true) => new()
    {
        Fecha = fecha,
        Turno = turno,
        EsTrabajoProgramadoEnRotacion = rotacion,
        Marcaciones = [entrada, salida]
    };

    [Fact]
    public void HEQA_01_06_salida_teorica_con_politica_aprobada_no_genera_sobretiempo()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Diu(), Lunes,
            new DateTime(2026, 11, 2, 7, 0, 0),
            new DateTime(2026, 11, 2, 17, 22, 0)));

        resultado.MinutosTrabajados.ShouldBe(622);
        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
    }

    [Fact]
    public void HEQA_02_salida_posterior_reparte_solo_la_cola_posterior_al_horario()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Diu(), Lunes,
            new DateTime(2026, 11, 2, 7, 0, 0),
            new DateTime(2026, 11, 2, 17, 45, 0)));

        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtra25.ShouldBe(23);
        resultado.MinutosExtra35.ShouldBe(0);
    }

    [Fact]
    public void HEQA_03_turno_nocturno_se_imputa_al_dia_de_inicio()
    {
        var turno = TurnoQa(
            902, "NOC-QA", new TimeOnly(19, 0), new TimeOnly(5, 22),
            refrigerio: 0, ordinarios: 622, DiasSemana.Todos);
        var resultado = CalculadoraTareo.Calcular(Entrada(
            turno, Lunes,
            new DateTime(2026, 11, 2, 19, 0, 0),
            new DateTime(2026, 11, 3, 5, 22, 0)));

        resultado.MinutosTrabajados.ShouldBe(622);
        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtraTotales.ShouldBe(0);
    }

    [Fact]
    public void HEQA_04_entrada_tardia_reduce_ordinarios_sin_inventar_extra()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Diu(), Lunes,
            new DateTime(2026, 11, 2, 7, 10, 0),
            new DateTime(2026, 11, 2, 17, 22, 0)));

        resultado.MinutosTrabajados.ShouldBe(612);
        resultado.MinutosOrdinarios.ShouldBe(612);
        resultado.MinutosExtraTotales.ShouldBe(0);
        resultado.MinutosTardanza.ShouldBe(10);
    }

    [Fact]
    public void HEQA_05_sin_turno_economico_conserva_marcas_y_no_genera_conceptos()
    {
        DateTime[] marcas =
        [
            new(2026, 11, 2, 7, 0, 0),
            new(2026, 11, 2, 17, 22, 0)
        ];
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Lunes,
            Turno = null,
            MotivoSinTurno = "Falta configuración económica vigente.",
            Marcaciones = marcas
        });

        resultado.Estado.ShouldBe(EstadoTareo.SinHorario);
        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtraTotales.ShouldBe(0);
        marcas.ShouldBe([
            new DateTime(2026, 11, 2, 7, 0, 0),
            new DateTime(2026, 11, 2, 17, 22, 0)
        ]);
    }

    [Fact]
    public void HEQA_07_version_historica_aplica_su_propia_jornada_y_refrigerio()
    {
        var actual = Diu();
        var historica = new VersionTurno
        {
            Id = 7001,
            TurnoId = actual.Id,
            FechaInicio = new DateOnly(2026, 1, 1),
            FechaFin = new DateOnly(2026, 6, 30),
            HoraInicio = new TimeOnly(8, 0),
            HoraFin = new TimeOnly(17, 0),
            MinutosRefrigerio = 60,
            MinutosJornadaOrdinaria = 480,
            MinutosTolerancia = 0,
            MinutosAnticipacionEntrada = 15,
            MinutosAnticipacionSalida = 5,
            MinutosGraciaSalida = 0,
            DiasAplicables = DiasSemana.LunesASabado
        };

        var aplicado = historica.AplicarA(actual);

        aplicado.HoraInicio.ShouldBe(new TimeOnly(8, 0));
        aplicado.MinutosRefrigerio.ShouldBe(60);
        aplicado.MinutosJornadaOrdinaria.ShouldBe(480);
        actual.HoraInicio.ShouldBe(new TimeOnly(7, 0));
        actual.MinutosJornadaOrdinaria.ShouldBe(622);
    }

    [Fact]
    public void HEQA_08_regla_industrial_legacy_permanece_ordinaria_hasta_fin_teorico()
    {
        var industrial = TurnoQa(
            903, "DIU-IND", new TimeOnly(7, 0), new TimeOnly(17, 22),
            refrigerio: 0, ordinarios: 480, DiasSemana.LunesASabado);
        var resultado = CalculadoraTareo.Calcular(Entrada(
            industrial, Lunes,
            new DateTime(2026, 11, 2, 7, 0, 0),
            new DateTime(2026, 11, 2, 17, 22, 0),
            rotacion: false));

        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtraTotales.ShouldBe(0);
    }

    [Fact]
    public void HEQA_09_refrigerio_explicito_se_descuenta_una_sola_vez()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Diu(refrigerio: 60, ordinarios: 562), Lunes,
            new DateTime(2026, 11, 2, 7, 0, 0),
            new DateTime(2026, 11, 2, 17, 22, 0)));

        resultado.MinutosTrabajados.ShouldBe(562);
        resultado.MinutosOrdinarios.ShouldBe(562);
        resultado.MinutosExtraTotales.ShouldBe(0);
    }

    [Fact]
    public void HEQA_10_marca_unica_no_inventa_jornada()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Lunes,
            Turno = Diu(),
            EsTrabajoProgramadoEnRotacion = true,
            Marcaciones = [new DateTime(2026, 11, 2, 7, 0, 0)]
        });

        resultado.Estado.ShouldBe(EstadoTareo.Incompleto);
        resultado.MinutosTrabajados.ShouldBe(0);
        resultado.MinutosExtraTotales.ShouldBe(0);
    }

    [Fact]
    public void HEQA_11_descanso_sin_marcas_no_genera_ausencia_ni_minutos()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Lunes,
            EsDescansoProgramado = true
        });

        resultado.Estado.ShouldBe(EstadoTareo.Descanso);
        resultado.MinutosTrabajados.ShouldBe(0);
        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtraTotales.ShouldBe(0);
    }
}
