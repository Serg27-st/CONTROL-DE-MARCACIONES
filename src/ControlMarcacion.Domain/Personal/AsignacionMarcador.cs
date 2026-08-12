using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Personal;

/// <summary>
/// Vínculo entre un trabajador y el identificador que le asigna el marcador ZKTeco,
/// <b>con vigencia por fecha</b>.
/// </summary>
/// <remarks>
/// El ID de marcador se reasigna a otra persona cuando alguien cesa. Si se guardara
/// como un campo simple del trabajador, al reasignarlo todas las marcaciones históricas
/// del anterior ocupante pasarían a atribuirse al nuevo, corrompiendo tareos ya cerrados.
/// Por eso la resolución ID → trabajador se hace <b>por la fecha de la marcación</b>,
/// no por la asignación actual.
/// </remarks>
public class AsignacionMarcador : EntidadBase, IVigenciaPorFecha
{
    public long TrabajadorId { get; set; }

    public Trabajador? Trabajador { get; set; }

    /// <summary>
    /// Identificador que reporta el equipo en el archivo TXT.
    /// <b>Texto</b>, para conservar los ceros a la izquierda (por ejemplo "00123").
    /// </summary>
    public string IdMarcador { get; set; } = null!;

    public DateOnly FechaInicio { get; set; }

    /// <summary>Nulo mientras la asignación siga vigente.</summary>
    public DateOnly? FechaFin { get; set; }

    public bool EstaVigenteEn(DateOnly fecha) =>
        fecha >= FechaInicio && (FechaFin is null || fecha <= FechaFin.Value);
}
