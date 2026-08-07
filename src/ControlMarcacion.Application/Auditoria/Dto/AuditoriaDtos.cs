using ControlMarcacion.Domain.Auditoria;

namespace ControlMarcacion.Application.Auditoria.Dto;

/// <summary>
/// Un cambio concreto sobre un campo, tal como se guardó en el detalle JSON.
/// </summary>
public sealed record CambioAuditadoDto(string Campo, string? Anterior, string? Nuevo);

/// <summary>
/// Fila de la pantalla de auditoría.
/// </summary>
public sealed record RegistroAuditoriaDto(
    long Id,
    string Tabla,
    string ClaveRegistro,
    OperacionAuditada Operacion,
    string Usuario,
    DateTime FechaHora,
    IReadOnlyList<CambioAuditadoDto> Cambios)
{
    public string OperacionLegible => Operacion switch
    {
        OperacionAuditada.Insercion => "Alta",
        OperacionAuditada.Modificacion => "Modificación",
        OperacionAuditada.BajaLogica => "Baja lógica",
        _ => Operacion.ToString()
    };
}

/// <summary>
/// Filtros de la bitácora.
/// </summary>
public sealed record FiltroAuditoria
{
    /// <summary>Nombre de tabla en snake_case. Nulo trae todas.</summary>
    public string? Tabla { get; init; }

    /// <summary>Clave del registro afectado. Solo tiene sentido junto con <see cref="Tabla"/>.</summary>
    public string? ClaveRegistro { get; init; }

    public string? Usuario { get; init; }

    public OperacionAuditada? Operacion { get; init; }

    public DateOnly? Desde { get; init; }

    public DateOnly? Hasta { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamanioPagina { get; init; } = 50;
}
