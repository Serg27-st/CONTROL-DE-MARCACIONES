using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Catalogos;

/// <summary>
/// Tipo de ausencia. Es un <b>catálogo</b>, no un enum cerrado: GCA indicó
/// explícitamente que la lista puede crecer.
/// </summary>
/// <remarks>
/// Tipos iniciales: vacaciones, descanso médico, licencia con goce, licencia sin goce,
/// permiso, licencia paternal, licencia por maternidad, subsidio por enfermedad y
/// subsidio por accidente.
///
/// Agregar "licencia por fallecimiento de familiar" debe ser un INSERT, no un despliegue.
/// Lo que sí es un enum es el <see cref="Comportamiento"/>: la lista de tipos crece, pero
/// las formas de tratarlos en el cálculo son finitas.
/// </remarks>
public class TipoAusencia : EntidadBase
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public ComportamientoAusencia Comportamiento { get; set; }

    /// <summary>
    /// El día cuenta como asistido para efectos del tareo (no genera falta).
    /// </summary>
    public bool ComputaComoAsistencia { get; set; } = true;

    /// <summary>Exige documento de sustento (certificado médico, resolución, etc.).</summary>
    public bool RequiereSustento { get; set; }

    /// <summary>Se registra por horas dentro de un día en vez de por días completos.</summary>
    public bool EsPorHoras { get; set; }
}

public enum ComportamientoAusencia
{
    /// <summary>Justificada y pagada por el empleador (vacaciones, licencia con goce).</summary>
    JustificadaRemunerada = 1,

    /// <summary>Justificada pero no pagada (licencia sin goce).</summary>
    JustificadaNoRemunerada = 2,

    /// <summary>Cubierta por subsidio (descanso médico, enfermedad, accidente).</summary>
    Subsidiada = 3,

    /// <summary>Permiso parcial dentro de la jornada.</summary>
    PermisoParcial = 4
}
