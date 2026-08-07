using ControlMarcacion.Application.Catalogos;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo de la pantalla de feriados: listado de un año más el botón de sincronizar.</summary>
public sealed class ListaFeriadosVm
{
    public int Anio { get; init; }

    public IReadOnlyList<FeriadoDto> Feriados { get; init; } = [];
}
