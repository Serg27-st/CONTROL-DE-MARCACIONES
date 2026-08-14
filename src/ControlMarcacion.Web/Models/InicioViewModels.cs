using ControlMarcacion.Application.Inicio;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo de la portada.</summary>
/// <remarks>
/// Los cuatro permisos vienen resueltos desde el controlador y no se calculan en la vista.
/// La portada es la única pantalla abierta a todos los roles, así que decide qué tarjetas
/// dibuja según lo que cada uno puede <b>abrir</b>: una tarjeta que enlaza a un 403 es
/// peor que no mostrarla. El control real sigue siendo el <c>[Authorize]</c> de cada
/// controlador.
/// </remarks>
public sealed class PanelInicioVm
{
    public PanelInicioDto Panel { get; init; } = PanelInicioDto.Vacio;

    /// <summary>Coordinador o administrador: gestionan el tareo día a día.</summary>
    public bool GestionaTareo { get; init; }

    /// <summary>Los anteriores más el trabajador social.</summary>
    public bool VeSolicitudes { get; init; }

    /// <summary>Coordinador o gerencia.</summary>
    public bool VeReportes { get; init; }

    /// <summary>
    /// Si esta persona ve alguna tarjeta. Un rol sin ninguna recibe la guía de siempre,
    /// no un panel vacío.
    /// </summary>
    public bool VePanel => GestionaTareo || VeSolicitudes || VeReportes;
}
