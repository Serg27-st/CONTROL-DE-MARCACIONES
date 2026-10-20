using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ControlMarcacion.Web.Operaciones;

internal sealed record EstadoInstanciaLocal(int Pid, int Puerto, DateTimeOffset IniciadaUtc);

/// <summary>
/// Exclusión mutua y descubrimiento de la instancia local. El archivo es solo una pista:
/// siempre se valida el PID y el endpoint /vivo antes de confiar en él.
/// </summary>
internal sealed class InstanciaLocalWindows : IDisposable
{
    private readonly Mutex mutex;
    private bool propietaria;

    private InstanciaLocalWindows(Mutex mutex, bool propietaria)
    {
        this.mutex = mutex;
        this.propietaria = propietaria;
    }

    public bool EsPropietaria => propietaria;

    public static InstanciaLocalWindows IntentarAdquirir()
    {
        var huella = HuellaUsuario();
        var mutex = new Mutex(false, $"Local\\ControlMarcacion-{huella}");
        var adquirida = false;

        try
        {
            adquirida = mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            adquirida = true;
        }

        return new InstanciaLocalWindows(mutex, adquirida);
    }

    public static string ResolverRutaEstado(
        IHostEnvironment ambiente,
        IConfiguration configuracion)
    {
        var configurada = configuracion["Despliegue:RutaRuntime"];
        if (!string.IsNullOrWhiteSpace(configurada))
        {
            return Path.Combine(
                Path.GetFullPath(configurada),
                $"instancia-{HuellaUsuario()}.json");
        }

        var carpeta = ambiente.IsDevelopment()
            ? Path.Combine(Path.GetTempPath(), "ControlMarcacion", "runtime")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ControlMarcacion",
                "runtime");
        return Path.Combine(carpeta, $"instancia-{HuellaUsuario()}.json");
    }

    public static EstadoInstanciaLocal? Leer(string ruta)
    {
        try
        {
            return File.Exists(ruta)
                ? JsonSerializer.Deserialize<EstadoInstanciaLocal>(File.ReadAllText(ruta))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static bool ProcesoExiste(EstadoInstanciaLocal estado)
    {
        try
        {
            using var proceso = Process.GetProcessById(estado.Pid);
            return !proceso.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static void Registrar(string ruta, int puerto)
    {
        var carpeta = Path.GetDirectoryName(ruta)
            ?? throw new InvalidOperationException("La ruta de runtime no tiene directorio.");
        Directory.CreateDirectory(carpeta);
        var estado = new EstadoInstanciaLocal(Environment.ProcessId, puerto, DateTimeOffset.UtcNow);
        var temporal = $"{ruta}.{Environment.ProcessId}.tmp";
        File.WriteAllText(temporal, JsonSerializer.Serialize(estado));
        File.Move(temporal, ruta, true);
    }

    public static void RetirarSiPerteneceAlProceso(string ruta)
    {
        try
        {
            if (Leer(ruta)?.Pid == Environment.ProcessId)
            {
                File.Delete(ruta);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static void RetirarStale(string ruta)
    {
        try
        {
            var estado = Leer(ruta);
            if (estado is null || !ProcesoExiste(estado))
            {
                File.Delete(ruta);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public void LiberarParaRelanzar()
    {
        if (!propietaria)
        {
            return;
        }

        mutex.ReleaseMutex();
        propietaria = false;
    }

    public void Dispose()
    {
        if (propietaria)
        {
            mutex.ReleaseMutex();
            propietaria = false;
        }

        mutex.Dispose();
    }

    private static string HuellaUsuario()
    {
        var identidad = $"{Environment.UserDomainName}\\{Environment.UserName}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identidad)))[..16];
    }
}
