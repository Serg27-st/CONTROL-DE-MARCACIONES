using ControlMarcacion.Domain.Asistencia;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

public sealed class DescansoProgramadoAcceptanceTests
{
    private static readonly DateOnly Martes = new(2026, 7, 7);

    [Fact]
    public void ROT_DESC_02_descanso_sin_marcas_no_es_falta_y_tiene_cero_minutos()
    {
        var resultado = Calcular(Martes);

        resultado.Estado.ShouldBe(EstadoTareo.Descanso);
        resultado.MinutosTrabajados.ShouldBe(0);
        resultado.MinutosExtra100.ShouldBe(0);
        resultado.RequiereValidacion.ShouldBeFalse();
    }

    [Fact]
    public void ROT_DESC_03_y_ROT_MARK_01_intervalo_inequivoco_es_todo_HE100()
    {
        var resultado = Calcular(Martes, Marca(Martes, 7), Marca(Martes, 17));

        resultado.MinutosTrabajados.ShouldBe(600);
        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
        resultado.MinutosExtra100.ShouldBe(600);
        resultado.MinutosTardanza.ShouldBe(0);
        resultado.MinutosSalidaAnticipada.ShouldBe(0);
    }

    [Fact]
    public void ROT_MARK_02_una_marca_es_incompleta_y_no_inventa_minutos()
    {
        var resultado = Calcular(Martes, Marca(Martes, 7));

        resultado.Estado.ShouldBe(EstadoTareo.Incompleto);
        resultado.MinutosTrabajados.ShouldBe(0);
        resultado.MinutosExtra100.ShouldBe(0);
        resultado.RequiereValidacion.ShouldBeTrue();
    }

    [Fact]
    public void ROT_MARK_04_marcas_interiores_no_se_emparejan_ni_descuentan_refrigerio()
    {
        var resultado = Calcular(
            Martes,
            Marca(Martes, 7),
            Marca(Martes, 12),
            Marca(Martes, 13),
            Marca(Martes, 17));

        resultado.MinutosTrabajados.ShouldBe(600);
        resultado.MinutosExtra100.ShouldBe(600);
    }

    [Fact]
    public void ROT_MARK_05_rebote_no_produce_una_jornada_falsa()
    {
        var resultado = Calcular(
            Martes,
            Marca(Martes, 7),
            Marca(Martes, 7).AddMinutes(1));

        resultado.Estado.ShouldBe(EstadoTareo.Incompleto);
        resultado.MinutosTrabajados.ShouldBe(0);
    }

    [Fact]
    public void ROT_DESC_09_domingo_y_descanso_generan_HE100_una_sola_vez()
    {
        var domingo = new DateOnly(2026, 7, 12);
        var resultado = Calcular(domingo, Marca(domingo, 7), Marca(domingo, 17));

        resultado.MinutosTrabajados.ShouldBe(600);
        resultado.MinutosExtra100.ShouldBe(600);
        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.DescansoSemanal);
    }

    [Fact]
    public void ROT_FER_01_feriado_ordinario_prevalece_sin_duplicar_minutos()
    {
        var resultado = Calcular(
            Martes,
            [Marca(Martes, 7), Marca(Martes, 17)],
            esFeriado: true);

        resultado.MinutosTrabajados.ShouldBe(600);
        resultado.MinutosExtra100.ShouldBe(600);
        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Feriado);
        resultado.CoincidenciaFeriadoDescanso
            .ShouldBe(TipoCoincidenciaFeriadoDescanso.FeriadoOrdinario);
    }

    [Fact]
    public void ROT_FER_02_primero_de_mayo_conserva_clasificacion_especial_y_minutos_unicos()
    {
        var primeroMayo = new DateOnly(2026, 5, 1);
        var resultado = Calcular(
            primeroMayo,
            [Marca(primeroMayo, 7), Marca(primeroMayo, 17)],
            esFeriado: true);

        resultado.MinutosTrabajados.ShouldBe(600);
        resultado.MinutosExtra100.ShouldBe(600);
        resultado.CoincidenciaFeriadoDescanso
            .ShouldBe(TipoCoincidenciaFeriadoDescanso.PrimeroDeMayo);
    }

    [Fact]
    public void BD01_08_ambiguedad_conserva_cero_y_exige_validacion()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Martes,
            EsDescansoProgramado = true,
            DelimitacionDescansoAmbigua = true,
            Marcaciones = [Marca(Martes, 7), Marca(Martes, 17)]
        });

        resultado.Estado.ShouldBe(EstadoTareo.PendienteValidacion);
        resultado.MinutosTrabajados.ShouldBe(0);
        resultado.RequiereValidacion.ShouldBeTrue();
        resultado.TipoIncidencia.ShouldBe(TipoIncidencia.DelimitacionDescansoAmbigua);
    }

    private static ResultadoCalculo Calcular(DateOnly fecha, params DateTime[] marcas) =>
        Calcular(fecha, marcas, false);

    private static ResultadoCalculo Calcular(
        DateOnly fecha,
        IReadOnlyList<DateTime> marcas,
        bool esFeriado) =>
        CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = fecha,
            EsDescansoProgramado = true,
            EsFeriado = esFeriado,
            Marcaciones = marcas
        });

    private static DateTime Marca(DateOnly fecha, int hora) =>
        fecha.ToDateTime(new TimeOnly(hora, 0));
}
