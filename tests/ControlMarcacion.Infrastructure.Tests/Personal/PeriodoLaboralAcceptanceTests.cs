using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using ControlMarcacion.Infrastructure.Persistencia.Repositorios;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Personal;

/// <summary>Casos de aceptación de la ruta real repositorio → servicio para NH-02.</summary>
public sealed class PeriodoLaboralAcceptanceTests : IDisposable
{
    private const long PlantaId = 2;
    private const long EmpresaId = 1;
    private const long PuestoId = 1;
    private const long TipoDocumentoId = 1;
    private static readonly DateOnly Ingreso = new(2026, 3, 1);

    private readonly SqliteConnection conexion;

    public PeriodoLaboralAcceptanceTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        using var contexto = CrearContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose()
    {
        conexion.Dispose();
    }

    [Fact]
    public async Task PL_01_crear_y_editar_dos_veces_conserva_un_solo_vinculo_abierto()
    {
        long id;
        await using (var contexto = CrearContexto())
        {
            id = await CrearServicio(contexto).CrearAsync(Ficha("41300081"), "tester");
        }

        await using (var contexto = CrearContexto())
        {
            await CrearServicio(contexto)
                .ActualizarAsync(Ficha("41300081") with { Id = id, Telefono = "111" }, "tester");
        }

        await using (var contexto = CrearContexto())
        {
            await CrearServicio(contexto)
                .ActualizarAsync(Ficha("41300081") with { Id = id, Telefono = "222" }, "tester");
        }

        await using var verificacion = CrearContexto();
        var vinculos = await verificacion.PeriodosLaborales.AsNoTracking()
            .Where(p => p.TrabajadorId == id).ToListAsync();
        vinculos.Count.ShouldBe(1);
        vinculos.Count(p => p.EstaAbierto).ShouldBe(1);
    }

    [Fact]
    public async Task PL_02_dar_de_baja_cierra_el_vinculo_activo()
    {
        long id;
        await using (var contexto = CrearContexto())
        {
            id = await CrearServicio(contexto).CrearAsync(Ficha("41300082"), "tester");
        }

        var cese = new DateOnly(2026, 9, 30);

        await using (var contexto = CrearContexto())
        {
            await CrearServicio(contexto).DarDeBajaAsync(new BajaTrabajadorRequest
            {
                TrabajadorId = id,
                FechaCese = cese,
                Motivo = MotivoCese.RenunciaVoluntaria
            }, "tester");
        }

        await using var verificacion = CrearContexto();
        var vinculo = await verificacion.PeriodosLaborales.AsNoTracking()
            .SingleAsync(p => p.TrabajadorId == id);
        vinculo.FechaCese.ShouldBe(cese);
        vinculo.MotivoCese.ShouldBe(MotivoCese.RenunciaVoluntaria);
    }

    [Fact]
    public async Task PL_03_alta_persiste_planta_dentro_del_vinculo_laboral()
    {
        long id;
        await using (var contexto = CrearContexto())
        {
            id = await CrearServicio(contexto).CrearAsync(Ficha("41300083"), "tester");
        }

        await using var verificacion = CrearContexto();
        var vinculo = await verificacion.PeriodosLaborales
            .AsNoTracking()
            .Include(p => p.AsignacionesPlanta)
            .SingleAsync(p => p.TrabajadorId == id);

        var asignacion = vinculo.AsignacionesPlanta.ShouldHaveSingleItem();
        asignacion.PeriodoLaboralId.ShouldBe(vinculo.Id);
        asignacion.PlantaId.ShouldBe(PlantaId);
        asignacion.FechaInicio.ShouldBe(Ingreso);
    }

    [Fact]
    public async Task PL_04_sqlite_rechaza_dos_periodos_abiertos_del_mismo_trabajador()
    {
        long id;
        await using (var contexto = CrearContexto())
        {
            id = await CrearServicio(contexto).CrearAsync(Ficha("41300084"), "tester");
        }

        await using var escrituraDirecta = CrearContexto();
        escrituraDirecta.PeriodosLaborales.Add(new PeriodoLaboral
        {
            TrabajadorId = id,
            EmpresaId = EmpresaId,
            FechaIngreso = Ingreso.AddYears(1)
        });

        await Should.ThrowAsync<DbUpdateException>(() => escrituraDirecta.SaveChangesAsync());
    }

    [Fact]
    public async Task PL_05_sqlite_rechaza_asignacion_de_planta_con_fechas_invertidas()
    {
        long id;
        await using (var contexto = CrearContexto())
        {
            id = await CrearServicio(contexto).CrearAsync(Ficha("41300085"), "tester");
        }

        await using var escrituraDirecta = CrearContexto();
        var periodoId = await escrituraDirecta.PeriodosLaborales
            .Where(p => p.TrabajadorId == id)
            .Select(p => p.Id)
            .SingleAsync();

        escrituraDirecta.AsignacionesPlanta.Add(new AsignacionPlanta
        {
            PeriodoLaboralId = periodoId,
            PlantaId = PlantaId,
            FechaInicio = Ingreso.AddDays(2),
            FechaFin = Ingreso.AddDays(1)
        });

        await Should.ThrowAsync<DbUpdateException>(() => escrituraDirecta.SaveChangesAsync());
    }

    private MarcacionDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);

    private static ServicioTrabajadores CrearServicio(MarcacionDbContext contexto)
    {
        var alcance = new AlcanceSinRestriccion();
        return new ServicioTrabajadores(
            new TrabajadorRepository(contexto),
            new ConsultaTrabajadores(contexto, alcance),
            new ConsultaCatalogos(contexto, alcance),
            alcance,
            new ServicioMarcadores(contexto),
            new ServicioPeriodos(contexto));
    }

    private static GuardarTrabajadorRequest Ficha(string documento) => new()
    {
        TipoDocumentoId = TipoDocumentoId,
        NumeroDocumento = documento,
        ApellidoPaterno = "PRUEBA",
        ApellidoMaterno = "VINCULO",
        Nombres = "LABORAL",
        Sexo = Sexo.Femenino,
        EmpresaId = EmpresaId,
        TipoPlanilla = TipoPlanilla.Obrero,
        PlantaId = PlantaId,
        PuestoId = PuestoId,
        FechaIngreso = Ingreso
    };
}
