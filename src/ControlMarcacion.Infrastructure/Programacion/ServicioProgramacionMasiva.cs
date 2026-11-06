using System.Security.Cryptography;
using System.Text;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Programacion;
using ControlMarcacion.Application.Programacion.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Programacion;

internal sealed class ServicioProgramacionMasiva(
    MarcacionDbContext contexto,
    IProveedorAlcancePlantas alcance,
    IServicioPeriodos periodos) : IServicioProgramacionMasiva
{
    private static readonly SemaphoreSlim Exclusividad = new(1, 1);

    public async Task<PrevisualizacionMasiva> PrevisualizarAsync(
        SolicitudProgramacionMasiva solicitud,
        CancellationToken cancelacion = default)
    {
        await ValidarAlcanceAsync(solicitud.PlantaId, cancelacion).ConfigureAwait(false);
        await ValidarPeriodosAbiertosAsync(solicitud.FechaEfectiva, cancelacion).ConfigureAwait(false);

        var turnoDestino = await contexto.Turnos.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == solicitud.TurnoId && t.Activo, cancelacion)
            .ConfigureAwait(false)
            ?? throw new ReglaNegocioException("El turno seleccionado no existe o está inactivo.");

        var trabajadores = await CargarTrabajadoresAsync(
            solicitud.PlantaId, solicitud.TrabajadorIds, cancelacion).ConfigureAwait(false);

        var filas = new List<FilaPrevisualizacion>(trabajadores.Count);

        foreach (var t in trabajadores.OrderBy(t => t.ApellidoPaterno).ThenBy(t => t.ApellidoMaterno).ThenBy(t => t.Nombres))
        {
            var fila = Previsualizar(t, solicitud.TurnoId, turnoDestino.Nombre, solicitud.FechaEfectiva);
            filas.Add(fila);
        }

        var hash = CalcularHashCompleto(solicitud, trabajadores);

        return new PrevisualizacionMasiva(
            hash,
            filas.Count,
            filas.Count(f => f.Estado == EstadoCambio.Cambio),
            filas.Count(f => f.Estado == EstadoCambio.SinCambio),
            filas.Count(f => f.Estado == EstadoCambio.Advertencia),
            filas);
    }

    public async Task<ResultadoProgramacionMasiva> ConfirmarAsync(
        SolicitudProgramacionMasiva solicitud,
        string hashPrevisualizado,
        string usuario,
        CancellationToken cancelacion = default)
    {
        await ValidarAlcanceAsync(solicitud.PlantaId, cancelacion).ConfigureAwait(false);
        await ValidarPeriodosAbiertosAsync(solicitud.FechaEfectiva, cancelacion).ConfigureAwait(false);

        var turnoDestino = await contexto.Turnos.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == solicitud.TurnoId && t.Activo, cancelacion)
            .ConfigureAwait(false)
            ?? throw new ReglaNegocioException("El turno seleccionado no existe o está inactivo.");

        await Exclusividad.WaitAsync(cancelacion).ConfigureAwait(false);
        try
        {
            return await contexto.Database.CreateExecutionStrategy().ExecuteAsync(
                async (ct) =>
                {
                    await using var transaccion = await contexto.Database
                        .BeginTransactionAsync(ct).ConfigureAwait(false);

                    var trabajadores = await CargarTrabajadoresAsync(
                        solicitud.PlantaId, solicitud.TrabajadorIds, ct).ConfigureAwait(false);

                    var hashActual = CalcularHashCompleto(solicitud, trabajadores);
                    if (!string.Equals(hashPrevisualizado, hashActual, StringComparison.Ordinal))
                    {
                        throw new ReglaNegocioException(
                            "El estado de los trabajadores cambió desde la previsualización. " +
                            "Vuelva a previsualizar antes de confirmar.");
                    }

                    var procesados = 0;
                    var sinCambio = 0;
                    var errores = new List<ErrorProgramacion>();

                    foreach (var t in trabajadores)
                    {
                        try
                        {
                            var cambio = ReasignarTurno(t, solicitud.TurnoId, solicitud.FechaEfectiva, usuario);
                            if (cambio)
                            {
                                procesados++;
                            }
                            else
                            {
                                sinCambio++;
                            }
                        }
                        catch (ReglaNegocioException ex)
                        {
                            errores.Add(new ErrorProgramacion(t.Id, t.ApellidosYNombres, ex.Message));
                        }
                    }

                    if (errores.Count > 0)
                    {
                        await transaccion.RollbackAsync(ct).ConfigureAwait(false);
                        return new ResultadoProgramacionMasiva(0, 0, errores.Count, errores);
                    }

                    await contexto.SaveChangesAsync(ct).ConfigureAwait(false);
                    await transaccion.CommitAsync(ct).ConfigureAwait(false);

                    return new ResultadoProgramacionMasiva(procesados, sinCambio, 0, []);
                },
                cancellationToken: cancelacion).ConfigureAwait(false);
        }
        finally
        {
            Exclusividad.Release();
        }
    }

    private static bool ReasignarTurno(
        Trabajador trabajador,
        long turnoId,
        DateOnly desde,
        string usuario)
    {
        var asignaciones = trabajador.AsignacionesTurno;
        var vigente = asignaciones.FirstOrDefault(a => a.FechaFin is null);

        if (vigente is null)
        {
            if (trabajador.FechaCese is not null)
            {
                return false;
            }

            asignaciones.Add(new AsignacionTurno
            {
                TurnoId = turnoId,
                CuadrillaId = null,
                FechaInicio = desde,
                CreadoPor = usuario
            });
            return true;
        }

        var mismoTurno = vigente.TurnoId == turnoId && vigente.CuadrillaId is null;
        var inicioVigente = vigente.FechaInicio;

        if (desde == inicioVigente && mismoTurno)
        {
            return false;
        }

        if (desde == inicioVigente)
        {
            vigente.TurnoId = turnoId;
            vigente.CuadrillaId = null;
            vigente.ModificadoPor = usuario;
            return true;
        }

        if (desde > inicioVigente && !mismoTurno)
        {
            vigente.FechaFin = desde.AddDays(-1);
            vigente.ModificadoPor = usuario;

            asignaciones.Add(new AsignacionTurno
            {
                TurnoId = turnoId,
                CuadrillaId = null,
                FechaInicio = desde,
                CreadoPor = usuario
            });
            return true;
        }

        if (desde > inicioVigente)
        {
            return false;
        }

        var anterior = asignaciones
            .Where(a => a.FechaFin is not null)
            .OrderByDescending(a => a.FechaInicio)
            .FirstOrDefault();

        if (anterior?.FechaFin is { } fin && fin >= desde)
        {
            if (anterior.FechaInicio >= desde)
            {
                throw new ReglaNegocioException(
                    $"La fecha {desde:dd/MM/yyyy} eliminaría la asignación anterior de " +
                    $"{trabajador.ApellidosYNombres}. Registre la rotación en orden.");
            }

            anterior.FechaFin = desde.AddDays(-1);
            anterior.ModificadoPor = usuario;
        }

        vigente.FechaInicio = desde;
        vigente.TurnoId = turnoId;
        vigente.CuadrillaId = null;
        vigente.ModificadoPor = usuario;
        return true;
    }

    private static FilaPrevisualizacion Previsualizar(
        Trabajador trabajador,
        long turnoDestinoId,
        string turnoDestinoNombre,
        DateOnly fecha)
    {
        if (trabajador.FechaCese is { } cese && cese < fecha)
        {
            return Fila(trabajador, turnoDestinoNombre, fecha, EstadoCambio.SinCambio, "Cesado");
        }

        var vigente = trabajador.AsignacionesTurno.FirstOrDefault(a => a.FechaFin is null);

        if (vigente is null)
        {
            return Fila(trabajador, turnoDestinoNombre, fecha, EstadoCambio.Cambio, "Primera asignación");
        }

        string? turnoActual = null;
        string? cuadrillaActual = null;
        string? observacion = null;
        var estado = EstadoCambio.Cambio;

        if (vigente.CuadrillaId is not null)
        {
            cuadrillaActual = vigente.Cuadrilla?.Nombre ?? $"Cuadrilla #{vigente.CuadrillaId}";
            estado = EstadoCambio.Advertencia;
            observacion = $"Corta asignación a {cuadrillaActual}";
        }
        else if (vigente.TurnoId == turnoDestinoId)
        {
            turnoActual = vigente.Turno?.Nombre ?? $"Turno #{vigente.TurnoId}";
            estado = EstadoCambio.SinCambio;
            observacion = $"Ya tiene turno {turnoActual} desde {vigente.FechaInicio:dd/MM/yyyy}";
        }
        else
        {
            turnoActual = vigente.Turno?.Nombre ?? $"Turno #{vigente.TurnoId}";
        }

        if (fecha < vigente.FechaInicio && estado == EstadoCambio.Cambio)
        {
            observacion = "Corrección en sitio";
        }

        return new FilaPrevisualizacion(
            trabajador.Id,
            trabajador.NumeroDocumento,
            trabajador.ApellidosYNombres,
            turnoActual,
            cuadrillaActual,
            turnoDestinoNombre,
            fecha,
            estado,
            observacion);
    }

    private static FilaPrevisualizacion Fila(
        Trabajador trabajador,
        string turnoNuevo,
        DateOnly fecha,
        EstadoCambio estado,
        string? observacion) =>
        new(trabajador.Id, trabajador.NumeroDocumento, trabajador.ApellidosYNombres,
            null, null, turnoNuevo, fecha, estado, observacion);

    private async Task<List<Trabajador>> CargarTrabajadoresAsync(
        long plantaId,
        IReadOnlyList<long> ids,
        CancellationToken cancelacion)
    {
        var trabajadores = await contexto.Trabajadores
            .Include(t => t.AsignacionesTurno.OrderByDescending(a => a.FechaInicio))
                .ThenInclude(a => a.Turno)
            .Include(t => t.AsignacionesTurno)
                .ThenInclude(a => a.Cuadrilla)
            .Where(t => ids.Contains(t.Id) && t.PlantaId == plantaId && t.Activo)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        if (trabajadores.Count != ids.Distinct().Count())
        {
            var encontrados = trabajadores.Select(t => t.Id).ToHashSet();
            var faltantes = ids.Where(id => !encontrados.Contains(id)).ToList();
            if (faltantes.Count > 0)
            {
                throw new ReglaNegocioException(
                    $"{faltantes.Count} trabajador(es) no pertenecen a la planta seleccionada o no están activos.");
            }
        }

        return trabajadores;
    }

    private async Task ValidarAlcanceAsync(long plantaId, CancellationToken cancelacion)
    {
        if (!await alcance.PuedeVerPlantaAsync(plantaId, cancelacion).ConfigureAwait(false))
        {
            throw new ReglaNegocioException("No tiene acceso a la planta seleccionada.");
        }
    }

    private async Task ValidarPeriodosAbiertosAsync(DateOnly fecha, CancellationToken cancelacion)
    {
        var cerrados = (await periodos.ListarAsync(incluirInactivos: false, cancelacion).ConfigureAwait(false))
            .Where(p => !p.EstaAbierto && p.FechaInicio <= fecha && p.FechaFin >= fecha)
            .ToList();

        if (cerrados.Count > 0)
        {
            throw new ReglaNegocioException(
                $"La fecha {fecha:dd/MM/yyyy} cae dentro del periodo {cerrados[0].Codigo}, " +
                "que está cerrado y no admite cambios.");
        }
    }

    internal static string CalcularHash(SolicitudProgramacionMasiva solicitud)
    {
        var datos = $"{solicitud.PlantaId}|{solicitud.TurnoId}|{solicitud.FechaEfectiva:yyyy-MM-dd}|" +
                    string.Join(",", solicitud.TrabajadorIds.Order());
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(datos));
        return Convert.ToHexStringLower(bytes);
    }

    internal static string CalcularHashCompleto(
        SolicitudProgramacionMasiva solicitud,
        IReadOnlyList<Trabajador> trabajadores)
    {
        var sb = new StringBuilder();
        sb.Append(solicitud.PlantaId).Append('|');
        sb.Append(solicitud.TurnoId).Append('|');
        sb.Append(solicitud.FechaEfectiva.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).Append('|');
        sb.Append(string.Join(",", solicitud.TrabajadorIds.Order()));

        foreach (var t in trabajadores.OrderBy(t => t.Id))
        {
            var v = t.AsignacionesTurno.FirstOrDefault(a => a.FechaFin is null);
            sb.Append('|').Append(t.Id).Append(':');
            sb.Append(v?.TurnoId ?? 0).Append(':');
            sb.Append(v?.CuadrillaId ?? 0).Append(':');
            sb.Append(v?.FechaInicio.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "-").Append(':');
            sb.Append(t.FechaCese?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "-");
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }
}
