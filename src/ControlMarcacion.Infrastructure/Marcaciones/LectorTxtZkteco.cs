using System.Globalization;
using System.Text;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Marcaciones.Dto;
using Microsoft.Extensions.Options;

namespace ControlMarcacion.Infrastructure.Marcaciones;

/// <summary>
/// Lee el archivo TXT que se extrae por USB del marcador ZKTeco G3.
/// </summary>
/// <remarks>
/// Formato confirmado contra un archivo real de producción
/// (<c>AEH2200760069_attlog.txt</c>, 2508 marcaciones de julio 2026):
/// <list type="bullet">
///   <item>Delimitado por tabuladores, sin encabezado, fin de línea CRLF.</item>
///   <item>Columnas: id · fecha_hora · modo_verificación · tipo_marcación · workcode · reservado.</item>
///   <item>El id viene rellenado con espacios a la izquierda y <b>hay ids con ceros
///   significativos</b> ("0014" convive con "14"): se trata siempre como texto.</item>
///   <item>Fecha en <c>yyyy-MM-dd HH:mm:ss</c>.</item>
/// </list>
///
/// <b>La columna de tipo de marcación viene siempre en 0.</b> El equipo no distingue
/// entrada de salida. Se guarda tal cual y el emparejamiento lo resuelve el motor contra
/// la ventana del turno; este lector no intenta adivinarlo.
///
/// La clase es pura: no toca la base de datos, para poder probarla sin infraestructura.
/// </remarks>
public sealed class LectorTxtZkteco(IOptions<OpcionesMarcador> opciones) : IFuenteDeMarcaciones
{
    private const int ColumnaId = 0;
    private const int ColumnaFechaHora = 1;
    private const int ColumnaModoVerificacion = 2;
    private const int ColumnaTipoMarcacion = 3;
    private const int ColumnasMinimas = 2;

    private readonly OpcionesMarcador _opciones = opciones.Value;

    public ResultadoLectura Leer(Stream contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        var marcaciones = new List<MarcacionLeida>();
        var rechazadas = new List<LineaRechazada>();

        var separador = string.IsNullOrEmpty(_opciones.Separador) ? "\t" : _opciones.Separador;
        var formatos = _opciones.FormatosFecha.ToArray();
        var limiteAntiguedad = DateTime.Now.AddDays(-_opciones.DiasAntiguedadMaxima);

        using var lector = new StreamReader(contenido, ResolverCodificacion(), leaveOpen: true);

        var numeroLinea = 0;

        while (lector.ReadLine() is { } linea)
        {
            numeroLinea++;

            // Las líneas en blanco no son un error: el archivo real termina con una.
            if (string.IsNullOrWhiteSpace(linea))
            {
                continue;
            }

            var columnas = linea.Split(separador);

            if (columnas.Length < ColumnasMinimas)
            {
                rechazadas.Add(new LineaRechazada(
                    numeroLinea, linea,
                    $"Se esperaban al menos {ColumnasMinimas} columnas separadas por tabulador y se encontraron {columnas.Length}."));
                continue;
            }

            // Recortar es obligatorio: el id viene con espacios a la izquierda y la última
            // columna a veces trae un espacio al final.
            var idMarcador = columnas[ColumnaId].Trim();

            if (string.IsNullOrEmpty(idMarcador))
            {
                rechazadas.Add(new LineaRechazada(
                    numeroLinea, linea, "El identificador del marcador está vacío."));
                continue;
            }

            var textoFecha = columnas[ColumnaFechaHora].Trim();

            if (!DateTime.TryParseExact(
                    textoFecha, formatos, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var fechaHora))
            {
                rechazadas.Add(new LineaRechazada(
                    numeroLinea, linea,
                    $"No se pudo interpretar la fecha y hora '{textoFecha}'. Formatos aceptados: {string.Join(", ", formatos)}."));
                continue;
            }

            // Un equipo con la fecha mal configurada reportaría marcaciones de hace años.
            // Cargarlas silenciosamente contaminaría periodos ya cerrados.
            if (fechaHora < limiteAntiguedad)
            {
                rechazadas.Add(new LineaRechazada(
                    numeroLinea, linea,
                    $"La fecha {fechaHora:dd/MM/yyyy} supera la antigüedad máxima de {_opciones.DiasAntiguedadMaxima} días. " +
                    "Verifique la fecha configurada en el marcador."));
                continue;
            }

            marcaciones.Add(new MarcacionLeida(
                idMarcador,
                fechaHora,
                Columna(columnas, ColumnaModoVerificacion),
                Columna(columnas, ColumnaTipoMarcacion),
                numeroLinea,
                linea.Trim()));
        }

        return new ResultadoLectura(marcaciones, rechazadas);
    }

    private static string? Columna(string[] columnas, int indice) =>
        indice < columnas.Length && !string.IsNullOrWhiteSpace(columnas[indice])
            ? columnas[indice].Trim()
            : null;

    /// <summary>
    /// Resuelve la codificación configurada, cayendo a UTF-8 si la página de códigos
    /// no está registrada.
    /// </summary>
    private Encoding ResolverCodificacion()
    {
        try
        {
            return Encoding.GetEncoding(_opciones.CodigoPagina);
        }
        catch (ArgumentException)
        {
            // Ocurre si nadie registró CodePagesEncodingProvider. El archivo real es ASCII,
            // así que UTF-8 lo lee sin pérdida y es preferible a abortar la carga entera.
            return Encoding.UTF8;
        }
    }
}
