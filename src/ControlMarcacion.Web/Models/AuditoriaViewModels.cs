using System.Globalization;
using ControlMarcacion.Application.Auditoria.Dto;
using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo de la pantalla de bitácora de auditoría.</summary>
public sealed class ListaAuditoriaVm
{
    public FiltroAuditoria Filtro { get; init; } = new();

    public ResultadoPaginado<RegistroAuditoriaDto> Resultado { get; init; } =
        ResultadoPaginado.Vacio<RegistroAuditoriaDto>(50);

    /// <summary>Tablas que ya registraron movimientos, para el desplegable del filtro.</summary>
    public IReadOnlyList<string> Tablas { get; init; } = [];

    /// <summary>Filtros activos, para conservarlos al cambiar de página.</summary>
    /// <remarks>
    /// Las fechas viajan en ISO y no en <c>dd/MM/yyyy</c>: una URL no es interfaz, la lee
    /// el binding del servidor.
    /// </remarks>
    public Dictionary<string, string?> RutaFiltros() => new()
    {
        ["Tabla"] = Filtro.Tabla,
        ["Usuario"] = Filtro.Usuario,
        ["ClaveRegistro"] = Filtro.ClaveRegistro,
        ["Operacion"] = Filtro.Operacion?.ToString(),
        ["Desde"] = Filtro.Desde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["Hasta"] = Filtro.Hasta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    };
}
