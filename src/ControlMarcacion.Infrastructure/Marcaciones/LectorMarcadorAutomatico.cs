using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Marcaciones.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace ControlMarcacion.Infrastructure.Marcaciones;

/// <summary>
/// Reconoce qué archivo del marcador le han dado y se lo pasa al lector que corresponde.
/// </summary>
/// <remarks>
/// El equipo exporta dos archivos distintos que se llaman igual de `.dat`, y quien está
/// frente al marcador no tiene por qué acordarse de cuál eligió en el menú:
/// <list type="bullet">
///   <item><b>Texto tabulado</b> (<c>SERIE_attlog.dat</c> o <c>.txt</c>) →
///   <see cref="LectorTxtZkteco"/>.</item>
///   <item><b>Respaldo binario</b> (<c>BKtransaction.dat</c>) →
///   <see cref="LectorRespaldoZkteco"/>.</item>
/// </list>
///
/// <b>Decide por el contenido, nunca por la extensión ni por el nombre.</b> Los dos formatos
/// llegan como `.dat`, y un archivo renombrado a mano —cosa que pasa— no debe cambiar cómo
/// se interpreta.
///
/// Esta clase es el único <see cref="IFuenteDeMarcaciones"/> registrado: el servicio de
/// importación sigue sin saber de dónde salen las marcaciones, que es justamente lo que
/// permitirá enchufar mañana la conexión directa al equipo sin tocar nada más.
/// </remarks>
public sealed class LectorMarcadorAutomatico(
    LectorTxtZkteco lectorTexto,
    LectorRespaldoZkteco lectorRespaldo,
    LectorRespaldoZktecoSdk? lectorSdk = null,
    IOptions<OpcionesLectorBkTransaction>? opciones = null,
    ComparadorLecturasBkTransaction? comparador = null,
    ILogger<LectorMarcadorAutomatico>? logger = null) : IFuenteDeMarcaciones
{
    private static readonly Action<ILogger, string, int, int, int, Exception?> LogInterno =
        LoggerMessage.Define<string, int, int, int>(
        LogLevel.Information,
        new EventId(2101, "LectorBkInterno"),
        "Lector BKtransaction seleccionado: Interno; hash={Hash}; aceptadas={Aceptadas}; " +
        "ignoradas={Ignoradas}; rechazadas={Rechazadas}.");
    private static readonly Action<ILogger, Exception?> LogSdk = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(2102, "LectorBkSdk"),
        "Lector BKtransaction seleccionado: Sdk.");
    private static readonly Action<ILogger, Exception?> LogComparar = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(2103, "LectorBkComparar"),
        "Lector BKtransaction seleccionado: Comparar; Interno es autoritativo.");
    private static readonly Action<ILogger, string, Exception?> LogComparacionIncompleta =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2104, "ComparacionBkIncompleta"),
            "La comparación BKtransaction quedó incompleta: {Codigo}.");
    private readonly OpcionesLectorBkTransaction opciones =
        opciones?.Value ?? new OpcionesLectorBkTransaction();
    private readonly ILogger<LectorMarcadorAutomatico> logger =
        logger ?? NullLogger<LectorMarcadorAutomatico>.Instance;

    public ResultadoLectura Leer(Stream contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        if (!DetectarRespaldo(contenido))
        {
            return lectorTexto.Leer(contenido);
        }

        var datos = LeerTodo(contenido);
        return opciones.Modo() switch
        {
            ModoLectorBkTransaction.Interno => LeerInterno(datos),
            ModoLectorBkTransaction.Sdk => LeerSdk(datos),
            ModoLectorBkTransaction.Comparar => Comparar(datos),
            _ => throw new InvalidOperationException("Modo de lector BKtransaction no soportado.")
        };
    }

    private ResultadoLectura LeerInterno(byte[] datos)
    {
        var resultado = Leer(lectorRespaldo, datos);
        LogInterno(
            logger,
            Convert.ToHexString(SHA256.HashData(datos)),
            resultado.Marcaciones.Count,
            resultado.EventosEquipoIgnorados.Count,
            resultado.Rechazadas.Count,
            null);
        return resultado;
    }

    private ResultadoLectura LeerSdk(byte[] datos)
    {
        if (lectorSdk is null)
        {
            throw new SdkZktecoException(
                "SDK_NO_DISPONIBLE", "El adaptador SDK no está registrado en Infrastructure.");
        }

        LogSdk(logger, null);
        return Leer(lectorSdk, datos);
    }

    private ResultadoLectura Comparar(byte[] datos)
    {
        LogComparar(logger, null);
        var interno = Leer(lectorRespaldo, datos);
        ResultadoLectura? resultadoSdk = null;
        SdkZktecoException? errorSdk = null;

        try
        {
            resultadoSdk = lectorSdk is null
                ? throw new SdkZktecoException(
                    "SDK_NO_DISPONIBLE", "El adaptador SDK no está registrado en Infrastructure.")
                : Leer(lectorSdk, datos);
        }
        catch (SdkZktecoException ex)
        {
            errorSdk = ex;
            LogComparacionIncompleta(logger, ex.Codigo, ex);
        }

        comparador?.Escribir(datos, interno, resultadoSdk, errorSdk);
        return interno;
    }

    private static ResultadoLectura Leer(IFuenteDeMarcaciones lector, byte[] datos)
    {
        using var copia = new MemoryStream(datos, writable: false);
        return lector.Leer(copia);
    }

    private static byte[] LeerTodo(Stream contenido)
    {
        if (contenido.CanSeek)
        {
            contenido.Position = 0;
        }

        using var memoria = new MemoryStream();
        contenido.CopyTo(memoria);
        return memoria.ToArray();
    }

    /// <summary>
    /// Indica si el contenido es un respaldo binario.
    /// </summary>
    /// <remarks>
    /// Un flujo que no se puede rebobinar no se puede examinar sin consumirlo, así que se
    /// asume texto: es el formato normal, y el lector de texto informa por su cuenta si la
    /// suposición era mala.
    /// </remarks>
    private static bool DetectarRespaldo(Stream contenido) =>
        contenido.CanSeek && DetectorFormatoMarcador.Detectar(contenido) is not null;
}
