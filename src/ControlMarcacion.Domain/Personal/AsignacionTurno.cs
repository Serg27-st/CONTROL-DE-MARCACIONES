using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Personal;

/// <summary>
/// Turno asignado a un trabajador durante un rango de fechas: un turno fijo, o una
/// cuadrilla que rota.
/// </summary>
/// <remarks>
/// Los obreros <b>rotan de turno</b> por calendario: una temporada diurno, otra nocturno.
/// El motor de cálculo debe resolver el turno por la asignación vigente en la fecha que
/// está calculando, y nunca por "el último turno del trabajador" — ese atajo produce
/// tareos incorrectos justo en las semanas de cambio de rotación.
///
/// <para>Lleva <b>exactamente uno</b> de <see cref="TurnoId"/> y <see cref="CuadrillaId"/>.
/// Con cuadrilla, el turno de cada día lo da la programación vigente de la cuadrilla en esa
/// fecha (<see cref="ProgramacionCuadrilla.TurnoEn"/>): la asignación dice <i>a qué grupo
/// pertenece</i> la persona, y la programación dice <i>qué turno le toca al grupo</i>. Van
/// en la misma línea de tiempo para que pasar de un turno fijo a una cuadrilla, o de una
/// cuadrilla a otra, sea un cambio de asignación como cualquier otro.</para>
///
/// Los empleados tienen un único turno, pero se modelan igual para no tener dos caminos.
/// </remarks>
public class AsignacionTurno : EntidadBase, IVigenciaPorFecha
{
    public long TrabajadorId { get; set; }

    public Trabajador? Trabajador { get; set; }

    /// <summary>Turno fijo. Nulo cuando la asignación es a una cuadrilla.</summary>
    public long? TurnoId { get; set; }

    public Turno? Turno { get; set; }

    /// <summary>Cuadrilla que rota. Nula cuando la asignación es a un turno fijo.</summary>
    public long? CuadrillaId { get; set; }

    public Cuadrilla? Cuadrilla { get; set; }

    public DateOnly FechaInicio { get; set; }

    /// <summary>Nulo mientras la asignación siga vigente.</summary>
    public DateOnly? FechaFin { get; set; }

    public bool EstaVigenteEn(DateOnly fecha) =>
        fecha >= FechaInicio && (FechaFin is null || fecha <= FechaFin.Value);

    /// <summary>La asignación dice lo mismo que otra: mismo turno fijo o misma cuadrilla.</summary>
    public bool Asigna(long? turnoId, long? cuadrillaId) =>
        TurnoId == turnoId && CuadrillaId == cuadrillaId;
}
