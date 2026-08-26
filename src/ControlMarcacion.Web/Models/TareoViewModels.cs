using System.Globalization;
using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Asistencia;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo de la pantalla de tareo.</summary>
/// <remarks>
/// El listado <b>pagina</b> y el resumen viene del servidor calculado sobre el periodo
/// completo. Antes los totales se sumaban aquí, sobre <c>Tareos</c>: eso solo funcionaba
/// porque la pantalla volcaba el periodo entero en una sola tabla —decenas de miles de
/// filas— y habría empezado a mentir en cuanto se paginara.
/// </remarks>
public sealed class TareoVm
{
    public IReadOnlyList<PeriodoListaDto> Periodos { get; init; } = [];

    /// <summary>Una página de <b>trabajadores</b>, cada uno con todos sus días.</summary>
    public ResultadoPaginado<TareoDeTrabajador> Resultado { get; init; } =
        ResultadoPaginado.Vacio<TareoDeTrabajador>(10);

    /// <summary>Días que trae la página, sumando los de todos sus trabajadores.</summary>
    /// <remarks>
    /// El total de la paginación cuenta personas, no filas; esto es lo que hace falta para
    /// decir «10 trabajadores · 310 días» y que nadie confunda las dos cifras.
    /// </remarks>
    public int DiasEnLaPagina => Resultado.Elementos.Sum(t => t.TotalDias);

    /// <summary>Totales y reparto por estado del periodo, no de la página visible.</summary>
    public ResumenTareo Resumen { get; init; } = ResumenTareo.Vacio;

    public long? PeriodoId { get; init; }

    public EstadoTareo? Estado { get; init; }

    public string? Busqueda { get; init; }

    /// <summary>Minutos por concepto con la tasa vigente aplicada.</summary>
    public LiquidacionPeriodo? Liquidacion { get; init; }

    public PeriodoListaDto? PeriodoSeleccionado =>
        Periodos.FirstOrDefault(p => p.Id == PeriodoId);

    public bool HayFiltroActivo => Estado is not null || !string.IsNullOrWhiteSpace(Busqueda);

    /// <summary>
    /// Filtros activos, con el estado que se indique. <b>Sin la página.</b>
    /// </summary>
    /// <remarks>
    /// Lo usan las pastillas de estado —que filtran cambiando solo ese parámetro— y el
    /// pie de paginación, que agrega la página por su cuenta. Se arma una vez aquí y no
    /// en cada enlace de la vista: repetir el diccionario es la forma segura de que uno
    /// de ellos se quede sin un filtro y descarte lo que el usuario acababa de escribir.
    ///
    /// Cambiar de estado vuelve a la página 1 sin decirlo: al no llevar página, la acción
    /// recibe 0 y lo resuelve como la primera. Es lo correcto — la página 7 de «todos»
    /// no tiene por qué existir en «faltas».
    /// </remarks>
    public Dictionary<string, string?> RutaFiltros(EstadoTareo? estado) => new()
    {
        ["periodoId"] = PeriodoId?.ToString(CultureInfo.InvariantCulture),
        ["estado"] = estado is null ? null : ((int)estado).ToString(CultureInfo.InvariantCulture),
        ["busqueda"] = Busqueda
    };

    /// <summary>Los filtros tal como están, para el pie de paginación.</summary>
    public Dictionary<string, string?> RutaFiltros() => RutaFiltros(Estado);
}
