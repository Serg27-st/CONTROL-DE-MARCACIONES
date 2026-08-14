using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Organizacion;
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

    /// <summary>
    /// Un feriado <b>de planta</b> no debe impedir que la sincronización traiga el nacional
    /// de esa misma fecha.
    /// </summary>
    /// <remarks>
    /// Es el fallo que la carga manual destapó: la sincronización comparaba contra todas
    /// las filas de la fecha, así que un aniversario de Planta 01 el 28 de julio habría
    /// hecho que se saltara Fiestas Patrias, dejando a las otras tres plantas sin el
    /// feriado nacional y con un día de trabajo pagado sin la sobretasa del 100 %.
    /// </remarks>
    [Fact]
    public async Task Sincronizar_trae_el_nacional_aunque_ese_dia_ya_tenga_un_feriado_de_planta()
    {
        await contexto.Feriados.AddAsync(new Feriado
        {
            Fecha = new DateOnly(2027, 7, 28),
            Descripcion = "Aniversario de Planta 01",
            PlantaId = 7
        });
        await contexto.SaveChangesAsync();

        proveedor.ObtenerAsync(2027, Arg.Any<CancellationToken>()).Returns(
        [
            new FeriadoExternoDto(new DateOnly(2027, 7, 28), "Fiestas Patrias")
        ]);

        var servicio = new ServicioFeriados(contexto, proveedor);

        var resultado = await servicio.SincronizarAsync(2027, "coordinador@gca.local", CancellationToken.None);

        resultado.Agregados.ShouldBe(1);

        var delDia = await contexto.Feriados
            .Where(f => f.Fecha == new DateOnly(2027, 7, 28))
            .ToListAsync();

        // Conviven los dos: el de planta que ya estaba y el nacional recién traído.
        delDia.Count.ShouldBe(2);
        delDia.ShouldContain(f => f.PlantaId == 7 && f.Descripcion == "Aniversario de Planta 01");
        delDia.ShouldContain(f => f.PlantaId == null && f.Descripcion == "Fiestas Patrias");
    }

    // ------------------------------------------------------------------
    // Carga manual
    // ------------------------------------------------------------------

    [Fact]
    public async Task Guardar_da_de_alta_un_feriado_de_planta()
    {
        await SembrarPlantaAsync(7);

        var servicio = new ServicioFeriados(contexto, proveedor);

        var id = await servicio.GuardarAsync(
            new GuardarFeriadoRequest
            {
                Fecha = new DateOnly(2027, 11, 12),
                Descripcion = "Aniversario de Planta 01",
                PlantaId = 7
            },
            "coordinador@gca.local",
            CancellationToken.None);

        var guardado = await contexto.Feriados.SingleAsync(f => f.Id == id);

        guardado.Fecha.ShouldBe(new DateOnly(2027, 11, 12));
        guardado.Descripcion.ShouldBe("Aniversario de Planta 01");
        guardado.PlantaId.ShouldBe(7);
        guardado.Activo.ShouldBeTrue();
        guardado.CreadoPor.ShouldBe("coordinador@gca.local");
    }

    [Fact]
    public async Task Guardar_rechaza_dos_feriados_con_el_mismo_alcance_el_mismo_dia()
    {
        var servicio = new ServicioFeriados(contexto, proveedor);
        var solicitud = new GuardarFeriadoRequest
        {
            Fecha = new DateOnly(2027, 6, 29),
            Descripcion = "San Pedro y San Pablo"
        };

        await servicio.GuardarAsync(solicitud, "coordinador@gca.local", CancellationToken.None);

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            () => servicio.GuardarAsync(solicitud, "coordinador@gca.local", CancellationToken.None));

        error.Message.ShouldContain("29/06/2027");
        (await contexto.Feriados.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Guardar_rechaza_un_feriado_de_planta_sobre_una_fecha_ya_nacional()
    {
        await SembrarPlantaAsync(7);

        var servicio = new ServicioFeriados(contexto, proveedor);

        await servicio.GuardarAsync(
            new GuardarFeriadoRequest
            {
                Fecha = new DateOnly(2027, 7, 28),
                Descripcion = "Fiestas Patrias"
            },
            "coordinador@gca.local",
            CancellationToken.None);

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            () => servicio.GuardarAsync(
                new GuardarFeriadoRequest
                {
                    Fecha = new DateOnly(2027, 7, 28),
                    Descripcion = "Aniversario",
                    PlantaId = 7
                },
                "coordinador@gca.local",
                CancellationToken.None));

        error.Message.ShouldContain("ya es feriado nacional");
        (await contexto.Feriados.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Guardar_permite_editar_el_propio_feriado_sin_chocar_consigo_mismo()
    {
        var servicio = new ServicioFeriados(contexto, proveedor);

        var id = await servicio.GuardarAsync(
            new GuardarFeriadoRequest
            {
                Fecha = new DateOnly(2027, 8, 30),
                Descripcion = "Santa Rosa de Lima"
            },
            "coordinador@gca.local",
            CancellationToken.None);

        // Misma fecha y mismo alcance: es él mismo, solo cambia el texto.
        await servicio.GuardarAsync(
            new GuardarFeriadoRequest
            {
                Id = id,
                Fecha = new DateOnly(2027, 8, 30),
                Descripcion = "Santa Rosa de Lima (corregido)"
            },
            "coordinador@gca.local",
            CancellationToken.None);

        var guardado = await contexto.Feriados.SingleAsync(f => f.Id == id);
        guardado.Descripcion.ShouldBe("Santa Rosa de Lima (corregido)");
        (await contexto.Feriados.CountAsync()).ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public async Task Guardar_exige_una_descripcion_utilizable(string descripcion)
    {
        var servicio = new ServicioFeriados(contexto, proveedor);

        await Should.ThrowAsync<ReglaNegocioException>(
            () => servicio.GuardarAsync(
                new GuardarFeriadoRequest
                {
                    Fecha = new DateOnly(2027, 1, 6),
                    Descripcion = descripcion
                },
                "coordinador@gca.local",
                CancellationToken.None));

        (await contexto.Feriados.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Guardar_rechaza_una_planta_que_no_existe()
    {
        var servicio = new ServicioFeriados(contexto, proveedor);

        await Should.ThrowAsync<RecursoNoEncontradoException>(
            () => servicio.GuardarAsync(
                new GuardarFeriadoRequest
                {
                    Fecha = new DateOnly(2027, 1, 6),
                    Descripcion = "Feriado de una planta inventada",
                    PlantaId = 999
                },
                "coordinador@gca.local",
                CancellationToken.None));

        (await contexto.Feriados.CountAsync()).ShouldBe(0);
    }

    /// <summary>
    /// Desactivar es baja lógica: la fila se conserva porque los tareos que ya aplicaron
    /// la sobretasa del 100 % necesitan que siga existiendo lo que los sustenta.
    /// </summary>
    [Fact]
    public async Task CambiarEstado_desactiva_sin_borrar_la_fila()
    {
        var servicio = new ServicioFeriados(contexto, proveedor);

        var id = await servicio.GuardarAsync(
            new GuardarFeriadoRequest
            {
                Fecha = new DateOnly(2027, 10, 8),
                Descripcion = "Combate de Angamos"
            },
            "coordinador@gca.local",
            CancellationToken.None);

        await servicio.CambiarEstadoAsync(id, activo: false, "coordinador@gca.local", CancellationToken.None);

        var guardado = await contexto.Feriados.SingleAsync(f => f.Id == id);
        guardado.Activo.ShouldBeFalse();
        (await contexto.Feriados.CountAsync()).ShouldBe(1);
    }

    private async Task SembrarPlantaAsync(long id)
    {
        await contexto.Plantas.AddAsync(new Planta
        {
            Id = id, Codigo = "P01", Nombre = "Planta 01", EmpresaId = 1
        });
        await contexto.SaveChangesAsync();
    }
}
