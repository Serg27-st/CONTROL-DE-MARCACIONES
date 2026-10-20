using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.AspNetCore.Connections;

namespace ControlMarcacion.Web.Operaciones;

internal enum EstadoInicioLocal
{
    Starting,
    SetupRequired,
    Ready,
    Failed
}

internal readonly record struct ResultadoInicioLocal(
    bool Vivo,
    EstadoInicioLocal Estado,
    HttpStatusCode? EstadoHttp,
    string Detalle)
{
    public bool Listo => Estado == EstadoInicioLocal.Ready;

    public bool RequiereConfiguracion => Estado == EstadoInicioLocal.SetupRequired;

    public static ResultadoInicioLocal NoResponde(string detalle) =>
        new(false, EstadoInicioLocal.Starting, null, detalle);
}

/// <summary>
/// Infraestructura del modo Windows local. No contiene reglas de negocio ni conoce el tareo.
/// </summary>
internal static class HostingLocalWindows
{
    public const string Direccion = "127.0.0.1";
    private const int PuertoMinimo = 1;
    private const int PuertoMaximo = 65535;

    public static int ResolverPuertoPreferido(IConfiguration configuracion)
    {
        var configurado = configuracion.GetValue<int?>("Despliegue:PuertoPreferido");
        if (configurado.HasValue)
        {
            return ValidarPuerto(configurado.Value, "Despliegue:PuertoPreferido");
        }

        var url = configuracion["Urls"]?.Split(';', StringSplitOptions.RemoveEmptyEntries)[0];
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? ValidarPuerto(uri.Port, "Urls")
            : 5000;
    }

    public static int ResolverPuertoActual(IConfiguration configuracion, int preferido)
    {
        var actual = configuracion.GetValue<int?>("Despliegue:PuertoActual");
        return actual.HasValue
            ? ValidarPuerto(actual.Value, "Despliegue:PuertoActual")
            : preferido;
    }

    public static string CrearUrl(int puerto) => $"http://{Direccion}:{ValidarPuerto(puerto, nameof(puerto))}";

    public static int SiguientePuerto(int puerto)
    {
        ValidarPuerto(puerto, nameof(puerto));
        if (puerto == PuertoMaximo)
        {
            throw new InvalidOperationException("No quedan puertos TCP disponibles después del 65535.");
        }

        return puerto + 1;
    }

    public static bool EsDireccionEnUso(Exception excepcion)
    {
        for (Exception? actual = excepcion; actual is not null; actual = actual.InnerException)
        {
            if (actual is AddressInUseException)
            {
                return true;
            }

            if (actual is SocketException socket
                && socket.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                return true;
            }
        }

        return false;
    }

    public static async Task<ResultadoInicioLocal> EsperarEstadoAsync(
        HttpClient cliente,
        string urlBase,
        TimeSpan esperaMaxima,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        ArgumentException.ThrowIfNullOrWhiteSpace(urlBase);

        var limite = DateTimeOffset.UtcNow + esperaMaxima;
        ResultadoInicioLocal ultimo = ResultadoInicioLocal.NoResponde("/vivo no respondió.");

        do
        {
            ultimo = await SondearAsync(cliente, urlBase, cancelacion).ConfigureAwait(false);
            if (ultimo.Vivo)
            {
                return ultimo;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancelacion).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < limite);

        return ultimo;
    }

    public static async Task<ResultadoInicioLocal> SondearAsync(
        HttpClient cliente,
        string urlBase,
        CancellationToken cancelacion)
    {
        try
        {
            using var vivo = await cliente
                .GetAsync($"{urlBase}/vivo", cancelacion)
                .ConfigureAwait(false);
            var cuerpoVivo = await vivo.Content
                .ReadAsStringAsync(cancelacion)
                .ConfigureAwait(false);

            if (vivo.StatusCode != HttpStatusCode.OK
                || !cuerpoVivo.Contains(Marcas.Sistema, StringComparison.Ordinal))
            {
                return ResultadoInicioLocal.NoResponde(
                    $"/vivo respondió {(int)vivo.StatusCode}, pero no identificó al sistema.");
            }

            using var estado = await cliente
                .GetAsync($"{urlBase}/estado", cancelacion)
                .ConfigureAwait(false);
            var cuerpoEstado = await estado.Content
                .ReadAsStringAsync(cancelacion)
                .ConfigureAwait(false);
            var listo = estado.StatusCode == HttpStatusCode.OK
                && cuerpoEstado.Contains(
                    $"{Marcas.Sistema};LISTO",
                    StringComparison.Ordinal);
            var requiereConfiguracion = estado.StatusCode == HttpStatusCode.ServiceUnavailable
                && cuerpoEstado.Contains(
                    "CONFIGURACION_INICIAL_REQUERIDA",
                    StringComparison.Ordinal);

            return new ResultadoInicioLocal(
                true,
                listo
                    ? EstadoInicioLocal.Ready
                    : requiereConfiguracion
                        ? EstadoInicioLocal.SetupRequired
                        : EstadoInicioLocal.Failed,
                estado.StatusCode,
                listo
                    ? "LISTO"
                    : requiereConfiguracion
                        ? "CONFIGURACION_INICIAL_REQUERIDA"
                    : $"/estado respondió HTTP {(int)estado.StatusCode}: {cuerpoEstado}");
        }
        catch (HttpRequestException excepcion)
        {
            return ResultadoInicioLocal.NoResponde(excepcion.Message);
        }
        catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            return ResultadoInicioLocal.NoResponde("Tiempo agotado consultando el proceso local.");
        }
    }

    public static bool RelanzarEnPuerto(string[] argumentosOriginales, int puerto)
    {
        var procesoActual = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(procesoActual))
        {
            return false;
        }

        var inicio = new ProcessStartInfo
        {
            FileName = procesoActual,
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory
        };

        // En dotnet run el proceso actual es dotnet.exe y hay que volver a anteponer el DLL.
        // En el publish autocontenido ProcessPath ya es ControlMarcacion.Web.exe.
        if (Path.GetFileNameWithoutExtension(procesoActual)
            .Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var nombreEnsamblado = Assembly.GetEntryAssembly()?.GetName().Name;
            if (string.IsNullOrWhiteSpace(nombreEnsamblado))
            {
                return false;
            }

            var ensamblado = Path.Combine(AppContext.BaseDirectory, $"{nombreEnsamblado}.dll");
            if (!File.Exists(ensamblado))
            {
                return false;
            }

            inicio.ArgumentList.Add(ensamblado);
        }

        foreach (var argumento in argumentosOriginales)
        {
            inicio.ArgumentList.Add(argumento);
        }

        inicio.ArgumentList.Add($"--Urls={CrearUrl(puerto)}");
        inicio.ArgumentList.Add($"--Despliegue:PuertoActual={puerto}");

        using var proceso = Process.Start(inicio);
        return proceso is not null;
    }

    private static int ValidarPuerto(int puerto, string nombre)
    {
        if (puerto is < PuertoMinimo or > PuertoMaximo)
        {
            throw new InvalidOperationException($"{nombre} debe estar entre 1 y 65535.");
        }

        return puerto;
    }
}
