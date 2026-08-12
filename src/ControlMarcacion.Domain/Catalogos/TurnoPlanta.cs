using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Organizacion;

namespace ControlMarcacion.Domain.Catalogos;

/// <summary>
/// Habilita un turno en una planta concreta.
/// </summary>
/// <remarks>
/// Existe para que la regla "el turno especial de sábado solo aplica a Planta 01"
/// sea un dato del catálogo y no un <c>if</c> en el código. Cuando mañana ese turno
/// se extienda a otra planta, será un INSERT y no un despliegue.
/// </remarks>
public class TurnoPlanta : EntidadBase
{
    public long TurnoId { get; set; }

    public Turno? Turno { get; set; }

    public long PlantaId { get; set; }

    public Planta? Planta { get; set; }
}
