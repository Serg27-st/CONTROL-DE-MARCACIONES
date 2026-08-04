namespace ControlMarcacion.Domain.Personal;

public enum Sexo
{
    Masculino = 1,
    Femenino = 2
}

/// <summary>
/// Tipo de planilla del trabajador.
/// </summary>
/// <remarks>
/// Determina el régimen de turnos: los empleados tienen un único turno fijo,
/// mientras que los obreros rotan entre turnos según calendario.
/// ⚠️ Confirmar con GCA si "tipo de planilla" es exactamente esta clasificación
/// o si además existe otra dimensión distinta en su nomenclatura.
/// </remarks>
public enum TipoPlanilla
{
    /// <summary>Obrero.</summary>
    Obrero = 1,

    /// <summary>Empleado.</summary>
    Empleado = 2
}
