using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Marcaciones.Dto;

namespace ControlMarcacion.Infrastructure.Marcaciones;

/// <summary>
/// Reconoce qué archivo del marcador le han dado y se lo pasa al lector que corresponde.
/// </summary>
/// <remarks>
/// El equipo exporta dos archivos distintos que se llaman igual de `.dat`, y quien está
/// frente al marcador no tiene por qué acordarse de cuál eligió en el menú:
/// <list type="bullet">
///   <item><b>Texto tabulado</b> (<c>SERIE_attlog.dat</c> o <c>.txt</c>) →
///   <see cref="LectorTxtZkteco"/>.</item>
///   <item><b>Respaldo binario</b> (<c>BKtransaction.dat</c>) →
///   <see cref="LectorRespaldoZkteco"/>.</item>
/// </list>
///
/// <b>Decide por el contenido, nunca por la extensión ni por el nombre.</b> Los dos formatos
/// llegan como `.dat`, y un archivo renombrado a mano —cosa que pasa— no debe cambiar cómo
/// se interpreta.
///
/// Esta clase es el único <see cref="IFuenteDeMarcaciones"/> registrado: el servicio de
/// importación sigue sin saber de dónde salen las marcaciones, que es justamente lo que
/// permitirá enchufar mañana la conexión directa al equipo sin tocar nada más.
/// </remarks>
public sealed class LectorMarcadorAutomatico(
    LectorTxtZkteco lectorTexto,
    LectorRespaldoZkteco lectorRespaldo) : IFuenteDeMarcaciones
{
    public ResultadoLectura Leer(Stream contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        return DetectarRespaldo(contenido)
            ? lectorRespaldo.Leer(contenido)
            : lectorTexto.Leer(contenido);
    }

    /// <summary>
    /// Indica si el contenido es un respaldo binario.
    /// </summary>
    /// <remarks>
    /// Un flujo que no se puede rebobinar no se puede examinar sin consumirlo, así que se
    /// asume texto: es el formato normal, y el lector de texto informa por su cuenta si la
    /// suposición era mala.
    /// </remarks>
    private static bool DetectarRespaldo(Stream contenido) =>
        contenido.CanSeek && DetectorFormatoMarcador.Detectar(contenido) is not null;
}
