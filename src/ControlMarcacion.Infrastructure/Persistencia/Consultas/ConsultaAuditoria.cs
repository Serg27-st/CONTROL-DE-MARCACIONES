using System.Text.Json;
using ControlMarcacion.Application.Auditoria;
using ControlMarcacion.Application.Auditoria.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Consultas;

/// <summary>
/// Lectura de la bitácora de auditoría.
/// </summary>
/// <remarks>
/// El detalle se guarda como JSON y se deserializa aquí, ya fuera de la base. Modelarlo
/// como una tabla hija de "un cambio por fila" habría multiplicado por diez el volumen
/// de la bitácora — el alta de un trabajador son treinta y pico campos — para permitir
/// una consulta por campo que nadie pidió.
/// </remarks>
internal sealed class ConsultaAuditoria(MarcacionDbContext contexto) : IConsultaAuditoria
{
    private static readonly IReadOnlyList<CambioAuditadoDto> SinCambios = [];

    public async Task<ResultadoPaginado<RegistroAuditoriaDto>> ListarAsync(
        FiltroAuditoria filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = contexto.RegistrosAuditoria.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Tabla))
        {
            var tabla = filtro.Tabla.Trim();
            consulta = consulta.Where(r => r.Tabla == tabla);
        }

        if (!string.IsNullOrWhiteSpace(filtro.ClaveRegistro))
        {
            var clave = filtro.ClaveRegistro.Trim();
            consulta = consulta.Where(r => r.ClaveRegistro == clave);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Usuario))
        {
            var usuario = filtro.Usuario.Trim();
            consulta = consulta.Where(r => r.Usuario.Contains(usuario));
        }

        if (filtro.Operacion is { } operacion)
        {
            consulta = consulta.Where(r => r.Operacion == operacion);
        }

        if (filtro.Desde is { } desde)
        {
            var limite = desde.ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(r => r.FechaHora >= limite);
        }

        if (filtro.Hasta is { } hasta)
        {
            // Hasta el final del día elegido: comparar contra las 00:00 dejaría fuera
            // todo lo ocurrido ese mismo día, que es justo lo que el usuario busca.
            var limite = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(r => r.FechaHora < limite);
        }

        var total = await consulta.CountAsync(cancelacion).ConfigureAwait(false);

        var pagina = Math.Max(filtro.Pagina, 1);
        var tamanio = Math.Clamp(filtro.TamanioPagina, 10, 200);

        var filas = await consulta
            .OrderByDescending(r => r.FechaHora)
            .ThenByDescending(r => r.Id)
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return new ResultadoPaginado<RegistroAuditoriaDto>(
            [.. filas.Select(Mapear)],
            total,
            pagina,
            tamanio);
    }

    public async Task<IReadOnlyList<RegistroAuditoriaDto>> DeRegistroAsync(
        string tabla,
        string claveRegistro,
        int maximo = 100,
        CancellationToken cancelacion = default)
    {
        var filas = await contexto.RegistrosAuditoria
            .AsNoTracking()
            .Where(r => r.Tabla == tabla && r.ClaveRegistro == claveRegistro)
            .OrderByDescending(r => r.FechaHora)
            .ThenByDescending(r => r.Id)
            .Take(Math.Clamp(maximo, 1, 500))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return [.. filas.Select(Mapear)];
    }

    public async Task<IReadOnlyList<string>> TablasAsync(CancellationToken cancelacion = default) =>
        await contexto.RegistrosAuditoria
            .AsNoTracking()
            .Select(r => r.Tabla)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    private static RegistroAuditoriaDto Mapear(RegistroAuditoria registro) =>
        new(registro.Id,
            registro.Tabla,
            registro.ClaveRegistro,
            registro.Operacion,
            registro.Usuario,
            registro.FechaHora,
            Deserializar(registro.Detalle));

    /// <summary>
    /// Un detalle ilegible no puede tumbar la pantalla de auditoría: se devuelve vacío.
    /// La bitácora es lo que se consulta cuando algo ya salió mal; que además reviente
    /// sería el peor momento posible.
    /// </summary>
    private static IReadOnlyList<CambioAuditadoDto> Deserializar(string detalle)
    {
        if (string.IsNullOrWhiteSpace(detalle))
        {
            return SinCambios;
        }

        try
        {
            return JsonSerializer.Deserialize<List<CambioAuditadoDto>>(detalle) ?? SinCambios;
        }
        catch (JsonException)
        {
            return SinCambios;
        }
    }
}
