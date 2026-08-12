using ControlMarcacion.Application.Catalogos.Dto;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo del listado de tipos de ausencia.</summary>
public sealed class ListaTiposAusenciaVm
{
    public IReadOnlyList<TipoAusenciaListaDto> Tipos { get; init; } = [];

    public bool IncluirInactivos { get; init; }
}

/// <summary>Modelo del formulario de alta y edición de un tipo de ausencia.</summary>
/// <remarks>
/// Las opciones de comportamiento se arman en la vista y no aquí: van acompañadas de una
/// explicación de qué hace cada una con el cálculo, y tenerlas en dos sitios garantizaba
/// que tarde o temprano dijeran cosas distintas.
/// </remarks>
public sealed class EditarTipoAusenciaVm
{
    public GuardarTipoAusenciaRequest Datos { get; set; } = new();

    public bool EsNuevo => Datos.Id is null;
}
