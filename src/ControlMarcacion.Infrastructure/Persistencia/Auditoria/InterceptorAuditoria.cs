using System.Globalization;
using System.Text.Json;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ControlMarcacion.Infrastructure.Persistencia.Auditoria;

/// <summary>
/// Escribe la bitácora de auditoría en cada guardado.
/// </summary>
/// <remarks>
/// Va en un interceptor de <c>SaveChanges</c> y no en cada servicio a propósito. Si
/// auditar fuera responsabilidad de quien escribe, auditaría solo quien se acuerde, y el
/// día que alguien agregue un caso de uso nuevo sin la llamada nadie lo notaría — que es
/// justamente el momento en que una auditoría hace falta.
///
/// <b>Dos fases.</b> Los cambios se leen en <c>SavingChanges</c>, cuando el
/// <c>ChangeTracker</c> todavía conserva los valores originales, pero las filas se
/// escriben en <c>SavedChanges</c>, cuando las inserciones ya tienen su clave asignada
/// por la base. Leerlo todo al final daría claves; leerlo todo al principio daría
/// valores anteriores; hacen falta las dos cosas.
///
/// <b>Ley 29733.</b> Los campos de dato personal se registran por nombre pero nunca por
/// valor: la bitácora dice que el domicilio de alguien cambió y quién lo cambió, no cuál
/// era. Lo mismo con los secretos de Identity. Ver <see cref="CamposSinValor"/>.
/// </remarks>
public sealed class InterceptorAuditoria(IProveedorUsuarioActual proveedorUsuario) : SaveChangesInterceptor
{
    /// <summary>
    /// Tablas que no se auditan.
    /// </summary>
    /// <remarks>
    /// La lista es corta y explícita a propósito: todo lo que no esté aquí se audita, así
    /// que agregar una entidad nueva al modelo la deja auditada sin tener que acordarse
    /// de nada.
    ///
    /// <list type="bullet">
    /// <item><see cref="RegistroAuditoria"/>: se auditaría a sí misma, sin fin.</item>
    /// <item><c>MarcacionCruda</c> y <c>ErrorImportacion</c>: son inmutables y solo se
    /// insertan, miles de filas por archivo. Auditar cada inserción duplicaría la tabla
    /// para decir lo que la tabla ya dice; quién cargó qué archivo y cuándo ya queda en
    /// <c>ImportacionMarcador</c>, que sí se audita.</item>
    /// <item><c>TareoDiario</c>: es dato derivado y reconstruible — recalcular un periodo
    /// reescribe miles de filas y el resultado es idéntico por definición (invariante 2).
    /// Además cada tareo ya guarda su propia observación explicando cómo se calculó
    /// (invariante 8). Lo que sí se audita es la apertura y el cierre del periodo, que es
    /// la decisión humana detrás del recálculo.</item>
    /// </list>
    /// </remarks>
    private static readonly HashSet<string> TablasExcluidas = new(StringComparer.Ordinal)
    {
        nameof(RegistroAuditoria),
        "MarcacionCruda",
        "ErrorImportacion",
        "TareoDiario"
    };

    /// <summary>
    /// Campos cuyo nombre se registra pero cuyo valor jamás: datos personales protegidos
    /// por la Ley 29733 y secretos de autenticación.
    /// </summary>
    private static readonly HashSet<string> CamposSinValor = new(StringComparer.OrdinalIgnoreCase)
    {
        // Datos personales del trabajador y del usuario.
        "NumeroDocumento",
        "Direccion",
        "Telefono",
        "CorreoElectronico",
        "Email",
        "NormalizedEmail",
        "PhoneNumber",

        // Secretos de ASP.NET Core Identity.
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "TwoFactorRecoveryCodes"
    };

    private const string ValorProtegido = "(protegido)";

    /// <summary>
    /// Campos de auditoría de <c>EntidadBase</c>. Registrarlos como cambios sería ruido:
    /// <c>ModificadoEn</c> cambia en toda modificación por definición, y esta bitácora ya
    /// guarda su propia fecha y su propio usuario.
    /// </summary>
    private static readonly HashSet<string> CamposDeRuido = new(StringComparer.Ordinal)
    {
        "CreadoEn",
        "CreadoPor",
        "ModificadoEn",
        "ModificadoPor"
    };

    private readonly List<CambioPendiente> _pendientes = [];

    /// <summary>
    /// Evita que el guardado de la propia bitácora vuelva a entrar a recolectar.
    /// </summary>
    private bool _escribiendoBitacora;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Recolectar(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Recolectar(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } contexto && MaterializarPendientes(contexto))
        {
            _escribiendoBitacora = true;

            try
            {
                await contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _escribiendoBitacora = false;
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } contexto && MaterializarPendientes(contexto))
        {
            _escribiendoBitacora = true;

            try
            {
                contexto.SaveChanges();
            }
            finally
            {
                _escribiendoBitacora = false;
            }
        }

        return base.SavedChanges(eventData, result);
    }

    /// <summary>
    /// Si el guardado falló, lo recolectado no ocurrió: se descarta. Sin esto, el
    /// siguiente guardado de este mismo contexto escribiría en la bitácora cambios que
    /// la base rechazó.
    /// </summary>
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pendientes.Clear();

        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pendientes.Clear();

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    // ------------------------------------------------------------------

    private void Recolectar(DbContext? contexto)
    {
        if (contexto is null || _escribiendoBitacora)
        {
            return;
        }

        _pendientes.Clear();

        // ToList() y no la secuencia perezosa: materializar las filas de la bitácora en
        // SavedChanges modifica el propio ChangeTracker que estaríamos recorriendo.
        foreach (var entrada in contexto.ChangeTracker.Entries().ToList())
        {
            if (entrada.Metadata.ClrType == typeof(RegistroAuditoria))
            {
                continue;
            }

            var tabla = entrada.Metadata.GetTableName();

            if (tabla is null || TablasExcluidas.Contains(entrada.Metadata.ClrType.Name))
            {
                continue;
            }

            var pendiente = entrada.State switch
            {
                EntityState.Added => DeInsercion(entrada, tabla),
                EntityState.Modified => DeModificacion(entrada, tabla),
                EntityState.Deleted => DeEliminacion(entrada, tabla),
                _ => null
            };

            if (pendiente is not null)
            {
                _pendientes.Add(pendiente);
            }
        }
    }

    private static CambioPendiente DeInsercion(EntityEntry entrada, string tabla)
    {
        var cambios = entrada.Properties
            .Where(p => !CamposDeRuido.Contains(p.Metadata.Name))
            .Where(p => p.CurrentValue is not null)
            .Select(p => new CambioAuditado(p.Metadata.Name, null, Describir(p.Metadata.Name, p.CurrentValue)))
            .ToList();

        return new CambioPendiente(entrada, tabla, OperacionAuditada.Insercion, cambios);
    }

    private static CambioPendiente? DeModificacion(EntityEntry entrada, string tabla)
    {
        var cambios = entrada.Properties
            .Where(p => p.IsModified)
            .Where(p => !CamposDeRuido.Contains(p.Metadata.Name))
            .Where(p => !Equals(p.OriginalValue, p.CurrentValue))
            .Select(p => new CambioAuditado(
                p.Metadata.Name,
                Describir(p.Metadata.Name, p.OriginalValue),
                Describir(p.Metadata.Name, p.CurrentValue)))
            .ToList();

        // EF marca como Modified toda entidad pasada por Update(), aunque no haya cambiado
        // ni un campo. Una fila de bitácora sin cambios no informa nada.
        if (cambios.Count == 0)
        {
            return null;
        }

        var esBaja = cambios.Any(c =>
            string.Equals(c.Campo, "Activo", StringComparison.Ordinal)
            && string.Equals(c.Nuevo, "False", StringComparison.OrdinalIgnoreCase));

        var operacion = esBaja ? OperacionAuditada.BajaLogica : OperacionAuditada.Modificacion;

        return new CambioPendiente(entrada, tabla, operacion, cambios);
    }

    private static CambioPendiente DeEliminacion(EntityEntry entrada, string tabla)
    {
        var cambios = entrada.Properties
            .Where(p => !CamposDeRuido.Contains(p.Metadata.Name))
            .Where(p => p.OriginalValue is not null)
            .Select(p => new CambioAuditado(p.Metadata.Name, Describir(p.Metadata.Name, p.OriginalValue), null))
            .ToList();

        return new CambioPendiente(entrada, tabla, OperacionAuditada.Eliminacion, cambios);
    }

    /// <summary>
    /// Convierte lo recolectado en filas de bitácora. Devuelve si hay algo que guardar.
    /// </summary>
    private bool MaterializarPendientes(DbContext contexto)
    {
        if (_pendientes.Count == 0)
        {
            return false;
        }

        var ahora = DateTime.Now;
        var usuario = proveedorUsuario.Usuario;
        var registros = new List<RegistroAuditoria>(_pendientes.Count);

        foreach (var pendiente in _pendientes)
        {
            registros.Add(new RegistroAuditoria
            {
                Tabla = pendiente.Tabla,
                ClaveRegistro = ResolverClave(pendiente.Entrada),
                Operacion = pendiente.Operacion,
                Usuario = usuario,
                FechaHora = ahora,
                Detalle = JsonSerializer.Serialize(pendiente.Cambios)
            });
        }

        _pendientes.Clear();

        contexto.Set<RegistroAuditoria>().AddRange(registros);

        return true;
    }

    /// <summary>
    /// Clave primaria del registro como texto. Compuesta se une con "|": las tablas de
    /// relación de Identity no tienen una sola columna clave.
    /// </summary>
    private static string ResolverClave(EntityEntry entrada)
    {
        var clave = entrada.Metadata.FindPrimaryKey();

        if (clave is null)
        {
            return "(sin clave)";
        }

        var valores = clave.Properties
            .Select(p => Convert.ToString(entrada.Property(p.Name).CurrentValue, CultureInfo.InvariantCulture))
            .Select(v => string.IsNullOrEmpty(v) ? "?" : v);

        return string.Join('|', valores);
    }

    private static string? Describir(string campo, object? valor)
    {
        if (CamposSinValor.Contains(campo))
        {
            return ValorProtegido;
        }

        return valor is null
            ? null
            : Convert.ToString(valor, CultureInfo.InvariantCulture);
    }

    /// <summary>Cambio recolectado a la espera de que la inserción reciba su clave.</summary>
    private sealed record CambioPendiente(
        EntityEntry Entrada,
        string Tabla,
        OperacionAuditada Operacion,
        IReadOnlyList<CambioAuditado> Cambios);

    /// <summary>Forma del JSON que se guarda en <see cref="RegistroAuditoria.Detalle"/>.</summary>
    private sealed record CambioAuditado(string Campo, string? Anterior, string? Nuevo);
}
