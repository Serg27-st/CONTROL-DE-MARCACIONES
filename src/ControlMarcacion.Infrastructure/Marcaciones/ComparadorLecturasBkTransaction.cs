using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ControlMarcacion.Application.Marcaciones.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ControlMarcacion.Infrastructure.Marcaciones;

/// <summary>Genera evidencia diagnóstica sin decidir qué registros se persisten.</summary>
public sealed class ComparadorLecturasBkTransaction
{
    private static readonly Action<ILogger, string, string, int, int?, int, int, Exception?>
        LogComparacion = LoggerMessage.Define<string, string, int, int?, int, int>(
            LogLevel.Information,
            new EventId(2120, "ComparacionBk"),
            "Comparación BKtransaction {Hash}: {Estado}; interno={Interno}; sdk={Sdk}; " +
            "coincidencias={Coincidencias}; diferencias={Diferencias}.");
    private static readonly UTF8Encoding Utf8SinBom = new(false);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly OpcionesLectorBkTransaction opciones;
    private readonly ILogger<ComparadorLecturasBkTransaction> logger;

    public ComparadorLecturasBkTransaction(
        IOptions<OpcionesLectorBkTransaction> opciones,
        ILogger<ComparadorLecturasBkTransaction>? logger = null)
    {
        this.opciones = opciones.Value;
        this.logger = logger ?? NullLogger<ComparadorLecturasBkTransaction>.Instance;
    }

    internal ResumenComparacionBkTransaction Escribir(
        ReadOnlySpan<byte> archivo,
        ResultadoLectura interno,
        ResultadoLectura? sdk,
        SdkZktecoException? errorSdk = null)
    {
        var hash = Convert.ToHexString(SHA256.HashData(archivo));
        var directorio = Path.GetFullPath(opciones.DirectorioDiagnostico);
        Directory.CreateDirectory(directorio);

        var filasInternas = interno.Marcaciones.ToDictionary(x => x.NumeroLinea);
        var filasSdk = (sdk?.Marcaciones ?? []).ToDictionary(x => x.NumeroLinea);
        var numeros = filasInternas.Keys.Union(filasSdk.Keys).Order().ToArray();
        var diferencias = new List<DiferenciaBkTransaction>();
        var coincidencias = 0;
        var soloInterno = 0;
        var soloSdk = 0;
        var idDiferente = 0;
        var fechaDiferente = 0;
        var codigoDiferente = 0;

        foreach (var numero in numeros)
        {
            filasInternas.TryGetValue(numero, out var filaInterna);
            filasSdk.TryGetValue(numero, out var filaSdk);

            if (filaInterna is null)
            {
                soloSdk++;
                diferencias.Add(DiferenciaBkTransaction.Solo("SoloSdk", filaSdk));
                continue;
            }

            if (filaSdk is null)
            {
                soloInterno++;
                diferencias.Add(DiferenciaBkTransaction.Solo("SoloInterno", filaInterna));
                continue;
            }

            var tipos = new List<string>();
            if (!string.Equals(filaInterna.IdMarcador, filaSdk.IdMarcador, StringComparison.Ordinal))
            {
                idDiferente++;
                tipos.Add("IdDiferente");
            }

            if (filaInterna.FechaHora != filaSdk.FechaHora)
            {
                fechaDiferente++;
                tipos.Add("FechaHoraDiferente");
            }

            if (!string.Equals(filaInterna.TipoMarcacion, filaSdk.TipoMarcacion, StringComparison.Ordinal))
            {
                codigoDiferente++;
                tipos.Add("CodigoEventoDiferente");
            }

            if (tipos.Count == 0)
            {
                coincidencias++;
            }
            else
            {
                diferencias.Add(DiferenciaBkTransaction.De(tipos, filaInterna, filaSdk));
            }
        }

        var resumen = new ResumenComparacionBkTransaction(
            hash,
            errorSdk is null ? "COMPLETA" : "INCOMPLETA",
            Resumir(interno),
            sdk is null ? null : Resumir(sdk),
            coincidencias,
            soloInterno,
            soloSdk,
            idDiferente,
            fechaDiferente,
            codigoDiferente,
            diferencias.Count,
            errorSdk?.Codigo,
            errorSdk?.Message);

        File.WriteAllText(
            Path.Combine(directorio, $"{hash}-interno.csv"),
            CsvLectura(interno),
            Utf8SinBom);
        File.WriteAllText(
            Path.Combine(directorio, $"{hash}-sdk.csv"),
            CsvLectura(sdk),
            Utf8SinBom);
        File.WriteAllText(
            Path.Combine(directorio, $"{hash}-diff.csv"),
            CsvDiferencias(diferencias),
            Utf8SinBom);
        File.WriteAllText(
            Path.Combine(directorio, $"{hash}-summary.json"),
            JsonSerializer.Serialize(resumen, JsonOptions),
            Utf8SinBom);

        LogComparacion(
            logger,
            hash,
            resumen.Estado,
            resumen.Interno.Registros,
            resumen.Sdk?.Registros,
            resumen.Coincidencias,
            resumen.Diferencias,
            null);

        return resumen;
    }

    private static ResumenFuenteBkTransaction Resumir(ResultadoLectura lectura) =>
        new(
            lectura.Marcaciones.Count,
            lectura.Marcaciones.Select(x => x.IdMarcador).Distinct(StringComparer.Ordinal).Count(),
            lectura.Marcaciones.Count == 0 ? null : lectura.Marcaciones.Min(x => x.FechaHora),
            lectura.Marcaciones.Count == 0 ? null : lectura.Marcaciones.Max(x => x.FechaHora),
            lectura.Rechazadas.Count,
            lectura.EventosEquipoIgnorados.Count);

    private static string CsvLectura(ResultadoLectura? lectura)
    {
        var salida = new StringBuilder("Registro,IdMarcador,FechaHora,CodigoEvento\r\n");
        if (lectura is null)
        {
            return salida.ToString();
        }

        foreach (var fila in lectura.Marcaciones.OrderBy(x => x.NumeroLinea))
        {
            salida.Append(fila.NumeroLinea.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Escapar(fila.IdMarcador)).Append(',')
                .Append(fila.FechaHora.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(',')
                .Append(Escapar(fila.TipoMarcacion)).Append("\r\n");
        }

        return salida.ToString();
    }

    private static string CsvDiferencias(IEnumerable<DiferenciaBkTransaction> diferencias)
    {
        var salida = new StringBuilder(
            "TipoDiferencia,RegistroInterno,RegistroSdk,IdInterno,IdSdk," +
            "FechaHoraInterno,FechaHoraSdk,CodigoInterno,CodigoSdk\r\n");

        foreach (var fila in diferencias)
        {
            salida.Append(Escapar(fila.TipoDiferencia)).Append(',')
                .Append(fila.RegistroInterno?.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(fila.RegistroSdk?.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Escapar(fila.IdInterno)).Append(',')
                .Append(Escapar(fila.IdSdk)).Append(',')
                .Append(Formatear(fila.FechaHoraInterno)).Append(',')
                .Append(Formatear(fila.FechaHoraSdk)).Append(',')
                .Append(Escapar(fila.CodigoInterno)).Append(',')
                .Append(Escapar(fila.CodigoSdk)).Append("\r\n");
        }

        return salida.ToString();
    }

    private static string Escapar(string? valor) =>
        valor is null
            ? string.Empty
            : $"\"{valor.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string Formatear(DateTime? valor) =>
        valor?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;
}

internal sealed record ResumenFuenteBkTransaction(
    int Registros,
    int Ids,
    DateTime? FechaMin,
    DateTime? FechaMax,
    int Rechazados,
    int Ignorados);

internal sealed record ResumenComparacionBkTransaction(
    string Hash,
    string Estado,
    ResumenFuenteBkTransaction Interno,
    ResumenFuenteBkTransaction? Sdk,
    int Coincidencias,
    int SoloInterno,
    int SoloSdk,
    int IdDiferente,
    int FechaHoraDiferente,
    int CodigoEventoDiferente,
    int Diferencias,
    string? CodigoErrorSdk,
    string? ErrorSdk);

internal sealed record DiferenciaBkTransaction(
    string TipoDiferencia,
    int? RegistroInterno,
    int? RegistroSdk,
    string? IdInterno,
    string? IdSdk,
    DateTime? FechaHoraInterno,
    DateTime? FechaHoraSdk,
    string? CodigoInterno,
    string? CodigoSdk)
{
    public static DiferenciaBkTransaction Solo(string tipo, MarcacionLeida? fila) =>
        tipo == "SoloInterno"
            ? De([tipo], fila, null)
            : De([tipo], null, fila);

    public static DiferenciaBkTransaction De(
        IEnumerable<string> tipos,
        MarcacionLeida? interno,
        MarcacionLeida? sdk) =>
        new(
            string.Join('+', tipos),
            interno?.NumeroLinea,
            sdk?.NumeroLinea,
            interno?.IdMarcador,
            sdk?.IdMarcador,
            interno?.FechaHora,
            sdk?.FechaHora,
            interno?.TipoMarcacion,
            sdk?.TipoMarcacion);
}
