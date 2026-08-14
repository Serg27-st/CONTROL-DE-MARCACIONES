using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Reportes;

namespace ControlMarcacion.Application.Reportes.Dto;

/// <summary>
/// Filtro común a todos los reportes: un alcance temporal más el recorte por organización.
/// </summary>
/// <remarks>
/// Los campos de fecha son la <b>elección cruda</b> del usuario, no un rango ya validado:
/// según <see cref="Alcance"/> solo algunos aplican. Quien los convierte en un
/// <see cref="RangoReporte"/> bien formado —y quien decide qué falta— es
/// <see cref="ResolutorRangoReporte"/>, para que la regla no se reescriba distinta en cada
/// pantalla.
/// </remarks>
public sealed record FiltroReporte
{
    /// <summary>Cómo se eligió el tramo de tiempo. Por omisión, un periodo de planilla.</summary>
    public AlcanceReporte Alcance { get; init; } = AlcanceReporte.Periodo;

    /// <summary>Obligatorio si <see cref="Alcance"/> es <c>Periodo</c>.</summary>
    public long? PeriodoId { get; init; }

    /// <summary>Obligatorios si <see cref="Alcance"/> es <c>Mes</c>.</summary>
    public int? Anio { get; init; }

    /// <inheritdoc cref="Anio"/>
    public int? Mes { get; init; }

    /// <summary>
    /// Con alcance <c>Dia</c> es el día reportado; con <c>Rango</c>, su fecha de inicio.
    /// </summary>
    public DateOnly? Desde { get; init; }

    /// <summary>Fecha de fin, solo con alcance <c>Rango</c>.</summary>
    public DateOnly? Hasta { get; init; }

    public long? EmpresaId { get; init; }

    public long? PlantaId { get; init; }

    public long? CentroId { get; init; }

    public long? CentroCostoId { get; init; }
}

/// <summary>
/// Fila del resumen mensual: un trabajador, con sus totales del periodo.
/// </summary>
/// <remarks>
/// Es el insumo para planilla, no la boleta: entrega minutos y conteos de días. Las dos
/// excepciones son <see cref="MontoBonoNocturno"/> — el sistema no calcula la remuneración
/// completa del trabajador, pero ese concepto puntual (D.S. 007-2002-TR, art. 8) sí lo
/// resuelve porque depende pura y exclusivamente de datos que el sistema ya tiene:
/// franja nocturna trabajada, RMV, sobretasa y sueldo básico, todos con vigencia por fecha —
/// y <see cref="MontoMovilidad"/>, días asistidos por la tarifa vigente, mismo criterio que
/// usa el tareo final.
///
/// <see cref="Centro"/>/<see cref="CentroCosto"/> vienen del catálogo real de SAP asignado
/// en Personal (<c>Trabajador.CentroCostoId</c>); si el trabajador todavía no tiene uno
/// asignado, caen a la aproximación con Área/Sección.
///
/// ⚠️ Ese respaldo es <b>propio de este reporte</b> y ya no coincide con el tareo final:
/// <c>ServicioReportes.ArmarFilasTareoAsync</c> deja las columnas de CECO vacías a
/// propósito, porque un código de Área/Sección no existe en SAP y planilla lo tomaría por
/// bueno. Aquí el respaldo sobrevive porque este resumen es de consulta interna y no se
/// pega en ninguna plantilla, pero es una divergencia real: el mismo trabajador muestra
/// centro en una pantalla y vacío en la otra. Unificar las dos está pendiente de decidir.
/// </remarks>
public sealed record ResumenTareoDto(
    long TrabajadorId,
    string Trabajador,
    string? IdMarcador,
    string Empresa,
    string Planta,
    string? Centro,
    string? CentroCosto,
    string Puesto,
    int DiasPuntual,
    int DiasTardanza,
    int DiasFalta,
    int DiasAusenciaJustificada,
    int DiasIncompleto,
    int DiasDescansoTrabajado,
    int DiasFeriadoTrabajado,
    int MinutosOrdinarios,
    int MinutosExtra25,
    int MinutosExtra35,
    int MinutosNocturnos,
    int MinutosTardanzaAcumulados,
    decimal MontoBonoNocturno,
    decimal MontoMovilidad)
{
    public decimal HorasOrdinarias => Math.Round(MinutosOrdinarios / 60m, 2);

    public decimal HorasExtra25 => Math.Round(MinutosExtra25 / 60m, 2);

    public decimal HorasExtra35 => Math.Round(MinutosExtra35 / 60m, 2);

    public decimal HorasNocturnas => Math.Round(MinutosNocturnos / 60m, 2);

    public decimal HorasTardanza => Math.Round(MinutosTardanzaAcumulados / 60m, 2);

    public int DiasNoLaborableTrabajado => DiasDescansoTrabajado + DiasFeriadoTrabajado;
}

/// <summary>
/// Totales del resumen sobre <b>todo el alcance</b>, no sobre la página que se ve.
/// </summary>
/// <remarks>
/// La fila TOTAL se sumaba en la vista, recorriendo las filas que le habían llegado. Eso
/// solo era correcto mientras la pantalla volcaba el alcance entero; en cuanto pagina,
/// esa suma pasa a ser "el total de esta página" sin que nada lo delate — y este es el
/// reporte que se lleva a planilla. Los totales se calculan aquí, junto a las filas, y
/// la vista solo los imprime.
/// </remarks>
public sealed record TotalesResumenTareo(
    int Trabajadores,
    int DiasPuntual,
    int DiasTardanza,
    int DiasFalta,
    int DiasAusenciaJustificada,
    int DiasIncompleto,
    int DiasNoLaborableTrabajado,
    int MinutosOrdinarios,
    int MinutosExtra25,
    int MinutosExtra35,
    int MinutosNocturnos,
    int MinutosTardanzaAcumulados,
    decimal MontoBonoNocturno,
    decimal MontoMovilidad)
{
    public static TotalesResumenTareo Vacio { get; } =
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0m, 0m);

    /// <summary>
    /// Suma las filas ya calculadas del alcance completo.
    /// </summary>
    /// <remarks>
    /// Se redondea a horas <b>al final</b>, sobre los minutos sumados, y no sumando los
    /// redondeos de cada fila: 200 trabajadores con dos céntimos de hora de diferencia
    /// cada uno descuadran el total en cuatro horas contra lo que da el Excel.
    /// </remarks>
    public static TotalesResumenTareo De(IReadOnlyCollection<ResumenTareoDto> filas)
    {
        ArgumentNullException.ThrowIfNull(filas);

        return new TotalesResumenTareo(
            filas.Count,
            filas.Sum(f => f.DiasPuntual),
            filas.Sum(f => f.DiasTardanza),
            filas.Sum(f => f.DiasFalta),
            filas.Sum(f => f.DiasAusenciaJustificada),
            filas.Sum(f => f.DiasIncompleto),
            filas.Sum(f => f.DiasNoLaborableTrabajado),
            filas.Sum(f => f.MinutosOrdinarios),
            filas.Sum(f => f.MinutosExtra25),
            filas.Sum(f => f.MinutosExtra35),
            filas.Sum(f => f.MinutosNocturnos),
            filas.Sum(f => f.MinutosTardanzaAcumulados),
            filas.Sum(f => f.MontoBonoNocturno),
            filas.Sum(f => f.MontoMovilidad));
    }

    public decimal HorasOrdinarias => Math.Round(MinutosOrdinarios / 60m, 2);

    public decimal HorasExtra25 => Math.Round(MinutosExtra25 / 60m, 2);

    public decimal HorasExtra35 => Math.Round(MinutosExtra35 / 60m, 2);

    public decimal HorasNocturnas => Math.Round(MinutosNocturnos / 60m, 2);

    public decimal HorasTardanza => Math.Round(MinutosTardanzaAcumulados / 60m, 2);
}

/// <summary>Una página del resumen, con los totales del alcance completo.</summary>
public sealed record ResumenTareoPagina(
    ResultadoPaginado<ResumenTareoDto> Filas,
    TotalesResumenTareo Totales)
{
    public static ResumenTareoPagina Vacia(int tamanioPagina) =>
        new(ResultadoPaginado.Vacio<ResumenTareoDto>(tamanioPagina), TotalesResumenTareo.Vacio);
}

/// <summary>
/// Fila del reporte de excepciones: un día que necesita revisión.
/// </summary>
/// <remarks>
/// Solo entran los estados que son una anomalía sobre la que alguien debe actuar
/// (falta, tardanza, ausencia justificada, marcación incompleta). Los días de descanso,
/// feriado o sin vínculo no son excepciones: son lo esperado, y meterlos aquí obligaría
/// a RRHH a descartar cientos de filas irrelevantes cada mes.
/// </remarks>
public sealed record ExcepcionDto(
    long TrabajadorId,
    string Trabajador,
    string? IdMarcador,
    DateOnly Fecha,
    string? Turno,
    DateTime? EntradaReal,
    DateTime? SalidaReal,
    EstadoTareo Estado,
    int MinutosTardanza,
    int MinutosSalidaAnticipada,
    string? Observacion);

/// <summary>
/// Encabezado que acompaña toda exportación: qué tramo, con qué filtros, quién, cuándo y
/// con un tareo calculado cuándo.
/// </summary>
/// <remarks>
/// Un reporte sin esta información es un número sin procedencia: nadie puede auditar
/// después de dónde salió, si el periodo seguía abierto cuando se generó, ni —lo que se
/// nos escapaba hasta ahora— si el tareo del que salió ya estaba obsoleto. Ver
/// <see cref="Calculo"/>.
///
/// <see cref="PeriodoCodigo"/>, <see cref="PeriodoNombre"/> y <see cref="PeriodoAbierto"/>
/// vienen en nulo cuando el alcance no es un periodo (un mes calendario, un día suelto o
/// un rango libre no tienen estado de apertura): en ese caso lo que ubica al reporte es
/// <see cref="Alcance"/>.
/// </remarks>
public sealed record MetadatosReporte(
    string Alcance,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string? PeriodoCodigo,
    string? PeriodoNombre,
    bool? PeriodoAbierto,
    string FiltrosDescripcion,
    DateTime GeneradoEn,
    string GeneradoPor,
    EstadoCalculoTareo Calculo)
{
    /// <summary>Sufijo ISO para el nombre del archivo: ordena bien alfabéticamente.</summary>
    public string SufijoArchivo => FechaInicio == FechaFin
        ? FechaInicio.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
        : $"{FechaInicio:yyyy-MM-dd}_a_{FechaFin:yyyy-MM-dd}";
}

/// <summary>
/// Si el tareo del que sale el reporte sigue reflejando los datos de entrada.
/// </summary>
/// <remarks>
/// <para>
/// El módulo de reportes no recalcula: consulta lo que el motor dejó escrito. Eso hace que
/// un reporte pueda estar desactualizado sin verse distinto —mismos encabezados, mismas
/// columnas, cifras viejas—, y el Excel además circula fuera del sistema, donde ya no hay
/// pantalla que avise. La única defensa es que el propio reporte declare de qué momento es.
/// </para>
/// <para>
/// <see cref="UltimoCambioRelevante"/> es lo más nuevo entre lo que puede mover el
/// resultado del periodo: una marcación cargada, una carga anulada o reactivada, y una
/// justificación creada o modificada. Si algo de eso es posterior a
/// <see cref="CalculadoMasReciente"/>, hay que recalcular antes de entregar.
/// </para>
/// </remarks>
public sealed record EstadoCalculoTareo(
    DateTime? CalculadoMasReciente,
    DateTime? UltimoCambioRelevante,
    string? MotivoUltimoCambio)
{
    /// <summary>No hay ni una fila de tareo en el alcance: nunca se calculó.</summary>
    public bool SinCalcular => CalculadoMasReciente is null;

    /// <summary>Entró un dato después del último cálculo.</summary>
    public bool Desactualizado =>
        CalculadoMasReciente is { } calculado
        && UltimoCambioRelevante is { } cambio
        && cambio > calculado;

    public bool EstaAlDia => !SinCalcular && !Desactualizado;

    /// <summary>Aviso listo para pantalla y para el encabezado del Excel, o <c>null</c>.</summary>
    public string? Advertencia => this switch
    {
        { SinCalcular: true } =>
            "No hay tareo calculado para este alcance: el reporte sale vacío hasta que se "
            + "calcule el periodo desde Tareo.",
        { Desactualizado: true } =>
            $"⚠️ Tareo desactualizado: se calculó el {CalculadoMasReciente:dd/MM/yyyy HH:mm} y "
            + $"después hubo {MotivoUltimoCambio} ({UltimoCambioRelevante:dd/MM/yyyy HH:mm}). "
            + "Recalcule el periodo antes de entregar este reporte.",
        _ => null
    };

    /// <summary>El estado cuando el alcance no tiene ni una fila de tareo.</summary>
    public static EstadoCalculoTareo NuncaCalculado { get; } = new(null, null, null);
}
