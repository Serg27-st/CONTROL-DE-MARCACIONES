namespace ControlMarcacion.Infrastructure.Seguridad;

/// <summary>
/// Indica que una base sin usuarios no puede crear su primer coordinador porque las
/// credenciales de bootstrap no fueron suministradas por una fuente externa.
/// </summary>
public sealed class CredencialesBootstrapFaltantesException : InvalidOperationException
{
    public CredencialesBootstrapFaltantesException()
        : base(
            "La base no contiene usuarios y faltan las credenciales externas " +
            "Seguridad:AdminCorreo y Seguridad:AdminClave para crear el primer coordinador.")
    {
    }
}
