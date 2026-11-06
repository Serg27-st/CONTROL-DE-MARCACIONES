namespace ControlMarcacion.Application.Marcaciones;

/// <summary>Simula la vinculación de usuarios de un equipo sin guardar cambios.</summary>
public interface IServicioConciliacionBiometrica
{
    Task<ResultadoConciliacionBiometrica> ConciliarAsync(
        SolicitudConciliacionBiometrica solicitud,
        CancellationToken cancelacion = default);
}

public sealed record SolicitudConciliacionBiometrica(
    long EquipoId,
    ResultadoLecturaUsuariosZkteco Lectura,
    DateOnly? FechaEfectiva,
    NaturalezaVigenciaBiometrica NaturalezaVigencia,
    IReadOnlyList<EvidenciaDocumentoConciliacion> Evidencias);

public enum NaturalezaVigenciaBiometrica
{
    NoInformada,
    Declarada,
    Acreditada
}

public enum BaseConfirmacionIdentidadBiometrica
{
    Ninguna,
    ReglaOperativaUserU,
    EvidenciaDocumental
}

/// <summary>
/// Evidencia externa a <c>user.u</c>. El cliente futuro no debe poder autodeclararla:
/// deberá provenir de un flujo autorizado que todavía no forma parte de esta fase.
/// </summary>
public sealed record EvidenciaDocumentoConciliacion(
    int IndiceRegistro,
    string NumeroDocumento,
    long? TipoDocumentoId,
    FuenteEvidenciaDocumento Fuente);

public enum FuenteEvidenciaDocumento
{
    Ninguna,
    PadronControlado,
    VerificacionDocumental
}

public enum EstadoConciliacionBiometrica
{
    CoincidenciaVerificable,
    CoincidenciaOperativaUserUConfirmable,
    CandidatoDocumentoNoVerificado,
    YaVinculadoCorrectamente,
    TrabajadorNoEncontrado,
    IdentificadorBiometricoSinCorrespondencia,
    DocumentoAusente,
    DocumentoAmbiguoODuplicado,
    PinDuplicadoDentroDelEquipo,
    PinAsignadoAOtroTrabajador,
    TrabajadorAsignadoAOtroEquipo,
    ConflictoDeVigencia,
    RegistroInvalidoOIncompleto,
    PropuestaCondicionada
}

public enum DiagnosticoConciliacionBiometrica
{
    RegistroInvalido,
    DocumentoAusente,
    DocumentoNoVerificado,
    DocumentoVerificadoPorEvidenciaExterna,
    DocumentoCoincideExactamentePorReglaOperativa,
    TipoDocumentoNoCompatibleConReglaDni,
    DniDuplicadoDentroDelArchivo,
    DocumentoAmbiguo,
    TrabajadorNoEncontrado,
    PinDuplicadoDentroDelArchivo,
    PinSuperaLongitudPermitida,
    PinSinAsignacionVigente,
    AsignacionCorrectaExistente,
    PinAsignadoAOtroTrabajador,
    TrabajadorAsignadoAOtroEquipo,
    TrabajadorAsignadoAOtroPin,
    AsignacionesSuperpuestas,
    FechaEfectivaFutura,
    FechaEfectivaNoAcreditada,
    FechaEfectivaDeclaradaNoAcreditada
}

public sealed record ResultadoRegistroConciliacionBiometrica(
    int IndiceRegistro,
    ulong? Uid,
    string Pin,
    string? ValorNameOriginal,
    string? DocumentoCandidato,
    bool DocumentoVerificado,
    BaseConfirmacionIdentidadBiometrica BaseConfirmacionIdentidad,
    long? TrabajadorId,
    EstadoConciliacionBiometrica Estado,
    IReadOnlyList<DiagnosticoConciliacionBiometrica> Diagnosticos);

public sealed record ResultadoConciliacionBiometrica(
    long EquipoId,
    string CodigoEquipo,
    long PlantaId,
    DateOnly? FechaEfectiva,
    NaturalezaVigenciaBiometrica NaturalezaVigencia,
    IReadOnlyList<ResultadoRegistroConciliacionBiometrica> Registros);
