using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Personal;

/// <summary>
/// Sueldo básico mensual asignado a un trabajador durante un rango de fechas.
/// </summary>
/// <remarks>
/// El sueldo básico <b>no es un campo plano</b> del trabajador: cambia con el tiempo
/// (aumentos, reclasificaciones) y algunos cálculos legales — por ejemplo el bono
/// nocturno — necesitan el sueldo que estaba vigente en la fecha exacta que se está
/// calculando, no el sueldo de hoy. Guardarlo como un campo simple reescribiría la
/// historia cada vez que cambia, igual que pasaría con el ID de marcador o el turno.
/// </remarks>
public class AsignacionSueldo : EntidadBase
{
    public long TrabajadorId { get; set; }

    public Trabajador? Trabajador { get; set; }

    /// <summary>Sueldo básico mensual, sin bonificaciones ni asignaciones adicionales.</summary>
    public decimal SueldoBasicoMensual { get; set; }

    public DateOnly FechaInicio { get; set; }

    /// <summary>Nulo mientras la asignación siga vigente.</summary>
    public DateOnly? FechaFin { get; set; }

    public bool EstaVigenteEn(DateOnly fecha) =>
        fecha >= FechaInicio && (FechaFin is null || fecha <= FechaFin.Value);
}
