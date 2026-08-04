using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Organizacion;

/// <summary>
/// Sección dentro de un área. Es el nivel más fino de la jerarquía organizativa
/// y una de las columnas del tareo final.
/// </summary>
public class Seccion : EntidadBase
{
    public long AreaId { get; set; }

    public Area? Area { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;
}
