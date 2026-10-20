using ControlMarcacion.Application.Personal.Dto;

namespace ControlMarcacion.Application.Personal;

/// <summary>
/// Alta masiva de trabajadores desde un archivo CSV.
/// </summary>
/// <remarks>
/// Existe porque el padrón inicial de Intradevco se entrega en planilla de cálculo:
/// dar de alta cientos de fichas a mano es horas de trabajo y una fuente garantizada de
/// erratas.
///
/// La carga <b>no reimplementa las reglas del alta individual</b>: resuelve los catálogos
/// por código, arma la misma solicitud que usa el formulario y llama al mismo servicio.
/// Si mañana cambia una validación, cambia en un solo sitio y las dos puertas de entrada
/// quedan iguales.
/// </remarks>
public interface ICargaMasivaTrabajadores
{
    /// <summary>
    /// Lee el archivo, valida fila por fila y registra las válidas.
    /// </summary>
    /// <param name="soloValidar">
    /// Recorre y reporta sin escribir nada. Permite revisar un padrón completo antes de
    /// comprometerlo, que es lo que uno quiere hacer con un archivo de 300 personas.
    /// </param>
    /// <param name="actualizarExistentes">
    /// Con un documento que ya está en el padrón, actualiza esa ficha en vez de rechazar
    /// la fila por duplicada. Es lo que permite volver a subir el mismo archivo corregido
    /// —con un dato que faltaba, o con los ceses que llegaron después— sin tener que
    /// borrar a nadie primero. Va apagado por omisión: una carga que pisa fichas
    /// existentes tiene que ser una decisión consciente, no el comportamiento normal.
    /// </param>
    /// <param name="registrarMarcadores">
    /// Da de alta en el catálogo de su aparato los ID de marcador que falten, en vez de
    /// rechazar la fila. Va encendido por omisión: sin acceso físico a los aparatos, el
    /// padrón es la única fuente de qué número lleva cada persona.
    /// </param>
    Task<ResultadoCargaMasiva> ProcesarAsync(
        Stream contenido,
        string nombreArchivo,
        string usuario,
        bool soloValidar,
        bool actualizarExistentes = false,
        bool registrarMarcadores = true,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Archivo CSV de ejemplo con el encabezado exacto que espera la carga y una fila
    /// de muestra. Es la documentación del formato que de verdad se lee, en vez de un
    /// manual aparte que se desactualiza.
    /// </summary>
    byte[] GenerarPlantillaCsv();
}
