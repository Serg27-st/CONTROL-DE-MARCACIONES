using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Programacion;
using ControlMarcacion.Application.Programacion.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0011

namespace ControlMarcacion.Infrastructure.Programacion;

internal sealed class ConciliadorProgramacion(
    MarcacionDbContext contexto,
    IValidadorPreparacionTareo validador) : IConciliadorProgramacion
{
    private const int MaximoFechas = 5000;
    private static readonly SemaphoreSlim Exclusividad = new(1, 1);

    public async Task<ResultadoPrevisualizacionProgramacion> PrevisualizarAsync(
        IReadOnlyList<EntradaProgramacion> entradas,
        string nombreOrigen,
        IReadOnlyCollection<long>? plantasAutorizadas,
        CancellationToken cancelacion = default) =>
        (await AnalizarAsync(entradas, nombreOrigen, plantasAutorizadas, cancelacion).ConfigureAwait(false)).Resultado;

    public async Task<ResultadoConfirmacionProgramacion> ConfirmarAsync(
        IReadOnlyList<EntradaProgramacion> entradas,
        string nombreOrigen,
        string huellaEsperada,
        string usuario,
        IReadOnlyCollection<long>? plantasAutorizadas,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(huellaEsperada))
            throw new ReglaNegocioException("La previsualización no es válida. Analice nuevamente los datos.");

        await Exclusividad.WaitAsync(cancelacion).ConfigureAwait(false);
        try
        {
            await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion).ConfigureAwait(false);
            var analisis = await AnalizarAsync(entradas, nombreOrigen, plantasAutorizadas, cancelacion).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(analisis.Resultado.Huella),
                    Encoding.UTF8.GetBytes(huellaEsperada)))
                throw new ReglaNegocioException(
                    "La programación o sus catálogos cambiaron después de la previsualización. Vuelva a analizar los datos.");

            if (!analisis.Resultado.PuedeConfirmar)
                throw new ReglaNegocioException("La previsualización contiene conflictos o datos incompletos.");

            var nuevosTurnos = analisis.Dias
                .Where(d => d.Tipo == TipoDiaProgramacion.Trabajo && d.TurnoId is null)
                .GroupBy(d => d.CodigoTurno!, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var mapaTurnos = analisis.Dias.Where(d => d.TurnoId is not null)
                .GroupBy(d => d.CodigoTurno!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().TurnoId!.Value, StringComparer.OrdinalIgnoreCase);

            foreach (var grupo in nuevosTurnos)
            {
                var dato = grupo.First();
                var turno = new Turno
                {
                    Codigo = dato.CodigoTurno!, Nombre = dato.NombreTurno!, HoraInicio = dato.HoraInicio!.Value,
                    HoraFin = dato.HoraFin!.Value, MinutosRefrigerio = dato.MinutosRefrigerio!.Value,
                    MinutosTolerancia = 0, MinutosAnticipacionEntrada = 15, MinutosAnticipacionSalida = 5,
                    MinutosGraciaSalida = 0,
                    MinutosJornadaOrdinaria = DuracionEfectiva(dato),
                    DiasAplicables = DiasSemana.Todos, CreadoPor = usuario
                };
                foreach (var plantaId in grupo.Select(d => d.PlantaId).Distinct())
                    turno.PlantasHabilitadas.Add(new TurnoPlanta { PlantaId = plantaId, CreadoPor = usuario });
                turno.Versiones.Add(VersionTurno.De(turno, grupo.Min(d => d.Fecha), usuario));
                contexto.Turnos.Add(turno);
                await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
                mapaTurnos[grupo.Key] = turno.Id;
            }

            var periodos = await contexto.Periodos
                .Where(p => p.Activo && analisis.Desde <= p.FechaFin && analisis.Hasta >= p.FechaInicio)
                .ToListAsync(cancelacion).ConfigureAwait(false);
            foreach (var periodo in periodos)
            {
                if (analisis.Dias.Any(d => periodo.Contiene(d.Fecha))) contexto.AfirmarAbierto(periodo);
            }

            var programados = 0;
            var idempotentes = 0;
            foreach (var trabajador in analisis.Dias.GroupBy(d => d.TrabajadorId))
            {
                var ordenados = trabajador.OrderBy(d => d.Fecha).ToList();
                foreach (var bloque in BloquesContiguos(ordenados))
                {
                    if (bloque.All(d => d.CoincideConExistente))
                    {
                        idempotentes += bloque.Count;
                        continue;
                    }

                    var plantaId = bloque.Select(d => d.PlantaId).Distinct().Single();
                    var codigo = $"IMP-{trabajador.Key}";
                    var cuadrilla = contexto.Cuadrillas.Local
                        .FirstOrDefault(c => c.PlantaId == plantaId && c.Codigo == codigo)
                        ?? await contexto.Cuadrillas
                            .FirstOrDefaultAsync(c => c.PlantaId == plantaId && c.Codigo == codigo, cancelacion)
                            .ConfigureAwait(false);
                    if (cuadrilla is null)
                    {
                        cuadrilla = new Cuadrilla
                        {
                            Codigo = codigo, Nombre = $"Programación individual {trabajador.Key}",
                            PlantaId = plantaId, CreadoPor = usuario
                        };
                        contexto.Cuadrillas.Add(cuadrilla);
                    }

                    var programacion = new ProgramacionCuadrilla
                    {
                        Cuadrilla = cuadrilla, FechaInicio = bloque[0].Fecha, FechaFin = bloque[^1].Fecha,
                        FechaAncla = bloque[0].Fecha, CreadoPor = usuario
                    };
                    var orden = 1;
                    foreach (var dia in bloque)
                    {
                        programacion.Tramos.Add(new TramoRotacion
                        {
                            Orden = orden++, Tipo = dia.Tipo == TipoDiaProgramacion.Descanso
                                ? TipoTramoRotacion.Descanso : TipoTramoRotacion.Trabajo,
                            TurnoId = dia.Tipo == TipoDiaProgramacion.Trabajo ? mapaTurnos[dia.CodigoTurno!] : null,
                            Dias = 1, CreadoPor = usuario
                        });
                    }
                    contexto.ProgramacionesCuadrilla.Add(programacion);
                    contexto.AsignacionesTurno.Add(new AsignacionTurno
                    {
                        TrabajadorId = trabajador.Key, Cuadrilla = cuadrilla,
                        FechaInicio = bloque[0].Fecha, FechaFin = bloque[^1].Fecha, CreadoPor = usuario
                    });
                    programados += bloque.Count;
                }
            }

            try
            {
                await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
                await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ReglaNegocioException(
                    "La programación cambió durante la confirmación. No se guardó el lote; vuelva a previsualizar.");
            }
            catch (DbUpdateException excepcion)
            {
                throw new ReglaNegocioException(
                    "Otra operación creó una programación incompatible durante la confirmación. " +
                    $"No se guardó el lote ({excepcion.GetBaseException().Message}).");
            }

            var reevaluados = analisis.Dias.Select(d => d.TrabajadorId).Distinct().ToList();
            foreach (var trabajadorId in reevaluados)
            {
                var dias = analisis.Dias.Where(d => d.TrabajadorId == trabajadorId).ToList();
                await validador.EvaluarIntervaloAsync(
                    trabajadorId, dias.Min(d => d.Fecha), dias.Max(d => d.Fecha),
                    operacion: OperacionPreparacionTareo.ConsultaHistorica, cancelacion: cancelacion)
                    .ConfigureAwait(false);
            }

            return new ResultadoConfirmacionProgramacion(
                nombreOrigen, reevaluados.Count, programados,
                analisis.Dias.Where(d => d.Tipo == TipoDiaProgramacion.Trabajo && d.TurnoId is not null)
                    .Select(d => d.CodigoTurno).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                nuevosTurnos.Count, analisis.Dias.Count(d => d.Tipo == TipoDiaProgramacion.Descanso && !d.CoincideConExistente),
                idempotentes, reevaluados, analisis.Resultado.Huella);
        }
        finally
        {
            Exclusividad.Release();
        }
    }

    internal async Task<Analisis> AnalizarAsync(
        IReadOnlyList<EntradaProgramacion> entradas,
        string nombreOrigen,
        IReadOnlyCollection<long>? plantasAutorizadas,
        CancellationToken cancelacion)
    {
        if (entradas.Count == 0) throw new ReglaNegocioException("No hay entradas de programación.");

        var tipos = await contexto.TiposDocumento.AsNoTracking().ToListAsync(cancelacion).ConfigureAwait(false);
        var empresas = await contexto.Empresas.AsNoTracking().ToListAsync(cancelacion).ConfigureAwait(false);
        var plantas = await contexto.Plantas.AsNoTracking().ToListAsync(cancelacion).ConfigureAwait(false);
        var trabajadores = await contexto.Trabajadores.AsNoTracking()
            .Include(t => t.PeriodosLaborales).ThenInclude(p => p.AsignacionesPlanta)
            .Include(t => t.AsignacionesTurno).ThenInclude(a => a.Turno)
            .Include(t => t.AsignacionesTurno).ThenInclude(a => a.Cuadrilla)!.ThenInclude(c => c!.Programaciones)
                .ThenInclude(p => p.Tramos)
            .ToListAsync(cancelacion).ConfigureAwait(false);
        var turnos = await contexto.Turnos.AsNoTracking().Include(t => t.PlantasHabilitadas)
            .Where(t => t.Activo).ToListAsync(cancelacion).ConfigureAwait(false);
        var periodos = await contexto.Periodos.AsNoTracking().Where(p => p.Activo)
            .ToListAsync(cancelacion).ConfigureAwait(false);

        var dias = new List<DiaAnalizado>();
        var vistas = new List<FilaProgramacionPrevisualizada>();
        foreach (var entrada in entradas)
        {
            var fechas = Expandir(entrada).ToList();
            if (dias.Count + fechas.Count > MaximoFechas)
                throw new ReglaNegocioException($"El lote supera el máximo de {MaximoFechas:N0} fechas programadas.");

            var tipoDoc = tipos.Where(t => Coincide(t.Codigo, entrada.TipoDocumento) || Coincide(t.Nombre, entrada.TipoDocumento)).ToList();
            var candidatos = tipoDoc.Count == 1
                ? trabajadores.Where(t => t.TipoDocumentoId == tipoDoc[0].Id
                    && string.Equals(t.NumeroDocumento.Trim(), entrada.Documento.Trim(), StringComparison.OrdinalIgnoreCase)).ToList()
                : [];
            foreach (var fecha in fechas)
            {
                var vista = ResolverDia(entrada, fecha, candidatos, empresas, plantas, turnos, periodos, plantasAutorizadas);
                vistas.Add(vista.Vista);
                if (vista.Dia is not null) dias.Add(vista.Dia);
            }
        }

        foreach (var duplicados in dias.GroupBy(d => (d.TrabajadorId, d.Fecha)).Where(g => g.Count() > 1))
        {
            foreach (var dia in duplicados) dia.MarcarConflicto("La fecha aparece más de una vez dentro del lote.");
            for (var i = 0; i < vistas.Count; i++)
                if (vistas[i].TrabajadorId == duplicados.Key.TrabajadorId && vistas[i].Fecha == duplicados.Key.Fecha)
                    vistas[i] = vistas[i] with { Estado = EstadoFilaProgramacion.Conflicto, Motivo = "Duplicado intralote." };
        }

        var turnosVista = dias.Where(d => d.Tipo == TipoDiaProgramacion.Trabajo)
            .GroupBy(d => d.CodigoTurno!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TurnoProgramacionConciliado(
                g.Key, g.First().NombreTurno ?? g.Key, g.First().HoraInicio, g.First().HoraFin,
                g.First().MinutosRefrigerio,
                g.Any(d => d.Estado == EstadoFilaProgramacion.Conflicto) ? EstadoFilaProgramacion.Conflicto
                    : g.First().TurnoId is null ? EstadoFilaProgramacion.NuevoPropuesto : EstadoFilaProgramacion.Existente,
                g.First().TurnoId, g.Count(), g.FirstOrDefault(d => d.Motivo is not null)?.Motivo))
            .ToList();

        var material = string.Join('\n', vistas.OrderBy(v => v.Fila).ThenBy(v => v.Fecha)
            .Select(v => $"{v.Fila}|{v.TrabajadorId}|{v.Fecha:yyyyMMdd}|{v.TipoDia}|{v.CodigoTurno}|{v.Estado}|{v.Motivo}"))
            + "\n" + string.Join('|', turnos.OrderBy(t => t.Id).Select(t => $"T{t.Id}:{t.ModificadoEn:o}:{t.Activo}"))
            + "\n" + string.Join('|', trabajadores.SelectMany(t => t.AsignacionesTurno).OrderBy(a => a.Id)
                .Select(a => $"A{a.Id}:{a.FechaInicio:yyyyMMdd}:{a.FechaFin:yyyyMMdd}:{a.TurnoId}:{a.CuadrillaId}:{a.ModificadoEn:o}"))
            + "\n" + string.Join('|', periodos.OrderBy(p => p.Id).Select(p => $"P{p.Id}:{p.Estado}:{p.Version}"));
        var huella = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        var resultado = new ResultadoPrevisualizacionProgramacion(
            nombreOrigen, entradas.Count, vistas.Count, dias.Select(d => d.TrabajadorId).Distinct().Count(),
            dias.Count(d => d.Tipo == TipoDiaProgramacion.Descanso), dias.Count(d => d.Nocturno), turnosVista, vistas, huella);
        return new Analisis(resultado, dias, dias.Count == 0 ? DateOnly.MinValue : dias.Min(d => d.Fecha),
            dias.Count == 0 ? DateOnly.MinValue : dias.Max(d => d.Fecha));
    }

    private static ResolucionDia ResolverDia(
        EntradaProgramacion entrada, DateOnly fecha, List<Trabajador> candidatos,
        List<ControlMarcacion.Domain.Organizacion.Empresa> empresas,
        List<ControlMarcacion.Domain.Organizacion.Planta> plantas,
        List<Turno> turnos, List<Periodo> periodos, IReadOnlyCollection<long>? alcance)
    {
        FilaProgramacionPrevisualizada Error(EstadoFilaProgramacion estado, string motivo) =>
            new(entrada.Fila, null, entrada.Documento, "No identificado", entrada.Empresa, entrada.Unidad,
                fecha, entrada.Tipo.ToString().ToUpperInvariant(), entrada.CodigoTurno, entrada.NombreTurno,
                entrada.Nocturno, estado, "No se aplicará", motivo);

        if (candidatos.Count != 1)
            return new(null, Error(candidatos.Count == 0 ? EstadoFilaProgramacion.Incompleto : EstadoFilaProgramacion.Conflicto,
                candidatos.Count == 0 ? "No se encontró un trabajador único por tipo y documento." : "El documento es ambiguo."));
        var trabajador = candidatos[0];
        var vinculos = trabajador.PeriodosLaborales.Where(p => p.EstaVigenteEn(fecha)).ToList();
        if (vinculos.Count != 1) return new(null, Error(EstadoFilaProgramacion.Conflicto,
            vinculos.Count == 0 ? "La fecha no pertenece a un vínculo laboral." : "Hay vínculos laborales solapados."));
        var empresa = empresas.SingleOrDefault(e => e.Id == vinculos[0].EmpresaId);
        if (empresa is null || !(Coincide(empresa.Codigo, entrada.Empresa) || Coincide(empresa.RazonSocial, entrada.Empresa)))
            return new(null, Error(EstadoFilaProgramacion.Conflicto, "La empresa no coincide con el vínculo histórico."));
        var asignacionesPlanta = vinculos[0].AsignacionesPlanta.Where(a => a.EstaVigenteEn(fecha)).ToList();
        if (asignacionesPlanta.Count != 1) return new(null, Error(EstadoFilaProgramacion.Conflicto,
            "No existe una unidad histórica única para la fecha."));
        var planta = plantas.Single(p => p.Id == asignacionesPlanta[0].PlantaId);
        if (!(Coincide(planta.Codigo, entrada.Unidad) || Coincide(planta.Nombre, entrada.Unidad)))
            return new(null, Error(EstadoFilaProgramacion.Conflicto, "La unidad no coincide con la historia laboral."));
        if (alcance is not null && !alcance.Contains(planta.Id))
            return new(null, Error(EstadoFilaProgramacion.Conflicto, "La unidad está fuera del alcance autorizado."));
        var cerrado = periodos.FirstOrDefault(p => p.Contiene(fecha) && !p.EstaAbierto);
        if (cerrado is not null) return new(null, Error(EstadoFilaProgramacion.Conflicto,
            $"El periodo {cerrado.Codigo} está cerrado."));

        Turno? turno = null;
        if (entrada.Tipo == TipoDiaProgramacion.Trabajo)
        {
            turno = turnos.SingleOrDefault(t => string.Equals(t.Codigo, entrada.CodigoTurno, StringComparison.OrdinalIgnoreCase));
            if (turno is not null && !DefinicionCompatible(turno, entrada))
                return new(null, Error(EstadoFilaProgramacion.Conflicto,
                    "El código existe con horas o reglas diferentes; no se sobrescribirá."));
            if (turno is null && !entrada.DefinicionCompleta)
                return new(null, Error(EstadoFilaProgramacion.Incompleto,
                    "El turno nuevo requiere nombre, hora de entrada, hora de salida y refrigerio."));
            if (turno is not null && turno.PlantasHabilitadas.Count > 0
                && turno.PlantasHabilitadas.All(tp => tp.PlantaId != planta.Id))
                return new(null, Error(EstadoFilaProgramacion.Conflicto, "El turno no está habilitado en la unidad."));
        }

        var existentes = trabajador.AsignacionesTurno.Where(a => a.EstaVigenteEn(fecha)).ToList();
        if (existentes.Count > 1) return new(null, Error(EstadoFilaProgramacion.Conflicto,
            "Existen asignaciones de turno solapadas en la fecha."));
        var coincide = existentes.Count == 1 && CoincideProgramacion(existentes[0], fecha, entrada, turno?.Id);
        if (existentes.Count == 1 && !coincide) return new(null, Error(EstadoFilaProgramacion.Conflicto,
            "Ya existe una programación diferente; no se sobrescribirá silenciosamente."));

        var estado = coincide ? EstadoFilaProgramacion.Existente
            : entrada.Tipo == TipoDiaProgramacion.Descanso ? EstadoFilaProgramacion.Descanso
            : turno is null ? EstadoFilaProgramacion.NuevoPropuesto : EstadoFilaProgramacion.Existente;
        var nombre = $"{trabajador.ApellidoPaterno} {trabajador.ApellidoMaterno}, {trabajador.Nombres}";
        var dia = new DiaAnalizado(entrada.Fila, trabajador.Id, planta.Id, fecha, entrada.Tipo,
            entrada.CodigoTurno, entrada.NombreTurno, entrada.HoraInicio, entrada.HoraFin,
            entrada.MinutosRefrigerio, turno?.Id, entrada.Nocturno, estado, coincide, null);
        var vista = new FilaProgramacionPrevisualizada(
            entrada.Fila, trabajador.Id, entrada.Documento, nombre, empresa.RazonSocial, planta.Nombre, fecha,
            entrada.Tipo.ToString().ToUpperInvariant(), entrada.CodigoTurno, entrada.NombreTurno,
            entrada.Nocturno, estado, coincide ? "Sin cambios (idempotente)" : "Crear programación finita");
        return new(dia, vista);
    }

    private static bool CoincideProgramacion(AsignacionTurno asignacion, DateOnly fecha, EntradaProgramacion entrada, long? turnoId)
    {
        if (asignacion.TurnoId is { } fijo)
            return entrada.Tipo == TipoDiaProgramacion.Trabajo && turnoId == fijo;
        var vigentes = asignacion.Cuadrilla?.Programaciones.Where(p => p.EstaVigenteEn(fecha)).ToList() ?? [];
        if (vigentes.Count != 1) return false;
        var resultado = vigentes[0].ResolverEn(fecha);
        return entrada.Tipo == TipoDiaProgramacion.Descanso
            ? resultado.Estado == EstadoProgramacionDiaria.DescansoProgramado
            : resultado.Estado == EstadoProgramacionDiaria.Trabajo && resultado.TurnoId == turnoId;
    }

    private static bool DefinicionCompatible(Turno turno, EntradaProgramacion entrada)
    {
        if (!entrada.DefinicionInformada) return true;
        return entrada.HoraInicio == turno.HoraInicio && entrada.HoraFin == turno.HoraFin
            && entrada.MinutosRefrigerio == turno.MinutosRefrigerio
            && entrada.Nocturno == turno.CruzaMedianoche;
    }

    internal static IEnumerable<DateOnly> Expandir(EntradaProgramacion e)
    {
        if (e.FechaEspecifica is { } unica) { yield return unica; yield break; }
        for (var fecha = e.FechaInicio!.Value; fecha <= e.FechaFin!.Value; fecha = fecha.AddDays(1)) yield return fecha;
    }

    internal static IEnumerable<List<DiaAnalizado>> BloquesContiguos(List<DiaAnalizado> dias)
    {
        var bloque = new List<DiaAnalizado>();
        foreach (var dia in dias)
        {
            if (bloque.Count > 0 && (dia.Fecha != bloque[^1].Fecha.AddDays(1)
                || dia.PlantaId != bloque[^1].PlantaId || dia.CoincideConExistente != bloque[^1].CoincideConExistente))
            { yield return bloque; bloque = []; }
            bloque.Add(dia);
        }
        if (bloque.Count > 0) yield return bloque;
    }

    internal static int DuracionEfectiva(DiaAnalizado d)
    {
        var bruto = d.HoraFin <= d.HoraInicio
            ? (int)(TimeSpan.FromDays(1) - d.HoraInicio!.Value.ToTimeSpan() + d.HoraFin!.Value.ToTimeSpan()).TotalMinutes
            : (int)(d.HoraFin!.Value.ToTimeSpan() - d.HoraInicio!.Value.ToTimeSpan()).TotalMinutes;
        var efectivo = bruto - d.MinutosRefrigerio!.Value;
        if (efectivo <= 0) throw new ReglaNegocioException($"El turno {d.CodigoTurno} no tiene duración efectiva positiva.");
        return efectivo;
    }

    internal static bool Coincide(string? a, string? b) => Normalizar(a) == Normalizar(b);

    internal static string Normalizar(string? valor)
    {
        var texto = (valor ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(texto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC).Replace('_', ' ');
    }

    internal sealed record ResolucionDia(DiaAnalizado? Dia, FilaProgramacionPrevisualizada Vista);
    internal sealed record Analisis(ResultadoPrevisualizacionProgramacion Resultado, List<DiaAnalizado> Dias,
        DateOnly Desde, DateOnly Hasta);
    internal sealed class DiaAnalizado(int fila, long trabajadorId, long plantaId, DateOnly fecha, TipoDiaProgramacion tipo,
        string? codigoTurno, string? nombreTurno, TimeOnly? horaInicio, TimeOnly? horaFin,
        int? minutosRefrigerio, long? turnoId, bool nocturno, EstadoFilaProgramacion estado,
        bool coincideConExistente, string? motivo)
    {
        public int Fila { get; } = fila; public long TrabajadorId { get; } = trabajadorId;
        public long PlantaId { get; } = plantaId; public DateOnly Fecha { get; } = fecha; public TipoDiaProgramacion Tipo { get; } = tipo;
        public string? CodigoTurno { get; } = codigoTurno; public string? NombreTurno { get; } = nombreTurno;
        public TimeOnly? HoraInicio { get; } = horaInicio; public TimeOnly? HoraFin { get; } = horaFin;
        public int? MinutosRefrigerio { get; } = minutosRefrigerio; public long? TurnoId { get; } = turnoId;
        public bool Nocturno { get; } = nocturno; public EstadoFilaProgramacion Estado { get; private set; } = estado;
        public bool CoincideConExistente { get; } = coincideConExistente; public string? Motivo { get; private set; } = motivo;
        public void MarcarConflicto(string motivoNuevo) { Estado = EstadoFilaProgramacion.Conflicto; Motivo = motivoNuevo; }
    }
}

#pragma warning restore IDE0011
