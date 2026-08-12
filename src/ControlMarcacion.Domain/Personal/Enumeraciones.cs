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

/// <summary>
/// Causal por la que termina el vínculo laboral.
/// </summary>
/// <remarks>
/// Es obligatoria para dar de baja a un trabajador: una fecha de cese sin causal deja
/// un vínculo cerrado que nadie puede explicar después, y el cese es justamente el
/// hecho que habilita reasignar su ID de marcador y cambiarlo de razón social.
///
/// La lista la fijó GCA. Es un enum y no un catálogo en base porque son causales
/// laborales, no datos de configuración: agregar una es una decisión de negocio que
/// debe pasar por código y quedar en el historial del repositorio.
/// </remarks>
public enum MotivoCese
{
    RenunciaVoluntaria = 1,

    TerminoContrato = 2,

    NoSuperoPeriodoPrueba = 3,

    AbandonoLaboral = 4,

    Despido = 5,

    Destitucion = 6
}
