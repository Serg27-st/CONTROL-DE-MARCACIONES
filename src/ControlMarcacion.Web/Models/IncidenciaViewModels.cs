using System.Globalization;
using ControlMarcacion.Application.Asistencia.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Asistencia;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControlMarcacion.Web.Models;

/// <summary>Bandeja de incidencias: los días que esperan una decisión.</summary>
public sealed class BandejaIncidenciasVm
{
    public ResultadoPaginado<IncidenciaDto> Resultado { get; init; } =
        ResultadoPaginado.Vacio<IncidenciaDto>(50);

    public long? PeriodoId { get; init; }

    /// <summary>
    /// Acotado a una persona. Es como se llega desde el tareo: con el día delante, lo que
    /// se quiere es resolver lo de esa persona, no revisar la planta entera.
    /// </summary>
    public long? TrabajadorId { get; init; }

    public TipoIncidencia? Tipo { get; init; }

    public bool SoloPendientes { get; init; } = true;

    /// <summary>
    /// Quien mira puede aprobar o rechazar. Decide qué botones se dibujan, no qué se
    /// permite: eso lo decide el <c>[Authorize]</c> del controlador.
    /// </summary>
    public bool PuedeResolver { get; init; }

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Periodos { get; init; } = [];

    /// <summary>Filtros activos, para conservarlos al cambiar de página.</summary>
    public Dictionary<string, string?> RutaFiltros() => new()
    {
        ["periodoId"] = PeriodoId?.ToString(CultureInfo.InvariantCulture),
        ["trabajadorId"] = TrabajadorId?.ToString(CultureInfo.InvariantCulture),
        ["tipo"] = Tipo is null ? null : ((int)Tipo).ToString(CultureInfo.InvariantCulture),
        // Solo viaja cuando se apagó: el valor por omisión es «solo pendientes».
        ["soloPendientes"] = SoloPendientes ? null : "false"
    };

    /// <summary>Nombre legible de cada incidencia, con el verbo de lo que hay que hacer.</summary>
    public static string Nombre(TipoIncidencia? tipo) => tipo switch
    {
        TipoIncidencia.IngresoNoRegistrado => "Ingreso no registrado",
        TipoIncidencia.SalidaNoRegistrada => "Salida no registrada",
        TipoIncidencia.FueraDeTurno => "Marcaciones fuera del turno",
        TipoIncidencia.TurnoDiscrepante => "El turno no coincide",
        TipoIncidencia.SinCoberturaDeCarga => "Falta cargar el archivo del marcador",
        _ => "—"
    };

    /// <summary>
    /// Qué hay que hacer con cada incidencia. La bandeja tiene que decirlo: el nombre
    /// describe el problema, esto describe la salida.
    /// </summary>
    public static string QueHacer(TipoIncidencia? tipo) => tipo switch
    {
        TipoIncidencia.IngresoNoRegistrado =>
            "Verifique a qué hora entró y regularice el ingreso.",
        TipoIncidencia.SalidaNoRegistrada =>
            "Verifique a qué hora salió y regularice la salida.",
        TipoIncidencia.FueraDeTurno =>
            "Revise si esas marcas son de otro turno o de otra fecha antes de regularizar.",
        TipoIncidencia.TurnoDiscrepante =>
            "Corrija la rotación en la cuadrilla o en la ficha; el día se recalcula solo.",
        TipoIncidencia.SinCoberturaDeCarga =>
            "Cargue el archivo de ese marcador y recalcule. Regularice solo si el archivo no existe.",
        _ => string.Empty
    };
}
