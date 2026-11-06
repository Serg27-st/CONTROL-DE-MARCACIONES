using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

public sealed class ToleranciaRecuperableRotacionTests
{
    private static readonly DateOnly Lunes = new(2026, 10, 26);

    [Theory]
    [InlineData(8, 0, 8, 472, 0)]
    [InlineData(8, 5, 3, 477, 0)]
    [InlineData(8, 8, 0, 480, 0)]
    [InlineData(8, 20, 0, 480, 0)]
    [InlineData(8, 23, 0, 480, 15)]
    [InlineData(12, 12, 2, 478, 0)]
    public void Rotacion_recupera_hasta_la_tolerancia_sin_duplicar_horas_extra(
        int retraso,
        int salidaPosterior,
        int tardanzaEsperada,
        int ordinariosEsperados,
        int extrasEsperadas)
    {
        var resultado = Calcular(retraso, salidaPosterior, esRotacion: true);

        resultado.MinutosTardanza.ShouldBe(tardanzaEsperada);
        resultado.MinutosOrdinarios.ShouldBe(ordinariosEsperados);
        resultado.MinutosExtraTotales.ShouldBe(extrasEsperadas);

        if (Math.Min(Math.Min(retraso, 10), salidaPosterior) > 0)
        {
            resultado.Observacion.ShouldContain("recuperó");
        }
    }

    [Fact]
    public void Jornada_no_rotativa_conserva_tolerancia_cero_sin_compensacion()
    {
        var resultado = Calcular(retraso: 5, salidaPosterior: 5, esRotacion: false);

        resultado.MinutosTardanza.ShouldBe(5);
        resultado.MinutosOrdinarios.ShouldBe(475);
        resultado.MinutosExtraTotales.ShouldBe(0);
        resultado.Observacion.ShouldNotContain("recuperó");
    }

    [Fact]
    public void Rotacion_industrial_lunes_a_sabado_conserva_tolerancia_cero()
    {
        var resultado = Calcular(
            retraso: 5,
            salidaPosterior: 5,
            esRotacion: true,
            diasAplicables: DiasSemana.LunesASabado);

        resultado.MinutosTardanza.ShouldBe(5);
        resultado.MinutosOrdinarios.ShouldBe(475);
        resultado.MinutosExtraTotales.ShouldBe(0);
        resultado.Observacion.ShouldNotContain("recuperó");
    }

    private static ResultadoCalculo Calcular(
        int retraso,
        int salidaPosterior,
        bool esRotacion,
        DiasSemana diasAplicables = DiasSemana.Todos)
    {
        var turno = new Turno
        {
            Id = 201,
            Codigo = "HOTEL",
            Nombre = "Hotel",
            HoraInicio = new TimeOnly(7, 0),
            HoraFin = new TimeOnly(16, 0),
            MinutosRefrigerio = 60,
            MinutosTolerancia = 10,
            MinutosAnticipacionEntrada = 15,
            MinutosAnticipacionSalida = 5,
            MinutosJornadaOrdinaria = 480,
            DiasAplicables = diasAplicables,
            Activo = true
        };

        return CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Lunes,
            Turno = turno,
            EsTrabajoProgramadoEnRotacion = esRotacion,
            Marcaciones =
            [
                Lunes.ToDateTime(turno.HoraInicio).AddMinutes(retraso),
                Lunes.ToDateTime(turno.HoraFin).AddMinutes(salidaPosterior)
            ]
        });
    }
}
