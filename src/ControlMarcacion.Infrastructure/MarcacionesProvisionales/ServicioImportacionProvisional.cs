using System.Globalization;
using System.Security.Cryptography;
using ControlMarcacion.Application.MarcacionesProvisionales;
using ControlMarcacion.Application.MarcacionesProvisionales.Dto;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.MarcacionesProvisionales;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.MarcacionesProvisionales;

// ──────────────────────────────────────────────────────────────────────────────
// Módulo provisional — eliminar cuando el motor principal cubra este flujo.
// ──────────────────────────────────────────────────────────────────────────────

internal sealed class ServicioImportacionProvisional(
    MarcacionDbContext contexto,
    IFuenteDeMarcaciones fuente,
    IProveedorAlcancePlantas alcance) : IServicioImportacionProvisional
{
    public async Task<ResultadoCargaProvisional> ImportarAsync(
        Stream contenido,
        string nombreArchivo,
        long equipoId,
        string usuario)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        if (string.IsNullOrWhiteSpace(nombreArchivo))
        {
            throw new ReglaNegocioException("El nombre del archivo es obligatorio.");
        }

        if (equipoId <= 0)
        {
            throw new ReglaNegocioException("Debe indicar el equipo marcador de origen.");
        }

        if (string.IsNullOrWhiteSpace(usuario))
        {
            throw new ReglaNegocioException("No se pudo determinar el usuario que carga el archivo.");
        }

        var plantasVisibles = await alcance.PlantasVisiblesAsync().ConfigureAwait(false);
        var consultaEquipo = contexto.EquiposMarcador
            .AsNoTracking()
            .Where(e => e.Id == equipoId && e.Activo);

        if (plantasVisibles is not null)
        {
            consultaEquipo = consultaEquipo.Where(e => plantasVisibles.Contains(e.PlantaId));
        }

        var equipo = await consultaEquipo
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (equipo is null)
        {
            throw new ReglaNegocioException("El equipo marcador indicado no existe o está inactivo.");
        }

        var hash = await CalcularHashAsync(contenido).ConfigureAwait(false);

        var cargaPrevia = await contexto.ImportacionesProvisionales
            .AsNoTracking()
            .Where(i => i.HashContenido == hash)
            .OrderByDescending(i => i.FechaCarga)
            .Select(i => new { i.NombreArchivo, i.FechaCarga, i.EquipoId, i.Equipo!.PlantaId })
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (cargaPrevia is not null)
        {
            if (plantasVisibles is not null && !plantasVisibles.Contains(cargaPrevia.PlantaId))
            {
                throw new ReglaNegocioException("Este archivo ya fue cargado previamente.");
            }

            var equipoNombre = cargaPrevia.EquipoId == equipoId
                ? equipo.Nombre
                : (await contexto.EquiposMarcador.AsNoTracking()
                    .Where(e => e.Id == cargaPrevia.EquipoId)
                    .Select(e => e.Nombre)
                    .FirstOrDefaultAsync().ConfigureAwait(false) ?? "desconocido");

            throw new ReglaNegocioException(
                $"Este archivo ya se cargó como «{cargaPrevia.NombreArchivo}» " +
                $"el {cargaPrevia.FechaCarga:dd/MM/yyyy HH:mm} en el equipo {equipoNombre}.");
        }

        contenido.Position = 0;
        var lectura = fuente.Leer(contenido);

        var importacion = new ImportacionProvisional
        {
            EquipoId = equipoId,
            NombreArchivo = Recortar(nombreArchivo, 260),
            HashContenido = hash,
            FechaCarga = DateTime.Now,
            CargadoPorUsuario = usuario,
            FilasLeidas = lectura.TotalLineas,
            CreadoPor = usuario
        };

        var yaExistentes = lectura.Marcaciones.Count > 0
            ? await ObtenerExistentesAsync(equipoId, lectura.Marcaciones).ConfigureAwait(false)
            : new HashSet<(string, DateTime)>();

        var vistasEnLote = new HashSet<(string, DateTime)>();
        var nuevas = new List<MarcacionProvisional>();
        var duplicadasEnBase = 0;
        var duplicadasEnLote = 0;

        foreach (var m in lectura.Marcaciones)
        {
            var clave = (m.IdMarcador, m.FechaHora);

            if (yaExistentes.Contains(clave))
            {
                duplicadasEnBase++;
                importacion.Errores.Add(new ErrorImportacionProvisional
                {
                    NumeroLinea = m.NumeroLinea,
                    ContenidoLinea = m.LineaOriginal is { Length: > 500 }
                        ? m.LineaOriginal[..500]
                        : m.LineaOriginal,
                    Motivo = $"Duplicada: ya existe en base (marcador {m.IdMarcador}, {m.FechaHora:dd/MM/yyyy HH:mm:ss})",
                    CreadoPor = usuario
                });
                continue;
            }

            if (!vistasEnLote.Add(clave))
            {
                duplicadasEnLote++;
                importacion.Errores.Add(new ErrorImportacionProvisional
                {
                    NumeroLinea = m.NumeroLinea,
                    ContenidoLinea = m.LineaOriginal is { Length: > 500 }
                        ? m.LineaOriginal[..500]
                        : m.LineaOriginal,
                    Motivo = $"Duplicada en el archivo (marcador {m.IdMarcador}, {m.FechaHora:dd/MM/yyyy HH:mm:ss})",
                    CreadoPor = usuario
                });
                continue;
            }

            nuevas.Add(new MarcacionProvisional
            {
                EquipoId = equipoId,
                IdMarcador = m.IdMarcador,
                FechaHora = m.FechaHora,
                ModoVerificacion = m.ModoVerificacion,
                LineaOriginal = m.LineaOriginal is { Length: > 500 }
                    ? m.LineaOriginal[..500]
                    : m.LineaOriginal,
                CreadoPor = usuario
            });
        }

        importacion.FilasInsertadas = nuevas.Count;
        importacion.FilasRechazadas = lectura.Rechazadas.Count + duplicadasEnBase + duplicadasEnLote;

        foreach (var r in lectura.Rechazadas)
        {
            importacion.Errores.Add(new ErrorImportacionProvisional
            {
                NumeroLinea = r.NumeroLinea,
                ContenidoLinea = r.ContenidoLinea is { Length: > 500 }
                    ? r.ContenidoLinea[..500]
                    : r.ContenidoLinea,
                Motivo = Recortar(r.Motivo, 300),
                CreadoPor = usuario
            });
        }

        var estrategia = contexto.Database.CreateExecutionStrategy();

        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await contexto.Database
                .BeginTransactionAsync().ConfigureAwait(false);

            await contexto.ImportacionesProvisionales
                .AddAsync(importacion).ConfigureAwait(false);
            await contexto.SaveChangesAsync().ConfigureAwait(false);

            foreach (var m in nuevas)
            {
                m.ImportacionProvisionalId = importacion.Id;
            }

            await contexto.MarcacionesProvisionales
                .AddRangeAsync(nuevas).ConfigureAwait(false);

            try
            {
                await contexto.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                throw new ReglaNegocioException(
                    "Algunas marcaciones duplicadas no pudieron filtrarse antes de guardar. " +
                    "Intente cargar el archivo nuevamente.");
            }

            await transaccion.CommitAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);

        DateOnly? fechaMin = null;
        DateOnly? fechaMax = null;

        if (nuevas.Count > 0)
        {
            var fechas = nuevas.Select(m => DateOnly.FromDateTime(m.FechaHora));
            fechaMin = fechas.Min();
            fechaMax = fechas.Max();
        }

        return new ResultadoCargaProvisional(
            importacion.Id,
            importacion.NombreArchivo,
            importacion.FilasLeidas,
            importacion.FilasInsertadas,
            importacion.FilasRechazadas,
            fechaMin,
            fechaMax,
            lectura.Rechazadas
                .Select(r => new ErrorCargaProvisional(r.NumeroLinea, r.ContenidoLinea, r.Motivo))
                .ToList());
    }

    public async Task<IReadOnlyList<ImportacionProvisionalListaDto>> ListarImportacionesAsync(
        long? equipoId = null)
    {
        var query = contexto.ImportacionesProvisionales
            .AsNoTracking()
            .OrderByDescending(i => i.FechaCarga)
            .AsQueryable();

        var plantasVisibles = await alcance.PlantasVisiblesAsync().ConfigureAwait(false);

        if (plantasVisibles is not null)
        {
            query = query.Where(i => plantasVisibles.Contains(i.Equipo!.PlantaId));
        }

        if (equipoId.HasValue)
        {
            query = query.Where(i => i.EquipoId == equipoId.Value);
        }

        return await query
            .Select(i => new ImportacionProvisionalListaDto(
                i.Id,
                i.NombreArchivo,
                i.EquipoId,
                i.Equipo!.Nombre,
                i.FechaCarga,
                i.CargadoPorUsuario,
                i.FilasLeidas,
                i.FilasInsertadas,
                i.FilasRechazadas))
            .ToListAsync()
            .ConfigureAwait(false);
    }

    private async Task<HashSet<(string, DateTime)>> ObtenerExistentesAsync(
        long equipoId,
        IReadOnlyList<Application.Marcaciones.Dto.MarcacionLeida> marcaciones)
    {
        var ids = marcaciones.Select(m => m.IdMarcador).Distinct().ToList();
        var fechaMin = marcaciones.Min(m => m.FechaHora);
        var fechaMax = marcaciones.Max(m => m.FechaHora);

        var existentes = await contexto.MarcacionesProvisionales
            .AsNoTracking()
            .Where(m => m.EquipoId == equipoId
                && ids.Contains(m.IdMarcador)
                && m.FechaHora >= fechaMin
                && m.FechaHora <= fechaMax)
            .Select(m => new { m.IdMarcador, m.FechaHora })
            .ToListAsync()
            .ConfigureAwait(false);

        return existentes
            .Select(e => (e.IdMarcador, e.FechaHora))
            .ToHashSet();
    }

    private static async Task<string> CalcularHashAsync(Stream contenido)
    {
        contenido.Position = 0;
        var bytes = await SHA256.HashDataAsync(contenido).ConfigureAwait(false);
        contenido.Position = 0;
        return Convert.ToHexString(bytes).ToLower(CultureInfo.InvariantCulture);
    }

    private static string Recortar(string valor, int maximo) =>
        valor.Length <= maximo ? valor : valor[..maximo];
}
