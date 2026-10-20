using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Domain.Comun;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Repositorios;

internal sealed class TrabajadorRepository(MarcacionDbContext contexto) : ITrabajadorRepository
{
    public Task<Trabajador?> ObtenerPorIdAsync(long id, CancellationToken cancelacion = default) =>
        contexto.Trabajadores
            .Include(t => t.AsignacionesMarcador)
            .Include(t => t.AsignacionesTurno)
            .Include(t => t.AsignacionesSueldo)
            .Include(t => t.AsignacionesArea)
            // Las escrituras necesitan toda la historia para impedir que una corrección
            // del vínculo abierto se solape con un periodo cerrado anterior.
            .Include(t => t.PeriodosLaborales)
                .ThenInclude(p => p.AsignacionesPlanta)
            .FirstOrDefaultAsync(t => t.Id == id, cancelacion);

    public async Task<long?> IdPorDocumentoAsync(
        long tipoDocumentoId,
        string numeroDocumento,
        CancellationToken cancelacion = default) =>
        await contexto.Trabajadores
            .AsNoTracking()
            .Where(t => t.TipoDocumentoId == tipoDocumentoId && t.NumeroDocumento == numeroDocumento)
            .Select(t => (long?)t.Id)
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

    public Task<bool> ExisteDocumentoAsync(
        long tipoDocumentoId,
        string numeroDocumento,
        long? excluyendoId = null,
        CancellationToken cancelacion = default) =>
        contexto.Trabajadores
            .AsNoTracking()
            .AnyAsync(
                t => t.TipoDocumentoId == tipoDocumentoId
                     && t.NumeroDocumento == numeroDocumento
                     && (excluyendoId == null || t.Id != excluyendoId),
                cancelacion);

    public Task<bool> IdMarcadorEnUsoAsync(
        long equipoId,
        string idMarcador,
        DateOnly desde,
        DateOnly? hasta,
        long? excluyendoTrabajadorId = null,
        CancellationToken cancelacion = default) =>
        contexto.AsignacionesMarcador
            .AsNoTracking()
            .AnyAsync(
                a => a.EquipoId == equipoId && a.IdMarcador == idMarcador
                     && (excluyendoTrabajadorId == null || a.TrabajadorId != excluyendoTrabajadorId)
                     // Dos rangos se solapan si cada uno empieza antes de que termine el otro.
                     // Un FechaFin nulo significa "sigue vigente", así que nunca termina.
                     && (hasta == null || a.FechaInicio <= hasta)
                     && (a.FechaFin == null || a.FechaFin >= desde),
                cancelacion);

    public async Task AgregarAsync(Trabajador trabajador, CancellationToken cancelacion = default) =>
        await contexto.Trabajadores.AddAsync(trabajador, cancelacion).ConfigureAwait(false);

    public void Actualizar(Trabajador trabajador) =>
        // ObtenerPorIdAsync devuelve un agregado seguido. Marcar solo la raíz evita que
        // Update(graph) reescriba periodos históricos cerrados que se cargaron únicamente
        // para validar solapamientos; EF detecta por separado los hijos realmente cambiados.
        contexto.Entry(trabajador).State = EntityState.Modified;

    public Task<int> GuardarCambiosAsync(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);

    public async Task<int> GuardarReingresoAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        }
        catch (DbUpdateException excepcion)
            when (excepcion.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            throw new ReglaNegocioException(
                "No se pudo registrar el reingreso porque los datos laborales cambiaron " +
                "al mismo tiempo. Actualice el historial y verifique si otro usuario ya " +
                "abrió el vínculo.");
        }
    }
}
