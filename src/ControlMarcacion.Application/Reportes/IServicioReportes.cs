using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Reportes.Dto;

namespace ControlMarcacion.Application.Reportes;

/// <summary>
/// Reportes que consultan el tareo ya calculado.
/// </summary>
/// <remarks>
/// Consulta datos que <see cref="Asistencia.IServicioCalculoTareo"/> ya produjo; no
/// recalcula nada. Si el tareo está desactualizado, el reporte lo está también — la
/// solución es recalcular el periodo, no que el reporte vuelva a hacer el trabajo del motor.
/// </remarks>
public interface IServicioReportes
{
    /// <summary>
    /// Un trabajador por fila, con sus totales del alcance elegido. <b>Alcance completo.</b>
    /// </summary>
    /// <remarks>
    /// Sin paginar a propósito: es lo que consume la exportación a Excel, que tiene que
    /// llevar todas las filas. La pantalla usa <see cref="ResumenTareoPaginadoAsync"/>.
    /// </remarks>
    Task<IReadOnlyList<ResumenTareoDto>> ResumenTareoAsync(
        FiltroReporte filtro,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Una página del resumen, más los totales calculados sobre el alcance completo.
    /// </summary>
    /// <remarks>
    /// Los totales <b>no</b> se sacan de la página devuelta: se calculan sobre todas las
    /// filas del alcance. Es la diferencia entre "el total del mes" y "el total de lo que
    /// alcanzo a ver", y este reporte va a planilla.
    ///
    /// El recorte se hace en memoria y no en SQL, al contrario que en
    /// <see cref="ExcepcionesPaginadasAsync"/>. No es un descuido: el resumen es una fila
    /// por trabajador —cientos, no miles— y sus totales exigen recorrer el alcance entero
    /// de todas formas, porque el bono nocturno se resuelve día por día con la RMV vigente
    /// en cada fecha. Paginar en la base obligaría a hacer ese trabajo dos veces.
    /// Lo que sí se evita, que es el problema real, es mandarle 200 filas de 18 columnas
    /// al navegador de una planta.
    /// </remarks>
    Task<ResumenTareoPagina> ResumenTareoPaginadoAsync(
        FiltroReporte filtro,
        int pagina,
        int tamanioPagina,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Un día por fila: solo los que necesitan revisión. <b>Alcance completo.</b>
    /// </summary>
    /// <remarks>
    /// Sin paginar a propósito, igual que <see cref="ResumenTareoAsync"/>: alimenta la
    /// exportación a Excel. La pantalla usa <see cref="ExcepcionesPaginadasAsync"/>.
    /// </remarks>
    Task<IReadOnlyList<ExcepcionDto>> ExcepcionesAsync(
        FiltroReporte filtro,
        CancellationToken cancelacion = default);

    /// <summary>Una página de excepciones, recortada en la base.</summary>
    /// <remarks>
    /// Aquí el recorte sí baja a SQL: las excepciones son un día por fila y un mes con
    /// problemas son decenas de miles. A diferencia del resumen, no hay ningún total que
    /// obligue a materializar el resto.
    /// </remarks>
    Task<ResultadoPaginado<ExcepcionDto>> ExcepcionesPaginadasAsync(
        FiltroReporte filtro,
        int pagina,
        int tamanioPagina,
        CancellationToken cancelacion = default);

    /// <summary>
    /// El tareo final en el formato que GCA entrega a planilla: las tres hojas del archivo
    /// modelo, ya armadas.
    /// </summary>
    /// <remarks>
    /// A diferencia de los otros dos, <b>exige un periodo</b> y rechaza los alcances por mes,
    /// día o rango libre. No es una limitación técnica: los reintegros se cargan por
    /// trabajador y periodo, y el estado abierto/cerrado que el encabezado declara también
    /// es del periodo. Emitir el libro sobre medio mes produciría un entregable de planilla
    /// sin sus reintegros y sin saber si está congelado — mejor negarse con un mensaje que
    /// entregarlo incompleto.
    /// </remarks>
    Task<TareoFinalDto> TareoFinalAsync(
        FiltroReporte filtro,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Encabezado de trazabilidad para acompañar la exportación, incluido el estado de
    /// vigencia del tareo (ver <see cref="EstadoCalculoTareo"/>).
    /// </summary>
    Task<MetadatosReporte> ObtenerMetadatosAsync(
        FiltroReporte filtro,
        string usuario,
        CancellationToken cancelacion = default);
}

/// <summary>
/// Exportación a Excel de los reportes.
/// </summary>
/// <remarks>
/// La interfaz vive en Application; la implementación con ClosedXML vive en
/// Infrastructure, porque Application no referencia paquetes externos — solo Domain.
/// </remarks>
public interface IExportadorReportesExcel
{
    byte[] ExportarResumenTareo(MetadatosReporte metadatos, IReadOnlyList<ResumenTareoDto> filas);

    byte[] ExportarExcepciones(MetadatosReporte metadatos, IReadOnlyList<ExcepcionDto> filas);

    /// <summary>
    /// El libro del tareo final: hojas <c>Tareo</c>, <c>Descanso Medico</c> y
    /// <c>Vacaciones-Licencias</c>, con la estructura del archivo modelo de GCA.
    /// </summary>
    byte[] ExportarTareoFinal(MetadatosReporte metadatos, TareoFinalDto tareo);
}
