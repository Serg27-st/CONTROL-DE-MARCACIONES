using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Marcaciones.Dto;
using Microsoft.Extensions.Options;

namespace ControlMarcacion.Infrastructure.Marcaciones;

/// <summary>
/// Lee el respaldo binario del marcador (<c>BKtransaction.dat</c>).
/// </summary>
/// <remarks>
/// El archivo es un volcado de la tabla de transacciones del equipo. <b>No está cifrado</b>
/// —entropía 2,85 bits/byte sobre 8, con la serie y el firmware legibles en claro—, sino
/// que es un formato binario propietario que ZKTeco no documenta. Estructura completa en
/// <c>docs/06-formato-archivos-marcador.md</c>.
///
/// <b>El layout no se da por supuesto: se deduce y se comprueba.</b> La cabecera declara el
/// tamaño de registro y el ancho de cada campo, pero no en qué byte del registro empieza el
/// bloque de campos —en la muestra real quedan 8 bytes por delante y 4 por detrás—. En vez
/// de fijar ese 8 a mano, se prueban todos los desplazamientos posibles y se elige el que
/// hace que el <b>último campo decodifique a una fecha plausible en todos los registros</b>.
/// Si ninguno lo consigue, el archivo se rechaza entero en lugar de entregar marcaciones
/// inventadas.
///
/// <b>Dos límites que este formato tiene y el texto no</b>, y que conviene conocer antes de
/// preferirlo:
/// <list type="bullet">
///   <item>El identificador es un entero, así que <b>los ceros a la izquierda se pierden</b>:
///   un equipo que distinga "0014" de "14" —el attlog de julio de 2026 los trae— colapsaría
///   ambos en 14. En texto eso no pasa.</item>
///   <item>De los siete campos, solo están confirmados el <b>primero</b> (identificador) y el
///   <b>último</b> (fecha y hora). El significado exacto de los cuatro bytes intermedios no
///   se pudo determinar con la única muestra disponible, así que el modo de verificación y
///   el tipo de marcación se leen pero <b>no se usan para decidir nada</b> — igual que en el
///   TXT, donde el tipo viene siempre en cero y el emparejamiento lo resuelve el motor.</item>
/// </list>
///
/// La clase es pura: no toca la base de datos, para poder probarla sin infraestructura.
/// </remarks>
public sealed class LectorRespaldoZkteco(IOptions<OpcionesMarcador> opciones) : IFuenteDeMarcaciones
{
    /// <summary>Bytes de un campo que se puede interpretar como fecha y hora.</summary>
    private const int AnchoTimestamp = 4;

    /// <summary>
    /// Año más antiguo que se acepta al deducir el layout.
    /// </summary>
    /// <remarks>
    /// El corte importa: un registro relleno de ceros decodifica a enero del 2000, y sin
    /// este mínimo un desplazamiento equivocado —que cae sobre bytes en cero— parecería
    /// tan válido como el correcto.
    /// </remarks>
    private const int AnioMinimoPlausible = 2015;

    /// <summary>Margen hacia el futuro, para tolerar un reloj algo adelantado.</summary>
    private const int AniosFuturoTolerados = 1;

    /// <summary>
    /// Proporción de registros que deben dar fecha válida para aceptar un desplazamiento.
    /// </summary>
    /// <remarks>
    /// No se exige el 100 % porque un volcado real puede traer alguna fila de sistema o a
    /// medio escribir, y una sola fila rara no debería tumbar la carga entera. Pero el
    /// umbral es alto a propósito: por debajo de esto no se está leyendo el campo correcto.
    /// </remarks>
    private const double ProporcionMinimaValida = 0.9;

    private readonly OpcionesMarcador _opciones = opciones.Value;

    /// <summary>
    /// Convierte el entero de tiempo del equipo en fecha y hora.
    /// </summary>
    /// <remarks>
    /// ZKTeco <b>no usa epoch Unix</b>: empaqueta la fecha tratando todos los meses como de
    /// 31 días. Interpretarlo como segundos desde 1970 da fechas plausibles pero
    /// equivocadas, que es la peor clase de error.
    ///
    /// Devuelve <c>null</c> si el valor no describe una fecha real —el empaquetado permite
    /// combinaciones como el 31 de febrero, que delatan un campo mal elegido—.
    /// </remarks>
    public static DateTime? DecodificarTiempo(uint valor)
    {
        var segundo = (int)(valor % 60);
        valor /= 60;
        var minuto = (int)(valor % 60);
        valor /= 60;
        var hora = (int)(valor % 24);
        valor /= 24;
        var dia = (int)(valor % 31) + 1;
        valor /= 31;
        var mes = (int)(valor % 12) + 1;
        var anio = (int)(valor / 12) + 2000;

        return anio is < 1 or > 9999 || dia > DateTime.DaysInMonth(anio, mes)
            ? null
            : new DateTime(anio, mes, dia, hora, minuto, segundo);
    }

    public ResultadoLectura Leer(Stream contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        var respaldo = DetectorFormatoMarcador.Detectar(contenido);

        if (respaldo is null)
        {
            return Rechazo("El archivo no tiene la cabecera de un respaldo del marcador.");
        }

        var datos = LeerTodo(contenido);
        var cuerpo = datos.AsSpan(respaldo.InicioRegistros);

        if (cuerpo.Length < respaldo.TamanoRegistro)
        {
            return Rechazo(
                $"El respaldo del equipo {respaldo.Serie} no contiene ningún registro completo.");
        }

        if (cuerpo.Length % respaldo.TamanoRegistro != 0)
        {
            return Rechazo(
                $"El respaldo del equipo {respaldo.Serie} está truncado: contiene " +
                $"{cuerpo.Length % respaldo.TamanoRegistro} byte(s) de un registro incompleto.");
        }

        var registros = cuerpo.Length / respaldo.TamanoRegistro;

        // El último campo declarado es el que tiene que ser la fecha. Si no mide cuatro
        // bytes, este no es el esquema que sabemos leer y no hay que forzarlo.
        if (respaldo.AnchosDeCampo[^1] != AnchoTimestamp)
        {
            return Rechazo(
                $"El respaldo del equipo {respaldo.Serie} declara un esquema desconocido: " +
                $"su último campo mide {respaldo.AnchosDeCampo[^1]} bytes y se esperaban " +
                $"{AnchoTimestamp} con la fecha y hora.");
        }

        var desplazamiento = DeducirDesplazamiento(cuerpo, respaldo, registros);

        if (desplazamiento is null)
        {
            return Rechazo(
                $"No se pudo interpretar el respaldo del equipo {respaldo.Serie} " +
                $"({respaldo.Modelo}): ninguna disposición de sus {respaldo.Campos} campos " +
                "produce fechas válidas. Cargue el archivo de marcaciones " +
                "(SERIE_attlog.dat) en su lugar.");
        }

        return Extraer(cuerpo, respaldo, registros, desplazamiento.Value);
    }

    /// <summary>
    /// Recorre los registros ya sabiendo dónde están los campos.
    /// </summary>
    private ResultadoLectura Extraer(
        ReadOnlySpan<byte> cuerpo,
        RespaldoZkteco respaldo,
        int registros,
        int desplazamiento)
    {
        var marcaciones = new List<MarcacionLeida>();
        var rechazadas = new List<LineaRechazada>();
        var ignoradas = new List<EventoEquipoIgnorado>();
        var limiteAntiguedad = DateTime.Now.AddDays(-_opciones.DiasAntiguedadMaxima);

        for (var i = 0; i < registros; i++)
        {
            var registro = cuerpo.Slice(i * respaldo.TamanoRegistro, respaldo.TamanoRegistro);
            var numero = i + 1;
            var campos = LeerCampos(registro, respaldo, desplazamiento);
            var original = Convert.ToHexString(registro);

            var fechaHora = DecodificarTiempo(campos[^1]);

            if (fechaHora is null)
            {
                rechazadas.Add(new LineaRechazada(
                    numero, original, "La fecha y hora del registro no es una fecha real."));
                continue;
            }

            // Un identificador en cero no es de nadie. En la muestra real aparece en los
            // registros que el equipo escribe para sí mismo, y atribuirlos a un trabajador
            // sería inventarse una marcación.
            if (campos[0] == 0)
            {
                // El timestamp y el layout ya fueron validados. Un ID físico cero no se
                // atribuye a nadie: es un evento del equipo, no una marcación inválida.
                // Los campos 5 y 6 forman el código opaco observado en el respaldo; se
                // conserva sin inventarle un significado funcional.
                ignoradas.Add(new EventoEquipoIgnorado(numero, CodigoEvento(campos)));
                continue;
            }

            // Misma protección que en el TXT: un equipo con la fecha mal configurada
            // contaminaría periodos ya cerrados.
            if (fechaHora < limiteAntiguedad)
            {
                rechazadas.Add(new LineaRechazada(
                    numero, original,
                    $"La fecha {fechaHora:dd/MM/yyyy} supera la antigüedad máxima de " +
                    $"{_opciones.DiasAntiguedadMaxima} días. Verifique la fecha configurada " +
                    "en el marcador."));
                continue;
            }

            marcaciones.Add(new MarcacionLeida(
                campos[0].ToString(System.Globalization.CultureInfo.InvariantCulture),
                fechaHora.Value,
                CampoOpcional(campos, 2),
                CampoOpcional(campos, 3),
                numero,
                original));
        }

        // La serie viaja hasta la ingesta para poder contrastarla con el equipo que declaró
        // quien carga: es la única comprobación automática de que el archivo salió de ese
        // aparato y no del de al lado.
        return new ResultadoLectura(marcaciones, rechazadas, respaldo.Serie, ignoradas);
    }

    private static uint CodigoEvento(uint[] campos) => campos[4] | (campos[5] << 8);

    /// <summary>
    /// Busca en qué byte del registro empieza el bloque de campos.
    /// </summary>
    /// <remarks>
    /// Se prueba cada desplazamiento posible y gana el que más registros deja con una fecha
    /// plausible, siempre que supere <see cref="ProporcionMinimaValida"/>. Es lo que evita
    /// tener grabado a fuego un offset descubierto sobre una sola muestra: si el equipo
    /// cambia el relleno, esto se reacomoda solo, y si el archivo no es lo que creemos,
    /// ningún desplazamiento pasa el umbral y la carga falla en vez de mentir.
    ///
    /// ⚠️ <b>La fecha por sí sola no basta para desempatar.</b> Sobre un archivo con pocos
    /// registros hay desplazamientos equivocados que también producen una fecha creíble: en
    /// el respaldo real, leer cuatro bytes tres posiciones antes da 2027-02-23, tan
    /// plausible como la buena. Ese desplazamiento gana por ser menor y corre el
    /// identificador, que pasa de <c>2</c> a <c>33 554 432</c> — una marcación con fecha
    /// correcta atribuida a nadie.
    ///
    /// Por eso se exige además que la fecha quede <b>alineada a cuatro bytes</b>: en un
    /// volcado de estructuras, un entero de 32 bits lo está siempre, y eso descarta de un
    /// golpe los desplazamientos corridos. Si ninguno alineado funciona, se vuelve a probar
    /// sin esa condición, para no depender de una suposición sobre el compilador del
    /// firmware.
    /// </remarks>
    private static int? DeducirDesplazamiento(
        ReadOnlySpan<byte> cuerpo,
        RespaldoZkteco respaldo,
        int registros)
    {
        var holgura = respaldo.TamanoRegistro - respaldo.BytesDeclarados;

        if (holgura < 0)
        {
            return null;
        }

        var offsetTimestamp = respaldo.BytesDeclarados - AnchoTimestamp;
        var minimoExigido = Math.Max(1, (int)Math.Ceiling(registros * ProporcionMinimaValida));

        // Dos pasadas: la primera exige la fecha alineada a cuatro bytes y solo si esa no
        // deja ningún candidato se repite sin la condición. (Va como bucle y no como
        // función local porque un ReadOnlySpan no se puede capturar en una.)
        for (var pasada = 0; pasada < 2; pasada++)
        {
            var soloAlineados = pasada == 0;
            int? mejor = null;
            var mejorValidos = 0;

            for (var desplazamiento = 0; desplazamiento <= holgura; desplazamiento++)
            {
                var offset = desplazamiento + offsetTimestamp;

                if (soloAlineados && offset % AnchoTimestamp != 0)
                {
                    continue;
                }

                var validos = ContarFechasPlausibles(cuerpo, respaldo, registros, offset);

                if (validos > mejorValidos)
                {
                    mejorValidos = validos;
                    mejor = desplazamiento;
                }
            }

            if (mejorValidos >= minimoExigido)
            {
                return mejor;
            }
        }

        return null;
    }

    private static int ContarFechasPlausibles(
        ReadOnlySpan<byte> cuerpo,
        RespaldoZkteco respaldo,
        int registros,
        int offset)
    {
        var maximo = DateTime.Now.AddYears(AniosFuturoTolerados);
        var validos = 0;

        for (var i = 0; i < registros; i++)
        {
            var registro = cuerpo.Slice(i * respaldo.TamanoRegistro, respaldo.TamanoRegistro);
            var fecha = DecodificarTiempo(LeerEntero(registro, offset, AnchoTimestamp));

            if (fecha is not null && fecha.Value.Year >= AnioMinimoPlausible && fecha <= maximo)
            {
                validos++;
            }
        }

        return validos;
    }

    /// <summary>Extrae los campos declarados de un registro, en orden.</summary>
    private static uint[] LeerCampos(
        ReadOnlySpan<byte> registro,
        RespaldoZkteco respaldo,
        int desplazamiento)
    {
        var campos = new uint[respaldo.Campos];
        var offset = desplazamiento;

        for (var i = 0; i < respaldo.Campos; i++)
        {
            campos[i] = LeerEntero(registro, offset, respaldo.AnchosDeCampo[i]);
            offset += respaldo.AnchosDeCampo[i];
        }

        return campos;
    }

    /// <summary>Entero sin signo de <paramref name="ancho"/> bytes, little-endian.</summary>
    private static uint LeerEntero(ReadOnlySpan<byte> datos, int offset, int ancho)
    {
        if (offset < 0 || offset + ancho > datos.Length)
        {
            return 0;
        }

        uint valor = 0;

        for (var i = 0; i < ancho; i++)
        {
            valor |= (uint)datos[offset + i] << (8 * i);
        }

        return valor;
    }

    private static string? CampoOpcional(uint[] campos, int indice) =>
        indice < campos.Length
            ? campos[indice].ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;

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
    /// Rechaza el archivo entero con un solo motivo.
    /// </summary>
    /// <remarks>
    /// Una línea y no una por registro: el problema es el archivo, y repetir el mismo texto
    /// mil veces entierra la única frase que hay que leer.
    /// </remarks>
    private static ResultadoLectura Rechazo(string motivo) =>
        new([], [new LineaRechazada(1, string.Empty, motivo)]);
}
