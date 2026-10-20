using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Catalogos;

/// <summary>
/// La gracia de salida viaja completa por el catálogo de turnos: se guarda, se lee y los
/// turnos sembrados nacen en 0.
/// </summary>
/// <remarks>
/// Va sobre SQLite y no sobre InMemory para que la columna exista de verdad en el esquema.
/// El riesgo que cubre es el silencioso: un campo que la pantalla muestra pero que el
/// servicio no copia a la entidad se pierde al guardar, y el turno sigue calculando con 0.
/// </remarks>
public sealed class GraciaDeSalidaEnTurnosTests : IDisposable
{
    private const long Nocturno = 2;

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioTurnos servicio;

    public GraciaDeSalidaEnTurnosTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        servicio = new ServicioTurnos(contexto);
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task Los_turnos_sembrados_nacen_sin_gracia_de_salida()
    {
        var turnos = await contexto.Turnos.AsNoTracking().ToListAsync(CancellationToken.None);

        turnos.ShouldNotBeEmpty();
        turnos.ShouldAllBe(t => t.MinutosGraciaSalida == 0);
    }

    [Fact]
    public async Task La_gracia_de_salida_se_guarda_y_se_lee()
    {
        var actual = await servicio.ObtenerAsync(Nocturno, CancellationToken.None);
        actual.ShouldNotBeNull();

        await servicio.GuardarAsync(
            new GuardarTurnoRequest
            {
                Id = actual.Id,
                Codigo = actual.Codigo,
                Nombre = actual.Nombre,
                HoraInicio = actual.HoraInicio,
                HoraFin = actual.HoraFin,
                MinutosRefrigerio = actual.MinutosRefrigerio,
                MinutosTolerancia = actual.MinutosTolerancia,
                MinutosAnticipacionEntrada = actual.MinutosAnticipacionEntrada,
                MinutosAnticipacionSalida = actual.MinutosAnticipacionSalida,
                MinutosGraciaSalida = 15,
                MinutosJornadaOrdinaria = actual.MinutosJornadaOrdinaria,
                DiasAplicables = actual.DiasAplicables,
                PlantasHabilitadas = actual.PlantasHabilitadasIds,
                Activo = actual.Activo
            },
            "coordinador",
            CancellationToken.None);

        var guardado = await servicio.ObtenerAsync(Nocturno, CancellationToken.None);

        guardado!.MinutosGraciaSalida.ShouldBe(15);
        guardado.LimiteGraciaSalida.ShouldBe(new TimeOnly(7, 15));
    }
}
