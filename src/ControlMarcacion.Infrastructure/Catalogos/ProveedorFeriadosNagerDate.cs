using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Infrastructure.Catalogos;

/// <summary>
/// Feriados nacionales de Perú desde la API pública de Nager.Date
/// (<c>https://date.nager.at</c>): gratis, sin key, cubre Perú.
/// </summary>
/// <remarks>
/// No existe todavía una API oficial confirmada de gob.pe para esto; si GCA identifica una
/// fuente oficial, esta es la única clase que habría que cambiar.
///
/// La planta puede no tener salida a internet (restricción explícita del proyecto): toda
/// falla de red, tiempo de espera o respuesta inválida se traduce a
/// <see cref="ReglaNegocioException"/> con un mensaje que le dice al usuario qué hacer, en
/// vez de dejar escapar la excepción técnica de <see cref="HttpClient"/>.
/// </remarks>
internal sealed class ProveedorFeriadosNagerDate(HttpClient httpClient) : IProveedorFeriadosExternos
{
    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<FeriadoExternoDto>> ObtenerAsync(
        int anio, CancellationToken cancelacion = default)
    {
        List<FeriadoNagerDto>? datos;

        try
        {
            datos = await httpClient
                .GetFromJsonAsync<List<FeriadoNagerDto>>(
                    $"api/v3/PublicHolidays/{anio}/PE", OpcionesJson, cancelacion)
                .ConfigureAwait(false);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Si quien canceló fue el propio llamador (se cerró la pantalla, se abortó la
            // petición HTTP entrante), se deja propagar tal cual: no es una falla de
            // conectividad con el proveedor, así que no corresponde este mensaje.
            if (excepcion is TaskCanceledException && cancelacion.IsCancellationRequested)
            {
                throw;
            }

            throw new ReglaNegocioException(
                "No se pudo conectar con el servicio de feriados. Intente más tarde o cárguelos desde una " +
                "red con internet.");
        }

        if (datos is null || datos.Count == 0)
        {
            return [];
        }

        return
        [
            .. datos.Select(d => new FeriadoExternoDto(
                DateOnly.ParseExact(d.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                d.LocalName))
        ];
    }

    /// <summary>Solo los dos campos que hacen falta; el resto de la respuesta se descarta.</summary>
    private sealed record FeriadoNagerDto(
        [property: JsonPropertyName("date")] string Date,
        [property: JsonPropertyName("localName")] string LocalName);
}
