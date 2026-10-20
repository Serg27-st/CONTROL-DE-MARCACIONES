using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Personal;

/// <summary>Contratos PL3D-INV-01..10 de escrituras futuras del vínculo laboral.</summary>
public sealed class PeriodoLaboralFase3DInvariantesTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);
    private static readonly DateOnly Fin = new(2026, 6, 30);

    [Fact]
    public void PL3D_INV_01_rechaza_un_segundo_periodo_abierto()
    {
        var trabajador = Activo();

        Should.Throw<ReglaNegocioException>(() =>
            trabajador.Reingresar(new DateOnly(2027, 1, 1), 1, 2, "prueba"));

        trabajador.PeriodosLaborales.ShouldHaveSingleItem().EstaAbierto.ShouldBeTrue();
    }

    [Fact]
    public void PL3D_INV_02_rechaza_el_solapamiento_parcial_del_reingreso()
    {
        var trabajador = Cerrado();

        Should.Throw<ReglaNegocioException>(() =>
            trabajador.Reingresar(new DateOnly(2026, 6, 15), 1, 2, "prueba"));

        trabajador.PeriodosLaborales.Count.ShouldBe(1);
    }

    [Fact]
    public void PL3D_INV_03_acepta_periodos_contiguos()
    {
        var trabajador = Cerrado();

        trabajador.Reingresar(Fin.AddDays(1), 1, 2, "prueba");

        Should.NotThrow(trabajador.ValidarPeriodosLaborales);
        trabajador.PeriodosLaborales.Count.ShouldBe(2);
    }

    [Fact]
    public void PL3D_INV_04_acepta_un_gap_sin_rellenarlo()
    {
        var trabajador = Cerrado();
        var reingreso = new DateOnly(2026, 9, 1);

        trabajador.Reingresar(reingreso, 1, 2, "prueba");

        trabajador.ObtenerPeriodoVigente(new DateOnly(2026, 8, 1)).ShouldBeNull();
        trabajador.PeriodosLaborales.Single(p => p.EstaAbierto).FechaIngreso.ShouldBe(reingreso);
    }

    [Fact]
    public void PL3D_INV_05_rechaza_fecha_fin_anterior_al_inicio()
    {
        var trabajador = Nuevo();
        trabajador.FechaCese = Inicio.AddDays(-1);
        trabajador.MotivoCese = MotivoCese.TerminoContrato;

        Should.Throw<ReglaNegocioException>(() => trabajador.AbrirVinculo("prueba"));
        trabajador.PeriodosLaborales.ShouldBeEmpty();
    }

    [Fact]
    public void PL3D_INV_06_rechaza_empresa_o_planta_no_validas_en_dominio()
    {
        var sinEmpresa = Nuevo();
        sinEmpresa.EmpresaId = 0;
        Should.Throw<ReglaNegocioException>(() => sinEmpresa.AbrirVinculo("prueba"));

        var sinPlanta = Nuevo();
        sinPlanta.PlantaId = 0;
        Should.Throw<ReglaNegocioException>(() => sinPlanta.AbrirVinculo("prueba"));
    }

    [Fact]
    public void PL3D_INV_07_cese_sin_periodo_abierto_falla_explicita_y_no_reescribe_historia()
    {
        var trabajador = Cerrado();
        var historico = trabajador.PeriodosLaborales.Single();
        trabajador.FechaCese = null;
        trabajador.MotivoCese = null;

        var error = Should.Throw<ReglaNegocioException>(() =>
            trabajador.DarDeBaja(Fin.AddMonths(1), MotivoCese.Despido, null, "prueba"));

        error.Message.ShouldContain("no tiene un periodo laboral abierto");
        historico.FechaCese.ShouldBe(Fin);
        trabajador.FechaCese.ShouldBeNull();
    }

    [Fact]
    public void PL3D_INV_08_cese_con_dos_periodos_abiertos_falla_sin_elegir_uno()
    {
        var trabajador = Activo();
        trabajador.PeriodosLaborales.Add(PeriodoAbierto(new DateOnly(2027, 1, 1), 1, 2));

        var error = Should.Throw<ReglaNegocioException>(() =>
            trabajador.DarDeBaja(Fin, MotivoCese.Despido, null, "prueba"));

        error.Message.ShouldContain("más de un periodo laboral abierto");
        trabajador.PeriodosLaborales.Count(p => p.EstaAbierto).ShouldBe(2);
        trabajador.FechaCese.ShouldBeNull();
    }

    [Fact]
    public void PL3D_INV_09_reingreso_y_cambio_de_planta_no_reescriben_el_periodo_cerrado()
    {
        var trabajador = Cerrado();
        var historico = trabajador.PeriodosLaborales.Single();
        var plantaHistorica = historico.AsignacionesPlanta.Single();

        trabajador.Reingresar(new DateOnly(2026, 9, 1), 2, 3, "prueba");
        trabajador.CambiarPlanta(4, new DateOnly(2026, 10, 1), "prueba");

        historico.EmpresaId.ShouldBe(1);
        historico.FechaIngreso.ShouldBe(Inicio);
        historico.FechaCese.ShouldBe(Fin);
        plantaHistorica.PlantaId.ShouldBe(2);
        plantaHistorica.FechaInicio.ShouldBe(Inicio);
        plantaHistorica.FechaFin.ShouldBe(Fin);
    }

    [Fact]
    public void PL3D_INV_10_reingreso_actualiza_solo_la_proyeccion_actual()
    {
        var trabajador = Cerrado();
        var reingreso = new DateOnly(2027, 2, 1);

        trabajador.Reingresar(reingreso, 2, 3, "prueba");

        trabajador.FechaIngreso.ShouldBe(reingreso);
        trabajador.FechaCese.ShouldBeNull();
        trabajador.EmpresaId.ShouldBe(2);
        trabajador.PlantaId.ShouldBe(3);
        trabajador.PeriodosLaborales.Single(p => p.FechaCese == Fin).EmpresaId.ShouldBe(1);
    }

    private static Trabajador Activo()
    {
        var trabajador = Nuevo();
        trabajador.AbrirVinculo("prueba");
        return trabajador;
    }

    private static Trabajador Cerrado()
    {
        var trabajador = Activo();
        trabajador.DarDeBaja(Fin, MotivoCese.TerminoContrato, null, "prueba");
        return trabajador;
    }

    private static Trabajador Nuevo() => new()
    {
        Id = 10,
        TipoDocumentoId = 1,
        NumeroDocumento = "45678901",
        ApellidoPaterno = "FASE",
        ApellidoMaterno = "TRES",
        Nombres = "INVARIANTE",
        EmpresaId = 1,
        PlantaId = 2,
        PuestoId = 1,
        FechaIngreso = Inicio
    };

    private static PeriodoLaboral PeriodoAbierto(DateOnly inicio, long empresaId, long plantaId)
    {
        var periodo = new PeriodoLaboral
        {
            EmpresaId = empresaId,
            FechaIngreso = inicio
        };
        periodo.AsignarPlanta(plantaId, inicio, null, "prueba");
        return periodo;
    }
}
