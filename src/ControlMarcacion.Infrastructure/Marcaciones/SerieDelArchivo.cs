using System.Text.RegularExpressions;
using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Infrastructure.Marcaciones;

/// <summary>
/// Serie del aparato que generó un archivo del marcador, sacada del propio archivo.
/// </summary>
/// <remarks>
/// Es el único lugar de donde puede salir: no hay acceso físico a los marcadores, así que
/// nadie puede leerla en el menú del equipo ni en su etiqueta. El respaldo binario la trae
/// en la cabecera; el attlog no trae ni una letra en su contenido, pero el equipo lo nombra
/// <c>SERIE_attlog.dat</c> al descargarlo.
/// </remarks>
internal static partial class SerieDelArchivo
{
    /// <summary>
    /// Serie del archivo: primero la del contenido, que no se puede renombrar; si no hay,
    /// la del nombre.
    /// </summary>
    /// <returns>
    /// La serie en mayúsculas, o nula si el archivo no trae ninguna, y si salió del
    /// contenido.
    /// </returns>
    /// <exception cref="ReglaNegocioException">
    /// El contenido y el nombre traen series distintas: el archivo se renombró con la serie
    /// de otro aparato, y no hay forma de saber cuál de las dos dice la verdad.
    /// </exception>
    public static (string? Serie, bool DelContenido) Resolver(string? delContenido, string? nombreArchivo)
    {
        var contenido = string.IsNullOrWhiteSpace(delContenido) ? null : delContenido.Trim().ToUpperInvariant();
        var nombre = DesdeNombre(nombreArchivo);

        if (contenido is not null && nombre is not null && contenido != nombre)
        {
            throw new ReglaNegocioException(
                $"El archivo trae dos series distintas: '{contenido}' en su contenido y '{nombre}' en " +
                "su nombre. Se renombró con la serie de otro aparato; verifique de cuál salió antes " +
                "de cargarlo.");
        }

        return contenido is not null ? (contenido, true) : (nombre, false);
    }

    /// <summary>
    /// Serie del nombre de un attlog (<c>AEWD193260084_attlog.dat</c>), o nula si el nombre
    /// no tiene esa forma.
    /// </summary>
    /// <remarks>
    /// El patrón se ancla al inicio e ignora lo que venga después de <c>_attlog</c>, porque
    /// en el share real hay sufijos agregados a mano (<c>_attlog PAT.dat</c>,
    /// <c>_attlog_prueba.txt</c>) y la copia de Windows agrega <c> (1)</c>. Un nombre que no
    /// empieza así —<c>03-03-2025.txt</c>, <c>NOV-POR TERMINAR.txt</c>— no trae serie, y el
    /// archivo se acepta sin verificar en vez de rechazarse: tres de los veintidós archivos
    /// reales se renombraron así.
    /// </remarks>
    public static string? DesdeNombre(string? nombreArchivo)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo))
        {
            return null;
        }

        // Algunos navegadores mandan la ruta completa del equipo del usuario.
        var nombre = nombreArchivo[(nombreArchivo.LastIndexOfAny(['/', '\\']) + 1)..];

        var coincidencia = PatronAttlog().Match(nombre);

        return coincidencia.Success ? coincidencia.Groups[1].Value.ToUpperInvariant() : null;
    }

    [GeneratedRegex("^([A-Za-z0-9]{6,20})_attlog", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PatronAttlog();
}
