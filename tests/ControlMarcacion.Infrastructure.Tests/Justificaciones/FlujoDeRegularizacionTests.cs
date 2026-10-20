using Microsoft.Extensions.Logging.Abstractions;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Asistencia.Dto;
using ControlMarcacion.Application.Justificaciones.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Auditoria;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Justificaciones;

/// <summary>
/// El circuito completo de una regularización: pedirla, autorizarla y verla en el tareo.
/// </summary>
/// <remarks>
/// La regla que fija todo esto: <b>una regularización pedida no cambia nada hasta que se
/// aprueba</b>. Si modificara el tareo al registrarse, la autorización sería un trámite
/// decorativo y cualquiera podría corregirse sus propios días.
/// </remarks>
public sealed class FlujoDeRegularizacionTests : IDisposable
{
    private const long PlantaP01 = 3;
    private const long EmpresaOutsourcing = 2;
    private const long PuestoOperario = 1;
    private const long TipoDocumentoDni = 1;
    private const long PeriodoJulio2026 = 1;
    private const long TurnoDiurno = 1;

    /// <summary>Miércoles 1 de julio de 2026.</summary>
    private static readonly DateOnly Miercoles = new(2026, 7, 1);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioJustificaciones justificaciones;
    private readonly ServicioCalculoTareo calculo;
    private readonly long equipoId;

    public FlujoDeRegularizacionTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>()
                .UseSqlite(conexion)
                .AddInterceptors(new InterceptorAuditoria(new UsuarioAuditoriaFase62()))
                .Options);
        contexto.Database.EnsureCreated();

        var equipo = new EquipoMarcador { Codigo = "P01-A", Nombre = "Planta 1", PlantaId = PlantaP01, Activo = true };
        contexto.EquiposMarcador.Add(equipo);
        contexto.SaveChanges();
        equipoId = equipo.Id;

        justificaciones = new ServicioJustificaciones(contexto, new AlcanceSinRestriccion());
        calculo = new ServicioCalculoTareo(contexto, new AlcanceSinRestriccion(), CoberturaDePrueba.TodoCargado(), NullLogger<ServicioCalculoTareo>.Instance);
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    /// <summary>
    /// El caso rector, de punta a punta: el marcador tiene solo la salida, el administrador
    /// pide la regularización, el coordinador la aprueba y el día pasa a valer la jornada
    /// que realmente ocurrió.
    /// </summary>
    [Fact]
    public async Task Una_regularizacion_aprobada_completa_la_jornada()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555501");
        await MarcarAsync(trabajadorId, new DateTime(2026, 7, 1, 19, 3, 0));

        var incompleto = await RecalcularYLeerAsync(trabajadorId);
        incompleto.Estado.ShouldBe(EstadoTareo.Incompleto);
        incompleto.TipoIncidencia.ShouldBe(TipoIncidencia.IngresoNoRegistrado);

        // El administrador la pide: no aprueba directamente.
        var id = await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 6, 58, 0)),
            "admin",
            apruebaDirectamente: false,
            CancellationToken.None);

        // Mientras está pendiente, el tareo no se mueve.
        var pendiente = await RecalcularYLeerAsync(trabajadorId);
        pendiente.Estado.ShouldBe(EstadoTareo.Incompleto);
        pendiente.MinutosTrabajados.ShouldBe(0);

        await justificaciones.ResolverAsync(
            id, aprueba: true, "Verificado con el jefe de planta", "coordinador", CancellationToken.None);

        var resuelto = await RecalcularYLeerAsync(trabajadorId);

        // Turno 07:00–19:00, entrada verificada 06:58 (se computa desde la hora del turno)
        // y salida real 19:03: 723 minutos.
        resuelto.Estado.ShouldBe(EstadoTareo.Puntual);
        // R-P0-03.1: la salida 19:03 queda 3 min tras el fin del turno sembrado (19:00), por
        // debajo del umbral de 15, así que no se computa. El DIU sembrado sigue en 07:00–19:00
        // hasta su reconfiguración operativa: el ordinario llega a su fin teórico.
        resuelto.MinutosTrabajados.ShouldBe(720);
        resuelto.MinutosOrdinarios.ShouldBe(720);
        resuelto.RequiereValidacion.ShouldBeFalse();

        // Y la marcación original intacta: el día conserva la salida del equipo y ninguna
        // entrada, porque el equipo nunca la registró.
        resuelto.EntradaReal.ShouldBeNull();
        resuelto.SalidaReal.ShouldBe(new DateTime(2026, 7, 1, 19, 3, 0));
        (await contexto.MarcacionesCrudas.CountAsync()).ShouldBe(1);

        var pendientes = await calculo.ListarIncidenciasAsync(
            new FiltroIncidencias
            {
                PeriodoId = PeriodoJulio2026,
                TrabajadorId = trabajadorId,
                SoloPendientes = true
            },
            CancellationToken.None);
        pendientes.TotalElementos.ShouldBe(0);
    }

    [Fact]
    public async Task INC_PARCIAL_01_regularizacion_incompleta_permanece_en_la_bandeja()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555509");
        await MarcarAsync(trabajadorId, new DateTime(2026, 7, 1, 7, 0, 0));

        var inicial = await RecalcularYLeerAsync(trabajadorId);
        inicial.TipoIncidencia.ShouldBe(TipoIncidencia.SalidaNoRegistrada);

        var justificacionId = await justificaciones.RegularizarAsync(
            Peticion(
                trabajadorId,
                entrada: new DateTime(2026, 7, 1, 7, 0, 0),
                tipoIncidencia: TipoIncidencia.SalidaNoRegistrada),
            "coordinador",
            apruebaDirectamente: true,
            CancellationToken.None);

        var parcial = await RecalcularYLeerAsync(trabajadorId);
        parcial.Estado.ShouldBe(EstadoTareo.Incompleto);
        parcial.TipoIncidencia.ShouldBe(TipoIncidencia.SalidaNoRegistrada);
        parcial.RequiereValidacion.ShouldBeTrue();

        var pendientes = await calculo.ListarIncidenciasAsync(
            new FiltroIncidencias
            {
                PeriodoId = PeriodoJulio2026,
                TrabajadorId = trabajadorId,
                SoloPendientes = true
            },
            CancellationToken.None);

        pendientes.TotalElementos.ShouldBe(1);
        var incidencia = pendientes.Elementos.ShouldHaveSingleItem();
        incidencia.TipoIncidencia.ShouldBe(TipoIncidencia.SalidaNoRegistrada);
        incidencia.JustificacionId.ShouldBe(justificacionId);
        incidencia.EstadoJustificacion.ShouldBe(EstadoJustificacion.Aprobada);
    }

    /// <summary>
    /// Una regularización rechazada no surte efecto, y deja el día libre para pedir otra:
    /// un rechazo no puede bloquear esa fecha para siempre.
    /// </summary>
    [Fact]
    public async Task Una_regularizacion_rechazada_no_aplica_y_libera_el_dia()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555502");
        await MarcarAsync(trabajadorId, new DateTime(2026, 7, 1, 19, 3, 0));

        var id = await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 6, 58, 0)),
            "admin",
            apruebaDirectamente: false,
            CancellationToken.None);

        await justificaciones.ResolverAsync(
            id, aprueba: false, "La hora no coincide con el parte de producción", "coordinador",
            CancellationToken.None);

        var sinCambios = await RecalcularYLeerAsync(trabajadorId);
        sinCambios.Estado.ShouldBe(EstadoTareo.Incompleto);
        sinCambios.MinutosTrabajados.ShouldBe(0);

        // Y se puede volver a pedir, con la hora corregida.
        var segunda = await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 7, 30, 0)),
            "admin",
            apruebaDirectamente: false,
            CancellationToken.None);

        segunda.ShouldNotBe(id);
        (await contexto.Justificaciones.CountAsync()).ShouldBe(2);
    }

    /// <summary>
    /// Quien la pidió puede retirarla mientras nadie la resolvió, y el día queda libre.
    /// </summary>
    /// <remarks>
    /// Es el caso corriente: el administrador nota que puso mal la hora antes de que el
    /// coordinador la mire. Sin esta salida, la única forma de corregirse era pedirle a otro
    /// que la rechazara, y quedaba un rechazo en el historial que parece un desacuerdo.
    /// </remarks>
    [Fact]
    public async Task Quien_la_pidio_puede_retirarla_y_el_dia_queda_libre()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555507");
        await MarcarAsync(trabajadorId, new DateTime(2026, 7, 1, 19, 3, 0));

        var id = await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 6, 58, 0)),
            "admin",
            apruebaDirectamente: false,
            CancellationToken.None);

        await justificaciones.CancelarAsync(id, "admin", CancellationToken.None);

        var retirada = await contexto.Justificaciones.AsNoTracking().FirstAsync(CancellationToken.None);
        retirada.Estado.ShouldBe(EstadoJustificacion.Cancelada);

        // No surte efecto...
        (await RecalcularYLeerAsync(trabajadorId)).Estado.ShouldBe(EstadoTareo.Incompleto);

        // ...y el día admite otra, con la hora corregida.
        await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 7, 15, 0)),
            "admin",
            apruebaDirectamente: false,
            CancellationToken.None);

        (await contexto.Justificaciones.CountAsync()).ShouldBe(2);
    }

    /// <summary>
    /// Retirar la de otro sería resolverla sin dejar constancia de la decisión, que es justo
    /// lo que la aprobación existe para evitar. Quien no esté de acuerdo la rechaza, con
    /// motivo, y eso queda escrito.
    /// </summary>
    [Fact]
    public async Task Nadie_retira_la_regularizacion_de_otro()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555508");

        var id = await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 7, 0, 0)),
            "admin",
            apruebaDirectamente: false,
            CancellationToken.None);

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            justificaciones.CancelarAsync(id, "coordinador", CancellationToken.None));

        error.Message.ShouldContain("recházela indicando el motivo");
    }

    [Fact]
    public async Task Una_regularizacion_ya_aprobada_no_se_retira()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555509");

        var id = await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 7, 0, 0)),
            "admin",
            apruebaDirectamente: false,
            CancellationToken.None);

        await justificaciones.ResolverAsync(id, aprueba: true, "Correcto", "coordinador", CancellationToken.None);

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            justificaciones.CancelarAsync(id, "admin", CancellationToken.None));

        error.Message.ShouldContain("ya está aprobada");
    }

    /// <summary>
    /// El listado de justificaciones tiene que decir si la fila está cambiando el tareo o
    /// solo lo pide: una solicitada se veía idéntica a una aplicada.
    /// </summary>
    [Fact]
    public async Task El_listado_distingue_lo_pedido_de_lo_aplicado()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555510");

        var id = await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 6, 58, 0)),
            "admin",
            apruebaDirectamente: false,
            CancellationToken.None);

        var pedida = (await justificaciones.ListarAsync(new FiltroJustificaciones(), CancellationToken.None))
            .Elementos.ShouldHaveSingleItem();

        pedida.Aplica.ShouldBeFalse();
        pedida.EsperaResolucion.ShouldBeTrue();
        pedida.EsRegularizacion.ShouldBeTrue();
        pedida.EntradaRegularizada.ShouldBe(new DateTime(2026, 7, 1, 6, 58, 0));
        pedida.ResueltaPorUsuario.ShouldBeNull();

        await justificaciones.ResolverAsync(id, aprueba: true, "Verificado", "coordinador", CancellationToken.None);

        var aplicada = (await justificaciones.ListarAsync(new FiltroJustificaciones(), CancellationToken.None))
            .Elementos.ShouldHaveSingleItem();

        aplicada.Aplica.ShouldBeTrue();
        aplicada.EsperaResolucion.ShouldBeFalse();
        aplicada.ResueltaPorUsuario.ShouldBe("coordinador");
    }

    [Fact]
    public async Task Un_rechazo_sin_motivo_se_rechaza()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555503");

        var id = await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 7, 0, 0)),
            "admin",
            apruebaDirectamente: false,
            CancellationToken.None);

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            justificaciones.ResolverAsync(id, aprueba: false, motivo: "  ", "coordinador", CancellationToken.None));

        error.Message.ShouldContain("por qué se rechaza");
    }

    [Fact]
    public async Task No_se_resuelve_dos_veces_la_misma_regularizacion()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555504");

        var id = await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 7, 0, 0)),
            "admin",
            apruebaDirectamente: false,
            CancellationToken.None);

        await justificaciones.ResolverAsync(id, aprueba: true, "Correcto", "coordinador", CancellationToken.None);

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            justificaciones.ResolverAsync(id, aprueba: false, "Me arrepentí", "otro", CancellationToken.None));

        error.Message.ShouldContain("ya está aprobada");
    }

    /// <summary>
    /// Sin ninguna hora no hay nada que corregir, y una hora de otro día es casi siempre un
    /// error de tipeo: sin tope produciría una jornada de días enteros.
    /// </summary>
    [Fact]
    public async Task Una_regularizacion_sin_hora_o_con_fecha_ajena_se_rechaza()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555505");

        var sinHora = await Should.ThrowAsync<ReglaNegocioException>(
            justificaciones.RegularizarAsync(
                Peticion(trabajadorId), "admin", apruebaDirectamente: true, CancellationToken.None));

        sinHora.Message.ShouldContain("al menos una hora");

        var fechaAjena = await Should.ThrowAsync<ReglaNegocioException>(
            justificaciones.RegularizarAsync(
                Peticion(trabajadorId, entrada: new DateTime(2026, 7, 20, 7, 0, 0)),
                "admin", apruebaDirectamente: true, CancellationToken.None));

        fechaAjena.Message.ShouldContain("no corresponde");
    }

    /// <summary>
    /// El coordinador registra y autoriza en un paso: exigirle que se apruebe a sí mismo no
    /// agrega control y convertiría el cierre en una fila de trámites suyos.
    /// </summary>
    [Fact]
    public async Task El_coordinador_puede_registrar_y_aprobar_en_un_paso()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555506");
        await MarcarAsync(trabajadorId, new DateTime(2026, 7, 1, 19, 0, 0));

        await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 7, 0, 0)),
            "coordinador",
            apruebaDirectamente: true,
            CancellationToken.None);

        var guardada = await contexto.Justificaciones.AsNoTracking().FirstAsync(CancellationToken.None);
        guardada.Estado.ShouldBe(EstadoJustificacion.Aprobada);
        guardada.ResueltaPorUsuario.ShouldBe("coordinador");

        (await RecalcularYLeerAsync(trabajadorId)).MinutosTrabajados.ShouldBe(720);
    }

    [Fact]
    public async Task REG_01_olvido_de_ingreso_conserva_la_salida_biometrica_y_separa_la_correccion()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555601");
        var salidaOriginal = new DateTime(2026, 7, 1, 19, 3, 0);
        await MarcarAsync(trabajadorId, salidaOriginal);

        var marcaAntes = await contexto.MarcacionesCrudas.AsNoTracking().SingleAsync();
        var id = await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 6, 58, 0)),
            "admin",
            apruebaDirectamente: false,
            CancellationToken.None);
        await justificaciones.ResolverAsync(
            id, aprueba: true, "Validado con parte de planta", "coordinador", CancellationToken.None);

        var marcaDespues = await contexto.MarcacionesCrudas.AsNoTracking().SingleAsync();
        marcaDespues.Id.ShouldBe(marcaAntes.Id);
        marcaDespues.FechaHora.ShouldBe(salidaOriginal);
        marcaDespues.IdMarcador.ShouldBe(marcaAntes.IdMarcador);
        marcaDespues.ImportacionId.ShouldBe(marcaAntes.ImportacionId);

        var correccion = await contexto.Justificaciones.AsNoTracking().SingleAsync(j => j.Id == id);
        correccion.EntradaRegularizada.ShouldBe(new DateTime(2026, 7, 1, 6, 58, 0));
        correccion.SalidaRegularizada.ShouldBeNull();
        correccion.Motivo.ShouldNotBeNullOrWhiteSpace();

        var tareo = await RecalcularYLeerAsync(trabajadorId);
        tareo.EntradaReal.ShouldBeNull();
        tareo.SalidaReal.ShouldBe(salidaOriginal);
        tareo.JustificacionId.ShouldBe(id);
    }

    [Fact]
    public async Task REG_02_olvido_de_salida_conserva_el_ingreso_biometrico_y_separa_la_correccion()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555602");
        var entradaOriginal = new DateTime(2026, 7, 1, 7, 0, 0);
        await MarcarAsync(trabajadorId, entradaOriginal);

        var id = await justificaciones.RegularizarAsync(
            Peticion(
                trabajadorId,
                salida: new DateTime(2026, 7, 1, 19, 0, 0),
                tipoIncidencia: TipoIncidencia.SalidaNoRegistrada),
            "admin",
            apruebaDirectamente: true,
            CancellationToken.None);

        var marca = await contexto.MarcacionesCrudas.AsNoTracking().SingleAsync();
        marca.FechaHora.ShouldBe(entradaOriginal);

        var correccion = await contexto.Justificaciones.AsNoTracking().SingleAsync(j => j.Id == id);
        correccion.EntradaRegularizada.ShouldBeNull();
        correccion.SalidaRegularizada.ShouldBe(new DateTime(2026, 7, 1, 19, 0, 0));

        var tareo = await RecalcularYLeerAsync(trabajadorId);
        tareo.EntradaReal.ShouldBe(entradaOriginal);
        tareo.SalidaReal.ShouldBeNull();
        tareo.JustificacionId.ShouldBe(id);
    }

    [Fact]
    public async Task REG_03_conserva_motivo_actores_fechas_validacion_y_auditoria()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555603");
        await MarcarAsync(trabajadorId, new DateTime(2026, 7, 1, 19, 0, 0));

        var id = await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 7, 0, 0)),
            "admin",
            apruebaDirectamente: false,
            CancellationToken.None);
        await justificaciones.ResolverAsync(
            id, aprueba: true, "Validado por coordinación", "coordinador", CancellationToken.None);

        var correccion = await contexto.Justificaciones.AsNoTracking().SingleAsync(j => j.Id == id);
        correccion.Motivo.ShouldBe("Olvido de marcación verificado por el supervisor");
        correccion.RegistradoPorUsuario.ShouldBe("admin");
        correccion.RegistradoEn.ShouldNotBe(default);
        correccion.Estado.ShouldBe(EstadoJustificacion.Aprobada);
        correccion.ResueltaPorUsuario.ShouldBe("coordinador");
        correccion.ResueltaEn.ShouldNotBeNull();
        correccion.MotivoResolucion.ShouldBe("Validado por coordinación");

        var auditoria = await contexto.RegistrosAuditoria
            .AsNoTracking()
            .Where(r => r.Tabla == "justificacion" && r.ClaveRegistro == id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToListAsync();
        auditoria.Count.ShouldBeGreaterThanOrEqualTo(2);
        auditoria.ShouldAllBe(r => r.Usuario == "fase-6.2");
        auditoria.ShouldContain(r => r.Detalle.Contains("EntradaRegularizada", StringComparison.Ordinal));
        auditoria.ShouldContain(r => r.Detalle.Contains("ResueltaPorUsuario", StringComparison.Ordinal));
    }

    [Fact]
    public async Task REG_04_una_correccion_posterior_no_sobrescribe_el_hecho_biometrico_ni_el_intento_previo()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555604");
        var salidaOriginal = new DateTime(2026, 7, 1, 19, 3, 0);
        await MarcarAsync(trabajadorId, salidaOriginal);

        var primerId = await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 7, 30, 0)),
            "admin",
            apruebaDirectamente: false,
            CancellationToken.None);
        await justificaciones.ResolverAsync(
            primerId, aprueba: false, "Hora incorrecta", "coordinador", CancellationToken.None);

        var segundoId = await justificaciones.RegularizarAsync(
            Peticion(trabajadorId, entrada: new DateTime(2026, 7, 1, 7, 0, 0)),
            "admin",
            apruebaDirectamente: true,
            CancellationToken.None);

        var marca = await contexto.MarcacionesCrudas.AsNoTracking().SingleAsync();
        marca.FechaHora.ShouldBe(salidaOriginal);

        var correcciones = await contexto.Justificaciones.AsNoTracking().OrderBy(j => j.Id).ToListAsync();
        correcciones.Count.ShouldBe(2);
        correcciones[0].Id.ShouldBe(primerId);
        correcciones[0].Estado.ShouldBe(EstadoJustificacion.Rechazada);
        correcciones[0].EntradaRegularizada.ShouldBe(new DateTime(2026, 7, 1, 7, 30, 0));
        correcciones[1].Id.ShouldBe(segundoId);
        correcciones[1].EntradaRegularizada.ShouldBe(new DateTime(2026, 7, 1, 7, 0, 0));
    }

    // ------------------------------------------------------------------

    private static RegistrarRegularizacionRequest Peticion(
        long trabajadorId,
        DateTime? entrada = null,
        DateTime? salida = null,
        TipoIncidencia tipoIncidencia = TipoIncidencia.IngresoNoRegistrado) =>
        new()
        {
            TrabajadorId = trabajadorId,
            Fecha = Miercoles,
            EntradaRegularizada = entrada,
            SalidaRegularizada = salida,
            TipoIncidencia = tipoIncidencia,
            Motivo = "Olvido de marcación verificado por el supervisor"
        };

    private async Task<TareoDiario> RecalcularYLeerAsync(long trabajadorId)
    {
        await calculo.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        return await contexto.TareosDiarios
            .AsNoTracking()
            .FirstAsync(t => t.TrabajadorId == trabajadorId && t.Fecha == Miercoles);
    }

    private async Task<long> CrearTrabajadorAsync(string documento)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "FLUJO",
            Nombres = "REGULARIZACION",
            Sexo = Sexo.Masculino,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        trabajador.AsignacionesTurno.Add(new AsignacionTurno
        {
            TurnoId = TurnoDiurno,
            FechaInicio = new DateOnly(2020, 1, 1)
        });

        trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            EquipoId = equipoId,
            IdMarcador = documento,
            FechaInicio = new DateOnly(2020, 1, 1)
        });

        trabajador.AbrirVinculo("prueba");
        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }

    private async Task MarcarAsync(long trabajadorId, params DateTime[] momentos)
    {
        var carga = new ImportacionMarcador
        {
            NombreArchivo = "prueba.txt",
            EquipoId = equipoId,
            HashContenido = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            FechaCarga = new DateTime(2026, 8, 1, 9, 0, 0),
            CargadoPorUsuario = "test",
            FilasLeidas = momentos.Length,
            FilasInsertadas = momentos.Length
        };

        contexto.Importaciones.Add(carga);
        await contexto.SaveChangesAsync(CancellationToken.None);

        var numero = await contexto.AsignacionesMarcador
            .Where(a => a.TrabajadorId == trabajadorId)
            .Select(a => a.IdMarcador)
            .FirstAsync(CancellationToken.None);

        foreach (var momento in momentos)
        {
            contexto.MarcacionesCrudas.Add(new MarcacionCruda
            {
                EquipoId = equipoId,
                IdMarcador = numero,
                FechaHora = momento,
                ImportacionId = carga.Id
            });
        }

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private sealed class UsuarioAuditoriaFase62 : IProveedorUsuarioActual
    {
        public string Usuario => "fase-6.2";
    }
}
