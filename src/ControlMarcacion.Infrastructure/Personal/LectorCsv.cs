using System.Text;

namespace ControlMarcacion.Infrastructure.Personal;

/// <summary>
/// Lector de CSV mínimo: delimitador detectado, campos entrecomillados y nada más.
/// </summary>
/// <remarks>
/// No se usa una librería de terceros por lo de siempre en este repositorio: una
/// dependencia nueva hay que mantenerla, auditarla y arrastrarla a producción, y aquí se
/// necesitan doscientas líneas de las diez mil que trae cualquiera de ellas.
///
/// Cubre lo que produce Excel en español, que es de donde salen estos archivos:
/// delimitador punto y coma o coma, comillas dobles alrededor de los campos que llevan
/// el delimitador, y comillas duplicadas para escapar una comilla literal.
/// </remarks>
internal static class LectorCsv
{
    /// <summary>Delimitadores candidatos, en orden de preferencia ante un empate.</summary>
    private static readonly char[] Candidatos = [';', ',', '\t'];

    /// <summary>
    /// Lee todo el contenido como filas de campos ya recortados.
    /// </summary>
    public static IReadOnlyList<string[]> Leer(Stream contenido)
    {
        var texto = Decodificar(contenido);

        if (string.IsNullOrWhiteSpace(texto))
        {
            return [];
        }

        var lineas = PartirRegistros(texto
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n'));

        var delimitador = DetectarDelimitador(lineas.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)));

        // Una fila de puros delimitadores (";;;;;;") es lo que deja Excel al guardar como
        // CSV una hoja con filas borradas al final: no es un trabajador sin datos, y
        // listarla como rechazada solo añade ruido al resultado de la carga.
        return [.. lineas
            .Where(linea => !string.IsNullOrWhiteSpace(linea))
            .Select(linea => PartirLinea(linea, delimitador))
            .Where(campos => campos.Any(campo => campo.Length > 0))];
    }

    /// <summary>
    /// Separa el texto en registros por salto de línea, salvo los que caen dentro de
    /// comillas.
    /// </summary>
    /// <remarks>
    /// Excel escribe entre comillas una celda con Alt+Enter, y el salto viaja dentro del
    /// campo: el padrón de Planta 1 del 18/09/2026 traía así su encabezado
    /// (<c>"tipo⏎documento"</c>). Partir por cada salto de línea lo convertía en cinco
    /// registros sueltos y rechazaba el archivo entero por columnas faltantes.
    /// Dos comillas seguidas cambian el estado dos veces, así que la comilla escapada no
    /// necesita un caso aparte.
    /// </remarks>
    private static List<string> PartirRegistros(string texto)
    {
        var registros = new List<string>();
        var inicio = 0;
        var dentroDeComillas = false;

        for (var i = 0; i < texto.Length; i++)
        {
            if (texto[i] == '"')
            {
                dentroDeComillas = !dentroDeComillas;
            }
            else if (texto[i] == '\n' && !dentroDeComillas)
            {
                registros.Add(texto[inicio..i]);
                inicio = i + 1;
            }
        }

        registros.Add(texto[inicio..]);

        return registros;
    }

    /// <summary>
    /// Decodifica el archivo.
    /// </summary>
    /// <remarks>
    /// Se intenta UTF-8 estricto y, si el contenido no es UTF-8 válido, se cae a
    /// Windows-1252. Excel en español guarda CSV en 1252 salvo que se pida explícitamente
    /// "CSV UTF-8", así que las dos variantes llegan en la práctica. Leerlo todo como
    /// UTF-8 convertiría cada "ñ" y cada tilde de un archivo 1252 en un rombo negro, y
    /// esos apellidos entrarían mal escritos al padrón.
    /// </remarks>
    private static string Decodificar(Stream contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        using var memoria = new MemoryStream();

        if (contenido.CanSeek)
        {
            contenido.Position = 0;
        }

        contenido.CopyTo(memoria);

        var bytes = memoria.ToArray();

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes)
                .TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    /// <summary>
    /// Elige el delimitador contando cuál aparece más veces fuera de comillas en el
    /// encabezado. Fijarlo en punto y coma fallaría con cualquier archivo exportado desde
    /// una máquina en inglés.
    /// </summary>
    private static char DetectarDelimitador(string? encabezado)
    {
        if (string.IsNullOrWhiteSpace(encabezado))
        {
            return ';';
        }

        var mejor = ';';
        var maximo = 0;

        foreach (var candidato in Candidatos)
        {
            var cuenta = ContarFueraDeComillas(encabezado, candidato);

            if (cuenta > maximo)
            {
                maximo = cuenta;
                mejor = candidato;
            }
        }

        return mejor;
    }

    private static int ContarFueraDeComillas(string linea, char delimitador)
    {
        var cuenta = 0;
        var dentroDeComillas = false;

        foreach (var caracter in linea)
        {
            if (caracter == '"')
            {
                dentroDeComillas = !dentroDeComillas;
            }
            else if (caracter == delimitador && !dentroDeComillas)
            {
                cuenta++;
            }
        }

        return cuenta;
    }

    private static string[] PartirLinea(string linea, char delimitador)
    {
        var campos = new List<string>();
        var actual = new StringBuilder();
        var dentroDeComillas = false;

        for (var i = 0; i < linea.Length; i++)
        {
            var caracter = linea[i];

            if (caracter == '"')
            {
                // Dos comillas seguidas dentro de un campo entrecomillado son una comilla
                // literal, no el fin del campo.
                if (dentroDeComillas && i + 1 < linea.Length && linea[i + 1] == '"')
                {
                    actual.Append('"');
                    i++;
                    continue;
                }

                dentroDeComillas = !dentroDeComillas;
                continue;
            }

            if (caracter == delimitador && !dentroDeComillas)
            {
                campos.Add(actual.ToString().Trim());
                actual.Clear();
                continue;
            }

            actual.Append(caracter);
        }

        campos.Add(actual.ToString().Trim());

        return [.. campos];
    }
}
