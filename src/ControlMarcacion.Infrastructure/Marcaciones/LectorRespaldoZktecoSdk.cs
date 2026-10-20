using System.Globalization;
using System.Security.Cryptography;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Marcaciones.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ControlMarcacion.Infrastructure.Marcaciones;

/// <summary>Adaptador diagnóstico del parser offline oficial del PULL SDK.</summary>
public sealed class LectorRespaldoZktecoSdk : IFuenteDeMarcaciones
{
    private static readonly Action<ILogger, string, string, string, int, int, Exception?> LogLectura =
        LoggerMessage.Define<string, string, string, int, int>(
            LogLevel.Information,
            new EventId(2110, "LecturaBkSdk"),
            "Lector SDK ZKTeco {Version} {Arquitectura}; hash={Hash}: {Leidas} leídas, " +
            "{Rechazadas} rechazadas.");
    private static readonly string[] FormatosFecha =
    [
        "yyyy-MM-dd HH:mm:ss",
        "yyyy/MM/dd HH:mm:ss",
        "dd/MM/yyyy HH:mm:ss"
    ];

    private readonly IZktecoSdkOfflineParser parser;
    private readonly ILogger<LectorRespaldoZktecoSdk> logger;

    public LectorRespaldoZktecoSdk(
        IOptions<OpcionesLectorBkTransaction> opciones,
        ILogger<LectorRespaldoZktecoSdk>? logger = null)
        : this(new ZktecoSdkOfflineParserNativo(opciones), logger)
    {
    }

    internal LectorRespaldoZktecoSdk(
        IZktecoSdkOfflineParser parser,
        ILogger<LectorRespaldoZktecoSdk>? logger = null)
    {
        this.parser = parser;
        this.logger = logger ?? NullLogger<LectorRespaldoZktecoSdk>.Instance;
    }

    public ResultadoLectura Leer(Stream contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        var datos = LeerTodo(contenido);
        var resultado = parser.Procesar(datos);
        var lectura = InterpretarCsv(resultado.Csv);

        LogLectura(
            logger,
            resultado.Version,
            resultado.Arquitectura,
            Convert.ToHexString(SHA256.HashData(datos.Span)),
            lectura.Marcaciones.Count,
            lectura.Rechazadas.Count,
            null);

        return lectura;
    }

    internal static ResultadoLectura InterpretarCsv(string csv)
    {
        var lineas = csv.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        if (lineas.Length < 3)
        {
            return Rechazo("SDK_ERROR: ProcessBackupDataExt no devolvió la tabla transaction.");
        }

        var cabecera = lineas[0].Split(',');
        var serie = cabecera.Length > 0 ? cabecera[0].Trim() : null;
        var indiceTabla = Array.FindIndex(
            lineas,
            linea => string.Equals(linea.Trim(), "transaction", StringComparison.OrdinalIgnoreCase));

        if (indiceTabla < 0 || indiceTabla + 1 >= lineas.Length)
        {
            return Rechazo("SDK_ERROR: la salida no contiene la tabla transaction.", serie);
        }

        var columnas = lineas[indiceTabla + 1].Split(',');
        var posiciones = columnas
            .Select((nombre, indice) => new { Nombre = nombre.Trim(), Indice = indice })
            .ToDictionary(x => x.Nombre, x => x.Indice, StringComparer.OrdinalIgnoreCase);

        string[] requeridas = ["Cardno", "Pin", "Verified", "EventType", "Time_second"];
        if (requeridas.Any(nombre => !posiciones.ContainsKey(nombre)))
        {
            return Rechazo("SDK_ERROR: la tabla transaction no contiene las columnas esperadas.", serie);
        }

        var marcaciones = new List<MarcacionLeida>();
        var rechazadas = new List<LineaRechazada>();
        for (var i = indiceTabla + 2; i < lineas.Length; i++)
        {
            var valores = lineas[i].Split(',');
            var numero = i - indiceTabla - 1;
            if (valores.Length < columnas.Length)
            {
                rechazadas.Add(new LineaRechazada(numero, string.Empty, "SDK_ERROR: fila incompleta."));
                continue;
            }

            var pin = valores[posiciones["Pin"]].Trim();
            var tarjeta = valores[posiciones["Cardno"]].Trim();
            var id = EsCeroOVacio(pin) ? tarjeta : pin;
            if (EsCeroOVacio(id))
            {
                rechazadas.Add(new LineaRechazada(
                    numero,
                    string.Empty,
                    "El SDK no devolvió Pin ni Cardno para el registro."));
                continue;
            }

            var tiempo = valores[posiciones["Time_second"]].Trim();
            var fecha = InterpretarFecha(tiempo);
            if (fecha is null)
            {
                rechazadas.Add(new LineaRechazada(
                    numero,
                    string.Empty,
                    $"El SDK devolvió Time_second='{tiempo}' sin una fecha válida."));
                continue;
            }

            marcaciones.Add(new MarcacionLeida(
                id,
                fecha.Value,
                valores[posiciones["Verified"]].Trim(),
                valores[posiciones["EventType"]].Trim(),
                numero,
                string.Empty));
        }

        return new ResultadoLectura(marcaciones, rechazadas, serie);
    }

    private static DateTime? InterpretarFecha(string valor)
    {
        if (DateTime.TryParseExact(
                valor,
                FormatosFecha,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var fecha))
        {
            return fecha;
        }

        return uint.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var empaquetada)
            ? LectorRespaldoZkteco.DecodificarTiempo(empaquetada)
            : null;
    }

    private static bool EsCeroOVacio(string? valor) =>
        string.IsNullOrWhiteSpace(valor) || valor.All(c => c == '0');

    private static ReadOnlyMemory<byte> LeerTodo(Stream contenido)
    {
        if (contenido.CanSeek)
        {
            contenido.Position = 0;
        }

        using var memoria = new MemoryStream();
        contenido.CopyTo(memoria);
        return memoria.ToArray();
    }

    private static ResultadoLectura Rechazo(string motivo, string? serie = null) =>
        new([], [new LineaRechazada(1, string.Empty, motivo)], serie);
}
