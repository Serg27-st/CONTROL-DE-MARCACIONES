using System.Text;

namespace ControlMarcacion.Infrastructure.Marcaciones;

/// <summary>
/// Cabecera de un respaldo del marcador: qué equipo lo escribió y cómo son sus registros.
/// </summary>
/// <param name="Serie">Número de serie del equipo, tal como lo declara la cabecera.</param>
/// <param name="Modelo">Plataforma y firmware, p. ej. <c>ZAM70-NF28VA-Ver3.0.15</c>.</param>
/// <param name="TamanoRegistro">Bytes que ocupa cada registro, incluido el relleno.</param>
/// <param name="AnchosDeCampo">Ancho en bytes de cada campo, en el orden declarado.</param>
/// <param name="InicioRegistros">Offset del primer registro.</param>
public sealed record RespaldoZkteco(
    string Serie,
    string Modelo,
    int TamanoRegistro,
    IReadOnlyList<int> AnchosDeCampo,
    int InicioRegistros)
{
    public int Campos => AnchosDeCampo.Count;

    /// <summary>Bytes que suman los campos declarados, sin el relleno del registro.</summary>
    public int BytesDeclarados => AnchosDeCampo.Sum();
}

/// <summary>
/// Distingue el archivo de asistencia del <b>respaldo interno</b> del equipo.
/// </summary>
/// <remarks>
/// El ZKTeco exporta por USB dos cosas muy distintas y el menú las ofrece juntas:
/// <list type="bullet">
///   <item>La <b>descarga de marcaciones</b> produce <c>SERIE_attlog.dat</c> (o
///   <c>.txt</c>): texto tabulado, una línea por marcación. Lo lee
///   <see cref="LectorTxtZkteco"/> — la extensión da igual, el contenido es el mismo.</item>
///   <item>El <b>respaldo de datos</b> produce <c>BKtransaction.dat</c>: un volcado
///   binario de las tablas del equipo, con su esquema por delante. Lo lee
///   <see cref="LectorRespaldoZkteco"/>.</item>
/// </list>
///
/// Este detector solo decide cuál de los dos es; el reparto lo hace
/// <see cref="LectorMarcadorAutomatico"/>.
/// </remarks>
public static class DetectorFormatoMarcador
{
    /// <summary>
    /// Bytes de cabecera que hace falta mirar. La del respaldo real ocupa 63; el tope
    /// cubre de sobra el caso extremo de <see cref="CamposMaximos"/> descriptores.
    /// </summary>
    private const int BytesInspeccionados = 512;

    // Rangos plausibles de la cabecera. Son estrechos a propósito: un attlog de texto
    // empieza con espacios y dígitos, y tiene que quedar fuera sin margen de duda.
    private const int LongitudMinimaTexto = 4;
    private const int LongitudMaximaSerie = 40;
    private const int LongitudMaximaModelo = 64;
    private const int TamanoRegistroMinimo = 8;
    private const int TamanoRegistroMaximo = 1024;
    private const int CamposMaximos = 64;

    /// <summary>Cada descriptor de campo son tres bytes: tipo, ancho y un reservado.</summary>
    private const int BytesPorDescriptor = 3;

    /// <summary>Marca de tipo entero que usan los descriptores del equipo real.</summary>
    private const byte TipoEntero = 0x69;

    /// <summary>
    /// Devuelve los datos del respaldo si el contenido lo es, o <c>null</c> si no.
    /// </summary>
    /// <remarks>
    /// Deja el <paramref name="contenido"/> en la posición en que lo encontró: quien
    /// llama sigue leyéndolo como si nada.
    /// </remarks>
    public static RespaldoZkteco? Detectar(Stream contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        if (!contenido.CanSeek)
        {
            return null;
        }

        var posicion = contenido.Position;

        try
        {
            var cabecera = new byte[BytesInspeccionados];
            var leidos = contenido.ReadAtLeast(cabecera, BytesInspeccionados, throwOnEndOfStream: false);
            return Detectar(cabecera.AsSpan(0, leidos));
        }
        finally
        {
            contenido.Position = posicion;
        }
    }

    /// <summary>
    /// La misma comprobación sobre los bytes ya leídos.
    /// </summary>
    /// <remarks>
    /// La cabecera son dos cadenas con prefijo de longitud —serie y modelo—, el tamaño de
    /// registro, el número de campos y un descriptor por campo. Se exige todo a la vez:
    /// cualquier señal por separado daría falsos positivos.
    /// </remarks>
    public static RespaldoZkteco? Detectar(ReadOnlySpan<byte> cabecera)
    {
        if (!LeerCadena(cabecera, 0, LongitudMaximaSerie, out var serie, out var siguiente) ||
            !LeerCadena(cabecera, siguiente, LongitudMaximaModelo, out var modelo, out siguiente))
        {
            return null;
        }

        // Tras las dos cadenas: uint32 con el tamaño de registro y un byte con el número
        // de campos. Que ambos caigan en un rango sensato es lo que descarta la casualidad.
        if (siguiente + 5 > cabecera.Length)
        {
            return null;
        }

        var tamanoRegistro = BitConverter.ToUInt32(cabecera[siguiente..(siguiente + 4)]);

        if (tamanoRegistro is < TamanoRegistroMinimo or > TamanoRegistroMaximo)
        {
            return null;
        }

        int campos = cabecera[siguiente + 4];

        if (campos is 0 or > CamposMaximos)
        {
            return null;
        }

        siguiente += 5;

        if (!LeerDescriptores(cabecera, siguiente, campos, (int)tamanoRegistro, out var anchos))
        {
            return null;
        }

        return new RespaldoZkteco(
            serie,
            modelo,
            (int)tamanoRegistro,
            anchos,
            siguiente + (campos * BytesPorDescriptor));
    }

    /// <summary>
    /// Lee los descriptores de campo y comprueba que describan algo que quepa en el
    /// registro.
    /// </summary>
    /// <remarks>
    /// Un ancho de cero, uno mayor que el propio registro, o una suma que no cabe, señalan
    /// que lo que se está leyendo no es un esquema. Vale más devolver <c>null</c> aquí que
    /// dejar que el parser interprete basura como marcaciones.
    /// </remarks>
    private static bool LeerDescriptores(
        ReadOnlySpan<byte> cabecera,
        int inicio,
        int campos,
        int tamanoRegistro,
        out IReadOnlyList<int> anchos)
    {
        anchos = [];

        if (inicio + (campos * BytesPorDescriptor) > cabecera.Length)
        {
            return false;
        }

        var leidos = new int[campos];
        var suma = 0;

        for (var i = 0; i < campos; i++)
        {
            var descriptor = cabecera.Slice(inicio + (i * BytesPorDescriptor), BytesPorDescriptor);

            if (descriptor[0] != TipoEntero)
            {
                return false;
            }

            int ancho = descriptor[1];

            if (ancho is 0 || ancho > tamanoRegistro)
            {
                return false;
            }

            leidos[i] = ancho;
            suma += ancho;
        }

        if (suma > tamanoRegistro)
        {
            return false;
        }

        anchos = leidos;
        return true;
    }

    /// <summary>
    /// Lee una cadena con prefijo de longitud de un byte, exigiendo que parezca un
    /// identificador de equipo y no texto cualquiera.
    /// </summary>
    /// <remarks>
    /// Solo se aceptan letras, dígitos, guion, punto y subrayado. Sin esta restricción la
    /// primera línea de un attlog —que empieza con 0x20, y 32 es una longitud
    /// perfectamente plausible— se leería como una cadena válida.
    /// </remarks>
    private static bool LeerCadena(
        ReadOnlySpan<byte> datos,
        int inicio,
        int longitudMaxima,
        out string valor,
        out int siguiente)
    {
        valor = string.Empty;
        siguiente = inicio;

        if (inicio >= datos.Length)
        {
            return false;
        }

        int longitud = datos[inicio];

        if (longitud < LongitudMinimaTexto || longitud > longitudMaxima ||
            inicio + 1 + longitud > datos.Length)
        {
            return false;
        }

        var texto = datos.Slice(inicio + 1, longitud);

        foreach (var b in texto)
        {
            if (!EsCaracterDeIdentificador(b))
            {
                return false;
            }
        }

        valor = Encoding.ASCII.GetString(texto);
        siguiente = inicio + 1 + longitud;
        return true;
    }

    private static bool EsCaracterDeIdentificador(byte b) =>
        b is (>= (byte)'0' and <= (byte)'9')
            or (>= (byte)'A' and <= (byte)'Z')
            or (>= (byte)'a' and <= (byte)'z')
            or (byte)'-' or (byte)'.' or (byte)'_';
}
