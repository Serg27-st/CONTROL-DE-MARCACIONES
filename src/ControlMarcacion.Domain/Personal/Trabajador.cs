using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Personal;

/// <summary>
/// Ficha del trabajador: datos personales y datos laborales.
/// </summary>
public class Trabajador : EntidadBase
{
    // ---------- Datos personales ----------

    public long TipoDocumentoId { get; set; }

    /// <summary>
    /// Número de documento de identidad.
    /// <b>Siempre texto, jamás numérico:</b> un DNI que empieza en cero pierde el dígito
    /// al convertirse a número, y el cruce con el marcador falla.
    /// </summary>
    public string NumeroDocumento { get; set; } = null!;

    public string ApellidoPaterno { get; set; } = null!;

    public string ApellidoMaterno { get; set; } = null!;

    public string Nombres { get; set; } = null!;

    public Sexo Sexo { get; set; }

    public string? Telefono { get; set; }

    public string? Direccion { get; set; }

    public string? CorreoElectronico { get; set; }

    public bool TieneHijos { get; set; }

    // ---------- Datos laborales ----------

    public long EmpresaId { get; set; }

    /// <summary>Unidad organizativa. ⚠️ Definición pendiente de confirmar con GCA.</summary>
    public string? Unidad { get; set; }

    /// <summary>Obrero (OBM) o empleado (EMP). Determina el régimen de turnos.</summary>
    public TipoPlanilla TipoPlanilla { get; set; }

    public long PlantaId { get; set; }

    public long AreaId { get; set; }

    public long SeccionId { get; set; }

    public DateOnly FechaIngreso { get; set; }

    /// <summary>Nulo mientras el trabajador siga activo.</summary>
    public DateOnly? FechaCese { get; set; }

    public long PuestoId { get; set; }

    // ---------- Asignaciones con vigencia ----------

    /// <summary>
    /// El ID de marcador NO es un campo plano de esta entidad: se reasigna a otra
    /// persona con el tiempo, y guardarlo aquí haría que las marcaciones históricas
    /// quedaran atribuidas a quien no era.
    /// </summary>
    public ICollection<AsignacionMarcador> AsignacionesMarcador { get; set; } = [];

    /// <summary>
    /// Los obreros rotan de turno por calendario, así que el turno tampoco es un campo
    /// plano: se resuelve por la asignación vigente a la fecha que se está calculando.
    /// </summary>
    public ICollection<AsignacionTurno> AsignacionesTurno { get; set; } = [];

    // ---------- Comportamiento ----------

    /// <summary>Formato usado en el tareo final.</summary>
    public string ApellidosYNombres => $"{ApellidoPaterno} {ApellidoMaterno}, {Nombres}";

    /// <summary>
    /// Indica si el trabajador tenía vínculo laboral en la fecha dada.
    /// El motor de cálculo lo usa para no generar faltas antes del ingreso
    /// ni después del cese.
    /// </summary>
    public bool TieneVinculoEn(DateOnly fecha) =>
        fecha >= FechaIngreso && (FechaCese is null || fecha <= FechaCese.Value);
}
