namespace ControlMarcacion.Domain.Marcaciones;

/// <summary>
/// Vigencia de un ID de marcador para un trabajador: en el aparato <see cref="EquipoId"/>,
/// el número <see cref="IdMarcador"/> fue de <see cref="TrabajadorId"/> entre
/// <see cref="Desde"/> y <see cref="Hasta"/> (abierta si es nula).
/// </summary>
public sealed record VigenciaIdMarcador(
    long TrabajadorId,
    long EquipoId,
    string IdMarcador,
    DateOnly Desde,
    DateOnly? Hasta);

/// <summary>
/// Decide de quién es cada marcación cruda: de quien tenía ese número en ese aparato
/// <b>en la fecha de la marcación</b>.
/// </summary>
/// <remarks>
/// <para>
/// Es la única regla de atribución del sistema. La usan el motor de cálculo, la ingesta
/// (para avisar qué IDs llegaron sin dueño) y la lista de marcaciones sin asignar, y
/// tienen que responder lo mismo: si la ingesta dijera «sin dueño» y el motor encontrara
/// uno, la pantalla de carga y el tareo se contradirían.
/// </para>
/// <para>
/// La atribución se hace <b>al calcular</b> y no al cargar el archivo. La marcación cruda
/// guarda solo lo que dice el aparato —equipo, número y hora—, que es inmutable; quién
/// era ese número es un dato del padrón, y el padrón se corrige. Resolverlo al cargar
/// congelaba el error: un ID asignado después, o asignado mal, dejaba las marcaciones
/// descartadas o a nombre de otra persona, y la única salida era volver a subir archivos.
/// Ahora basta con corregir la asignación y recalcular.
/// </para>
/// <para>
/// ⚠️ No se resuelve por la asignación vigente HOY: los números se reasignan cuando alguien
/// cesa, y hacerlo le atribuiría al reemplazo las marcaciones históricas de quien se fue.
/// </para>
/// </remarks>
public sealed class AtribucionMarcaciones
{
    private readonly Dictionary<(long EquipoId, string IdMarcador), List<VigenciaIdMarcador>> _porNumero;

    public AtribucionMarcaciones(IEnumerable<VigenciaIdMarcador> vigencias)
    {
        ArgumentNullException.ThrowIfNull(vigencias);

        _porNumero = vigencias
            .GroupBy(v => (v.EquipoId, v.IdMarcador))
            .ToDictionary(g => g.Key, g => g.OrderBy(v => v.Desde).ToList());
    }

    /// <summary>
    /// Trabajador que tenía el número <paramref name="idMarcador"/> del aparato
    /// <paramref name="equipoId"/> en la fecha de <paramref name="fechaHora"/>, o nulo si
    /// nadie lo tenía.
    /// </summary>
    /// <remarks>
    /// El número se compara <b>tal cual</b>, sin quitar ceros: <c>0014</c> y <c>14</c> son
    /// usuarios distintos del aparato.
    /// </remarks>
    public long? Resolver(long equipoId, string idMarcador, DateTime fechaHora)
    {
        if (!_porNumero.TryGetValue((equipoId, idMarcador), out var vigencias))
        {
            return null;
        }

        var fecha = DateOnly.FromDateTime(fechaHora);

        foreach (var vigencia in vigencias)
        {
            if (vigencia.Desde <= fecha && (vigencia.Hasta is null || vigencia.Hasta >= fecha))
            {
                return vigencia.TrabajadorId;
            }
        }

        return null;
    }
}
