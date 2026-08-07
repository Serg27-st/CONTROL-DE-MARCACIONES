using ControlMarcacion.Domain.Personal;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Repositorios;

internal sealed class TrabajadorRepository(MarcacionDbContext contexto) : ITrabajadorRepository
{
    public Task<Trabajador?> ObtenerPorIdAsync(long id, CancellationToken cancelacion = default) =>
        contexto.Trabajadores
            .Include(t => t.AsignacionesMarcador)
            .Include(t => t.AsignacionesTurno)
            .Include(t => t.AsignacionesSueldo)
            .FirstOrDefaultAsync(t => t.Id == id, cancelacion);

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
        string idMarcador,
        DateOnly desde,
        DateOnly? hasta,
        long? excluyendoTrabajadorId = null,
        CancellationToken cancelacion = default) =>
        contexto.AsignacionesMarcador
            .AsNoTracking()
            .AnyAsync(
                a => a.IdMarcador == idMarcador
                     && (excluyendoTrabajadorId == null || a.TrabajadorId != excluyendoTrabajadorId)
                     // Dos rangos se solapan si cada uno empieza antes de que termine el otro.
                     // Un FechaFin nulo significa "sigue vigente", así que nunca termina.
                     && (hasta == null || a.FechaInicio <= hasta)
                     && (a.FechaFin == null || a.FechaFin >= desde),
                cancelacion);

    public async Task AgregarAsync(Trabajador trabajador, CancellationToken cancelacion = default) =>
        await contexto.Trabajadores.AddAsync(trabajador, cancelacion).ConfigureAwait(false);

    public void Actualizar(Trabajador trabajador) =>
        contexto.Trabajadores.Update(trabajador);

    public Task<int> GuardarCambiosAsync(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);
}
