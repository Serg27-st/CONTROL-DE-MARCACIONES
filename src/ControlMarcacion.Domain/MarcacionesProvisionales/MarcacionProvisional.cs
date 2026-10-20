using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.MarcacionesProvisionales;

/// <summary>
/// Marcación leída del archivo en el módulo provisional. No guarda trabajador.
/// </summary>
/// <remarks>
/// Módulo temporal — eliminar junto con las tablas <c>prov_*</c> cuando el motor
/// principal cubra este flujo.
///
/// No tiene FK a <c>Trabajador</c>: el cruce se resuelve al consultar, por
/// <c>AsignacionMarcador</c> vigente en la fecha, igual que en el motor principal.
/// </remarks>
public class MarcacionProvisional : EntidadBase
{
    public long EquipoId { get; set; }

    public string IdMarcador { get; set; } = null!;

    public DateTime FechaHora { get; set; }

    public string? ModoVerificacion { get; set; }

    public long ImportacionProvisionalId { get; set; }

    public string? LineaOriginal { get; set; }
}
