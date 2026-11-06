namespace ControlMarcacion.Domain.Asistencia;

/// <summary>
/// Qué le pasa a un día que una persona tiene que revisar.
/// </summary>
/// <remarks>
/// <para>
/// El estado del tareo dice <b>cómo quedó</b> el día; la incidencia dice <b>qué hay que
/// resolver</b>. Son preguntas distintas y por eso son campos distintos: un día
/// <c>Incompleto</c> puede serlo por falta de entrada o de salida —lo que se regulariza es
/// diferente—, y un día <c>Puntual</c> puede necesitar revisión igual si sus marcas
/// contradicen el turno que el padrón creía vigente.
/// </para>
/// <para>
/// Solo lleva valores que el motor puede <b>producir por sí mismo</b>. Un enum con opciones
/// que nada genera es una bandeja con filtros que nunca devuelven nada, y termina
/// pareciendo que el sistema no detecta lo que en realidad nunca supo detectar. Quedaron
/// fuera, a propósito:
/// </para>
/// <list type="bullet">
///   <item><b>Ambas marcaciones olvidadas</b>: el motor no puede distinguirlo de una falta
///   real —no hay ninguna marca que lo delate—. Es un <i>motivo de regularización</i>, no
///   una incidencia detectable: se registra llenando las dos horas de la regularización.</item>
///   <item><b>Marcación de trabajador inactivo</b>: no hay día de tareo donde ponerla,
///   porque no hay a quién atribuirla. Vive en la lista de «números sin trabajador» de la
///   pantalla de Marcaciones, que es donde se resuelve.</item>
///   <item><b>Doble marcación</b>: el motor ya consolida los rebotes del lector y los
///   declara en la observación del día. Marcarla como incidencia llenaría la bandeja de
///   días correctos, y una bandeja con ruido no se revisa.</item>
/// </list>
/// </remarks>
public enum TipoIncidencia
{
    /// <summary>
    /// Hay salida pero no entrada. Es el caso rector: el marcador registró la salida y la
    /// persona olvidó marcar al llegar.
    /// </summary>
    IngresoNoRegistrado = 1,

    /// <summary>Hay entrada pero no salida.</summary>
    SalidaNoRegistrada = 2,

    /// <summary>
    /// Hubo marcaciones del trabajador ese día, pero fuera de la ventana de su turno, así
    /// que el cálculo no las usó.
    /// </summary>
    /// <remarks>
    /// Hoy esas marcas se descartan sin dejar rastro: no aparecen en la observación, no
    /// generan aviso y solo quedan en <c>marcacion_cruda</c>, que nadie consulta a mano.
    /// Pueden ser un turno cubierto, una rotación no anotada o un error de aparato, y
    /// ninguna de las tres la puede decidir el motor solo.
    /// </remarks>
    FueraDeTurno = 3,

    /// <summary>
    /// Las marcas del día encajan mucho mejor con otro turno que con el asignado.
    /// </summary>
    /// <remarks>
    /// Lo detecta <see cref="DetectorTurnoDiscrepante"/>. El día se calcula igual, con el
    /// turno que el padrón dice: corregir la rotación es una decisión con autor, y si el
    /// motor reescribiera el turno por su cuenta el tareo dejaría de ser reconstruible
    /// desde el padrón.
    /// </remarks>
    TurnoDiscrepante = 4,

    /// <summary>
    /// No se cargó el archivo del aparato que le correspondía a esa persona ese día.
    /// </summary>
    /// <remarks>
    /// <b>Es la incidencia más importante de todas.</b> Sin ella, un día sin archivo
    /// cargado produce una falta por cada trabajador del aparato, idéntica a una falta
    /// real y con una observación que afirma algo que el sistema no sabe: que no registró
    /// ninguna marcación. La cobertura por aparato ya existe y ya alimenta el calendario de
    /// Marcaciones y la portada; lo que falta es que el cálculo la mire.
    /// </remarks>
    SinCoberturaDeCarga = 5,

    /// <summary>Las marcas residuales de un descanso no fijan un intervalo inequívoco.</summary>
    DelimitacionDescansoAmbigua = 6,

    /// <summary>
    /// Aparecieron marcaciones en un día otorgado como descanso sustitutorio. El motor no
    /// decide por sí solo si se perdió la sustitución ni cómo valorizar ese segundo trabajo.
    /// </summary>
    TrabajoEnDescansoSustitutorio = 7
}
