using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Organizacion;

namespace ControlMarcacion.Domain.Personal;

/// <summary>
/// Planta que correspondió a un vínculo laboral durante un rango de fechas.
/// </summary>
/// <remarks>
/// La relación pertenece estructuralmente a <see cref="PeriodoLaboral"/>. De este modo,
/// un traslado interno conserva el vínculo y su historia, mientras un reingreso abre un
/// periodo nuevo con su propia línea temporal de planta.
/// </remarks>
public class AsignacionPlanta : EntidadBase, IVigenciaPorFecha
{
    public long PeriodoLaboralId { get; set; }

    public PeriodoLaboral? PeriodoLaboral { get; set; }

    public long PlantaId { get; set; }

    public Planta? Planta { get; set; }

    public DateOnly FechaInicio { get; set; }

    /// <summary>Nulo mientras la asignación siga vigente dentro de un periodo abierto.</summary>
    public DateOnly? FechaFin { get; set; }

    public bool EstaVigenteEn(DateOnly fecha) =>
        RangoTemporal.Contiene(FechaInicio, FechaFin, fecha);
}
