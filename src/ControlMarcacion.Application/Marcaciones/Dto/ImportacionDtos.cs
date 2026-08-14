namespace ControlMarcacion.Application.Marcaciones.Dto;

/// <summary>
/// Una marcación tal como viene del archivo, antes de resolverse contra el padrón.
/// </summary>
/// <remarks>
/// <see cref="IdMarcador"/> es <b>texto</b>: el archivo real trae identificadores como
/// "0014" junto a "14", y convertirlos a número los haría colisionar.
/// </remarks>
public sealed record MarcacionLeida(
    string IdMarcador,
    DateTime FechaHora,
    string? ModoVerificacion,
    string? TipoMarcacion,
    int NumeroLinea,
    string LineaOriginal);

/// <summary>Una línea que no se pudo interpretar.</summary>
public sealed record LineaRechazada(int NumeroLinea, string ContenidoLinea, string Motivo);

/// <summary>Resultado de leer el archivo, sin tocar todavía la base de datos.</summary>
public sealed record ResultadoLectura(
    IReadOnlyList<MarcacionLeida> Marcaciones,
    IReadOnlyList<LineaRechazada> Rechazadas)
{
    public int TotalLineas => Marcaciones.Count + Rechazadas.Count;
}

/// <summary>
/// Identificador del marcador que aparece en el archivo pero no está asignado a nadie.
/// </summary>
/// <remarks>
/// No es un error del archivo: es un trabajador que falta registrar, o una asignación
/// cuya vigencia no cubre esas fechas. Se reporta aparte para que RRHH sepa exactamente
/// a quién dar de alta.
/// </remarks>
public sealed record IdMarcadorSinAsignar(
    string IdMarcador,
    int Marcaciones,
    DateOnly PrimeraFecha,
    DateOnly UltimaFecha);

/// <summary>Resultado completo de una importación ya persistida.</summary>
public sealed record ResultadoImportacion(
    long ImportacionId,
    string NombreArchivo,
    int FilasLeidas,
    int FilasInsertadas,
    int FilasDuplicadas,
    int FilasRechazadas,
    IReadOnlyList<LineaRechazada> Rechazadas,
    IReadOnlyList<IdMarcadorSinAsignar> IdsSinAsignar)
{
    public bool HuboProblemas => FilasRechazadas > 0 || IdsSinAsignar.Count > 0;
}

/// <summary>Aviso de que el mismo archivo ya se cargó antes.</summary>
public sealed record ImportacionPrevia(
    long ImportacionId,
    string NombreArchivo,
    DateTime FechaCarga,
    string CargadoPorUsuario,
    int FilasInsertadas);

/// <summary>Fila del historial de importaciones.</summary>
/// <remarks>
/// Hay dos fechas distintas y conviene no confundirlas: <see cref="FechaCarga"/> es cuándo
/// alguien subió el archivo, y <see cref="PrimeraMarcacion"/>–<see cref="UltimaMarcacion"/>
/// es <b>qué días trae dentro</b>. La segunda es la que responde "¿ya cargué julio?"; la
/// primera solo dice cuándo se hizo el trámite. Un archivo subido el 2 de agosto puede
/// contener julio entero.
/// </remarks>
public sealed record ImportacionHistorialDto(
    long Id,
    string NombreArchivo,
    DateTime FechaCarga,
    string CargadoPorUsuario,
    int FilasLeidas,
    int FilasInsertadas,
    int FilasDuplicadas,
    int FilasRechazadas,
    bool Anulada = false,
    string? MotivoAnulacion = null,
    DateTime? AnuladaEn = null,
    string? AnuladaPorUsuario = null,
    DateOnly? PrimeraMarcacion = null,
    DateOnly? UltimaMarcacion = null)
{
    /// <summary>
    /// La carga dejó marcaciones consultables. Es falso cuando todas resultaron duplicadas
    /// o rechazadas: el archivo se procesó, pero no aportó ningún día.
    /// </summary>
    public bool TieneCobertura => PrimeraMarcacion is not null && UltimaMarcacion is not null;
}

/// <summary>Marcaciones vigentes cargadas para un día concreto.</summary>
/// <remarks>
/// <see cref="Trabajadores"/> es cuántas personas distintas marcaron ese día. Es el dato
/// que delata un archivo parcial: 40 marcaciones de 20 trabajadores en una planta de 200
/// no es un día cargado, es un día a medias.
/// </remarks>
public sealed record DiaConMarcaciones(DateOnly Fecha, int Marcaciones, int Trabajadores)
{
    public bool TieneMarcaciones => Marcaciones > 0;
}

/// <summary>
/// Cobertura de marcaciones sobre un rango, resumida: lo que hace falta para responder
/// «¿me falta cargar algo de este periodo?» sin pintar un calendario.
/// </summary>
/// <remarks>
/// Los días que todavía no ocurrieron no cuentan como hueco: en un periodo en curso, la
/// mitad del mes está en el futuro y contarla como faltante convertiría el aviso en ruido
/// permanente.
/// </remarks>
public sealed record CoberturaDeRango(
    DateOnly Desde,
    DateOnly Hasta,
    int Marcaciones,
    int DiasConMarcacion,
    int DiasTranscurridos)
{
    public static CoberturaDeRango Vacia(DateOnly desde, DateOnly hasta) =>
        new(desde, hasta, 0, 0, 0);

    /// <summary>Días ya transcurridos del rango sin ninguna marcación cargada.</summary>
    public int DiasSinMarcacion => Math.Max(DiasTranscurridos - DiasConMarcacion, 0);

    public bool HayHuecos => DiasSinMarcacion > 0;

    /// <summary>Días del rango, extremos incluidos, hayan ocurrido o no.</summary>
    public int DiasDelRango => Hasta.DayNumber - Desde.DayNumber + 1;
}

/// <summary>Resumen de un mes dentro del año consultado.</summary>
public sealed record MesConMarcaciones(int Anio, int Mes, int Marcaciones, int DiasConMarcacion)
{
    public bool TieneMarcaciones => Marcaciones > 0;
}

/// <summary>
/// Qué marcaciones hay cargadas, vistas por año, por mes y por día.
/// </summary>
/// <remarks>
/// Cuenta <b>solo las cargas vigentes</b>: las de una importación anulada existen en la
/// base pero el motor no las usa, así que mostrarlas aquí haría creer que un mes está
/// cubierto cuando el tareo lo verá vacío.
///
/// Los días se devuelven <b>todos</b>, incluidos los que tienen cero: un mes con huecos es
/// exactamente lo que esta pantalla existe para mostrar, y filtrarlos aquí obligaría a la
/// vista a reconstruir el calendario.
/// </remarks>
public sealed record CoberturaMarcaciones(
    int Anio,
    int Mes,
    IReadOnlyList<int> Anios,
    IReadOnlyList<MesConMarcaciones> Meses,
    IReadOnlyList<DiaConMarcaciones> Dias)
{
    public int MarcacionesDelAnio => Meses.Sum(m => m.Marcaciones);

    public int MarcacionesDelMes => Dias.Sum(d => d.Marcaciones);

    public int DiasConMarcacion => Dias.Count(d => d.TieneMarcaciones);

    /// <summary>Días del mes ya transcurridos que no tienen ninguna marcación cargada.</summary>
    /// <remarks>
    /// Los días futuros no se cuentan como hueco: nadie ha marcado todavía. Sin esta
    /// distinción, agosto aparecería con 19 días faltantes el día 12.
    /// </remarks>
    public int DiasSinMarcacion(DateOnly hoy) =>
        Dias.Count(d => !d.TieneMarcaciones && d.Fecha <= hoy);

    public bool HayDatos => Anios.Count > 0;
}
