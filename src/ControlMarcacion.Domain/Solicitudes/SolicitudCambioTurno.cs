using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Solicitudes;

/// <summary>
/// Petición auditable para sustituir el turno de un trabajador durante un único día.
/// La aprobación materializa la excepción en la historia de AsignacionTurno; nunca altera
/// marcaciones biométricas.
/// </summary>
public sealed class SolicitudCambioTurno : EntidadBase
{
    public long TrabajadorId { get; set; }

    public DateOnly FechaEfectiva { get; set; }

    public long TurnoSolicitadoId { get; set; }

    public Turno? TurnoSolicitado { get; set; }

    public long? TurnoAnteriorId { get; set; }

    public long? CuadrillaAnteriorId { get; set; }

    public string Motivo { get; set; } = null!;

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    public string SolicitadoPorUsuario { get; set; } = null!;

    public DateTime SolicitadoEn { get; set; }

    public string? ResueltoPorUsuario { get; set; }

    public DateTime? ResueltoEn { get; set; }

    public string? MotivoResolucion { get; set; }

    public int Version { get; set; }
}
