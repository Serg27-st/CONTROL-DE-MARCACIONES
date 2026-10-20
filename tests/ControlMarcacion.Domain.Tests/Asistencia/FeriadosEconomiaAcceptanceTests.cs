using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>Contratos físicos FER-NAC-02/03/05/06 y FER-007.</summary>
public sealed class FeriadosEconomiaAcceptanceTests
{
    [Fact]
    public void FER_007_1_de_mayo_en_descanso_semanal_sin_trabajo_conserva_la_coincidencia()
    {
        var fecha = new DateOnly(2026, 5, 1);

        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = fecha,
            Turno = Diurno(DiasSemana.Lunes | DiasSemana.Martes
                           | DiasSemana.Miercoles | DiasSemana.Jueves),
            EsFeriado = true
        });

        resultado.Estado.ShouldBe(EstadoTareo.Feriado);
        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Feriado);
        resultado.CoincidenciaFeriadoDescanso
            .ShouldBe(TipoCoincidenciaFeriadoDescanso.PrimeroDeMayo);
        resultado.MinutosTrabajados.ShouldBe(0);
    }

    [Fact]
    public void FER_007_1_de_mayo_domingo_es_coincidencia_aunque_el_turno_incluya_domingo()
    {
        var fecha = new DateOnly(2022, 5, 1);

        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = fecha,
            Turno = Diurno(DiasSemana.Todos),
            EsFeriado = true,
            Marcaciones = [fecha.ToDateTime(new TimeOnly(7, 0)), fecha.ToDateTime(new TimeOnly(15, 0))]
        });

        resultado.CoincidenciaFeriadoDescanso
            .ShouldBe(TipoCoincidenciaFeriadoDescanso.PrimeroDeMayo);
        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Feriado);
        resultado.MinutosTrabajados.ShouldBe(480);
    }

    [Fact]
    public void FER_002_trabajo_parcial_conserva_solo_los_minutos_fisicos_reales()
    {
        var fecha = new DateOnly(2026, 7, 28);

        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = fecha,
            Turno = Diurno(DiasSemana.Todos),
            EsFeriado = true,
            Marcaciones = [fecha.ToDateTime(new TimeOnly(7, 0)), fecha.ToDateTime(new TimeOnly(9, 0))]
        });

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Feriado);
        resultado.MinutosTrabajados.ShouldBe(120);
        resultado.MinutosExtra100.ShouldBe(120);
    }

    [Fact]
    public void FER_NAC_05_turno_iniciado_antes_del_feriado_conserva_el_dia_de_inicio()
    {
        var fechaInicio = new DateOnly(2026, 4, 30);

        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = fechaInicio,
            Turno = Nocturno(),
            EsFeriado = false,
            Marcaciones =
            [
                fechaInicio.ToDateTime(new TimeOnly(19, 0)),
                fechaInicio.AddDays(1).ToDateTime(new TimeOnly(5, 22))
            ]
        });

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Ninguno);
        resultado.MinutosTrabajados.ShouldBe(622);
    }

    [Fact]
    public void FER_NAC_06_turno_iniciado_en_feriado_conserva_feriado_hasta_el_dia_siguiente()
    {
        var fechaInicio = new DateOnly(2026, 5, 1);

        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = fechaInicio,
            Turno = Nocturno(),
            EsFeriado = true,
            Marcaciones =
            [
                fechaInicio.ToDateTime(new TimeOnly(19, 0)),
                fechaInicio.AddDays(1).ToDateTime(new TimeOnly(5, 22))
            ]
        });

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Feriado);
        resultado.MinutosTrabajados.ShouldBe(622);
        resultado.MinutosExtra100.ShouldBe(622);
    }

    private static Turno Diurno(DiasSemana dias) => new()
    {
        HoraInicio = new TimeOnly(7, 0),
        HoraFin = new TimeOnly(15, 0),
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = dias
    };

    private static Turno Nocturno() => new()
    {
        HoraInicio = new TimeOnly(19, 0),
        HoraFin = new TimeOnly(5, 22),
        MinutosJornadaOrdinaria = 622,
        DiasAplicables = DiasSemana.Todos
    };
}
