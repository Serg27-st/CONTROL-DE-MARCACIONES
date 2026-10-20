namespace ControlMarcacion.Infrastructure.Marcaciones;

/// <summary>Configuración del lector del respaldo binario <c>BKtransaction.dat</c>.</summary>
public sealed class OpcionesLectorBkTransaction
{
    public const string Seccion = "Marcaciones";

    /// <summary><c>Interno</c>, <c>Sdk</c> o <c>Comparar</c>.</summary>
    public string LectorBkTransaction { get; set; } = "Interno";

    /// <summary>
    /// Carpeta de desarrollo que contiene <c>plcommpro.dll</c> y sus dependencias.
    /// Las DLL no se distribuyen con la aplicación mientras licencia y compatibilidad no
    /// estén aprobadas.
    /// </summary>
    public string? DirectorioSdkZkteco { get; set; }

    /// <summary>Destino de los reportes del modo <c>Comparar</c>.</summary>
    public string DirectorioDiagnostico { get; set; } =
        Path.Combine("artifacts", "bktransaction-validation");

    public ModoLectorBkTransaction Modo()
    {
        if (Enum.TryParse<ModoLectorBkTransaction>(LectorBkTransaction, true, out var modo))
        {
            return modo;
        }

        throw new InvalidOperationException(
            $"Marcaciones:LectorBkTransaction='{LectorBkTransaction}' no es válido. " +
            "Use Interno, Sdk o Comparar.");
    }
}

public enum ModoLectorBkTransaction
{
    Interno,
    Sdk,
    Comparar
}
