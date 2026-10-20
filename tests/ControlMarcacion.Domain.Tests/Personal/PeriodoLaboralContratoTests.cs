using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Personal;

/// <summary>Contratos PL-001..PL-020 y AP-001..AP-010 del gate P1-DATA.</summary>
public sealed class PeriodoLaboralContratoTests
{
    private static readonly DateOnly Ingreso = new(2026, 1, 10);
    private static readonly DateOnly Cese = new(2026, 3, 31);

    [Fact]
    public void PL_001_alta_inicial_genera_un_periodo_valido()
    {
        var trabajador = NuevoTrabajador();

        trabajador.AbrirVinculo("tester");

        trabajador.ValidarPeriodosLaborales();
        trabajador.PeriodosLaborales.ShouldHaveSingleItem().EmpresaId.ShouldBe(1);
    }

    [Fact]
    public void PL_002_fecha_inicio_es_inclusiva()
    {
        var trabajador = ConVinculoInicial();

        trabajador.TieneVinculoEn(Ingreso).ShouldBeTrue();
    }

    [Fact]
    public void PL_003_fecha_fin_es_inclusiva()
    {
        var trabajador = Cerrado();

        trabajador.TieneVinculoEn(Cese).ShouldBeTrue();
    }

    [Fact]
    public void PL_004_fecha_anterior_al_inicio_no_esta_vigente()
    {
        var trabajador = ConVinculoInicial();

        trabajador.TieneVinculoEn(Ingreso.AddDays(-1)).ShouldBeFalse();
    }

    [Fact]
    public void PL_005_fecha_posterior_al_fin_no_esta_vigente()
    {
        var trabajador = Cerrado();

        trabajador.TieneVinculoEn(Cese.AddDays(1)).ShouldBeFalse();
    }

    [Fact]
    public void PL_006_periodo_abierto_permanece_vigente_hacia_adelante()
    {
        var trabajador = ConVinculoInicial();

        trabajador.TieneVinculoEn(new DateOnly(2035, 12, 31)).ShouldBeTrue();
    }

    [Fact]
    public void PL_007_fecha_fin_anterior_al_inicio_es_rechazada()
    {
        var trabajador = NuevoTrabajador();
        trabajador.FechaCese = Ingreso.AddDays(-1);
        trabajador.MotivoCese = MotivoCese.TerminoContrato;

        Should.Throw<ReglaNegocioException>(() => trabajador.AbrirVinculo("tester"));
    }

    [Fact]
    public void PL_008_segundo_periodo_abierto_es_rechazado()
    {
        var trabajador = ConVinculoInicial();

        Should.Throw<ReglaNegocioException>(() =>
            trabajador.Reingresar(Ingreso.AddYears(1), 1, 2, "tester"));
    }

    [Fact]
    public void PL_009_solapamiento_total_es_rechazado()
    {
        var trabajador = Cerrado();
        trabajador.PeriodosLaborales.Add(Periodo(Ingreso.AddDays(-5), Cese.AddDays(5)));

        Should.Throw<ReglaNegocioException>(trabajador.ValidarPeriodosLaborales);
    }

    [Fact]
    public void PL_010_solapamiento_parcial_inicial_es_rechazado()
    {
        var trabajador = Cerrado();
        trabajador.PeriodosLaborales.Add(Periodo(Ingreso.AddDays(-5), Ingreso.AddDays(5)));

        Should.Throw<ReglaNegocioException>(trabajador.ValidarPeriodosLaborales);
    }

    [Fact]
    public void PL_011_solapamiento_parcial_final_es_rechazado()
    {
        var trabajador = Cerrado();
        trabajador.PeriodosLaborales.Add(Periodo(Cese.AddDays(-5), Cese.AddDays(5)));

        Should.Throw<ReglaNegocioException>(trabajador.ValidarPeriodosLaborales);
    }

    [Fact]
    public void PL_012_periodo_contenido_en_otro_es_rechazado()
    {
        var trabajador = Cerrado();
        trabajador.PeriodosLaborales.Add(Periodo(Ingreso.AddDays(5), Cese.AddDays(-5)));

        Should.Throw<ReglaNegocioException>(trabajador.ValidarPeriodosLaborales);
    }

    [Fact]
    public void PL_013_periodo_historico_seguido_de_otro_no_solapado_es_valido()
    {
        var trabajador = Cerrado();

        trabajador.Reingresar(Cese.AddDays(1), 1, 2, "tester");

        Should.NotThrow(trabajador.ValidarPeriodosLaborales);
    }

    [Fact]
    public void PL_014_hueco_entre_periodos_no_destruye_historial()
    {
        var trabajador = Cerrado();
        var reingreso = Cese.AddMonths(2);

        trabajador.Reingresar(reingreso, 1, 2, "tester");

        trabajador.PeriodosLaborales.Count.ShouldBe(2);
        trabajador.TieneVinculoEn(Cese.AddDays(10)).ShouldBeFalse();
    }

    [Fact]
    public void PL_015_reingreso_conserva_trabajador()
    {
        var trabajador = Cerrado();
        var id = trabajador.Id;

        trabajador.Reingresar(Cese.AddDays(1), 1, 2, "tester");

        trabajador.Id.ShouldBe(id);
    }

    [Fact]
    public void PL_016_reingreso_conserva_periodo_anterior()
    {
        var trabajador = Cerrado();
        var anterior = trabajador.PeriodosLaborales.Single();

        trabajador.Reingresar(Cese.AddDays(1), 1, 2, "tester");

        trabajador.PeriodosLaborales.ShouldContain(anterior);
        anterior.FechaCese.ShouldBe(Cese);
    }

    [Fact]
    public void PL_017_reingreso_crea_nuevo_periodo()
    {
        var trabajador = Cerrado();

        trabajador.Reingresar(Cese.AddDays(1), 1, 2, "tester");

        trabajador.PeriodosLaborales.Count.ShouldBe(2);
        trabajador.PeriodosLaborales.Count(p => p.EstaAbierto).ShouldBe(1);
    }

    [Fact]
    public void PL_018_empresa_puede_diferir_entre_periodos()
    {
        var trabajador = Cerrado();

        trabajador.Reingresar(Cese.AddDays(1), 2, 3, "tester");

        trabajador.EmpresaEn(Ingreso).ShouldBe(1);
        trabajador.EmpresaEn(Cese.AddDays(1)).ShouldBe(2);
    }

    [Fact]
    public void PL_019_estado_actual_no_cambia_respuesta_historica()
    {
        var trabajador = Cerrado();
        trabajador.Activo = false;

        trabajador.TieneVinculoEn(Ingreso).ShouldBeTrue();
    }

    [Fact]
    public void PL_020_fecha_en_hueco_devuelve_no_vigente()
    {
        var trabajador = Cerrado();
        trabajador.Reingresar(Cese.AddDays(10), 1, 2, "tester");

        trabajador.TieneVinculoEn(Cese.AddDays(5)).ShouldBeFalse();
    }

    [Fact]
    public void AP_001_asignacion_de_planta_pertenece_a_un_vinculo_laboral()
    {
        var periodo = ConVinculoInicial().PeriodosLaborales.Single();

        var asignacion = periodo.AsignacionesPlanta.ShouldHaveSingleItem();
        asignacion.FechaInicio.ShouldBe(periodo.FechaIngreso);
    }

    [Fact]
    public void AP_002_cambio_de_planta_conserva_asignacion_anterior()
    {
        var trabajador = ConVinculoInicial();
        var cambio = Ingreso.AddMonths(1);

        trabajador.CambiarPlanta(3, cambio, "tester");

        var asignaciones = trabajador.PeriodosLaborales.Single().AsignacionesPlanta;
        asignaciones.Count.ShouldBe(2);
        asignaciones.Single(a => a.PlantaId == 2).FechaFin.ShouldBe(cambio.AddDays(-1));
    }

    [Fact]
    public void AP_003_consulta_historica_devuelve_planta_correcta()
    {
        var trabajador = ConVinculoInicial();
        var cambio = Ingreso.AddMonths(1);
        trabajador.CambiarPlanta(3, cambio, "tester");

        trabajador.PlantaEn(Ingreso).ShouldBe(2);
        trabajador.PlantaEn(cambio).ShouldBe(3);
    }

    [Fact]
    public void AP_004_puede_cambiar_planta_sin_cambiar_trabajador()
    {
        var trabajador = ConVinculoInicial();
        var id = trabajador.Id;

        trabajador.CambiarPlanta(3, Ingreso.AddDays(10), "tester");

        trabajador.Id.ShouldBe(id);
        trabajador.PeriodosLaborales.Count.ShouldBe(1);
    }

    [Fact]
    public void AP_005_puede_cambiar_planta_dentro_de_la_misma_empresa()
    {
        var trabajador = ConVinculoInicial();

        trabajador.CambiarPlanta(3, Ingreso.AddDays(10), "tester");

        trabajador.EmpresaEn(Ingreso.AddDays(20)).ShouldBe(1);
        trabajador.PlantaEn(Ingreso.AddDays(20)).ShouldBe(3);
    }

    [Fact]
    public void AP_006_cambio_de_empresa_inicia_nueva_relacion_laboral()
    {
        var trabajador = Cerrado();

        trabajador.Reingresar(Cese.AddDays(1), 2, 3, "tester");

        trabajador.PeriodosLaborales.Count.ShouldBe(2);
        trabajador.EmpresaEn(Cese.AddDays(1)).ShouldBe(2);
    }

    [Fact]
    public void AP_007_rechaza_asignacion_fuera_del_periodo_que_la_contiene()
    {
        var periodo = Cerrado().PeriodosLaborales.Single();

        Should.Throw<ReglaNegocioException>(() =>
            periodo.AsignarPlanta(3, Cese.AddDays(1), Cese.AddDays(2), "tester"));
    }

    [Fact]
    public void AP_008_cambiar_proyeccion_actual_no_altera_planta_historica()
    {
        var trabajador = Cerrado();
        trabajador.PlantaId = 99;

        trabajador.PlantaEn(Ingreso).ShouldBe(2);
    }

    [Fact]
    public void AP_009_rechaza_dos_asignaciones_ambiguas_en_la_misma_fecha()
    {
        var periodo = ConVinculoInicial().PeriodosLaborales.Single();

        Should.Throw<ReglaNegocioException>(() =>
            periodo.AsignarPlanta(3, Ingreso.AddDays(10), null, "tester"));
    }

    [Fact]
    public void AP_010_reingreso_crea_nueva_asignacion_de_planta()
    {
        var trabajador = Cerrado();

        var reingreso = trabajador.Reingresar(Cese.AddDays(1), 2, 3, "tester");

        reingreso.AsignacionesPlanta.ShouldHaveSingleItem().PlantaId.ShouldBe(3);
    }

    private static Trabajador ConVinculoInicial()
    {
        var trabajador = NuevoTrabajador();
        trabajador.AbrirVinculo("tester");
        return trabajador;
    }

    private static Trabajador Cerrado()
    {
        var trabajador = ConVinculoInicial();
        trabajador.DarDeBaja(Cese, MotivoCese.TerminoContrato, null, "tester");
        return trabajador;
    }

    private static Trabajador NuevoTrabajador() => new()
    {
        Id = 10,
        TipoDocumentoId = 1,
        NumeroDocumento = "12345678",
        ApellidoPaterno = "PRUEBA",
        ApellidoMaterno = "CONTRATO",
        Nombres = "TEMPORAL",
        EmpresaId = 1,
        PlantaId = 2,
        PuestoId = 1,
        FechaIngreso = Ingreso
    };

    private static PeriodoLaboral Periodo(DateOnly inicio, DateOnly? fin) => new()
    {
        EmpresaId = 1,
        FechaIngreso = inicio,
        FechaCese = fin
    };
}
