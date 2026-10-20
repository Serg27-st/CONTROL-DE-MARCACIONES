using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.MarcacionesProvisionales;

/// <summary>
/// Fila rechazada en una importación provisional.
/// </summary>
/// <remarks>
/// Módulo temporal — eliminar junto con las tablas <c>prov_*</c> cuando el motor
/// principal cubra este flujo.
/// </remarks>
public class ErrorImportacionProvisional : EntidadBase
{
    public long ImportacionProvisionalId { get; set; }

    public int NumeroLinea { get; set; }

    public string? ContenidoLinea { get; set; }

    public string Motivo { get; set; } = null!;
}
