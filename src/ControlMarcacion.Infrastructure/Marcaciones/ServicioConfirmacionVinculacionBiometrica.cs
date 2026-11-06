using System.Security.Cryptography;
using System.Text;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Marcaciones;

internal sealed class ServicioConfirmacionVinculacionBiometrica(
    MarcacionDbContext contexto,
    IServicioConciliacionBiometrica conciliador,
    IProveedorEvidenciaIdentidad evidencias,
    IContextoAutorizacionVinculacionBiometrica autorizacion,
    IProveedorAlcancePlantas alcance) : IServicioConfirmacionVinculacionBiometrica
{
    private static readonly TimeSpan VigenciaPropuesta = TimeSpan.FromMinutes(30);
    private const int VersionPoliticaIdentidad = 1;

    public async Task<PropuestaConfirmacionBiometricaDto> PrepararAsync(
        PrepararConfirmacionBiometricaRequest solicitud,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(solicitud.Lectura);
        ArgumentNullException.ThrowIfNull(solicitud.Evidencias);
        ValidarAutorizacion();
        if (solicitud.FechaInicio == default)
        {
            throw new ReglaNegocioException("Debe indicarse una fecha efectiva explícita.");
        }
        if (solicitud.FechaFin is { } fin && fin < solicitud.FechaInicio)
        {
            throw new ReglaNegocioException("La fecha final no puede ser anterior a la fecha inicial.");
        }
        if (solicitud.Lectura.Estado == EstadoArchivoUsuariosZkteco.Rechazado
            || solicitud.Lectura.Usuarios.Count == 0
            || solicitud.Lectura.FormatoDetectado != "user.u/table=1/fields=1..11"
            || solicitud.Lectura.HuellaContenidoSha256 is not { Length: 64 }
            || !solicitud.Lectura.HuellaContenidoSha256.All(Uri.IsHexDigit)
            || solicitud.Lectura.TamanoBytes <= 0
            || solicitud.Lectura.LeidoEnUtc == default
            || solicitud.Lectura.LeidoEnUtc > DateTime.UtcNow.AddMinutes(5))
        {
            throw new ReglaNegocioException("El respaldo user.u no tiene una lectura íntegra y trazable.");
        }

        var equipo = await ObtenerEquipoAutorizadoAsync(solicitud.EquipoId, cancelacion).ConfigureAwait(false);
        if (solicitud.Lectura.Equipo.EquipoId != equipo.Id
            || !string.Equals(
                solicitud.Lectura.Equipo.Codigo,
                equipo.Codigo,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ReglaNegocioException("La lectura no corresponde al equipo seleccionado.");
        }

        var ahora = DateTime.Now;
        var publico = Guid.NewGuid();
        var referencias = solicitud.Evidencias.GroupBy(e => e.IndiceRegistro)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var lote = new ConciliacionBiometrica
        {
            IdentificadorPublico = publico,
            EquipoId = equipo.Id,
            PlantaId = equipo.PlantaId,
            Version = 1,
            HuellaEstado = string.Empty,
            HuellaRespaldo = solicitud.Lectura.HuellaContenidoSha256,
            TamanoRespaldo = solicitud.Lectura.TamanoBytes,
            FormatoRespaldo = solicitud.Lectura.FormatoDetectado,
            RespaldoLeidoEnUtc = solicitud.Lectura.LeidoEnUtc,
            VersionPoliticaIdentidad = VersionPoliticaIdentidad,
            ExpiraEn = ahora.Add(VigenciaPropuesta),
            Estado = EstadoConciliacionPersistida.Pendiente,
            CreadoPor = autorizacion.Usuario
        };

        foreach (var usuario in solicitud.Lectura.Usuarios.OrderBy(u => u.IndiceRegistro))
        {
            var referencia = referencias.GetValueOrDefault(usuario.IndiceRegistro);
            var decision = await PrepararDecisionAsync(
                    lote,
                    usuario,
                    referencia,
                    solicitud,
                    ahora,
                    cancelacion)
                .ConfigureAwait(false);
            lote.Decisiones.Add(decision);
        }

        lote.HuellaEstado = Hash(string.Join('|', lote.Decisiones.Select(d => d.HuellaEstado)));
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion).ConfigureAwait(false);
        contexto.ConciliacionesBiometricas.Add(lote);
        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

        return await ProyectarAsync(lote, cancelacion).ConfigureAwait(false);
    }

    public async Task<ResultadoConfirmacionBiometricaLote> ConfirmarLoteAsync(
        ConfirmarVinculacionesBiometricasRequest solicitud,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ValidarAutorizacion();
        var cabecera = await contexto.ConciliacionesBiometricas.AsNoTracking()
            .SingleOrDefaultAsync(c => c.IdentificadorPublico == solicitud.ConciliacionId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("la conciliación", solicitud.ConciliacionId);
        await GarantizarPlantaAutorizadaAsync(cabecera.PlantaId, cancelacion).ConfigureAwait(false);

        if (cabecera.Version != solicitud.Version)
        {
            throw new ReglaNegocioException("La versión de la conciliación está desactualizada.");
        }

        var ids = solicitud.Decisiones.Distinct().ToArray();
        var pertenecen = await contexto.DecisionesVinculacionBiometrica.AsNoTracking()
            .CountAsync(d => d.ConciliacionId == cabecera.Id && ids.Contains(d.Id), cancelacion)
            .ConfigureAwait(false);
        if (pertenecen != ids.Length)
        {
            throw new ReglaNegocioException("La solicitud contiene decisiones ajenas o inexistentes.");
        }

        var resultados = new List<ResultadoDecisionConfirmacionBiometrica>(ids.Length);
        foreach (var id in ids)
        {
            try
            {
                resultados.Add(await ConfirmarUnaAsync(cabecera.Id, id, cancelacion).ConfigureAwait(false));
            }
            catch (Exception excepcion) when (excepcion is DbUpdateException or SqliteException)
            {
                contexto.ChangeTracker.Clear();
                resultados.Add(await RegistrarFalloTecnicoAsync(cabecera.Id, id, cancelacion).ConfigureAwait(false));
            }
        }

        await ActualizarEstadoLoteAsync(cabecera.Id, cancelacion).ConfigureAwait(false);
        return new(solicitud.ConciliacionId, solicitud.Version, resultados);
    }

    private async Task<DecisionVinculacionBiometrica> PrepararDecisionAsync(
        ConciliacionBiometrica lote,
        UsuarioZktecoLeido usuario,
        ReferenciaEvidenciaRegistro[]? referencias,
        PrepararConfirmacionBiometricaRequest solicitud,
        DateTime ahora,
        CancellationToken cancelacion)
    {
        var referencia = referencias is { Length: 1 } ? referencias[0] : null;
        var evidencia = referencia is null
            ? null
            : await evidencias.VerificarAsync(referencia.Fuente, referencia.Referencia, cancelacion).ConfigureAwait(false);
        var referenciaPersistida = evidencia?.Referencia
            ?? $"HASH:{Hash(referencia?.Referencia ?? string.Empty)}";
        var decision = new DecisionVinculacionBiometrica
        {
            IndiceRegistro = usuario.IndiceRegistro,
            UidRegistro = usuario.Uid,
            EquipoId = solicitud.EquipoId,
            Pin = usuario.Pin,
            FechaInicio = solicitud.FechaInicio,
            FechaFin = solicitud.FechaFin,
            BaseConfirmacion = BaseConfirmacionVinculacion.Ninguna,
            NaturalezaVigencia = NaturalezaVigenciaVinculacion.NoInformada,
            CategoriaEvidencia = (referencia?.Fuente ?? FuenteEvidenciaDocumento.Ninguna).ToString(),
            ReferenciaEvidencia = referenciaPersistida,
            HuellaEvidencia = evidencia?.Huella ?? Hash(string.Empty),
            EvidenciaVerificadaEn = evidencia?.VerificadaEn ?? ahora,
            EvidenciaExpiraEn = evidencia?.ExpiraEn,
            Resultado = ResultadoDecisionVinculacion.EvidenciaInsuficiente,
            MotivoResultado = "No existe evidencia servidor válida y vigente.",
            HuellaEstado = Hash($"INVALIDA|{usuario.IndiceRegistro}|{usuario.Pin}"),
            ClaveIdempotencia = Hash($"{lote.IdentificadorPublico}|{usuario.IndiceRegistro}|{usuario.Pin}"),
            CreadoPor = autorizacion.Usuario
        };

        if (referencias is null or { Length: 0 })
        {
            return await PrepararDecisionOperativaAsync(
                    lote, decision, usuario, solicitud, ahora, cancelacion)
                .ConfigureAwait(false);
        }

        if (referencia is null)
        {
            decision.MotivoResultado = "Existen varias referencias de evidencia para el mismo registro.";
            return decision;
        }

        if (!EvidenciaValida(evidencia, referencia, solicitud.FechaInicio, solicitud.FechaFin, ahora))
        {
            return decision;
        }

        decision.TrabajadorId = evidencia!.TrabajadorId;
        decision.BaseConfirmacion = BaseConfirmacionVinculacion.EvidenciaDocumental;
        decision.NaturalezaVigencia = solicitud.FechaInicio < DateOnly.FromDateTime(ahora)
            ? NaturalezaVigenciaVinculacion.Acreditada
            : NaturalezaVigenciaVinculacion.Declarada;
        decision.CategoriaEvidencia = evidencia.Fuente.ToString();
        decision.ReferenciaEvidencia = evidencia.Referencia;
        decision.HuellaEvidencia = evidencia.Huella;
        decision.EvidenciaVerificadaEn = evidencia.VerificadaEn;
        decision.EvidenciaExpiraEn = evidencia.ExpiraEn;

        var lectura = solicitud.Lectura with { Usuarios = [usuario] };
        var simulacion = await conciliador.ConciliarAsync(
                new SolicitudConciliacionBiometrica(
                    solicitud.EquipoId,
                    lectura,
                    solicitud.FechaInicio,
                    decision.NaturalezaVigencia == NaturalezaVigenciaVinculacion.Acreditada
                        ? NaturalezaVigenciaBiometrica.Acreditada
                        : NaturalezaVigenciaBiometrica.Declarada,
                    [new EvidenciaDocumentoConciliacion(
                        usuario.IndiceRegistro,
                        evidencia.NumeroDocumento,
                        evidencia.TipoDocumentoId,
                        evidencia.Fuente)]),
                cancelacion)
            .ConfigureAwait(false);
        var resultado = simulacion.Registros.Single();
        decision.Diagnosticos = string.Join(',', resultado.Diagnosticos);
        if (resultado.TrabajadorId != evidencia.TrabajadorId)
        {
            decision.Resultado = ResultadoDecisionVinculacion.Rechazada;
            decision.MotivoResultado = "La evidencia no corresponde a un trabajador único de la planta.";
        }
        else if (resultado.Estado == EstadoConciliacionBiometrica.YaVinculadoCorrectamente)
        {
            decision.Resultado = ResultadoDecisionVinculacion.YaVinculada;
            decision.MotivoResultado = "La vinculación ya existe en la fecha solicitada.";
        }
        else if (resultado.Estado != EstadoConciliacionBiometrica.CoincidenciaVerificable)
        {
            decision.Resultado = ResultadoDecisionVinculacion.ConflictoVigencia;
            decision.MotivoResultado = $"La conciliación no es confirmable: {resultado.Estado}.";
        }
        else if (await ExisteSolapamientoAsync(
                     evidencia.TrabajadorId,
                     solicitud.EquipoId,
                     usuario.Pin,
                     solicitud.FechaInicio,
                     solicitud.FechaFin,
                     cancelacion).ConfigureAwait(false))
        {
            decision.Resultado = ResultadoDecisionVinculacion.ConflictoVigencia;
            decision.MotivoResultado = "Existe una asignación superpuesta en el intervalo solicitado.";
        }
        else
        {
            decision.Resultado = ResultadoDecisionVinculacion.Pendiente;
            decision.MotivoResultado = null;
        }

        decision.HuellaEstado = await CalcularHuellaEstadoAsync(decision, cancelacion).ConfigureAwait(false);
        decision.ClaveIdempotencia = Hash(
            $"{lote.IdentificadorPublico}|{usuario.IndiceRegistro}|{evidencia.TrabajadorId}|" +
            $"{solicitud.EquipoId}|{usuario.Pin}|{solicitud.FechaInicio:yyyyMMdd}|{solicitud.FechaFin:yyyyMMdd}");
        return decision;
    }

    private async Task<DecisionVinculacionBiometrica> PrepararDecisionOperativaAsync(
        ConciliacionBiometrica lote,
        DecisionVinculacionBiometrica decision,
        UsuarioZktecoLeido usuario,
        PrepararConfirmacionBiometricaRequest solicitud,
        DateTime ahora,
        CancellationToken cancelacion)
    {
        decision.BaseConfirmacion = BaseConfirmacionVinculacion.ReglaOperativaUserU;
        decision.NaturalezaVigencia = NaturalezaVigenciaVinculacion.Declarada;
        decision.CategoriaEvidencia = BaseConfirmacionVinculacion.ReglaOperativaUserU.ToString();
        decision.ReferenciaEvidencia = $"USERU:{lote.IdentificadorPublico:N}:{usuario.IndiceRegistro}";
        decision.EvidenciaVerificadaEn = ahora;
        decision.EvidenciaExpiraEn = lote.ExpiraEn;

        var lectura = solicitud.Lectura with { Usuarios = [usuario] };
        var simulacion = await conciliador.ConciliarAsync(
                new SolicitudConciliacionBiometrica(
                    solicitud.EquipoId,
                    lectura,
                    solicitud.FechaInicio,
                    NaturalezaVigenciaBiometrica.Declarada,
                    []),
                cancelacion)
            .ConfigureAwait(false);
        var resultado = simulacion.Registros.Single();
        decision.TrabajadorId = resultado.TrabajadorId;
        decision.Diagnosticos = string.Join(',', resultado.Diagnosticos);

        if (resultado.TrabajadorId is { } trabajadorId)
        {
            var identidad = await contexto.Trabajadores.AsNoTracking()
                .Where(t => t.Id == trabajadorId)
                .Select(t => new { t.TipoDocumentoId, t.NumeroDocumento })
                .SingleAsync(cancelacion)
                .ConfigureAwait(false);
            decision.HuellaEvidencia = HuellaOperativa(
                lote, decision, usuario, identidad.NumeroDocumento, identidad.TipoDocumentoId);
        }

        if (resultado.Estado == EstadoConciliacionBiometrica.YaVinculadoCorrectamente)
        {
            decision.Resultado = ResultadoDecisionVinculacion.YaVinculada;
            decision.MotivoResultado = "La vinculación ya existe en la fecha solicitada.";
        }
        else if (resultado.Estado != EstadoConciliacionBiometrica.CoincidenciaOperativaUserUConfirmable)
        {
            decision.Resultado = EsConflicto(resultado.Estado)
                ? ResultadoDecisionVinculacion.ConflictoVigencia
                : ResultadoDecisionVinculacion.RevisionManual;
            decision.MotivoResultado = $"La coincidencia operativa requiere revisión: {resultado.Estado}.";
        }
        else if (solicitud.FechaInicio < DateOnly.FromDateTime(ahora))
        {
            decision.Resultado = ResultadoDecisionVinculacion.RevisionManual;
            decision.MotivoResultado =
                "user.u no acredita una fecha histórica de enrolamiento; se requiere control específico.";
        }
        else if (!await TrabajadorVigenteEnPlantaAsync(
                     resultado.TrabajadorId!.Value,
                     lote.PlantaId,
                     solicitud.FechaInicio,
                     solicitud.FechaFin,
                     cancelacion).ConfigureAwait(false))
        {
            decision.Resultado = ResultadoDecisionVinculacion.RevisionManual;
            decision.MotivoResultado =
                "La vigencia laboral o de planta no cubre íntegramente el intervalo declarado.";
        }
        else if (await ExisteSolapamientoAsync(
                     resultado.TrabajadorId!.Value,
                     solicitud.EquipoId,
                     usuario.Pin,
                     solicitud.FechaInicio,
                     solicitud.FechaFin,
                     cancelacion).ConfigureAwait(false))
        {
            decision.Resultado = ResultadoDecisionVinculacion.ConflictoVigencia;
            decision.MotivoResultado = "Existe una asignación superpuesta en el intervalo solicitado.";
        }
        else
        {
            decision.Resultado = ResultadoDecisionVinculacion.Pendiente;
            decision.MotivoResultado = null;
        }

        decision.HuellaEstado = await CalcularHuellaEstadoAsync(decision, cancelacion).ConfigureAwait(false);
        decision.ClaveIdempotencia = Hash(
            $"{lote.IdentificadorPublico}|{usuario.IndiceRegistro}|{decision.TrabajadorId}|" +
            $"{solicitud.EquipoId}|{usuario.Pin}|{solicitud.FechaInicio:yyyyMMdd}|{solicitud.FechaFin:yyyyMMdd}");
        return decision;
    }

    private async Task<ResultadoDecisionConfirmacionBiometrica> ConfirmarUnaAsync(
        long conciliacionId,
        long decisionId,
        CancellationToken cancelacion)
    {
        await contexto.Database.OpenConnectionAsync(cancelacion).ConfigureAwait(false);
        await using var transaccionSqlite = ((SqliteConnection)contexto.Database.GetDbConnection())
            .BeginTransaction(deferred: false);
        await contexto.Database.UseTransactionAsync(transaccionSqlite, cancelacion).ConfigureAwait(false);
        try
        {
            var decision = await contexto.DecisionesVinculacionBiometrica
                .Include(d => d.Conciliacion)
                .SingleAsync(d => d.Id == decisionId && d.ConciliacionId == conciliacionId, cancelacion)
                .ConfigureAwait(false);
            if (decision.Resultado != ResultadoDecisionVinculacion.Pendiente)
            {
                await transaccionSqlite.CommitAsync(cancelacion).ConfigureAwait(false);
                return Resultado(decision);
            }

            if (decision.Conciliacion!.ExpiraEn <= DateTime.Now)
            {
                decision.Resultado = ResultadoDecisionVinculacion.PropuestaDesactualizada;
                decision.MotivoResultado = "La propuesta expiró; ejecute una nueva conciliación.";
                await GuardarResultadoAsync(decision, cancelacion).ConfigureAwait(false);
                await transaccionSqlite.CommitAsync(cancelacion).ConfigureAwait(false);
                return Resultado(decision);
            }

            var evidenciaCoincide = decision.BaseConfirmacion == BaseConfirmacionVinculacion.ReglaOperativaUserU
                ? await EvidenciaOperativaCoincideAsync(decision, cancelacion).ConfigureAwait(false)
                : await EvidenciaDocumentalCoincideAsync(decision, cancelacion).ConfigureAwait(false);
            if (!evidenciaCoincide)
            {
                decision.Resultado = ResultadoDecisionVinculacion.EvidenciaInsuficiente;
                decision.MotivoResultado = "La base de confirmación fue retirada, venció o cambió.";
                await GuardarResultadoAsync(decision, cancelacion).ConfigureAwait(false);
                await transaccionSqlite.CommitAsync(cancelacion).ConfigureAwait(false);
                return Resultado(decision);
            }

            var huellaActual = await CalcularHuellaEstadoAsync(decision, cancelacion).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(huellaActual),
                    Encoding.ASCII.GetBytes(decision.HuellaEstado)))
            {
                decision.Resultado = ResultadoDecisionVinculacion.PropuestaDesactualizada;
                decision.MotivoResultado = "Las asignaciones cambiaron desde la conciliación.";
                await GuardarResultadoAsync(decision, cancelacion).ConfigureAwait(false);
                await transaccionSqlite.CommitAsync(cancelacion).ConfigureAwait(false);
                return Resultado(decision);
            }

            if (await ExisteSolapamientoAsync(
                    decision.TrabajadorId!.Value,
                    decision.EquipoId,
                    decision.Pin,
                    decision.FechaInicio,
                    decision.FechaFin,
                    cancelacion).ConfigureAwait(false))
            {
                decision.Resultado = ResultadoDecisionVinculacion.ConflictoVigencia;
                decision.MotivoResultado = "Existe una asignación superpuesta.";
                await GuardarResultadoAsync(decision, cancelacion).ConfigureAwait(false);
                await transaccionSqlite.CommitAsync(cancelacion).ConfigureAwait(false);
                return Resultado(decision);
            }

            var asignacion = new AsignacionMarcador
            {
                TrabajadorId = decision.TrabajadorId.Value,
                EquipoId = decision.EquipoId,
                IdMarcador = decision.Pin,
                FechaInicio = decision.FechaInicio,
                FechaFin = decision.FechaFin,
                CreadoPor = autorizacion.Usuario
            };
            contexto.AsignacionesMarcador.Add(asignacion);
            decision.Resultado = ResultadoDecisionVinculacion.Creada;
            decision.MotivoResultado = null;
            decision.ConfirmadaPor = autorizacion.Usuario;
            decision.ConfirmadaEn = DateTime.Now;
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            decision.AsignacionMarcadorId = asignacion.Id;
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            await transaccionSqlite.CommitAsync(cancelacion).ConfigureAwait(false);
            return Resultado(decision);
        }
        finally
        {
            contexto.Database.UseTransaction(null);
            await contexto.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    private async Task<string> CalcularHuellaEstadoAsync(
        DecisionVinculacionBiometrica decision,
        CancellationToken cancelacion)
    {
        var asignaciones = await contexto.AsignacionesMarcador.AsNoTracking()
            .Where(a => (decision.TrabajadorId != null && a.TrabajadorId == decision.TrabajadorId)
                || (a.EquipoId == decision.EquipoId && a.IdMarcador == decision.Pin))
            .OrderBy(a => a.TrabajadorId).ThenBy(a => a.EquipoId).ThenBy(a => a.IdMarcador).ThenBy(a => a.FechaInicio)
            .Select(a => new { a.Id, a.TrabajadorId, a.EquipoId, a.IdMarcador, a.FechaInicio, a.FechaFin })
            .ToArrayAsync(cancelacion)
            .ConfigureAwait(false);
        var estado = asignaciones.Select(a =>
            $"{a.Id}:{a.TrabajadorId}:{a.EquipoId}:{a.IdMarcador}:{a.FechaInicio}:{a.FechaFin}");
        return Hash(
            $"{decision.TrabajadorId}|{decision.EquipoId}|{decision.Pin}|{decision.FechaInicio}|" +
            $"{decision.FechaFin}|{decision.HuellaEvidencia}|{string.Join(';', estado)}");
    }

    private async Task<bool> ExisteSolapamientoAsync(
        long trabajadorId,
        long equipoId,
        string pin,
        DateOnly inicio,
        DateOnly? fin,
        CancellationToken cancelacion) =>
        await contexto.AsignacionesMarcador.AsNoTracking().AnyAsync(a =>
            (a.TrabajadorId == trabajadorId || (a.EquipoId == equipoId && a.IdMarcador == pin))
            && (fin == null || a.FechaInicio <= fin)
            && (a.FechaFin == null || a.FechaFin >= inicio), cancelacion).ConfigureAwait(false);

    private static bool EvidenciaValida(
        EvidenciaIdentidadVerificada? evidencia,
        ReferenciaEvidenciaRegistro? referencia,
        DateOnly inicio,
        DateOnly? fin,
        DateTime ahora)
    {
        if (evidencia is null || referencia is null
            || evidencia.Fuente != referencia.Fuente
            || !string.Equals(evidencia.Referencia, referencia.Referencia, StringComparison.Ordinal)
            || evidencia.ExpiraEn is { } vence && vence <= ahora)
        {
            return false;
        }

        if (inicio >= DateOnly.FromDateTime(ahora))
        {
            return true;
        }

        return evidencia.AcreditaHistoriaDesde is { } desde
            && desde <= inicio
            && evidencia.AcreditaHistoriaHasta is { } hasta
            && hasta >= (fin ?? inicio);
    }

    private static bool EvidenciaCoincide(
        DecisionVinculacionBiometrica decision,
        EvidenciaIdentidadVerificada? evidencia) =>
        evidencia is not null
        && evidencia.TrabajadorId == decision.TrabajadorId
        && string.Equals(evidencia.Huella, decision.HuellaEvidencia, StringComparison.Ordinal)
        && EvidenciaValida(
            evidencia,
            new ReferenciaEvidenciaRegistro(
                decision.IndiceRegistro,
                evidencia.Fuente,
                decision.ReferenciaEvidencia),
            decision.FechaInicio,
            decision.FechaFin,
            DateTime.Now);

    private async Task<bool> EvidenciaDocumentalCoincideAsync(
        DecisionVinculacionBiometrica decision,
        CancellationToken cancelacion)
    {
        if (decision.BaseConfirmacion != BaseConfirmacionVinculacion.EvidenciaDocumental
            || !Enum.TryParse<FuenteEvidenciaDocumento>(decision.CategoriaEvidencia, out var fuente))
        {
            return false;
        }

        var evidencia = await evidencias.VerificarAsync(
                fuente, decision.ReferenciaEvidencia, cancelacion)
            .ConfigureAwait(false);
        return EvidenciaCoincide(decision, evidencia);
    }

    private async Task<bool> EvidenciaOperativaCoincideAsync(
        DecisionVinculacionBiometrica decision,
        CancellationToken cancelacion)
    {
        if (decision.BaseConfirmacion != BaseConfirmacionVinculacion.ReglaOperativaUserU
            || decision.NaturalezaVigencia != NaturalezaVigenciaVinculacion.Declarada
            || !string.Equals(
                decision.CategoriaEvidencia,
                BaseConfirmacionVinculacion.ReglaOperativaUserU.ToString(),
                StringComparison.Ordinal)
            || !decision.ReferenciaEvidencia.StartsWith("USERU:", StringComparison.Ordinal)
            || decision.TrabajadorId is not { } trabajadorId
            || decision.FechaInicio < DateOnly.FromDateTime(DateTime.Today)
            || decision.Conciliacion is null
            || decision.Conciliacion.VersionPoliticaIdentidad != VersionPoliticaIdentidad)
        {
            return false;
        }

        var identidad = await contexto.Trabajadores.AsNoTracking()
            .Where(t => t.Id == trabajadorId)
            .Join(
                contexto.TiposDocumento.AsNoTracking(),
                t => t.TipoDocumentoId,
                tipo => tipo.Id,
                (t, tipo) => new { t.TipoDocumentoId, t.NumeroDocumento, TipoCodigo = tipo.Codigo })
            .SingleOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);
        if (identidad is null
            || !string.Equals(identidad.TipoCodigo, "DNI", StringComparison.Ordinal)
            || identidad.NumeroDocumento.Length != 8
            || !identidad.NumeroDocumento.All(char.IsAsciiDigit)
            || !await TrabajadorVigenteEnPlantaAsync(
                trabajadorId,
                decision.Conciliacion.PlantaId,
                decision.FechaInicio,
                decision.FechaFin,
                cancelacion).ConfigureAwait(false))
        {
            return false;
        }

        var usuario = new UsuarioZktecoLeido(
            decision.IndiceRegistro,
            decision.UidRegistro,
            decision.Pin,
            identidad.NumeroDocumento,
            false,
            false,
            EstadoRegistroUsuarioZkteco.LeidoConAdvertencias,
            EstadoCandidatoIdentidadZkteco.NoVerificado,
            []);
        var actual = HuellaOperativa(
            decision.Conciliacion,
            decision,
            usuario,
            identidad.NumeroDocumento,
            identidad.TipoDocumentoId);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actual),
            Encoding.ASCII.GetBytes(decision.HuellaEvidencia));
    }

    private async Task<bool> TrabajadorVigenteEnPlantaAsync(
        long trabajadorId,
        long plantaId,
        DateOnly inicio,
        DateOnly? fin,
        CancellationToken cancelacion)
    {
        var limite = fin ?? inicio;
        return await contexto.PeriodosLaborales.AsNoTracking().AnyAsync(p =>
            p.TrabajadorId == trabajadorId
            && p.FechaIngreso <= inicio
            && (p.FechaCese == null || p.FechaCese >= limite)
            && p.AsignacionesPlanta.Any(a =>
                a.PlantaId == plantaId
                && a.FechaInicio <= inicio
                && (a.FechaFin == null || a.FechaFin >= limite)), cancelacion).ConfigureAwait(false);
    }

    private async Task<ResultadoDecisionConfirmacionBiometrica> RegistrarFalloTecnicoAsync(
        long conciliacionId,
        long decisionId,
        CancellationToken cancelacion)
    {
        var decision = await contexto.DecisionesVinculacionBiometrica
            .SingleAsync(d => d.Id == decisionId && d.ConciliacionId == conciliacionId, cancelacion)
            .ConfigureAwait(false);
        if (decision.Resultado == ResultadoDecisionVinculacion.Pendiente)
        {
            decision.Resultado = ResultadoDecisionVinculacion.Rechazada;
            decision.MotivoResultado = "La transacción no pudo completarse; no se creó ninguna asignación.";
            await GuardarResultadoAsync(decision, cancelacion).ConfigureAwait(false);
        }

        return Resultado(decision);
    }

    private async Task<(long Id, long PlantaId, string Codigo)> ObtenerEquipoAutorizadoAsync(
        long equipoId,
        CancellationToken cancelacion)
    {
        var equipo = await contexto.EquiposMarcador.AsNoTracking()
            .Where(e => e.Id == equipoId && e.Activo)
            .Select(e => new { e.Id, e.PlantaId, e.Codigo })
            .SingleOrDefaultAsync(cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el equipo", equipoId);
        await GarantizarPlantaAutorizadaAsync(equipo.PlantaId, cancelacion).ConfigureAwait(false);
        return (equipo.Id, equipo.PlantaId, equipo.Codigo);
    }

    private async Task GarantizarPlantaAutorizadaAsync(long plantaId, CancellationToken cancelacion)
    {
        var plantas = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);
        if (plantas is { } restringidas && !restringidas.Contains(plantaId))
        {
            throw new RecursoNoEncontradoException("el recurso autorizado", plantaId);
        }
    }

    private void ValidarAutorizacion()
    {
        if (!autorizacion.EstaAutenticado || !autorizacion.EsCoordinador
            || string.IsNullOrWhiteSpace(autorizacion.Usuario)
            || string.Equals(autorizacion.Usuario, "sistema", StringComparison.OrdinalIgnoreCase))
        {
            throw new ReglaNegocioException("Solo un COORDINADOR autenticado puede confirmar vinculaciones biométricas.");
        }
    }

    private async Task GuardarResultadoAsync(
        DecisionVinculacionBiometrica decision,
        CancellationToken cancelacion)
    {
        decision.ConfirmadaPor = autorizacion.Usuario;
        decision.ConfirmadaEn = DateTime.Now;
        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    private async Task ActualizarEstadoLoteAsync(long loteId, CancellationToken cancelacion)
    {
        var lote = await contexto.ConciliacionesBiometricas.Include(c => c.Decisiones)
            .SingleAsync(c => c.Id == loteId, cancelacion).ConfigureAwait(false);
        lote.Estado = lote.Decisiones.All(d => d.Resultado != ResultadoDecisionVinculacion.Pendiente)
            ? EstadoConciliacionPersistida.Completada
            : EstadoConciliacionPersistida.Parcial;
        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        contexto.ChangeTracker.Clear();
    }

    private async Task<PropuestaConfirmacionBiometricaDto> ProyectarAsync(
        ConciliacionBiometrica lote,
        CancellationToken cancelacion)
    {
        var trabajadores = lote.Decisiones
            .Where(d => d.TrabajadorId.HasValue)
            .Select(d => d.TrabajadorId!.Value)
            .Distinct()
            .ToArray();
        var documentos = await contexto.Trabajadores.AsNoTracking()
            .Where(t => trabajadores.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.NumeroDocumento, cancelacion)
            .ConfigureAwait(false);
        return new(
            lote.IdentificadorPublico,
            lote.Version,
            lote.ExpiraEn,
            lote.Decisiones.Select(d => new PropuestaDecisionBiometricaDto(
                d.Id,
                d.IndiceRegistro,
                d.Pin,
                d.TrabajadorId,
                d.TrabajadorId is { } id ? documentos.GetValueOrDefault(id) : null,
                (BaseConfirmacionIdentidadBiometrica)(int)d.BaseConfirmacion,
                (NaturalezaVigenciaBiometrica)(int)d.NaturalezaVigencia,
                d.FechaInicio,
                d.FechaFin,
                d.Resultado.ToString(),
                d.MotivoResultado,
                string.IsNullOrWhiteSpace(d.Diagnosticos)
                    ? []
                    : d.Diagnosticos.Split(',', StringSplitOptions.RemoveEmptyEntries))).ToArray());
    }

    private static ResultadoDecisionConfirmacionBiometrica Resultado(DecisionVinculacionBiometrica decision) =>
        new(decision.Id, decision.Resultado.ToString(), decision.MotivoResultado, decision.AsignacionMarcadorId);

    private static string Hash(string valor) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));

    private static string HuellaOperativa(
        ConciliacionBiometrica lote,
        DecisionVinculacionBiometrica decision,
        UsuarioZktecoLeido usuario,
        string documento,
        long tipoDocumentoId) =>
        Hash(
            $"{lote.HuellaRespaldo}|{lote.EquipoId}|{usuario.IndiceRegistro}|{usuario.Uid}|" +
            $"{usuario.Pin}|{documento}|{decision.TrabajadorId}|{tipoDocumentoId}|" +
            $"{decision.FechaInicio:yyyyMMdd}|{decision.FechaFin:yyyyMMdd}|{lote.VersionPoliticaIdentidad}");

    private static bool EsConflicto(EstadoConciliacionBiometrica estado) => estado is
        EstadoConciliacionBiometrica.PinDuplicadoDentroDelEquipo
        or EstadoConciliacionBiometrica.PinAsignadoAOtroTrabajador
        or EstadoConciliacionBiometrica.TrabajadorAsignadoAOtroEquipo
        or EstadoConciliacionBiometrica.ConflictoDeVigencia;
}
