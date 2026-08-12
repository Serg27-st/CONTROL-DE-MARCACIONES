using ControlMarcacion.Application.Marcaciones.Dto;

namespace ControlMarcacion.Application.Marcaciones;

/// <summary>
/// Carga de marcaciones desde un archivo del marcador.
/// </summary>
public interface IServicioImportacion
{
    /// <summary>
    /// Indica si este contenido exacto ya se importó antes, comparando su hash.
    /// </summary>
    /// <remarks>
    /// Permite avisar "este archivo ya se cargó el 12/07 a las 14:30" <b>antes</b> de
    /// procesarlo, en vez de que el usuario lo descubra por dos mil filas duplicadas.
    /// </remarks>
    Task<ImportacionPrevia?> BuscarImportacionPreviaAsync(
        Stream contenido,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Lee el archivo, resuelve cada marcación contra el padrón y persiste las nuevas.
    /// </summary>
    Task<ResultadoImportacion> ImportarAsync(
        Stream contenido,
        string nombreArchivo,
        string usuario,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<ImportacionHistorialDto>> HistorialAsync(
        int maximo = 50,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Qué días tienen marcaciones cargadas, agrupado por año, mes y día.
    /// </summary>
    /// <remarks>
    /// El historial de cargas responde "qué archivos subí"; esto responde la pregunta que
    /// de verdad se hace antes de calcular un tareo: <b>"¿qué días me faltan?"</b>. No son
    /// lo mismo —un archivo puede traer medio mes, o tres— y contarlos por archivo no
    /// permite ver un hueco de tres días en mitad del periodo.
    /// </remarks>
    Task<CoberturaMarcaciones> CoberturaAsync(
        int anio,
        int mes,
        CancellationToken cancelacion = default);

    Task<ResultadoImportacion?> ObtenerResultadoAsync(
        long importacionId,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Deja una carga sin efecto sobre el cálculo, sin borrar ninguna de sus marcaciones.
    /// </summary>
    /// <remarks>
    /// Es lo que se usa cuando se subió el archivo equivocado. No hay una operación de
    /// borrado y no debe haberla: las marcaciones crudas son inmutables por diseño, y
    /// eliminarlas dejaría tareos ya calculados sin nada que los sustente. Anular conserva
    /// el archivo, sus filas y sus errores —todo sigue consultable— y solo hace que el
    /// motor deje de contarlas.
    ///
    /// <b>Recalcular el periodo después es obligatorio</b>: anular no toca los tareos ya
    /// calculados, solo cambia lo que se leerá la próxima vez.
    /// </remarks>
    Task AnularAsync(
        long importacionId,
        string motivo,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>Revierte una anulación: sus marcaciones vuelven a contar.</summary>
    Task ReactivarAsync(
        long importacionId,
        string usuario,
        CancellationToken cancelacion = default);
}
