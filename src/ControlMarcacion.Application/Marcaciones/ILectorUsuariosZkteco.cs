namespace ControlMarcacion.Application.Marcaciones;

/// <summary>Lee de forma pasiva el catálogo <c>user.u</c> de un equipo seleccionado.</summary>
public interface ILectorUsuariosZkteco
{
    ResultadoLecturaUsuariosZkteco Leer(
        Stream contenido,
        EquipoOrigenUsuariosZkteco equipo,
        OpcionesLecturaUsuariosZkteco? opciones = null);
}

/// <summary>Equipo elegido por el operador; <c>user.u</c> no contiene este origen.</summary>
public sealed record EquipoOrigenUsuariosZkteco(long EquipoId, string Codigo);

public sealed record OpcionesLecturaUsuariosZkteco(
    int MaximoBytes = 5 * 1024 * 1024,
    int MaximoRegistros = 10_000);

public enum EstadoArchivoUsuariosZkteco
{
    Valido,
    ValidoConAdvertencias,
    Rechazado
}

public enum EstadoRegistroUsuarioZkteco
{
    Leido,
    LeidoConAdvertencias,
    Invalido
}

/// <summary>
/// El lector no acredita documentos. La conciliación puede aplicar después la regla operativa
/// confirmada <c>Name = DNI</c>, manteniéndola separada de una verificación documental.
/// </summary>
public enum EstadoCandidatoIdentidadZkteco
{
    Ausente,
    NoVerificado,
    Ambiguo,
    NoInterpretable
}

public enum IncidenciaUsuarioZkteco
{
    PinAusente,
    PinNoNumerico,
    PinDuplicado,
    NombreAusente,
    NombreNoTieneOchoCaracteres,
    NombreDuplicado,
    NombreCandidatoNoVerificado,
    UsuarioAdministrador,
    BytesDeTextoInvalidos
}

public sealed record UsuarioZktecoLeido(
    int IndiceRegistro,
    ulong? Uid,
    string Pin,
    string? NombreOriginal,
    bool TieneNumeroTarjeta,
    bool EsAdministrador,
    EstadoRegistroUsuarioZkteco Estado,
    EstadoCandidatoIdentidadZkteco EstadoCandidatoIdentidad,
    IReadOnlyList<IncidenciaUsuarioZkteco> Observaciones);

public sealed record ResultadoLecturaUsuariosZkteco(
    EquipoOrigenUsuariosZkteco Equipo,
    EstadoArchivoUsuariosZkteco Estado,
    IReadOnlyList<UsuarioZktecoLeido> Usuarios,
    IReadOnlyList<string> Errores,
    string FormatoDetectado,
    string HuellaContenidoSha256,
    long TamanoBytes,
    DateTime LeidoEnUtc);
