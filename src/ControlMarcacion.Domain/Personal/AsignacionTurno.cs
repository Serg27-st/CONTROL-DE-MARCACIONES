using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Personal;

/// <summary>
/// Turno asignado a un trabajador durante un rango de fechas.
/// </summary>
/// <remarks>
/// Los obreros <b>rotan de turno</b> por calendario: una temporada diurno, otra nocturno.
/// El motor de cálculo debe resolver el turno por la asignación vigente en la fecha que
/// está calculando, y nunca por "el último turno del trabajador" — ese atajo produce
/// tareos incorrectos justo en las semanas de cambio de rotación.
///
/// Los empleados tienen un único turno, pero se modelan igual para no tener dos caminos.
/// </remarks>
public class AsignacionTurno : EntidadBase, IVigenciaPorFecha
{
    public long TrabajadorId { get; set; }

    public Trabajador? Trabajador { get; set; }

    public long TurnoId { get; set; }

    public Turno? Turno { get; set; }

    public DateOnly FechaInicio { get; set; }

    /// <summary>Nulo mientras la asignación siga vigente.</summary>
    public DateOnly? FechaFin { get; set; }

    public bool EstaVigenteEn(DateOnly fecha) =>
        fecha >= FechaInicio && (FechaFin is null || fecha <= FechaFin.Value);
}
