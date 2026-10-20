using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.MarcacionesProvisionales;
using ControlMarcacion.Infrastructure.MarcacionesProvisionales;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace ControlMarcacion.Infrastructure.Tests.Quality;

/// <summary>
/// Baseline sintético, no SLA. Imprime tiempo, memoria aproximada, filas y tamaño del
/// resultado para que Fase 5 tenga mediciones reproducibles sin tocar la base canónica.
/// </summary>
public sealed class SyntheticPerformanceBaselineTests(ITestOutputHelper salida)
{
    [Theory]
    [InlineData(1_000)]
    [InlineData(20_000)]
    public async Task PERF_PMV_importacion_sintetica(int filas)
    {
        await using var entorno = new EntornoTemporal();
        var fuente = Substitute.For<IFuenteDeMarcaciones>();
        var inicio = new DateTime(2026, 1, 1, 0, 0, 0);
        var marcas = Enumerable.Range(0, filas)
            .Select(i => new MarcacionLeida(
                (i % 250).ToString(CultureInfo.InvariantCulture),
                inicio.AddSeconds(i), null, null, i + 1, $"L{i + 1}"))
            .ToList();
        fuente.Leer(Arg.Any<Stream>()).Returns(new ResultadoLectura(marcas, []));

        var servicio = new ServicioImportacionProvisional(entorno.Contexto, fuente, new AlcanceSinRestriccion());
        await using var contenido = new MemoryStream(Encoding.UTF8.GetBytes($"synthetic-{filas}"));
        GC.Collect();
        var memoriaAntes = GC.GetTotalMemory(true);
        var reloj = Stopwatch.StartNew();

        var resultado = await servicio.ImportarAsync(
            contenido, $"synthetic-{filas}.dat", entorno.EquipoId, "fase5");

        reloj.Stop();
        var memoriaDelta = GC.GetTotalMemory(false) - memoriaAntes;
        resultado.FilasInsertadas.ShouldBe(filas);
        salida.WriteLine(
            $"SINTETICO importacion PMV filas={filas}; ms={reloj.Elapsed.TotalMilliseconds:F2}; " +
            $"memoria_delta_bytes={memoriaDelta}; filas_salida={resultado.FilasInsertadas}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(31)]
    [InlineData(40)]
    [InlineData(41)]
    public async Task PERF_PMV_consulta_sintetica(int dias)
    {
        await using var entorno = new EntornoTemporal();
        var desde = new DateOnly(2026, 1, 1);
        var importacion = new ImportacionProvisional
        {
            EquipoId = entorno.EquipoId,
            NombreArchivo = $"query-{dias}.dat",
            HashContenido = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            FechaCarga = new DateTime(2026, 2, 15, 12, 0, 0),
            CargadoPorUsuario = "fase5",
            FilasLeidas = dias * 2,
            FilasInsertadas = dias * 2,
            CreadoPor = "fase5"
        };
        entorno.Contexto.ImportacionesProvisionales.Add(importacion);
        await entorno.Contexto.SaveChangesAsync();

        var marcas = new List<MarcacionProvisional>(dias * 2);
        for (var i = 0; i < dias; i++)
        {
            var fecha = desde.AddDays(i);
            marcas.Add(NuevaMarca(entorno.EquipoId, importacion.Id, fecha, new TimeOnly(7, 0)));
            marcas.Add(NuevaMarca(entorno.EquipoId, importacion.Id, fecha, new TimeOnly(17, 22)));
        }
        entorno.Contexto.MarcacionesProvisionales.AddRange(marcas);
        await entorno.Contexto.SaveChangesAsync();
        entorno.Contexto.ChangeTracker.Clear();

        var servicio = new ServicioConsultaMarcacionProvisional(entorno.Contexto, new AlcanceSinRestriccion());
        GC.Collect();
        var memoriaAntes = GC.GetTotalMemory(true);
        var reloj = Stopwatch.StartNew();
        var resultado = await servicio.ConsultarAsync(
            entorno.EquipoId, desde, desde.AddDays(dias - 1));
        reloj.Stop();
        var memoriaDelta = GC.GetTotalMemory(false) - memoriaAntes;
        var bytesResultado = JsonSerializer.SerializeToUtf8Bytes(resultado).Length;

        resultado.TotalDias.ShouldBe(dias);
        resultado.Jornadas.Count.ShouldBe(dias);
        resultado.AdvertenciaRangoExcesivo.ShouldBe(dias > 30);
        resultado.RangoExcedeMaximoExportacion.ShouldBe(dias > 40);
        salida.WriteLine(
            $"SINTETICO consulta PMV dias={dias}; marcas={dias * 2}; jornadas={resultado.Jornadas.Count}; " +
            $"ms={reloj.Elapsed.TotalMilliseconds:F2}; memoria_delta_bytes={memoriaDelta}; " +
            $"resultado_json_bytes={bytesResultado}");
    }

    private static MarcacionProvisional NuevaMarca(
        long equipoId, long importacionId, DateOnly fecha, TimeOnly hora) => new()
    {
        EquipoId = equipoId,
        IdMarcador = "sin-asignar",
        FechaHora = fecha.ToDateTime(hora),
        ImportacionProvisionalId = importacionId,
        CreadoPor = "fase5"
    };

    private sealed class EntornoTemporal : IAsyncDisposable
    {
        private readonly SqliteConnection conexion;

        public EntornoTemporal()
        {
            conexion = new SqliteConnection("DataSource=:memory:");
            conexion.Open();
            Contexto = new MarcacionDbContext(
                new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
            Contexto.Database.EnsureCreated();
            var plantaId = Contexto.Plantas.First().Id;
            var equipo = new EquipoMarcador
            {
                Codigo = $"PERF-{Guid.NewGuid():N}"[..20],
                Nombre = "Equipo sintético Fase 5",
                PlantaId = plantaId,
                Activo = true
            };
            Contexto.EquiposMarcador.Add(equipo);
            Contexto.SaveChanges();
            EquipoId = equipo.Id;
        }

        public MarcacionDbContext Contexto { get; }
        public long EquipoId { get; }

        public async ValueTask DisposeAsync()
        {
            await Contexto.DisposeAsync();
            await conexion.DisposeAsync();
        }
    }
}
