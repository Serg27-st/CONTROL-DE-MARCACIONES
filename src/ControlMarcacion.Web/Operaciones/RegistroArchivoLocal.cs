using System.Text;

namespace ControlMarcacion.Web.Operaciones;

/// <summary>
/// Duplica la salida existente en logs diarios bajo ProgramData. Rota a archivos numerados
/// al llegar a 20 MB y elimina únicamente logs propios con más de 30 días.
/// </summary>
internal sealed class RegistroArchivoLocal : IDisposable
{
    internal const long TamanioMaximoBytes = 20L * 1024 * 1024;
    internal const int DiasRetencion = 30;

    private readonly TextWriter salidaOriginal;
    private readonly TextWriter errorOriginal;
    private readonly DestinoRotativo destino;

    private RegistroArchivoLocal(string carpeta)
    {
        Directory.CreateDirectory(carpeta);
        LimpiarExpirados(carpeta, DateTime.UtcNow.AddDays(-DiasRetencion));
        salidaOriginal = Console.Out;
        errorOriginal = Console.Error;
        destino = new DestinoRotativo(carpeta, TamanioMaximoBytes);
        Console.SetOut(new EscritorDuplicado(salidaOriginal, destino));
        Console.SetError(new EscritorDuplicado(errorOriginal, destino));
    }

    public static RegistroArchivoLocal? Inicializar(
        IHostEnvironment ambiente,
        IConfiguration configuracion)
    {
        if (ambiente.IsDevelopment())
        {
            return null;
        }

        var carpeta = configuracion["Despliegue:RutaLogs"];
        if (string.IsNullOrWhiteSpace(carpeta))
        {
            carpeta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ControlMarcacion",
                "logs");
        }

        return new RegistroArchivoLocal(Path.GetFullPath(carpeta));
    }

    internal static string ResolverArchivoActual(string carpeta, DateOnly fecha, long maximoBytes)
    {
        var baseNombre = $"ControlMarcacion-{fecha:yyyyMMdd}";
        for (var indice = 0; ; indice++)
        {
            var sufijo = indice == 0 ? string.Empty : $"-{indice:000}";
            var ruta = Path.Combine(carpeta, $"{baseNombre}{sufijo}.log");
            if (!File.Exists(ruta) || new FileInfo(ruta).Length < maximoBytes)
            {
                return ruta;
            }
        }
    }

    internal static int LimpiarExpirados(string carpeta, DateTime limiteUtc)
    {
        if (!Directory.Exists(carpeta))
        {
            return 0;
        }

        var eliminados = 0;
        foreach (var ruta in Directory.EnumerateFiles(carpeta, "ControlMarcacion-*.log"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(ruta) < limiteUtc)
                {
                    File.Delete(ruta);
                    eliminados++;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return eliminados;
    }

    public void Dispose()
    {
        Console.SetOut(salidaOriginal);
        Console.SetError(errorOriginal);
        destino.Dispose();
    }

    private sealed class DestinoRotativo(string carpeta, long maximoBytes) : TextWriter
    {
        private readonly object sincronizacion = new();
        private StreamWriter? archivo;
        private DateOnly fechaAbierta;

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => Escribir(value.ToString());

        public override void Write(string? value) => Escribir(value ?? string.Empty);

        public override void WriteLine(string? value) =>
            Escribir($"{value}{Environment.NewLine}");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (sincronizacion)
                {
                    archivo?.Dispose();
                    archivo = null;
                }
            }

            base.Dispose(disposing);
        }

        private void Escribir(string texto)
        {
            lock (sincronizacion)
            {
                var hoy = DateOnly.FromDateTime(DateTime.Today);
                var bytes = Encoding.UTF8.GetByteCount(texto);
                if (archivo is null
                    || fechaAbierta != hoy
                    || archivo.BaseStream.Length + bytes > maximoBytes)
                {
                    archivo?.Dispose();
                    var ruta = ResolverArchivoActual(carpeta, hoy, maximoBytes);
                    var flujo = new FileStream(
                        ruta,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite | FileShare.Delete);
                    archivo = new StreamWriter(flujo, Encoding.UTF8) { AutoFlush = true };
                    fechaAbierta = hoy;
                }

                archivo.Write(texto);
            }
        }
    }

    private sealed class EscritorDuplicado(TextWriter primero, TextWriter segundo) : TextWriter
    {
        public override Encoding Encoding => primero.Encoding;

        public override void Write(char value)
        {
            primero.Write(value);
            segundo.Write(value);
        }

        public override void Write(string? value)
        {
            primero.Write(value);
            segundo.Write(value);
        }

        public override void WriteLine(string? value)
        {
            primero.WriteLine(value);
            segundo.WriteLine(value);
        }
    }
}
