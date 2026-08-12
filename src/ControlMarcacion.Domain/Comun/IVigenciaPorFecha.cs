namespace ControlMarcacion.Domain.Comun;

/// <summary>
/// Dato que rige durante un rango de fechas y se reemplaza cerrando el anterior.
/// </summary>
/// <remarks>
/// Lo implementan las tres asignaciones del trabajador —ID de marcador, turno y sueldo
/// básico—, que comparten exactamente la misma mecánica: nunca se editan, se cierra la
/// vigente y se abre una nueva. Existe para que la regla que las gobierna
/// (<see cref="ValidadorVigencias"/>) se escriba una sola vez: cuando estaba repetida por
/// entidad, el turno terminó con validaciones que el sueldo no tenía.
/// </remarks>
public interface IVigenciaPorFecha
{
    DateOnly FechaInicio { get; set; }

    /// <summary>Nulo mientras la vigencia siga abierta.</summary>
    DateOnly? FechaFin { get; set; }
}
