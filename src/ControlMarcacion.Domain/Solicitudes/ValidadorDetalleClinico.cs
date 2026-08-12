using ControlMarcacion.Domain.Catalogos;

namespace ControlMarcacion.Domain.Solicitudes;

/// <summary>
/// Detalle clínico exigible a una solicitud según su tipo de ausencia.
/// </summary>
/// <remarks>
/// Es una función <b>pura</b>, como el resto de validadores del dominio: no consulta la
/// base de datos ni el reloj.
///
/// Qué tipos lo exigen se decide por el <b>comportamiento</b> del catálogo
/// (<see cref="ComportamientoAusencia.Subsidiada"/>), no por una lista de códigos escrita
/// aquí. Hoy eso alcanza a descanso médico, subsidio por enfermedad y subsidio por
/// accidente; si GCA da de alta otro tipo subsidiado, hereda la exigencia sin tocar código.
/// Un <c>switch</c> por código —"si es DM o SUBE"— habría dejado fuera al tipo nuevo en
/// silencio, y la hoja del tareo habría vuelto a salir con las columnas vacías.
/// </remarks>
public static class ValidadorDetalleClinico
{
    /// <summary>Largo máximo del diagnóstico, alineado con la columna de la base.</summary>
    public const int LargoMaximoDiagnostico = 250;

    /// <summary>
    /// El tipo de ausencia exige contingencia, diagnóstico y centro de atención.
    /// </summary>
    public static bool Exige(TipoAusencia tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return tipo.Comportamiento == ComportamientoAusencia.Subsidiada;
    }

    /// <summary>
    /// Comprueba el detalle clínico contra el tipo. Devuelve <c>null</c> si está correcto.
    /// </summary>
    /// <param name="tipo">Tipo de ausencia elegido, del catálogo.</param>
    /// <param name="contingencia">Origen de la incapacidad.</param>
    /// <param name="diagnostico">Diagnóstico del certificado médico.</param>
    /// <param name="centroAtencion">Dónde se atendió al trabajador.</param>
    /// <returns>El mensaje que se le muestra al usuario, o <c>null</c> si no hay problema.</returns>
    public static string? Validar(
        TipoAusencia tipo,
        Contingencia? contingencia,
        string? diagnostico,
        CentroAtencion? centroAtencion)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        if (!Exige(tipo))
        {
            return null;
        }

        if (contingencia is null)
        {
            return $"Indique la contingencia: {tipo.Nombre} la exige para poder sustentar " +
                   "quién asume el costo del subsidio.";
        }

        if (centroAtencion is null)
        {
            return $"Indique el centro de atención: {tipo.Nombre} lo exige.";
        }

        if (string.IsNullOrWhiteSpace(diagnostico))
        {
            return $"Indique el diagnóstico que consta en el certificado médico: " +
                   $"{tipo.Nombre} lo exige.";
        }

        if (diagnostico.Trim().Length > LargoMaximoDiagnostico)
        {
            return $"El diagnóstico no puede superar los {LargoMaximoDiagnostico} caracteres.";
        }

        return null;
    }

    /// <summary>
    /// Deja el detalle en <c>null</c> cuando el tipo no lo admite.
    /// </summary>
    /// <remarks>
    /// Se aplica al guardar. Sin esto, cambiar una solicitud de descanso médico a permiso
    /// conservaría el diagnóstico anterior: un dato sensible de salud sobreviviendo en una
    /// solicitud que ya no tiene ningún motivo para llevarlo.
    /// </remarks>
    public static (Contingencia? Contingencia, string? Diagnostico, CentroAtencion? CentroAtencion)
        Depurar(
            TipoAusencia tipo,
            Contingencia? contingencia,
            string? diagnostico,
            CentroAtencion? centroAtencion)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return Exige(tipo)
            ? (contingencia, diagnostico?.Trim(), centroAtencion)
            : (null, null, null);
    }
}
