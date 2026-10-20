namespace ControlMarcacion.Domain.Comun;

/// <summary>
/// Semántica temporal única para los rangos inclusivos del dominio.
/// </summary>
/// <remarks>
/// Un fin nulo representa una vigencia abierta. Centralizar estas comparaciones evita
/// que ingreso, cese y asignaciones organizativas discrepen justo en los días límite.
/// </remarks>
public static class RangoTemporal
{
    public static bool Contiene(DateOnly inicio, DateOnly? fin, DateOnly fecha) =>
        fecha >= inicio && (fin is null || fecha <= fin.Value);

    public static bool EsValido(DateOnly inicio, DateOnly? fin) =>
        fin is null || fin.Value >= inicio;

    public static bool SeSolapan(
        DateOnly inicioA,
        DateOnly? finA,
        DateOnly inicioB,
        DateOnly? finB) =>
        (finB is null || inicioA <= finB.Value)
        && (finA is null || inicioB <= finA.Value);
}
