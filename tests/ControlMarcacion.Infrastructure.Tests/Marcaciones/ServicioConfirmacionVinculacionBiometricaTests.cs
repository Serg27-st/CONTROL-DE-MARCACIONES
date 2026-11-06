using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.MarcacionesProvisionales;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Marcaciones;
using ControlMarcacion.Infrastructure.MarcacionesProvisionales;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

public sealed class ServicioConfirmacionVinculacionBiometricaTests : IDisposable
{
    private static readonly DateOnly FechaFutura = DateOnly.FromDateTime(DateTime.Today).AddDays(10);
    private const long EquipoId = 301;
    private const long EquipoOtroId = 302;
    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly EvidenciasPrueba evidencias = new();
    private readonly AutorizacionPrueba autorizacion = new();

    public ServicioConfirmacionVinculacionBiometricaTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:;Pooling=False");
        conexion.Open();
        contexto = new MarcacionDbContext(new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexion)
            .Options);
        contexto.Database.EnsureCreated();
        contexto.AddRange(
            new EquipoMarcador { Id = EquipoId, Codigo = "BIO-A", Nombre = "Biométrico A", PlantaId = 1 },
            new EquipoMarcador { Id = EquipoOtroId, Codigo = "BIO-B", Nombre = "Biométrico B", PlantaId = 1 });
        contexto.SaveChanges();
    }

    [Fact]
    public async Task Alta_correcta_crea_asignacion_y_auditoria_de_decision()
    {
        var trabajador = await TrabajadorAsync("12345678");
        evidencias.Agregar(Evidencia(trabajador));
        var propuesta = await PrepararAsync("15");

        var resultado = await ConfirmarAsync(propuesta);

        resultado.Resultados.Single().Resultado.ShouldBe("Creada");
        var asignacion = await contexto.AsignacionesMarcador.AsNoTracking().SingleAsync();
        asignacion.TrabajadorId.ShouldBe(trabajador.Id);
        asignacion.EquipoId.ShouldBe(EquipoId);
        asignacion.IdMarcador.ShouldBe("15");
        var decision = await contexto.DecisionesVinculacionBiometrica.AsNoTracking().SingleAsync();
        decision.ConfirmadaPor.ShouldBe("coordinador.prueba");
        decision.AsignacionMarcadorId.ShouldBe(asignacion.Id);
    }

    [Fact]
    public async Task Confirmacion_repetida_es_idempotente()
    {
        var trabajador = await TrabajadorAsync("12345678");
        evidencias.Agregar(Evidencia(trabajador));
        var propuesta = await PrepararAsync("0015");

        var primero = await ConfirmarAsync(propuesta);
        var segundo = await ConfirmarAsync(propuesta);

        primero.Resultados.Single().AsignacionMarcadorId.ShouldBe(segundo.Resultados.Single().AsignacionMarcadorId);
        (await contexto.AsignacionesMarcador.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Pin_ocupado_no_se_confirma()
    {
        var objetivo = await TrabajadorAsync("12345678");
        var ocupante = await TrabajadorAsync("87654321");
        await AsignarAsync(ocupante.Id, EquipoId, "15", FechaFutura, null);
        evidencias.Agregar(Evidencia(objetivo));

        var propuesta = await PrepararAsync("15");

        propuesta.Decisiones.Single().Resultado.ShouldBe("ConflictoVigencia");
        (await contexto.AsignacionesMarcador.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Trabajador_en_otro_equipo_no_se_cierra_ni_sustituye()
    {
        var trabajador = await TrabajadorAsync("12345678");
        await AsignarAsync(trabajador.Id, EquipoOtroId, "20", FechaFutura, null);
        evidencias.Agregar(Evidencia(trabajador));

        var propuesta = await PrepararAsync("15");

        propuesta.Decisiones.Single().Resultado.ShouldBe("ConflictoVigencia");
        (await contexto.AsignacionesMarcador.SingleAsync()).IdMarcador.ShouldBe("20");
    }

    [Fact]
    public async Task Historial_cerrado_sin_superposicion_permite_alta()
    {
        var trabajador = await TrabajadorAsync("12345678");
        await AsignarAsync(trabajador.Id, EquipoOtroId, "20", FechaFutura.AddDays(-20), FechaFutura.AddDays(-1));
        evidencias.Agregar(Evidencia(trabajador));

        var propuesta = await PrepararAsync("15");
        await ConfirmarAsync(propuesta);

        (await contexto.AsignacionesMarcador.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Historial_superpuesto_queda_bloqueado()
    {
        var trabajador = await TrabajadorAsync("12345678");
        await AsignarAsync(trabajador.Id, EquipoOtroId, "20", FechaFutura.AddDays(-1), FechaFutura.AddDays(5));
        evidencias.Agregar(Evidencia(trabajador));

        var propuesta = await PrepararAsync("15", fin: FechaFutura.AddDays(2));

        propuesta.Decisiones.Single().Resultado.ShouldBe("ConflictoVigencia");
    }

    [Fact]
    public async Task Vigencia_futura_explicita_esta_permitida()
    {
        var trabajador = await TrabajadorAsync("12345678");
        evidencias.Agregar(Evidencia(trabajador));

        var propuesta = await PrepararAsync("15");
        await ConfirmarAsync(propuesta);

        (await contexto.AsignacionesMarcador.SingleAsync()).FechaInicio.ShouldBe(FechaFutura);
    }

    [Fact]
    public async Task Vigencia_historica_exige_evidencia_que_cubra_la_fecha()
    {
        var trabajador = await TrabajadorAsync("12345678", new DateOnly(2025, 1, 1));
        evidencias.Agregar(Evidencia(trabajador) with
        {
            AcreditaHistoriaDesde = new DateOnly(2025, 1, 1),
            AcreditaHistoriaHasta = new DateOnly(2025, 12, 31)
        });

        var propuesta = await PrepararAsync("15", inicio: new DateOnly(2025, 6, 1));

        propuesta.Decisiones.Single().Resultado.ShouldBe("Pendiente");
    }

    [Fact]
    public async Task Name_igual_a_dni_crea_propuesta_operativa_no_documental()
    {
        var trabajador = await TrabajadorAsync("12345678");

        var propuesta = await PrepararAsync(
            "15", name: "12345678", evidenciaDocumental: false);

        var decision = propuesta.Decisiones.Single();
        decision.Resultado.ShouldBe("Pendiente");
        decision.TrabajadorId.ShouldBe(trabajador.Id);
        decision.BaseConfirmacionIdentidad.ShouldBe(BaseConfirmacionIdentidadBiometrica.ReglaOperativaUserU);
        decision.NaturalezaVigencia.ShouldBe(NaturalezaVigenciaBiometrica.Declarada);
        var persistida = await contexto.DecisionesVinculacionBiometrica.AsNoTracking().SingleAsync();
        persistida.BaseConfirmacion.ShouldBe(BaseConfirmacionVinculacion.ReglaOperativaUserU);
        persistida.CategoriaEvidencia.ShouldBe("ReglaOperativaUserU");
        var lote = await contexto.ConciliacionesBiometricas.AsNoTracking().SingleAsync();
        lote.HuellaRespaldo.ShouldBe(new string('A', 64));
        lote.TamanoRespaldo.ShouldBe(128);
    }

    [Fact]
    public async Task Confirmacion_operativa_repetida_es_idempotente()
    {
        await TrabajadorAsync("12345678");
        var propuesta = await PrepararAsync("0015", "12345678", evidenciaDocumental: false);

        var primero = await ConfirmarAsync(propuesta);
        var segundo = await ConfirmarAsync(propuesta);

        primero.Resultados.Single().AsignacionMarcadorId
            .ShouldBe(segundo.Resultados.Single().AsignacionMarcadorId);
        (await contexto.AsignacionesMarcador.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Referencia_documental_invalida_no_degrada_silenciosamente_a_regla_operativa()
    {
        await TrabajadorAsync("12345678");

        var propuesta = await PrepararAsync("15", name: "12345678");

        propuesta.Decisiones.Single().Resultado.ShouldBe("EvidenciaInsuficiente");
    }

    [Fact]
    public async Task Vigencia_historica_user_u_queda_en_revision_no_acreditada()
    {
        await TrabajadorAsync("12345678", new DateOnly(2025, 1, 1));

        var propuesta = await PrepararAsync(
            "15",
            name: "12345678",
            inicio: new DateOnly(2025, 6, 1),
            evidenciaDocumental: false);

        propuesta.Decisiones.Single().Resultado.ShouldBe("RevisionManual");
        propuesta.Decisiones.Single().NaturalezaVigencia.ShouldBe(NaturalezaVigenciaBiometrica.Declarada);
        (await contexto.AsignacionesMarcador.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Pin_largo_operativo_se_conserva_sin_transformacion()
    {
        await TrabajadorAsync("12345678");
        const string pin = "000012345678901234567890123456";

        var propuesta = await PrepararAsync(pin, "12345678", evidenciaDocumental: false);
        await ConfirmarAsync(propuesta);

        (await contexto.AsignacionesMarcador.SingleAsync()).IdMarcador.ShouldBe(pin);
    }

    [Fact]
    public async Task Cambio_de_dni_despues_de_preparar_invalida_confirmacion_operativa()
    {
        var trabajador = await TrabajadorAsync("12345678");
        var propuesta = await PrepararAsync("15", "12345678", evidenciaDocumental: false);
        trabajador.NumeroDocumento = "87654321";
        await contexto.SaveChangesAsync();

        var resultado = await ConfirmarAsync(propuesta);

        resultado.Resultados.Single().Resultado.ShouldBe("EvidenciaInsuficiente");
        (await contexto.AsignacionesMarcador.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Evidencia_vencida_es_insuficiente()
    {
        var trabajador = await TrabajadorAsync("12345678");
        evidencias.Agregar(Evidencia(trabajador) with { ExpiraEn = DateTime.Now.AddMinutes(-1) });

        var propuesta = await PrepararAsync("15");

        propuesta.Decisiones.Single().Resultado.ShouldBe("EvidenciaInsuficiente");
    }

    [Fact]
    public async Task Cambio_posterior_a_la_conciliacion_la_desactualiza()
    {
        var trabajador = await TrabajadorAsync("12345678");
        evidencias.Agregar(Evidencia(trabajador));
        var propuesta = await PrepararAsync("15");
        await AsignarAsync(trabajador.Id, EquipoOtroId, "99", FechaFutura.AddDays(-30), FechaFutura.AddDays(-20));

        var resultado = await ConfirmarAsync(propuesta);

        resultado.Resultados.Single().Resultado.ShouldBe("PropuestaDesactualizada");
    }

    [Fact]
    public async Task Fallo_de_insert_revierte_asignacion_y_decision()
    {
        var trabajador = await TrabajadorAsync("12345678");
        evidencias.Agregar(Evidencia(trabajador));
        var propuesta = await PrepararAsync("15");
        await contexto.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER falla_bio BEFORE INSERT ON asignacion_marcador BEGIN SELECT RAISE(ABORT, 'fallo controlado'); END;");

        var resultado = await ConfirmarAsync(propuesta);
        contexto.ChangeTracker.Clear();

        resultado.Resultados.Single().Resultado.ShouldBe("Rechazada");
        (await contexto.AsignacionesMarcador.CountAsync()).ShouldBe(0);
        (await contexto.DecisionesVinculacionBiometrica.AsNoTracking().SingleAsync()).Resultado
            .ShouldBe(ResultadoDecisionVinculacion.Rechazada);
    }

    [Fact]
    public async Task Coordinador_sin_planta_falla_cerrado()
    {
        var trabajador = await TrabajadorAsync("12345678");
        evidencias.Agregar(Evidencia(trabajador));

        await Should.ThrowAsync<RecursoNoEncontradoException>(() =>
            Servicio(new AlcanceDePlantas(2)).PrepararAsync(Request("15")));
    }

    [Fact]
    public async Task Equipo_inactivo_no_puede_ser_origen_de_user_u()
    {
        var equipo = await contexto.EquiposMarcador.SingleAsync(e => e.Id == EquipoId);
        equipo.Activo = false;
        await contexto.SaveChangesAsync();

        await Should.ThrowAsync<RecursoNoEncontradoException>(() =>
            PrepararAsync("15", "12345678", evidenciaDocumental: false));
    }

    [Fact]
    public async Task Usuario_no_coordinador_no_puede_preparar()
    {
        autorizacion.EsCoordinador = false;

        await Should.ThrowAsync<ReglaNegocioException>(() => Servicio().PrepararAsync(Request("15")));
    }

    [Fact]
    public async Task Solicitud_manipulada_con_decision_ajena_se_rechaza()
    {
        var trabajador = await TrabajadorAsync("12345678");
        evidencias.Agregar(Evidencia(trabajador));
        var propuesta = await PrepararAsync("15");

        await Should.ThrowAsync<ReglaNegocioException>(() => Servicio().ConfirmarLoteAsync(
            new(propuesta.ConciliacionId, propuesta.Version, [propuesta.Decisiones.Single().DecisionId + 999])));
    }

    [Fact]
    public async Task Confirmacion_no_modifica_marcaciones_pmv()
    {
        var trabajador = await TrabajadorAsync("12345678");
        evidencias.Agregar(Evidencia(trabajador));
        var propuesta = await PrepararAsync("15");
        var antes = await contexto.MarcacionesProvisionales.CountAsync();

        await ConfirmarAsync(propuesta);

        (await contexto.MarcacionesProvisionales.CountAsync()).ShouldBe(antes);
    }

    [Fact]
    public async Task PMV_resuelve_dinamicamente_solo_dentro_de_la_vigencia()
    {
        var trabajador = await TrabajadorAsync("12345678");
        var importacion = new ImportacionProvisional
        {
            NombreArchivo = "sintetico.dat",
            EquipoId = EquipoId,
            HashContenido = new string('A', 64),
            FechaCarga = DateTime.Now,
            CargadoPorUsuario = "prueba",
            FilasLeidas = 1,
            FilasInsertadas = 1
        };
        contexto.ImportacionesProvisionales.Add(importacion);
        await contexto.SaveChangesAsync();
        contexto.MarcacionesProvisionales.Add(new MarcacionProvisional
        {
            EquipoId = EquipoId,
            IdMarcador = "15",
            FechaHora = FechaFutura.ToDateTime(new TimeOnly(8, 0)),
            ImportacionProvisionalId = importacion.Id
        });
        await contexto.SaveChangesAsync();
        var pmv = new ServicioConsultaMarcacionProvisional(contexto, new AlcanceDePlantas(1));
        (await pmv.ConsultarAsync(EquipoId, FechaFutura, FechaFutura))
            .Jornadas.Single().TrabajadorId.ShouldBeNull();
        var marcasAntes = await contexto.MarcacionesProvisionales.CountAsync();
        var propuesta = await PrepararAsync(
            "15", "12345678", fin: FechaFutura.AddDays(5), evidenciaDocumental: false);
        await ConfirmarAsync(propuesta);

        var asignacion = await contexto.AsignacionesMarcador.AsNoTracking().SingleAsync();

        (await pmv.ConsultarAsync(EquipoId, FechaFutura, FechaFutura))
            .Jornadas.Single().TrabajadorId.ShouldBe(trabajador.Id);
        (await contexto.MarcacionesProvisionales.CountAsync()).ShouldBe(marcasAntes);
        asignacion.EstaVigenteEn(FechaFutura).ShouldBeTrue();
        asignacion.EstaVigenteEn(FechaFutura.AddDays(-1)).ShouldBeFalse();
        asignacion.EstaVigenteEn(FechaFutura.AddDays(6)).ShouldBeFalse();
    }

    [Fact]
    public async Task Confirmaciones_simultaneas_crean_una_sola_asignacion()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"fase3-concurrencia-{Guid.NewGuid():N}.db");
        var cadena = $"Data Source={ruta};Pooling=False;Default Timeout=10";
        try
        {
            await using var primero = NuevoContexto(cadena);
            await primero.Database.EnsureCreatedAsync();
            primero.AddRange(
                new EquipoMarcador { Id = EquipoId, Codigo = "BIO-A", Nombre = "Biométrico A", PlantaId = 1 },
                new EquipoMarcador { Id = EquipoOtroId, Codigo = "BIO-B", Nombre = "Biométrico B", PlantaId = 1 });
            await primero.SaveChangesAsync();
            await CrearTrabajadorEnAsync(primero, "12345678");
            var proveedor = new EvidenciasPrueba();
            var servicio1 = ServicioPara(primero, proveedor);
            var propuesta = await servicio1.PrepararAsync(
                Request("15", "12345678", evidenciaDocumental: false));
            primero.ChangeTracker.Clear();

            await using var segundo = NuevoContexto(cadena);
            var servicio2 = ServicioPara(segundo, proveedor);
            var comando = new ConfirmarVinculacionesBiometricasRequest(
                propuesta.ConciliacionId,
                propuesta.Version,
                [propuesta.Decisiones.Single().DecisionId]);

            var resultados = await Task.WhenAll(
                servicio1.ConfirmarLoteAsync(comando),
                servicio2.ConfirmarLoteAsync(comando));

            (await primero.AsignacionesMarcador.CountAsync()).ShouldBe(1);
            resultados.ShouldAllBe(r => r.Resultados.Single().AsignacionMarcadorId.HasValue);
        }
        finally
        {
            if (File.Exists(ruta))
            {
                File.Delete(ruta);
            }
        }
    }

    [Fact]
    public async Task Confirmacion_no_modifica_trabajador_tareo_ni_marcaciones_originales()
    {
        var trabajador = await TrabajadorAsync("12345678");
        evidencias.Agregar(Evidencia(trabajador));
        var propuesta = await PrepararAsync("15");
        var tareos = await contexto.TareosDiarios.CountAsync();
        var marcas = await contexto.MarcacionesCrudas.CountAsync();

        await ConfirmarAsync(propuesta);

        (await contexto.TareosDiarios.CountAsync()).ShouldBe(tareos);
        (await contexto.MarcacionesCrudas.CountAsync()).ShouldBe(marcas);
        (await contexto.Trabajadores.AsNoTracking().SingleAsync(t => t.Id == trabajador.Id)).NumeroDocumento
            .ShouldBe("12345678");
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    private ServicioConfirmacionVinculacionBiometrica Servicio(IProveedorAlcancePlantas? alcance = null)
    {
        var scope = alcance ?? new AlcanceDePlantas(1);
        return new(
            contexto,
            new ServicioConciliacionBiometrica(contexto, scope),
            evidencias,
            autorizacion,
            scope);
    }

    private static ServicioConfirmacionVinculacionBiometrica ServicioPara(
        MarcacionDbContext db,
        IProveedorEvidenciaIdentidad proveedor)
    {
        var scope = new AlcanceDePlantas(1);
        return new(
            db,
            new ServicioConciliacionBiometrica(db, scope),
            proveedor,
            new AutorizacionPrueba(),
            scope);
    }

    private static MarcacionDbContext NuevoContexto(string cadena) =>
        new(new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(cadena).Options);

    private async Task<PropuestaConfirmacionBiometricaDto> PrepararAsync(
        string pin,
        string? name = null,
        DateOnly? inicio = null,
        DateOnly? fin = null,
        bool evidenciaDocumental = true) =>
        await Servicio().PrepararAsync(Request(pin, name, inicio, fin, evidenciaDocumental));

    private static PrepararConfirmacionBiometricaRequest Request(
        string pin,
        string? name = null,
        DateOnly? inicio = null,
        DateOnly? fin = null,
        bool evidenciaDocumental = true) =>
        new(
            EquipoId,
            new ResultadoLecturaUsuariosZkteco(
                new EquipoOrigenUsuariosZkteco(EquipoId, "BIO-A"),
                EstadoArchivoUsuariosZkteco.ValidoConAdvertencias,
                [new UsuarioZktecoLeido(
                    1, 1, pin, name, false, false,
                    EstadoRegistroUsuarioZkteco.LeidoConAdvertencias,
                    name is null ? EstadoCandidatoIdentidadZkteco.Ausente : EstadoCandidatoIdentidadZkteco.NoVerificado,
                    [])],
                [],
                "user.u/table=1/fields=1..11",
                new string('A', 64),
                128,
                DateTime.UtcNow),
            inicio ?? FechaFutura,
            fin,
            evidenciaDocumental
                ? [new ReferenciaEvidenciaRegistro(1, FuenteEvidenciaDocumento.PadronControlado, "EVID-1")]
                : []);

    private async Task<ResultadoConfirmacionBiometricaLote> ConfirmarAsync(PropuestaConfirmacionBiometricaDto propuesta) =>
        await Servicio().ConfirmarLoteAsync(new(
            propuesta.ConciliacionId,
            propuesta.Version,
            propuesta.Decisiones.Select(d => d.DecisionId).ToArray()));

    private async Task<Trabajador> TrabajadorAsync(string documento, DateOnly? ingreso = null)
        => await CrearTrabajadorEnAsync(contexto, documento, ingreso);

    private static async Task<Trabajador> CrearTrabajadorEnAsync(
        MarcacionDbContext db,
        string documento,
        DateOnly? ingreso = null)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = 1,
            NumeroDocumento = documento,
            ApellidoPaterno = "FASE",
            ApellidoMaterno = "TRES",
            Nombres = "PRUEBA",
            Sexo = Sexo.Femenino,
            EmpresaId = 1,
            PlantaId = 1,
            TipoPlanilla = TipoPlanilla.Empleado,
            PuestoId = 1,
            FechaIngreso = ingreso ?? DateOnly.FromDateTime(DateTime.Today)
        };
        var periodo = new PeriodoLaboral { EmpresaId = 1, FechaIngreso = trabajador.FechaIngreso };
        periodo.AsignacionesPlanta.Add(new AsignacionPlanta { PlantaId = 1, FechaInicio = trabajador.FechaIngreso });
        trabajador.PeriodosLaborales.Add(periodo);
        db.Trabajadores.Add(trabajador);
        await db.SaveChangesAsync();
        return trabajador;
    }

    private async Task AsignarAsync(long trabajadorId, long equipoId, string pin, DateOnly inicio, DateOnly? fin)
    {
        contexto.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            TrabajadorId = trabajadorId,
            EquipoId = equipoId,
            IdMarcador = pin,
            FechaInicio = inicio,
            FechaFin = fin
        });
        await contexto.SaveChangesAsync();
    }

    private static EvidenciaIdentidadVerificada Evidencia(Trabajador trabajador) =>
        new(
            FuenteEvidenciaDocumento.PadronControlado,
            "EVID-1",
            $"HUELLA-{trabajador.Id}",
            trabajador.Id,
            trabajador.TipoDocumentoId,
            trabajador.NumeroDocumento,
            DateTime.Now,
            DateTime.Now.AddDays(1),
            null,
            null);

    private sealed class EvidenciasPrueba : IProveedorEvidenciaIdentidad
    {
        private readonly Dictionary<string, EvidenciaIdentidadVerificada> valores = [];
        public void Agregar(EvidenciaIdentidadVerificada evidencia) => valores[evidencia.Referencia] = evidencia;
        public Task<EvidenciaIdentidadVerificada?> VerificarAsync(
            FuenteEvidenciaDocumento fuente,
            string referencia,
            CancellationToken cancelacion = default) =>
            Task.FromResult(valores.GetValueOrDefault(referencia));
    }

    private sealed class AutorizacionPrueba : IContextoAutorizacionVinculacionBiometrica
    {
        public bool EstaAutenticado { get; set; } = true;
        public bool EsCoordinador { get; set; } = true;
        public string Usuario => "coordinador.prueba";
    }
}
