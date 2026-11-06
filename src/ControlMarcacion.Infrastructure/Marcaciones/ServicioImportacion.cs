using System.Globalization;
using System.Security.Cryptography;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
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
    IOptions<OpcionesMarcador> opciones,
    IServicioEquipos equipos) : IServicioImportacion
{
    private const string MotivoSinAsignar = "ID de marcador sin trabajador asignado";

    /// <summary>
    /// Días, contados hacia atrás desde la última marcación del archivo, que se comparan
    /// contra lo ya cargado de los demás aparatos.
    /// </summary>
    /// <remarks>
    /// El attlog acumula el historial del equipo: una descarga repite los días de la
    /// anterior, y esos días repetidos son los que delatan un archivo declarado con el
    /// aparato equivocado. El tramo reciente basta para encontrarlos, y acota el costo: un
    /// attlog de dos años no obliga a leer dos años de marcaciones de todos los equipos.
    /// </remarks>
    private const int DiasComparadosConOtrosEquipos = 92;

    /// <summary>
    /// Proporción de las marcaciones comparables que tienen que coincidir, en número y
    /// segundo exacto, con las de otro aparato para dar el archivo por suyo.
    /// </summary>
    /// <remarks>
    /// ⚠️ Es una proporción y no un conteo. En el cambio de turno, el 14 de A-L y el 14 de
    /// M-Z pueden marcar en el mismo segundo por azar algunas veces al mes; un archivo
    /// declarado con el equipo equivocado coincide en casi todo el tramo ya cargado.
    /// </remarks>
    private const double ProporcionCoincidenteConOtroEquipo = 0.5;

    /// <summary>Menos coincidencias que estas no prueban nada, sea cual sea la proporción.</summary>
    private const int MinimoCoincidentesConOtroEquipo = 5;

    private readonly OpcionesMarcador _opciones = opciones.Value;

    public async Task<ImportacionPrevia?> BuscarImportacionPreviaAsync(
        Stream contenido,
        CancellationToken cancelacion = default)
    {
        var hash = await CalcularHashAsync(contenido, cancelacion).ConfigureAwait(false);

        // La vigente primero: si el archivo se cargó en un aparato y luego se anuló para
        // cargarlo en otro, lo que importa es dónde cuenta hoy, no la carga más reciente.
        return await contexto.Importaciones
            .AsNoTracking()
            .Where(i => i.HashContenido == hash)
            .OrderBy(i => i.Anulada)
            .ThenByDescending(i => i.FechaCarga)
            .Select(i => new ImportacionPrevia(
                i.Id, i.NombreArchivo, i.EquipoId, i.Equipo!.Nombre, i.FechaCarga, i.CargadoPorUsuario,
                i.FilasInsertadas, i.Anulada))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<ResultadoImportacion> ImportarAsync(
        Stream contenido,
        string nombreArchivo,
        long plantaId,
        long equipoId,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        var equipo = await ValidarEquipoAsync(plantaId, equipoId, cancelacion).ConfigureAwait(false);

        var hash = await CalcularHashAsync(contenido, cancelacion).ConfigureAwait(false);

        contenido.Position = 0;
        var lectura = fuente.Leer(contenido);

        var (serie, serieDelContenido) = SerieDelArchivo.Resolver(lectura.SerieEquipo, nombreArchivo);
        var origen = await VerificarOrigenAsync(serie, serieDelContenido, equipo, cancelacion)
            .ConfigureAwait(false);

        // ⚠️ La deduplicación es por (equipo, número, instante), así que el mismo archivo
        // declarado con otro aparato entraría entero otra vez: cada marcación contaría dos
        // veces, a nombre de quien tenga ese número en cada equipo. No se admite ni con la
        // casilla de confirmación. Un archivo sin marcaciones no atribuye nada a nadie, y
        // dos archivos vacíos de aparatos distintos tienen el mismo hash.
        if (lectura.Marcaciones.Count > 0
            && await BuscarCargaVigenteEnOtroEquipoAsync(hash, equipoId, cancelacion).ConfigureAwait(false)
                is { } previa)
        {
            throw new ReglaNegocioException(
                $"Este archivo ya está cargado como marcaciones del equipo '{previa.Equipo}' " +
                $"(carga del {previa.FechaCarga:dd/MM/yyyy HH:mm}, por {previa.CargadoPorUsuario}). " +
                $"Cargarlo también como '{equipo.Nombre}' contaría cada marcación dos veces, a nombre " +
                "de personas distintas: el mismo número es alguien diferente en cada aparato. Si esa " +
                "carga se hizo con el equipo equivocado, anúlela en el historial y vuelva a cargar " +
                "el archivo.");
        }

        var deduplicadas = ColapsarDobleFichada(lectura.Marcaciones, out var descartadasEnArchivo);

        // La misma pregunta sin depender del nombre ni del hash: una descarga posterior del
        // mismo aparato trae otro contenido, y un archivo renombrado no trae serie.
        if (await BuscarCoincidenciaConOtroEquipoAsync(
                    deduplicadas.ConvertAll(m => (m.IdMarcador, m.FechaHora)), equipoId, cancelacion)
                .ConfigureAwait(false) is { } coincidencia)
        {
            throw new ReglaNegocioException(
                $"{coincidencia.Coincidentes} de las {coincidencia.Comparadas} marcaciones de este archivo " +
                $"entre el {coincidencia.Desde:dd/MM/yyyy} y el {coincidencia.Hasta:dd/MM/yyyy} ya están " +
                $"cargadas en el equipo '{coincidencia.Equipo}', con el mismo número y al segundo exacto. " +
                $"Dos aparatos distintos no registran eso: el archivo es de '{coincidencia.Equipo}', no de " +
                $"'{equipo.Nombre}'. Cárguelo eligiendo '{coincidencia.Equipo}'; si la carga equivocada es " +
                "la que ya estaba, anúlela y vuelva a cargar.");
        }

        var importacion = new ImportacionMarcador
        {
            NombreArchivo = nombreArchivo,
            EquipoId = equipoId,
            HashContenido = hash,
            SerieArchivo = serie,
            OrigenVerificado = origen,
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

        // Todas las marcaciones se guardan, tengan o no dueño hoy: quién era cada número se
        // decide al calcular (AtribucionMarcaciones). Aquí solo se averigua cuáles llegan
        // sin dueño para avisarlo en el resultado de la carga.
        var sinAsignar = await DetectarSinAsignarAsync(lectura.Marcaciones, equipoId, cancelacion)
            .ConfigureAwait(false);

        foreach (var id in sinAsignar)
        {
            importacion.Errores.Add(new ErrorImportacion
            {
                NumeroLinea = id.PrimeraLinea,
                // El resumen se guarda ADEMÁS en forma estructurada, separado por
                // tabuladores. El motivo de al lado lo dice en prosa para quien lee la
                // fila suelta, pero reconstruir el conteo y el rango a partir de esa frase
                // exigiría analizar un texto en español: la pantalla de resultado los
                // mostraba en blanco justamente porque no había de dónde sacarlos.
                ContenidoLinea = Recortar(
                    string.Join(
                        '\t',
                        $"ID {id.Resumen.IdMarcador}",
                        id.Resumen.Marcaciones.ToString(CultureInfo.InvariantCulture),
                        id.Resumen.PrimeraFecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        id.Resumen.UltimaFecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    500),
                Motivo = Recortar(
                    $"{MotivoSinAsignar}: '{id.Resumen.IdMarcador}' aparece {id.Resumen.Marcaciones} vez/veces " +
                    $"entre el {id.Resumen.PrimeraFecha:dd/MM/yyyy} y el {id.Resumen.UltimaFecha:dd/MM/yyyy} " +
                    "y ningún trabajador lo tenía asignado en esas fechas. Las marcaciones se guardaron: " +
                    "cuentan en cuanto se asigne el ID a un trabajador y se recalcule el periodo.", 300),
                CreadoPor = usuario
            });
        }

        var yaExistentes = await ContarYaExistentesAsync(deduplicadas, equipoId, cancelacion)
            .ConfigureAwait(false);

        var nuevas = deduplicadas
            .Where(m => !yaExistentes.Contains((m.IdMarcador, m.FechaHora)))
            .ToList();

        importacion.FilasInsertadas = nuevas.Count;
        importacion.FilasDuplicadas = descartadasEnArchivo + (deduplicadas.Count - nuevas.Count);

        // Solo las líneas ilegibles. Las de un ID sin dueño ya no se rechazan: se guardan.
        importacion.FilasRechazadas = lectura.Rechazadas.Count;

        // Todo lo costoso y puramente lector quedó arriba: archivo, hash, parsing, origen,
        // asignaciones y deduplicación. El write lock de SQLite empieza recién cuando la
        // unidad está lista para persistirse. Los tests unitarios que usan InMemory no
        // abren una transacción ficticia; la garantía se prueba sobre SQLite real.
        await using var transaccion = contexto.Database.IsRelational()
            ? await contexto.Database.BeginTransactionAsync(cancelacion).ConfigureAwait(false)
            : null;

        try
        {
            // Solo se aprende cuando todas las validaciones pasaron, pero forma parte de la
            // misma operación persistente: si después fallan las marcas, tampoco debe quedar
            // una serie aprendida por una importación que no existe.
            if (origen == OrigenCarga.SerieAprendida)
            {
                await equipos.RegistrarSerieAsync(equipoId, serie!, usuario, cancelacion)
                    .ConfigureAwait(false);
            }

            await contexto.Importaciones.AddAsync(importacion, cancelacion).ConfigureAwait(false);
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

            foreach (var marcacion in nuevas)
            {
                await contexto.MarcacionesCrudas.AddAsync(new MarcacionCruda
                {
                    EquipoId = equipoId,
                    IdMarcador = marcacion.IdMarcador,
                    FechaHora = marcacion.FechaHora,
                    ImportacionId = importacion.Id,
                    ModoVerificacion = marcacion.ModoVerificacion,
                    TipoMarcacion = marcacion.TipoMarcacion,
                    LineaOriginal = Recortar(marcacion.LineaOriginal, 500),
                    CreadoPor = usuario
                }, cancelacion).ConfigureAwait(false);
            }

            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

            if (transaccion is not null)
            {
                await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);
            }
        }
        catch
        {
            if (transaccion is not null)
            {
                await transaccion.RollbackAsync(cancelacion).ConfigureAwait(false);
            }

            throw;
        }

        return new ResultadoImportacion(
            importacion.Id,
            importacion.NombreArchivo,
            equipo.Nombre,
            importacion.FilasLeidas,
            importacion.FilasInsertadas,
            importacion.FilasDuplicadas,
            importacion.FilasRechazadas,
            lectura.Rechazadas,
            [.. sinAsignar.Select(s => s.Resumen)],
            origen,
            serie,
            lectura.EventosEquipoIgnorados.Count);
    }

    public async Task<IReadOnlyList<ImportacionHistorialDto>> HistorialAsync(
        int maximo = 50,
        CancellationToken cancelacion = default)
    {
        var importaciones = await contexto.Importaciones
            .AsNoTracking()
            .OrderByDescending(i => i.FechaCarga)
            .Take(maximo)
            .Select(i => new ImportacionHistorialDto(
                i.Id, i.NombreArchivo, i.Equipo!.Nombre, i.FechaCarga, i.CargadoPorUsuario,
                i.FilasLeidas, i.FilasInsertadas, i.FilasDuplicadas, i.FilasRechazadas,
                i.Anulada, i.MotivoAnulacion, i.AnuladaEn, i.AnuladaPorUsuario,
                i.OrigenVerificado, i.SerieArchivo))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        importaciones = importaciones.ConvertAll(i => i with
        {
            FilasIgnoradas = CalcularFilasIgnoradas(
                i.FilasLeidas, i.FilasInsertadas, i.FilasDuplicadas, i.FilasRechazadas)
        });

        if (importaciones.Count == 0)
        {
            return importaciones;
        }

        // El rango de días que trae cada archivo no está guardado en ImportacionMarcador y
        // no debe estarlo: es un dato derivado de sus marcaciones, y duplicarlo abriría la
        // puerta a que quedara desfasado. Se agrega sobre el índice (ImportacionId,
        // FechaHora), que resuelve el MIN/MAX sin tocar la tabla.
        var ids = importaciones.ConvertAll(i => i.Id);

        var cobertura = await contexto.MarcacionesCrudas
            .AsNoTracking()
            .Where(m => ids.Contains(m.ImportacionId))
            .GroupBy(m => m.ImportacionId)
            .Select(g => new
            {
                ImportacionId = g.Key,
                Primera = g.Min(m => m.FechaHora),
                Ultima = g.Max(m => m.FechaHora)
            })
            .ToDictionaryAsync(x => x.ImportacionId, cancelacion)
            .ConfigureAwait(false);

        return importaciones.ConvertAll(i => cobertura.TryGetValue(i.Id, out var rango)
            ? i with
            {
                PrimeraMarcacion = DateOnly.FromDateTime(rango.Primera),
                UltimaMarcacion = DateOnly.FromDateTime(rango.Ultima)
            }
            : i);
    }

    public async Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<long>>> AparatosSinCargaAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion = default)
    {
        var faltantes = new Dictionary<DateOnly, IReadOnlyList<long>>();

        if (hasta < desde)
        {
            return faltantes;
        }

        var anuladas = contexto.Importaciones.Where(i => i.Anulada).Select(i => i.Id);

        var vigentes = contexto.MarcacionesCrudas
            .AsNoTracking()
            .Where(m => !anuladas.Contains(m.ImportacionId));

        var inicio = desde.ToDateTime(TimeOnly.MinValue);
        var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var porDiaYEquipo = await vigentes
            .Where(m => m.FechaHora >= inicio && m.FechaHora < fin)
            .GroupBy(m => new { m.FechaHora.Year, m.FechaHora.Month, m.FechaHora.Day, m.EquipoId })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.EquipoId })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var equiposPorDia = porDiaYEquipo
            .GroupBy(d => new DateOnly(d.Year, d.Month, d.Day))
            .ToDictionary(g => g.Key, g => g.Select(d => d.EquipoId).ToArray());

        var esperados = (await EquiposEnCoberturaAsync(vigentes, cancelacion).ConfigureAwait(false))
            .ConvertAll(e => e.Esperado);

        // Un día futuro no tiene nada que faltar, y CoberturaDeCarga ya lo resuelve: se le
        // pasa el mismo «hoy» que usa el calendario para que las dos pantallas coincidan.
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        for (var fecha = desde; fecha <= hasta; fecha = fecha.AddDays(1))
        {
            var delDia = equiposPorDia.TryGetValue(fecha, out var ids) ? ids : [];
            var sinCargar = CoberturaDeCarga.Faltantes(fecha, hoy, esperados, delDia).ToArray();

            if (sinCargar.Length > 0)
            {
                faltantes[fecha] = sinCargar;
            }
        }

        return faltantes;
    }

    public async Task<CoberturaDeRango> CoberturaDeRangoAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion = default)
    {
        if (hasta < desde)
        {
            return CoberturaDeRango.Vacia(desde, hasta);
        }

        // Mismo criterio que el calendario y que el motor: una carga anulada conserva sus
        // filas pero no cuenta para nada que alimente el tareo.
        var anuladas = contexto.Importaciones.Where(i => i.Anulada).Select(i => i.Id);

        // El rango se compara con DateTime porque MarcacionCruda guarda fecha y hora: el
        // límite superior es el día siguiente EXCLUSIVO, no "hasta las 00:00 del último
        // día", que dejaría fuera la jornada entera de la fecha final.
        var inicio = desde.ToDateTime(TimeOnly.MinValue);
        var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var vigentes = contexto.MarcacionesCrudas
            .AsNoTracking()
            .Where(m => !anuladas.Contains(m.ImportacionId));

        // Por día Y por aparato: con dos aparatos en la planta, un día con las marcaciones
        // de uno solo parece cargado y no lo está.
        var porDiaYEquipo = await vigentes
            .Where(m => m.FechaHora >= inicio && m.FechaHora < fin)
            .GroupBy(m => new { m.FechaHora.Year, m.FechaHora.Month, m.FechaHora.Day, m.EquipoId })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.EquipoId, Marcaciones = g.Count() })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var porDia = porDiaYEquipo
            .GroupBy(d => new { d.Year, d.Month, d.Day })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                Marcaciones = g.Sum(d => d.Marcaciones),
                Equipos = g.Select(d => d.EquipoId).ToArray()
            })
            .ToList();

        // Los días futuros no son huecos: en un periodo en curso, media tabla está por
        // ocurrir. El corte se hace aquí y no en la vista porque decidir qué cuenta como
        // faltante es una regla, no presentación.
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var ultimoTranscurrido = hasta < hoy ? hasta : hoy;

        var transcurridos = ultimoTranscurrido < desde
            ? 0
            : ultimoTranscurrido.DayNumber - desde.DayNumber + 1;

        // Los días con marcación se cuentan solo dentro del tramo ya transcurrido, que es
        // contra el que se restan. Mezclar los dos alcances haría que una marcación con
        // fecha futura —reloj del equipo mal puesto— tapara un hueco real del pasado.
        var conMarcacionTranscurridos = porDia
            .Count(d => new DateOnly(d.Year, d.Month, d.Day) <= ultimoTranscurrido);

        // La misma regla que el calendario (CoberturaDeCarga), para que la portada no diga
        // «todo cargado» sobre un mes que el calendario marca incompleto.
        var equipos = await EquiposEnCoberturaAsync(vigentes, cancelacion).ConfigureAwait(false);
        var esperados = equipos.ConvertAll(e => e.Esperado);
        var equiposPorDia = porDia.ToDictionary(d => new DateOnly(d.Year, d.Month, d.Day), d => d.Equipos);

        var diasIncompletos = 0;
        var conFaltantes = new HashSet<long>();

        for (var fecha = desde; fecha <= ultimoTranscurrido; fecha = fecha.AddDays(1))
        {
            var delDia = equiposPorDia.TryGetValue(fecha, out var ids) ? ids : [];

            if (CoberturaDeCarga.Evaluar(fecha, hoy, esperados, delDia) == EstadoCobertura.Incompleto)
            {
                diasIncompletos++;
            }

            conFaltantes.UnionWith(CoberturaDeCarga.Faltantes(fecha, hoy, esperados, delDia));
        }

        return new CoberturaDeRango(
            desde,
            hasta,
            porDia.Sum(d => d.Marcaciones),
            conMarcacionTranscurridos,
            transcurridos,
            diasIncompletos,
            [.. equipos.Where(e => conFaltantes.Contains(e.EquipoId)).Select(e => e.Nombre).Order(StringComparer.CurrentCulture)]);
    }

    public async Task<CoberturaMarcaciones> CoberturaAsync(
        int anio,
        int mes,
        CancellationToken cancelacion = default)
    {
        // Mismo criterio que el motor de cálculo: las marcaciones de una carga anulada
        // existen y se conservan, pero no cuentan. Si aparecieran aquí, esta pantalla
        // diría que julio está cubierto sobre un archivo que el tareo ignora.
        var anuladas = contexto.Importaciones.Where(i => i.Anulada).Select(i => i.Id);

        var vigentes = contexto.MarcacionesCrudas
            .AsNoTracking()
            .Where(m => !anuladas.Contains(m.ImportacionId));

        // Dos agregados sobre el índice de FechaHora, no un recorrido de la tabla. Se
        // enumeran también los años intermedios sin datos: un año vacío entre dos con
        // marcaciones es información, no ruido.
        var primera = await vigentes
            .Select(m => (DateTime?)m.FechaHora)
            .MinAsync(cancelacion)
            .ConfigureAwait(false);

        var ultima = await vigentes
            .Select(m => (DateTime?)m.FechaHora)
            .MaxAsync(cancelacion)
            .ConfigureAwait(false);

        var anios = primera is { } desde && ultima is { } hasta
            ? Enumerable.Range(desde.Year, hasta.Year - desde.Year + 1).ToList()
            : [];

        var inicioAnio = new DateTime(anio, 1, 1);
        var finAnio = inicioAnio.AddYears(1);

        // Un solo barrido del año que resuelve a la vez las doce pastillas de mes y el
        // calendario del mes elegido: pedir los meses por un lado y los días por otro
        // serían dos recorridos del mismo rango para el mismo dato.
        //
        // Las personas se cuentan como números distintos POR APARATO y luego se suman: el
        // 14 del A-L y el 14 del M-Z son dos personas. La marcación ya no guarda el
        // trabajador (se atribuye al calcular), y un número del aparato es una persona.
        var porDiaYEquipo = await vigentes
            .Where(m => m.FechaHora >= inicioAnio && m.FechaHora < finAnio)
            .GroupBy(m => new { m.FechaHora.Month, m.FechaHora.Day, m.EquipoId })
            .Select(g => new
            {
                g.Key.Month,
                g.Key.Day,
                g.Key.EquipoId,
                Marcaciones = g.Count(),
                Personas = g.Select(m => m.IdMarcador).Distinct().Count()
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        // El desglose por aparato del mismo barrido: es lo que permite decir que un día con
        // marcaciones del A-L está incompleto porque falta el archivo del M-Z.
        var porDia = porDiaYEquipo
            .GroupBy(d => new DateOnly(anio, d.Month, d.Day))
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<MarcacionesDeEquipo>)[.. g.Select(d => new MarcacionesDeEquipo(d.EquipoId, d.Marcaciones, d.Personas))]);

        var equipos = await EquiposEnCoberturaAsync(vigentes, cancelacion).ConfigureAwait(false);
        var esperados = equipos.ConvertAll(e => e.Esperado);
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        DiaConMarcaciones DiaDe(DateOnly fecha) =>
            porDia.TryGetValue(fecha, out var deCadaEquipo)
                ? new DiaConMarcaciones(
                    fecha, deCadaEquipo.Sum(e => e.Marcaciones), deCadaEquipo.Sum(e => e.Personas), deCadaEquipo)
                : new DiaConMarcaciones(fecha, 0, 0, []);

        IEnumerable<DiaConMarcaciones> DiasDelMes(int numero) =>
            Enumerable.Range(1, DateTime.DaysInMonth(anio, numero))
                .Select(dia => DiaDe(new DateOnly(anio, numero, dia)));

        var meses = Enumerable.Range(1, 12)
            .Select(numero =>
            {
                var delMes = DiasDelMes(numero).ToList();

                return new MesConMarcaciones(
                    anio,
                    numero,
                    delMes.Sum(d => d.Marcaciones),
                    delMes.Count(d => d.TieneMarcaciones),
                    delMes.Count(d => CoberturaDeCarga.Evaluar(
                            d.Fecha, hoy, esperados, [.. d.PorEquipo.Select(e => e.EquipoId)])
                        == EstadoCobertura.Incompleto));
            })
            .ToList();

        return new CoberturaMarcaciones(anio, mes, anios, meses, [.. DiasDelMes(mes)], equipos);
    }

    /// <summary>
    /// Todos los aparatos, con desde cuándo se espera cada uno: su primera marcación vigente
    /// o, si no tiene ninguna, su alta (<see cref="AparatoEsperado.Para"/>).
    /// </summary>
    private async Task<List<EquipoEnCobertura>> EquiposEnCoberturaAsync(
        IQueryable<MarcacionCruda> vigentes,
        CancellationToken cancelacion)
    {
        var primeras = await vigentes
            .GroupBy(m => m.EquipoId)
            .Select(g => new { EquipoId = g.Key, Primera = g.Min(m => m.FechaHora) })
            .ToDictionaryAsync(x => x.EquipoId, x => DateOnly.FromDateTime(x.Primera), cancelacion)
            .ConfigureAwait(false);

        var equipos = await contexto.EquiposMarcador
            .AsNoTracking()
            .Select(e => new { e.Id, e.Codigo, e.Nombre, Planta = e.Planta!.Nombre, e.Activo, e.CreadoEn })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return equipos.ConvertAll(e =>
        {
            var esperado = AparatoEsperado.Para(
                e.Id,
                e.Activo,
                primeras.TryGetValue(e.Id, out var primera) ? primera : null,
                DateOnly.FromDateTime(e.CreadoEn));

            return new EquipoEnCobertura(e.Id, e.Codigo, e.Nombre, e.Planta, e.Activo, esperado.Desde);
        });
    }

    public async Task AnularAsync(
        long importacionId,
        string motivo,
        string usuario,
        CancellationToken cancelacion = default)
    {
        var importacion = await contexto.Importaciones
            .FirstOrDefaultAsync(i => i.Id == importacionId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("la carga de marcaciones", importacionId);

        // DateTime.Now y no una fecha inyectada: es el sello de una acción humana que
        // acaba de ocurrir, no un dato de negocio que haya que poder recalcular.
        importacion.Anular(motivo, usuario, DateTime.Now);

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    public async Task ReactivarAsync(
        long importacionId,
        string usuario,
        CancellationToken cancelacion = default)
    {
        var importacion = await contexto.Importaciones
            .FirstOrDefaultAsync(i => i.Id == importacionId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("la carga de marcaciones", importacionId);

        // La misma regla que al cargar, por la otra puerta: anular la carga equivocada,
        // cargar el archivo en el aparato correcto y después reactivar la primera dejaría
        // las dos vigentes.
        if (importacion.Anulada
            && importacion.FilasInsertadas > 0
            && await BuscarCargaVigenteEnOtroEquipoAsync(importacion.HashContenido, importacion.EquipoId, cancelacion)
                .ConfigureAwait(false) is { } vigente)
        {
            throw new ReglaNegocioException(
                $"No se puede reactivar: el mismo archivo está vigente como marcaciones del equipo " +
                $"'{vigente.Equipo}' (carga del {vigente.FechaCarga:dd/MM/yyyy HH:mm}, por " +
                $"{vigente.CargadoPorUsuario}). Con las dos cargas activas, cada marcación contaría " +
                "dos veces, a nombre de personas distintas. Si la correcta es esta, anule primero " +
                "aquella.");
        }

        if (importacion.Anulada)
        {
            var propias = await contexto.MarcacionesCrudas
                .AsNoTracking()
                .Where(m => m.ImportacionId == importacion.Id)
                .Select(m => new { m.IdMarcador, m.FechaHora })
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);

            if (await BuscarCoincidenciaConOtroEquipoAsync(
                        propias.ConvertAll(m => (m.IdMarcador, m.FechaHora)), importacion.EquipoId, cancelacion)
                    .ConfigureAwait(false) is { } coincidencia)
            {
                throw new ReglaNegocioException(
                    $"No se puede reactivar: {coincidencia.Coincidentes} de sus {coincidencia.Comparadas} " +
                    $"marcaciones entre el {coincidencia.Desde:dd/MM/yyyy} y el {coincidencia.Hasta:dd/MM/yyyy} " +
                    $"ya están vigentes en el equipo '{coincidencia.Equipo}', con el mismo número y al " +
                    "segundo exacto. Con las dos cargas activas, cada marcación contaría dos veces, a " +
                    "nombre de personas distintas. Si la correcta es esta, anule primero aquella.");
            }
        }

        importacion.Reactivar(usuario);

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    public async Task<ResultadoImportacion?> ObtenerResultadoAsync(
        long importacionId,
        CancellationToken cancelacion = default)
    {
        var importacion = await contexto.Importaciones
            .AsNoTracking()
            .Include(i => i.Errores)
            .Include(i => i.Equipo)
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
            .Select(e => LeerSinAsignar(e.ContenidoLinea))
            .ToList();

        return new ResultadoImportacion(
            importacion.Id, importacion.NombreArchivo, importacion.Equipo?.Nombre ?? "—", importacion.FilasLeidas,
            importacion.FilasInsertadas, importacion.FilasDuplicadas, importacion.FilasRechazadas,
            rechazadas, sinAsignar, importacion.OrigenVerificado, importacion.SerieArchivo,
            CalcularFilasIgnoradas(
                importacion.FilasLeidas,
                importacion.FilasInsertadas,
                importacion.FilasDuplicadas,
                importacion.FilasRechazadas));
    }

    /// <summary>
    /// Los eventos ignorados no necesitan una columna: son la única categoría física que
    /// no termina aceptada (insertada o duplicada) ni rechazada. Esta identidad conserva
    /// el contador histórico sin persistir datos de eventos ni crear una migración.
    /// </summary>
    private static int CalcularFilasIgnoradas(
        int leidas,
        int insertadas,
        int duplicadas,
        int rechazadas) =>
        Math.Max(leidas - insertadas - duplicadas - rechazadas, 0);

    public async Task<IReadOnlyList<NumeroSinAsignarDto>> NumerosSinAsignarAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion = default)
    {
        if (hasta < desde)
        {
            return [];
        }

        var anuladas = contexto.Importaciones.Where(i => i.Anulada).Select(i => i.Id);
        var inicio = desde.ToDateTime(TimeOnly.MinValue);
        var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);

        // Agregado por (aparato, número, día) en la base: un mes son miles de marcaciones
        // pero unos pocos miles de combinaciones, y la vigencia de una asignación se decide
        // por día, así que no hace falta traer cada hora.
        var porDia = await contexto.MarcacionesCrudas
            .AsNoTracking()
            .Where(m => !anuladas.Contains(m.ImportacionId) && m.FechaHora >= inicio && m.FechaHora < fin)
            .GroupBy(m => new { m.EquipoId, m.IdMarcador, m.FechaHora.Date })
            .Select(g => new { g.Key.EquipoId, g.Key.IdMarcador, Fecha = g.Key.Date, Marcaciones = g.Count() })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        if (porDia.Count == 0)
        {
            return [];
        }

        var equipos = porDia.Select(d => d.EquipoId).Distinct().ToList();
        var numeros = porDia.Select(d => d.IdMarcador).Distinct().ToList();

        var vigencias = await contexto.AsignacionesMarcador
            .AsNoTracking()
            .Where(a => equipos.Contains(a.EquipoId) && numeros.Contains(a.IdMarcador))
            .Select(a => new VigenciaIdMarcador(a.TrabajadorId, a.EquipoId, a.IdMarcador, a.FechaInicio, a.FechaFin))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var nombres = await contexto.EquiposMarcador
            .AsNoTracking()
            .Where(e => equipos.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Nombre, cancelacion)
            .ConfigureAwait(false);

        var atribucion = new AtribucionMarcaciones(vigencias);

        return porDia
            .Where(d => atribucion.Resolver(d.EquipoId, d.IdMarcador, d.Fecha) is null)
            .GroupBy(d => new { d.EquipoId, d.IdMarcador })
            .Select(g => new NumeroSinAsignarDto(
                g.Key.EquipoId,
                nombres.GetValueOrDefault(g.Key.EquipoId, "—"),
                g.Key.IdMarcador,
                g.Sum(d => d.Marcaciones),
                g.Count(),
                DateOnly.FromDateTime(g.Min(d => d.Fecha)),
                DateOnly.FromDateTime(g.Max(d => d.Fecha))))
            // Orden natural del número (2 antes que 10), sin perder los ceros a la izquierda.
            .OrderBy(n => n.Equipo, StringComparer.Ordinal)
            .ThenBy(n => n.IdMarcador.TrimStart('0').Length)
            .ThenBy(n => n.IdMarcador, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Reconstruye el resumen de un ID sin asignar a partir de lo guardado.
    /// </summary>
    /// <remarks>
    /// Tolera el formato antiguo, que guardaba solo <c>"ID xxx"</c>: esas importaciones
    /// siguen mostrando el identificador, pero con el conteo y el rango en blanco, porque
    /// ese dato no llegó a escribirse y no se puede inventar.
    /// </remarks>
    private static IdMarcadorSinAsignar LeerSinAsignar(string? contenido)
    {
        var partes = (contenido ?? string.Empty).Split('\t');

        var id = partes[0].Replace("ID ", string.Empty, StringComparison.Ordinal).Trim();

        if (partes.Length < 4
            || !int.TryParse(partes[1], CultureInfo.InvariantCulture, out var marcaciones)
            || !DateOnly.TryParse(partes[2], CultureInfo.InvariantCulture, out var primera)
            || !DateOnly.TryParse(partes[3], CultureInfo.InvariantCulture, out var ultima))
        {
            return new IdMarcadorSinAsignar(id, 0, default, default);
        }

        return new IdMarcadorSinAsignar(id, marcaciones, primera, ultima);
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Números del archivo que ningún trabajador tenía asignados en la fecha en que
    /// marcaron, resumidos por número.
    /// </summary>
    /// <remarks>
    /// Es solo un aviso: las marcaciones se guardan igual y cuentan en cuanto alguien
    /// tenga ese número asignado y se recalcule. La regla es la misma que aplica el motor
    /// (<see cref="AtribucionMarcaciones"/>), para que la carga no diga «sin dueño» de algo
    /// que el tareo sí atribuye.
    ///
    /// ⚠️ El filtro por equipo no es una optimización, es parte de la identidad. Dos
    /// aparatos de la misma planta numeran a su gente desde el 1: sin acotar al equipo del
    /// que salió el archivo, el usuario 14 del A-L se resolvería al trabajador que tiene
    /// el 14 en el M-Z.
    /// </remarks>
    private async Task<List<SinAsignar>> DetectarSinAsignarAsync(
        IReadOnlyList<MarcacionLeida> marcaciones,
        long equipoId,
        CancellationToken cancelacion)
    {
        var ids = marcaciones.Select(m => m.IdMarcador).Distinct().ToList();

        var vigencias = await contexto.AsignacionesMarcador
            .AsNoTracking()
            .Where(a => a.EquipoId == equipoId && ids.Contains(a.IdMarcador))
            .Select(a => new VigenciaIdMarcador(a.TrabajadorId, a.EquipoId, a.IdMarcador, a.FechaInicio, a.FechaFin))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var atribucion = new AtribucionMarcaciones(vigencias);

        return marcaciones
            .Where(m => atribucion.Resolver(equipoId, m.IdMarcador, m.FechaHora) is null)
            .GroupBy(m => m.IdMarcador, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new SinAsignar(
                new IdMarcadorSinAsignar(
                    g.Key,
                    g.Count(),
                    DateOnly.FromDateTime(g.Min(m => m.FechaHora)),
                    DateOnly.FromDateTime(g.Max(m => m.FechaHora))),
                g.Min(m => m.NumeroLinea)))
            .ToList();
    }

    /// <summary>
    /// Descarta la segunda marca cuando la misma persona fichó dos veces seguidas.
    /// </summary>
    /// <remarks>
    /// En el archivo real hay casos con 9 a 19 segundos de diferencia: la persona pasa el
    /// dedo otra vez porque no vio la confirmación. Si esa marca se conservara, el motor
    /// la tomaría como la salida del día y el tareo saldría con cero minutos trabajados.
    ///
    /// Se agrupa por <b>número del aparato</b>: todo el archivo sale de un solo equipo, y
    /// dentro de él un número es una sola persona a la vez. Ya no se agrupa por trabajador
    /// porque la marcación se guarda antes de saber de quién es.
    /// </remarks>
    private List<MarcacionLeida> ColapsarDobleFichada(
        IReadOnlyList<MarcacionLeida> marcaciones,
        out int descartadas)
    {
        var ventana = TimeSpan.FromSeconds(_opciones.VentanaDeduplicacionSegundos);
        var resultado = new List<MarcacionLeida>(marcaciones.Count);
        var descartadasLocal = 0;

        foreach (var grupo in marcaciones.GroupBy(m => m.IdMarcador, StringComparer.Ordinal))
        {
            MarcacionLeida? anterior = null;

            foreach (var actual in grupo.OrderBy(m => m.FechaHora))
            {
                if (anterior is not null && actual.FechaHora - anterior.FechaHora <= ventana)
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
    /// Qué marcaciones de este equipo y este rango ya están guardadas, para no
    /// reinsertarlas.
    /// </summary>
    /// <remarks>
    /// La clave es <b>(equipo, código, instante)</b>, la misma del índice único que
    /// protege la tabla, que sigue siendo la defensa real: entre esta consulta y el INSERT
    /// hay una ventana, y dos cargas simultáneas solo las detiene la base. Esto es lo que
    /// permite volver a subir un archivo sin duplicar nada.
    /// </remarks>
    private async Task<HashSet<(string, DateTime)>> ContarYaExistentesAsync(
        List<MarcacionLeida> marcaciones,
        long equipoId,
        CancellationToken cancelacion)
    {
        if (marcaciones.Count == 0)
        {
            return [];
        }

        var ids = marcaciones.Select(m => m.IdMarcador).Distinct().ToList();
        var desde = marcaciones.Min(m => m.FechaHora).Date;
        var hasta = marcaciones.Max(m => m.FechaHora).Date.AddDays(1);

        var existentes = await contexto.MarcacionesCrudas
            .AsNoTracking()
            .Where(m => m.EquipoId == equipoId
                        && ids.Contains(m.IdMarcador)
                        && m.FechaHora >= desde && m.FechaHora < hasta)
            .Select(m => new { m.IdMarcador, m.FechaHora })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return [.. existentes.Select(e => (e.IdMarcador, e.FechaHora))];
    }

    /// <summary>
    /// Comprueba que el equipo declarado existe, está activo y es de la planta indicada.
    /// </summary>
    /// <remarks>
    /// Pedir planta y equipo por separado es redundante —el equipo ya sabe su planta— y
    /// esa redundancia es justamente el control: si quien carga elige la planta equivocada,
    /// el sistema lo dice en vez de atribuir el archivo a la gente de otra planta.
    /// </remarks>
    private async Task<EquipoMarcador> ValidarEquipoAsync(
        long plantaId,
        long equipoId,
        CancellationToken cancelacion)
    {
        var equipo = await contexto.EquiposMarcador
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == equipoId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new ReglaNegocioException(
                "El marcador indicado no existe. Regístrelo en Catálogos → Marcadores.");

        if (!equipo.Activo)
        {
            throw new ReglaNegocioException(
                $"El marcador '{equipo.Nombre}' está dado de baja y no admite cargas.");
        }

        if (equipo.PlantaId != plantaId)
        {
            throw new ReglaNegocioException(
                $"El marcador '{equipo.Nombre}' no pertenece a la unidad indicada. " +
                "Verifique la unidad antes de cargar: el archivo se atribuiría al personal " +
                "equivocado.");
        }

        return equipo;
    }

    /// <summary>
    /// Contrasta la serie del archivo con la del equipo declarado y con las de los demás.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cargar el archivo del marcador A-L declarando el M-Z atribuye media planta a las
    /// personas equivocadas, y nada en el resultado lo delata: todos los ZKTeco numeran desde
    /// el 1, así que cada marcación encuentra dueño.
    /// </para>
    /// <para>
    /// La serie se <b>aprende</b> del primer archivo de cada equipo en vez de exigir que
    /// alguien la teclee, porque no hay acceso físico a los aparatos. El riesgo conocido es
    /// que ese primer archivo se suba al equipo equivocado y se aprenda la serie equivocada:
    /// el cruce contra los demás equipos hace que el error salga en la siguiente carga de
    /// cualquiera de los dos aparatos, en vez de no salir nunca. Esa carga queda marcada
    /// como <see cref="OrigenCarga.SerieAprendida"/> para encontrarla.
    /// </para>
    /// </remarks>
    private async Task<OrigenCarga> VerificarOrigenAsync(
        string? serie,
        bool serieDelContenido,
        EquipoMarcador equipo,
        CancellationToken cancelacion)
    {
        if (serie is null)
        {
            return OrigenCarga.NoVerificado;
        }

        if (equipo.Serie is not null && string.Equals(equipo.Serie.Trim(), serie, StringComparison.OrdinalIgnoreCase))
        {
            return serieDelContenido ? OrigenCarga.PorContenido : OrigenCarga.PorNombre;
        }

        // En memoria para no depender de si SQLite distingue mayúsculas: son unos pocos
        // equipos. Incluye los dados de baja: la serie sigue siendo de ese aparato.
        var duenio = (await contexto.EquiposMarcador
                .AsNoTracking()
                .Where(e => e.Serie != null && e.Id != equipo.Id)
                .Select(e => new { e.Nombre, e.Serie })
                .ToListAsync(cancelacion)
                .ConfigureAwait(false))
            .FirstOrDefault(e => string.Equals(e.Serie!.Trim(), serie, StringComparison.OrdinalIgnoreCase));

        if (duenio is not null)
        {
            throw new ReglaNegocioException(
                $"El archivo lo generó el equipo '{duenio.Nombre}' (serie '{serie}'), pero se declaró " +
                $"'{equipo.Nombre}'. Cárguelo eligiendo '{duenio.Nombre}': con otro equipo, cada número " +
                "se atribuiría a otra persona.");
        }

        if (equipo.Serie is not null)
        {
            throw new ReglaNegocioException(
                $"El archivo lo generó el equipo con serie '{serie}', pero se declaró '{equipo.Nombre}' " +
                $"(serie '{equipo.Serie}'). Elija el marcador correcto; si la serie registrada en " +
                $"'{equipo.Nombre}' está mal, corríjala en Equipos.");
        }

        return OrigenCarga.SerieAprendida;
    }

    /// <summary>
    /// Otro aparato que ya tiene cargadas, con el mismo número y al segundo exacto, la
    /// mayoría de estas marcaciones, si lo hay.
    /// </summary>
    /// <remarks>
    /// Dos aparatos distintos no registran el mismo número en el mismo segundo salvo por
    /// azar, y el azar no pasa de unos pocos casos al mes. Solo se comparan los días en que
    /// el otro aparato tiene marcaciones: si su archivo cubría medio mes y este el mes
    /// entero, contra el mes entero coincidiría la mitad aunque el archivo fuera suyo. Las
    /// cargas anuladas no cuentan: anular la equivocada es el camino para corregirla.
    /// </remarks>
    private async Task<CoincidenciaConOtroEquipo?> BuscarCoincidenciaConOtroEquipoAsync(
        List<(string IdMarcador, DateTime FechaHora)> marcaciones,
        long equipoId,
        CancellationToken cancelacion)
    {
        if (marcaciones.Count < MinimoCoincidentesConOtroEquipo)
        {
            return null;
        }

        var hasta = marcaciones.Max(m => m.FechaHora).Date.AddDays(1);
        var desde = hasta.AddDays(-DiasComparadosConOtrosEquipos);
        var comparables = marcaciones.FindAll(m => m.FechaHora >= desde);
        var ids = comparables.Select(m => m.IdMarcador).Distinct().ToList();

        var anuladas = contexto.Importaciones.Where(i => i.Anulada).Select(i => i.Id);

        var ajenas = await contexto.MarcacionesCrudas
            .AsNoTracking()
            .Where(m => m.EquipoId != equipoId
                        && ids.Contains(m.IdMarcador)
                        && m.FechaHora >= desde && m.FechaHora < hasta
                        && !anuladas.Contains(m.ImportacionId))
            .Select(m => new { m.EquipoId, m.IdMarcador, m.FechaHora })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        (long EquipoId, List<(string, DateTime)> Coincidentes, int Comparadas)? mayor = null;

        foreach (var deOtroEquipo in ajenas.GroupBy(a => a.EquipoId))
        {
            var instantes = deOtroEquipo.Select(a => (a.IdMarcador, a.FechaHora)).ToHashSet();
            var dias = deOtroEquipo.Select(a => a.FechaHora.Date).ToHashSet();

            var enDiasComunes = comparables.FindAll(m => dias.Contains(m.FechaHora.Date));
            var coincidentes = enDiasComunes.FindAll(instantes.Contains);

            if (coincidentes.Count >= MinimoCoincidentesConOtroEquipo
                && coincidentes.Count >= enDiasComunes.Count * ProporcionCoincidenteConOtroEquipo
                && (mayor is null || coincidentes.Count > mayor.Value.Coincidentes.Count))
            {
                mayor = (deOtroEquipo.Key, coincidentes, enDiasComunes.Count);
            }
        }

        if (mayor is not { } elegido)
        {
            return null;
        }

        var nombre = await contexto.EquiposMarcador
            .AsNoTracking()
            .Where(e => e.Id == elegido.EquipoId)
            .Select(e => e.Nombre)
            .FirstAsync(cancelacion)
            .ConfigureAwait(false);

        return new CoincidenciaConOtroEquipo(
            nombre,
            elegido.Coincidentes.Count,
            elegido.Comparadas,
            DateOnly.FromDateTime(elegido.Coincidentes.Min(c => c.Item2)),
            DateOnly.FromDateTime(elegido.Coincidentes.Max(c => c.Item2)));
    }

    /// <summary>
    /// Carga vigente de este mismo contenido hecha con un aparato distinto, si la hay.
    /// </summary>
    /// <remarks>
    /// Las anuladas no cuentan: anular la carga hecha con el equipo equivocado es
    /// justamente el camino para volver a cargar el archivo con el correcto.
    /// </remarks>
    private Task<CargaVigenteEnOtroEquipo?> BuscarCargaVigenteEnOtroEquipoAsync(
        string hash,
        long equipoId,
        CancellationToken cancelacion) =>
        contexto.Importaciones
            .AsNoTracking()
            .Where(i => i.HashContenido == hash && !i.Anulada && i.EquipoId != equipoId)
            .OrderByDescending(i => i.FechaCarga)
            .Select(i => new CargaVigenteEnOtroEquipo(i.Equipo!.Nombre, i.FechaCarga, i.CargadoPorUsuario))
            .FirstOrDefaultAsync(cancelacion);

    private static async Task<string> CalcularHashAsync(Stream contenido, CancellationToken cancelacion)
    {
        contenido.Position = 0;

        var bytes = await SHA256.HashDataAsync(contenido, cancelacion).ConfigureAwait(false);

        contenido.Position = 0;

        return Convert.ToHexString(bytes).ToLower(CultureInfo.InvariantCulture);
    }

    private static string Recortar(string valor, int maximo) =>
        valor.Length <= maximo ? valor : valor[..maximo];

    private sealed record SinAsignar(IdMarcadorSinAsignar Resumen, int PrimeraLinea);

    private sealed record CargaVigenteEnOtroEquipo(string Equipo, DateTime FechaCarga, string CargadoPorUsuario);

    private sealed record CoincidenciaConOtroEquipo(
        string Equipo,
        int Coincidentes,
        int Comparadas,
        DateOnly Desde,
        DateOnly Hasta);
}
