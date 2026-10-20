using System.Text;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

/// <summary>
/// Arma respaldos binarios del marcador byte a byte, para las pruebas.
/// </summary>
/// <remarks>
/// Se construyen aquí en vez de versionar un volcado real por dos motivos: el archivo de
/// muestra son las tablas internas de un equipo de producción, y un binario opaco en
/// <c>Recursos/</c> no le explica el formato a nadie. Esto sí: <b>este helper es la
/// especificación ejecutable</b> de lo que documenta
/// <c>docs/06-formato-archivos-marcador.md</c>.
///
/// Los valores por omisión son los del equipo que motivó el análisis.
/// </remarks>
internal static class RespaldoDePrueba
{
    public const string SerieReal = "VGU6251600671";
    public const string ModeloReal = "ZAM70-NF28VA-Ver3.0.15";

    /// <summary>Tamaño de registro del equipo real.</summary>
    public const int TamanoRegistro = 28;

    /// <summary>Anchos de los siete campos declarados, en orden.</summary>
    public static readonly byte[] AnchosReales = [4, 4, 1, 1, 1, 1, 4];

    /// <summary>
    /// Byte del registro donde empieza el bloque de campos en el equipo real.
    /// </summary>
    /// <remarks>
    /// Los siete campos suman 16 bytes y el registro mide 28: quedan 8 de relleno por
    /// delante y 4 por detrás. El lector <b>deduce</b> este 8, no lo tiene grabado; aquí se
    /// usa para fabricar registros que se parezcan a los de verdad.
    /// </remarks>
    public const int DesplazamientoReal = 8;

    /// <summary>Un registro tal como lo escribe el equipo.</summary>
    /// <param name="Id">Identificador del marcador. Cero = registro del propio equipo.</param>
    /// <param name="Fecha">Fecha y hora de la marcación.</param>
    /// <param name="Verificacion">Modo de verificación.</param>
    /// <param name="Tipo">Tipo de marcación.</param>
    public sealed record Marca(
        uint Id,
        DateTime Fecha,
        byte Verificacion = 1,
        byte Tipo = 1,
        ushort Evento = 3);

    /// <summary>
    /// Codifica una fecha como la codifica el equipo.
    /// </summary>
    /// <remarks>
    /// Inverso exacto de <c>LectorRespaldoZkteco.DecodificarTiempo</c>. ZKTeco trata
    /// <b>todos los meses como de 31 días</b>: no es epoch Unix. Verificado contra el
    /// archivo real — 24/08/2026 15:11:43 da 856 451 503, que es lo que trae el primer
    /// registro.
    /// </remarks>
    public static uint CodificarTiempo(DateTime f) =>
        (uint)(((((f.Year - 2000) * 12 + (f.Month - 1)) * 31 + (f.Day - 1)) * 86400)
            + (f.Hour * 3600) + (f.Minute * 60) + f.Second);

    /// <summary>Cabecera sola, sin registros.</summary>
    public static byte[] Cabecera(
        string serie = SerieReal,
        string modelo = ModeloReal,
        uint tamanoRegistro = TamanoRegistro,
        byte[]? anchos = null,
        byte tipoDeCampo = 0x69)
    {
        anchos ??= AnchosReales;

        var bytes = new List<byte> { (byte)serie.Length };
        bytes.AddRange(Encoding.ASCII.GetBytes(serie));
        bytes.Add((byte)modelo.Length);
        bytes.AddRange(Encoding.ASCII.GetBytes(modelo));
        bytes.AddRange(BitConverter.GetBytes(tamanoRegistro));
        bytes.Add((byte)anchos.Length);

        // Un descriptor por campo: tipo, ancho y un byte reservado.
        foreach (var ancho in anchos)
        {
            bytes.AddRange([tipoDeCampo, ancho, 0]);
        }

        return [.. bytes];
    }

    /// <summary>Archivo completo: cabecera más los registros indicados.</summary>
    public static byte[] Archivo(params Marca[] marcas) => Archivo(SerieReal, marcas);

    /// <summary>Archivo completo de un equipo concreto.</summary>
    public static byte[] Archivo(string serie, params Marca[] marcas)
    {
        var bytes = new List<byte>(Cabecera(serie));

        foreach (var marca in marcas)
        {
            bytes.AddRange(Registro(marca));
        }

        return [.. bytes];
    }

    /// <summary>
    /// Un registro de 28 bytes con el relleno en su sitio.
    /// </summary>
    private static byte[] Registro(Marca marca)
    {
        var registro = new byte[TamanoRegistro];
        var offset = DesplazamientoReal;

        BitConverter.GetBytes(marca.Id).CopyTo(registro, offset);          // campo 1 (4)
        offset += 4;
        BitConverter.GetBytes(0u).CopyTo(registro, offset);                // campo 2 (4)
        offset += 4;
        registro[offset++] = marca.Verificacion;                           // campo 3 (1)
        registro[offset++] = marca.Tipo;                                   // campo 4 (1)
        registro[offset++] = (byte)(marca.Evento & 0xff);                   // campo 5 (1)
        registro[offset++] = (byte)(marca.Evento >> 8);                     // campo 6 (1)
        BitConverter.GetBytes(CodificarTiempo(marca.Fecha)).CopyTo(registro, offset); // campo 7

        return registro;
    }
}
