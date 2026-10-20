using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>
/// R-P0-HE.2: la jornada conserva la fecha en que empieza, pero el tramo que entra al
/// domingo cambia de clasificación económica desde las 00:00.
/// </summary>
public sealed class CrucesNocturnosFinDeSemanaAcceptanceTests
{
    private static readonly DateOnly Viernes = new(2026, 7, 3);
    private static readonly DateOnly Sabado = new(2026, 7, 4);

    [Fact]
    public void NOC_WE_01_viernes_a_sabado_regular_no_convierte_el_sabado_en_horas_extra()
    {
        var resultado = Calcular(Viernes, Viernes.AddDays(1).ToDateTime(new TimeOnly(5, 22)));

        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
        resultado.MinutosExtra100.ShouldBe(0);
        resultado.MinutosNocturnos.ShouldBe(442);
    }

    [Fact]
    public void NOC_WE_02_viernes_a_sabado_solo_reconoce_el_sobretiempo_posterior_al_fin()
    {
        var resultado = Calcular(Viernes, Viernes.AddDays(1).ToDateTime(new TimeOnly(5, 37)));

        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtra25.ShouldBe(15);
        resultado.MinutosExtra35.ShouldBe(0);
        resultado.MinutosExtra100.ShouldBe(0);
        resultado.MinutosNocturnos.ShouldBe(457);
        resultado.MinutosExtra25Nocturnos.ShouldBe(15);
    }

    [Fact]
    public void NOC_WE_03_sabado_a_domingo_reparte_cada_tramo_sin_duplicar_minutos()
    {
        var resultado = Calcular(Sabado, Sabado.AddDays(1).ToDateTime(new TimeOnly(5, 22)));

        resultado.MinutosTrabajados.ShouldBe(622);
        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtra25.ShouldBe(120);
        resultado.MinutosExtra35.ShouldBe(180);
        resultado.MinutosExtra100.ShouldBe(322);
        resultado.MinutosNocturnos.ShouldBe(442);
        resultado.MinutosExtra25Nocturnos.ShouldBe(0);
        resultado.MinutosExtra35Nocturnos.ShouldBe(120);
        (resultado.MinutosExtra35Nocturnos + resultado.MinutosExtra100)
            .ShouldBe(resultado.MinutosNocturnos);
        MinutosClasificados(resultado).ShouldBe(resultado.MinutosTrabajados);
    }

    [Fact]
    public void NOC_WE_04_sabado_sin_llegar_al_domingo_sigue_integramente_en_25_y_35()
    {
        var resultado = Calcular(Sabado, Sabado.ToDateTime(new TimeOnly(23, 0)));

        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtra25.ShouldBe(120);
        resultado.MinutosExtra35.ShouldBe(120);
        resultado.MinutosExtra100.ShouldBe(0);
        resultado.MinutosNocturnos.ShouldBe(60);
        resultado.MinutosExtra35Nocturnos.ShouldBe(60);
    }

    [Fact]
    public void NOC_WE_05_diez_minutos_del_domingo_son_extra100_desde_el_primero()
    {
        var resultado = Calcular(Sabado, Sabado.AddDays(1).ToDateTime(new TimeOnly(0, 10)));

        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtra25.ShouldBe(120);
        resultado.MinutosExtra35.ShouldBe(180);
        resultado.MinutosExtra100.ShouldBe(10);
        resultado.MinutosNocturnos.ShouldBe(130);
        (resultado.MinutosExtra35Nocturnos + resultado.MinutosExtra100)
            .ShouldBe(resultado.MinutosNocturnos);
    }

    [Fact]
    public void NOC_WE_06_un_minuto_del_domingo_es_extra100_sin_umbral()
    {
        var resultado = Calcular(Sabado, Sabado.AddDays(1).ToDateTime(new TimeOnly(0, 1)));

        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtra25.ShouldBe(120);
        resultado.MinutosExtra35.ShouldBe(180);
        resultado.MinutosExtra100.ShouldBe(1);
        resultado.MinutosNocturnos.ShouldBe(121);
        (resultado.MinutosExtra35Nocturnos + resultado.MinutosExtra100)
            .ShouldBe(resultado.MinutosNocturnos);
    }

    private static ResultadoCalculo Calcular(DateOnly fechaJornada, DateTime salida) =>
        CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = fechaJornada,
            Turno = Nocturno(),
            Marcaciones =
            [
                fechaJornada.ToDateTime(new TimeOnly(19, 0)),
                salida
            ]
        });

    private static int MinutosClasificados(ResultadoCalculo resultado) =>
        resultado.MinutosOrdinarios
        + resultado.MinutosExtra25
        + resultado.MinutosExtra35
        + resultado.MinutosExtra100;

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
