using ControlMarcacion.Domain.Catalogos;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Catalogos;

/// <summary>Contratos Fase 6.2 para programaciones variables y descanso explícito.</summary>
public sealed class RotacionesVariablesAcceptanceTests
{
    private const long Diurno = 1;
    private const long Nocturno = 2;
    private static readonly DateOnly Ancla = new(2026, 7, 6);

    [Fact]
    public void ROT_01_admite_siete_dias_dia_y_siete_dias_noche()
    {
        var ciclo = Crear((Diurno, 7), (Nocturno, 7));

        ciclo.TurnoEn(Ancla).ShouldBe(Diurno);
        ciclo.TurnoEn(Ancla.AddDays(6)).ShouldBe(Diurno);
        ciclo.TurnoEn(Ancla.AddDays(7)).ShouldBe(Nocturno);
        ciclo.TurnoEn(Ancla.AddDays(14)).ShouldBeNull();
    }

    [Fact]
    public void ROT_02_admite_diez_dias_dia_y_cinco_dias_noche()
    {
        var ciclo = Crear((Diurno, 10), (Nocturno, 5));

        ciclo.TurnoEn(Ancla.AddDays(9)).ShouldBe(Diurno);
        ciclo.TurnoEn(Ancla.AddDays(10)).ShouldBe(Nocturno);
        ciclo.TurnoEn(Ancla.AddDays(14)).ShouldBe(Nocturno);
        ciclo.TurnoEn(Ancla.AddDays(15)).ShouldBeNull();
    }

    [Fact]
    public void ROT_DESC_01_un_descanso_explicito_deberia_poder_existir_sin_turno()
    {
        var tipoTurnoId = typeof(TramoRotacion)
            .GetProperty(nameof(TramoRotacion.TurnoId))!
            .PropertyType;

        Nullable.GetUnderlyingType(tipoTurnoId).ShouldBe(
            typeof(long),
            "un descanso explícito de un día no debe exigir un TurnoId ni un turno ficticio");
    }

    [Fact]
    public void ROT_DESC_05_resuelve_trabajo_descanso_trabajo_en_sus_fechas()
    {
        var secuencia = new ProgramacionCuadrilla
        {
            Id = 40,
            FechaInicio = Ancla,
            FechaAncla = Ancla,
            FechaFin = Ancla.AddDays(21)
        };
        secuencia.Tramos.Add(new TramoRotacion
            { Id = 1, Orden = 1, Tipo = TipoTramoRotacion.Trabajo, TurnoId = Diurno, Dias = 14 });
        secuencia.Tramos.Add(new TramoRotacion
            { Id = 2, Orden = 2, Tipo = TipoTramoRotacion.Descanso, Dias = 1 });
        secuencia.Tramos.Add(new TramoRotacion
            { Id = 3, Orden = 3, Tipo = TipoTramoRotacion.Trabajo, TurnoId = Nocturno, Dias = 7 });

        secuencia.ResolverEn(Ancla.AddDays(13)).Estado.ShouldBe(EstadoProgramacionDiaria.Trabajo);
        secuencia.ResolverEn(Ancla.AddDays(14)).Estado.ShouldBe(EstadoProgramacionDiaria.DescansoProgramado);
        secuencia.ResolverEn(Ancla.AddDays(15)).TurnoId.ShouldBe(Nocturno);
    }

    [Fact]
    public void ROT_DESC_04_una_secuencia_solo_de_trabajo_conserva_sus_fronteras()
    {
        var secuencia = Crear((Diurno, 7), (Nocturno, 7));

        secuencia.ResolverEn(Ancla.AddDays(6)).TurnoId.ShouldBe(Diurno);
        secuencia.ResolverEn(Ancla.AddDays(7)).TurnoId.ShouldBe(Nocturno);
        secuencia.ResolverEn(Ancla.AddDays(13)).TurnoId.ShouldBe(Nocturno);
    }

    [Fact]
    public void ROT_DESC_06_una_programacion_futura_no_reinterpreta_la_anterior()
    {
        var historica = Crear((Diurno, 2));
        historica.FechaFin = Ancla.AddDays(1);
        var futura = new ProgramacionCuadrilla
        {
            FechaInicio = Ancla.AddDays(2),
            FechaAncla = Ancla.AddDays(2),
            FechaFin = Ancla.AddDays(2)
        };
        futura.Tramos.Add(new TramoRotacion
            { Orden = 1, Tipo = TipoTramoRotacion.Trabajo, TurnoId = Nocturno, Dias = 1 });

        historica.ResolverEn(Ancla.AddDays(1)).TurnoId.ShouldBe(Diurno);
        futura.ResolverEn(Ancla.AddDays(2)).TurnoId.ShouldBe(Nocturno);
    }

    [Fact]
    public void ROT_DESC_07_descanso_programado_no_es_sin_programacion()
    {
        var secuencia = new ProgramacionCuadrilla
        {
            FechaInicio = Ancla,
            FechaAncla = Ancla,
            FechaFin = Ancla
        };
        secuencia.Tramos.Add(new TramoRotacion
            { Orden = 1, Tipo = TipoTramoRotacion.Descanso, Dias = 1 });

        secuencia.ResolverEn(Ancla).Estado.ShouldBe(EstadoProgramacionDiaria.DescansoProgramado);
        secuencia.ResolverEn(Ancla.AddDays(1)).Estado.ShouldBe(EstadoProgramacionDiaria.SinProgramacion);
    }

    [Fact]
    public void ROT_DESC_08_configuracion_invalida_no_se_degrada_a_descanso()
    {
        var secuencia = new ProgramacionCuadrilla { FechaInicio = Ancla, FechaAncla = Ancla };
        secuencia.Tramos.Add(new TramoRotacion
            { Orden = 1, Tipo = TipoTramoRotacion.Trabajo, Dias = 1 });

        secuencia.ResolverEn(Ancla).Estado.ShouldBe(EstadoProgramacionDiaria.ErrorConfiguracion);
    }

    [Fact]
    public void ROT_REP_01_la_secuencia_no_vuelve_automaticamente_al_primer_tramo()
    {
        var secuencia = Crear((Diurno, 1), (Nocturno, 1));

        secuencia.ResolverEn(Ancla.AddDays(2)).Estado.ShouldBe(EstadoProgramacionDiaria.SinProgramacion);
    }

    private static ProgramacionCuadrilla Crear(params (long Turno, int Dias)[] tramos)
    {
        var programacion = new ProgramacionCuadrilla
        {
            FechaInicio = Ancla,
            FechaAncla = Ancla
        };

        var orden = 1;
        foreach (var (turno, dias) in tramos)
        {
            programacion.Tramos.Add(new TramoRotacion
            {
                Orden = orden++,
                Tipo = TipoTramoRotacion.Trabajo,
                TurnoId = turno,
                Dias = dias
            });
        }

        return programacion;
    }
}
