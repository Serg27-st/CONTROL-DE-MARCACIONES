using System.Text.Json;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Marcaciones;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

public sealed class ServicioConciliacionBiometricaTests : IDisposable
{
    private static readonly DateOnly Fecha = new(2026, 10, 1);
    private const long EquipoSeleccionado = 101;
    private const long EquipoContrario = 102;
    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;

    public ServicioConciliacionBiometricaTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:;Pooling=False");
        conexion.Open();
        contexto = new MarcacionDbContext(new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexion)
            .Options);
        contexto.Database.EnsureCreated();
        SembrarCatalogos();
    }

    [Fact]
    public async Task Documento_verificado_y_trabajador_unico_es_coincidencia_verificable()
    {
        var trabajador = await CrearTrabajadorAsync("12345678");

        var fila = await ConciliarUnoAsync(
            Usuario("15", "12345678"),
            Evidencia("12345678", tipoDocumentoId: 1));

        fila.Estado.ShouldBe(EstadoConciliacionBiometrica.CoincidenciaVerificable);
        fila.TrabajadorId.ShouldBe(trabajador.Id);
        fila.DocumentoVerificado.ShouldBeTrue();
    }

    [Fact]
    public async Task Name_igual_a_dni_es_coincidencia_operativa_no_documental()
    {
        await CrearTrabajadorAsync("12345678");

        var fila = await ConciliarUnoAsync(Usuario("15", "12345678"));

        fila.Estado.ShouldBe(EstadoConciliacionBiometrica.CoincidenciaOperativaUserUConfirmable);
        fila.BaseConfirmacionIdentidad.ShouldBe(BaseConfirmacionIdentidadBiometrica.ReglaOperativaUserU);
        fila.DocumentoVerificado.ShouldBeFalse();
        fila.Diagnosticos.ShouldContain(DiagnosticoConciliacionBiometrica.DocumentoNoVerificado);
        fila.Diagnosticos.ShouldContain(
            DiagnosticoConciliacionBiometrica.DocumentoCoincideExactamentePorReglaOperativa);
    }

    [Fact]
    public async Task Documento_inexistente_no_crea_trabajador()
    {
        var antes = await contexto.Trabajadores.CountAsync();

        var fila = await ConciliarUnoAsync(Usuario("15", "99999999"));

        fila.Estado.ShouldBe(EstadoConciliacionBiometrica.TrabajadorNoEncontrado);
        (await contexto.Trabajadores.CountAsync()).ShouldBe(antes);
    }

    [Fact]
    public async Task Regla_operativa_solo_selecciona_tipo_dni()
    {
        await CrearTrabajadorAsync("12345678", tipoDocumentoId: 1);
        await CrearTrabajadorAsync("12345678", tipoDocumentoId: 2);

        var fila = await ConciliarUnoAsync(Usuario("15", "12345678"));

        fila.Estado.ShouldBe(EstadoConciliacionBiometrica.CoincidenciaOperativaUserUConfirmable);
        fila.TrabajadorId.ShouldNotBeNull();
        (await contexto.Trabajadores.SingleAsync(t => t.Id == fila.TrabajadorId.Value))
            .TipoDocumentoId.ShouldBe(1);
    }

    [Fact]
    public async Task Documento_de_otro_tipo_no_es_confirmable_por_convencion_dni()
    {
        await CrearTrabajadorAsync("12345678", tipoDocumentoId: 2);

        var fila = await ConciliarUnoAsync(Usuario("15", "12345678"));

        fila.Estado.ShouldBe(EstadoConciliacionBiometrica.CandidatoDocumentoNoVerificado);
        fila.Diagnosticos.ShouldContain(
            DiagnosticoConciliacionBiometrica.TipoDocumentoNoCompatibleConReglaDni);
    }

    [Fact]
    public async Task Dni_duplicado_dentro_de_user_u_deja_ambas_filas_en_revision()
    {
        await CrearTrabajadorAsync("12345678");

        var resultado = await Servicio().ConciliarAsync(Solicitud([
            Usuario("15", "12345678", indice: 1),
            Usuario("16", "12345678", indice: 2)]));

        resultado.Registros.ShouldAllBe(r =>
            r.Estado == EstadoConciliacionBiometrica.DocumentoAmbiguoODuplicado
            && r.Diagnosticos.Contains(DiagnosticoConciliacionBiometrica.DniDuplicadoDentroDelArchivo));
    }

    [Theory]
    [InlineData(" 12345678")]
    [InlineData("12345678 ")]
    [InlineData("1234-5678")]
    [InlineData("１２３４５６７８")]
    public async Task Name_no_se_normaliza_para_buscar_dni(string name)
    {
        await CrearTrabajadorAsync("12345678");

        var fila = await ConciliarUnoAsync(Usuario("15", name));

        fila.TrabajadorId.ShouldBeNull();
        fila.Estado.ShouldNotBe(EstadoConciliacionBiometrica.CoincidenciaOperativaUserUConfirmable);
    }

    [Fact]
    public async Task Nombre_vacio_clasifica_documento_ausente()
    {
        var fila = await ConciliarUnoAsync(Usuario("15", null));

        fila.Estado.ShouldBe(EstadoConciliacionBiometrica.DocumentoAusente);
    }

    [Fact]
    public async Task Pin_largo_se_conserva_como_texto()
    {
        await CrearTrabajadorAsync("12345678");

        var fila = await ConciliarUnoAsync(Usuario("123456789", "12345678"));

        fila.Pin.ShouldBe("123456789");
    }

    [Fact]
    public async Task Pin_con_ceros_iniciales_se_conserva()
    {
        await CrearTrabajadorAsync("12345678");

        var fila = await ConciliarUnoAsync(Usuario("000015", "12345678"));

        fila.Pin.ShouldBe("000015");
    }

    [Fact]
    public async Task Pin_de_treinta_caracteres_se_conserva_y_uno_mayor_se_rechaza()
    {
        await CrearTrabajadorAsync("12345678");
        var permitido = new string('1', 30);
        var excesivo = new string('1', 31);

        (await ConciliarUnoAsync(Usuario(permitido, "12345678"))).Pin.ShouldBe(permitido);
        var rechazado = await ConciliarUnoAsync(Usuario(excesivo, "12345678"));

        rechazado.Estado.ShouldBe(EstadoConciliacionBiometrica.RegistroInvalidoOIncompleto);
        rechazado.Diagnosticos.ShouldContain(DiagnosticoConciliacionBiometrica.PinSuperaLongitudPermitida);
    }

    [Fact]
    public async Task Mismo_pin_en_equipos_distintos_no_es_conflicto_por_si_solo()
    {
        await CrearTrabajadorAsync("12345678");
        var otro = await CrearTrabajadorAsync("87654321");
        await AsignarAsync(otro.Id, EquipoContrario, "15", Fecha, null);

        var fila = await ConciliarUnoAsync(
            Usuario("15", "12345678"),
            Evidencia("12345678", tipoDocumentoId: 1));

        fila.Estado.ShouldBe(EstadoConciliacionBiometrica.CoincidenciaVerificable);
    }

    [Fact]
    public async Task Pin_repetido_dentro_del_archivo_bloquea_ambas_propuestas()
    {
        await CrearTrabajadorAsync("12345678");
        await CrearTrabajadorAsync("87654321");
        var resultado = await Servicio().ConciliarAsync(Solicitud([
            Usuario("15", "12345678", indice: 1),
            Usuario("15", "87654321", indice: 2)]));

        resultado.Registros.ShouldAllBe(r =>
            r.Estado == EstadoConciliacionBiometrica.PinDuplicadoDentroDelEquipo);
    }

    [Fact]
    public async Task Asignacion_correcta_existente_es_no_op()
    {
        var trabajador = await CrearTrabajadorAsync("12345678");
        await AsignarAsync(trabajador.Id, EquipoSeleccionado, "15", Fecha, null);

        var fila = await ConciliarUnoAsync(
            Usuario("15", "12345678"),
            Evidencia("12345678", tipoDocumentoId: 1));

        fila.Estado.ShouldBe(EstadoConciliacionBiometrica.YaVinculadoCorrectamente);
    }

    [Fact]
    public async Task Pin_ocupado_por_otro_trabajador_es_conflicto()
    {
        await CrearTrabajadorAsync("12345678");
        var ocupante = await CrearTrabajadorAsync("87654321");
        await AsignarAsync(ocupante.Id, EquipoSeleccionado, "15", Fecha, null);

        var fila = await ConciliarUnoAsync(
            Usuario("15", "12345678"),
            Evidencia("12345678", tipoDocumentoId: 1));

        fila.Estado.ShouldBe(EstadoConciliacionBiometrica.PinAsignadoAOtroTrabajador);
    }

    [Fact]
    public async Task Trabajador_asignado_al_equipo_contrario_es_conflicto()
    {
        var trabajador = await CrearTrabajadorAsync("12345678");
        await AsignarAsync(trabajador.Id, EquipoContrario, "20", Fecha, null);

        var fila = await ConciliarUnoAsync(
            Usuario("15", "12345678"),
            Evidencia("12345678", tipoDocumentoId: 1));

        fila.Estado.ShouldBe(EstadoConciliacionBiometrica.TrabajadorAsignadoAOtroEquipo);
    }

    [Fact]
    public async Task Asignaciones_historicas_no_superpuestas_no_bloquean()
    {
        var trabajador = await CrearTrabajadorAsync("12345678");
        await AsignarAsync(trabajador.Id, EquipoContrario, "20", new DateOnly(2026, 1, 1), Fecha.AddDays(-1));

        var fila = await ConciliarUnoAsync(
            Usuario("15", "12345678"),
            Evidencia("12345678", tipoDocumentoId: 1));

        fila.Estado.ShouldBe(EstadoConciliacionBiometrica.CoincidenciaVerificable);
    }

    [Fact]
    public async Task Asignaciones_historicas_superpuestas_se_exponen()
    {
        var trabajador = await CrearTrabajadorAsync("12345678");
        await AsignarAsync(trabajador.Id, EquipoSeleccionado, "15", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        await AsignarAsync(trabajador.Id, EquipoContrario, "20", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 31));

        var fila = await ConciliarUnoAsync(
            Usuario("15", "12345678"),
            Evidencia("12345678", tipoDocumentoId: 1));

        fila.Estado.ShouldBe(EstadoConciliacionBiometrica.TrabajadorAsignadoAOtroEquipo);
        fila.Diagnosticos.ShouldContain(DiagnosticoConciliacionBiometrica.AsignacionesSuperpuestas);
    }

    [Fact]
    public async Task Fecha_efectiva_futura_se_conserva_como_diagnostico()
    {
        await CrearTrabajadorAsync("12345678", fechaIngreso: Fecha);
        var futura = DateOnly.FromDateTime(DateTime.Today).AddDays(10);

        var fila = await ConciliarUnoAsync(
            Usuario("15", "12345678"),
            Evidencia("12345678", tipoDocumentoId: 1),
            fecha: futura);

        fila.Diagnosticos.ShouldContain(DiagnosticoConciliacionBiometrica.FechaEfectivaFutura);
    }

    [Fact]
    public async Task Fecha_efectiva_no_acreditada_condiciona_la_propuesta()
    {
        await CrearTrabajadorAsync("12345678");

        var fila = await ConciliarUnoAsync(
            Usuario("15", "12345678"),
            Evidencia("12345678", tipoDocumentoId: 1),
            fechaAcreditada: false);

        fila.Estado.ShouldBe(EstadoConciliacionBiometrica.PropuestaCondicionada);
    }

    [Fact]
    public async Task Usuario_sin_autorizacion_sobre_planta_no_conoce_el_equipo()
    {
        await Should.ThrowAsync<RecursoNoEncontradoException>(() =>
            Servicio(new AlcanceDePlantas(2)).ConciliarAsync(Solicitud([Usuario("15", null)])));
    }

    [Fact]
    public async Task Alcance_vacio_falla_cerrado()
    {
        await Should.ThrowAsync<RecursoNoEncontradoException>(() =>
            Servicio(new AlcanceDePlantas()).ConciliarAsync(Solicitud([Usuario("15", null)])));
    }

    [Fact]
    public async Task Repetir_conciliacion_produce_resultado_identico()
    {
        await CrearTrabajadorAsync("12345678");
        var solicitud = Solicitud([Usuario("0015", "12345678")]);

        var primero = await Servicio().ConciliarAsync(solicitud);
        var segundo = await Servicio().ConciliarAsync(solicitud);

        JsonSerializer.Serialize(segundo).ShouldBe(JsonSerializer.Serialize(primero));
    }

    [Fact]
    public async Task Conciliacion_no_escribe_en_ninguna_tabla()
    {
        await CrearTrabajadorAsync("12345678");
        contexto.ChangeTracker.Clear();
        var antes = await ConteosAsync();

        await Servicio().ConciliarAsync(Solicitud([Usuario("15", "12345678")]));

        (await ConteosAsync()).ShouldBe(antes);
        contexto.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact]
    public async Task Conciliacion_no_modifica_aislamiento_pmv()
    {
        var antes = await contexto.MarcacionesProvisionales.CountAsync();

        await Servicio().ConciliarAsync(Solicitud([Usuario("15", null)]));

        (await contexto.MarcacionesProvisionales.CountAsync()).ShouldBe(antes);
    }

    [Fact]
    public async Task Conciliacion_no_modifica_datos_de_carga_masiva()
    {
        var trabajador = await CrearTrabajadorAsync("12345678");
        contexto.ChangeTracker.Clear();

        await Servicio().ConciliarAsync(Solicitud([Usuario("15", "12345678")]));

        var persistido = await contexto.Trabajadores.AsNoTracking().SingleAsync(t => t.Id == trabajador.Id);
        persistido.NumeroDocumento.ShouldBe("12345678");
        persistido.AsignacionesMarcador.ShouldBeEmpty();
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    private ServicioConciliacionBiometrica Servicio(IProveedorAlcancePlantas? alcance = null) =>
        new(contexto, alcance ?? new AlcanceDePlantas(1));

    private async Task<ResultadoRegistroConciliacionBiometrica> ConciliarUnoAsync(
        UsuarioZktecoLeido usuario,
        EvidenciaDocumentoConciliacion? evidencia = null,
        DateOnly? fecha = null,
        bool fechaAcreditada = true)
    {
        var resultado = await Servicio().ConciliarAsync(Solicitud(
            [usuario],
            evidencia is null ? [] : [evidencia],
            fecha ?? Fecha,
            fechaAcreditada));
        return resultado.Registros.Single();
    }

    private static SolicitudConciliacionBiometrica Solicitud(
        IReadOnlyList<UsuarioZktecoLeido> usuarios,
        IReadOnlyList<EvidenciaDocumentoConciliacion>? evidencias = null,
        DateOnly? fecha = null,
        bool fechaAcreditada = true) =>
        new(
            EquipoSeleccionado,
            new ResultadoLecturaUsuariosZkteco(
                new EquipoOrigenUsuariosZkteco(EquipoSeleccionado, "EQ-A"),
                EstadoArchivoUsuariosZkteco.ValidoConAdvertencias,
                usuarios,
                [],
                "user.u/table=1/fields=1..11",
                new string('A', 64),
                128,
                DateTime.UtcNow),
            fecha ?? Fecha,
            fechaAcreditada
                ? NaturalezaVigenciaBiometrica.Acreditada
                : NaturalezaVigenciaBiometrica.NoInformada,
            evidencias ?? []);

    private static UsuarioZktecoLeido Usuario(string pin, string? name, int indice = 1) =>
        new(
            indice,
            (ulong)indice,
            pin,
            name,
            false,
            false,
            EstadoRegistroUsuarioZkteco.LeidoConAdvertencias,
            name is null ? EstadoCandidatoIdentidadZkteco.Ausente : EstadoCandidatoIdentidadZkteco.NoVerificado,
            []);

    private static EvidenciaDocumentoConciliacion Evidencia(string documento, long? tipoDocumentoId) =>
        new(1, documento, tipoDocumentoId, FuenteEvidenciaDocumento.PadronControlado);

    private async Task<Trabajador> CrearTrabajadorAsync(
        string documento,
        long tipoDocumentoId = 1,
        long plantaId = 1,
        DateOnly? fechaIngreso = null)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = tipoDocumentoId,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "CONCILIACION",
            Nombres = "SINTETICO",
            Sexo = Sexo.Femenino,
            EmpresaId = 1,
            PlantaId = plantaId,
            TipoPlanilla = TipoPlanilla.Empleado,
            PuestoId = 1,
            FechaIngreso = fechaIngreso ?? new DateOnly(2026, 1, 1)
        };
        var periodo = new PeriodoLaboral
        {
            EmpresaId = 1,
            FechaIngreso = trabajador.FechaIngreso
        };
        periodo.AsignacionesPlanta.Add(new AsignacionPlanta
        {
            PlantaId = plantaId,
            FechaInicio = trabajador.FechaIngreso
        });
        trabajador.PeriodosLaborales.Add(periodo);
        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync();
        return trabajador;
    }

    private async Task AsignarAsync(
        long trabajadorId,
        long equipoId,
        string pin,
        DateOnly inicio,
        DateOnly? fin)
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

    private async Task<(int Trabajadores, int Asignaciones, int Provisionales, int Marcaciones)> ConteosAsync() =>
        (await contexto.Trabajadores.CountAsync(),
         await contexto.AsignacionesMarcador.CountAsync(),
         await contexto.MarcacionesProvisionales.CountAsync(),
         await contexto.MarcacionesCrudas.CountAsync());

    private void SembrarCatalogos()
    {
        contexto.AddRange(
            new EquipoMarcador { Id = EquipoSeleccionado, Codigo = "EQ-A", Nombre = "A-L", PlantaId = 1 },
            new EquipoMarcador { Id = EquipoContrario, Codigo = "EQ-B", Nombre = "M-Z", PlantaId = 1 },
            new EquipoMarcador { Id = 201, Codigo = "EQ-P2", Nombre = "Otra planta", PlantaId = 2 });
        contexto.SaveChanges();
    }
}
