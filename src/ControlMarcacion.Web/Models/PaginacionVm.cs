using System.Globalization;
using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Web.Models;

/// <summary>
/// Lo que necesita el parcial <c>_Paginacion</c> para dibujar el pie de un listado.
/// </summary>
/// <remarks>
/// Existe para que los siete listados paginados del sistema tengan <b>un solo</b> control de
/// páginas. Antes cada vista traía el suyo: Personal y Tareo con botones y el rango
/// «Mostrando 51–100 de 155»; Solicitudes, Justificaciones y Auditoría con dos enlaces de
/// texto sueltos y sin rango. Además cada una rearmaba a mano el diccionario de filtros
/// —cinco copias de la misma idea—, que es la forma segura de que «Siguiente» acabe
/// perdiendo el filtro que el usuario acababa de aplicar.
///
/// La clave de página la escribe el parcial: las vistas solo aportan sus filtros. El
/// enlace <b>omite</b> la página cuando es la 1, porque todas las acciones reciben
/// <c>int pagina</c> y un 0 ausente ya se resuelve como la primera.
/// </remarks>
public sealed class PaginacionVm
{
    /// <summary>Nombre del parámetro de página en la query string.</summary>
    /// <remarks>
    /// Uno solo para todo el sistema. El enlace de modelos de MVC no distingue
    /// mayúsculas, así que <c>pagina</c> alimenta igual a un parámetro <c>Pagina</c>.
    /// </remarks>
    public const string ClavePagina = "pagina";

    public required int Pagina { get; init; }

    public required int TotalPaginas { get; init; }

    public required int TotalElementos { get; init; }

    public required int TamanioPagina { get; init; }

    /// <summary>Filas que trae realmente esta página.</summary>
    /// <remarks>
    /// Hace falta porque el número de página viene de la URL y puede apuntar fuera de
    /// rango. Calculando el «desde» solo con la aritmética de la página, pedir la 2 de un
    /// listado de 5 filas producía «Mostrando 26–5 de 5»: el inicio salía de la página
    /// pedida y el fin del total real.
    /// </remarks>
    public required int ElementosEnPagina { get; init; }

    /// <summary>Acción a la que apuntan «Anterior» y «Siguiente».</summary>
    public required string Accion { get; init; }

    /// <summary>Controlador, o <c>null</c> para el de la vista actual.</summary>
    public string? Controlador { get; init; }

    /// <summary>
    /// Filtros que hay que conservar al cambiar de página, <b>sin</b> la página.
    /// </summary>
    public required IReadOnlyDictionary<string, string?> Filtros { get; init; }

    /// <summary>Qué se está contando: «trabajador(es)», «solicitud(es)»…</summary>
    public required string Sustantivo { get; init; }

    public int Desde => ElementosEnPagina == 0 ? 0 : ((Pagina - 1) * TamanioPagina) + 1;

    public int Hasta => ElementosEnPagina == 0 ? 0 : Desde + ElementosEnPagina - 1;

    /// <summary>
    /// La página pedida no tiene filas aunque el listado sí las tenga: número fuera de
    /// rango en la URL. Se muestra el total sin rango, en vez de un rango imposible.
    /// </summary>
    public bool PaginaFueraDeRango => ElementosEnPagina == 0 && TotalElementos > 0;

    public bool HayPaginaAnterior => Pagina > 1;

    public bool HayPaginaSiguiente => Pagina < TotalPaginas;

    /// <summary>Con una sola página sobra el «Página 1 de 1» y los dos botones ausentes.</summary>
    public bool HayVariasPaginas => TotalPaginas > 1;

    /// <summary>Los filtros más la página pedida, listos para <c>asp-all-route-data</c>.</summary>
    public Dictionary<string, string?> RutaHacia(int pagina)
    {
        var ruta = new Dictionary<string, string?>(Filtros, StringComparer.OrdinalIgnoreCase);

        // La página 1 no viaja: deja la URL limpia al volver al principio, y el binding
        // resuelve la ausencia como primera página.
        if (pagina > 1)
        {
            ruta[ClavePagina] = pagina.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            ruta.Remove(ClavePagina);
        }

        return ruta;
    }

    /// <summary>Arma el modelo a partir de una página de resultados.</summary>
    /// <remarks>
    /// Genérico para aceptar cualquier <see cref="ResultadoPaginado{T}"/>, pero el modelo
    /// que sale no lo es: el parcial no necesita saber qué hay dentro de las filas.
    /// </remarks>
    public static PaginacionVm De<T>(
        ResultadoPaginado<T> resultado,
        string accion,
        string sustantivo,
        IReadOnlyDictionary<string, string?>? filtros = null,
        string? controlador = null)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        return new PaginacionVm
        {
            Pagina = resultado.Pagina,
            TotalPaginas = resultado.TotalPaginas,
            TotalElementos = resultado.TotalElementos,
            TamanioPagina = resultado.TamanioPagina,
            ElementosEnPagina = resultado.Elementos.Count,
            Accion = accion,
            Controlador = controlador,
            Sustantivo = sustantivo,
            Filtros = filtros ?? new Dictionary<string, string?>()
        };
    }
}
