namespace ControlMarcacion.Infrastructure.Marcaciones;

/// <summary>Error explícito de disponibilidad, compatibilidad o ejecución del SDK.</summary>
public sealed class SdkZktecoException : InvalidOperationException
{
    public SdkZktecoException(string codigo, string mensaje)
        : base($"{codigo}: {mensaje}")
    {
        Codigo = codigo;
    }

    public SdkZktecoException(string codigo, string mensaje, Exception interna)
        : base($"{codigo}: {mensaje}", interna)
    {
        Codigo = codigo;
    }

    public string Codigo { get; }
}
