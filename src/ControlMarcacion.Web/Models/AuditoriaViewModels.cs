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
}
