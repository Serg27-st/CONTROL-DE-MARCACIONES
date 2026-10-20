using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Application.Catalogos;

/// <summary>
/// Mantenimiento del catálogo de equipos marcadores: los aparatos ZKTeco físicos.
/// </summary>
/// <remarks>
/// Es el catálogo <b>raíz</b> de la ingesta. Sin un equipo dado de alta no se pueden
/// registrar marcadores —el código pertenece a un aparato— ni cargar archivos, porque la
/// pantalla de carga exige declarar de qué equipo salieron las marcaciones.
///
/// Va aparte del mantenimiento genérico de catálogos por lo mismo que los marcadores: no
/// es código más nombre. Tiene planta, serie, y reglas de baja que dependen de lo que
/// cuelgue de él.
/// </remarks>
public interface IServicioEquipos
{
    Task<IReadOnlyList<EquipoListaDto>> ListarAsync(
        FiltroEquipos filtro,
        CancellationToken cancelacion = default);

    Task<EquipoListaDto?> ObtenerAsync(long id, CancellationToken cancelacion = default);

    Task<long> GuardarAsync(
        GuardarEquipoRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    Task CambiarEstadoAsync(
        long id,
        bool activo,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Registra la serie de un equipo que todavía no tiene una, tomada del primer archivo
    /// suyo que se carga.
    /// </summary>
    /// <remarks>
    /// Existe porque nadie puede leer la serie en el aparato: no hay acceso físico a los
    /// marcadores, y la serie solo aparece en los archivos que se descargan. Es
    /// deliberadamente estrecha: no reemplaza una serie ya registrada —cambiarla es
    /// decisión de quien edita el equipo— y rechaza una que ya tenga otro equipo.
    /// </remarks>
    Task RegistrarSerieAsync(
        long id,
        string serie,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>Plantas disponibles, con su empresa, para el desplegable del formulario.</summary>
    Task<IReadOnlyList<OpcionDto>> PlantasAsync(CancellationToken cancelacion = default);
}
