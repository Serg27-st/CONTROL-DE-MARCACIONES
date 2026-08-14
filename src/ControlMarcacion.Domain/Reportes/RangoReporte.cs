using System.Globalization;
using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Reportes;

/// <summary>
/// Forma en que el usuario eligió el tramo de tiempo que cubre un reporte.
/// </summary>
/// <remarks>
/// El periodo de planilla sigue siendo el alcance natural del tareo, pero no es el único
/// que se consulta: "¿qué pasó ayer?", "dame la primera quincena" o "el mes calendario,
/// que no coincide con el corte" son preguntas legítimas que antes obligaban a exportar el
/// periodo entero y filtrar a mano en Excel.
///
/// Los cuatro alcances terminan en lo mismo —un <see cref="RangoReporte"/>—, así que el
/// resto del módulo no sabe cuál se eligió: consulta por fecha y nada más.
/// </remarks>
public enum AlcanceReporte
{
    /// <summary>Un periodo de planilla, por su id. Es el alcance por omisión.</summary>
    Periodo = 0,

    /// <summary>Un mes calendario completo.</summary>
    Mes = 1,

    /// <summary>Un solo día.</summary>
    Dia = 2,

    /// <summary>Un rango de fechas arbitrario.</summary>
    Rango = 3
}

/// <summary>
/// El tramo de fechas que cubre un reporte, ya resuelto y validado.
/// </summary>
/// <remarks>
/// <para>
/// Es lo único que las consultas de reportes necesitan saber sobre el tiempo. Un
/// <see cref="RangoReporte"/> siempre está bien formado: <see cref="Desde"/> nunca es
/// posterior a <see cref="Hasta"/> y el tramo nunca excede
/// <see cref="ResolutorRangoReporte.MaximoDias"/> días. Construirlo solo se puede a través
/// de las factorías de <see cref="ResolutorRangoReporte"/>, que es donde vive la validación.
/// </para>
/// <para>
/// ⚠️ Filtrar el tareo por rango de fechas y no por <c>PeriodoId</c> es deliberado: un rango
/// libre puede cruzar dos periodos, y el <c>TareoDiario</c> ya lleva su fecha. El
/// <c>PeriodoId</c> sigue haciendo falta donde el periodo <b>es</b> el sujeto —cierre,
/// reintegros y el tareo final que se entrega a planilla—, no para acotar una consulta.
/// </para>
/// </remarks>
public sealed record RangoReporte
{
    internal RangoReporte(DateOnly desde, DateOnly hasta, string etiqueta, long? periodoId)
    {
        Desde = desde;
        Hasta = hasta;
        Etiqueta = etiqueta;
        PeriodoId = periodoId;
    }

    public DateOnly Desde { get; }

    public DateOnly Hasta { get; }

    /// <summary>Cómo se describe este alcance en pantalla y en el encabezado del Excel.</summary>
    public string Etiqueta { get; }

    /// <summary>
    /// El periodo del que salió el rango, o <c>null</c> si se eligió por mes, día o fechas.
    /// </summary>
    /// <remarks>
    /// Solo lo consumen las salidas que son por periodo por definición: el tareo final
    /// (sus reintegros se cargan por periodo) y el estado abierto/cerrado del encabezado.
    /// </remarks>
    public long? PeriodoId { get; }

    public int Dias => Hasta.DayNumber - Desde.DayNumber + 1;

    public bool Contiene(DateOnly fecha) => fecha >= Desde && fecha <= Hasta;

    /// <summary>Sufijo ISO para el nombre del archivo exportado: ordena bien alfabéticamente.</summary>
    public string Sufijo => Desde == Hasta
        ? Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : $"{Desde:yyyy-MM-dd}_a_{Hasta:yyyy-MM-dd}";
}

/// <summary>
/// Convierte la elección del usuario en un <see cref="RangoReporte"/> válido, o falla
/// diciendo qué falta.
/// </summary>
/// <remarks>
/// Función pura en <c>Domain</c>: sin base de datos ni reloj, probable con fechas literales.
/// La capa de infraestructura solo aporta las fechas del periodo cuando el alcance es
/// <see cref="AlcanceReporte.Periodo"/>; toda la validación vive aquí para que no se
/// reimplemente distinta en cada pantalla.
/// </remarks>
public static class ResolutorRangoReporte
{
    /// <summary>
    /// Tope de un rango libre, en días.
    /// </summary>
    /// <remarks>
    /// Un año más un día bisiesto. No es una limitación técnica sino de gestión: el tareo se
    /// entrega y se audita por periodo, y un rango de varios años en una sola tabla no
    /// responde ninguna pregunta operativa —solo produce un archivo que nadie revisa y una
    /// consulta que barre la tabla entera—. Quien necesite más, exporta por periodo.
    /// </remarks>
    public const int MaximoDias = 366;

    /// <summary>Un periodo de planilla completo.</summary>
    public static RangoReporte DePeriodo(long periodoId, string codigo, DateOnly inicio, DateOnly fin)
    {
        if (fin < inicio)
        {
            throw new ReglaNegocioException(
                $"El periodo {codigo} tiene la fecha de fin ({fin:dd/MM/yyyy}) antes que la de " +
                $"inicio ({inicio:dd/MM/yyyy}): corrija el periodo antes de reportar sobre él.");
        }

        return new RangoReporte(
            inicio,
            fin,
            $"Periodo {codigo} · {inicio:dd/MM/yyyy} al {fin:dd/MM/yyyy}",
            periodoId);
    }

    /// <summary>Un mes calendario, que no tiene por qué coincidir con el corte del periodo.</summary>
    public static RangoReporte DeMes(int? anio, int? mes)
    {
        if (anio is not { } a || mes is not { } m)
        {
            throw new ReglaNegocioException("Elija el mes y el año que quiere reportar.");
        }

        if (m is < 1 or > 12)
        {
            throw new ReglaNegocioException($"El mes {m} no existe: debe estar entre 1 y 12.");
        }

        if (a is < 2000 or > 2100)
        {
            throw new ReglaNegocioException(
                $"El año {a} está fuera del rango que maneja el sistema (2000-2100).");
        }

        var inicio = new DateOnly(a, m, 1);
        var fin = new DateOnly(a, m, DateTime.DaysInMonth(a, m));

        return new RangoReporte(inicio, fin, $"Mes de {NombreMes(m)} {a}", periodoId: null);
    }

    /// <summary>Un solo día.</summary>
    public static RangoReporte DeDia(DateOnly? dia)
    {
        if (dia is not { } d)
        {
            throw new ReglaNegocioException("Elija el día que quiere reportar.");
        }

        return new RangoReporte(d, d, $"Día {d:dd/MM/yyyy}", periodoId: null);
    }

    /// <summary>Un rango arbitrario de fechas, con tope.</summary>
    public static RangoReporte DeRango(DateOnly? desde, DateOnly? hasta)
    {
        if (desde is not { } d || hasta is not { } h)
        {
            throw new ReglaNegocioException("Indique la fecha de inicio y la de fin del rango.");
        }

        if (h < d)
        {
            throw new ReglaNegocioException(
                $"La fecha de fin ({h:dd/MM/yyyy}) no puede ser anterior a la de inicio " +
                $"({d:dd/MM/yyyy}).");
        }

        var dias = h.DayNumber - d.DayNumber + 1;

        if (dias > MaximoDias)
        {
            throw new ReglaNegocioException(
                $"El rango pedido cubre {dias} días y el máximo es {MaximoDias}. " +
                "Para tramos más largos, exporte periodo por periodo.");
        }

        return new RangoReporte(d, h, $"Del {d:dd/MM/yyyy} al {h:dd/MM/yyyy}", periodoId: null);
    }

    /// <summary>
    /// El nombre del mes en español, con cultura explícita.
    /// </summary>
    /// <remarks>
    /// La aplicación fija <c>es-PE</c> al arrancar, pero un test corre sin ese arranque:
    /// tomar la cultura del hilo haría que la etiqueta cambiara según la máquina.
    /// </remarks>
    private static string NombreMes(int mes)
    {
        var nombre = CultureInfo.GetCultureInfo("es-PE").DateTimeFormat.GetMonthName(mes);

        return string.Concat(char.ToUpperInvariant(nombre[0]), nombre[1..]);
    }
}
