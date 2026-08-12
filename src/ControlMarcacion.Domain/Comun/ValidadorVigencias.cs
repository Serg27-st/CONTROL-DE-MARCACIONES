namespace ControlMarcacion.Domain.Comun;

/// <summary>
/// Comprueba que un conjunto de vigencias por fecha describa una línea de tiempo sana.
/// </summary>
/// <remarks>
/// Puro y sin base de datos, para poder probarlo con fechas literales.
///
/// <para>Es la defensa que faltaba en la rotación de turnos. El esquema solo declara un
/// índice <b>no único</b> sobre <c>(TrabajadorId, FechaInicio, FechaFin)</c>, así que dos
/// asignaciones solapadas se guardaban sin una sola queja y el motor resolvía por una de
/// las dos en silencio. Con obreros que rotan cada una o dos semanas eso no es una
/// hipótesis de laboratorio: es un guardado a medias durante la rotación quincenal, y el
/// tareo del mes sale mal sin que nada lo señale.</para>
///
/// <para>Se valida al guardar y no al calcular a propósito: el cálculo debe poder
/// reconstruir cualquier periodo antiguo sin negarse por datos que ya están en la base.
/// Quien tiene que decir «esto no» es quien lo escribe.</para>
/// </remarks>
public static class ValidadorVigencias
{
    /// <summary>
    /// Lanza <see cref="ReglaNegocioException"/> si hay rangos imposibles o solapados.
    /// </summary>
    /// <param name="vigencias">Las vigencias de un mismo trabajador y un mismo concepto.</param>
    /// <param name="queSeAsigna">
    /// Cómo nombrar el dato en el mensaje de error, en singular y en minúscula:
    /// «turno», «ID de marcador», «sueldo básico». El mensaje lo lee un usuario de planta.
    /// </param>
    public static void GarantizarLineaDeTiempoSana<T>(IEnumerable<T> vigencias, string queSeAsigna)
        where T : IVigenciaPorFecha
    {
        ArgumentNullException.ThrowIfNull(vigencias);

        // Se ordena por inicio: así basta comparar cada una con la anterior para detectar
        // cualquier solapamiento, en vez de cruzar todas contra todas.
        var ordenadas = vigencias
            .OrderBy(v => v.FechaInicio)
            .ThenBy(v => v.FechaFin ?? DateOnly.MaxValue)
            .ToList();

        foreach (var vigencia in ordenadas)
        {
            if (vigencia.FechaFin is { } fin && fin < vigencia.FechaInicio)
            {
                throw new ReglaNegocioException(
                    $"El {queSeAsigna} tendría una vigencia imposible: termina el " +
                    $"{fin:dd/MM/yyyy}, antes de empezar el {vigencia.FechaInicio:dd/MM/yyyy}.");
            }
        }

        for (var i = 1; i < ordenadas.Count; i++)
        {
            var previa = ordenadas[i - 1];
            var actual = ordenadas[i];

            // Una vigencia abierta se extiende hasta el infinito: cualquier cosa que empiece
            // después de ella la solapa.
            if (previa.FechaFin is not { } finPrevia)
            {
                throw new ReglaNegocioException(
                    $"El trabajador tendría dos asignaciones de {queSeAsigna} a la vez: una " +
                    $"abierta desde el {previa.FechaInicio:dd/MM/yyyy} y otra desde el " +
                    $"{actual.FechaInicio:dd/MM/yyyy}. Cierre la primera antes de abrir la " +
                    "siguiente.");
            }

            if (finPrevia >= actual.FechaInicio)
            {
                throw new ReglaNegocioException(
                    $"Dos asignaciones de {queSeAsigna} se solapan: una va del " +
                    $"{previa.FechaInicio:dd/MM/yyyy} al {finPrevia:dd/MM/yyyy} y la siguiente " +
                    $"empieza el {actual.FechaInicio:dd/MM/yyyy}. Una fecha no puede tener dos " +
                    $"{queSeAsigna} distintos.");
            }
        }
    }
}
