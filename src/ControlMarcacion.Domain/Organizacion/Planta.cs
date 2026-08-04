using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Organizacion;

/// <summary>
/// Planta de operación. Consulting Advisers: Detergentes y 20 mil.
/// CA Outsourcing: Planta 01 y Sulfonación.
/// </summary>
/// <remarks>
/// La planta condiciona qué turnos aplican: el turno especial de sábado
/// (07:00–15:00) solo existe en Planta 01. Esa restricción se modela como relación
/// turno ↔ plantas habilitadas en el catálogo, nunca como una condición en el código.
/// </remarks>
public class Planta : EntidadBase
{
    public long EmpresaId { get; set; }

    public Empresa? Empresa { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public ICollection<Area> Areas { get; set; } = [];
}
