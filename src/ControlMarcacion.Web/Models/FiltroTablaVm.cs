namespace ControlMarcacion.Web.Models;

/// <summary>
/// Filtro en vivo sobre las filas ya cargadas de una tabla.
/// </summary>
/// <remarks>
/// Lo dibuja el parcial <c>_FiltroTabla</c> y lo mueve <c>filtro-tabla.js</c>, sin viajar
/// al servidor. Es para las tablas que <b>caben enteras</b> en la respuesta —los catálogos,
/// los feriados de un año, los usuarios—; los listados que paginan (Personal, Tareo,
/// Solicitudes) necesitan el filtro del formulario, porque ahí sí hay filas que no llegaron.
///
/// <b>Solo se dibuja a partir de <see cref="MinimoFilas"/>.</b> Casi todas estas tablas
/// arrancan con un puñado de filas —cinco turnos, nueve tipos de ausencia, un periodo— y
/// una caja de búsqueda encima de cinco filas es ruido: ocupa sitio, sugiere que hay algo
/// que buscar y no ahorra nada. El umbral hace que aparezca sola cuando la tabla deja de
/// caber de un vistazo, sin que haya que acordarse de añadirla el día que crezca.
/// </remarks>
public sealed class FiltroTablaVm
{
    /// <summary>
    /// A partir de cuántas filas compensa. Es aproximadamente una pantalla de tabla: por
    /// debajo, recorrerla con la vista es más rápido que escribir.
    /// </summary>
    public const int MinimoFilas = 12;

    /// <summary>Id del <c>&lt;table&gt;</c> cuyas filas se filtran.</summary>
    public required string IdTabla { get; init; }

    /// <summary>Cuántas filas tiene ahora mismo.</summary>
    public required int Filas { get; init; }

    /// <summary>Id del elemento donde escribir «N de M»; opcional.</summary>
    public string? IdConteo { get; init; }

    /// <summary>Qué columnas tiene sentido teclear, para el marcador de posición.</summary>
    public string Ejemplo { get; init; } = "Escriba para filtrar…";

    /// <summary>
    /// Qué se está contando: «feriado(s)», «turno(s)». Lo usa el contador al filtrar,
    /// para no pasar de «13 feriado(s)» a «2 de 13 registro(s)» al primer tecleo.
    /// </summary>
    public string Sustantivo { get; init; } = "registro(s)";

    public bool MerecePena => Filas >= MinimoFilas;
}
