using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>
/// Casos de aceptación de Fase 5 contra las reglas confirmadas por negocio.
/// Estas pruebas describen el resultado requerido; mientras el motor continúe
/// repartiendo desde un tope móvil de 480 minutos, sus fallos son evidencia del gap NH-07.
/// </summary>
public sealed class ReglasConfirmadasHorasExtraAcceptanceTests
{
    private static readonly DateOnly Lunes = new(2026, 7, 6);
    private static readonly DateOnly Sabado = new(2026, 7, 4);
    private static readonly DateOnly Domingo = new(2026, 7, 5);

    // R-P0-03.1: el DIU regular es 07:00–17:22 (antes el fixture usaba 07:00–19:00 sembrado).
    private static Turno TurnoDiurno() => new()
    {
        Id = 1,
        Codigo = "DIU",
        Nombre = "Diurno",
        HoraInicio = new TimeOnly(7, 0),
        HoraFin = new TimeOnly(17, 22),
        MinutosRefrigerio = 0,
        MinutosTolerancia = 5,
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = DiasSemana.LunesASabado
    };

    [Fact]
    public void HE_01_lunes_hasta_17_22_es_integramente_ordinario()
    {
        var resultado = Calcular(Lunes, 17, 22);

        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
    }

    [Fact]
    public void HE_02_lunes_dos_horas_despues_de_17_22_son_extra_25()
    {
        var resultado = Calcular(Lunes, 19, 22);

        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtra25.ShouldBe(120);
        resultado.MinutosExtra35.ShouldBe(0);
    }

    [Fact]
    public void HE_03_sabado_de_07_00_a_09_00_es_dos_horas_extra_25()
    {
        var resultado = Calcular(Sabado, 9, 0);

        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtra25.ShouldBe(120);
        resultado.MinutosExtra35.ShouldBe(0);
    }

    [Fact]
    public void HE_04_sabado_de_07_00_a_12_00_reparte_25_y_35()
    {
        var resultado = Calcular(Sabado, 12, 0);

        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtra25.ShouldBe(120);
        resultado.MinutosExtra35.ShouldBe(180);
    }

    [Fact]
    public void HE_05_domingo_trabajado_no_se_reparte_como_ordinario_ni_extra()
    {
        var resultado = Calcular(Domingo, 17, 22);

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.DescansoSemanal);
        resultado.MinutosTrabajados.ShouldBe(622);
        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
    }

    private static ResultadoCalculo Calcular(DateOnly fecha, int horaSalida, int minutoSalida) =>
        CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = fecha,
            Turno = TurnoDiurno(),
            Marcaciones =
            [
                fecha.ToDateTime(new TimeOnly(7, 0)),
                fecha.ToDateTime(new TimeOnly(horaSalida, minutoSalida))
            ]
        });
}
