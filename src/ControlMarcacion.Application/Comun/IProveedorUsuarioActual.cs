namespace ControlMarcacion.Application.Comun;

/// <summary>
/// Quién está haciendo el cambio que se va a guardar.
/// </summary>
/// <remarks>
/// Existe para la bitácora de auditoría. Los servicios de caso de uso ya reciben el
/// usuario como parámetro, pero el interceptor que escribe la auditoría corre por debajo
/// de todos ellos, en <c>SaveChanges</c>: ahí no hay parámetro que consultar, y hacer que
/// cada servicio se lo pase sería confiar en que nadie lo olvide — que es exactamente lo
/// que una auditoría no puede permitirse.
///
/// La implementación real lee la identidad de la petición HTTP y vive en la capa Web:
/// Application no conoce <c>HttpContext</c>.
/// </remarks>
public interface IProveedorUsuarioActual
{
    /// <summary>
    /// Nombre del usuario autenticado, o <c>sistema</c> cuando el cambio no viene de una
    /// petición con sesión (siembra, arranque, tarea programada).
    /// </summary>
    string Usuario { get; }
}
