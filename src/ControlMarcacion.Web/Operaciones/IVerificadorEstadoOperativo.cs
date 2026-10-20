using System.Data;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ControlMarcacion.Web.Operaciones;

public interface IVerificadorEstadoOperativo
{
    Task<ResultadoEstadoOperativo> VerificarAsync(CancellationToken cancelacion);
}

public enum EstadoOperativo
{
    Starting,
    SetupRequired,
    Ready,
    Failed
}

public readonly record struct ResultadoEstadoOperativo(EstadoOperativo Estado, string Codigo)
{
    public bool Listo => Estado == EstadoOperativo.Ready;

    public bool RequiereConfiguracion => Estado == EstadoOperativo.SetupRequired;

    public static ResultadoEstadoOperativo Iniciando() =>
        new(EstadoOperativo.Starting, "INICIANDO");

    public static ResultadoEstadoOperativo ConfiguracionInicialRequerida() =>
        new(EstadoOperativo.SetupRequired, "CONFIGURACION_INICIAL_REQUERIDA");

    public static ResultadoEstadoOperativo Preparado() =>
        new(EstadoOperativo.Ready, "LISTO");

    public static ResultadoEstadoOperativo NoPreparado(string codigo) =>
        new(EstadoOperativo.Failed, codigo);
}

/// <summary>
/// Verificación read-only del esquema que necesita el binario actual.
/// </summary>
/// <remarks>
/// No intenta migrar ni reparar. Así una consulta de health nunca cambia datos y la recuperación
/// solo se anuncia cuando una intervención externa dejó la base realmente compatible.
/// </remarks>
internal sealed class VerificadorEstadoOperativo(MarcacionDbContext contexto)
    : IVerificadorEstadoOperativo
{
    public async Task<ResultadoEstadoOperativo> VerificarAsync(CancellationToken cancelacion)
    {
        try
        {
            if (!await contexto.Database.CanConnectAsync(cancelacion).ConfigureAwait(false))
            {
                return ResultadoEstadoOperativo.NoPreparado("BASE_NO_DISPONIBLE");
            }

            var esperadas = contexto.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
            var aplicadas = (await contexto.Database
                    .GetAppliedMigrationsAsync(cancelacion)
                    .ConfigureAwait(false))
                .ToHashSet(StringComparer.Ordinal);

            if (aplicadas.Except(esperadas).Any())
            {
                return ResultadoEstadoOperativo.NoPreparado("ESQUEMA_MAS_NUEVO_QUE_BINARIO");
            }

            if (esperadas.Except(aplicadas).Any())
            {
                return ResultadoEstadoOperativo.NoPreparado("MIGRACIONES_PENDIENTES");
            }

            if (!await IntegridadEsCorrectaAsync(cancelacion).ConfigureAwait(false)
                || !await EstructuraEsCompatibleAsync(cancelacion).ConfigureAwait(false))
            {
                return ResultadoEstadoOperativo.NoPreparado("ESQUEMA_INCOMPATIBLE");
            }

            if (!await contexto.Set<ApplicationUser>()
                    .AsNoTracking()
                    .AnyAsync(cancelacion)
                    .ConfigureAwait(false))
            {
                return ResultadoEstadoOperativo.ConfiguracionInicialRequerida();
            }

            return ResultadoEstadoOperativo.Preparado();
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return ResultadoEstadoOperativo.NoPreparado("VERIFICACION_FALLIDA");
        }
    }

    private async Task<bool> IntegridadEsCorrectaAsync(CancellationToken cancelacion)
    {
        var conexion = contexto.Database.GetDbConnection();
        var cerrar = conexion.State != ConnectionState.Open;

        if (cerrar)
        {
            await conexion.OpenAsync(cancelacion).ConfigureAwait(false);
        }

        try
        {
            await using (var comando = conexion.CreateCommand())
            {
                comando.CommandText = "PRAGMA quick_check;";
                var resultado = await comando.ExecuteScalarAsync(cancelacion).ConfigureAwait(false);

                if (!string.Equals(resultado?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            await using (var comando = conexion.CreateCommand())
            {
                comando.CommandText = "PRAGMA foreign_key_check;";
                await using var lector = await comando.ExecuteReaderAsync(cancelacion).ConfigureAwait(false);
                return !await lector.ReadAsync(cancelacion).ConfigureAwait(false);
            }
        }
        finally
        {
            if (cerrar)
            {
                await conexion.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> EstructuraEsCompatibleAsync(CancellationToken cancelacion)
    {
        var tablas = contexto.Model
            .GetEntityTypes()
            .Where(tipo => tipo.GetTableName() is not null)
            .GroupBy(
                tipo => new { Tabla = tipo.GetTableName()!, Esquema = tipo.GetSchema() },
                tipo => tipo)
            .ToList();

        var conexion = contexto.Database.GetDbConnection();
        var cerrar = conexion.State != ConnectionState.Open;

        if (cerrar)
        {
            await conexion.OpenAsync(cancelacion).ConfigureAwait(false);
        }

        try
        {
            foreach (var tabla in tablas)
            {
                var objeto = StoreObjectIdentifier.Table(tabla.Key.Tabla, tabla.Key.Esquema);
                var columnasEsperadas = tabla
                    .SelectMany(tipo => tipo.GetProperties())
                    .Select(propiedad => propiedad.GetColumnName(objeto))
                    .Where(nombre => nombre is not null)
                    .Cast<string>()
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var columnasExistentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using var comando = conexion.CreateCommand();
                comando.CommandText = "SELECT name FROM pragma_table_info($tabla);";
                var parametro = comando.CreateParameter();
                parametro.ParameterName = "$tabla";
                parametro.Value = tabla.Key.Tabla;
                comando.Parameters.Add(parametro);

                await using var lector = await comando.ExecuteReaderAsync(cancelacion).ConfigureAwait(false);

                while (await lector.ReadAsync(cancelacion).ConfigureAwait(false))
                {
                    columnasExistentes.Add(lector.GetString(0));
                }

                if (columnasExistentes.Count == 0 || columnasEsperadas.Except(columnasExistentes).Any())
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            if (cerrar)
            {
                await conexion.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}
