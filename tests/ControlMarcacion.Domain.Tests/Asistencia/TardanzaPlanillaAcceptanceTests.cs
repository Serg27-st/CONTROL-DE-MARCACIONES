using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>
/// TAR-00..04: la tolerancia operativa configurada no debe convertirse en tolerancia de
/// planilla. Para planilla, cada minuto posterior a las 07:00 cuenta como tardanza.
/// </summary>
public sealed class TardanzaPlanillaAcceptanceTests
{
    private static readonly DateOnly Lunes = new(2026, 7, 6);

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(17, 17)]
    public void TAR_00_04_tardanza_de_planilla_no_aplica_tolerancia(
        int minutosDespuesDeLasSiete,
        int tardanzaEsperada)
    {
        var entrada = new DateTime(2026, 7, 6, 7, 0, 0).AddMinutes(minutosDespuesDeLasSiete);
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Lunes,
            Turno = new Turno
            {
                Id = 1,
                Codigo = "DIU",
                Nombre = "Diurno",
                HoraInicio = new TimeOnly(7, 0),
                HoraFin = new TimeOnly(17, 22),
                MinutosRefrigerio = 0,
                MinutosTolerancia = 5,
                MinutosJornadaOrdinaria = 622,
                DiasAplicables = DiasSemana.LunesASabado
            },
            Marcaciones = [entrada, new DateTime(2026, 7, 6, 17, 22, 0)]
        });

        resultado.MinutosTardanza.ShouldBe(tardanzaEsperada);
    }

    [Fact]
    public void TAR_SA_NOC_01_turno_nocturno_conserva_diferencias_al_cruzar_medianoche()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Lunes,
            Turno = new Turno
            {
                Id = 2,
                Codigo = "NOC",
                Nombre = "Nocturno",
                HoraInicio = new TimeOnly(19, 0),
                HoraFin = new TimeOnly(7, 0),
                MinutosRefrigerio = 0,
                MinutosTolerancia = 5,
                MinutosAnticipacionSalida = 5,
                MinutosJornadaOrdinaria = 720,
                DiasAplicables = DiasSemana.LunesASabado
            },
            Marcaciones =
            [
                Lunes.ToDateTime(new TimeOnly(19, 1)),
                Lunes.AddDays(1).ToDateTime(new TimeOnly(6, 59))
            ]
        });

        resultado.MinutosTardanza.ShouldBe(1);
        resultado.MinutosSalidaAnticipada.ShouldBe(1);
    }
}
