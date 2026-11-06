using System.Globalization;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Auditoria;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Auditoria;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Catalogos;

/// <summary>
/// Contratos de regresión de QA-CAT-001 sobre SQLite real.
/// </summary>
public sealed class ServicioCatalogosQACAT001Tests : IDisposable
{
    private const string Actor = "qa-cat-001";

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioCatalogos servicio;

    public ServicioCatalogosQACAT001Tests()
    {
        conexion = new SqliteConnection("Data Source=:memory:");
        conexion.Open();

        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText = "PRAGMA foreign_keys = ON";
            comando.ExecuteNonQuery();
        }

        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexion)
            .AddInterceptors(new InterceptorAuditoria(new UsuarioActualDePrueba()))
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();
        servicio = new ServicioCatalogos(contexto);
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task QACAT_001_crear_planta_devuelve_el_id_persistido()
    {
        var idDevuelto = await servicio.GuardarAsync(
            "plantas",
            NuevaSolicitud("QACAT-PL", "Planta QA-CAT", padreId: 1),
            Actor,
            CancellationToken.None);

        var persistida = await contexto.Plantas
            .AsNoTracking()
            .SingleAsync(p => p.Codigo == "QACAT-PL");

        idDevuelto.ShouldBeGreaterThan(0);
        idDevuelto.ShouldBe(persistida.Id);
    }

    [Theory]
    [InlineData("puestos", null, "QACAT-PUE", "Puesto QA-CAT")]
    [InlineData("areas", 1L, "QACAT-ARE", "Área QA-CAT")]
    [InlineData("secciones", 1L, "QACAT-SEC", "Sección QA-CAT")]
    [InlineData("tipos-documento", null, "QACAT-TDO", "Documento QA-CAT")]
    public async Task QACAT_002_crear_otro_catalogo_afectado_devuelve_el_id_persistido(
        string clave,
        long? padreId,
        string codigo,
        string nombre)
    {
        var idDevuelto = await servicio.GuardarAsync(
            clave,
            NuevaSolicitud(codigo, nombre, padreId),
            Actor,
            CancellationToken.None);

        var persistido = await servicio.ObtenerAsync(clave, idDevuelto, CancellationToken.None);

        idDevuelto.ShouldBeGreaterThan(0);
        persistido.ShouldNotBeNull();
        persistido.Id.ShouldBe(idDevuelto);
        persistido.Codigo.ShouldBe(codigo);
    }

    [Fact]
    public async Task QACAT_003_actualizar_una_entidad_conserva_su_id()
    {
        var id = await servicio.GuardarAsync(
            "plantas",
            NuevaSolicitud("QACAT-UPD", "Planta antes", padreId: 1),
            Actor,
            CancellationToken.None);

        var idActualizado = await servicio.GuardarAsync(
            "plantas",
            NuevaSolicitud("QACAT-UPD", "Planta después", padreId: 1, id),
            Actor,
            CancellationToken.None);

        idActualizado.ShouldBe(id);
        (await contexto.Plantas.AsNoTracking().CountAsync(p => p.Codigo == "QACAT-UPD"))
            .ShouldBe(1);
        (await contexto.Plantas.AsNoTracking().SingleAsync(p => p.Id == id))
            .Nombre.ShouldBe("Planta después");
    }

    [Fact]
    public async Task QACAT_004_validacion_fallida_no_devuelve_un_id_de_alta_exitosa()
    {
        var primeraId = await servicio.GuardarAsync(
            "plantas",
            NuevaSolicitud("QACAT-DUP", "Planta única", padreId: 1),
            Actor,
            CancellationToken.None);

        primeraId.ShouldBeGreaterThan(0);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() => servicio.GuardarAsync(
            "plantas",
            NuevaSolicitud("QACAT-DUP", "Planta repetida", padreId: 1),
            Actor,
            CancellationToken.None));

        error.Message.ShouldContain("QACAT-DUP");
        (await contexto.Plantas.AsNoTracking().CountAsync(p => p.Codigo == "QACAT-DUP"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task QACAT_005_la_correccion_no_genera_registros_duplicados()
    {
        var id = await servicio.GuardarAsync(
            "puestos",
            NuevaSolicitud("QACAT-ONE", "Puesto único"),
            Actor,
            CancellationToken.None);

        id.ShouldBeGreaterThan(0);
        (await contexto.Puestos.AsNoTracking().CountAsync(p => p.Codigo == "QACAT-ONE"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task QACAT_006_el_alta_continua_siendo_auditable()
    {
        var id = await servicio.GuardarAsync(
            "plantas",
            NuevaSolicitud("QACAT-AUD", "Planta auditable", padreId: 1),
            Actor,
            CancellationToken.None);

        var auditoria = await contexto.RegistrosAuditoria
            .AsNoTracking()
            .SingleAsync(r => r.Tabla == "planta"
                              && r.ClaveRegistro == id.ToString(CultureInfo.InvariantCulture)
                              && r.Operacion == OperacionAuditada.Insercion);

        auditoria.Usuario.ShouldBe(Actor);
        auditoria.Detalle.ShouldContain("QACAT-AUD");
    }

    private static GuardarCatalogoRequest NuevaSolicitud(
        string codigo,
        string nombre,
        long? padreId = null,
        long? id = null) =>
        new()
        {
            Id = id,
            Codigo = codigo,
            Nombre = nombre,
            PadreId = padreId,
            Activo = true
        };

    private sealed class UsuarioActualDePrueba : IProveedorUsuarioActual
    {
        public string Usuario => Actor;
    }
}
