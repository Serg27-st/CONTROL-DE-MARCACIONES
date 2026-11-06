using System.Security.Claims;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Seguridad;
using ControlMarcacion.Web.Seguridad;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Web.Tests.Seguridad;

public sealed class ProveedorAlcancePlantasHttpTests : IDisposable
{
    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;

    public ProveedorAlcancePlantasHttpTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();
        contexto = new MarcacionDbContext(new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexion)
            .Options);
        contexto.Database.EnsureCreated();
    }

    [Fact]
    public async Task SEC_CATREADY_01_coordinador_con_asignacion_queda_restringido()
    {
        await CrearUsuarioAsync("coord-1", 1);

        var plantas = await Proveedor("coord-1", RolesSistema.Coordinador).PlantasVisiblesAsync();

        plantas.ShouldNotBeNull();
        plantas.ShouldBe([1]);
    }

    [Fact]
    public async Task SEC_CATREADY_06_alcance_vacio_de_administrador_no_es_global()
    {
        await CrearUsuarioAsync("admin-vacio");

        var plantas = await Proveedor("admin-vacio", RolesSistema.Administrador).PlantasVisiblesAsync();

        plantas.ShouldNotBeNull();
        plantas.ShouldBeEmpty();
    }

    [Fact]
    public async Task SEC_CATREADY_07_coordinador_global_sin_asignaciones_conserva_acceso_legitimo()
    {
        await CrearUsuarioAsync("coord-global");

        var plantas = await Proveedor("coord-global", RolesSistema.Coordinador).PlantasVisiblesAsync();

        plantas.ShouldBeNull();
    }

    [Fact]
    public async Task SEC_CATREADY_08_asignaciones_multiples_limitan_al_conjunto_registrado()
    {
        await CrearUsuarioAsync("coord-multiple", 2, 1, 2);

        var plantas = await Proveedor("coord-multiple", RolesSistema.Coordinador).PlantasVisiblesAsync();

        plantas.ShouldNotBeNull();
        plantas.Order().ShouldBe([1, 2]);
    }

    [Fact]
    public async Task SEC_CATREADY_14_identidad_operativa_sin_identificador_falla_cerrada()
    {
        var identidad = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RolesSistema.Coordinador)],
            authenticationType: "Prueba");
        var acceso = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
        };

        var plantas = await new ProveedorAlcancePlantasHttp(acceso, contexto).PlantasVisiblesAsync();

        plantas.ShouldNotBeNull();
        plantas.ShouldBeEmpty();
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    private ProveedorAlcancePlantasHttp Proveedor(string usuarioId, string rol)
    {
        var identidad = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, usuarioId),
            new Claim(ClaimTypes.Name, $"{usuarioId}@prueba.local"),
            new Claim(ClaimTypes.Role, rol)
        ], "Prueba");
        var acceso = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
        };
        return new ProveedorAlcancePlantasHttp(acceso, contexto);
    }

    private async Task CrearUsuarioAsync(string id, params long[] plantas)
    {
        contexto.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = $"{id}@prueba.local",
            NormalizedUserName = $"{id}@prueba.local".ToUpperInvariant(),
            Email = $"{id}@prueba.local",
            NormalizedEmail = $"{id}@prueba.local".ToUpperInvariant(),
            NombreCompleto = "USUARIO DE PRUEBA",
            Activo = true
        });
        contexto.UsuariosPlanta.AddRange(plantas.Distinct().Select(plantaId => new UsuarioPlanta
        {
            UsuarioId = id,
            PlantaId = plantaId
        }));
        await contexto.SaveChangesAsync();
    }
}
