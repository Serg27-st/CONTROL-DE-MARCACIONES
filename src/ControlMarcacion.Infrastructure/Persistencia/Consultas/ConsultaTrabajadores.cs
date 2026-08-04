using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Consultas;

/// <summary>
/// Consultas de lectura del padrón, proyectadas directamente a DTO.
/// </summary>
internal sealed class ConsultaTrabajadores(MarcacionDbContext contexto) : IConsultaTrabajadores
{
    public async Task<ResultadoPaginado<TrabajadorListaDto>> ListarAsync(
        FiltroTrabajadores filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = contexto.Trabajadores.AsNoTracking();

        if (filtro.EmpresaId is { } empresaId)
        {
            consulta = consulta.Where(t => t.EmpresaId == empresaId);
        }

        if (filtro.PlantaId is { } plantaId)
        {
            consulta = consulta.Where(t => t.PlantaId == plantaId);
        }

        if (filtro.AreaId is { } areaId)
        {
            consulta = consulta.Where(t => t.AreaId == areaId);
        }

        if (filtro.SeccionId is { } seccionId)
        {
            consulta = consulta.Where(t => t.SeccionId == seccionId);
        }

        if (filtro.TipoPlanilla is { } tipoPlanilla)
        {
            consulta = consulta.Where(t => t.TipoPlanilla == tipoPlanilla);
        }

        if (filtro.SoloActivos is { } soloActivos)
        {
            consulta = soloActivos
                ? consulta.Where(t => t.FechaCese == null)
                : consulta.Where(t => t.FechaCese != null);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            var termino = filtro.Busqueda.Trim();

            consulta = consulta.Where(t =>
                t.NumeroDocumento.Contains(termino)
                || t.ApellidoPaterno.Contains(termino)
                || t.ApellidoMaterno.Contains(termino)
                || t.Nombres.Contains(termino));
        }

        var total = await consulta.CountAsync(cancelacion).ConfigureAwait(false);

        var pagina = Math.Max(filtro.Pagina, 1);
        var tamanio = Math.Clamp(filtro.TamanioPagina, 10, 200);

        // Una sola consulta con todos los nombres resueltos: sin esto, mostrar planta,
        // área y sección de cada fila dispararía una consulta por trabajador.
        var elementos = await consulta
            .OrderBy(t => t.ApellidoPaterno)
            .ThenBy(t => t.ApellidoMaterno)
            .ThenBy(t => t.Nombres)
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .Select(t => new TrabajadorListaDto(
                t.Id,
                contexto.TiposDocumento.Where(d => d.Id == t.TipoDocumentoId).Select(d => d.Codigo).FirstOrDefault() ?? "-",
                t.NumeroDocumento,
                t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres,
                contexto.Empresas.Where(e => e.Id == t.EmpresaId).Select(e => e.RazonSocial).FirstOrDefault() ?? "-",
                contexto.Plantas.Where(p => p.Id == t.PlantaId).Select(p => p.Nombre).FirstOrDefault() ?? "-",
                contexto.Areas.Where(a => a.Id == t.AreaId).Select(a => a.Nombre).FirstOrDefault() ?? "-",
                contexto.Secciones.Where(s => s.Id == t.SeccionId).Select(s => s.Nombre).FirstOrDefault() ?? "-",
                contexto.Puestos.Where(p => p.Id == t.PuestoId).Select(p => p.Nombre).FirstOrDefault() ?? "-",
                contexto.AsignacionesTurno
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.Turno!.Codigo)
                    .FirstOrDefault(),
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.IdMarcador)
                    .FirstOrDefault(),
                t.FechaIngreso,
                t.FechaCese))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return new ResultadoPaginado<TrabajadorListaDto>(elementos, total, pagina, tamanio);
    }

    public Task<TrabajadorDetalleDto?> ObtenerDetalleAsync(
        long id,
        CancellationToken cancelacion = default) =>
        contexto.Trabajadores
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TrabajadorDetalleDto(
                t.Id,
                t.TipoDocumentoId,
                t.NumeroDocumento,
                t.ApellidoPaterno,
                t.ApellidoMaterno,
                t.Nombres,
                t.Sexo,
                t.Telefono,
                t.Direccion,
                t.CorreoElectronico,
                t.TieneHijos,
                t.EmpresaId,
                t.Unidad,
                t.TipoPlanilla,
                t.PlantaId,
                t.AreaId,
                t.SeccionId,
                t.FechaIngreso,
                t.FechaCese,
                t.PuestoId,
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.IdMarcador)
                    .FirstOrDefault(),
                contexto.AsignacionesTurno
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => (long?)a.TurnoId)
                    .FirstOrDefault()))
            .FirstOrDefaultAsync(cancelacion);
}
