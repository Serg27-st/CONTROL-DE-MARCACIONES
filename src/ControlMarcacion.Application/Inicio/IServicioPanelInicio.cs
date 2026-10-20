using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Application.Reportes.Dto;

namespace ControlMarcacion.Application.Inicio;

/// <summary>
/// El estado del mes en curso, reunido para la portada.
/// </summary>
/// <remarks>
/// No calcula nada nuevo: junta lo que ya producen los servicios de cada módulo. La
/// portada era una tabla de enlaces —"Personal · Disponible", "Tareo · Disponible"— que no
/// decía si el mes iba bien. Para saberlo había que entrar a cuatro pantallas: el periodo
/// abierto en Periodos, los días sin cargar en Marcaciones, las faltas en Tareo y las
/// solicitudes esperando en Solicitudes.
///
/// Vive en <c>Application</c> y no en el controlador porque es orquestación de casos de
/// uso, no presentación: qué se considera "el periodo actual" o "un mes con huecos" es una
/// decisión de negocio, y repetirla en una vista Razor la dejaría sin pruebas.
/// </remarks>
public interface IServicioPanelInicio
{
    /// <summary>Estado del periodo más reciente y de lo que cuelga de él.</summary>
    /// <param name="usuario">Quién mira, para el sello de trazabilidad del estado del cálculo.</param>
    Task<PanelInicioDto> ObtenerAsync(string usuario, CancellationToken cancelacion = default);
}

/// <summary>Lo que la portada necesita para responder «¿cómo va el mes?».</summary>
/// <remarks>
/// Todo puede venir en <c>null</c>: un sistema recién instalado no tiene periodos, y sin
/// periodo no hay cobertura ni tareo que resumir. La portada tiene que servir también ese
/// día, diciendo qué falta configurar.
/// </remarks>
public sealed record PanelInicioDto(
    PeriodoActualDto? Periodo,
    CoberturaDeRango? Marcaciones,
    TareoDelPeriodoDto? Tareo,
    int SolicitudesPendientes)
{
    public static PanelInicioDto Vacio { get; } = new(null, null, null, 0);

    public bool HayPeriodo => Periodo is not null;

    /// <summary>
    /// Hay algo que exige atención ahora mismo: huecos de marcación, tareo desactualizado
    /// o solicitudes esperando decisión.
    /// </summary>
    public bool HayPendientes =>
        Marcaciones?.HayHuecos == true
        || Tareo?.Calculo.EstaAlDia == false
        || Tareo?.PendientesDeValidar > 0
        || SolicitudesPendientes > 0;
}

/// <summary>El periodo sobre el que se está trabajando.</summary>
public sealed record PeriodoActualDto(
    long Id,
    string Codigo,
    string Nombre,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    bool EstaAbierto);

/// <summary>Cómo va el tareo del periodo, y de cuándo son sus cifras.</summary>
public sealed record TareoDelPeriodoDto(
    int Trabajadores,
    int DiasCalculados,
    int Faltas,
    int Tardanzas,
    int MinutosExtra,
    int PendientesDeValidar,
    EstadoCalculoTareo Calculo);
