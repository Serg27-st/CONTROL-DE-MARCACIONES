using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Organizacion;

namespace ControlMarcacion.Domain.Personal;

/// <summary>
/// Área interna en la que estuvo un trabajador durante un rango de fechas.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Trabajador.AreaId"/> guarda el área <b>vigente hoy</b> —lo que filtra el
/// listado de Personal y lo que resuelve el respaldo de CENTRO— y esta entidad guarda la que
/// tuvo <b>en cada fecha</b>, que es la que deben mostrar los reportes de un rango pasado.
/// </para>
/// <para>
/// Sin ella, mover a alguien de área reescribía el pasado: un resumen de julio emitido en
/// setiembre lo mostraba en el área que tiene ahora, y la columna <c>AREA</c> del tareo final
/// —que va a planilla— cambiaba sola en un mes ya pagado. Es el mismo error que el
/// invariante 5 previene para el ID de marcador, y el que
/// <c>ResolverMarcadoresDelRangoAsync</c> ya había corregido para el aparato.
/// </para>
/// <para>
/// ⚠️ Hoy el área <b>no se captura en el formulario de Personal</b> —Centro y Centro de costo
/// cumplen ese rol— y entra sobre todo por la carga masiva del padrón. Por eso lo normal es
/// que cada trabajador tenga una sola fila: la vigencia importa el día que se vuelve a subir
/// el padrón con «actualizar» y alguien cambió de área.
/// </para>
/// </remarks>
public class AsignacionArea : EntidadBase, IVigenciaPorFecha
{
    public long TrabajadorId { get; set; }

    public Trabajador? Trabajador { get; set; }

    public long AreaId { get; set; }

    public Area? Area { get; set; }

    public DateOnly FechaInicio { get; set; }

    /// <summary>Nulo mientras la asignación siga vigente.</summary>
    public DateOnly? FechaFin { get; set; }

    public bool EstaVigenteEn(DateOnly fecha) =>
        fecha >= FechaInicio && (FechaFin is null || fecha <= FechaFin.Value);
}
