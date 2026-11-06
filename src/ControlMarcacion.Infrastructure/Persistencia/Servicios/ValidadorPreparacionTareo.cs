using System.Globalization;
using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>
/// Proyección diagnóstica, histórica y de solo lectura de los mismos datos que consume el
/// motor. No lee marcaciones ni tareos y nunca completa configuración por su cuenta.
/// </summary>
internal sealed class ValidadorPreparacionTareo(
    MarcacionDbContext contexto,
    IProveedorAlcancePlantas alcance) : IValidadorPreparacionTareo
{
    public Task<PreparacionTrabajadorTareo> EvaluarFechaAsync(
        long trabajadorId,
        DateOnly fecha,
        long? periodoId = null,
        OperacionPreparacionTareo operacion = OperacionPreparacionTareo.ConsultaHistorica,
        CancellationToken cancelacion = default) =>
        EvaluarIntervaloAsync(trabajadorId, fecha, fecha, periodoId, operacion, cancelacion);

    public async Task<PreparacionTrabajadorTareo> EvaluarIntervaloAsync(
        long trabajadorId,
        DateOnly desde,
        DateOnly hasta,
        long? periodoId = null,
        OperacionPreparacionTareo operacion = OperacionPreparacionTareo.ConsultaHistorica,
        CancellationToken cancelacion = default)
    {
        ValidarRango(desde, hasta);

        var filtro = new FiltroPreparacionTareo
        {
            PeriodoId = periodoId,
            Desde = desde,
            Hasta = hasta,
            TrabajadoresId = [trabajadorId],
            Operacion = operacion
        };

        var resultados = await EvaluarAutorizadosAsync(filtro, cancelacion).ConfigureAwait(false);
        return resultados.SingleOrDefault()
            ?? throw new RecursoNoEncontradoException("Trabajador", trabajadorId);
    }

    public async Task<IReadOnlyList<PreparacionTrabajadorTareo>> EvaluarAutorizadosAsync(
        FiltroPreparacionTareo filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ValidarRango(filtro.Desde, filtro.Hasta);

        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);
        if (plantasVisibles is { Count: 0 })
        {
            return [];
        }

        var consulta = contexto.Trabajadores
            .AsNoTracking()
            .Include(t => t.PeriodosLaborales)
                .ThenInclude(p => p.AsignacionesPlanta)
                    .ThenInclude(a => a.Planta)
            .Include(t => t.AsignacionesMarcador)
                .ThenInclude(a => a.Equipo)
            .Include(t => t.AsignacionesTurno)
            .AsSplitQuery()
            .AsQueryable();

        if (filtro.TrabajadoresId is { Count: > 0 } ids)
        {
            consulta = consulta.Where(t => ids.Contains(t.Id));
        }

        if (plantasVisibles is not null)
        {
            consulta = consulta.Where(t => t.PeriodosLaborales.Any(p =>
                p.FechaIngreso <= filtro.Hasta
                && (p.FechaCese == null || p.FechaCese >= filtro.Desde)
                && p.AsignacionesPlanta.Any(a =>
                    plantasVisibles.Contains(a.PlantaId)
                    && a.FechaInicio <= filtro.Hasta
                    && (a.FechaFin == null || a.FechaFin >= filtro.Desde))));
        }

        var trabajadores = await consulta
            .OrderBy(t => t.ApellidoPaterno)
            .ThenBy(t => t.ApellidoMaterno)
            .ThenBy(t => t.Nombres)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        if (trabajadores.Count == 0)
        {
            return [];
        }

        var cuadrillas = trabajadores
            .SelectMany(t => t.AsignacionesTurno)
            .Where(a => a.CuadrillaId is not null)
            .Select(a => a.CuadrillaId!.Value)
            .Distinct()
            .ToList();

        var programaciones = cuadrillas.Count == 0
            ? []
            : await contexto.ProgramacionesCuadrilla
                .AsNoTracking()
                .Include(p => p.Tramos)
                .Include(p => p.Cuadrilla)
                .Where(p => cuadrillas.Contains(p.CuadrillaId)
                            && p.FechaInicio <= filtro.Hasta
                            && (p.FechaFin == null || p.FechaFin >= filtro.Desde))
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);

        var porCuadrilla = programaciones
            .GroupBy(p => p.CuadrillaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var turnos = await contexto.Turnos
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, cancelacion)
            .ConfigureAwait(false);
        var versionesTurno = await contexto.VersionesTurno
            .AsNoTracking()
            .Where(v => v.Activo
                        && v.FechaInicio <= filtro.Hasta
                        && (v.FechaFin == null || v.FechaFin >= filtro.Desde))
            .GroupBy(v => v.TurnoId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), cancelacion)
            .ConfigureAwait(false);
        var empresas = await contexto.Empresas
            .AsNoTracking()
            .Select(e => e.Id)
            .ToHashSetAsync(cancelacion)
            .ConfigureAwait(false);
        var marcadoresCatalogados = await contexto.Marcadores
            .AsNoTracking()
            .Where(m => m.Activo)
            .Select(m => new { m.EquipoId, m.Codigo })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
        var identidadesMarcador = marcadoresCatalogados
            .Select(m => (m.EquipoId, m.Codigo))
            .ToHashSet();
        var periodos = await contexto.Periodos
            .AsNoTracking()
            .Where(p => filtro.PeriodoId == null || p.Id == filtro.PeriodoId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var resultados = trabajadores
            .Select(t => Evaluar(
                t, filtro, plantasVisibles, empresas, identidadesMarcador,
                turnos, versionesTurno, porCuadrilla, periodos))
            .Where(r => r.Dias.Count > 0)
            .Where(r => filtro.PlantaId is null || r.Dias.Any(d => d.PlantaId == filtro.PlantaId))
            .Where(r => filtro.Estado is null || r.Estado == filtro.Estado)
            .ToList();

        return resultados;
    }

    private static PreparacionTrabajadorTareo Evaluar(
        Trabajador trabajador,
        FiltroPreparacionTareo filtro,
        IReadOnlyCollection<long>? plantasVisibles,
        HashSet<long> empresas,
        HashSet<(long EquipoId, string Codigo)> marcadoresCatalogados,
        IReadOnlyDictionary<long, Turno> turnos,
        IReadOnlyDictionary<long, List<VersionTurno>> versionesTurno,
        IReadOnlyDictionary<long, List<ProgramacionCuadrilla>> programaciones,
        IReadOnlyList<Periodo> periodos)
    {
        var dias = new List<PreparacionDiaTareo>();

        for (var fecha = filtro.Desde; fecha <= filtro.Hasta; fecha = fecha.AddDays(1))
        {
            var vinculos = trabajador.PeriodosLaborales.Where(p => p.EstaVigenteEn(fecha)).ToList();
            var plantasDelDia = vinculos
                .SelectMany(p => p.AsignacionesPlanta)
                .Where(a => a.EstaVigenteEn(fecha))
                .Select(a => a.PlantaId)
                .Distinct()
                .ToList();

            // No se devuelve ni siquiera un día perteneciente exclusivamente a otra unidad.
            if (plantasVisibles is not null
                && plantasDelDia.Count > 0
                && !plantasDelDia.Any(plantasVisibles.Contains))
            {
                continue;
            }

            dias.Add(EvaluarDia(
                trabajador, fecha, filtro, plantasVisibles, vinculos, empresas,
                marcadoresCatalogados, turnos, versionesTurno, programaciones, periodos));
        }

        var incidencias = dias
            .SelectMany(d => d.Incidencias)
            .DistinctBy(i => (i.Codigo, i.Explicacion, i.Referencia))
            .ToList();
        var conflictos = dias.Count(d => d.Estado == EstadoPreparacionTareo.Conflicto);
        var incompletos = dias.Count(d => d.Estado == EstadoPreparacionTareo.Incompleto);
        var listos = dias.Count - conflictos - incompletos;
        var estado = conflictos > 0
            ? EstadoPreparacionTareo.Conflicto
            : incompletos > 0
                ? EstadoPreparacionTareo.Incompleto
                : EstadoPreparacionTareo.Listo;

        return new PreparacionTrabajadorTareo(
            trabajador.Id,
            trabajador.NumeroDocumento,
            trabajador.ApellidosYNombres,
            filtro.Desde,
            filtro.Hasta,
            estado,
            listos,
            incompletos,
            conflictos,
            dias,
            incidencias);
    }

    private static PreparacionDiaTareo EvaluarDia(
        Trabajador trabajador,
        DateOnly fecha,
        FiltroPreparacionTareo filtro,
        IReadOnlyCollection<long>? plantasVisibles,
        List<PeriodoLaboral> vinculos,
        HashSet<long> empresas,
        HashSet<(long EquipoId, string Codigo)> marcadoresCatalogados,
        IReadOnlyDictionary<long, Turno> turnos,
        IReadOnlyDictionary<long, List<VersionTurno>> versionesTurno,
        IReadOnlyDictionary<long, List<ProgramacionCuadrilla>> programaciones,
        IReadOnlyList<Periodo> periodos)
    {
        var requisitos = new List<RequisitoPreparacionTareo>();
        var incidencias = new List<IncidenciaPreparacionTareo>();
        long? empresaId = null;
        long? plantaId = null;
        string? planta = null;
        long? equipoId = null;
        string? idMarcador = null;
        long? turnoId = null;
        var esDescanso = false;

        if (vinculos.Count == 0)
        {
            Pendiente("CATREADY-01", "Relación laboral",
                "No existe una relación laboral aplicable a la fecha.",
                "Registre o corrija el periodo laboral del trabajador.");
            NoAplica("CATREADY-02", "Empresa", "Sin vínculo laboral que determine empresa.");
            NoAplica("CATREADY-03", "Unidad", "Sin vínculo laboral que determine unidad.");
            NoAplica("CATREADY-04", "Equipo", "Sin vínculo laboral aplicable.");
            NoAplica("CATREADY-05", "Marcador", "Sin vínculo laboral aplicable.");
            NoAplica("CATREADY-06", "Turno", "Sin vínculo laboral aplicable.");
            NoAplica("CATREADY-07", "Descanso", "Sin vínculo laboral aplicable.");
        }
        else if (vinculos.Count > 1)
        {
            Conflicto("CATREADY-09-PERIODO-LABORAL", "Relación laboral",
                "Hay más de un periodo laboral vigente en la misma fecha.",
                "Sanee los periodos laborales solapados.",
                string.Join(",", vinculos.Select(v => v.Id)));
            NoAplica("CATREADY-02", "Empresa", "La empresa es ambigua por vínculos solapados.");
            NoAplica("CATREADY-03", "Unidad", "La unidad es ambigua por vínculos solapados.");
            NoAplica("CATREADY-04", "Equipo", "No se evalúa hasta resolver el vínculo.");
            NoAplica("CATREADY-05", "Marcador", "No se evalúa hasta resolver el vínculo.");
            NoAplica("CATREADY-06", "Turno", "No se evalúa hasta resolver el vínculo.");
            NoAplica("CATREADY-07", "Descanso", "No se evalúa hasta resolver el vínculo.");
        }
        else
        {
            var vinculo = vinculos[0];
            empresaId = vinculo.EmpresaId;
            Cumplido("CATREADY-01", "Relación laboral",
                $"Vínculo {vinculo.FechaIngreso:dd/MM/yyyy}–{(vinculo.FechaCese is null ? "abierto" : vinculo.FechaCese.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))}.");

            if (empresaId <= 0 || !empresas.Contains(empresaId.Value))
            {
                Pendiente("CATREADY-02", "Empresa", "La empresa histórica no existe.",
                    "Corrija la empresa del periodo laboral.", Referencia(vinculo.Id));
            }
            else
            {
                Cumplido("CATREADY-02", "Empresa", $"Empresa histórica {empresaId}.");
            }

            var plantas = vinculo.AsignacionesPlanta.Where(a => a.EstaVigenteEn(fecha)).ToList();
            if (plantas.Count == 0)
            {
                Pendiente("CATREADY-03", "Unidad", "No existe una unidad vigente.",
                    "Asigne la unidad histórica dentro del vínculo laboral.", Referencia(vinculo.Id));
            }
            else if (plantas.Count > 1)
            {
                Conflicto("CATREADY-09-UNIDAD", "Unidad",
                    "Hay más de una unidad vigente en la misma fecha.",
                    "Sanee las asignaciones históricas de unidad.",
                    string.Join(",", plantas.Select(p => p.Id)));
            }
            else
            {
                plantaId = plantas[0].PlantaId;
                planta = plantas[0].Planta?.Nombre;
                if (plantas[0].Planta is null)
                {
                    Pendiente("CATREADY-03", "Unidad", "La unidad histórica no existe.",
                        "Corrija la asignación de unidad.", Referencia(plantas[0].Id));
                }
                else
                {
                    Cumplido("CATREADY-03", "Unidad", $"{planta}.");
                }
            }

            var marcadores = trabajador.AsignacionesMarcador.Where(a => a.EstaVigenteEn(fecha)).ToList();
            if (marcadores.Count == 0)
            {
                Pendiente("CATREADY-05", "Marcador", "No existe una asignación de marcador vigente.",
                    "Asigne el ID y el equipo que correspondan desde esta fecha.");
                Pendiente("CATREADY-04", "Equipo", "No puede determinarse un equipo sin asignación de marcador.",
                    "Asigne el marcador al aparato físico correcto.");
            }
            else if (marcadores.Count > 1)
            {
                Conflicto("CATREADY-09-MARCADOR", "Marcador",
                    "Hay asignaciones de marcador solapadas.",
                    "Cierre la vigencia anterior antes de abrir la siguiente.",
                    string.Join(",", marcadores.Select(m => m.Id)));
                NoAplica("CATREADY-04", "Equipo", "El equipo es ambiguo por asignaciones solapadas.");
            }
            else
            {
                var marcador = marcadores[0];
                equipoId = marcador.EquipoId;
                idMarcador = marcador.IdMarcador;
                if (string.IsNullOrWhiteSpace(idMarcador))
                {
                    Pendiente("CATREADY-05", "Marcador", "La asignación no tiene un ID de marcador.",
                        "Registre el código que emite el aparato.", Referencia(marcador.Id));
                }
                else if (!marcadoresCatalogados.Contains((marcador.EquipoId, idMarcador)))
                {
                    Pendiente("CATREADY-05", "Marcador",
                        "El ID asignado no existe como marcador activo del equipo.",
                        "Registre o seleccione el marcador correcto para ese equipo.", Referencia(marcador.Id));
                }
                else
                {
                    Cumplido("CATREADY-05", "Marcador", $"ID {idMarcador} vigente.");
                }

                if (marcador.Equipo is null)
                {
                    Pendiente("CATREADY-04", "Equipo", "El equipo asignado no existe.",
                        "Corrija la asignación o registre el equipo.", Referencia(marcador.EquipoId));
                }
                else if (plantaId is { } unidad && marcador.Equipo.PlantaId != unidad)
                {
                    Conflicto("CATREADY-09-EQUIPO-UNIDAD", "Equipo",
                        "El equipo pertenece a una unidad distinta de la unidad histórica.",
                        "Asigne un equipo de la unidad correspondiente.", Referencia(marcador.EquipoId));
                }
                else
                {
                    Cumplido("CATREADY-04", "Equipo", $"Equipo {marcador.Equipo.Codigo}.");
                }
            }

            var asignacionesTurno = trabajador.AsignacionesTurno.Where(a => a.EstaVigenteEn(fecha)).ToList();
            if (asignacionesTurno.Count == 0)
            {
                Pendiente("CATREADY-06", "Turno", "No existe una programación de turno vigente.",
                    "Asigne un turno fijo o una cuadrilla desde la fecha correcta.");
                Pendiente("CATREADY-07", "Descanso", "No puede determinarse la política de descanso.",
                    "Complete la programación de turno o rotación.");
            }
            else if (asignacionesTurno.Count > 1)
            {
                Conflicto("CATREADY-09-TURNO", "Turno",
                    "Hay asignaciones de turno o cuadrilla solapadas.",
                    "Sanee las vigencias de turno.",
                    string.Join(",", asignacionesTurno.Select(a => a.Id)));
                NoAplica("CATREADY-07", "Descanso", "La política de descanso es ambigua.");
            }
            else
            {
                ResolverTurno(asignacionesTurno[0]);
            }
        }

        EvaluarPoliticaEconomica();
        EvaluarPeriodo();

        if (plantasVisibles is null)
        {
            Cumplido("CATREADY-10", "Seguridad", "Consulta sin restricción por unidad.");
        }
        else if (plantaId is { } visible && plantasVisibles.Contains(visible))
        {
            Cumplido("CATREADY-10", "Seguridad", "La unidad está dentro del alcance efectivo.");
        }
        else if (plantaId is null)
        {
            NoAplica("CATREADY-10", "Seguridad", "No existe una unidad que autorizar.");
        }

        var estado = incidencias.Any(i => i.Estado == EstadoRequisitoPreparacion.Conflicto)
            ? EstadoPreparacionTareo.Conflicto
            : incidencias.Any(i => i.Estado == EstadoRequisitoPreparacion.Pendiente)
                ? EstadoPreparacionTareo.Incompleto
                : EstadoPreparacionTareo.Listo;

        requisitos.Add(new RequisitoPreparacionTareo(
            "CATREADY-09",
            "Conflictos históricos",
            estado == EstadoPreparacionTareo.Conflicto
                ? EstadoRequisitoPreparacion.Conflicto
                : EstadoRequisitoPreparacion.Cumplido,
            estado == EstadoPreparacionTareo.Conflicto
                ? "Existen relaciones contradictorias; no se eligió una arbitrariamente."
                : "No se detectaron relaciones históricas contradictorias."));

        return new PreparacionDiaTareo(
            fecha, estado, empresaId, plantaId, planta, equipoId, idMarcador,
            turnoId, esDescanso, requisitos, incidencias);

        void ResolverTurno(AsignacionTurno asignacion)
        {
            var tieneTurno = asignacion.TurnoId is not null;
            var tieneCuadrilla = asignacion.CuadrillaId is not null;
            if (tieneTurno == tieneCuadrilla)
            {
                Conflicto("CATREADY-09-TURNO-ORIGEN", "Turno",
                    "La asignación debe indicar exactamente un turno o una cuadrilla.",
                    "Corrija el origen de la programación.", Referencia(asignacion.Id));
                NoAplica("CATREADY-07", "Descanso", "La programación es inválida.");
                return;
            }

            if (asignacion.TurnoId is { } fijo)
            {
                if (!turnos.TryGetValue(fijo, out var turno))
                {
                    Pendiente("CATREADY-06", "Turno", "El turno fijo asignado no existe.",
                        "Corrija la asignación de turno.", Referencia(fijo));
                    Pendiente("CATREADY-07", "Descanso", "No puede determinarse el calendario del turno.",
                        "Registre o corrija el turno.");
                    return;
                }

                turnoId = turno.Id;
                esDescanso = !turno.DiasAplicables.HasFlag(ADiaSemana(fecha.DayOfWeek));
                Cumplido("CATREADY-06", "Turno", $"Turno {turno.Codigo}.");
                Cumplido("CATREADY-07", "Descanso", esDescanso
                    ? "El calendario del turno identifica el día como descanso semanal."
                    : "El calendario del turno identifica el día como laborable.");
                return;
            }

            var vigentes = programaciones.GetValueOrDefault(asignacion.CuadrillaId!.Value, [])
                .Where(p => p.EstaVigenteEn(fecha))
                .ToList();
            if (vigentes.Count == 0)
            {
                Pendiente("CATREADY-06", "Turno", "La cuadrilla no tiene programación vigente.",
                    "Programe la secuencia de la cuadrilla.", Referencia(asignacion.CuadrillaId));
                Pendiente("CATREADY-07", "Descanso", "No puede determinarse trabajo o descanso.",
                    "Programe la secuencia de la cuadrilla.");
                return;
            }

            if (vigentes.Count > 1)
            {
                Conflicto("CATREADY-09-ROTACION", "Turno",
                    "La cuadrilla tiene programaciones solapadas.",
                    "Sanee las vigencias de la programación.",
                    string.Join(",", vigentes.Select(p => p.Id)));
                NoAplica("CATREADY-07", "Descanso", "La programación es ambigua.");
                return;
            }

            var programacion = vigentes[0];
            if (plantaId is { } unidad && programacion.Cuadrilla?.PlantaId != unidad)
            {
                Conflicto("CATREADY-09-CUADRILLA-UNIDAD", "Turno",
                    "La cuadrilla pertenece a una unidad distinta.",
                    "Asigne una cuadrilla de la unidad histórica.", Referencia(asignacion.CuadrillaId));
                NoAplica("CATREADY-07", "Descanso", "La cuadrilla no corresponde a la unidad.");
                return;
            }

            var resuelto = programacion.ResolverEn(fecha);
            switch (resuelto.Estado)
            {
                case EstadoProgramacionDiaria.Trabajo when resuelto.TurnoId is { } rotativo
                                                           && turnos.ContainsKey(rotativo):
                    turnoId = rotativo;
                    Cumplido("CATREADY-06", "Turno", $"Turno {turnos[rotativo].Codigo} por rotación.");
                    Cumplido("CATREADY-07", "Descanso", "La rotación identifica el día como trabajo.");
                    break;
                case EstadoProgramacionDiaria.DescansoProgramado:
                    esDescanso = true;
                    Cumplido("CATREADY-06", "Turno", "La rotación resuelve el día sin turno por descanso.");
                    Cumplido("CATREADY-07", "Descanso", "Descanso programado explícito.");
                    break;
                case EstadoProgramacionDiaria.ErrorConfiguracion:
                    Conflicto("CATREADY-09-ROTACION", "Turno",
                        resuelto.Error ?? "La rotación es inválida.",
                        "Corrija la secuencia de la cuadrilla.", Referencia(programacion.Id));
                    NoAplica("CATREADY-07", "Descanso", "La rotación es inválida.");
                    break;
                default:
                    Pendiente("CATREADY-06", "Turno", "La secuencia no cubre la fecha.",
                        "Extienda o reprograme la secuencia de la cuadrilla.", Referencia(programacion.Id));
                    Pendiente("CATREADY-07", "Descanso", "No puede determinarse trabajo o descanso.",
                        "Complete la secuencia de la cuadrilla.");
                    break;
            }
        }

        void EvaluarPoliticaEconomica()
        {
            if (esDescanso)
            {
                NoAplica("CATREADY-11", "Política económica",
                    "El descanso programado no requiere jornada ordinaria.");
                return;
            }

            if (turnoId is not { } id)
            {
                NoAplica("CATREADY-11", "Política económica",
                    "No puede evaluarse hasta resolver el turno del día.");
                return;
            }

            var aplicables = versionesTurno.GetValueOrDefault(id, [])
                .Where(v => v.EstaVigenteEn(fecha))
                .ToList();
            if (aplicables.Count == 0)
            {
                Pendiente("CATREADY-11", "Política económica",
                    "El turno no tiene una versión económica vigente para la fecha.",
                    "Registre o regularice la vigencia del turno antes de calcular.",
                    Referencia(id));
            }
            else if (aplicables.Count > 1)
            {
                Conflicto("CATREADY-09-POLITICA-ECONOMICA", "Política económica",
                    "El turno tiene versiones económicas solapadas para la fecha.",
                    "Sanee las vigencias del turno antes de calcular.",
                    string.Join(",", aplicables.Select(v => v.Id)));
            }
            else
            {
                var version = aplicables[0];
                Cumplido("CATREADY-11", "Política económica",
                    $"Versión vigente desde {version.FechaInicio:dd/MM/yyyy}; " +
                    $"{version.MinutosJornadaOrdinaria} min ordinarios y " +
                    $"{version.MinutosRefrigerio} min de refrigerio descontable.");
            }
        }

        void EvaluarPeriodo()
        {
            var aplicables = filtro.PeriodoId is { } id
                ? periodos.Where(p => p.Id == id && p.Contiene(fecha)).ToList()
                : periodos.Where(p => p.Activo && p.Contiene(fecha)).ToList();

            if (aplicables.Count == 0)
            {
                Pendiente("CATREADY-08", "Periodo", "La fecha no pertenece al periodo solicitado.",
                    "Seleccione o registre un periodo que cubra la fecha.", Referencia(filtro.PeriodoId));
                return;
            }

            if (aplicables.Count > 1)
            {
                Conflicto("CATREADY-09-PERIODO", "Periodo", "Más de un periodo cubre la fecha.",
                    "Sanee los periodos solapados.", string.Join(",", aplicables.Select(p => p.Id)));
                return;
            }

            var periodo = aplicables[0];
            if (!periodo.Activo)
            {
                Pendiente("CATREADY-08", "Periodo", "El periodo está inactivo.",
                    "Active el periodo o seleccione otro.", Referencia(periodo.Id));
            }
            else if (filtro.Operacion != OperacionPreparacionTareo.ConsultaHistorica && !periodo.EstaAbierto)
            {
                Pendiente("CATREADY-08", "Periodo",
                    $"El periodo cerrado admite consulta, no {NombreOperacion(filtro.Operacion)}.",
                    "Consulte el histórico o reabra el periodo mediante el flujo autorizado.", Referencia(periodo.Id));
            }
            else
            {
                Cumplido("CATREADY-08", "Periodo",
                    $"Periodo {periodo.Codigo} compatible con {NombreOperacion(filtro.Operacion)}.");
            }
        }

        void Cumplido(string codigo, string nombre, string explicacion) =>
            requisitos.Add(new(codigo, nombre, EstadoRequisitoPreparacion.Cumplido, explicacion));

        void NoAplica(string codigo, string nombre, string explicacion) =>
            requisitos.Add(new(codigo, nombre, EstadoRequisitoPreparacion.NoAplica, explicacion));

        void Pendiente(string codigo, string nombre, string explicacion, string accion, string? referencia = null)
        {
            requisitos.Add(new(codigo, nombre, EstadoRequisitoPreparacion.Pendiente, explicacion));
            incidencias.Add(new(codigo, EstadoRequisitoPreparacion.Pendiente, explicacion, accion, referencia));
        }

        void Conflicto(string codigo, string nombre, string explicacion, string accion, string? referencia = null)
        {
            requisitos.Add(new(codigo, nombre, EstadoRequisitoPreparacion.Conflicto, explicacion));
            incidencias.Add(new(codigo, EstadoRequisitoPreparacion.Conflicto, explicacion, accion, referencia));
        }
    }

    private static void ValidarRango(DateOnly desde, DateOnly hasta)
    {
        if (hasta < desde)
        {
            throw new ReglaNegocioException("La fecha final no puede ser anterior a la inicial.");
        }

        if (hasta.DayNumber - desde.DayNumber > 366)
        {
            throw new ReglaNegocioException("La evaluación no puede superar 367 días.");
        }
    }

    private static string NombreOperacion(OperacionPreparacionTareo operacion) => operacion switch
    {
        OperacionPreparacionTareo.ConsultaHistorica => "consulta histórica",
        OperacionPreparacionTareo.Calculo => "cálculo",
        OperacionPreparacionTareo.Recalculo => "recálculo",
        OperacionPreparacionTareo.Cierre => "cierre",
        _ => "la operación solicitada"
    };

    private static string? Referencia(long? id) =>
        id?.ToString(CultureInfo.InvariantCulture);

    private static DiasSemana ADiaSemana(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => DiasSemana.Lunes,
        DayOfWeek.Tuesday => DiasSemana.Martes,
        DayOfWeek.Wednesday => DiasSemana.Miercoles,
        DayOfWeek.Thursday => DiasSemana.Jueves,
        DayOfWeek.Friday => DiasSemana.Viernes,
        DayOfWeek.Saturday => DiasSemana.Sabado,
        DayOfWeek.Sunday => DiasSemana.Domingo,
        _ => DiasSemana.Ninguno
    };
}
