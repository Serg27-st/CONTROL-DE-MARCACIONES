using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Domain.Reportes;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Reportes;

/// <summary>Contratos PL3C-REP-01..06 de reportes históricos.</summary>
public sealed class PeriodoLaboralReportesFase3CTests : IDisposable
{
    private const long EmpresaHistorica = 1;
    private const long EmpresaActual = 2;
    private const long PlantaHistorica = 3;
    private const long PlantaActual = 4;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioReportes servicio;

    public PeriodoLaboralReportesFase3CTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();
        servicio = new ServicioReportes(contexto, new AlcanceSinRestriccion());
    }

    public void Dispose() => contexto.Dispose();

    [Fact]
    public async Task PL3C_REP_01_muestra_la_empresa_historica_y_no_la_actual()
    {
        var trabajador = TrabajadorActual();
        trabajador.PeriodosLaborales.Add(Periodo(
            EmpresaHistorica, PlantaHistorica, new DateOnly(2026, 1, 1), null));
        await GuardarAsync(trabajador);

        var fila = (await servicio.ResumenTareoAsync(RangoJulio())).ShouldHaveSingleItem();
        var nombreHistorico = await contexto.Empresas
            .Where(e => e.Id == EmpresaHistorica).Select(e => e.RazonSocial).SingleAsync();
        var nombreActual = await contexto.Empresas
            .Where(e => e.Id == EmpresaActual).Select(e => e.RazonSocial).SingleAsync();

        fila.Empresa.ShouldBe(nombreHistorico);
        fila.Empresa.ShouldNotBe(nombreActual);
    }

    [Fact]
    public async Task PL3C_REP_02_muestra_la_planta_historica_y_no_la_actual()
    {
        var trabajador = TrabajadorActual();
        trabajador.PeriodosLaborales.Add(Periodo(
            EmpresaHistorica, PlantaHistorica, new DateOnly(2026, 1, 1), null));
        await GuardarAsync(trabajador);

        var fila = (await servicio.ResumenTareoAsync(RangoJulio())).ShouldHaveSingleItem();
        var nombreHistorico = await contexto.Plantas
            .Where(p => p.Id == PlantaHistorica).Select(p => p.Nombre).SingleAsync();
        var nombreActual = await contexto.Plantas
            .Where(p => p.Id == PlantaActual).Select(p => p.Nombre).SingleAsync();

        fila.Planta.ShouldBe(nombreHistorico);
        fila.Planta.ShouldNotBe(nombreActual);
    }

    [Fact]
    public async Task PL3C_REP_03_un_cesado_aparece_en_su_rango_historico()
    {
        var trabajador = TrabajadorActual();
        trabajador.FechaCese = new DateOnly(2026, 7, 15);
        trabajador.PeriodosLaborales.Add(Periodo(
            EmpresaHistorica, PlantaHistorica,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 15)));
        await GuardarAsync(trabajador);

        (await servicio.ResumenTareoAsync(new FiltroReporte
        {
            Alcance = AlcanceReporte.Dia,
            Desde = new DateOnly(2026, 7, 10)
        })).ShouldHaveSingleItem();

        (await servicio.ResumenTareoAsync(new FiltroReporte
        {
            Alcance = AlcanceReporte.Dia,
            Desde = new DateOnly(2026, 7, 16)
        })).ShouldBeEmpty();
    }

    [Fact]
    public async Task PL3C_REP_04_no_muestra_al_trabajador_antes_del_ingreso()
    {
        var trabajador = TrabajadorActual();
        trabajador.PeriodosLaborales.Add(Periodo(
            EmpresaHistorica, PlantaHistorica, new DateOnly(2026, 7, 10), null));
        await GuardarAsync(trabajador);

        var resultado = await servicio.ResumenTareoAsync(new FiltroReporte
        {
            Alcance = AlcanceReporte.Rango,
            Desde = new DateOnly(2026, 7, 1),
            Hasta = new DateOnly(2026, 7, 9)
        });

        resultado.ShouldBeEmpty();
    }

    [Fact]
    public async Task PL3C_REP_05_reingreso_no_fusiona_el_episodio_anterior_con_el_actual()
    {
        var trabajador = TrabajadorActual();
        trabajador.PeriodosLaborales.Add(Periodo(
            EmpresaHistorica, PlantaHistorica,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)));
        trabajador.PeriodosLaborales.Add(Periodo(
            EmpresaActual, PlantaActual, new DateOnly(2026, 5, 1), null));
        await GuardarAsync(trabajador);

        var historico = await servicio.ResumenTareoAsync(new FiltroReporte
        {
            Alcance = AlcanceReporte.Dia,
            Desde = new DateOnly(2026, 2, 1)
        });
        var hueco = await servicio.ResumenTareoAsync(new FiltroReporte
        {
            Alcance = AlcanceReporte.Dia,
            Desde = new DateOnly(2026, 4, 15)
        });

        historico.ShouldHaveSingleItem().Empresa.ShouldBe(
            await contexto.Empresas.Where(e => e.Id == EmpresaHistorica)
                .Select(e => e.RazonSocial).SingleAsync());
        hueco.ShouldBeEmpty();
    }

    [Fact]
    public async Task PL3C_REP_06_cambio_de_planta_en_el_rango_se_representa_y_filtra_por_dia()
    {
        var trabajador = TrabajadorActual();
        var periodo = Periodo(
            EmpresaHistorica, PlantaHistorica, new DateOnly(2026, 1, 1), null);
        periodo.AsignacionesPlanta.Single().FechaFin = new DateOnly(2026, 7, 15);
        periodo.AsignacionesPlanta.Add(new AsignacionPlanta
        {
            PlantaId = PlantaActual,
            FechaInicio = new DateOnly(2026, 7, 16)
        });
        trabajador.PeriodosLaborales.Add(periodo);
        await GuardarAsync(trabajador);
        await AgregarTareoAsync(trabajador.Id, new DateOnly(2026, 7, 10));
        await AgregarTareoAsync(trabajador.Id, new DateOnly(2026, 7, 20));

        var nombreAntes = await contexto.Plantas.Where(p => p.Id == PlantaHistorica)
            .Select(p => p.Nombre).SingleAsync();
        var nombreDespues = await contexto.Plantas.Where(p => p.Id == PlantaActual)
            .Select(p => p.Nombre).SingleAsync();

        var completo = (await servicio.ResumenTareoAsync(RangoJulio())).ShouldHaveSingleItem();
        completo.Planta.ShouldBe($"{nombreAntes} → {nombreDespues}");
        completo.DiasPuntual.ShouldBe(2);

        var filtrado = (await servicio.ResumenTareoAsync(RangoJulio() with
        {
            PlantaId = PlantaHistorica
        })).ShouldHaveSingleItem();
        filtrado.Planta.ShouldBe(nombreAntes);
        filtrado.DiasPuntual.ShouldBe(1);
    }

    private async Task GuardarAsync(Trabajador trabajador)
    {
        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();
    }

    private async Task AgregarTareoAsync(long trabajadorId, DateOnly fecha)
    {
        contexto.TareosDiarios.Add(new TareoDiario
        {
            PeriodoId = 1,
            TrabajadorId = trabajadorId,
            Fecha = fecha,
            Estado = EstadoTareo.Puntual,
            EntradaReal = fecha.ToDateTime(new TimeOnly(7, 0)),
            SalidaReal = fecha.ToDateTime(new TimeOnly(16, 0)),
            MinutosTrabajados = 540,
            MinutosOrdinarios = 480,
            CalculadoEn = new DateTime(2026, 8, 1, 8, 0, 0)
        });
        await contexto.SaveChangesAsync();
    }

    private static FiltroReporte RangoJulio() => new()
    {
        Alcance = AlcanceReporte.Rango,
        Desde = new DateOnly(2026, 7, 1),
        Hasta = new DateOnly(2026, 7, 31)
    };

    private static Trabajador TrabajadorActual() => new()
    {
        TipoDocumentoId = 1,
        NumeroDocumento = Guid.NewGuid().ToString("N"),
        ApellidoPaterno = "HISTORICO",
        ApellidoMaterno = "REPORTE",
        Nombres = "PRUEBA",
        EmpresaId = EmpresaActual,
        PlantaId = PlantaActual,
        AreaId = 3,
        SeccionId = 3,
        PuestoId = 1,
        FechaIngreso = new DateOnly(2025, 1, 1)
    };

    private static PeriodoLaboral Periodo(
        long empresaId,
        long plantaId,
        DateOnly inicio,
        DateOnly? fin)
    {
        var periodo = new PeriodoLaboral
        {
            EmpresaId = empresaId,
            FechaIngreso = inicio,
            FechaCese = fin
        };
        periodo.AsignacionesPlanta.Add(new AsignacionPlanta
        {
            PlantaId = plantaId,
            FechaInicio = inicio,
            FechaFin = fin
        });
        return periodo;
    }
}

