using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Consultas;

/// <summary>
/// Consultas de lectura del padrón, proyectadas directamente a DTO.
/// </summary>
internal sealed class ConsultaTrabajadores(
    MarcacionDbContext contexto,
    IProveedorAlcancePlantas alcance) : IConsultaTrabajadores
{
    public async Task<ResultadoPaginado<TrabajadorListaDto>> ListarAsync(
        FiltroTrabajadores filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = contexto.Trabajadores.AsNoTracking();

        // Aislamiento por planta: un ADMINISTRADOR solo ve el padrón de las plantas que
        // gestiona. Va antes que los filtros de pantalla porque es un límite del alcance, no
        // una preferencia del usuario; null = sin restricción (COORDINADOR, GERENCIA).
        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        if (plantasVisibles is not null)
        {
            consulta = consulta.Where(t => plantasVisibles.Contains(t.PlantaId));
        }

        if (filtro.EmpresaId is { } empresaId)
        {
            consulta = consulta.Where(t => t.EmpresaId == empresaId);
        }

        if (filtro.PlantaId is { } plantaId)
        {
            consulta = consulta.Where(t => t.PlantaId == plantaId);
        }

        if (filtro.CentroId is { } centroId)
        {
            consulta = consulta.Where(t =>
                contexto.CentrosCosto.Any(cc => cc.Id == t.CentroCostoId && cc.CentroId == centroId));
        }

        if (filtro.CentroCostoId is { } centroCostoId)
        {
            consulta = consulta.Where(t => t.CentroCostoId == centroCostoId);
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
                contexto.CentrosCosto.Where(cc => cc.Id == t.CentroCostoId).Select(cc => cc.Centro!.Codigo).FirstOrDefault(),
                contexto.CentrosCosto.Where(cc => cc.Id == t.CentroCostoId).Select(cc => cc.Codigo + " - " + cc.Descripcion).FirstOrDefault(),
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

    public async Task<TrabajadorDetalleDto?> ObtenerDetalleAsync(
        long id,
        CancellationToken cancelacion = default)
    {
        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        var consulta = contexto.Trabajadores
            .AsNoTracking()
            .Where(t => t.Id == id);

        // Aislamiento por id directo: si el trabajador es de otra planta, la consulta no
        // devuelve nada y el servicio responde 404, sin revelar que existe en otra planta.
        if (plantasVisibles is not null)
        {
            consulta = consulta.Where(t => plantasVisibles.Contains(t.PlantaId));
        }

        return await consulta
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
                t.CentroCostoId,
                // No se guarda en Trabajador: se resuelve aquí solo para que la
                // pantalla de edición sepa qué Centro precargar en la cascada.
                contexto.CentrosCosto
                    .Where(cc => cc.Id == t.CentroCostoId)
                    .Select(cc => (long?)cc.CentroId)
                    .FirstOrDefault(),
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.IdMarcador)
                    .FirstOrDefault(),
                contexto.AsignacionesTurno
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => (long?)a.TurnoId)
                    .FirstOrDefault(),
                t.MotivoCese,
                t.ObservacionCese))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<TrabajadorPanelDto?> ObtenerPanelAsync(
        long id,
        CancellationToken cancelacion = default)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        var consulta = contexto.Trabajadores
            .AsNoTracking()
            .Where(t => t.Id == id);

        if (plantasVisibles is not null)
        {
            consulta = consulta.Where(t => plantasVisibles.Contains(t.PlantaId));
        }

        return await consulta
            .Select(t => new TrabajadorPanelDto(
                t.Id,
                contexto.TiposDocumento.Where(d => d.Id == t.TipoDocumentoId).Select(d => d.Codigo + " - " + d.Nombre).FirstOrDefault() ?? "-",
                t.NumeroDocumento,
                t.ApellidoPaterno,
                t.ApellidoMaterno,
                t.Nombres,
                t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres,
                t.Sexo,
                t.Telefono,
                t.Direccion,
                t.CorreoElectronico,
                t.TieneHijos,
                contexto.Empresas.Where(e => e.Id == t.EmpresaId).Select(e => e.RazonSocial).FirstOrDefault() ?? "-",
                t.Unidad,
                t.TipoPlanilla,
                contexto.Plantas.Where(p => p.Id == t.PlantaId).Select(p => p.Nombre).FirstOrDefault() ?? "-",
                contexto.Puestos.Where(p => p.Id == t.PuestoId).Select(p => p.Nombre).FirstOrDefault() ?? "-",
                contexto.CentrosCosto
                    .Where(cc => cc.Id == t.CentroCostoId)
                    .Select(cc => cc.Codigo + " - " + cc.Descripcion)
                    .FirstOrDefault(),
                t.FechaIngreso,
                t.FechaCese,
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.IdMarcador)
                    .FirstOrDefault(),
                contexto.AsignacionesTurno
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.Turno!.Codigo + " - " + a.Turno!.Nombre)
                    .FirstOrDefault(),
                contexto.AsignacionesSueldo
                    .Where(a => a.TrabajadorId == t.Id
                        && a.FechaInicio <= hoy
                        && (a.FechaFin == null || a.FechaFin >= hoy))
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => (decimal?)a.SueldoBasicoMensual)
                    .FirstOrDefault(),
                t.MotivoCese,
                t.ObservacionCese))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<HistorialTrabajadorDto?> ObtenerHistorialAsync(
        long id,
        CancellationToken cancelacion = default)
    {
        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        var consultaCabecera = contexto.Trabajadores
            .AsNoTracking()
            .Where(t => t.Id == id);

        if (plantasVisibles is not null)
        {
            consultaCabecera = consultaCabecera.Where(t => plantasVisibles.Contains(t.PlantaId));
        }

        var cabecera = await consultaCabecera
            .Select(t => new
            {
                t.Id,
                Nombre = t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres,
                t.NumeroDocumento
            })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        // Fuera de alcance o inexistente: en ambos casos null. Como el resto del historial
        // (vínculos, sueldos, marcadores, turnos) solo se consulta si hay cabecera, un
        // trabajador de otra planta nunca llega a esas cuatro consultas.
        if (cabecera is null)
        {
            return null;
        }

        // Cuatro consultas y no una con Include: cada historial es una lista independiente
        // y traerlas juntas multiplicaría las filas de las cuatro entre sí.
        var vinculos = await contexto.PeriodosLaborales
            .AsNoTracking()
            .Where(p => p.TrabajadorId == id)
            .OrderByDescending(p => p.FechaIngreso)
            .Select(p => new PeriodoLaboralDto(
                p.Id,
                contexto.Empresas.Where(e => e.Id == p.EmpresaId).Select(e => e.RazonSocial).FirstOrDefault() ?? "-",
                p.FechaIngreso,
                p.FechaCese,
                p.MotivoCese,
                p.ObservacionCese,
                p.ModificadoPor ?? p.CreadoPor,
                p.ModificadoEn ?? p.CreadoEn))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var sueldos = await contexto.AsignacionesSueldo
            .AsNoTracking()
            .Where(a => a.TrabajadorId == id)
            .OrderByDescending(a => a.FechaInicio)
            .Select(a => new HistorialSueldoDto(
                a.Id,
                a.SueldoBasicoMensual,
                a.FechaInicio,
                a.FechaFin,
                a.ModificadoPor ?? a.CreadoPor,
                a.ModificadoEn ?? a.CreadoEn))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var marcadores = await contexto.AsignacionesMarcador
            .AsNoTracking()
            .Where(a => a.TrabajadorId == id)
            .OrderByDescending(a => a.FechaInicio)
            .Select(a => new HistorialMarcadorDto(
                a.Id,
                a.IdMarcador,
                // Se resuelve contra el catálogo, no contra la planta del trabajador: si
                // la persona cambió de planta, el marcador que usaba antes sigue siendo
                // el de la planta anterior.
                contexto.Marcadores
                    .Where(m => m.Codigo == a.IdMarcador)
                    .Select(m => m.Planta!.Nombre)
                    .FirstOrDefault(),
                a.FechaInicio,
                a.FechaFin,
                a.ModificadoPor ?? a.CreadoPor,
                a.ModificadoEn ?? a.CreadoEn))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var turnos = await contexto.AsignacionesTurno
            .AsNoTracking()
            .Where(a => a.TrabajadorId == id)
            .OrderByDescending(a => a.FechaInicio)
            .Select(a => new HistorialTurnoDto(
                a.Id,
                a.Turno!.Codigo + " - " + a.Turno!.Nombre,
                a.FechaInicio,
                a.FechaFin,
                a.ModificadoPor ?? a.CreadoPor,
                a.ModificadoEn ?? a.CreadoEn))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return new HistorialTrabajadorDto(
            cabecera.Id,
            cabecera.Nombre,
            cabecera.NumeroDocumento,
            vinculos,
            sueldos,
            marcadores,
            turnos);
    }

    public async Task<IReadOnlyList<TrabajadorBusquedaRapidaDto>> BuscarRapidoAsync(
        string? texto,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return [];
        }

        var termino = texto.Trim();
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        // "Activa" es la asignación de marcador vigente hoy: empezó en el pasado (o
        // hoy) y todavía no tiene fecha de fin, o su fecha de fin es futura.
        var consulta = contexto.Trabajadores
            .AsNoTracking()
            .Where(t =>
                t.NumeroDocumento.Contains(termino)
                || t.ApellidoPaterno.Contains(termino)
                || t.ApellidoMaterno.Contains(termino)
                || t.Nombres.Contains(termino)
                || contexto.AsignacionesMarcador.Any(a =>
                    a.TrabajadorId == t.Id
                    && a.IdMarcador.Contains(termino)
                    && a.FechaInicio <= hoy
                    && (a.FechaFin == null || a.FechaFin >= hoy)));

        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        if (plantasVisibles is not null)
        {
            consulta = consulta.Where(t => plantasVisibles.Contains(t.PlantaId));
        }

        return await consulta
            .OrderBy(t => t.ApellidoPaterno)
            .ThenBy(t => t.ApellidoMaterno)
            .ThenBy(t => t.Nombres)
            .Take(50)
            .Select(t => new TrabajadorBusquedaRapidaDto(
                t.Id,
                contexto.TiposDocumento.Where(d => d.Id == t.TipoDocumentoId).Select(d => d.Codigo).FirstOrDefault() ?? "-",
                t.NumeroDocumento,
                t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres,
                contexto.Empresas.Where(e => e.Id == t.EmpresaId).Select(e => e.RazonSocial).FirstOrDefault() ?? "-",
                contexto.Plantas.Where(p => p.Id == t.PlantaId).Select(p => p.Nombre).FirstOrDefault() ?? "-",
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == t.Id
                        && a.FechaInicio <= hoy
                        && (a.FechaFin == null || a.FechaFin >= hoy))
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.IdMarcador)
                    .FirstOrDefault(),
                t.FechaCese == null))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }
}
