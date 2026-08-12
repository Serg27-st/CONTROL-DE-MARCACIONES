namespace ControlMarcacion.Domain.Comun;

/// <summary>
/// Se pidió un recurso que no existe.
/// </summary>
public class RecursoNoEncontradoException(string recurso, object clave)
    : Exception($"No se encontró {recurso} con identificador '{clave}'.")
{
    public string Recurso { get; } = recurso;

    public object Clave { get; } = clave;
}

/// <summary>
/// Se violó una regla de negocio. El mensaje se muestra al usuario, así que debe
/// estar redactado en español y explicar qué hacer, no solo qué falló.
/// </summary>
public class ReglaNegocioException(string mensaje) : Exception(mensaje);

/// <summary>
/// Se intentó escribir sobre un periodo cerrado.
/// </summary>
/// <remarks>
/// Es una excepción propia y no una <see cref="ReglaNegocioException"/> genérica porque
/// es la barrera que protege la integridad del tareo ya cerrado: conviene poder
/// distinguirla, registrarla y auditarla por separado.
/// </remarks>
public class PeriodoCerradoException(string periodo)
    : Exception($"El periodo '{periodo}' está cerrado y no admite modificaciones. " +
                "Para corregir información debe reabrirse explícitamente, indicando el motivo.")
{
    public string Periodo { get; } = periodo;
}
