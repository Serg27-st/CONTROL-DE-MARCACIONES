using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Personal;

/// <summary>
/// Un vínculo laboral del trabajador con la empresa, <b>con rango de fechas</b>:
/// desde que ingresa hasta que cesa.
/// </summary>
/// <remarks>
/// <see cref="Trabajador.FechaIngreso"/> y <see cref="Trabajador.FechaCese"/> siguen
/// existiendo como campos planos porque el motor de cálculo los consulta para cada día
/// del periodo y no puede pagar una búsqueda en una colección por jornada. Esta entidad
/// es la <b>historia</b> de esos dos campos: una fila por cada vez que la persona entró
/// y salió de la empresa.
///
/// Sin ella, un reingreso sobrescribiría la fecha de ingreso anterior y el tareo de un
/// periodo ya cerrado pasaría a calcularse contra un vínculo que en esa fecha no existía.
/// Es el mismo motivo por el que el ID de marcador, el turno y el sueldo tampoco son
/// campos planos: ver <see cref="AsignacionMarcador"/>.
/// </remarks>
public class PeriodoLaboral : EntidadBase
{
    public long TrabajadorId { get; set; }

    public Trabajador? Trabajador { get; set; }

    /// <summary>Empresa con la que se tuvo el vínculo en este periodo.</summary>
    /// <remarks>
    /// Se guarda aquí y no solo en <see cref="Trabajador.EmpresaId"/> porque una persona
    /// puede reingresar por la otra razón social: Consulting Advisers y CA Outsourcing
    /// son personas jurídicas distintas, y el periodo trabajado en una no puede quedar
    /// atribuido a la otra cuando alguien edite la ficha.
    /// </remarks>
    public long EmpresaId { get; set; }

    public DateOnly FechaIngreso { get; set; }

    /// <summary>Nulo mientras el vínculo siga vigente.</summary>
    public DateOnly? FechaCese { get; set; }

    /// <summary>Obligatorio al cerrar el periodo. Nulo mientras el vínculo siga vigente.</summary>
    public MotivoCese? MotivoCese { get; set; }

    /// <summary>Detalle libre del cese, opcional.</summary>
    public string? ObservacionCese { get; set; }

    public bool EstaVigenteEn(DateOnly fecha) =>
        fecha >= FechaIngreso && (FechaCese is null || fecha <= FechaCese.Value);

    public bool EstaAbierto => FechaCese is null;
}
