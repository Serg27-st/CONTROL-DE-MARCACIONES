using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Reportes;

/// <summary>
/// Captura manual de reintegros: una sola fila por trabajador y periodo, y solo mientras
/// el periodo siga abierto — misma regla de periodo cerrado que el resto del sistema.
/// </summary>
public sealed class ServicioReintegrosTareoTests : IDisposable
{
    private const long PeriodoJulio2026Abierto = 1;

    private readonly MarcacionDbContext contexto;
    private readonly ServicioReintegrosTareo servicio;

    public ServicioReintegrosTareoTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();

        servicio = new ServicioReintegrosTareo(contexto, new AlcanceSinRestriccion());
    }

    public void Dispose() => contexto.Dispose();

    [Fact]
    public async Task Obtener_devuelve_null_si_no_se_cargo_nada_todavia()
    {
        var trabajadorId = await CrearTrabajadorAsync("10101010");

        var resultado = await servicio.ObtenerAsync(trabajadorId, PeriodoJulio2026Abierto, CancellationToken.None);

        resultado.ShouldBeNull();
    }

    [Fact]
    public async Task Guardar_dos_veces_actualiza_la_misma_fila_no_inserta_una_segunda()
    {
        var trabajadorId = await CrearTrabajadorAsync("20202020");

        await servicio.GuardarAsync(
            new GuardarReintegroTareoRequest { TrabajadorId = trabajadorId, PeriodoId = PeriodoJulio2026Abierto, Viatico = 40m },
            "coordinador@gca.local",
            CancellationToken.None);

        await servicio.GuardarAsync(
            new GuardarReintegroTareoRequest { TrabajadorId = trabajadorId, PeriodoId = PeriodoJulio2026Abierto, Viatico = 90m },
            "coordinador@gca.local",
            CancellationToken.None);

        var filas = await contexto.ReintegrosTareo
            .Where(r => r.TrabajadorId == trabajadorId && r.PeriodoId == PeriodoJulio2026Abierto)
            .ToListAsync();

        filas.Count.ShouldBe(1);
        filas[0].Viatico.ShouldBe(90m);

        var dto = await servicio.ObtenerAsync(trabajadorId, PeriodoJulio2026Abierto, CancellationToken.None);
        dto.ShouldNotBeNull();
        dto.Viatico.ShouldBe(90m);
    }

    [Fact]
    public async Task Guardar_en_periodo_cerrado_falla_y_no_escribe_nada()
    {
        var periodoCerradoId = await CrearPeriodoCerradoAsync();
        var trabajadorId = await CrearTrabajadorAsync("30303030");

        await Should.ThrowAsync<PeriodoCerradoException>(() => servicio.GuardarAsync(
            new GuardarReintegroTareoRequest { TrabajadorId = trabajadorId, PeriodoId = periodoCerradoId, Viatico = 40m },
            "coordinador@gca.local",
            CancellationToken.None));

        (await contexto.ReintegrosTareo.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Guardar_con_trabajador_inexistente_falla_con_recurso_no_encontrado()
    {
        const long trabajadorInexistente = 999_999;

        await Should.ThrowAsync<RecursoNoEncontradoException>(() => servicio.GuardarAsync(
            new GuardarReintegroTareoRequest { TrabajadorId = trabajadorInexistente, PeriodoId = PeriodoJulio2026Abierto },
            "coordinador@gca.local",
            CancellationToken.None));
    }

    // ------------------------------------------------------------------

    private async Task<long> CrearTrabajadorAsync(string dni)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = 1,
            NumeroDocumento = dni,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "TEST",
            Nombres = "REINTEGROS",
            Sexo = Sexo.Masculino,
            EmpresaId = 2,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = 3,
            AreaId = 3,
            SeccionId = 3,
            PuestoId = 1,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        await contexto.Trabajadores.AddAsync(trabajador);
        await contexto.SaveChangesAsync();

        return trabajador.Id;
    }

    private async Task<long> CrearPeriodoCerradoAsync()
    {
        var periodo = new Periodo
        {
            Codigo = "2024-01",
            Nombre = "Enero 2024",
            FechaInicio = new DateOnly(2024, 1, 1),
            FechaFin = new DateOnly(2024, 1, 31),
            Estado = EstadoPeriodo.Cerrado,
            CerradoEn = DateTime.Now,
            CerradoPor = "coordinador@gca.local"
        };

        await contexto.Periodos.AddAsync(periodo);
        await contexto.SaveChangesAsync();

        return periodo.Id;
    }
}
