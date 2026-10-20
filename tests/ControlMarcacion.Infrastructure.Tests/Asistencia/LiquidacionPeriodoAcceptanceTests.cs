using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Normativa;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Asistencia;

/// <summary>
/// Casos LIQ-01..04 de Fase 5. Verifican que una dimensión superpuesta (nocturnidad o
/// descanso trabajado) no vuelva a sumar la base que ya está en minutos ordinarios.
/// </summary>
public sealed class LiquidacionPeriodoAcceptanceTests : IDisposable
{
    private const long PeriodoId = 1;
    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioCalculoTareo servicio;

    public LiquidacionPeriodoAcceptanceTests()
    {
        conexion = new SqliteConnection("Data Source=:memory:");
        conexion.Open();
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexion)
            .Options;
        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();
        servicio = new ServicioCalculoTareo(
            contexto,
            new AlcanceSinRestriccion(),
            CoberturaDePrueba.TodoCargado(),
            NullLogger<ServicioCalculoTareo>.Instance);
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task LIQ_01_solo_base_ordinaria()
    {
        await AgregarTareoAsync(480, 0, 0, 0, TipoDiaNoLaborable.Ninguno);

        var liquidacion = await servicio.LiquidarPeriodoAsync(PeriodoId);

        liquidacion.ShouldNotBeNull();
        liquidacion.MinutosOrdinarios.ShouldBe(480);
        liquidacion.TotalHorasEquivalentes.ShouldBe(8m);
    }

    [Fact]
    public async Task LIQ_02_ordinarias_y_extras_aplican_una_sola_base()
    {
        await AgregarTareoAsync(480, 120, 60, 0, TipoDiaNoLaborable.Ninguno);

        var liquidacion = await servicio.LiquidarPeriodoAsync(PeriodoId);

        liquidacion.ShouldNotBeNull();
        liquidacion.TotalHorasEquivalentes.ShouldBe(11.85m);
    }

    [Fact]
    public async Task LIQ_03_nocturnidad_agrega_solo_la_sobretasa_no_otra_base()
    {
        await AgregarTareoAsync(480, 0, 0, 480, TipoDiaNoLaborable.Ninguno);

        var liquidacion = await servicio.LiquidarPeriodoAsync(PeriodoId);

        liquidacion.ShouldNotBeNull();
        liquidacion.TotalHorasEquivalentes.ShouldBe(10.8m);
    }

    [Fact]
    public async Task LIQ_04_descanso_trabajado_no_se_suma_ademas_como_ordinario()
    {
        await AgregarTareoAsync(480, 0, 0, 0, TipoDiaNoLaborable.DescansoSemanal);

        var liquidacion = await servicio.LiquidarPeriodoAsync(PeriodoId);

        liquidacion.ShouldNotBeNull();
        liquidacion.TotalHorasEquivalentes.ShouldBe(16m);
    }

    [Fact]
    public async Task LIQ_HE_NOC_01_cruce_sabado_domingo_conserva_base_unica_y_prima_nocturna()
    {
        // 19:00–05:22: 120 HE25 + 180 HE35 + 322 HE100. La nocturnidad (442)
        // atraviesa esas categorías, pero no representa otros 442 minutos físicos.
        await AgregarTareoAsync(
            ordinarios: 0,
            extra25: 120,
            extra35: 180,
            nocturnos: 442,
            tipoDia: TipoDiaNoLaborable.Ninguno,
            extra100: 322);

        var liquidacion = await servicio.LiquidarPeriodoAsync(PeriodoId);

        liquidacion.ShouldNotBeNull();
        liquidacion.Conceptos.Single(c => c.Concepto == ConceptoSobretasa.DescansoSemanalTrabajado)
            .Minutos.ShouldBe(322);
        liquidacion.Conceptos.Single(c => c.Concepto == ConceptoSobretasa.TrabajoNocturno)
            .Minutos.ShouldBe(442);
        liquidacion.TotalHorasEquivalentes.ShouldBe(19.86m);
    }

    [Fact]
    public async Task FER_002_dia_completo_expone_ordinario_base_y_recargo_sin_triplicar_fisico()
    {
        await AgregarTareoAsync(
            0, 0, 0, 0, TipoDiaNoLaborable.Feriado,
            extra100: 480,
            fecha: new DateOnly(2026, 7, 28));

        var liquidacion = await servicio.LiquidarPeriodoAsync(PeriodoId);

        liquidacion.ShouldNotBeNull();
        var feriado = liquidacion.Conceptos.Single(c =>
            c.Concepto == ConceptoSobretasa.FeriadoTrabajado);
        feriado.Minutos.ShouldBe(480);
        feriado.MinutosBaseEconomica.ShouldBe(480m);
        feriado.MinutosRecargo.ShouldBe(480m);
        feriado.MinutosEquivalentes.ShouldBe(960m);
        liquidacion.ComponentesDiariosConDerecho.Single(c =>
                c.Tipo == TipoComponenteRemuneracionDiaria.RemuneracionOrdinariaFeriado)
            .CantidadDias.ShouldBe(1);
        liquidacion.TotalHorasEquivalentes.ShouldBe(16m);
    }

    [Fact]
    public async Task FER_002_trabajo_parcial_prorratea_base_y_recargo_por_minutos_reales()
    {
        await AgregarTareoAsync(
            0, 0, 0, 0, TipoDiaNoLaborable.Feriado,
            extra100: 120,
            fecha: new DateOnly(2026, 7, 28));

        var liquidacion = await servicio.LiquidarPeriodoAsync(PeriodoId);

        liquidacion.ShouldNotBeNull();
        var feriado = liquidacion.Conceptos.Single(c =>
            c.Concepto == ConceptoSobretasa.FeriadoTrabajado);
        feriado.Minutos.ShouldBe(120);
        feriado.MinutosBaseEconomica.ShouldBe(120m);
        feriado.MinutosRecargo.ShouldBe(120m);
        liquidacion.TotalHorasEquivalentes.ShouldBe(4m);
    }

    [Fact]
    public async Task FER_001_feriado_no_trabajado_conserva_derecho_sin_inventar_minutos()
    {
        await AgregarTareoAsync(
            0, 0, 0, 0, TipoDiaNoLaborable.Feriado,
            fecha: new DateOnly(2026, 7, 28),
            estado: EstadoTareo.Feriado);

        var liquidacion = await servicio.LiquidarPeriodoAsync(PeriodoId);

        liquidacion.ShouldNotBeNull();
        liquidacion.ComponentesDiariosConDerecho.Single().Tipo
            .ShouldBe(TipoComponenteRemuneracionDiaria.RemuneracionOrdinariaFeriado);
        liquidacion.Conceptos.Single(c => c.Concepto == ConceptoSobretasa.FeriadoTrabajado)
            .Minutos.ShouldBe(0);
        liquidacion.TotalHorasEquivalentes.ShouldBe(0m);
    }

    [Fact]
    public async Task FER_007_1_de_mayo_trabajado_expone_cuatro_componentes_y_una_base_fisica()
    {
        await AgregarTareoAsync(
            0, 0, 0, 0, TipoDiaNoLaborable.Feriado,
            extra100: 480,
            fecha: new DateOnly(2026, 5, 1),
            coincidencia: TipoCoincidenciaFeriadoDescanso.PrimeroDeMayo);

        var liquidacion = await servicio.LiquidarPeriodoAsync(PeriodoId);

        liquidacion.ShouldNotBeNull();
        liquidacion.ComponentesDiariosConDerecho.Count.ShouldBe(2);
        liquidacion.ComponentesDiariosConDerecho.Single(c =>
                c.Tipo == TipoComponenteRemuneracionDiaria.RemuneracionDescansoSemanal)
            .CantidadDias.ShouldBe(1);
        liquidacion.ComponentesDiariosConDerecho.Single(c =>
                c.Tipo == TipoComponenteRemuneracionDiaria.RemuneracionAdicionalPrimeroDeMayo)
            .CantidadDias.ShouldBe(1);

        var feriado = liquidacion.Conceptos.Single(c =>
            c.Concepto == ConceptoSobretasa.FeriadoTrabajado);
        feriado.Minutos.ShouldBe(480);
        feriado.MinutosBaseEconomica.ShouldBe(480m);
        feriado.MinutosRecargo.ShouldBe(480m);
        liquidacion.Conceptos.Single(c =>
                c.Concepto == ConceptoSobretasa.DescansoSemanalTrabajado)
            .Minutos.ShouldBe(0);
    }

    [Fact]
    public async Task FER_007_1_de_mayo_no_trabajado_expone_dos_componentes_y_cero_fisico()
    {
        await AgregarTareoAsync(
            0, 0, 0, 0, TipoDiaNoLaborable.Feriado,
            fecha: new DateOnly(2026, 5, 1),
            estado: EstadoTareo.Feriado,
            coincidencia: TipoCoincidenciaFeriadoDescanso.PrimeroDeMayo);

        var liquidacion = await servicio.LiquidarPeriodoAsync(PeriodoId);

        liquidacion.ShouldNotBeNull();
        liquidacion.ComponentesDiariosConDerecho.Count.ShouldBe(2);
        liquidacion.ComponentesDiariosConDerecho.Sum(c => c.CantidadDias).ShouldBe(2);
        liquidacion.Conceptos.Single(c => c.Concepto == ConceptoSobretasa.FeriadoTrabajado)
            .Minutos.ShouldBe(0);
        liquidacion.TotalHorasEquivalentes.ShouldBe(0m);
    }

    private async Task AgregarTareoAsync(
        int ordinarios,
        int extra25,
        int extra35,
        int nocturnos,
        TipoDiaNoLaborable tipoDia,
        int extra100 = 0,
        DateOnly? fecha = null,
        EstadoTareo estado = EstadoTareo.Puntual,
        TipoCoincidenciaFeriadoDescanso coincidencia =
            TipoCoincidenciaFeriadoDescanso.Ninguna)
    {
        contexto.TareosDiarios.Add(new TareoDiario
        {
            PeriodoId = PeriodoId,
            TrabajadorId = 1,
            Fecha = fecha ?? new DateOnly(2026, 7, 6),
            MinutosTrabajados = ordinarios + extra25 + extra35 + extra100,
            MinutosOrdinarios = ordinarios,
            MinutosExtra25 = extra25,
            MinutosExtra35 = extra35,
            MinutosNocturnos = nocturnos,
            TipoDiaNoLaborable = tipoDia,
            CoincidenciaFeriadoDescanso = coincidencia,
            Estado = estado,
            CalculadoEn = new DateTime(2026, 8, 1, 12, 0, 0)
        });
        await contexto.SaveChangesAsync();
    }
}
