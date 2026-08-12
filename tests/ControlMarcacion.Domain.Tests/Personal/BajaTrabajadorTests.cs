using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Personal;

/// <summary>
/// Reglas de la baja del trabajador y del cambio de razón social.
/// </summary>
/// <remarks>
/// Son reglas de dominio puro: no tocan base de datos, así que se prueban con fechas
/// literales y sin dobles. Fechas literales y nunca <c>DateTime.Now</c>: un test que
/// depende del día en que corre falla solo, un martes cualquiera, sin que nadie haya
/// tocado nada.
/// </remarks>
public class BajaTrabajadorTests
{
    private const long ConsultingAdvisers = 1;
    private const long CaOutsourcing = 2;

    private static readonly DateOnly Ingreso = new(2026, 1, 15);
    private static readonly DateOnly Cese = new(2026, 7, 31);

    private static Trabajador Activo()
    {
        var trabajador = new Trabajador
        {
            Id = 10,
            NumeroDocumento = "09876543",
            ApellidoPaterno = "PEREZ",
            ApellidoMaterno = "QUISPE",
            Nombres = "JUAN CARLOS",
            EmpresaId = ConsultingAdvisers,
            PlantaId = 1,
            PuestoId = 1,
            TipoDocumentoId = 1,
            FechaIngreso = Ingreso
        };

        trabajador.AbrirVinculo("sistema");

        return trabajador;
    }

    // ---------- Baja ----------

    [Fact]
    public void DarDeBaja_registra_fecha_y_motivo()
    {
        var trabajador = Activo();

        trabajador.DarDeBaja(Cese, MotivoCese.RenunciaVoluntaria, "Se muda de ciudad", "coordinador");

        trabajador.FechaCese.ShouldBe(Cese);
        trabajador.MotivoCese.ShouldBe(MotivoCese.RenunciaVoluntaria);
        trabajador.ObservacionCese.ShouldBe("Se muda de ciudad");
        trabajador.EstaVigente.ShouldBeFalse();
    }

    [Fact]
    public void DarDeBaja_cierra_el_vinculo_laboral_abierto()
    {
        var trabajador = Activo();

        trabajador.DarDeBaja(Cese, MotivoCese.TerminoContrato, null, "coordinador");

        var vinculo = trabajador.PeriodosLaborales.ShouldHaveSingleItem();
        vinculo.FechaCese.ShouldBe(Cese);
        vinculo.MotivoCese.ShouldBe(MotivoCese.TerminoContrato);
        vinculo.EstaAbierto.ShouldBeFalse();
    }

    [Fact]
    public void DarDeBaja_libera_el_id_de_marcador_cerrando_su_asignacion()
    {
        var trabajador = Activo();
        trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            IdMarcador = "00123",
            FechaInicio = Ingreso
        });

        trabajador.DarDeBaja(Cese, MotivoCese.AbandonoLaboral, null, "coordinador");

        // Si quedara abierta, el ID seguiría figurando como ocupado por alguien que ya no
        // viene y nadie podría asignárselo a su reemplazo.
        trabajador.AsignacionesMarcador.ShouldHaveSingleItem().FechaFin.ShouldBe(Cese);
    }

    [Fact]
    public void DarDeBaja_cierra_tambien_turno_y_sueldo()
    {
        var trabajador = Activo();
        trabajador.AsignacionesTurno.Add(new AsignacionTurno { TurnoId = 1, FechaInicio = Ingreso });
        trabajador.AsignacionesSueldo.Add(new AsignacionSueldo
        {
            SueldoBasicoMensual = 1200m,
            FechaInicio = Ingreso
        });

        trabajador.DarDeBaja(Cese, MotivoCese.Despido, null, "coordinador");

        trabajador.AsignacionesTurno.ShouldHaveSingleItem().FechaFin.ShouldBe(Cese);
        trabajador.AsignacionesSueldo.ShouldHaveSingleItem().FechaFin.ShouldBe(Cese);
    }

    [Fact]
    public void DarDeBaja_no_toca_las_asignaciones_ya_cerradas()
    {
        var trabajador = Activo();
        var cerradaEn = new DateOnly(2026, 3, 31);

        trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            IdMarcador = "00100",
            FechaInicio = Ingreso,
            FechaFin = cerradaEn
        });
        trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            IdMarcador = "00123",
            FechaInicio = cerradaEn.AddDays(1)
        });

        trabajador.DarDeBaja(Cese, MotivoCese.RenunciaVoluntaria, null, "coordinador");

        trabajador.AsignacionesMarcador.First(a => a.IdMarcador == "00100").FechaFin.ShouldBe(cerradaEn);
        trabajador.AsignacionesMarcador.First(a => a.IdMarcador == "00123").FechaFin.ShouldBe(Cese);
    }

    [Fact]
    public void DarDeBaja_rechaza_una_fecha_anterior_al_ingreso()
    {
        var trabajador = Activo();

        var error = Should.Throw<ReglaNegocioException>(() =>
            trabajador.DarDeBaja(Ingreso.AddDays(-1), MotivoCese.Destitucion, null, "coordinador"));

        error.Message.ShouldContain("anterior");
        trabajador.EstaVigente.ShouldBeTrue();
    }

    [Fact]
    public void DarDeBaja_rechaza_un_segundo_cese()
    {
        var trabajador = Activo();
        trabajador.DarDeBaja(Cese, MotivoCese.RenunciaVoluntaria, null, "coordinador");

        var error = Should.Throw<ReglaNegocioException>(() =>
            trabajador.DarDeBaja(Cese.AddDays(10), MotivoCese.Despido, null, "coordinador"));

        error.Message.ShouldContain("ya tiene un cese registrado");

        // El cese original queda intacto: el segundo intento no debe reescribir la historia.
        trabajador.FechaCese.ShouldBe(Cese);
        trabajador.MotivoCese.ShouldBe(MotivoCese.RenunciaVoluntaria);
    }

    // ---------- Cambio de empresa ----------

    [Fact]
    public void CambiarEmpresa_se_rechaza_mientras_el_trabajador_este_activo()
    {
        var trabajador = Activo();

        var error = Should.Throw<ReglaNegocioException>(() =>
            trabajador.CambiarEmpresa(CaOutsourcing, "coordinador"));

        error.Message.ShouldContain("Registre primero");
        trabajador.EmpresaId.ShouldBe(ConsultingAdvisers);
    }

    [Fact]
    public void CambiarEmpresa_se_permite_despues_de_registrar_la_baja()
    {
        var trabajador = Activo();
        trabajador.DarDeBaja(Cese, MotivoCese.NoSuperoPeriodoPrueba, null, "coordinador");

        trabajador.CambiarEmpresa(CaOutsourcing, "coordinador");

        trabajador.EmpresaId.ShouldBe(CaOutsourcing);
    }

    [Fact]
    public void CambiarEmpresa_a_la_misma_empresa_no_hace_nada_ni_falla()
    {
        var trabajador = Activo();

        // Es el caso normal de guardar la ficha sin tocar la empresa: no debe estallar
        // solo porque el trabajador siga activo.
        Should.NotThrow(() => trabajador.CambiarEmpresa(ConsultingAdvisers, "coordinador"));

        trabajador.EmpresaId.ShouldBe(ConsultingAdvisers);
    }

    [Fact]
    public void El_vinculo_cerrado_conserva_la_empresa_del_periodo_trabajado()
    {
        var trabajador = Activo();
        trabajador.DarDeBaja(Cese, MotivoCese.TerminoContrato, null, "coordinador");

        trabajador.CambiarEmpresa(CaOutsourcing, "coordinador");

        // El tareo de enero a julio se pagó desde la planilla de Consulting Advisers:
        // moverlo a la otra razón social falsearía a quién se le pagó.
        trabajador.PeriodosLaborales.ShouldHaveSingleItem().EmpresaId.ShouldBe(ConsultingAdvisers);
    }

    // ---------- Vínculo ----------

    [Fact]
    public void TieneVinculoEn_cubre_desde_el_ingreso_hasta_el_cese_inclusive()
    {
        var trabajador = Activo();
        trabajador.DarDeBaja(Cese, MotivoCese.RenunciaVoluntaria, null, "coordinador");

        trabajador.TieneVinculoEn(Ingreso.AddDays(-1)).ShouldBeFalse();
        trabajador.TieneVinculoEn(Ingreso).ShouldBeTrue();
        trabajador.TieneVinculoEn(Cese).ShouldBeTrue();
        trabajador.TieneVinculoEn(Cese.AddDays(1)).ShouldBeFalse();
    }
}
