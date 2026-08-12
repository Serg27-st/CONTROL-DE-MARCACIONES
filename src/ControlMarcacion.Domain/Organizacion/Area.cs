using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Organizacion;

/// <summary>
/// Área dentro de una planta. Nivel intermedio entre planta y sección.
/// </summary>
public class Area : EntidadBase
{
    public long PlantaId { get; set; }

    public Planta? Planta { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public ICollection<Seccion> Secciones { get; set; } = [];
}
