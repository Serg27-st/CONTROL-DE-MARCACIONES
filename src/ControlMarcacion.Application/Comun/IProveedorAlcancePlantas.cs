namespace ControlMarcacion.Application.Comun;

/// <summary>
/// Qué plantas puede ver el usuario de la petición actual — el control de aislamiento entre
/// las dos razones sociales (Consulting Advisers y CA Outsourcing).
/// </summary>
/// <remarks>
/// Un <c>ADMINISTRADOR</c> solo ve los trabajadores de las plantas que gestiona
/// (<c>UsuarioPlanta</c>). COORDINADOR, GERENCIA, TRABAJADOR_SOCIAL y los procesos sin sesión
/// (siembra, arranque, herramientas de EF) no tienen restricción: ven todo.
///
/// El filtro se aplica SIEMPRE en la consulta de <c>Application</c>, nunca ocultando botones
/// ni enlaces: un rol que puede pedir el listado completo y recibir todo el padrón está roto
/// aunque la pantalla se vea correcta. Y como cada planta pertenece a una sola razón social,
/// filtrar por planta separa datos entre personas jurídicas distintas — no es un adorno.
///
/// La implementación real lee la identidad y el rol de la petición HTTP y vive en la capa Web;
/// Application no conoce <c>HttpContext</c>. El respaldo de Infrastructure devuelve «sin
/// restricción» para que la siembra y las herramientas de EF sigan funcionando.
/// </remarks>
public interface IProveedorAlcancePlantas
{
    /// <summary>
    /// Ids de las plantas visibles, o <c>null</c> cuando no hay restricción (ve todo).
    /// </summary>
    /// <remarks>
    /// Una colección <b>vacía</b> significa «no ve ninguna»: es el ADMINISTRADOR al que
    /// todavía no se le asignó ninguna planta. Es fail-closed a propósito —no ver nada es la
    /// lectura segura—; por eso el alta de un ADMINISTRADOR exige al menos una planta.
    /// </remarks>
    Task<IReadOnlyCollection<long>?> PlantasVisiblesAsync(CancellationToken cancelacion = default);
}

/// <summary>
/// Atajos sobre <see cref="IProveedorAlcancePlantas"/>.
/// </summary>
public static class AlcancePlantasExtensiones
{
    /// <summary>
    /// ¿El usuario actual puede ver o tocar a un trabajador de esta planta?
    /// </summary>
    /// <remarks>
    /// Es el control del acceso por id directo: filtrar el listado no basta —quien escribe la
    /// URL con el id de otro trabajador igual llegaría a su ficha—. Se consulta antes de
    /// devolver o modificar una ficha concreta; si es <c>false</c>, el servicio responde como
    /// si el trabajador no existiera (404), sin revelar que existe en otra planta.
    ///
    /// Va como extensión y no como método del contrato para que se derive de una sola fuente
    /// —<see cref="IProveedorAlcancePlantas.PlantasVisiblesAsync"/>— y para que un doble de
    /// prueba solo tenga que simular esa única consulta.
    /// </remarks>
    public static async Task<bool> PuedeVerPlantaAsync(
        this IProveedorAlcancePlantas alcance,
        long plantaId,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(alcance);

        var plantas = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        return plantas is null || plantas.Contains(plantaId);
    }
}
