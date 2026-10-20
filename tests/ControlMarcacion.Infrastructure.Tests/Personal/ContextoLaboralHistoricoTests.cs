using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Personal;

/// <summary>Contratos PL3C-ORG-01..07 de empresa/planta histórica y diagnóstico.</summary>
public sealed class ContextoLaboralHistoricoTests : IDisposable
{
    private readonly MarcacionDbContext contexto;
    private readonly ConsultaContextoLaboral consulta;

    public ContextoLaboralHistoricoTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();
        consulta = new ConsultaContextoLaboral(contexto);
    }

    public void Dispose() => contexto.Dispose();

    [Fact]
    public async Task PL3C_ORG_01_resuelve_empresa_del_periodo_historico()
    {
        var id = await CrearTrabajadorAsync(
            Periodo(1, new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), 10),
            Periodo(2, new DateOnly(2025, 1, 1), null, 20));

        var historia = await CargarAsync(id);

        historia.Resolver(new DateOnly(2024, 6, 1)).Contexto!.EmpresaId.ShouldBe(1);
        historia.Resolver(new DateOnly(2025, 6, 1)).Contexto!.EmpresaId.ShouldBe(2);
    }

    [Fact]
    public async Task PL3C_ORG_02_resuelve_planta_del_periodo_historico()
    {
        var id = await CrearTrabajadorAsync(
            Periodo(1, new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), 10),
            Periodo(1, new DateOnly(2025, 1, 1), null, 20));

        var historia = await CargarAsync(id);

        historia.Resolver(new DateOnly(2024, 6, 1)).Contexto!.PlantaId.ShouldBe(10);
        historia.Resolver(new DateOnly(2025, 6, 1)).Contexto!.PlantaId.ShouldBe(20);
    }

    [Fact]
    public async Task PL3C_ORG_03_resuelve_cambio_de_planta_dentro_del_vinculo()
    {
        var periodo = Periodo(1, new DateOnly(2025, 1, 1), null, 10);
        periodo.AsignacionesPlanta.Single().FechaFin = new DateOnly(2025, 6, 30);
        periodo.AsignacionesPlanta.Add(new AsignacionPlanta
        {
            PlantaId = 20,
            FechaInicio = new DateOnly(2025, 7, 1)
        });
        var id = await CrearTrabajadorAsync(periodo);

        var historia = await CargarAsync(id);

        historia.Resolver(new DateOnly(2025, 6, 30)).Contexto!.PlantaId.ShouldBe(10);
        historia.Resolver(new DateOnly(2025, 7, 1)).Contexto!.PlantaId.ShouldBe(20);
    }

    [Fact]
    public async Task PL3C_ORG_04_sin_periodo_no_usa_la_empresa_o_planta_actual()
    {
        var trabajador = TrabajadorBase();
        trabajador.EmpresaId = 99;
        trabajador.PlantaId = 98;
        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync();

        var historia = await CargarAsync(trabajador.Id);
        var resultado = historia.Resolver(new DateOnly(2025, 5, 1));

        resultado.Estado.ShouldBe(EstadoResolucionContextoLaboral.SinPeriodo);
        resultado.Contexto.ShouldBeNull();
    }

    [Fact]
    public async Task PL3C_ORG_05_periodos_solapados_devuelven_error_controlado()
    {
        var id = await CrearTrabajadorAsync(
            Periodo(1, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), 10),
            Periodo(2, new DateOnly(2025, 6, 1), null, 20));

        var resultado = (await CargarAsync(id)).Resolver(new DateOnly(2025, 7, 1));

        resultado.Estado.ShouldBe(EstadoResolucionContextoLaboral.ErrorConfiguracion);
        Should.Throw<Domain.Comun.ReglaNegocioException>(() => resultado.ObtenerOError());
    }

    [Fact]
    public async Task PL3C_ORG_06_dos_plantas_vigentes_devuelven_error_controlado()
    {
        var periodo = Periodo(1, new DateOnly(2025, 1, 1), null, 10);
        periodo.AsignacionesPlanta.Add(new AsignacionPlanta
        {
            PlantaId = 20,
            FechaInicio = new DateOnly(2025, 2, 1)
        });
        var id = await CrearTrabajadorAsync(periodo);

        var resultado = (await CargarAsync(id)).Resolver(new DateOnly(2025, 3, 1));

        resultado.Estado.ShouldBe(EstadoResolucionContextoLaboral.ErrorConfiguracion);
    }

    [Fact]
    public void PL3C_ORG_07_fallback_legacy_es_explicito_y_diagnosticable()
    {
        var resultado = ResultadoResolucionContextoLaboral.DesdeLegacy(
            new ContextoLaboralResuelto(0, 1, 2, new DateOnly(2025, 1, 1), null));

        resultado.Estado.ShouldBe(EstadoResolucionContextoLaboral.LegacyFallback);
        resultado.Diagnostico.ShouldBe("PENDIENTE_3C");
    }

    private async Task<HistoriaLaboralTrabajador> CargarAsync(long trabajadorId)
    {
        var historias = await consulta.CargarRangoAsync(
            new DateOnly(2020, 1, 1), new DateOnly(2030, 12, 31), [trabajadorId]);
        return historias[trabajadorId];
    }

    private async Task<long> CrearTrabajadorAsync(params PeriodoLaboral[] periodos)
    {
        var trabajador = TrabajadorBase();
        foreach (var periodo in periodos)
        {
            trabajador.PeriodosLaborales.Add(periodo);
        }

        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();
        return trabajador.Id;
    }

    private static Trabajador TrabajadorBase() => new()
    {
        TipoDocumentoId = 1,
        NumeroDocumento = Guid.NewGuid().ToString("N"),
        ApellidoPaterno = "HISTORIA",
        ApellidoMaterno = "LABORAL",
        Nombres = "PRUEBA",
        EmpresaId = 2,
        PlantaId = 20,
        PuestoId = 1,
        FechaIngreso = new DateOnly(2025, 1, 1)
    };

    private static PeriodoLaboral Periodo(
        long empresaId,
        DateOnly inicio,
        DateOnly? fin,
        long plantaId)
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

