namespace ControlMarcacion.Domain.Comun;

/// <summary>
/// Base de toda entidad de negocio: identidad y auditoría.
/// </summary>
/// <remarks>
/// En este sistema <b>nada se borra físicamente</b>. Un trabajador que cesa lleva
/// fecha de cese; un catálogo que ya no se usa se marca como inactivo. Las marcaciones
/// y los tareos históricos deben seguir siendo consultables indefinidamente porque
/// sustentan planillas ya pagadas.
/// </remarks>
public abstract class EntidadBase
{
    public long Id { get; set; }

    public DateTime CreadoEn { get; set; }

    public string? CreadoPor { get; set; }

    public DateTime? ModificadoEn { get; set; }

    public string? ModificadoPor { get; set; }

    /// <summary>
    /// Baja lógica. Nunca uses DELETE sobre estas entidades.
    /// </summary>
    public bool Activo { get; set; } = true;
}
