using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Catalogos;

/// <summary>
/// El ciclo de una cuadrilla: qué turno toca cada día y cuándo cambia.
/// </summary>
/// <remarks>
/// El escenario es el de julio de 2026: la cuadrilla rota el lunes tras el descanso del
/// domingo. El lunes 6 de julio empieza un ciclo de 14 días de diurno y 14 de nocturno.
/// </remarks>
public sealed class ProgramacionCuadrillaTests
{
    private const long Diurno = 1;
    private const long Nocturno = 2;
    private const long Sabatino = 3;

    private static readonly DateOnly Lunes6 = new(2026, 7, 6);

    private static ProgramacionCuadrilla Ciclo(DateOnly ancla, params (long Turno, int Dias)[] tramos)
    {
        var programacion = new ProgramacionCuadrilla
        {
            FechaInicio = ancla,
            FechaAncla = ancla
        };

        var orden = 1;

        foreach (var (turno, dias) in tramos)
        {
            programacion.Tramos.Add(new TramoRotacion { Orden = orden++, TurnoId = turno, Dias = dias });
        }

        return programacion;
    }

    private static ProgramacionCuadrilla DosPorDos() => Ciclo(Lunes6, (Diurno, 14), (Nocturno, 14));

    [Theory]
    [InlineData(2026, 7, 6, Diurno)]    // primer día del ciclo
    [InlineData(2026, 7, 19, Diurno)]   // domingo 19: último día del tramo diurno
    [InlineData(2026, 7, 20, Nocturno)] // lunes 20: la rotación
    [InlineData(2026, 8, 2, Nocturno)]  // último día del tramo nocturno
    public void Cada_fecha_cae_en_el_tramo_que_le_corresponde(int anio, int mes, int dia, long turnoEsperado)
    {
        DosPorDos().TurnoEn(new DateOnly(anio, mes, dia)).ShouldBe(turnoEsperado);
    }

    [Fact]
    public void Antes_del_ancla_no_hay_programacion()
    {
        DosPorDos().ResolverEn(Lunes6.AddDays(-1)).Estado
            .ShouldBe(EstadoProgramacionDiaria.SinProgramacion);
    }

    [Fact]
    public void El_dia_dentro_del_tramo_se_cuenta_desde_cero()
    {
        var (tramo, dia) = DosPorDos().TramoEn(new DateOnly(2026, 7, 22));

        tramo.TurnoId.ShouldBe(Nocturno);
        dia.ShouldBe(2);
    }

    [Fact]
    public void Un_ciclo_de_tres_tramos_respeta_el_orden_y_no_el_de_insercion()
    {
        var programacion = new ProgramacionCuadrilla { FechaInicio = Lunes6, FechaAncla = Lunes6 };
        programacion.Tramos.Add(new TramoRotacion { Orden = 3, TurnoId = Sabatino, Dias = 2 });
        programacion.Tramos.Add(new TramoRotacion { Orden = 1, TurnoId = Diurno, Dias = 5 });
        programacion.Tramos.Add(new TramoRotacion { Orden = 2, TurnoId = Nocturno, Dias = 5 });

        programacion.TurnoEn(Lunes6).ShouldBe(Diurno);
        programacion.TurnoEn(Lunes6.AddDays(5)).ShouldBe(Nocturno);
        programacion.TurnoEn(Lunes6.AddDays(10)).ShouldBe(Sabatino);
        programacion.TurnoEn(Lunes6.AddDays(12)).ShouldBeNull();
    }

    [Fact]
    public void Los_proximos_cambios_son_las_fechas_de_rotacion_con_su_turno()
    {
        var cambios = DosPorDos().ProximosCambios(Lunes6, 3);

        cambios.ShouldBe([(new DateOnly(2026, 7, 20), Nocturno)]);
    }

    /// <summary>Dos tramos seguidos del mismo turno no son una rotación.</summary>
    [Fact]
    public void Dos_tramos_seguidos_del_mismo_turno_no_cuentan_como_cambio()
    {
        var programacion = Ciclo(Lunes6, (Diurno, 7), (Diurno, 7), (Nocturno, 14));

        programacion.ProximosCambios(Lunes6, 1).ShouldBe([(new DateOnly(2026, 7, 20), Nocturno)]);
    }

    [Fact]
    public void Un_tramo_de_cero_dias_no_es_un_ciclo_valido()
    {
        var programacion = Ciclo(Lunes6, (Diurno, 14), (Nocturno, 0));

        Should.Throw<ReglaNegocioException>(() => programacion.TurnoEn(Lunes6));
    }

    [Fact]
    public void Una_programacion_dada_de_baja_no_esta_vigente_nunca()
    {
        var programacion = DosPorDos();

        programacion.EstaVigenteEn(Lunes6).ShouldBeTrue();

        programacion.Activo = false;

        programacion.EstaVigenteEn(Lunes6).ShouldBeFalse();
    }
}
