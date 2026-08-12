using ControlMarcacion.Domain.Catalogos;

namespace ControlMarcacion.Domain.Personal;

/// <summary>
/// Formato y longitud del número de documento de identidad.
/// </summary>
/// <remarks>
/// Es una función <b>pura</b>, igual que <c>ValidadorLimitesSolicitud</c>: no consulta la
/// base de datos ni el reloj. Quien llama trae la fila del catálogo ya cargada y aquí solo
/// se decide si el número la cumple.
///
/// Las reglas viven en <see cref="TipoDocumento"/> —<see cref="TipoDocumento.LongitudExacta"/>,
/// <see cref="TipoDocumento.LongitudMaxima"/> y <see cref="TipoDocumento.SoloDigitos"/>—, no
/// en un <c>switch</c> por código. Escribir <c>if (codigo == "DNI") ...</c> aquí obligaría a
/// recompilar para dar de alta un tipo nuevo, que es justo lo que el catálogo evita.
///
/// El número se compara <b>tal cual</b>, sin recortar ceros ni normalizar: el documento es
/// texto en todo el sistema porque los ceros a la izquierda son significativos y perderlos
/// rompe el cruce con la planilla.
/// </remarks>
public static class ValidadorDocumentoIdentidad
{
    /// <summary>
    /// Comprueba el número contra las reglas de la fila del catálogo.
    /// </summary>
    /// <param name="tipo">Fila del catálogo, con sus longitudes ya cargadas.</param>
    /// <param name="numero">Número tal como lo escribió el usuario.</param>
    /// <returns>El mensaje que se le muestra al usuario, o <c>null</c> si no hay problema.</returns>
    public static string? Validar(TipoDocumento tipo, string? numero)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return Validar(tipo.Codigo, tipo.LongitudExacta, tipo.LongitudMaxima, tipo.SoloDigitos, numero);
    }

    /// <summary>
    /// Comprueba el número contra las reglas sueltas del tipo.
    /// </summary>
    /// <remarks>
    /// Esta es la sobrecarga que usa <c>Application</c>: allí el tipo de documento llega
    /// como DTO desde el catálogo, no como entidad, y fabricar una entidad de mentira solo
    /// para poder validar sería peor que recibir los cuatro datos que de verdad importan.
    /// </remarks>
    /// <param name="codigo">Código del tipo (DNI, CE…), solo para redactar el mensaje.</param>
    /// <param name="longitudExacta">Longitud exigida, si el tipo tiene una.</param>
    /// <param name="longitudMaxima">Techo de longitud cuando no hay exacta.</param>
    /// <param name="soloDigitos">El tipo admite únicamente dígitos.</param>
    /// <param name="numero">Número tal como lo escribió el usuario.</param>
    /// <returns>El mensaje que se le muestra al usuario, o <c>null</c> si no hay problema.</returns>
    public static string? Validar(
        string codigo,
        int? longitudExacta,
        int? longitudMaxima,
        bool soloDigitos,
        string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return "El número de documento es obligatorio.";
        }

        var valor = numero.Trim();

        // char.IsAsciiDigit y no char.IsDigit: este último acepta los dígitos de otros
        // alfabetos (árabe-índico, devanagari…), que se ven como números pero no cruzan
        // con nada. Un copiar y pegar desde un PDF los introduce sin que nadie lo note.
        if (soloDigitos && !valor.All(char.IsAsciiDigit))
        {
            return $"El {codigo} admite solo dígitos del 0 al 9.";
        }

        var unidad = soloDigitos ? "dígitos" : "caracteres";

        if (longitudExacta is { } exacta && valor.Length != exacta)
        {
            return $"El {codigo} debe tener exactamente {exacta} {unidad}; " +
                   $"se ingresaron {valor.Length}.";
        }

        if (longitudExacta is null && longitudMaxima is { } maxima && valor.Length > maxima)
        {
            return $"El {codigo} admite como máximo {maxima} {unidad}; " +
                   $"se ingresaron {valor.Length}.";
        }

        return null;
    }
}
