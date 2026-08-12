namespace ControlMarcacion.Application.Catalogos;

/// <summary>
/// Fuente externa de feriados nacionales, para la sincronización manual del catálogo local.
/// </summary>
/// <remarks>
/// La implementación real vive en Infrastructure (llama por HTTP a un proveedor público) y
/// puede fallar por falta de conectividad: la planta puede no tener salida a internet. El
/// cálculo del tareo <b>nunca</b> pasa por esta interfaz — siempre lee la tabla
/// <c>Feriado</c> local; esto solo alimenta esa tabla bajo pedido explícito de un usuario.
/// </remarks>
public interface IProveedorFeriadosExternos
{
    Task<IReadOnlyList<FeriadoExternoDto>> ObtenerAsync(int anio, CancellationToken cancelacion = default);
}

/// <summary>Un feriado tal como lo entrega el proveedor externo.</summary>
public sealed record FeriadoExternoDto(DateOnly Fecha, string Descripcion);
