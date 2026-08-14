using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Domain.Solicitudes;

namespace ControlMarcacion.Application.Solicitudes.Dto;

/// <summary>Fila del listado de solicitudes de ausencia.</summary>
public sealed record SolicitudListaDto(
    long Id,
    long TrabajadorId,
    string Trabajador,
    string TipoAusencia,
    string ComportamientoEtiqueta,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFin,
    EstadoSolicitud Estado,
    bool RequiereSustento,
    string? Sustento,
    string? Observacion,
    string? MotivoAnulacion,
    string? SubtipoVacaciones,
    string? PeriodoVacacional,
    int JustificacionesGeneradas,
    Contingencia? Contingencia = null,
    string? Diagnostico = null,
    CentroAtencion? CentroAtencion = null)
{
    /// <summary>
    /// Contingencia y centro de atención en una línea, para el listado. Nulo si la
    /// solicitud no lleva detalle clínico.
    /// </summary>
    /// <remarks>
    /// <b>Deliberadamente sin el diagnóstico.</b> El listado es la pantalla más expuesta
    /// del módulo —se abre en una planta, con gente alrededor y a menudo proyectada— y el
    /// diagnóstico es un dato sensible de salud (Ley 29733). Que la solicitud sea por
    /// accidente laboral y se atendiera en EsSalud es lo que hace falta para gestionarla;
    /// el diagnóstico solo se necesita en la hoja que se entrega a planilla.
    /// </remarks>
    public string? DetalleClinico => Contingencia is null && CentroAtencion is null
        ? null
        : string.Join(" · ", new[] { EtiquetaContingencia, EtiquetaCentroAtencion }
            .Where(p => p is not null));

    // Los enums van con su espacio de nombres completo: dentro de
    // ControlMarcacion.Application.Solicitudes.Dto, un "Solicitudes." a secas resuelve al
    // propio espacio de Application y no al del dominio.
    public string? EtiquetaContingencia => Contingencia switch
    {
        Domain.Solicitudes.Contingencia.EnfermedadComun => "Enfermedad común",
        Domain.Solicitudes.Contingencia.AccidenteLaboral => "Accidente laboral",
        _ => null
    };

    public string? EtiquetaCentroAtencion => CentroAtencion switch
    {
        Domain.Solicitudes.CentroAtencion.Essalud => "EsSalud",
        Domain.Solicitudes.CentroAtencion.Particular => "Particular",
        _ => null
    };

    public int Dias => FechaFin.DayNumber - FechaInicio.DayNumber + 1;

    /// <summary>Detalle del subtipo y periodo, solo en las solicitudes de vacaciones.</summary>
    public string? DetalleVacaciones => SubtipoVacaciones switch
    {
        "GOZADAS" => $"Gozadas · periodo {PeriodoVacacional}",
        "VENTA" => $"Venta · periodo {PeriodoVacacional}",
        _ => null
    };

    public bool EsPorHoras => HoraInicio is not null;

    /// <summary>Solo una solicitud pendiente admite decisión.</summary>
    public bool AdmiteDecision => Estado == EstadoSolicitud.Pendiente;

    /// <summary>Una solicitud aprobada puede anularse; una anulada o rechazada, no.</summary>
    public bool AdmiteAnulacion => Estado == EstadoSolicitud.Aprobada;

    /// <summary>El tipo exige sustento documentado y la solicitud no lo tiene.</summary>
    /// <remarks>
    /// Es el mismo criterio que aplica <c>ServicioSolicitudes.AprobarAsync</c>, que se
    /// niega a aprobar en ese caso. Vive aquí para que el listado pueda <b>decirlo antes</b>
    /// en vez de dejar que el usuario pulse Aprobar y reciba el rechazo después.
    ///
    /// Esto <b>no</b> sustituye a la comprobación del servicio: ocultar un botón nunca es
    /// el control. Solo evita ofrecer una acción que ya se sabe que va a fallar.
    /// </remarks>
    public bool FaltaSustento => RequiereSustento && string.IsNullOrWhiteSpace(Sustento);

    /// <summary>Está pendiente y tiene todo lo que su tipo exige para poder aprobarse.</summary>
    public bool PuedeAprobarse => AdmiteDecision && !FaltaSustento;
}

/// <summary>Alta y edición de una solicitud de ausencia.</summary>
public sealed record GuardarSolicitudRequest
{
    public long? Id { get; init; }

    [Required(ErrorMessage = "Seleccione el trabajador.")]
    public long TrabajadorId { get; init; }

    [Required(ErrorMessage = "Seleccione el tipo de ausencia.")]
    public long TipoAusenciaId { get; init; }

    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    public DateOnly FechaInicio { get; init; }

    [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
    public DateOnly FechaFin { get; init; }

    /// <summary>Obligatorias solo si el tipo de ausencia se registra por horas.</summary>
    public TimeOnly? HoraInicio { get; init; }

    public TimeOnly? HoraFin { get; init; }

    /// <summary>
    /// <c>GOZADAS</c> o <c>VENTA</c>. Obligatorio cuando el tipo es vacaciones; en el resto
    /// de tipos el servicio lo descarta.
    /// </summary>
    /// <remarks>
    /// No lleva <c>[Required]</c> porque solo aplica a un tipo de ausencia entre nueve:
    /// exigirlo aquí bloquearía cualquier otra solicitud. La obligatoriedad condicional la
    /// resuelve <c>ValidadorLimitesSolicitud</c>, que sí sabe de qué tipo se trata.
    /// </remarks>
    [StringLength(10)]
    public string? SubtipoVacaciones { get; init; }

    /// <summary>Periodo vacacional en formato AAAA-AAAA, p. ej. 2025-2026.</summary>
    /// <remarks>
    /// La expresión regular no alcanza a comprobar que el segundo año sea el siguiente del
    /// primero: 2025-2027 la pasa. Esa validación va en el servidor, en el validador de
    /// límites.
    /// </remarks>
    [StringLength(9)]
    [RegularExpression(@"^\d{4}-\d{4}$", ErrorMessage = "El periodo debe tener el formato AAAA-AAAA.")]
    public string? PeriodoVacacional { get; init; }

    /// <summary>
    /// Descripción de por qué se pide la ausencia. Obligatoria <b>al aprobar</b>, no al
    /// registrar, y solo en los tipos que la exigen.
    /// </summary>
    /// <remarks>
    /// Una solicitud sin sustento escrito no se puede defender ante una inspección: la
    /// justificación que genera al aprobarse hereda este texto y es lo que explica, meses
    /// después, por qué ese día no quedó como falta. Por eso <c>AprobarAsync</c> se niega a
    /// aprobar sin él cuando el tipo lo marca con <c>RequiereSustento</c>.
    ///
    /// Pero <b>no lleva <c>[Required]</c></b>, por el mismo motivo que
    /// <see cref="SubtipoVacaciones"/>: solo lo exigen cinco tipos de nueve, y pedirlo aquí
    /// bloquearía las vacaciones y los permisos, que no lo necesitan nunca. La
    /// obligatoriedad condicional la resuelve quien sabe de qué tipo se trata.
    ///
    /// Además, exigirlo al registrar rompe el caso real para el que se diseñó la regla:
    /// quien pide un descanso médico avisa el mismo día y trae el certificado de EsSalud
    /// tres días después. Con el campo obligatorio, RRHH solo tenía dos salidas —no
    /// registrar la ausencia, y que el día salga como falta, o escribir "pendiente" para
    /// pasar la validación—, y esa segunda es peor: ese texto es el que acaba heredando la
    /// justificación como explicación ante una inspección.
    ///
    /// El sustento que falta se registra después con
    /// <c>IServicioSolicitudes.RegistrarSustentoAsync</c>.
    /// </remarks>
    [StringLength(500, ErrorMessage = "El sustento no puede superar los 500 caracteres.")]
    public string? Sustento { get; init; }

    [StringLength(400)]
    public string? RutaDocumentoSustento { get; init; }

    [StringLength(500)]
    public string? Observacion { get; init; }

    /// <summary>
    /// Origen de la incapacidad. Obligatorio en los tipos subsidiados.
    /// </summary>
    /// <remarks>
    /// Sin <c>[Required]</c> por el mismo motivo que <see cref="SubtipoVacaciones"/>: solo
    /// aplica a tres tipos de nueve, y exigirlo aquí bloquearía todos los demás. La
    /// obligatoriedad condicional la resuelve <c>ValidadorDetalleClinico</c>, que sí sabe
    /// de qué tipo se trata.
    /// </remarks>
    public Contingencia? Contingencia { get; init; }

    /// <summary>
    /// Diagnóstico del certificado médico. Obligatorio en los tipos subsidiados.
    /// </summary>
    /// <remarks>
    /// Dato sensible de salud (Ley 29733, art. 2.5). No se registra su valor en la
    /// bitácora de auditoría ni debe salir en logs.
    /// </remarks>
    [StringLength(250, ErrorMessage = "El diagnóstico no puede superar los 250 caracteres.")]
    public string? Diagnostico { get; init; }

    /// <summary>Dónde se atendió al trabajador. Obligatorio en los tipos subsidiados.</summary>
    public CentroAtencion? CentroAtencion { get; init; }
}

/// <summary>
/// Opción del desplegable de tipos de ausencia, con lo que el formulario necesita saber.
/// </summary>
/// <remarks>
/// Lleva el código y los topes además del nombre porque la pantalla cambia según el tipo
/// elegido: vacaciones abre el subtipo, y los tipos con tope muestran su límite. Enviarlos
/// como dato evita que el JavaScript tenga una copia de las reglas —"si es DM, 20 días"—
/// que quedaría desfasada la primera vez que GCA cambie un tope en el catálogo.
/// </remarks>
public sealed record TipoAusenciaOpcionDto(
    long Id,
    string Codigo,
    string Nombre,
    int? MaximoDiasPorEvento,
    int? MaximoDiasPorAnioCalendario,
    bool RequiereDetalleClinico = false)
{
    /// <summary>Etiqueta del desplegable, con el mismo formato que el resto del sistema.</summary>
    public string Etiqueta => $"{Codigo} - {Nombre}";

    /// <summary>Es el tipo de vacaciones, el único que pide subtipo y periodo.</summary>
    public bool EsVacaciones =>
        string.Equals(Codigo, "VAC", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Filtro del listado de solicitudes.</summary>
/// <remarks>
/// Los cuatro filtros son opcionales: sin paginar, entrar a la pantalla traería todas las
/// solicitudes registradas desde que el sistema existe.
/// </remarks>
public sealed record FiltroSolicitudes
{
    public long? TrabajadorId { get; init; }

    public EstadoSolicitud? Estado { get; init; }

    public DateOnly? Desde { get; init; }

    public DateOnly? Hasta { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamanioPagina { get; init; } = 50;
}

/// <summary>
/// Resultado de aprobar una solicitud.
/// </summary>
/// <remarks>
/// Aprobar no es solo cambiar un estado: genera una justificación por cada día cubierto.
/// Los días que caen en un periodo cerrado no se pueden justificar y se informan aparte,
/// en vez de fallar entera la aprobación o —peor— saltárselos en silencio.
/// </remarks>
public sealed record ResultadoAprobacion(
    long SolicitudId,
    int JustificacionesCreadas,
    IReadOnlyList<DateOnly> DiasEnPeriodoCerrado,
    IReadOnlyList<DateOnly> DiasSinPeriodo,
    IReadOnlyList<DateOnly> DiasConJustificacionExistente)
{
    public bool HuboOmisiones =>
        DiasEnPeriodoCerrado.Count > 0 || DiasSinPeriodo.Count > 0 || DiasConJustificacionExistente.Count > 0;
}
