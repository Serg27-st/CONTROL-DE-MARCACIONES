using System.Reflection;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Application.MarcacionesProvisionales;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.MarcacionesProvisionales;
using ControlMarcacion.Infrastructure.MarcacionesProvisionales;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Seguridad;

/// <summary>
/// Contratos deliberadamente rojos para el aislamiento por planta del módulo PMV.
/// La construcción por reflexión permite expresar el contrato antes de cambiar las firmas
/// productivas: cuando los servicios acepten el proveedor, estas pruebas continuarán hasta
/// verificar el comportamiento y no quedarán reducidas a una comprobación estructural.
/// </summary>
public sealed class AislamientoPmvAcceptanceTests : IDisposable
{
    private readonly SqliteConnection _conexion;
    private readonly MarcacionDbContext _contexto;
    private readonly IProveedorAlcancePlantas _alcance;
    private readonly long _plantaA;
    private readonly long _equipoA;
    private readonly long _equipoB;

    public AislamientoPmvAcceptanceTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        _contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(_conexion).Options);
        _contexto.Database.EnsureCreated();

        var plantas = _contexto.Plantas.OrderBy(p => p.Id).Take(2).ToList();
        plantas.Count.ShouldBe(2);
        _plantaA = plantas[0].Id;
        var plantaB = plantas[1].Id;
        _alcance = new AlcanceDePlantas(_plantaA);

        var equipoA = new EquipoMarcador
        {
            Codigo = "PMV-A",
            Nombre = "Equipo planta A",
            PlantaId = _plantaA,
            Activo = true
        };
        var equipoB = new EquipoMarcador
        {
            Codigo = "PMV-B",
            Nombre = "Equipo planta B",
            PlantaId = plantaB,
            Activo = true
        };
        _contexto.EquiposMarcador.AddRange(equipoA, equipoB);
        _contexto.SaveChanges();
        _equipoA = equipoA.Id;
        _equipoB = equipoB.Id;

        _contexto.ImportacionesProvisionales.AddRange(
            CrearImportacion(equipoA.Id, "planta-a.txt", "HASH-A"),
            CrearImportacion(equipoB.Id, "planta-b.txt", "HASH-B"));
        _contexto.SaveChanges();
    }

    public void Dispose()
    {
        _contexto.Dispose();
        _conexion.Dispose();
    }

    [Fact]
    public async Task SEC_01_admin_de_planta_A_no_lista_equipos_de_planta_B_en_PMV()
    {
        var consulta = CrearConAlcance<IConsultaCatalogos>(typeof(ConsultaCatalogos), _contexto);

        var equipos = await consulta.EquiposActivosAsync();

        equipos.ShouldNotBeEmpty();
        equipos.ShouldAllBe(e => e.PlantaId == _plantaA);

        var fuente = Substitute.For<IFuenteDeMarcaciones>();
        var importacion = CrearConAlcance<IServicioImportacionProvisional>(
            typeof(ServicioImportacionProvisional), _contexto, fuente);
        var historial = await importacion.ListarImportacionesAsync();
        historial.Select(i => i.EquipoId).ShouldBe([_equipoA]);
    }

    [Fact]
    public async Task SEC_02_admin_de_planta_A_no_consulta_ni_exporta_equipo_de_planta_B_en_PMV()
    {
        var consulta = CrearConAlcance<IServicioConsultaMarcacionProvisional>(
            typeof(ServicioConsultaMarcacionProvisional), _contexto);

        await Should.ThrowAsync<ReglaNegocioException>(() => consulta.ConsultarAsync(
            _equipoB,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1)));
    }

    [Fact]
    public async Task SEC_03_admin_de_planta_A_no_importa_en_equipo_de_planta_B_en_PMV()
    {
        var fuente = Substitute.For<IFuenteDeMarcaciones>();
        fuente.Leer(Arg.Any<Stream>()).Returns(new ResultadoLectura([], []));
        var importacion = CrearConAlcance<IServicioImportacionProvisional>(
            typeof(ServicioImportacionProvisional), _contexto, fuente);

        await using var contenido = new MemoryStream([1]);
        await Should.ThrowAsync<ReglaNegocioException>(() => importacion.ImportarAsync(
            contenido,
            "PMV-B_attlog.txt",
            _equipoB,
            "admin-planta-a"));
    }

    [Fact]
    public async Task SEC_04_admin_de_planta_A_puede_consultar_e_importar_en_equipo_de_planta_A()
    {
        var consulta = CrearConAlcance<IServicioConsultaMarcacionProvisional>(
            typeof(ServicioConsultaMarcacionProvisional), _contexto);
        var resultado = await consulta.ConsultarAsync(
            _equipoA,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1));
        resultado.Jornadas.ShouldBeEmpty();

        var fuente = Substitute.For<IFuenteDeMarcaciones>();
        fuente.Leer(Arg.Any<Stream>()).Returns(new ResultadoLectura([], []));
        var importacion = CrearConAlcance<IServicioImportacionProvisional>(
            typeof(ServicioImportacionProvisional), _contexto, fuente);
        await using var contenido = new MemoryStream([4]);
        var carga = await importacion.ImportarAsync(
            contenido,
            "PMV-A_attlog.txt",
            _equipoA,
            "admin-planta-a");
        carga.ImportacionId.ShouldBeGreaterThan(0);
    }

    private static ImportacionProvisional CrearImportacion(long equipoId, string archivo, string hash) =>
        new()
        {
            EquipoId = equipoId,
            NombreArchivo = archivo,
            HashContenido = hash,
            FechaCarga = new DateTime(2026, 7, 1, 8, 0, 0),
            CargadoPorUsuario = "prueba",
            CreadoPor = "prueba"
        };

    private TServicio CrearConAlcance<TServicio>(
        Type implementacion,
        MarcacionDbContext contexto,
        params object[] adicionales)
        where TServicio : class
    {
        var constructor = implementacion
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(c => c.GetParameters()
                .Any(p => p.ParameterType == typeof(IProveedorAlcancePlantas)));

        constructor.ShouldNotBeNull(
            $"{implementacion.Name} debe consumir IProveedorAlcancePlantas para aplicar el aislamiento PMV");

        object[] disponibles = [contexto, _alcance, .. adicionales];
        var argumentos = constructor.GetParameters()
            .Select(p => disponibles.Single(o => p.ParameterType.IsInstanceOfType(o)))
            .ToArray();

        return constructor.Invoke(argumentos).ShouldBeAssignableTo<TServicio>();
    }
}
