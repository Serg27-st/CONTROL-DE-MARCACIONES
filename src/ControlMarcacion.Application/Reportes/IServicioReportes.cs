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
    /// <summary>Un trabajador por fila, con sus totales del periodo.</summary>
    Task<IReadOnlyList<ResumenTareoDto>> ResumenTareoAsync(
        FiltroReporte filtro,
        CancellationToken cancelacion = default);

    /// <summary>Un día por fila: solo los que necesitan revisión.</summary>
    Task<IReadOnlyList<ExcepcionDto>> ExcepcionesAsync(
        FiltroReporte filtro,
        CancellationToken cancelacion = default);

    /// <summary>
    /// El tareo final en el formato que GCA entrega a planilla: las tres hojas del archivo
    /// modelo, ya armadas.
    /// </summary>
    Task<TareoFinalDto> TareoFinalAsync(
        FiltroReporte filtro,
        CancellationToken cancelacion = default);

    /// <summary>Encabezado de trazabilidad para acompañar la exportación.</summary>
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
