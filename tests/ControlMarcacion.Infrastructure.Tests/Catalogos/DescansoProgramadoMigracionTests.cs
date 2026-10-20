using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Catalogos;

#pragma warning disable CA1001 // xUnit v2 libera los recursos mediante IAsyncLifetime.
public sealed class DescansoProgramadoMigracionTests : IAsyncLifetime
{
    private const string MigracionAnterior = "20261001212252_MarcacionesProvisionales";
    private readonly SqliteConnection conexion = new("DataSource=:memory:");
    private MarcacionDbContext contexto = null!;

    public async Task InitializeAsync()
    {
        await conexion.OpenAsync();
        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        await contexto.GetService<IMigrator>().MigrateAsync(MigracionAnterior);

        await contexto.Database.ExecuteSqlRawAsync(
            "INSERT INTO cuadrilla " +
            "(id,codigo,nombre,planta_id,creado_en,activo) " +
            "VALUES (9001,'MIG','Migración',1,'2026-10-06 00:00:00',1)");
        await contexto.Database.ExecuteSqlRawAsync(
            "INSERT INTO programacion_cuadrilla " +
            "(id,cuadrilla_id,fecha_inicio,fecha_fin,fecha_ancla,creado_en,activo) " +
            "VALUES (9001,9001,'2026-07-01','2026-07-01','2026-07-01','2026-10-06 00:00:00',1)");
        await contexto.Database.ExecuteSqlRawAsync(
            "INSERT INTO tramo_rotacion " +
            "(id,programacion_id,orden,turno_id,dias,creado_en,activo) " +
            "VALUES (9001,9001,1,1,1,'2026-10-06 00:00:00',1)");
    }

    public async Task DisposeAsync()
    {
        await contexto.DisposeAsync();
        await conexion.DisposeAsync();
    }

    [Fact]
    public async Task MIG_ROT_DESC_01_preserva_tramos_y_los_clasifica_como_trabajo()
    {
        await contexto.GetService<IMigrator>().MigrateAsync();

        (await EscalarAsync<string>(
            "SELECT tipo FROM tramo_rotacion WHERE id=9001")).ShouldBe("Trabajo");
        (await EscalarAsync<long>(
            "SELECT turno_id FROM tramo_rotacion WHERE id=9001")).ShouldBe(1L);
        (await EscalarAsync<string>("PRAGMA integrity_check")).ShouldBe("ok");
    }

    [Fact]
    public async Task MIG_ROT_DESC_02_los_CHECK_rechazan_combinaciones_invalidas()
    {
        await contexto.GetService<IMigrator>().MigrateAsync();

        await DebeFallarAsync(
            "INSERT INTO tramo_rotacion " +
            "(programacion_id,orden,tipo,turno_id,dias,creado_en,activo) " +
            "VALUES (9001,2,'Trabajo',NULL,1,'2026-10-06 00:00:00',1)");
        await DebeFallarAsync(
            "INSERT INTO tramo_rotacion " +
            "(programacion_id,orden,tipo,turno_id,dias,creado_en,activo) " +
            "VALUES (9001,2,'Descanso',1,1,'2026-10-06 00:00:00',1)");
        await DebeFallarAsync(
            "INSERT INTO tramo_rotacion " +
            "(programacion_id,orden,tipo,turno_id,dias,creado_en,activo) " +
            "VALUES (9001,2,'Descanso',NULL,2,'2026-10-06 00:00:00',1)");
    }

    [Fact]
    public async Task MIG_ROT_DESC_03_down_restaura_el_esquema_si_no_hay_descansos_nuevos()
    {
        var migrador = contexto.GetService<IMigrator>();
        await migrador.MigrateAsync();
        await migrador.MigrateAsync(MigracionAnterior);

        (await EscalarAsync<long>(
            "SELECT COUNT(*) FROM pragma_table_info('tramo_rotacion') WHERE name='tipo'"))
            .ShouldBe(0L);
        (await EscalarAsync<long>(
            "SELECT \"notnull\" FROM pragma_table_info('tramo_rotacion') WHERE name='turno_id'"))
            .ShouldBe(1L);
        (await EscalarAsync<string>("PRAGMA integrity_check")).ShouldBe("ok");
    }

    private async Task DebeFallarAsync(string sql) =>
        await Should.ThrowAsync<SqliteException>(() => contexto.Database.ExecuteSqlRawAsync(sql));

    private async Task<T> EscalarAsync<T>(string sql)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return (T)(await comando.ExecuteScalarAsync())!;
    }
}
#pragma warning restore CA1001
