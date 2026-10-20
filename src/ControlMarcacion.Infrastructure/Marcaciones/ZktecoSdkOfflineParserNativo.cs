using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;

namespace ControlMarcacion.Infrastructure.Marcaciones;

internal interface IZktecoSdkOfflineParser
{
    ResultadoSdkOffline Procesar(ReadOnlyMemory<byte> archivo);
}

internal sealed record ResultadoSdkOffline(string Version, string Arquitectura, string Csv, int Registros);

/// <summary>
/// Único límite nativo del PULL SDK. Carga dinámicamente la DLL configurada y nunca la
/// referencia desde Domain ni Application.
/// </summary>
internal sealed class ZktecoSdkOfflineParserNativo(IOptions<OpcionesLectorBkTransaction> opciones)
    : IZktecoSdkOfflineParser
{
    private const ushort PeX64 = 0x8664;
    private const int CapacidadSalida = 32 * 1024 * 1024;
    private readonly OpcionesLectorBkTransaction opciones = opciones.Value;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int ProcessBackupDataExt(
        IntPtr entrada,
        int longitudEntrada,
        IntPtr salida,
        int capacidadSalida);

    public ResultadoSdkOffline Procesar(ReadOnlyMemory<byte> archivo)
    {
        var ruta = ResolverRutaDll();
        ValidarArquitecturaX64(ruta);

        nint modulo = 0;
        nint entrada = 0;
        nint salida = 0;

        try
        {
            modulo = NativeLibrary.Load(ruta);
            var exportada = NativeLibrary.GetExport(modulo, "ProcessBackupDataExt");
            var procesar = Marshal.GetDelegateForFunctionPointer<ProcessBackupDataExt>(exportada);

            entrada = Marshal.AllocHGlobal(archivo.Length);
            salida = Marshal.AllocHGlobal(CapacidadSalida);
            var bytes = archivo.ToArray();
            Marshal.Copy(bytes, 0, entrada, bytes.Length);
            Marshal.Copy(new byte[CapacidadSalida], 0, salida, CapacidadSalida);

            var registros = procesar(entrada, bytes.Length, salida, CapacidadSalida);
            if (registros < 0)
            {
                throw new SdkZktecoException(
                    "SDK_ERROR", $"ProcessBackupDataExt devolvió el código {registros}.");
            }

            var csv = Marshal.PtrToStringAnsi(salida) ?? string.Empty;
            var version = FileVersionInfo.GetVersionInfo(ruta).FileVersion ?? "NO_DETERMINADA";
            return new ResultadoSdkOffline(version, "x64", csv, registros);
        }
        catch (SdkZktecoException)
        {
            throw;
        }
        catch (BadImageFormatException ex)
        {
            throw new SdkZktecoException(
                "SDK_INCOMPATIBLE", "plcommpro.dll no es compatible con el proceso win-x64.", ex);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            throw new SdkZktecoException(
                "SDK_NO_DISPONIBLE", "No se pudo cargar plcommpro.dll o ProcessBackupDataExt.", ex);
        }
        catch (Exception ex)
        {
            throw new SdkZktecoException(
                "SDK_ERROR", "El PULL SDK falló al procesar el respaldo.", ex);
        }
        finally
        {
            if (salida != 0)
            {
                Marshal.FreeHGlobal(salida);
            }

            if (entrada != 0)
            {
                Marshal.FreeHGlobal(entrada);
            }

            if (modulo != 0)
            {
                NativeLibrary.Free(modulo);
            }
        }
    }

    internal static void ValidarArquitecturaX64(string ruta)
    {
        if (!Environment.Is64BitProcess)
        {
            throw new SdkZktecoException(
                "SDK_INCOMPATIBLE", "La integración diagnóstica requiere un proceso x64.");
        }

        using var archivo = File.OpenRead(ruta);
        using var lector = new BinaryReader(archivo);
        if (archivo.Length < 0x40)
        {
            throw new SdkZktecoException("SDK_INCOMPATIBLE", "La DLL no contiene una cabecera PE válida.");
        }

        archivo.Position = 0x3c;
        var offsetPe = lector.ReadInt32();
        if (offsetPe < 0 || offsetPe > archivo.Length - 6)
        {
            throw new SdkZktecoException("SDK_INCOMPATIBLE", "La DLL no contiene una cabecera PE válida.");
        }

        archivo.Position = offsetPe;
        if (lector.ReadUInt32() != 0x00004550 || lector.ReadUInt16() != PeX64)
        {
            throw new SdkZktecoException(
                "SDK_INCOMPATIBLE", "Se requiere la distribución x64 de plcommpro.dll.");
        }
    }

    private string ResolverRutaDll()
    {
        if (string.IsNullOrWhiteSpace(opciones.DirectorioSdkZkteco))
        {
            throw new SdkZktecoException(
                "SDK_NO_DISPONIBLE", "Marcaciones:DirectorioSdkZkteco no está configurado.");
        }

        var ruta = Path.GetFullPath(Path.Combine(opciones.DirectorioSdkZkteco, "plcommpro.dll"));
        if (!File.Exists(ruta))
        {
            throw new SdkZktecoException(
                "SDK_NO_DISPONIBLE", $"No existe plcommpro.dll en '{opciones.DirectorioSdkZkteco}'.");
        }

        return ruta;
    }
}
