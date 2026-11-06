using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Marcaciones;

/// <summary>Conciliación explicable y estrictamente de solo lectura.</summary>
internal sealed class ServicioConciliacionBiometrica(
    MarcacionDbContext contexto,
    IProveedorAlcancePlantas alcance) : IServicioConciliacionBiometrica
{
    public async Task<ResultadoConciliacionBiometrica> ConciliarAsync(
        SolicitudConciliacionBiometrica solicitud,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(solicitud.Lectura);
        ArgumentNullException.ThrowIfNull(solicitud.Evidencias);

        if (solicitud.EquipoId <= 0
            || solicitud.Lectura.Estado == EstadoArchivoUsuariosZkteco.Rechazado
            || solicitud.Lectura.Equipo.EquipoId != solicitud.EquipoId)
        {
            throw new ReglaNegocioException(
                "La lectura no corresponde a un equipo y archivo válidos para conciliar.");
        }

        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);
        if (plantasVisibles is { Count: 0 })
        {
            throw new RecursoNoEncontradoException("el equipo autorizado", solicitud.EquipoId);
        }

        var equipos = contexto.EquiposMarcador.AsNoTracking().Where(e => e.Id == solicitud.EquipoId);
        if (plantasVisibles is not null)
        {
            equipos = equipos.Where(e => plantasVisibles.Contains(e.PlantaId));
        }

        var equipo = await equipos
            .Select(e => new { e.Id, e.Codigo, e.PlantaId })
            .SingleOrDefaultAsync(cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el equipo autorizado", solicitud.EquipoId);

        if (!string.Equals(
                solicitud.Lectura.Equipo.Codigo.Trim(),
                equipo.Codigo,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ReglaNegocioException(
                "El código del equipo leído no coincide con el equipo registrado.");
        }

        var evidenciasPorRegistro = solicitud.Evidencias
            .GroupBy(e => e.IndiceRegistro)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var dnisDuplicadosEnArchivo = solicitud.Lectura.Usuarios
            .Where(u => EsDniCandidato(u.NombreOriginal))
            .GroupBy(u => u.NombreOriginal!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
        var entradas = solicitud.Lectura.Usuarios
            .OrderBy(u => u.IndiceRegistro)
            .Select(u => PrepararEntrada(
                u,
                evidenciasPorRegistro.GetValueOrDefault(u.IndiceRegistro),
                u.NombreOriginal is not null && dnisDuplicadosEnArchivo.Contains(u.NombreOriginal)))
            .ToArray();
        var documentos = entradas
            .Where(e => e.Documento is not null)
            .Select(e => e.Documento!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var trabajadores = await ConsultarTrabajadoresAsync(
                documentos,
                equipo.PlantaId,
                solicitud.FechaEfectiva,
                cancelacion)
            .ConfigureAwait(false);
        var trabajadoresPorDocumento = trabajadores
            .GroupBy(t => t.NumeroDocumento, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var tiposDni = (await contexto.TiposDocumento.AsNoTracking()
                .Where(t => t.Codigo == "DNI")
                .Select(t => t.Id)
                .ToArrayAsync(cancelacion)
                .ConfigureAwait(false))
            .ToHashSet();
        var idsTrabajadores = trabajadores.Select(t => t.Id).Distinct().ToArray();
        var pins = entradas.Select(e => e.Usuario.Pin).Where(p => p.Length > 0).Distinct().ToArray();
        var asignaciones = await ConsultarAsignacionesAsync(
                solicitud.EquipoId,
                pins,
                idsTrabajadores,
                cancelacion)
            .ConfigureAwait(false);
        var pinsDuplicados = entradas
            .Where(e => e.Usuario.Pin.Length > 0)
            .GroupBy(e => e.Usuario.Pin, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        var resultados = entradas.Select(entrada => Clasificar(
                entrada,
                trabajadoresPorDocumento,
                asignaciones,
                pinsDuplicados,
                tiposDni,
                solicitud.EquipoId,
                solicitud.FechaEfectiva,
                solicitud.NaturalezaVigencia))
            .ToArray();

        return new(
            equipo.Id,
            equipo.Codigo,
            equipo.PlantaId,
            solicitud.FechaEfectiva,
            solicitud.NaturalezaVigencia,
            resultados);
    }

    private async Task<TrabajadorPlano[]> ConsultarTrabajadoresAsync(
        string[] documentos,
        long plantaId,
        DateOnly? fecha,
        CancellationToken cancelacion)
    {
        if (documentos.Length == 0)
        {
            return [];
        }

        var consulta = contexto.Trabajadores.AsNoTracking()
            .Where(t => documentos.Contains(t.NumeroDocumento));
        consulta = fecha is { } dia
            ? consulta.Where(t => t.PeriodosLaborales.Any(p =>
                p.FechaIngreso <= dia
                && (p.FechaCese == null || p.FechaCese >= dia)
                && p.AsignacionesPlanta.Any(a =>
                    a.PlantaId == plantaId
                    && a.FechaInicio <= dia
                    && (a.FechaFin == null || a.FechaFin >= dia))))
            : consulta.Where(t => t.PlantaId == plantaId);

        return await consulta
            .Select(t => new TrabajadorPlano(t.Id, t.TipoDocumentoId, t.NumeroDocumento))
            .ToArrayAsync(cancelacion)
            .ConfigureAwait(false);
    }

    private async Task<AsignacionPlano[]> ConsultarAsignacionesAsync(
        long equipoId,
        string[] pins,
        long[] trabajadores,
        CancellationToken cancelacion)
    {
        if (pins.Length == 0 && trabajadores.Length == 0)
        {
            return [];
        }

        return await contexto.AsignacionesMarcador.AsNoTracking()
            .Where(a => (a.EquipoId == equipoId && pins.Contains(a.IdMarcador))
                || trabajadores.Contains(a.TrabajadorId))
            .Select(a => new AsignacionPlano(
                a.TrabajadorId,
                a.EquipoId,
                a.IdMarcador,
                a.FechaInicio,
                a.FechaFin))
            .ToArrayAsync(cancelacion)
            .ConfigureAwait(false);
    }

    private static Entrada PrepararEntrada(
        UsuarioZktecoLeido usuario,
        EvidenciaDocumentoConciliacion[]? evidencias,
        bool dniDuplicadoEnArchivo)
    {
        evidencias ??= [];
        var validas = evidencias
            .Where(e => (e.Fuente is FuenteEvidenciaDocumento.PadronControlado
                    or FuenteEvidenciaDocumento.VerificacionDocumental)
                && !string.IsNullOrWhiteSpace(e.NumeroDocumento))
            .ToArray();
        var evidenciaAmbigua = validas
            .Select(e => new { Documento = e.NumeroDocumento.Trim(), e.TipoDocumentoId })
            .Distinct()
            .Count() > 1;
        var evidencia = evidenciaAmbigua ? null : validas.FirstOrDefault();
        var documentoName = EsDniCandidato(usuario.NombreOriginal)
            ? usuario.NombreOriginal
            : null;

        return new(
            usuario,
            evidencia?.NumeroDocumento.Trim() ?? documentoName,
            evidencia?.TipoDocumentoId,
            evidencia is not null,
            evidencia is not null
                ? BaseConfirmacionIdentidadBiometrica.EvidenciaDocumental
                : documentoName is not null
                    ? BaseConfirmacionIdentidadBiometrica.ReglaOperativaUserU
                    : BaseConfirmacionIdentidadBiometrica.Ninguna,
            evidenciaAmbigua,
            dniDuplicadoEnArchivo);
    }

    private static ResultadoRegistroConciliacionBiometrica Clasificar(
        Entrada entrada,
        IReadOnlyDictionary<string, TrabajadorPlano[]> trabajadoresPorDocumento,
        AsignacionPlano[] asignaciones,
        HashSet<string> pinsDuplicados,
        HashSet<long> tiposDni,
        long equipoId,
        DateOnly? fecha,
        NaturalezaVigenciaBiometrica naturalezaVigencia)
    {
        var diagnosticos = new HashSet<DiagnosticoConciliacionBiometrica>();
        if (entrada.Usuario.Estado == EstadoRegistroUsuarioZkteco.Invalido)
        {
            diagnosticos.Add(DiagnosticoConciliacionBiometrica.RegistroInvalido);
        }

        if (pinsDuplicados.Contains(entrada.Usuario.Pin))
        {
            diagnosticos.Add(DiagnosticoConciliacionBiometrica.PinDuplicadoDentroDelArchivo);
        }
        if (entrada.Usuario.Pin.Length > 30)
        {
            diagnosticos.Add(DiagnosticoConciliacionBiometrica.PinSuperaLongitudPermitida);
        }

        if (entrada.Documento is null)
        {
            diagnosticos.Add(DiagnosticoConciliacionBiometrica.DocumentoAusente);
        }
        else if (entrada.DocumentoVerificado)
        {
            diagnosticos.Add(DiagnosticoConciliacionBiometrica.DocumentoVerificadoPorEvidenciaExterna);
        }
        else
        {
            diagnosticos.Add(DiagnosticoConciliacionBiometrica.DocumentoNoVerificado);
        }

        if (entrada.EvidenciaAmbigua)
        {
            diagnosticos.Add(DiagnosticoConciliacionBiometrica.DocumentoAmbiguo);
        }
        if (entrada.DniDuplicadoEnArchivo)
        {
            diagnosticos.Add(DiagnosticoConciliacionBiometrica.DniDuplicadoDentroDelArchivo);
        }

        TrabajadorPlano? trabajador = null;
        if (entrada.Documento is not null
            && trabajadoresPorDocumento.TryGetValue(entrada.Documento, out var coincidencias))
        {
            var filtradas = entrada.BaseConfirmacion == BaseConfirmacionIdentidadBiometrica.ReglaOperativaUserU
                ? coincidencias.Where(t => tiposDni.Contains(t.TipoDocumentoId)).ToArray()
                : entrada.TipoDocumentoId is { } tipo
                    ? coincidencias.Where(t => t.TipoDocumentoId == tipo).ToArray()
                    : coincidencias;
            if (filtradas.Length == 1)
            {
                trabajador = filtradas[0];
                if (entrada.BaseConfirmacion == BaseConfirmacionIdentidadBiometrica.ReglaOperativaUserU)
                {
                    diagnosticos.Add(DiagnosticoConciliacionBiometrica.DocumentoCoincideExactamentePorReglaOperativa);
                }
            }
            else if (filtradas.Length > 1)
            {
                diagnosticos.Add(DiagnosticoConciliacionBiometrica.DocumentoAmbiguo);
            }
            else
            {
                diagnosticos.Add(
                    entrada.BaseConfirmacion == BaseConfirmacionIdentidadBiometrica.ReglaOperativaUserU
                    && coincidencias.Length > 0
                        ? DiagnosticoConciliacionBiometrica.TipoDocumentoNoCompatibleConReglaDni
                        : DiagnosticoConciliacionBiometrica.TrabajadorNoEncontrado);
            }
        }
        else if (entrada.Documento is not null)
        {
            diagnosticos.Add(DiagnosticoConciliacionBiometrica.TrabajadorNoEncontrado);
        }

        if (fecha is null || naturalezaVigencia == NaturalezaVigenciaBiometrica.NoInformada)
        {
            diagnosticos.Add(DiagnosticoConciliacionBiometrica.FechaEfectivaNoAcreditada);
        }
        else if (naturalezaVigencia == NaturalezaVigenciaBiometrica.Declarada)
        {
            diagnosticos.Add(DiagnosticoConciliacionBiometrica.FechaEfectivaDeclaradaNoAcreditada);
        }

        if (fecha is { } dia)
        {
            if (dia > DateOnly.FromDateTime(DateTime.Today))
            {
                diagnosticos.Add(DiagnosticoConciliacionBiometrica.FechaEfectivaFutura);
            }

            var pinVigente = asignaciones.Where(a =>
                a.EquipoId == equipoId
                && a.Pin == entrada.Usuario.Pin
                && a.EstaVigenteEn(dia)).ToArray();
            if (pinVigente.Length == 0)
            {
                diagnosticos.Add(DiagnosticoConciliacionBiometrica.PinSinAsignacionVigente);
            }
            else if (pinVigente.Length > 1)
            {
                diagnosticos.Add(DiagnosticoConciliacionBiometrica.AsignacionesSuperpuestas);
            }

            if (trabajador is not null)
            {
                if (pinVigente.Any(a => a.TrabajadorId != trabajador.Id))
                {
                    diagnosticos.Add(DiagnosticoConciliacionBiometrica.PinAsignadoAOtroTrabajador);
                }

                var delTrabajador = asignaciones
                    .Where(a => a.TrabajadorId == trabajador.Id && a.EstaVigenteEn(dia))
                    .ToArray();
                if (delTrabajador.Length > 1)
                {
                    diagnosticos.Add(DiagnosticoConciliacionBiometrica.AsignacionesSuperpuestas);
                }

                if (delTrabajador.Any(a => a.EquipoId != equipoId))
                {
                    diagnosticos.Add(DiagnosticoConciliacionBiometrica.TrabajadorAsignadoAOtroEquipo);
                }

                if (delTrabajador.Any(a => a.EquipoId == equipoId && a.Pin != entrada.Usuario.Pin))
                {
                    diagnosticos.Add(DiagnosticoConciliacionBiometrica.TrabajadorAsignadoAOtroPin);
                }

                if (pinVigente.Any(a => a.TrabajadorId == trabajador.Id)
                    && delTrabajador.Any(a => a.EquipoId == equipoId && a.Pin == entrada.Usuario.Pin))
                {
                    diagnosticos.Add(DiagnosticoConciliacionBiometrica.AsignacionCorrectaExistente);
                }
            }
        }

        var estado = EstadoPrincipal(entrada, trabajador, diagnosticos);
        return new(
            entrada.Usuario.IndiceRegistro,
            entrada.Usuario.Uid,
            entrada.Usuario.Pin,
            entrada.Usuario.NombreOriginal,
            entrada.Documento,
            entrada.DocumentoVerificado,
            entrada.BaseConfirmacion,
            trabajador?.Id,
            estado,
            diagnosticos.OrderBy(d => d).ToArray());
    }

    private static EstadoConciliacionBiometrica EstadoPrincipal(
        Entrada entrada,
        TrabajadorPlano? trabajador,
        HashSet<DiagnosticoConciliacionBiometrica> diagnosticos)
    {
        if (diagnosticos.Contains(DiagnosticoConciliacionBiometrica.RegistroInvalido))
        {
            return EstadoConciliacionBiometrica.RegistroInvalidoOIncompleto;
        }
        if (diagnosticos.Contains(DiagnosticoConciliacionBiometrica.PinDuplicadoDentroDelArchivo))
        {
            return EstadoConciliacionBiometrica.PinDuplicadoDentroDelEquipo;
        }
        if (diagnosticos.Contains(DiagnosticoConciliacionBiometrica.PinSuperaLongitudPermitida))
        {
            return EstadoConciliacionBiometrica.RegistroInvalidoOIncompleto;
        }
        if (diagnosticos.Contains(DiagnosticoConciliacionBiometrica.DocumentoAusente))
        {
            return EstadoConciliacionBiometrica.DocumentoAusente;
        }
        if (diagnosticos.Contains(DiagnosticoConciliacionBiometrica.DocumentoAmbiguo))
        {
            return EstadoConciliacionBiometrica.DocumentoAmbiguoODuplicado;
        }
        if (diagnosticos.Contains(DiagnosticoConciliacionBiometrica.DniDuplicadoDentroDelArchivo))
        {
            return EstadoConciliacionBiometrica.DocumentoAmbiguoODuplicado;
        }
        if (diagnosticos.Contains(DiagnosticoConciliacionBiometrica.TipoDocumentoNoCompatibleConReglaDni))
        {
            return EstadoConciliacionBiometrica.CandidatoDocumentoNoVerificado;
        }
        if (diagnosticos.Contains(DiagnosticoConciliacionBiometrica.TrabajadorNoEncontrado))
        {
            return EstadoConciliacionBiometrica.TrabajadorNoEncontrado;
        }
        if (diagnosticos.Contains(DiagnosticoConciliacionBiometrica.PinAsignadoAOtroTrabajador))
        {
            return EstadoConciliacionBiometrica.PinAsignadoAOtroTrabajador;
        }
        if (diagnosticos.Contains(DiagnosticoConciliacionBiometrica.TrabajadorAsignadoAOtroEquipo))
        {
            return EstadoConciliacionBiometrica.TrabajadorAsignadoAOtroEquipo;
        }
        if (diagnosticos.Contains(DiagnosticoConciliacionBiometrica.AsignacionesSuperpuestas)
            || diagnosticos.Contains(DiagnosticoConciliacionBiometrica.TrabajadorAsignadoAOtroPin))
        {
            return EstadoConciliacionBiometrica.ConflictoDeVigencia;
        }
        if (diagnosticos.Contains(DiagnosticoConciliacionBiometrica.FechaEfectivaNoAcreditada))
        {
            return EstadoConciliacionBiometrica.PropuestaCondicionada;
        }
        if (diagnosticos.Contains(DiagnosticoConciliacionBiometrica.AsignacionCorrectaExistente))
        {
            return EstadoConciliacionBiometrica.YaVinculadoCorrectamente;
        }
        if (trabajador is null)
        {
            return EstadoConciliacionBiometrica.IdentificadorBiometricoSinCorrespondencia;
        }

        return entrada.BaseConfirmacion switch
        {
            BaseConfirmacionIdentidadBiometrica.EvidenciaDocumental =>
                EstadoConciliacionBiometrica.CoincidenciaVerificable,
            BaseConfirmacionIdentidadBiometrica.ReglaOperativaUserU =>
                EstadoConciliacionBiometrica.CoincidenciaOperativaUserUConfirmable,
            _ => EstadoConciliacionBiometrica.CandidatoDocumentoNoVerificado
        };
    }

    private static bool EsDniCandidato(string? valor) =>
        valor is { Length: 8 } && valor.All(char.IsAsciiDigit);

    private sealed record Entrada(
        UsuarioZktecoLeido Usuario,
        string? Documento,
        long? TipoDocumentoId,
        bool DocumentoVerificado,
        BaseConfirmacionIdentidadBiometrica BaseConfirmacion,
        bool EvidenciaAmbigua,
        bool DniDuplicadoEnArchivo);

    private sealed record TrabajadorPlano(long Id, long TipoDocumentoId, string NumeroDocumento);

    private sealed record AsignacionPlano(
        long TrabajadorId,
        long EquipoId,
        string Pin,
        DateOnly FechaInicio,
        DateOnly? FechaFin)
    {
        public bool EstaVigenteEn(DateOnly fecha) =>
            fecha >= FechaInicio && (FechaFin is null || fecha <= FechaFin.Value);
    }
}
