using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Catalogos;

/// <summary>
/// Periodo de tareo. Gobierna qué se puede modificar y qué está congelado.
/// </summary>
/// <remarks>
/// ⚠️ La duración y el corte del periodo (¿mes calendario? ¿del 26 al 25?) todavía no
/// están definidos con GCA. Por eso el rango se guarda como dos fechas explícitas y no
/// se deriva de un mes: así el modelo soporta cualquier corte sin cambios.
/// </remarks>
public class Periodo : EntidadBase
{
    /// <summary>Identificador legible, por ejemplo "2026-07".</summary>
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public DateOnly FechaInicio { get; set; }

    public DateOnly FechaFin { get; set; }

    public EstadoPeriodo Estado { get; set; } = EstadoPeriodo.Abierto;

    public DateTime? CerradoEn { get; set; }

    public string? CerradoPor { get; set; }

    public DateTime? ReabiertoEn { get; set; }

    public string? ReabiertoPor { get; set; }

    /// <summary>Motivo de la reapertura. Obligatorio: es información de auditoría.</summary>
    public string? MotivoReapertura { get; set; }

    public bool EstaAbierto => Estado == EstadoPeriodo.Abierto;

    public bool Contiene(DateOnly fecha) => fecha >= FechaInicio && fecha <= FechaFin;
}

public enum EstadoPeriodo
{
    /// <summary>Admite carga de marcaciones, solicitudes, justificaciones y recálculo.</summary>
    Abierto = 1,

    /// <summary>Solo consulta y reportes. Ninguna escritura, sin excepciones.</summary>
    Cerrado = 2
}
