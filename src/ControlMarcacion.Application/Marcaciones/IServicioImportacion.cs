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
    /// <param name="plantaId">Planta declarada por quien carga.</param>
    /// <param name="equipoId">Aparato del que se extrajo el archivo.</param>
    /// <remarks>
    /// ⚠️ <b>Planta y equipo son obligatorios y no se deducen del archivo.</b> El TXT del
    /// G3 no trae ninguna columna que diga de qué aparato salió, y en una planta con dos
    /// marcadores el mismo código identifica a dos personas distintas. Sin declararlo, la
    /// resolución ID → trabajador es ambigua y funde las jornadas de ambas.
    ///
    /// La planta es redundante —el equipo ya la conoce— y esa redundancia es el control:
    /// si no coinciden, la carga se rechaza en vez de atribuir el archivo a otra planta.
    /// </remarks>
    Task<ResultadoImportacion> ImportarAsync(
        Stream contenido,
        string nombreArchivo,
        long plantaId,
        long equipoId,
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

    /// <summary>
    /// La misma pregunta que <see cref="CoberturaAsync"/>, pero sobre un rango arbitrario
    /// y resumida en tres cifras.
    /// </summary>
    /// <remarks>
    /// Existe aparte porque el calendario recorre el <b>año entero</b> para poder pintar
    /// las doce pastillas de mes, y quien solo necesita "¿cuántos días del periodo me
    /// faltan?" no tiene por qué pagar ese barrido. Además un periodo de planilla no tiene
    /// por qué caber en un mes calendario.
    ///
    /// Cuenta <b>solo cargas vigentes</b>, igual que el calendario y que el motor: las
    /// marcaciones de una importación anulada existen pero no alimentan ningún tareo.
    /// </remarks>
    /// <summary>
    /// Por cada día del rango, los aparatos de los que se esperaba archivo y no llegó.
    /// </summary>
    /// <remarks>
    /// Lo consume el motor de cálculo: un día sin archivo del aparato de una persona no es
    /// una falta suya, es un dato que no llegó. Vive aquí y no en Asistencia para que haya
    /// <b>una sola regla de cobertura</b> en el sistema — la misma que pinta el calendario
    /// de Marcaciones y la tarjeta de la portada—. Si divergieran, el calendario diría que
    /// el mes está cubierto y el tareo sacaría faltas de los días que faltan.
    ///
    /// Los días sin faltantes no aparecen en el diccionario.
    /// </remarks>
    Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<long>>> AparatosSinCargaAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion = default);

    Task<CoberturaDeRango> CoberturaDeRangoAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion = default);

    Task<ResultadoImportacion?> ObtenerResultadoAsync(
        long importacionId,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Números de aparato con marcaciones vigentes en el rango que ningún trabajador tenía
    /// asignados en esas fechas.
    /// </summary>
    /// <remarks>
    /// Se calcula en vivo con la misma regla que el motor, así que se vacía sola a medida
    /// que se asignan los números: no depende de lo que se avisó al cargar el archivo.
    /// </remarks>
    Task<IReadOnlyList<NumeroSinAsignarDto>> NumerosSinAsignarAsync(
        DateOnly desde,
        DateOnly hasta,
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
