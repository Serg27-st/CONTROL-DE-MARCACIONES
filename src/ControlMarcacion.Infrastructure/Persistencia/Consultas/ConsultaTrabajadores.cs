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
    private readonly ConsultaContextoLaboral consultaContextoLaboral = new(contexto);

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

        if (filtro.PuestoId is { } puestoId)
        {
            consulta = consulta.Where(t => t.PuestoId == puestoId);
        }

        if (filtro.AreaId is { } areaId)
        {
            consulta = consulta.Where(t => t.AreaId == areaId);
        }

        // Aparato y número de marcador miran la asignación VIGENTE (sin fecha de fin): es la
        // que usará el cálculo para las marcaciones de hoy en adelante.
        if (filtro.EquipoId is { } equipoId)
        {
            consulta = consulta.Where(t => contexto.AsignacionesMarcador
                .Any(a => a.TrabajadorId == t.Id && a.FechaFin == null && a.EquipoId == equipoId));
        }

        if (filtro.ConNumeroMarcador is { } conNumero)
        {
            consulta = conNumero
                ? consulta.Where(t => contexto.AsignacionesMarcador.Any(a => a.TrabajadorId == t.Id && a.FechaFin == null))
                : consulta.Where(t => !contexto.AsignacionesMarcador.Any(a => a.TrabajadorId == t.Id && a.FechaFin == null));
        }

        if (filtro.TurnoId is { } turnoId)
        {
            if (turnoId == FiltroTrabajadores.SinTurno)
            {
                consulta = consulta.Where(t => !contexto.AsignacionesTurno.Any(a => a.TrabajadorId == t.Id && a.FechaFin == null));
            }
            else
            {
                // Quien integra una cuadrilla tiene hoy el turno que le toca al grupo: se
                // resuelve en memoria qué cuadrillas están hoy en ese turno —son pocas— y la
                // base filtra por ellas. Buscar «NOC» y no ver a la cuadrilla que está de noche
                // sería el filtro mintiendo.
                var enEseTurno = await CuadrillasEnTurnoAsync(
                        turnoId, DateOnly.FromDateTime(DateTime.Now), cancelacion)
                    .ConfigureAwait(false);

                consulta = consulta.Where(t => contexto.AsignacionesTurno
                    .Any(a => a.TrabajadorId == t.Id
                              && a.FechaFin == null
                              && (a.TurnoId == turnoId
                                  || (a.CuadrillaId != null && enEseTurno.Contains(a.CuadrillaId.Value)))));
            }
        }

        if (filtro.IngresoDesde is { } ingresoDesde)
        {
            consulta = consulta.Where(t => t.FechaIngreso >= ingresoDesde);
        }

        if (filtro.IngresoHasta is { } ingresoHasta)
        {
            consulta = consulta.Where(t => t.FechaIngreso <= ingresoHasta);
        }

        if (filtro.SoloActivos is { } soloActivos)
        {
            consulta = soloActivos
                ? consulta.Where(t => t.FechaCese == null)
                : consulta.Where(t => t.FechaCese != null);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            // Sin distinguir mayúsculas: ver BusquedaTexto, que explica también por qué se
            // silencian los analizadores de cultura aquí (esto se traduce a SQL).
            var termino = BusquedaTexto.Normalizar(filtro.Busqueda);

            // El número de marcador se compara exacto y tal cual —con sus ceros—: «14» no
            // debe traer también al 140, y «0014» es otra persona.
            var numero = filtro.Busqueda.Trim();

#pragma warning disable CA1304, CA1311, CA1862
            consulta = consulta.Where(t =>
                t.NumeroDocumento.ToUpper().Contains(termino)
                || t.ApellidoPaterno.ToUpper().Contains(termino)
                || t.ApellidoMaterno.ToUpper().Contains(termino)
                || t.Nombres.ToUpper().Contains(termino)
                || contexto.AsignacionesMarcador.Any(a =>
                    a.TrabajadorId == t.Id && a.FechaFin == null && a.IdMarcador == numero));
#pragma warning restore CA1304, CA1311, CA1862
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
                    .Select(a => a.Turno != null ? a.Turno.Codigo : "Cuadrilla " + a.Cuadrilla!.Codigo)
                    .FirstOrDefault(),
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.IdMarcador)
                    .FirstOrDefault(),
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.Equipo!.Nombre)
                    .FirstOrDefault(),
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.Equipo!.Codigo)
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
                // El aparato de esa misma asignación: el código por sí solo no dice cuál
                // de los dos "21" de la planta es el suyo.
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => (long?)a.EquipoId)
                    .FirstOrDefault(),
                contexto.AsignacionesTurno
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.TurnoId)
                    .FirstOrDefault(),
                contexto.AsignacionesTurno
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.CuadrillaId)
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
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.Equipo!.Nombre)
                    .FirstOrDefault(),
                contexto.AsignacionesTurno
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.Turno != null
                        ? a.Turno.Codigo + " - " + a.Turno.Nombre
                        : "Cuadrilla " + a.Cuadrilla!.Codigo + " - " + a.Cuadrilla.Nombre + " (rota)")
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
                a.Equipo!.Nombre,
                // Se resuelve contra el catálogo, no contra la planta del trabajador: si
                // la persona cambió de planta, el marcador que usaba antes sigue siendo
                // el de la planta anterior.
                //
                // ⚠️ Acotado al EQUIPO de la asignación. Buscar solo por código elegía una
                // fila cualquiera entre los aparatos que comparten ese número, así que el
                // historial podía atribuir el tramo a la planta equivocada.
                contexto.Marcadores
                    .Where(m => m.Codigo == a.IdMarcador && m.EquipoId == a.EquipoId)
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
                a.Turno != null
                    ? a.Turno.Codigo + " - " + a.Turno.Nombre
                    : "Cuadrilla " + a.Cuadrilla!.Codigo + " - " + a.Cuadrilla.Nombre + " (rota)",
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

        // Sin distinguir mayúsculas: ver BusquedaTexto.
        var termino = BusquedaTexto.Normalizar(texto);
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        // "Activa" es la asignación de marcador vigente hoy: empezó en el pasado (o
        // hoy) y todavía no tiene fecha de fin, o su fecha de fin es futura.
#pragma warning disable CA1304, CA1311, CA1862
        var consulta = contexto.Trabajadores
            .AsNoTracking()
            .Where(t =>
                t.NumeroDocumento.ToUpper().Contains(termino)
                || t.ApellidoPaterno.ToUpper().Contains(termino)
                || t.ApellidoMaterno.ToUpper().Contains(termino)
                || t.Nombres.ToUpper().Contains(termino)
                || contexto.AsignacionesMarcador.Any(a =>
                    a.TrabajadorId == t.Id
                    && a.IdMarcador.ToUpper().Contains(termino)
                    && a.FechaInicio <= hoy
                    && (a.FechaFin == null || a.FechaFin >= hoy)));
#pragma warning restore CA1304, CA1311, CA1862

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
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == t.Id
                        && a.FechaInicio <= hoy
                        && (a.FechaFin == null || a.FechaFin >= hoy))
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.Equipo!.Nombre)
                    .FirstOrDefault(),
                t.FechaCese == null))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<IntegranteCuadrillaDto>> IntegrantesDeCuadrillaAsync(
        long cuadrillaId,
        DateOnly fecha,
        CancellationToken cancelacion = default)
    {
        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        var integrantes = await contexto.AsignacionesTurno
            .AsNoTracking()
            .Where(a => a.CuadrillaId == cuadrillaId
                        && a.FechaInicio <= fecha
                        && (a.FechaFin == null || a.FechaFin >= fecha))
            .OrderBy(a => a.Trabajador!.ApellidoPaterno)
            .ThenBy(a => a.Trabajador!.ApellidoMaterno)
            .ThenBy(a => a.Trabajador!.Nombres)
            .Select(a => new IntegranteCuadrillaDto(
                a.TrabajadorId,
                a.Trabajador!.ApellidoPaterno + " " + a.Trabajador.ApellidoMaterno + ", " + a.Trabajador.Nombres,
                a.Trabajador.NumeroDocumento,
                a.FechaInicio,
                a.FechaFin))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var ids = integrantes.Select(i => i.TrabajadorId).Distinct().ToList();
        var contextos = await ContextosLaboralesEnFechaAsync(ids, fecha, cancelacion)
            .ConfigureAwait(false);

        return
        [
            .. integrantes.Where(i =>
                contextos.TryGetValue(i.TrabajadorId, out var laboral)
                && laboral.PlantaId is { } plantaId
                && (plantasVisibles is null || plantasVisibles.Contains(plantaId)))
        ];
    }

    public async Task<IReadOnlyList<CandidatoCuadrillaDto>> CandidatosParaCuadrillaAsync(
        long cuadrillaId,
        long plantaId,
        DateOnly fecha,
        CancellationToken cancelacion = default)
    {
        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        if (plantasVisibles is not null && !plantasVisibles.Contains(plantaId))
        {
            return [];
        }

        var historias = await consultaContextoLaboral
            .CargarRangoAsync(fecha, fecha, cancelacion: cancelacion)
            .ConfigureAwait(false);
        var candidatosHistoricos = historias
            .Where(par => par.Value.Resolver(fecha).ObtenerOError()?.PlantaId == plantaId)
            .Select(par => par.Key)
            .ToList();

        return await contexto.Trabajadores
            .AsNoTracking()
            .Where(t => candidatosHistoricos.Contains(t.Id)
                        && !contexto.AsignacionesTurno.Any(a =>
                            a.TrabajadorId == t.Id
                            && a.CuadrillaId == cuadrillaId
                            && a.FechaInicio <= fecha
                            && (a.FechaFin == null || a.FechaFin >= fecha)))
            .OrderBy(t => t.ApellidoPaterno)
            .ThenBy(t => t.ApellidoMaterno)
            .ThenBy(t => t.Nombres)
            .Select(t => new CandidatoCuadrillaDto(
                t.Id,
                t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres,
                t.NumeroDocumento,
                contexto.AsignacionesTurno
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .OrderByDescending(a => a.FechaInicio)
                    .Select(a => a.Turno != null
                        ? "Turno " + a.Turno.Codigo
                        : "Cuadrilla " + a.Cuadrilla!.Codigo)
                    .FirstOrDefault()))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<long, ContextoLaboralFechaDto>> ContextosLaboralesEnFechaAsync(
        IReadOnlyCollection<long> trabajadorIds,
        DateOnly fecha,
        CancellationToken cancelacion = default)
    {
        var ids = trabajadorIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<long, ContextoLaboralFechaDto>();
        }

        var historias = await consultaContextoLaboral
            .CargarRangoAsync(fecha, fecha, ids, cancelacion)
            .ConfigureAwait(false);
        var resultado = new Dictionary<long, ContextoLaboralFechaDto>();

        foreach (var id in ids)
        {
            var laboral = historias[id].Resolver(fecha).ObtenerOError();
            if (laboral is null)
            {
                continue;
            }

            resultado.Add(id, new ContextoLaboralFechaDto(
                laboral.PeriodoLaboralId,
                laboral.EmpresaId,
                laboral.PlantaId,
                laboral.FechaInicio,
                laboral.FechaFin));
        }

        return resultado;
    }

    /// <summary>
    /// Cuadrillas cuyo ciclo está en <paramref name="turnoId"/> en <paramref name="fecha"/>.
    /// </summary>
    /// <remarks>
    /// Se resuelve en memoria con <see cref="ProgramacionCuadrilla.TurnoEn"/>, la misma regla
    /// que usa el motor: traducir el ciclo a SQL duplicaría la lógica y podría divergir.
    /// </remarks>
    private async Task<List<long>> CuadrillasEnTurnoAsync(
        long turnoId,
        DateOnly fecha,
        CancellationToken cancelacion)
    {
        var vigentes = await contexto.ProgramacionesCuadrilla
            .AsNoTracking()
            .Include(p => p.Tramos)
            .Where(p => p.Activo && p.FechaInicio <= fecha && (p.FechaFin == null || p.FechaFin >= fecha))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return vigentes
            .Where(p => p.TurnoEn(fecha) == turnoId)
            .Select(p => p.CuadrillaId)
            .Distinct()
            .ToList();
    }
}
