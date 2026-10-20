namespace ControlMarcacion.Domain.Marcaciones;

/// <summary>Qué tan cargado está un día, mirado aparato por aparato.</summary>
public enum EstadoCobertura
{
    /// <summary>Todavía no ocurre: no puede faltar nada.</summary>
    Futuro,

    /// <summary>Ya pasó y no tiene ninguna marcación de ningún aparato.</summary>
    SinMarcaciones,

    /// <summary>
    /// Tiene marcaciones de unos aparatos y le faltan las de otro que se esperaba. Es el
    /// hueco que no se ve contando marcaciones: el día parece cargado.
    /// </summary>
    Incompleto,

    /// <summary>Tiene marcaciones de todos los aparatos que se esperaban ese día.</summary>
    Completo
}

/// <summary>
/// Un aparato del que se espera un archivo, y desde cuándo.
/// </summary>
/// <param name="Desde">
/// Primer día en que se esperan sus marcaciones: el de su primera marcación vigente, o el de
/// su alta en el sistema si todavía no se cargó nada suyo.
/// </param>
public sealed record AparatoEsperado(long EquipoId, bool Activo, DateOnly Desde)
{
    /// <summary>
    /// Desde cuándo se espera un aparato: su primera marcación vigente o, sin ninguna, su alta.
    /// </summary>
    /// <remarks>
    /// La primera marcación y no el alta, cuando la hay, porque el attlog acumula la memoria
    /// del equipo: su primer archivo trae todo lo que el aparato registró, y los días
    /// anteriores no son un archivo olvidado sino días en que el aparato no se usaba.
    /// Sin ninguna marcación, el alta: un aparato registrado del que nunca se cargó nada es
    /// exactamente el olvido que este cálculo existe para mostrar.
    /// </remarks>
    public static AparatoEsperado Para(long equipoId, bool activo, DateOnly? primeraMarcacion, DateOnly alta) =>
        new(equipoId, activo, primeraMarcacion ?? alta);

    /// <summary>
    /// Si en esa fecha tendría que haber marcaciones suyas. Un aparato dado de baja no se
    /// espera nunca.
    /// </summary>
    public bool EsperadoEn(DateOnly fecha) => Activo && fecha >= Desde;
}

/// <summary>
/// Decide si un día tiene cargado todo lo que debía, aparato por aparato.
/// </summary>
/// <remarks>
/// <para>
/// Contar marcaciones por día no alcanza con dos aparatos en la planta: si se cargó el
/// archivo del A-L y se olvidó el del M-Z, el día tiene marcaciones y parece cubierto, y la
/// mitad de la planta sale en falta al calcular. Por eso el día se evalúa contra los
/// aparatos que se esperaban ese día, no contra el total.
/// </para>
/// <para>
/// Es la única regla de cobertura del sistema: la usan el calendario de Marcaciones y la
/// tarjeta de la portada, y tienen que decir lo mismo.
/// </para>
/// </remarks>
public static class CoberturaDeCarga
{
    public static EstadoCobertura Evaluar(
        DateOnly fecha,
        DateOnly hoy,
        IEnumerable<AparatoEsperado> aparatos,
        IReadOnlyCollection<long> conMarcacion)
    {
        ArgumentNullException.ThrowIfNull(aparatos);
        ArgumentNullException.ThrowIfNull(conMarcacion);

        if (fecha > hoy)
        {
            return EstadoCobertura.Futuro;
        }

        if (conMarcacion.Count == 0)
        {
            return EstadoCobertura.SinMarcaciones;
        }

        return Faltantes(fecha, hoy, aparatos, conMarcacion).Any()
            ? EstadoCobertura.Incompleto
            : EstadoCobertura.Completo;
    }

    /// <summary>
    /// Aparatos que se esperaban ese día y no tienen ninguna marcación. Vacío para un día
    /// futuro.
    /// </summary>
    public static IEnumerable<long> Faltantes(
        DateOnly fecha,
        DateOnly hoy,
        IEnumerable<AparatoEsperado> aparatos,
        IReadOnlyCollection<long> conMarcacion)
    {
        ArgumentNullException.ThrowIfNull(aparatos);
        ArgumentNullException.ThrowIfNull(conMarcacion);

        return fecha > hoy
            ? []
            : aparatos
                .Where(a => a.EsperadoEn(fecha) && !conMarcacion.Contains(a.EquipoId))
                .Select(a => a.EquipoId);
    }
}
