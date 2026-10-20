using ControlMarcacion.Domain.Marcaciones;

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

/// <summary>
/// Evento estructuralmente válido escrito por el equipo que no representa una marcación
/// humana y se ignora deliberadamente.
/// </summary>
/// <remarks>
/// Solo conserva el número físico y el código opaco del evento. No se le asigna un nombre
/// funcional sin documentación fiable del fabricante, y nunca se convierte en
/// <see cref="MarcacionCruda"/>.
/// </remarks>
public sealed record EventoEquipoIgnorado(int NumeroLinea, uint CodigoEvento);

/// <summary>Resultado de leer el archivo, sin tocar todavía la base de datos.</summary>
/// <param name="SerieEquipo">
/// Serie del aparato que generó el archivo, cuando el formato la declara.
/// </param>
/// <remarks>
/// La serie es opcional porque no todos los formatos la traen: el respaldo binario la
/// lleva en su cabecera, y el TXT solo en el nombre del archivo —que alguien puede haber
/// renombrado—. Sirve para <b>contrastar</b> contra el equipo que declaró quien carga, no
/// para sustituirlo: un archivo sin serie se sigue pudiendo importar, uno cuya serie
/// contradice lo declarado se rechaza.
/// </remarks>
public sealed record ResultadoLectura(
    IReadOnlyList<MarcacionLeida> Marcaciones,
    IReadOnlyList<LineaRechazada> Rechazadas,
    string? SerieEquipo = null,
    IReadOnlyList<EventoEquipoIgnorado>? Ignoradas = null)
{
    public IReadOnlyList<EventoEquipoIgnorado> EventosEquipoIgnorados => Ignoradas ?? [];

    public int TotalLineas => Marcaciones.Count + EventosEquipoIgnorados.Count + Rechazadas.Count;
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

/// <summary>
/// Número de un aparato con marcaciones guardadas que ningún trabajador tenía asignado en
/// esas fechas. Calculado en vivo, no al cargar.
/// </summary>
/// <remarks>
/// Es la lista de trabajo para completar el padrón: cada fila es alguien que marcó y cuyo
/// tareo sale en falta porque el sistema no sabe quién es. Se resuelve asignando el número
/// en la ficha del trabajador y recalculando; las marcaciones ya están guardadas.
/// </remarks>
public sealed record NumeroSinAsignarDto(
    long EquipoId,
    string Equipo,
    string IdMarcador,
    int Marcaciones,
    int Dias,
    DateOnly PrimeraFecha,
    DateOnly UltimaFecha);

/// <summary>Resultado completo de una importación ya persistida.</summary>
public sealed record ResultadoImportacion(
    long ImportacionId,
    string NombreArchivo,
    string Equipo,
    int FilasLeidas,
    int FilasInsertadas,
    int FilasDuplicadas,
    int FilasRechazadas,
    IReadOnlyList<LineaRechazada> Rechazadas,
    IReadOnlyList<IdMarcadorSinAsignar> IdsSinAsignar,
    OrigenCarga OrigenVerificado = OrigenCarga.NoVerificado,
    string? SerieArchivo = null,
    int FilasIgnoradas = 0)
{
    public bool HuboProblemas => FilasRechazadas > 0 || IdsSinAsignar.Count > 0;

    public int FilasAceptadas => FilasInsertadas + FilasDuplicadas;
}

/// <summary>Aviso de que el mismo archivo ya se cargó antes.</summary>
/// <remarks>
/// Lleva el aparato y si la carga sigue vigente porque de eso depende qué se puede hacer:
/// repetirlo en el mismo aparato es inocuo, y cargarlo en otro mientras la primera carga
/// esté vigente se rechaza.
/// </remarks>
public sealed record ImportacionPrevia(
    long ImportacionId,
    string NombreArchivo,
    long EquipoId,
    string Equipo,
    DateTime FechaCarga,
    string CargadoPorUsuario,
    int FilasInsertadas,
    bool Anulada);

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
    string Equipo,
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
    OrigenCarga OrigenVerificado = OrigenCarga.NoVerificado,
    string? SerieArchivo = null,
    DateOnly? PrimeraMarcacion = null,
    DateOnly? UltimaMarcacion = null,
    int FilasIgnoradas = 0)
{
    /// <summary>
    /// La carga dejó marcaciones consultables. Es falso cuando todas resultaron duplicadas
    /// o rechazadas: el archivo se procesó, pero no aportó ningún día.
    /// </summary>
    public bool TieneCobertura => PrimeraMarcacion is not null && UltimaMarcacion is not null;

    public int FilasAceptadas => FilasInsertadas + FilasDuplicadas;
}

/// <summary>Marcaciones vigentes cargadas para un día concreto.</summary>
/// <remarks>
/// <see cref="Trabajadores"/> es cuántas personas distintas marcaron ese día. Es el dato
/// que delata un archivo parcial: 40 marcaciones de 20 trabajadores en una planta de 200
/// no es un día cargado, es un día a medias.
/// </remarks>
public sealed record DiaConMarcaciones(
    DateOnly Fecha,
    int Marcaciones,
    int Trabajadores,
    IReadOnlyList<MarcacionesDeEquipo> PorEquipo)
{
    public bool TieneMarcaciones => Marcaciones > 0;

    public MarcacionesDeEquipo? De(long equipoId) =>
        PorEquipo.FirstOrDefault(e => e.EquipoId == equipoId);
}

/// <summary>Marcaciones de un aparato en un día, y cuántas personas distintas marcaron.</summary>
public sealed record MarcacionesDeEquipo(long EquipoId, int Marcaciones, int Personas);

/// <summary>Un aparato en el calendario de cobertura.</summary>
/// <param name="EsperadoDesde">
/// Desde cuándo se esperan sus archivos: ver <see cref="AparatoEsperado.Para"/>.
/// </param>
public sealed record EquipoEnCobertura(
    long EquipoId,
    string Codigo,
    string Nombre,
    string Planta,
    bool Activo,
    DateOnly EsperadoDesde)
{
    public AparatoEsperado Esperado => new(EquipoId, Activo, EsperadoDesde);
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
/// <param name="DiasIncompletos">
/// Días transcurridos con marcaciones de unos aparatos y sin las de otro que se esperaba.
/// No se solapan con los días sin ninguna marcación.
/// </param>
/// <param name="AparatosIncompletos">Nombres de los aparatos a los que les falta algún día.</param>
public sealed record CoberturaDeRango(
    DateOnly Desde,
    DateOnly Hasta,
    int Marcaciones,
    int DiasConMarcacion,
    int DiasTranscurridos,
    int DiasIncompletos = 0,
    IReadOnlyList<string>? AparatosIncompletos = null)
{
    public static CoberturaDeRango Vacia(DateOnly desde, DateOnly hasta) =>
        new(desde, hasta, 0, 0, 0);

    /// <summary>Días ya transcurridos del rango sin ninguna marcación cargada.</summary>
    public int DiasSinMarcacion => Math.Max(DiasTranscurridos - DiasConMarcacion, 0);

    /// <summary>
    /// Días transcurridos a los que les falta algo: ninguna marcación, o las de algún aparato.
    /// </summary>
    public int DiasPorRevisar => DiasSinMarcacion + DiasIncompletos;

    /// <remarks>
    /// ⚠️ Incluye los días incompletos. Antes solo miraba los días sin ninguna marcación, y
    /// con dos aparatos en la planta un archivo olvidado no se notaba: el día tenía las
    /// marcaciones del otro.
    /// </remarks>
    public bool HayHuecos => DiasPorRevisar > 0;

    public IReadOnlyList<string> AparatosConFaltantes => AparatosIncompletos ?? [];

    /// <summary>Días del rango, extremos incluidos, hayan ocurrido o no.</summary>
    public int DiasDelRango => Hasta.DayNumber - Desde.DayNumber + 1;
}

/// <summary>Resumen de un mes dentro del año consultado.</summary>
/// <param name="DiasIncompletos">
/// Días ya transcurridos del mes con marcaciones de unos aparatos y sin las de otro que se
/// esperaba. Es lo que la cifra de marcaciones esconde: un mes con un aparato olvidado tiene
/// marcaciones todos los días.
/// </param>
public sealed record MesConMarcaciones(
    int Anio,
    int Mes,
    int Marcaciones,
    int DiasConMarcacion,
    int DiasIncompletos = 0)
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
    IReadOnlyList<DiaConMarcaciones> Dias,
    IReadOnlyList<EquipoEnCobertura> Equipos)
{
    public int MarcacionesDelAnio => Meses.Sum(m => m.Marcaciones);

    public int MarcacionesDelMes => Dias.Sum(d => d.Marcaciones);

    public int DiasConMarcacion => Dias.Count(d => d.TieneMarcaciones);

    /// <summary>
    /// Aparatos que van en la tabla del mes: los que se esperaban algún día del mes y los que
    /// trajeron marcaciones aunque no se esperaran (uno dado de baja, por ejemplo), para que
    /// las cifras del calendario cuadren con las filas.
    /// </summary>
    public IReadOnlyList<EquipoEnCobertura> EquiposDelMes =>
    [
        .. Equipos
            .Where(e => Dias.Any(d => e.Esperado.EsperadoEn(d.Fecha) || d.De(e.EquipoId) is not null))
            .OrderBy(e => e.Planta, StringComparer.CurrentCulture)
            .ThenBy(e => e.Nombre, StringComparer.CurrentCulture)
    ];

    public EstadoCobertura EstadoDe(DiaConMarcaciones dia, DateOnly hoy) =>
        CoberturaDeCarga.Evaluar(dia.Fecha, hoy, Esperados, IdsConMarcacion(dia));

    /// <summary>Aparatos que se esperaban ese día y no tienen ninguna marcación.</summary>
    public IReadOnlyList<EquipoEnCobertura> FaltantesEn(DiaConMarcaciones dia, DateOnly hoy)
    {
        var faltantes = CoberturaDeCarga.Faltantes(dia.Fecha, hoy, Esperados, IdsConMarcacion(dia)).ToHashSet();

        return [.. Equipos.Where(e => faltantes.Contains(e.EquipoId))];
    }

    /// <summary>Días del mes ya transcurridos que no tienen ninguna marcación cargada.</summary>
    /// <remarks>
    /// Los días futuros no se cuentan como hueco: nadie ha marcado todavía. Sin esta
    /// distinción, agosto aparecería con 19 días faltantes el día 12.
    /// </remarks>
    public int DiasSinMarcacion(DateOnly hoy) =>
        Dias.Count(d => EstadoDe(d, hoy) == EstadoCobertura.SinMarcaciones);

    /// <summary>
    /// Días transcurridos con marcaciones de unos aparatos y sin las de otro que se esperaba.
    /// </summary>
    public int DiasIncompletos(DateOnly hoy) =>
        Dias.Count(d => EstadoDe(d, hoy) == EstadoCobertura.Incompleto);

    /// <summary>
    /// Cuántos días transcurridos del mes le faltan a cada aparato que se esperaba, solo los
    /// que tienen alguno. Es la lista de archivos por cargar.
    /// </summary>
    public IReadOnlyList<(EquipoEnCobertura Equipo, int Dias)> FaltantesPorEquipo(DateOnly hoy) =>
    [
        .. EquiposDelMes
            .Select(e => (Equipo: e, Dias: Dias.Count(d =>
                d.Fecha <= hoy && e.Esperado.EsperadoEn(d.Fecha) && d.De(e.EquipoId) is null)))
            .Where(x => x.Dias > 0)
    ];

    public bool HayDatos => Anios.Count > 0;

    private IEnumerable<AparatoEsperado> Esperados => Equipos.Select(e => e.Esperado);

    private static long[] IdsConMarcacion(DiaConMarcaciones dia) =>
        [.. dia.PorEquipo.Select(e => e.EquipoId)];
}
