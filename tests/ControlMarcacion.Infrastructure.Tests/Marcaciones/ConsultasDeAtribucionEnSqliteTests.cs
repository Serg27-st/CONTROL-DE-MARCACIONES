using Microsoft.Extensions.Logging.Abstractions;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Marcaciones;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

/// <summary>
/// Las consultas que cambiaron con la atribución al calcular, sobre <b>SQLite real</b>.
/// </summary>
/// <remarks>
/// El proveedor InMemory ejecuta los LINQ en memoria y no traduce nada a SQL: un
/// <c>GroupBy</c> por aparato con conteo distinto, o una subconsulta con
/// <c>FirstOrDefault</c> en la proyección, pueden pasar ahí y reventar en producción. Esta
/// prueba las ejecuta contra el motor que usa el sistema.
/// </remarks>
public sealed class ConsultasDeAtribucionEnSqliteTests : IDisposable
{
    private const long PlantaDetergentes = 1;
    private const long EmpresaConsulting = 1;
    private const long PuestoOperario = 1;
    private const long TipoDocumentoDni = 1;

    private static readonly DateOnly Dia = new(2026, 7, 6);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioImportacion importacion;
    private readonly long aparatoAL;
    private readonly long aparatoMZ;

    public ConsultasDeAtribucionEnSqliteTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        var al = new EquipoMarcador { Codigo = "DET-AL", Nombre = "Detergentes A-L", PlantaId = PlantaDetergentes, Activo = true };
        var mz = new EquipoMarcador { Codigo = "DET-MZ", Nombre = "Detergentes M-Z", PlantaId = PlantaDetergentes, Activo = true };
        contexto.EquiposMarcador.AddRange(al, mz);
        contexto.SaveChanges();
        aparatoAL = al.Id;
        aparatoMZ = mz.Id;

        var configuracion = Options.Create(new OpcionesMarcador { DiasAntiguedadMaxima = 100_000 });
        importacion = new ServicioImportacion(
            contexto,
            new LectorMarcadorAutomatico(
                new LectorTxtZkteco(configuracion), new LectorRespaldoZkteco(configuracion)),
            configuracion,
            new ServicioEquipos(contexto));
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    /// <summary>
    /// El 14 del A-L tiene dueño; el 14 del M-Z no. Solo el segundo es pendiente, y el
    /// calendario cuenta dos personas: mismo número, dos aparatos.
    /// </summary>
    [Fact]
    public async Task Numeros_sin_asignar_y_cobertura_distinguen_aparatos()
    {
        await AsignarAsync(await CrearTrabajadorAsync("41300014"), aparatoAL, "14");
        await CargarAsync(aparatoAL, "14");
        await CargarAsync(aparatoMZ, "14");

        var pendientes = await importacion.NumerosSinAsignarAsync(Dia, Dia, CancellationToken.None);

        pendientes.Count.ShouldBe(1);
        pendientes[0].EquipoId.ShouldBe(aparatoMZ);
        pendientes[0].IdMarcador.ShouldBe("14");
        pendientes[0].Marcaciones.ShouldBe(2);

        var cobertura = await importacion.CoberturaAsync(Dia.Year, Dia.Month, CancellationToken.None);
        var delDia = cobertura.Dias.Single(d => d.Fecha == Dia);

        delDia.Marcaciones.ShouldBe(4);
        delDia.Trabajadores.ShouldBe(2);
    }

    [Fact]
    public async Task El_catalogo_de_marcadores_dice_quien_tiene_cada_numero()
    {
        var marcadores = new ServicioMarcadores(contexto);

        await marcadores.GuardarAsync(new GuardarMarcadorRequest { Codigo = "14", EquipoId = aparatoAL }, "test");
        await marcadores.GuardarAsync(new GuardarMarcadorRequest { Codigo = "15", EquipoId = aparatoAL }, "test");
        await AsignarAsync(await CrearTrabajadorAsync("41300014"), aparatoAL, "14");

        var lista = await marcadores.ListarAsync(new FiltroMarcadores(), CancellationToken.None);

        lista.Single(m => m.Codigo == "14").AsignadoA.ShouldBe("PRUEBA SQLITE, CONSULTA");
        lista.Single(m => m.Codigo == "15").AsignadoA.ShouldBeNull();
    }

    /// <summary>
    /// El motor busca las marcaciones por (aparato, número) sobre el índice único y las
    /// atribuye en memoria: cada persona recibe solo las de su aparato.
    /// </summary>
    [Fact]
    public async Task El_motor_atribuye_por_aparato_y_numero_al_recalcular()
    {
        var flores = await CrearTrabajadorAsync("41300014");
        var quispe = await CrearTrabajadorAsync("41300114");
        await AsignarAsync(flores, aparatoAL, "14");
        await AsignarAsync(quispe, aparatoMZ, "14");
        await AsignarTurnoDiurnoAsync(flores);
        await AsignarTurnoDiurnoAsync(quispe);

        // Solo el A-L trae archivo: QUISPE no marcó.
        await CargarAsync(aparatoAL, "14");

        await new ServicioCalculoTareo(contexto, new AlcanceSinRestriccion(), CoberturaDePrueba.TodoCargado(), NullLogger<ServicioCalculoTareo>.Instance)
            .RecalcularPeriodoAsync(1, "test", CancellationToken.None);

        var tareos = await contexto.TareosDiarios.AsNoTracking()
            .Where(t => t.Fecha == Dia)
            .ToDictionaryAsync(t => t.TrabajadorId, t => t.MinutosTrabajados);

        tareos[flores].ShouldBe(720);
        tareos[quispe].ShouldBe(0);
    }

    // ------------------------------------------------------------------

    private async Task AsignarTurnoDiurnoAsync(long trabajadorId)
    {
        await contexto.AsignacionesTurno.AddAsync(new AsignacionTurno
        {
            TrabajadorId = trabajadorId,
            TurnoId = 1,
            FechaInicio = new DateOnly(2026, 1, 1)
        }, CancellationToken.None);

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<long> CrearTrabajadorAsync(string documento)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "SQLITE",
            Nombres = "CONSULTA",
            Sexo = Sexo.Femenino,
            EmpresaId = EmpresaConsulting,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaDetergentes,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2026, 1, 1)
        };

        trabajador.AbrirVinculo("prueba");
        await contexto.Trabajadores.AddAsync(trabajador, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }

    private async Task AsignarAsync(long trabajadorId, long equipoId, string numero)
    {
        await contexto.AsignacionesMarcador.AddAsync(new AsignacionMarcador
        {
            TrabajadorId = trabajadorId,
            EquipoId = equipoId,
            IdMarcador = numero,
            FechaInicio = new DateOnly(2026, 1, 1)
        }, CancellationToken.None);

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private async Task CargarAsync(long equipoId, string numero)
    {
        var carga = new ImportacionMarcador
        {
            NombreArchivo = $"equipo-{equipoId}.txt",
            EquipoId = equipoId,
            HashContenido = new string((char)('a' + equipoId), 64),
            FechaCarga = new DateTime(2026, 7, 7, 9, 0, 0),
            CargadoPorUsuario = "test",
            FilasLeidas = 2,
            FilasInsertadas = 2
        };

        await contexto.Importaciones.AddAsync(carga, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        foreach (var hora in new[] { new TimeOnly(7, 0), new TimeOnly(19, 0) })
        {
            await contexto.MarcacionesCrudas.AddAsync(new MarcacionCruda
            {
                EquipoId = equipoId,
                IdMarcador = numero,
                FechaHora = Dia.ToDateTime(hora),
                ImportacionId = carga.Id
            }, CancellationToken.None);
        }

        await contexto.SaveChangesAsync(CancellationToken.None);
    }
}
