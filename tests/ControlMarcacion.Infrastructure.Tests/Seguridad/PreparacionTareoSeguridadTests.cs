using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Seguridad;

public sealed class PreparacionTareoSeguridadTests : IDisposable
{
    private static readonly DateOnly Julio = new(2026, 7, 1);
    private static readonly DateOnly FinJulio = new(2026, 7, 31);
    private static readonly DateOnly Agosto = new(2026, 8, 1);
    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;

    public PreparacionTareoSeguridadTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();
        contexto = new MarcacionDbContext(new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexion)
            .Options);
        contexto.Database.EnsureCreated();
        SembrarCatalogos();
    }

    [Fact]
    public async Task SEC_CATREADY_02_consulta_sin_filtro_respeta_alcance()
    {
        var autorizado = await CrearTrabajadorAsync(1, "AUTORIZADO");
        var ajeno = await CrearTrabajadorAsync(2, "AJENO");

        var resultados = await Servicio(1).EvaluarAutorizadosAsync(Filtro());

        resultados.Select(r => r.TrabajadorId).ShouldBe([autorizado.Id]);
        resultados.ShouldNotContain(r => r.TrabajadorId == ajeno.Id);
    }

    [Fact]
    public async Task SEC_CATREADY_03_unidad_no_autorizada_no_devuelve_informacion()
    {
        await CrearTrabajadorAsync(1, "AUTORIZADO");
        await CrearTrabajadorAsync(2, "AJENO");

        var resultados = await Servicio(1).EvaluarAutorizadosAsync(Filtro(plantaId: 2));

        resultados.ShouldBeEmpty();
    }

    [Fact]
    public async Task SEC_CATREADY_04_parametro_planta_no_amplia_alcance()
    {
        var ajeno = await CrearTrabajadorAsync(2, "PARAMETRO");

        var resultados = await Servicio(1).EvaluarAutorizadosAsync(Filtro(plantaId: 2));

        resultados.ShouldNotContain(r => r.TrabajadorId == ajeno.Id);
    }

    [Fact]
    public async Task SEC_CATREADY_05_id_de_trabajador_ajeno_responde_como_no_encontrado()
    {
        var ajeno = await CrearTrabajadorAsync(2, "ID AJENO");

        await Should.ThrowAsync<RecursoNoEncontradoException>(() =>
            Servicio(1).EvaluarFechaAsync(ajeno.Id, Julio, 1));
    }

    [Fact]
    public async Task SEC_CATREADY_09_consulta_historica_respeta_unidad_de_la_fecha()
    {
        var trabajador = await CrearTrabajadorAsync(2, "HISTORICO");
        var periodo = await contexto.PeriodosLaborales
            .Include(p => p.AsignacionesPlanta)
            .SingleAsync(p => p.TrabajadorId == trabajador.Id);
        periodo.AsignacionesPlanta.Single().FechaFin = FinJulio;
        periodo.AsignacionesPlanta.Add(new AsignacionPlanta
        {
            PlantaId = 1,
            FechaInicio = Agosto
        });
        trabajador.PlantaId = 1;
        await contexto.SaveChangesAsync();

        await Should.ThrowAsync<RecursoNoEncontradoException>(() =>
            Servicio(1).EvaluarFechaAsync(trabajador.Id, Julio, 1));

        var actual = await Servicio(1).EvaluarFechaAsync(trabajador.Id, Agosto, 2);
        actual.Dias.Single().PlantaId.ShouldBe(1);
    }

    [Fact]
    public async Task SEC_CATREADY_10_cambio_de_unidad_no_expone_dias_ajenos()
    {
        var trabajador = await CrearTrabajadorAsync(1, "CAMBIO");
        var periodo = await contexto.PeriodosLaborales
            .Include(p => p.AsignacionesPlanta)
            .SingleAsync(p => p.TrabajadorId == trabajador.Id);
        periodo.AsignacionesPlanta.Single().FechaFin = FinJulio;
        periodo.AsignacionesPlanta.Add(new AsignacionPlanta
        {
            PlantaId = 2,
            FechaInicio = Agosto
        });
        trabajador.PlantaId = 2;
        await contexto.SaveChangesAsync();

        var resultado = await Servicio(1).EvaluarIntervaloAsync(
            trabajador.Id, FinJulio, Agosto, periodoId: null);

        resultado.Dias.Count.ShouldBe(1);
        resultado.Dias.Single().Fecha.ShouldBe(FinJulio);
        resultado.Dias.Single().PlantaId.ShouldBe(1);
    }

    [Fact]
    public async Task SEC_CATREADY_12_estados_funcionales_se_conservan_con_alcance()
    {
        var listo = await CrearTrabajadorAsync(1, "LISTO");
        var incompleto = await CrearTrabajadorAsync(1, "INCOMPLETO", conTurno: false);
        var conflicto = await CrearTrabajadorAsync(1, "CONFLICTO", conflictoMarcador: true);

        var resultados = await Servicio(1).EvaluarAutorizadosAsync(Filtro());

        resultados.Single(r => r.TrabajadorId == listo.Id).Estado.ShouldBe(EstadoPreparacionTareo.Listo);
        resultados.Single(r => r.TrabajadorId == incompleto.Id).Estado.ShouldBe(EstadoPreparacionTareo.Incompleto);
        resultados.Single(r => r.TrabajadorId == conflicto.Id).Estado.ShouldBe(EstadoPreparacionTareo.Conflicto);
    }

    [Fact]
    public async Task SEC_CATREADY_13_consulta_de_seguridad_no_produce_escrituras()
    {
        await CrearTrabajadorAsync(1, "SOLO LECTURA");
        contexto.ChangeTracker.Clear();
        var antes = await ConteosAsync();

        await Servicio(1).EvaluarAutorizadosAsync(Filtro());

        (await ConteosAsync()).ShouldBe(antes);
        contexto.ChangeTracker.Entries().ShouldBeEmpty();
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    private ValidadorPreparacionTareo Servicio(params long[] plantas) =>
        new(contexto, new AlcanceDePlantas(plantas));

    private static FiltroPreparacionTareo Filtro(long? plantaId = null) => new()
    {
        PeriodoId = 1,
        Desde = Julio,
        Hasta = FinJulio,
        PlantaId = plantaId
    };

    private async Task<Trabajador> CrearTrabajadorAsync(
        long plantaId,
        string nombre,
        bool conTurno = true,
        bool conflictoMarcador = false)
    {
        var secuencia = await contexto.Trabajadores.CountAsync() + 1;
        var equipoId = 93000 + secuencia * 2;
        var codigoMarcador = $"SEC-{secuencia}-A";
        contexto.AddRange(
            new EquipoMarcador
            {
                Id = equipoId,
                Codigo = $"SEC-EQ-{secuencia}-A",
                Nombre = $"Equipo seguridad {secuencia}A",
                PlantaId = plantaId
            },
            new Marcador
            {
                Id = equipoId,
                EquipoId = equipoId,
                PlantaId = plantaId,
                Codigo = codigoMarcador
            });

        var trabajador = new Trabajador
        {
            TipoDocumentoId = 1,
            NumeroDocumento = $"{91000000 + secuencia}",
            ApellidoPaterno = nombre,
            ApellidoMaterno = "SEGURIDAD",
            Nombres = "CATREADY",
            Sexo = Sexo.Femenino,
            EmpresaId = 1,
            PlantaId = plantaId,
            TipoPlanilla = TipoPlanilla.Empleado,
            PuestoId = 1,
            FechaIngreso = Julio
        };
        var periodo = new PeriodoLaboral
        {
            EmpresaId = 1,
            FechaIngreso = Julio
        };
        periodo.AsignacionesPlanta.Add(new AsignacionPlanta
        {
            PlantaId = plantaId,
            FechaInicio = Julio
        });
        trabajador.PeriodosLaborales.Add(periodo);
        trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            EquipoId = equipoId,
            IdMarcador = codigoMarcador,
            FechaInicio = Julio
        });
        if (conflictoMarcador)
        {
            contexto.AddRange(
                new EquipoMarcador
                {
                    Id = equipoId + 1,
                    Codigo = $"SEC-EQ-{secuencia}-B",
                    Nombre = $"Equipo seguridad {secuencia}B",
                    PlantaId = plantaId
                },
                new Marcador
                {
                    Id = equipoId + 1,
                    EquipoId = equipoId + 1,
                    PlantaId = plantaId,
                    Codigo = $"SEC-{secuencia}-B"
                });
            trabajador.AsignacionesMarcador.First().FechaFin = FinJulio;
            trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
            {
                EquipoId = equipoId + 1,
                IdMarcador = $"SEC-{secuencia}-B",
                FechaInicio = Julio
            });
        }
        if (conTurno)
        {
            trabajador.AsignacionesTurno.Add(new AsignacionTurno
            {
                TurnoId = 1,
                FechaInicio = Julio
            });
        }

        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync();
        return trabajador;
    }

    private async Task<(int Trabajadores, int Periodos, int Plantas, int Marcadores, int Turnos)> ConteosAsync() =>
        (await contexto.Trabajadores.CountAsync(),
         await contexto.PeriodosLaborales.CountAsync(),
         await contexto.AsignacionesPlanta.CountAsync(),
         await contexto.AsignacionesMarcador.CountAsync(),
         await contexto.AsignacionesTurno.CountAsync());

    private void SembrarCatalogos()
    {
        contexto.AddRange(
            new EquipoMarcador { Id = 91001, Codigo = "SEC-EQ-1A", Nombre = "Equipo seguridad 1A", PlantaId = 1 },
            new EquipoMarcador { Id = 91002, Codigo = "SEC-EQ-1B", Nombre = "Equipo seguridad 1B", PlantaId = 1 },
            new EquipoMarcador { Id = 92001, Codigo = "SEC-EQ-2A", Nombre = "Equipo seguridad 2A", PlantaId = 2 },
            new Marcador { Id = 91001, EquipoId = 91001, PlantaId = 1, Codigo = "100" },
            new Marcador { Id = 91002, EquipoId = 91002, PlantaId = 1, Codigo = "200" },
            new Marcador { Id = 92001, EquipoId = 92001, PlantaId = 2, Codigo = "100" });
        contexto.SaveChanges();
    }
}
