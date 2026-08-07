using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Reportes;

/// <summary>
/// Sincronización manual de feriados: inserta solo lo que falta, nunca borra ni pisa lo
/// ya cargado, y traduce cualquier falla del proveedor externo a un mensaje accionable.
/// </summary>
/// <remarks>
/// El proveedor externo (<see cref="IProveedorFeriadosExternos"/>) se sustituye con
/// NSubstitute: estas pruebas verifican la regla de sincronización, no la llamada HTTP real
/// a Nager.Date, que además exigiría internet en el entorno de build.
/// </remarks>
public sealed class ServicioFeriadosTests : IDisposable
{
    private readonly MarcacionDbContext contexto;
    private readonly IProveedorFeriadosExternos proveedor = Substitute.For<IProveedorFeriadosExternos>();

    public ServicioFeriadosTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => contexto.Dispose();

    [Fact]
    public async Task Sincronizar_inserta_solo_los_feriados_que_faltan()
    {
        // 01/01 ya está cargado (a mano, inactivo — para probar que ni siquiera se toca).
        await contexto.Feriados.AddAsync(new Feriado
        {
            Fecha = new DateOnly(2027, 1, 1), Descripcion = "Año Nuevo (carga manual)", Activo = false
        });
        await contexto.SaveChangesAsync();

        proveedor.ObtenerAsync(2027, Arg.Any<CancellationToken>()).Returns(
        [
            new FeriadoExternoDto(new DateOnly(2027, 1, 1), "Año Nuevo"),
            new FeriadoExternoDto(new DateOnly(2027, 5, 1), "Día del Trabajo"),
            new FeriadoExternoDto(new DateOnly(2027, 12, 25), "Navidad")
        ]);

        var servicio = new ServicioFeriados(contexto, proveedor);

        var resultado = await servicio.SincronizarAsync(2027, "coordinador@gca.local", CancellationToken.None);

        resultado.Agregados.ShouldBe(2);
        resultado.YaExistian.ShouldBe(1);

        var feriados = await contexto.Feriados.Where(f => f.Fecha.Year == 2027).ToListAsync();
        feriados.Count.ShouldBe(3);

        // El que ya existía no se tocó: sigue con su descripción y estado originales.
        var existente = feriados.Single(f => f.Fecha == new DateOnly(2027, 1, 1));
        existente.Descripcion.ShouldBe("Año Nuevo (carga manual)");
        existente.Activo.ShouldBeFalse();
    }

    [Fact]
    public async Task Sincronizar_agrupa_fechas_repetidas_del_proveedor_en_un_solo_feriado()
    {
        proveedor.ObtenerAsync(2027, Arg.Any<CancellationToken>()).Returns(
        [
            new FeriadoExternoDto(new DateOnly(2027, 12, 8), "Inmaculada Concepción"),
            new FeriadoExternoDto(new DateOnly(2027, 12, 8), "Otra conmemoración el mismo día")
        ]);

        var servicio = new ServicioFeriados(contexto, proveedor);

        var resultado = await servicio.SincronizarAsync(2027, "coordinador@gca.local", CancellationToken.None);

        resultado.Agregados.ShouldBe(1);

        var feriados = await contexto.Feriados.Where(f => f.Fecha.Year == 2027).ToListAsync();
        feriados.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Sincronizar_no_inserta_nada_si_el_proveedor_falla()
    {
        proveedor.ObtenerAsync(2027, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<FeriadoExternoDto>>(
                new ReglaNegocioException("No se pudo conectar con el servicio de feriados.")));

        var servicio = new ServicioFeriados(contexto, proveedor);

        await Should.ThrowAsync<ReglaNegocioException>(
            () => servicio.SincronizarAsync(2027, "coordinador@gca.local", CancellationToken.None));

        (await contexto.Feriados.CountAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData(1800)]
    [InlineData(2200)]
    public async Task Sincronizar_rechaza_anios_fuera_de_rango_sin_llamar_al_proveedor(int anio)
    {
        var servicio = new ServicioFeriados(contexto, proveedor);

        await Should.ThrowAsync<ReglaNegocioException>(
            () => servicio.SincronizarAsync(anio, "coordinador@gca.local", CancellationToken.None));

        await proveedor.DidNotReceive().ObtenerAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
