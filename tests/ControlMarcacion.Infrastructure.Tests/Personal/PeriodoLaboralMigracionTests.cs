using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Personal;

#pragma warning disable CA1001 // xUnit v2 libera los recursos mediante IAsyncLifetime.
public sealed class PeriodoLaboralMigracionTests : IAsyncLifetime
{
    private const string MigracionAnterior = "20261007033130_DescansoProgramadoEnRotaciones";
    private readonly SqliteConnection conexion = new("DataSource=:memory:");
    private MarcacionDbContext contexto = null!;

    public async Task InitializeAsync()
    {
        await conexion.OpenAsync();
        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        await contexto.GetService<IMigrator>().MigrateAsync(MigracionAnterior);
    }

    public async Task DisposeAsync()
    {
        await contexto.DisposeAsync();
        await conexion.DisposeAsync();
    }

    [Fact]
    public async Task MIG_PL_01_up_crea_asignacion_planta_sin_inventar_historicos()
    {
        await contexto.GetService<IMigrator>().MigrateAsync();

        (await EscalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='asignacion_planta'"))
            .ShouldBe(1L);
        (await EscalarAsync<long>("SELECT COUNT(*) FROM asignacion_planta")).ShouldBe(0L);
        (await EscalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' " +
            "AND name='uq_periodo_laboral_abierto'"))
            .ShouldBe(1L);
        (await EscalarAsync<string>("PRAGMA integrity_check")).ShouldBe("ok");
    }

    [Fact]
    public async Task MIG_PL_02_down_restaura_esquema_anterior_sin_reconstruir_periodo()
    {
        var migrador = contexto.GetService<IMigrator>();
        await migrador.MigrateAsync();
        await migrador.MigrateAsync(MigracionAnterior);

        (await EscalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='asignacion_planta'"))
            .ShouldBe(0L);
        (await EscalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' " +
            "AND name='uq_periodo_laboral_abierto'"))
            .ShouldBe(0L);
        (await EscalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='periodo_laboral'"))
            .ShouldBe(1L);
        (await EscalarAsync<string>("PRAGMA integrity_check")).ShouldBe("ok");
    }

    private async Task<T> EscalarAsync<T>(string sql)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return (T)(await comando.ExecuteScalarAsync())!;
    }
}
#pragma warning restore CA1001
