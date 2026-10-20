using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
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

/// <summary>
/// La ficha asigna el número de marcador escribiéndolo, con el aparato y la fecha desde la
/// que rige.
/// </summary>
/// <remarks>
/// Antes solo se podía elegir un número ya dado de alta en el catálogo, y cualquier
/// cambio cortaba "hoy": un número mal puesto desde el ingreso no tenía corrección, y sus
/// marcaciones quedaban a nombre de otra persona. Corre sobre SQLite en memoria con la
/// semilla real, porque ejercita los índices únicos del catálogo de marcadores.
/// </remarks>
public sealed class NumeroDeMarcadorEnFichaTests : IDisposable
{
    private const long PlantaIntracan = 2;
    private const long EmpresaConsulting = 1;
    private const long PuestoOperario = 1;
    private const long TipoDocumentoDni = 1;

    private static readonly DateOnly Ingreso = new(2026, 3, 1);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioTrabajadores servicio;
    private readonly long aparato;

    public NumeroDeMarcadorEnFichaTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        var equipo = new EquipoMarcador
        {
            Codigo = "P20M-01",
            Nombre = "Planta 20 mil",
            PlantaId = PlantaIntracan,
            Activo = true
        };
        contexto.EquiposMarcador.Add(equipo);
        contexto.SaveChanges();
        aparato = equipo.Id;

        var alcance = new AlcanceSinRestriccion();

        servicio = new ServicioTrabajadores(
            new TrabajadorRepository(contexto),
            new ConsultaTrabajadores(contexto, alcance),
            new ConsultaCatalogos(contexto, new AlcanceSinRestriccion()),
            alcance,
            new ServicioMarcadores(contexto),
            new ServicioPeriodos(contexto));
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task Un_numero_nuevo_se_registra_en_el_catalogo_y_rige_desde_el_ingreso()
    {
        var id = await servicio.CrearAsync(Ficha("41300001") with
        {
            IdMarcador = "25",
            EquipoId = aparato,
            RegistrarMarcadorSiFalta = true
        }, "tester");

        (await contexto.Marcadores.AnyAsync(m => m.EquipoId == aparato && m.Codigo == "25")).ShouldBeTrue();

        var asignacion = await contexto.AsignacionesMarcador.SingleAsync(a => a.TrabajadorId == id);
        asignacion.IdMarcador.ShouldBe("25");
        asignacion.FechaInicio.ShouldBe(Ingreso);
        asignacion.FechaFin.ShouldBeNull();
    }

    [Fact]
    public async Task Sin_pedir_registro_un_numero_que_no_esta_en_el_catalogo_se_rechaza()
    {
        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            servicio.CrearAsync(Ficha("41300002") with { IdMarcador = "25", EquipoId = aparato }, "tester"));

        error.Message.ShouldContain("no está registrado en el equipo indicado");
        (await contexto.Marcadores.AnyAsync()).ShouldBeFalse();
    }

    /// <summary>
    /// El número estaba mal desde el ingreso. Con la fecha de ingreso como "rige desde" se
    /// corrige en sitio: queda una sola asignación, con el número correcto desde el inicio.
    /// </summary>
    [Fact]
    public async Task Una_fecha_igual_al_inicio_corrige_el_numero_en_sitio()
    {
        var id = await servicio.CrearAsync(Ficha("41300003") with
        {
            IdMarcador = "52",
            EquipoId = aparato,
            RegistrarMarcadorSiFalta = true
        }, "tester");

        await servicio.ActualizarAsync(Ficha("41300003") with
        {
            Id = id,
            IdMarcador = "25",
            EquipoId = aparato,
            MarcadorVigenteDesde = Ingreso,
            RegistrarMarcadorSiFalta = true
        }, "tester");

        var asignaciones = await contexto.AsignacionesMarcador.AsNoTracking()
            .Where(a => a.TrabajadorId == id).ToListAsync();

        asignaciones.Count.ShouldBe(1);
        asignaciones[0].IdMarcador.ShouldBe("25");
        asignaciones[0].FechaInicio.ShouldBe(Ingreso);
    }

    [Fact]
    public async Task Una_fecha_posterior_cierra_el_numero_anterior_el_dia_previo()
    {
        var id = await servicio.CrearAsync(Ficha("41300004") with
        {
            IdMarcador = "52",
            EquipoId = aparato,
            RegistrarMarcadorSiFalta = true
        }, "tester");

        await servicio.ActualizarAsync(Ficha("41300004") with
        {
            Id = id,
            IdMarcador = "25",
            EquipoId = aparato,
            MarcadorVigenteDesde = new DateOnly(2026, 8, 1),
            RegistrarMarcadorSiFalta = true
        }, "tester");

        var asignaciones = await contexto.AsignacionesMarcador.AsNoTracking()
            .Where(a => a.TrabajadorId == id).OrderBy(a => a.FechaInicio).ToListAsync();

        asignaciones.Count.ShouldBe(2);
        asignaciones[0].IdMarcador.ShouldBe("52");
        asignaciones[0].FechaFin.ShouldBe(new DateOnly(2026, 7, 31));
        asignaciones[1].IdMarcador.ShouldBe("25");
        asignaciones[1].FechaInicio.ShouldBe(new DateOnly(2026, 8, 1));
        asignaciones[1].FechaFin.ShouldBeNull();
    }

    /// <summary>
    /// Un número que otro trabajador dejó al cesar se puede reutilizar desde el día
    /// siguiente, no desde el ingreso de quien lo recibe.
    /// </summary>
    [Fact]
    public async Task Un_numero_liberado_se_reutiliza_desde_la_fecha_indicada()
    {
        var anterior = await servicio.CrearAsync(Ficha("41300005") with
        {
            IdMarcador = "25",
            EquipoId = aparato,
            RegistrarMarcadorSiFalta = true
        }, "tester");

        await servicio.DarDeBajaAsync(new BajaTrabajadorRequest
        {
            TrabajadorId = anterior,
            FechaCese = new DateOnly(2026, 6, 30),
            Motivo = MotivoCese.RenunciaVoluntaria
        }, "tester");

        var solapado = await Should.ThrowAsync<ReglaNegocioException>(() =>
            servicio.CrearAsync(Ficha("41300006") with
            {
                IdMarcador = "25",
                EquipoId = aparato,
                MarcadorVigenteDesde = new DateOnly(2026, 6, 15),
                RegistrarMarcadorSiFalta = true
            }, "tester"));

        solapado.Message.ShouldContain("ya está asignado a otro trabajador");

        var reemplazo = await servicio.CrearAsync(Ficha("41300006") with
        {
            IdMarcador = "25",
            EquipoId = aparato,
            MarcadorVigenteDesde = new DateOnly(2026, 7, 1),
            RegistrarMarcadorSiFalta = true
        }, "tester");

        (await contexto.AsignacionesMarcador.SingleAsync(a => a.TrabajadorId == reemplazo))
            .FechaInicio.ShouldBe(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public async Task El_numero_no_puede_regir_antes_del_ingreso()
    {
        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            servicio.CrearAsync(Ficha("41300007") with
            {
                IdMarcador = "25",
                EquipoId = aparato,
                MarcadorVigenteDesde = Ingreso.AddDays(-1),
                RegistrarMarcadorSiFalta = true
            }, "tester"));

        error.Message.ShouldContain("anterior a la fecha de ingreso");
    }

    private static GuardarTrabajadorRequest Ficha(string documento) => new()
    {
        TipoDocumentoId = TipoDocumentoDni,
        NumeroDocumento = documento,
        ApellidoPaterno = "PRUEBA",
        ApellidoMaterno = "FICHA",
        Nombres = "NUMERO",
        Sexo = Sexo.Femenino,
        EmpresaId = EmpresaConsulting,
        TipoPlanilla = TipoPlanilla.Obrero,
        PlantaId = PlantaIntracan,
        PuestoId = PuestoOperario,
        FechaIngreso = Ingreso
    };
}
