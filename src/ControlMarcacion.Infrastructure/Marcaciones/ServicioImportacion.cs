using System.Globalization;
using System.Security.Cryptography;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ControlMarcacion.Infrastructure.Marcaciones;

/// <summary>
/// Importa marcaciones desde un archivo del marcador.
/// </summary>
/// <remarks>
/// Las marcaciones crudas son <b>inmutables</b>: aquí solo se insertan. Cualquier
/// corrección posterior vive en Justificaciones, nunca reescribiendo este dato.
/// </remarks>
internal sealed class ServicioImportacion(
    MarcacionDbContext contexto,
    IFuenteDeMarcaciones fuente,
    IOptions<OpcionesMarcador> opciones) : IServicioImportacion
{
    private const string MotivoSinAsignar = "ID de marcador sin trabajador asignado";

    private readonly OpcionesMarcador _opciones = opciones.Value;

    public async Task<ImportacionPrevia?> BuscarImportacionPreviaAsync(
        Stream contenido,
        CancellationToken cancelacion = default)
    {
        var hash = await CalcularHashAsync(contenido, cancelacion).ConfigureAwait(false);

        return await contexto.Importaciones
            .AsNoTracking()
            .Where(i => i.HashContenido == hash)
            .OrderByDescending(i => i.FechaCarga)
            .Select(i => new ImportacionPrevia(
                i.Id, i.NombreArchivo, i.FechaCarga, i.CargadoPorUsuario, i.FilasInsertadas))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<ResultadoImportacion> ImportarAsync(
        Stream contenido,
        string nombreArchivo,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        var hash = await CalcularHashAsync(contenido, cancelacion).ConfigureAwait(false);

        contenido.Position = 0;
        var lectura = fuente.Leer(contenido);

        var importacion = new ImportacionMarcador
        {
            NombreArchivo = nombreArchivo,
            HashContenido = hash,
            FechaCarga = DateTime.Now,
            CargadoPorUsuario = usuario,
            FilasLeidas = lectura.TotalLineas,
            CreadoPor = usuario
        };

        // Las líneas ilegibles se registran tal cual: quien revise necesita el número de
        // línea y el contenido para poder corregir el archivo o el equipo.
        foreach (var rechazada in lectura.Rechazadas)
        {
            importacion.Errores.Add(new ErrorImportacion
            {
                NumeroLinea = rechazada.NumeroLinea,
                ContenidoLinea = Recortar(rechazada.ContenidoLinea, 500),
                Motivo = Recortar(rechazada.Motivo, 300),
                CreadoPor = usuario
            });
        }

        var (atribuidas, sinAsignar) = await AtribuirATrabajadoresAsync(lectura.Marcaciones, cancelacion)
            .ConfigureAwait(false);

        foreach (var id in sinAsignar)
        {
            importacion.Errores.Add(new ErrorImportacion
            {
                NumeroLinea = id.PrimeraLinea,
                ContenidoLinea = Recortar($"ID {id.Resumen.IdMarcador}", 500),
                Motivo = Recortar(
                    $"{MotivoSinAsignar}: '{id.Resumen.IdMarcador}' aparece {id.Resumen.Marcaciones} vez/veces " +
                    $"entre el {id.Resumen.PrimeraFecha:dd/MM/yyyy} y el {id.Resumen.UltimaFecha:dd/MM/yyyy}, " +
                    "pero ningún trabajador lo tenía asignado en esas fechas.", 300),
                CreadoPor = usuario
            });
        }

        var deduplicadas = ColapsarDobleFichada(atribuidas, out var descartadasEnArchivo);

        var yaExistentes = await ContarYaExistentesAsync(deduplicadas, cancelacion).ConfigureAwait(false);

        var nuevas = deduplicadas.Where(m => !yaExistentes.Contains((m.TrabajadorId, m.Lectura.FechaHora))).ToList();

        importacion.FilasInsertadas = nuevas.Count;
        importacion.FilasDuplicadas = descartadasEnArchivo + (deduplicadas.Count - nuevas.Count);
        importacion.FilasRechazadas = lectura.Rechazadas.Count + sinAsignar.Sum(s => s.Resumen.Marcaciones);

        await contexto.Importaciones.AddAsync(importacion, cancelacion).ConfigureAwait(false);
        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        foreach (var marcacion in nuevas)
        {
            await contexto.MarcacionesCrudas.AddAsync(new MarcacionCruda
            {
                TrabajadorId = marcacion.TrabajadorId,
                IdMarcador = marcacion.Lectura.IdMarcador,
                FechaHora = marcacion.Lectura.FechaHora,
                ImportacionId = importacion.Id,
                ModoVerificacion = marcacion.Lectura.ModoVerificacion,
                TipoMarcacion = marcacion.Lectura.TipoMarcacion,
                LineaOriginal = Recortar(marcacion.Lectura.LineaOriginal, 500),
                CreadoPor = usuario
            }, cancelacion).ConfigureAwait(false);
        }

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        return new ResultadoImportacion(
            importacion.Id,
            importacion.NombreArchivo,
            importacion.FilasLeidas,
            importacion.FilasInsertadas,
            importacion.FilasDuplicadas,
            importacion.FilasRechazadas,
            lectura.Rechazadas,
            [.. sinAsignar.Select(s => s.Resumen)]);
    }

    public async Task<IReadOnlyList<ImportacionHistorialDto>> HistorialAsync(
        int maximo = 50,
        CancellationToken cancelacion = default) =>
        await contexto.Importaciones
            .AsNoTracking()
            .OrderByDescending(i => i.FechaCarga)
            .Take(maximo)
            .Select(i => new ImportacionHistorialDto(
                i.Id, i.NombreArchivo, i.FechaCarga, i.CargadoPorUsuario,
                i.FilasLeidas, i.FilasInsertadas, i.FilasDuplicadas, i.FilasRechazadas))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<ResultadoImportacion?> ObtenerResultadoAsync(
        long importacionId,
        CancellationToken cancelacion = default)
    {
        var importacion = await contexto.Importaciones
            .AsNoTracking()
            .Include(i => i.Errores)
            .FirstOrDefaultAsync(i => i.Id == importacionId, cancelacion)
            .ConfigureAwait(false);

        if (importacion is null)
        {
            return null;
        }

        var rechazadas = importacion.Errores
            .Where(e => !e.Motivo.StartsWith(MotivoSinAsignar, StringComparison.Ordinal))
            .Select(e => new LineaRechazada(e.NumeroLinea, e.ContenidoLinea ?? string.Empty, e.Motivo))
            .ToList();

        var sinAsignar = importacion.Errores
            .Where(e => e.Motivo.StartsWith(MotivoSinAsignar, StringComparison.Ordinal))
            .Select(e => new IdMarcadorSinAsignar(
                (e.ContenidoLinea ?? string.Empty).Replace("ID ", string.Empty, StringComparison.Ordinal),
                0, default, default))
            .ToList();

        return new ResultadoImportacion(
            importacion.Id, importacion.NombreArchivo, importacion.FilasLeidas,
            importacion.FilasInsertadas, importacion.FilasDuplicadas, importacion.FilasRechazadas,
            rechazadas, sinAsignar);
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Resuelve cada marcación al trabajador que tenía ese ID de marcador
    /// <b>en la fecha de la marcación</b>.
    /// </summary>
    /// <remarks>
    /// No se resuelve por la asignación actual: los IDs se reasignan entre personas, y
    /// hacerlo por la vigente atribuiría marcaciones históricas a quien no las hizo.
    /// </remarks>
    private async Task<(List<MarcacionAtribuida> Atribuidas, List<SinAsignar> SinAsignar)>
        AtribuirATrabajadoresAsync(IReadOnlyList<MarcacionLeida> marcaciones, CancellationToken cancelacion)
    {
        var ids = marcaciones.Select(m => m.IdMarcador).Distinct().ToList();

        var asignaciones = await contexto.AsignacionesMarcador
            .AsNoTracking()
            .Where(a => ids.Contains(a.IdMarcador))
            .Select(a => new { a.IdMarcador, a.TrabajadorId, a.FechaInicio, a.FechaFin })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var atribuidas = new List<MarcacionAtribuida>(marcaciones.Count);
        var huerfanas = new Dictionary<string, List<MarcacionLeida>>(StringComparer.Ordinal);

        foreach (var marcacion in marcaciones)
        {
            var fecha = DateOnly.FromDateTime(marcacion.FechaHora);

            var asignacion = asignaciones.FirstOrDefault(a =>
                string.Equals(a.IdMarcador, marcacion.IdMarcador, StringComparison.Ordinal)
                && a.FechaInicio <= fecha
                && (a.FechaFin == null || a.FechaFin >= fecha));

            if (asignacion is null)
            {
                if (!huerfanas.TryGetValue(marcacion.IdMarcador, out var lista))
                {
                    lista = [];
                    huerfanas[marcacion.IdMarcador] = lista;
                }

                lista.Add(marcacion);
                continue;
            }

            atribuidas.Add(new MarcacionAtribuida(asignacion.TrabajadorId, marcacion));
        }

        var sinAsignar = huerfanas
            .OrderBy(par => par.Key, StringComparer.Ordinal)
            .Select(par => new SinAsignar(
                new IdMarcadorSinAsignar(
                    par.Key,
                    par.Value.Count,
                    DateOnly.FromDateTime(par.Value.Min(m => m.FechaHora)),
                    DateOnly.FromDateTime(par.Value.Max(m => m.FechaHora))),
                par.Value.Min(m => m.NumeroLinea)))
            .ToList();

        return (atribuidas, sinAsignar);
    }

    /// <summary>
    /// Descarta la segunda marca cuando el trabajador fichó dos veces seguidas.
    /// </summary>
    /// <remarks>
    /// En el archivo real hay casos con 9 a 19 segundos de diferencia: la persona pasa el
    /// dedo otra vez porque no vio la confirmación. Si esa marca se conservara, el motor
    /// la tomaría como la salida del día y el tareo saldría con cero minutos trabajados.
    /// </remarks>
    private List<MarcacionAtribuida> ColapsarDobleFichada(
        List<MarcacionAtribuida> marcaciones,
        out int descartadas)
    {
        var ventana = TimeSpan.FromSeconds(_opciones.VentanaDeduplicacionSegundos);
        var resultado = new List<MarcacionAtribuida>(marcaciones.Count);
        var descartadasLocal = 0;

        foreach (var grupo in marcaciones.GroupBy(m => m.TrabajadorId))
        {
            MarcacionAtribuida? anterior = null;

            foreach (var actual in grupo.OrderBy(m => m.Lectura.FechaHora))
            {
                if (anterior is not null
                    && actual.Lectura.FechaHora - anterior.Lectura.FechaHora <= ventana)
                {
                    descartadasLocal++;
                    continue;
                }

                resultado.Add(actual);
                anterior = actual;
            }
        }

        descartadas = descartadasLocal;

        return resultado;
    }

    /// <summary>
    /// Marcaciones que ya están en la base, para no reinsertarlas.
    /// </summary>
    /// <remarks>
    /// Esto es una optimización para no chocar contra el índice único fila por fila. El
    /// índice <c>uq_marcacion_cruda_trabajador_fecha_marcador</c> sigue siendo la defensa
    /// real: entre esta consulta y el INSERT hay una ventana, y dos cargas simultáneas
    /// solo las detiene la base.
    /// </remarks>
    private async Task<HashSet<(long, DateTime)>> ContarYaExistentesAsync(
        List<MarcacionAtribuida> marcaciones,
        CancellationToken cancelacion)
    {
        if (marcaciones.Count == 0)
        {
            return [];
        }

        var trabajadores = marcaciones.Select(m => m.TrabajadorId).Distinct().ToList();
        var desde = marcaciones.Min(m => m.Lectura.FechaHora).Date;
        var hasta = marcaciones.Max(m => m.Lectura.FechaHora).Date.AddDays(1);

        var existentes = await contexto.MarcacionesCrudas
            .AsNoTracking()
            .Where(m => trabajadores.Contains(m.TrabajadorId)
                        && m.FechaHora >= desde && m.FechaHora < hasta)
            .Select(m => new { m.TrabajadorId, m.FechaHora })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return [.. existentes.Select(e => (e.TrabajadorId, e.FechaHora))];
    }

    private static async Task<string> CalcularHashAsync(Stream contenido, CancellationToken cancelacion)
    {
        contenido.Position = 0;

        var bytes = await SHA256.HashDataAsync(contenido, cancelacion).ConfigureAwait(false);

        contenido.Position = 0;

        return Convert.ToHexString(bytes).ToLower(CultureInfo.InvariantCulture);
    }

    private static string Recortar(string valor, int maximo) =>
        valor.Length <= maximo ? valor : valor[..maximo];

    private sealed record MarcacionAtribuida(long TrabajadorId, MarcacionLeida Lectura);

    private sealed record SinAsignar(IdMarcadorSinAsignar Resumen, int PrimeraLinea);
}
