using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Justificaciones;

/// <summary>
/// Capa que reconcilia el dato duro del marcador con la realidad, sin falsificarlo.
/// </summary>
/// <remarks>
/// El marcador dice que alguien no vino; tres días después esa persona presenta un
/// descanso médico. La justificación es lo que permite reflejar ese hecho
/// <b>sin tocar la marcación original</b>:
/// <code>Marcador (inmutable) + Justificaciones (editable) = Tareo final</code>
///
/// Se alimenta automáticamente desde las solicitudes aprobadas y además el administrador
/// puede crearlas a mano durante el cierre previo. Solo se puede escribir mientras el
/// periodo esté abierto.
///
/// Es el punto más sensible del sistema desde el lado de la auditoría: es donde se podría
/// maquillar una falta. Por eso registra siempre quién, cuándo y el valor anterior.
/// </remarks>
public class Justificacion : EntidadBase
{
    public long TrabajadorId { get; set; }

    public long PeriodoId { get; set; }

    public DateOnly Fecha { get; set; }

    public OrigenJustificacion Origen { get; set; }

    /// <summary>Solicitud que la originó. Nulo cuando el origen es manual.</summary>
    public long? SolicitudAusenciaId { get; set; }

    /// <summary>Tipo de ausencia aplicado. Nulo si solo justifica una tardanza.</summary>
    public long? TipoAusenciaId { get; set; }

    public TipoJustificacion Tipo { get; set; }

    /// <summary>Minutos justificados, cuando la justificación es parcial (tardanza).</summary>
    public int? MinutosJustificados { get; set; }

    /// <summary>Qué incidencia del día viene a resolver esta justificación.</summary>
    public TipoIncidencia? TipoIncidencia { get; set; }

    // ---------- Regularización: la hora que el marcador no registró ----------

    /// <summary>
    /// Hora de entrada verificada por quien autoriza, cuando el marcador no la registró.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es la pieza que faltaba para resolver el caso más común de todos: el marcador tiene
    /// la salida de las 17:03, la persona olvidó marcar al llegar y el supervisor verifica
    /// que entró 07:58. Hasta ahora eso solo se podía reflejar convirtiendo el día en una
    /// ausencia justificada, que es falso —la persona trabajó— y además pagaba una jornada
    /// ordinaria plana en vez de la que realmente hubo.
    /// </para>
    /// <para>
    /// ⚠️ <b>No toca la marcación original y no la reemplaza.</b> La marca del aparato
    /// sigue intacta en <c>marcacion_cruda</c>, y el día conserva su
    /// <c>EntradaReal</c>/<c>SalidaReal</c>: esto es un dato aparte, con autor y fecha, que
    /// el cálculo suma encima. Es la diferencia entre corregir un tareo y falsificar un
    /// registro biométrico.
    /// </para>
    /// <para>
    /// Las dos horas pueden venir llenas a la vez: es el día en que se olvidaron las dos
    /// marcas, que no se detecta como incidencia —nada lo delata— pero sí se regulariza.
    /// </para>
    /// </remarks>
    public DateTime? EntradaRegularizada { get; set; }

    /// <summary>Hora de salida verificada por quien autoriza.</summary>
    public DateTime? SalidaRegularizada { get; set; }

    // ---------- Ciclo de aprobación ----------

    /// <summary>
    /// En qué punto del trámite está. Quien la registra no es necesariamente quien la
    /// autoriza.
    /// </summary>
    /// <remarks>
    /// Nace <see cref="EstadoJustificacion.Aprobada"/> en todo lo que ya existía: las que
    /// genera una solicitud aprobada ya pasaron por su propio ciclo de autorización, y
    /// convertirlas en pendientes dejaría sin efecto ausencias ya decididas.
    /// </remarks>
    public EstadoJustificacion Estado { get; set; } = EstadoJustificacion.Aprobada;

    /// <summary>Quién la aprobó o la rechazó. Nulo mientras nadie la haya resuelto.</summary>
    public string? ResueltaPorUsuario { get; set; }

    public DateTime? ResueltaEn { get; set; }

    /// <summary>
    /// Por qué se aprobó o —sobre todo— por qué se rechazó.
    /// </summary>
    /// <remarks>
    /// Un rechazo sin motivo deja al trabajador sin saber qué corregir, y a quien audita
    /// sin saber por qué ese día quedó como quedó.
    /// </remarks>
    public string? MotivoResolucion { get; set; }

    /// <summary>
    /// Estado que tenía el tareo antes de aplicar esta justificación.
    /// Es la evidencia de qué se cambió, y no se recalcula: se congela al crearla.
    /// </summary>
    public string? EstadoAnterior { get; set; }

    public string Motivo { get; set; } = null!;

    public string? RutaDocumentoSustento { get; set; }

    /// <summary>Quién la registró: el solicitante de la regularización.</summary>
    /// <remarks>
    /// No hay un <c>SolicitadaPor</c> aparte a propósito: sería este mismo dato con otro
    /// nombre, y dos campos que significan lo mismo se desincronizan.
    /// </remarks>
    public string RegistradoPorUsuario { get; set; } = null!;

    public DateTime RegistradoEn { get; set; }

    /// <summary>
    /// Token de concurrencia optimista.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SQLite no tiene <c>rowversion</c>, así que es un entero que incrementa quien
    /// modifica la fila: EF lo incluye en el <c>WHERE</c> del <c>UPDATE</c> y la segunda
    /// escritura no encuentra a quién actualizar.
    /// </para>
    /// <para>
    /// Hoy es inerte —una justificación se crea y se retira, nunca se modifica— y se pone
    /// ahora porque el ciclo de aprobación es justamente lo que introduce la carrera: dos
    /// personas resolviendo la misma regularización, una aprobándola y otra rechazándola.
    /// </para>
    /// </remarks>
    public int Version { get; set; }
}

/// <summary>
/// Estado del trámite de una justificación.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ El motor de cálculo solo debe aplicar las <see cref="Aprobada"/>. Una regularización
/// pedida y todavía no autorizada que ya modificara el tareo convertiría la aprobación en
/// un trámite decorativo.
/// </para>
/// <para>
/// <b>No hay un estado «en revisión»</b>, y se quitó a propósito después de escribirlo.
/// Existía para que dos personas no tomaran la misma regularización a la vez, pero de esa
/// colisión ya se encarga el token de concurrencia —la segunda recibe un mensaje que dice
/// quién se adelantó—, y un estado «tomado» que nadie libera deja la fila trabada sin que
/// ninguna pantalla explique por qué. Un valor que nada produce es peor que no tenerlo: sale
/// en los filtros y nunca devuelve nada.
/// </para>
/// </remarks>
public enum EstadoJustificacion
{
    /// <summary>Registrada, esperando que alguien la autorice.</summary>
    Solicitada = 1,

    /// <summary>Autorizada. Es la única que el cálculo aplica.</summary>
    Aprobada = 3,

    /// <summary>
    /// Rechazada con motivo. Se conserva: es la evidencia de que se pidió y de por qué no
    /// se concedió, y borrarla dejaría el día sin explicación.
    /// </summary>
    Rechazada = 4,

    /// <summary>
    /// Retirada por quien la pidió, antes de que nadie la resolviera.
    /// </summary>
    /// <remarks>
    /// Es la salida para el caso corriente: quien registró la regularización se da cuenta de
    /// que la hora estaba mal antes de que el coordinador la mire. Se conserva la fila —el
    /// intento también es historia— y el día queda libre para volver a pedirla, porque el
    /// índice único filtrado no cuenta las canceladas.
    /// </remarks>
    Cancelada = 5
}

public enum OrigenJustificacion
{
    /// <summary>Generada automáticamente desde una solicitud de ausencia aprobada.</summary>
    Solicitud = 1,

    /// <summary>Cargada a mano por el administrador durante el cierre previo.</summary>
    Manual = 2
}

public enum TipoJustificacion
{
    /// <summary>Convierte una falta en ausencia justificada.</summary>
    Ausencia = 1,

    /// <summary>Perdona total o parcialmente una tardanza.</summary>
    Tardanza = 2,

    /// <summary>Completa una jornada con marcación impar (entrada sin salida).</summary>
    MarcacionIncompleta = 3,

    /// <summary>Justifica una salida anticipada.</summary>
    SalidaAnticipada = 4,

    /// <summary>
    /// Permiso por horas dentro de la jornada: perdona los minutos que caen dentro de su
    /// ventana, sean de tardanza, de salida anticipada o de las dos.
    /// </summary>
    /// <remarks>
    /// Existe porque un permiso de 15:00 a 17:00 no es una tardanza, y hasta ahora se
    /// registraba como tal: el descuento se aplicaba sobre <see cref="Tardanza"/>, que ese
    /// día valía cero, y la salida anticipada de 120 minutos quedaba intacta. Solo
    /// funcionaba por casualidad cuando el permiso arrancaba a la hora del turno.
    ///
    /// La ventana del permiso viaja con la justificación, así que el motor la ubica contra
    /// el turno del día en vez de suponer dónde cae.
    ///
    /// No la genera el alta manual: nace siempre de una solicitud de un tipo de ausencia
    /// con <c>EsPorHoras</c>, que es la única que valida y guarda las dos horas.
    /// </remarks>
    PermisoParcial = 5,

    /// <summary>
    /// Aporta la hora que el marcador no registró, para que el día se calcule como la
    /// jornada que realmente ocurrió.
    /// </summary>
    /// <remarks>
    /// Es distinta de <see cref="MarcacionIncompleta"/>, que se limita a declarar el día
    /// como justificado sin decir a qué hora entró o salió la persona. Aquí el dato llega:
    /// una hora verificada por quien autoriza, guardada aparte de la marcación original,
    /// que el cálculo usa para completar la jornada — con su tardanza, su sobretiempo y su
    /// nocturnidad reales, no con una jornada ordinaria plana.
    /// </remarks>
    MarcacionRegularizada = 6
}
