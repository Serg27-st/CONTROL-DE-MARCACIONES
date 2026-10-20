using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Organizacion;

namespace ControlMarcacion.Domain.Catalogos;

/// <summary>
/// Grupo de obreros de una planta que rota de turno a la vez.
/// </summary>
/// <remarks>
/// <para>
/// En planta no rota cada persona por su cuenta: rota la cuadrilla entera, el mismo lunes
/// tras el descanso dominical (medido sobre el attlog de julio de 2026). Anotar esa rotación
/// ficha por ficha eran cuarenta ediciones cada quincena, y bastaba olvidar una para que esa
/// persona se calculara con el turno viejo.
/// </para>
/// <para>
/// La cuadrilla no guarda un turno: guarda una <see cref="ProgramacionCuadrilla"/> con
/// vigencia por fecha, y el turno de cada día sale de ella. Así la rotación de la semana que
/// viene ya está decidida sin que nadie tenga que aplicarla, y un recálculo de un mes
/// antiguo usa la programación que regía entonces.
/// </para>
/// </remarks>
public class Cuadrilla : EntidadBase
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    /// <summary>
    /// Planta a la que pertenece. Solo pueden integrarla trabajadores de esa planta, y sus
    /// turnos tienen que estar habilitados en ella.
    /// </summary>
    public long PlantaId { get; set; }

    public Planta? Planta { get; set; }

    public ICollection<ProgramacionCuadrilla> Programaciones { get; set; } = [];
}
