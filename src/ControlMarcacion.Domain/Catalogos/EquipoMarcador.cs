using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Organizacion;

namespace ControlMarcacion.Domain.Catalogos;

/// <summary>
/// El aparato ZKTeco físico instalado en una planta.
/// </summary>
/// <remarks>
/// Antes no existía: <see cref="Marcador"/> era a la vez el identificador de una persona
/// y el lugar donde estaba el equipo, y por eso el código tenía que ser único en todo el
/// sistema. Eso funciona mientras haya un solo aparato por planta, y deja de funcionar en
/// cuanto hay dos: en Planta Detergentes conviven un equipo para apellidos A–L y otro para
/// M–Z, ambos con usuarios numerados del 1 al 100. Con el código como única identidad, el
/// usuario 14 de un equipo y el 14 del otro son la misma persona para el sistema, y sus
/// marcaciones se funden en una sola jornada — con el agravante de que la fusión no se
/// nota: la fila resultante parece correcta.
///
/// Al modelar el aparato, la identidad de una persona en el marcador pasa a ser el par
/// <b>(equipo, código)</b>. El código vuelve a ser único solo dentro de su equipo, que es
/// como lo numera el ZKTeco, y la planta se declara una vez aquí en vez de repetirse en
/// cada uno de los cien identificadores del aparato.
///
/// <b>La serie es la que imprime el propio equipo</b> y sirve para reconocer de qué aparato
/// salió un archivo: el respaldo binario la trae en su cabecera y el TXT la trae en el
/// nombre (<c>SERIE_attlog.txt</c>). Es opcional porque un equipo se puede dar de alta
/// antes de haber visto un archivo suyo.
/// </remarks>
public class EquipoMarcador : EntidadBase
{
    /// <summary>Código corto con el que se elige el equipo en pantalla.</summary>
    /// <remarks>Único en todo el sistema: es lo que distingue un aparato de otro.</remarks>
    public string Codigo { get; set; } = null!;

    /// <summary>Nombre legible, por ejemplo "Puerta principal A–L".</summary>
    public string Nombre { get; set; } = null!;

    /// <summary>
    /// Número de serie que reporta el equipo, si se conoce.
    /// </summary>
    /// <remarks>
    /// <b>Texto</b> y no número: las series del ZKTeco mezclan letras y dígitos
    /// (<c>VGU6251600671</c>). Cuando está declarada, la carga puede comprobar que el
    /// archivo que se está subiendo salió de verdad del equipo elegido.
    /// </remarks>
    public string? Serie { get; set; }

    /// <summary>Planta donde está instalado el aparato.</summary>
    public long PlantaId { get; set; }

    public Planta? Planta { get; set; }

    /// <summary>Ubicación o referencia libre, opcional.</summary>
    public string? Descripcion { get; set; }
}
