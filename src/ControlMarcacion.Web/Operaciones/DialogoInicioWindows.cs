using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ControlMarcacion.Web.Operaciones;

/// <summary>Presenta fallos de arranque que no deben depender de una consola fugaz.</summary>
internal static partial class DialogoInicioWindows
{
    private const uint MbIconError = 0x00000010;
    private const uint MbYesNo = 0x00000004;
    private const int IdYes = 6;
    private static int manejadorRegistrado;

    public static string RutaLogsPredeterminada => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ControlMarcacion",
        "logs");

    public static void RegistrarManejadorGlobal()
    {
        if (Interlocked.Exchange(ref manejadorRegistrado, 1) != 0)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, argumentos) =>
        {
            if (argumentos.IsTerminating && argumentos.ExceptionObject is Exception)
            {
                MostrarFallo(RutaLogsPredeterminada);
            }
        };
    }

    public static string CrearMensaje(string rutaLogs) =>
        "Control de Marcación no pudo iniciarse.\n\n" +
        $"Revise el registro en:\n{rutaLogs}\n\n" +
        "Pulse Sí para abrir la carpeta de registros o No para cerrar.";

    public static void MostrarFallo(string rutaLogs)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("No fue posible iniciar Control de Marcación.");
        Console.Error.WriteLine($"Revise los registros en: {rutaLogs}");

        if (!OperatingSystem.IsWindows() || !Environment.UserInteractive)
        {
            return;
        }

        try
        {
            var respuesta = MessageBox(
                IntPtr.Zero,
                CrearMensaje(rutaLogs),
                "Control de Marcación",
                MbIconError | MbYesNo);
            if (respuesta == IdYes && Directory.Exists(rutaLogs))
            {
                using var proceso = Process.Start(
                    new ProcessStartInfo(rutaLogs) { UseShellExecute = true });
            }
        }
        catch (Win32Exception)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }
    }

    [LibraryImport(
        "user32.dll",
        EntryPoint = "MessageBoxW",
        StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(
        IntPtr ventana,
        string texto,
        string titulo,
        uint tipo);
}
