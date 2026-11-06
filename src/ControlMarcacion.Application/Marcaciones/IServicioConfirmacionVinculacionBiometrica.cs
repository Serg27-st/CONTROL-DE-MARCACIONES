namespace ControlMarcacion.Application.Marcaciones;

public interface IServicioConfirmacionVinculacionBiometrica
{
    Task<PropuestaConfirmacionBiometricaDto> PrepararAsync(
        PrepararConfirmacionBiometricaRequest solicitud,
        CancellationToken cancelacion = default);

    Task<ResultadoConfirmacionBiometricaLote> ConfirmarLoteAsync(
        ConfirmarVinculacionesBiometricasRequest solicitud,
        CancellationToken cancelacion = default);
}

public interface IContextoAutorizacionVinculacionBiometrica
{
    bool EstaAutenticado { get; }
    bool EsCoordinador { get; }
    string Usuario { get; }
}

/// <summary>Fuente servidor; una referencia enviada por el cliente nunca basta por sí sola.</summary>
public interface IProveedorEvidenciaIdentidad
{
    Task<EvidenciaIdentidadVerificada?> VerificarAsync(
        FuenteEvidenciaDocumento fuente,
        string referencia,
        CancellationToken cancelacion = default);
}

public sealed record EvidenciaIdentidadVerificada(
    FuenteEvidenciaDocumento Fuente,
    string Referencia,
    string Huella,
    long TrabajadorId,
    long TipoDocumentoId,
    string NumeroDocumento,
    DateTime VerificadaEn,
    DateTime? ExpiraEn,
    DateOnly? AcreditaHistoriaDesde,
    DateOnly? AcreditaHistoriaHasta);

public sealed record ReferenciaEvidenciaRegistro(
    int IndiceRegistro,
    FuenteEvidenciaDocumento Fuente,
    string Referencia);

public sealed record PrepararConfirmacionBiometricaRequest(
    long EquipoId,
    ResultadoLecturaUsuariosZkteco Lectura,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    IReadOnlyList<ReferenciaEvidenciaRegistro> Evidencias);

public sealed record ConfirmarVinculacionesBiometricasRequest(
    Guid ConciliacionId,
    int Version,
    IReadOnlyList<long> Decisiones);

public sealed record PropuestaDecisionBiometricaDto(
    long DecisionId,
    int IndiceRegistro,
    string Pin,
    long? TrabajadorId,
    string? DocumentoCandidato,
    BaseConfirmacionIdentidadBiometrica BaseConfirmacionIdentidad,
    NaturalezaVigenciaBiometrica NaturalezaVigencia,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    string Resultado,
    string? Motivo,
    IReadOnlyList<string> Diagnosticos);

public sealed record PropuestaConfirmacionBiometricaDto(
    Guid ConciliacionId,
    int Version,
    DateTime ExpiraEn,
    IReadOnlyList<PropuestaDecisionBiometricaDto> Decisiones);

public sealed record ResultadoDecisionConfirmacionBiometrica(
    long DecisionId,
    string Resultado,
    string? Motivo,
    long? AsignacionMarcadorId);

public sealed record ResultadoConfirmacionBiometricaLote(
    Guid ConciliacionId,
    int Version,
    IReadOnlyList<ResultadoDecisionConfirmacionBiometrica> Resultados);
