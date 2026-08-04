using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Solicitudes;

/// <summary>
/// Solicitud de ausencia de un trabajador para un rango de fechas.
/// </summary>
/// <remarks>
/// Es la fuente formal de las ausencias previstas. Al calcular el tareo, una solicitud
/// aprobada evita que el día se marque como falta.
/// </remarks>
public class SolicitudAusencia : EntidadBase
{
    public long TrabajadorId { get; set; }

    public long TipoAusenciaId { get; set; }

    public TipoAusencia? TipoAusencia { get; set; }

    public DateOnly FechaInicio { get; set; }

    public DateOnly FechaFin { get; set; }

    /// <summary>Hora de inicio, solo si el tipo de ausencia es por horas.</summary>
    public TimeOnly? HoraInicio { get; set; }

    /// <summary>Hora de fin, solo si el tipo de ausencia es por horas.</summary>
    public TimeOnly? HoraFin { get; set; }

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    public string? Sustento { get; set; }

    public string? RutaDocumentoSustento { get; set; }

    public string? Observacion { get; set; }

    /// <summary>Motivo de la anulación. Las solicitudes no se borran: se anulan.</summary>
    public string? MotivoAnulacion { get; set; }

    public bool CubreFecha(DateOnly fecha) =>
        Estado == EstadoSolicitud.Aprobada && fecha >= FechaInicio && fecha <= FechaFin;

    /// <summary>
    /// Detecta si esta solicitud se solapa con otra del mismo trabajador.
    /// Dos ausencias que se pisan en fechas son un error de captura y deben rechazarse
    /// al guardar, indicando con cuál chocan.
    /// </summary>
    public bool SeSolapaCon(SolicitudAusencia otra) =>
        TrabajadorId == otra.TrabajadorId
        && Id != otra.Id
        && Estado != EstadoSolicitud.Anulada
        && otra.Estado != EstadoSolicitud.Anulada
        && FechaInicio <= otra.FechaFin
        && otra.FechaInicio <= FechaFin;
}

public enum EstadoSolicitud
{
    Pendiente = 1,
    Aprobada = 2,
    Rechazada = 3,
    Anulada = 4
}
