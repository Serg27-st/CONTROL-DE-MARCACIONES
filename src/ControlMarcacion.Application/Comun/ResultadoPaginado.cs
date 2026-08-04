namespace ControlMarcacion.Application.Comun;

/// <summary>
/// Página de resultados de un listado.
/// </summary>
/// <remarks>
/// Todo listado del sistema pagina, sin excepción: las tablas de marcaciones y tareos
/// crecen a miles de filas por periodo y una pantalla sin paginación termina tumbando
/// el navegador del usuario.
/// </remarks>
public sealed record ResultadoPaginado<T>(
    IReadOnlyList<T> Elementos,
    int TotalElementos,
    int Pagina,
    int TamanioPagina)
{
    public int TotalPaginas => TamanioPagina <= 0
        ? 0
        : (int)Math.Ceiling(TotalElementos / (double)TamanioPagina);

    public bool HayPaginaAnterior => Pagina > 1;

    public bool HayPaginaSiguiente => Pagina < TotalPaginas;
}

/// <summary>
/// Fábricas de <see cref="ResultadoPaginado{T}"/>.
/// </summary>
/// <remarks>
/// Van en una clase no genérica aparte porque los analizadores rechazan miembros
/// estáticos sobre tipos genéricos (CA1000): obligan a escribir el tipo dos veces
/// en la llamada y confunden la resolución de sobrecargas.
/// </remarks>
public static class ResultadoPaginado
{
    public static ResultadoPaginado<T> Vacio<T>(int tamanioPagina) =>
        new([], 0, 1, tamanioPagina);
}
