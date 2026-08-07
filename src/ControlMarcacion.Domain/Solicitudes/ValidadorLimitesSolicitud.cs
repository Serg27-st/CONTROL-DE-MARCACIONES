using System.Globalization;
using ControlMarcacion.Domain.Catalogos;

namespace ControlMarcacion.Domain.Solicitudes;

/// <summary>
/// Topes de días de una solicitud de ausencia.
/// </summary>
/// <remarks>
/// Es una función <b>pura</b>: no consulta la base de datos ni el reloj. Quien llama —el
/// servicio de <c>Application</c>— resuelve cuántos días ya tiene acumulados el trabajador
/// y se los pasa; aquí solo se decide si el registro cabe o no.
///
/// Los topes vienen del catálogo (<see cref="TipoAusencia.MaximoDiasPorEvento"/> y
/// <see cref="TipoAusencia.MaximoDiasPorAnioCalendario"/>), no de un <c>switch</c> por código:
/// agregar un tipo con tope propio tiene que seguir siendo un INSERT y no un despliegue.
///
/// La única excepción son los topes de vacaciones (15 días de venta y 30 de goce por periodo
/// vacacional): dependen del <b>subtipo</b>, que no es una fila del catálogo sino un atributo
/// de la solicitud, así que no hay dónde guardarlos como dato sin inventar un catálogo nuevo
/// para dos valores. Están declarados como constantes públicas para que se vean y se prueben.
/// </remarks>
public static class ValidadorLimitesSolicitud
{
    /// <summary>Código del tipo de ausencia "Vacaciones" en el catálogo sembrado.</summary>
    public const string CodigoVacaciones = "VAC";

    /// <summary>Días de vacaciones efectivamente descansados.</summary>
    public const string SubtipoGozadas = "GOZADAS";

    /// <summary>Días de vacaciones vendidos al empleador.</summary>
    public const string SubtipoVenta = "VENTA";

    /// <summary>Tope de días de vacaciones gozadas por periodo vacacional.</summary>
    public const int MaximoDiasGozadasPorPeriodo = 30;

    /// <summary>
    /// Tope de días de vacaciones vendidos por periodo vacacional: la ley permite disponer
    /// de hasta la mitad de los 30 días.
    /// </summary>
    public const int MaximoDiasVentaPorPeriodo = 15;

    /// <summary>
    /// Comprueba los topes aplicables al tipo. Devuelve <c>null</c> si la solicitud cabe.
    /// </summary>
    /// <param name="tipo">Tipo de ausencia del catálogo, con sus topes ya cargados.</param>
    /// <param name="fechaInicio">Primer día de la ausencia.</param>
    /// <param name="fechaFin">Último día de la ausencia, inclusive.</param>
    /// <param name="diasYaAcumulados">
    /// Días que el trabajador ya tiene registrados en el cupo que corresponde a este tipo:
    /// el año calendario de <paramref name="fechaInicio"/> cuando el tope es anual, o el
    /// mismo subtipo y periodo vacacional cuando se trata de vacaciones. Lo calcula quien
    /// llama; este método no toca base de datos.
    /// </param>
    /// <param name="subtipoVacaciones">
    /// <see cref="SubtipoGozadas"/> o <see cref="SubtipoVenta"/>. Solo se mira si el tipo es
    /// <see cref="CodigoVacaciones"/>; en cualquier otro tipo se ignora.
    /// </param>
    /// <param name="periodoVacacional">Periodo vacacional en formato AAAA-AAAA.</param>
    /// <returns>El mensaje que se le muestra al usuario, o <c>null</c> si no hay problema.</returns>
    public static string? Validar(
        TipoAusencia tipo,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        int diasYaAcumulados,
        string? subtipoVacaciones,
        string? periodoVacacional)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        // Del 1 al 10 son diez días, no nueve: la ausencia incluye ambos extremos.
        var dias = fechaFin.DayNumber - fechaInicio.DayNumber + 1;

        return ValidarTopePorEvento(tipo, dias)
            ?? ValidarTopePorAnioCalendario(tipo, fechaInicio, dias, diasYaAcumulados)
            ?? ValidarVacaciones(tipo, dias, diasYaAcumulados, subtipoVacaciones, periodoVacacional);
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Tope que se agota en cada evento, no en el año: licencia paternal y por maternidad.
    /// </summary>
    /// <remarks>
    /// No mira el acumulado a propósito. Un segundo hijo en el mismo año da derecho a otra
    /// licencia completa; contarlas juntas negaría un derecho que la ley sí reconoce.
    /// </remarks>
    private static string? ValidarTopePorEvento(TipoAusencia tipo, int dias)
    {
        if (tipo.MaximoDiasPorEvento is not { } maximo || dias <= maximo)
        {
            return null;
        }

        return $"{ConArticulo(tipo.Nombre)} no puede exceder {maximo} días{EnSemanas(maximo)}. " +
               $"Se solicitaron {dias} días.";
    }

    /// <summary>Tope que se consume a lo largo del año calendario: descanso médico.</summary>
    private static string? ValidarTopePorAnioCalendario(
        TipoAusencia tipo,
        DateOnly fechaInicio,
        int dias,
        int diasYaAcumulados)
    {
        if (tipo.MaximoDiasPorAnioCalendario is not { } maximo
            || diasYaAcumulados + dias <= maximo)
        {
            return null;
        }

        var exceso = diasYaAcumulados + dias - maximo;

        return $"No se puede registrar: el trabajador ya acumula {diasYaAcumulados} día(s) de " +
               $"{EnMinuscula(tipo.Nombre)} en el año {fechaInicio.Year}. El máximo permitido es " +
               $"{maximo} días por año. Los días solicitados ({dias}) exceden el límite en " +
               $"{exceso} día(s).";
    }

    /// <summary>
    /// Topes de vacaciones, que se cuentan por periodo vacacional y por subtipo.
    /// </summary>
    /// <remarks>
    /// El subtipo es obligatorio: sin él no hay cupo contra el cual contrastar, y dejarlo
    /// pasar sería una vía silenciosa para saltarse los topes de 15 y 30 días.
    /// </remarks>
    private static string? ValidarVacaciones(
        TipoAusencia tipo,
        int dias,
        int diasYaAcumulados,
        string? subtipoVacaciones,
        string? periodoVacacional)
    {
        if (!string.Equals(tipo.Codigo?.Trim(), CodigoVacaciones, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var subtipo = subtipoVacaciones?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(subtipo))
        {
            return "Debe indicar si las vacaciones son gozadas o venta de vacaciones.";
        }

        if (subtipo is not (SubtipoGozadas or SubtipoVenta))
        {
            return $"El subtipo de vacaciones '{subtipoVacaciones!.Trim()}' no es válido: " +
                   $"debe ser '{SubtipoGozadas}' o '{SubtipoVenta}'.";
        }

        var periodo = periodoVacacional?.Trim();

        if (string.IsNullOrEmpty(periodo))
        {
            return "Debe indicar el periodo vacacional al que se imputan estos días " +
                   "(formato AAAA-AAAA, por ejemplo 2025-2026).";
        }

        if (!TieneFormatoDePeriodo(periodo, out var primerAnio, out var segundoAnio))
        {
            return $"El periodo vacacional '{periodo}' no es válido: debe tener el formato " +
                   "AAAA-AAAA (por ejemplo, 2025-2026).";
        }

        // La expresión regular del formulario acepta 2025-2027; que el segundo año sea el
        // siguiente del primero solo se puede comprobar aquí.
        if (segundoAnio != primerAnio + 1)
        {
            return $"El periodo vacacional '{periodo}' no es válido: el segundo año debe ser " +
                   "el siguiente del primero (por ejemplo, 2025-2026).";
        }

        var (maximo, etiqueta) = subtipo == SubtipoVenta
            ? (MaximoDiasVentaPorPeriodo, "venta de vacaciones")
            : (MaximoDiasGozadasPorPeriodo, "vacaciones gozadas");

        if (diasYaAcumulados + dias <= maximo)
        {
            return null;
        }

        return $"No se puede registrar: el trabajador ya tiene {diasYaAcumulados} día(s) de " +
               $"{etiqueta} en el periodo {periodo}. El máximo es {maximo} días por periodo.";
    }

    /// <summary>Valida el formato AAAA-AAAA sin recurrir a una expresión regular.</summary>
    private static bool TieneFormatoDePeriodo(string periodo, out int primerAnio, out int segundoAnio)
    {
        primerAnio = 0;
        segundoAnio = 0;

        return periodo.Length == 9
               && periodo[4] == '-'
               && int.TryParse(
                   periodo.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out primerAnio)
               && int.TryParse(
                   periodo.AsSpan(5, 4), NumberStyles.None, CultureInfo.InvariantCulture, out segundoAnio);
    }

    /// <summary>
    /// El nombre del tipo con su artículo, para armar mensajes legibles sin cablear el
    /// nombre de cada tipo en el código.
    /// </summary>
    /// <remarks>
    /// El género se deduce de la primera palabra ("licencia" → La, "permiso" → El). Es una
    /// heurística, pero acierta en todos los tipos sembrados y evita un <c>switch</c> por
    /// código que habría que tocar cada vez que GCA agregue un tipo nuevo.
    /// </remarks>
    private static string ConArticulo(string nombre)
    {
        var texto = EnMinuscula(nombre);

        var primeraPalabra = texto.Split(' ', 2)[0];
        var articulo = primeraPalabra.EndsWith('a') ? "La" : "El";

        return $"{articulo} {texto}";
    }

    /// <summary>
    /// Baja solo la inicial: el resto del nombre puede llevar mayúsculas legítimas y
    /// pasarlo entero a minúsculas las perdería.
    /// </summary>
    private static string EnMinuscula(string nombre)
    {
        var texto = nombre?.Trim() ?? string.Empty;

        return texto.Length == 0
            ? texto
            : char.ToLowerInvariant(texto[0]) + texto[1..];
    }

    /// <summary>
    /// Traduce el tope a semanas cuando es múltiplo exacto de siete, que es como lo enuncia
    /// la norma para la licencia por maternidad (98 días = 14 semanas).
    /// </summary>
    private static string EnSemanas(int dias) =>
        dias > 0 && dias % 7 == 0 ? $" ({dias / 7} semanas)" : string.Empty;
}
