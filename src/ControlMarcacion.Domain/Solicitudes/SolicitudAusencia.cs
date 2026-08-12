using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Solicitudes;

/// <summary>
/// Solicitud de ausencia de un trabajador para un rango de fechas.
/// </summary>
/// <remarks>
/// Es la fuente formal de las ausencias previstas. Al calcular el tareo, una solicitud
/// aprobada evita que el día se marque como falta.
/// </remarks>
public class SolicitudAusencia : EntidadBase
{
    public long TrabajadorId { get; set; }

    public long TipoAusenciaId { get; set; }

    public TipoAusencia? TipoAusencia { get; set; }

    public DateOnly FechaInicio { get; set; }

    public DateOnly FechaFin { get; set; }

    /// <summary>Hora de inicio, solo si el tipo de ausencia es por horas.</summary>
    public TimeOnly? HoraInicio { get; set; }

    /// <summary>Hora de fin, solo si el tipo de ausencia es por horas.</summary>
    public TimeOnly? HoraFin { get; set; }

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    /// <summary>
    /// Distingue vacaciones gozadas de venta de vacaciones. Solo aplica cuando el tipo es
    /// <c>VAC</c>; en cualquier otro tipo queda nulo.
    /// </summary>
    /// <remarks>
    /// Valores esperados: <c>GOZADAS</c> y <c>VENTA</c>. Se guarda como texto y no como enum
    /// porque el cupo de cada subtipo es distinto (30 y 15 días por periodo) y quien lo lee
    /// es el validador de límites, no el motor de cálculo: para el tareo, ambos son
    /// vacaciones.
    /// </remarks>
    public string? SubtipoVacaciones { get; set; }

    /// <summary>
    /// Periodo vacacional al que se imputan los días, en formato <c>AAAA-AAAA</c>.
    /// </summary>
    /// <remarks>
    /// Las vacaciones se ganan por año de servicio, no por año calendario: un trabajador
    /// puede gozar en 2026 días del periodo 2024-2025. Sin este dato no se sabría contra qué
    /// cupo descontar.
    /// </remarks>
    public string? PeriodoVacacional { get; set; }

    /// <summary>
    /// Origen de la incapacidad. Solo en los tipos subsidiados; nulo en el resto.
    /// </summary>
    /// <remarks>
    /// Es una de las tres columnas clínicas de la hoja <c>Descanso Medico</c> del tareo
    /// final. Sin ella esa columna salía vacía y planilla la llenaba a mano, que es
    /// justamente lo que este sistema viene a eliminar.
    /// </remarks>
    public Contingencia? Contingencia { get; set; }

    /// <summary>
    /// Diagnóstico que consta en el certificado médico. Solo en los tipos subsidiados.
    /// </summary>
    /// <remarks>
    /// <b>Dato sensible de salud</b> (Ley 29733, art. 2.5): tiene protección más estricta
    /// que un dato personal común. Por eso está en la lista de campos que la bitácora de
    /// auditoría registra por nombre pero nunca por valor —queda constancia de que alguien
    /// cambió el diagnóstico, no de cuál era— y no debe aparecer en ningún log ni en
    /// ninguna pantalla que no sea la del propio expediente.
    /// </remarks>
    public string? Diagnostico { get; set; }

    /// <summary>
    /// Dónde se atendió al trabajador. Solo en los tipos subsidiados; nulo en el resto.
    /// </summary>
    public CentroAtencion? CentroAtencion { get; set; }

    public string? Sustento { get; set; }

    public string? RutaDocumentoSustento { get; set; }

    public string? Observacion { get; set; }

    /// <summary>Motivo de la anulación. Las solicitudes no se borran: se anulan.</summary>
    public string? MotivoAnulacion { get; set; }

    public bool CubreFecha(DateOnly fecha) =>
        Estado == EstadoSolicitud.Aprobada && fecha >= FechaInicio && fecha <= FechaFin;

    /// <summary>
    /// Detecta si esta solicitud se solapa con otra del mismo trabajador.
    /// Dos ausencias que se pisan en fechas son un error de captura y deben rechazarse
    /// al guardar, indicando con cuál chocan.
    /// </summary>
    public bool SeSolapaCon(SolicitudAusencia otra) =>
        TrabajadorId == otra.TrabajadorId
        && Id != otra.Id
        && Estado != EstadoSolicitud.Anulada
        && otra.Estado != EstadoSolicitud.Anulada
        && FechaInicio <= otra.FechaFin
        && otra.FechaInicio <= FechaFin;
}

public enum EstadoSolicitud
{
    Pendiente = 1,
    Aprobada = 2,
    Rechazada = 3,
    Anulada = 4
}

/// <summary>
/// Origen de la incapacidad que sustenta un descanso médico o un subsidio.
/// </summary>
/// <remarks>
/// Los dos valores salen del archivo modelo de GCA: la columna <c>CONTINGENCIA</c> de la
/// hoja <c>Descanso Medico</c> solo contiene <c>ENFERMEDAD COMUN</c> y
/// <c>ACCIDENTE LABORAL</c>.
///
/// Es un enum y no un catálogo en base, igual que <c>MotivoCese</c>: la distinción define
/// quién asume el costo —EsSalud o el seguro complementario de trabajo de riesgo— y no es
/// configuración que GCA deba poder cambiar sin que quede en el historial del repositorio.
/// </remarks>
public enum Contingencia
{
    EnfermedadComun = 1,

    AccidenteLaboral = 2
}

/// <summary>
/// Dónde se atendió al trabajador, según la columna <c>CENTRO DE ATENCIÓN</c> del modelo.
/// </summary>
public enum CentroAtencion
{
    Essalud = 1,

    Particular = 2
}
