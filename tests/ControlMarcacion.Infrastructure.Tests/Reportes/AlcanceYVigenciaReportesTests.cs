using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Domain.Reportes;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Reportes;

/// <summary>
/// Dos cosas que el módulo de reportes no hacía y que corrompían el entregable en silencio:
/// listar gente que no pertenecía al periodo, y entregar cifras viejas sin avisarlo.
/// </summary>
/// <remarks>
/// Usa el proveedor InMemory con la semilla de <c>DatosSemilla</c>, igual que
/// <see cref="ServicioReportesTests"/>: el periodo 1 es "2026-07" (01/07 al 31/07/2026).
/// </remarks>
public sealed class AlcanceYVigenciaReportesTests : IDisposable
{
    private const long PlantaP01 = 3;
    private const long AreaProduccion = 3;
    private const long SeccionGeneral = 3;
    private const long PuestoOperario = 1;
    private const long EmpresaOutsourcing = 2;
    private const long TipoDocumentoDni = 1;
    private const long PeriodoJulio2026 = 1;

    private static readonly DateOnly InicioJulio = new(2026, 7, 1);
    private static readonly DateOnly FinJulio = new(2026, 7, 31);

    private readonly MarcacionDbContext contexto;
    private readonly ServicioReportes servicio;

    public AlcanceYVigenciaReportesTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();

        servicio = new ServicioReportes(contexto);
    }

    public void Dispose() => contexto.Dispose();

    // ------------------------------------------------------------------
    // Punto 2: el padrón se acota al rango
    // ------------------------------------------------------------------

    /// <summary>
    /// Quien cesó antes de que empezara el periodo no es de este tareo.
    /// </summary>
    /// <remarks>
    /// Salía con todo en cero y, en el tareo final, además con su fecha de cese y la
    /// observación «CESE», como si hubiera cesado este mes. <c>Activo</c> no lo filtraba
    /// porque registrar un cese no apaga la ficha: solo escribe <c>FechaCese</c>.
    /// </remarks>
    [Fact]
    public async Task Un_trabajador_cesado_antes_del_periodo_no_aparece()
    {
        await CrearTrabajadorAsync("10000001", "VIGENTE", ingreso: new DateOnly(2020, 1, 1));

        await CrearTrabajadorAsync(
            "10000002", "CESADOENMARZO",
            ingreso: new DateOnly(2020, 1, 1),
            cese: new DateOnly(2026, 3, 31));

        var resumen = await servicio.ResumenTareoAsync(FiltroPeriodo(), CancellationToken.None);

        resumen.Select(r => r.Trabajador).ShouldNotContain(n => n.Contains("CESADOENMARZO"));
        resumen.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Un_trabajador_que_ingresa_despues_del_periodo_no_aparece()
    {
        await CrearTrabajadorAsync("10000003", "VIGENTE", ingreso: new DateOnly(2020, 1, 1));
        await CrearTrabajadorAsync("10000004", "INGRESAENAGOSTO", ingreso: new DateOnly(2026, 8, 3));

        var resumen = await servicio.ResumenTareoAsync(FiltroPeriodo(), CancellationToken.None);

        resumen.Select(r => r.Trabajador).ShouldNotContain(n => n.Contains("INGRESAENAGOSTO"));
    }

    /// <summary>
    /// Quien cesa <b>dentro</b> del periodo sí va: trabajó parte del mes y hay que pagarlo.
    /// </summary>
    /// <remarks>
    /// Es el caso que justifica que la columna FECHA CESE y la observación «CESE» existan en
    /// el formato de GCA. Un filtro que se pasara de estricto lo dejaría fuera de la planilla
    /// del mes en que se le liquida.
    /// </remarks>
    [Fact]
    public async Task Un_trabajador_que_cesa_dentro_del_periodo_si_aparece()
    {
        await CrearTrabajadorAsync(
            "10000005", "CESAENJULIO",
            ingreso: new DateOnly(2020, 1, 1),
            cese: new DateOnly(2026, 7, 15));

        var tareo = await servicio.TareoFinalAsync(FiltroPeriodo(), CancellationToken.None);

        var fila = tareo.Tareo.ShouldHaveSingleItem();
        fila.FechaCese.ShouldBe(new DateOnly(2026, 7, 15));
        fila.Observacion.ShouldBe("CESE");
    }

    /// <summary>El día del cese y el del ingreso cuentan: el solapamiento es inclusive.</summary>
    [Fact]
    public async Task El_cese_el_primer_dia_del_periodo_todavia_cuenta()
    {
        await CrearTrabajadorAsync(
            "10000006", "CESAELPRIMERO",
            ingreso: new DateOnly(2020, 1, 1),
            cese: InicioJulio);

        var resumen = await servicio.ResumenTareoAsync(FiltroPeriodo(), CancellationToken.None);

        resumen.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task El_ingreso_el_ultimo_dia_del_periodo_todavia_cuenta()
    {
        await CrearTrabajadorAsync("10000007", "INGRESAELULTIMO", ingreso: FinJulio);

        var resumen = await servicio.ResumenTareoAsync(FiltroPeriodo(), CancellationToken.None);

        resumen.ShouldHaveSingleItem();
    }

    // ------------------------------------------------------------------
    // Alcance por mes, día y rango
    // ------------------------------------------------------------------

    /// <summary>
    /// Un alcance por día trae solo ese día, aunque el tareo del mes entero esté calculado.
    /// </summary>
    [Fact]
    public async Task El_alcance_por_dia_solo_cuenta_ese_dia()
    {
        var trabajadorId = await CrearTrabajadorAsync(
            "10000008", "PORDIA", ingreso: new DateOnly(2020, 1, 1));

        await AgregarTareoAsync(trabajadorId, new DateOnly(2026, 7, 10), EstadoTareo.Puntual);
        await AgregarTareoAsync(trabajadorId, new DateOnly(2026, 7, 11), EstadoTareo.Puntual);
        await AgregarTareoAsync(trabajadorId, new DateOnly(2026, 7, 12), EstadoTareo.Falta);

        var soloEl10 = await servicio.ResumenTareoAsync(
            new FiltroReporte { Alcance = AlcanceReporte.Dia, Desde = new DateOnly(2026, 7, 10) },
            CancellationToken.None);

        var fila = soloEl10.ShouldHaveSingleItem();
        fila.DiasPuntual.ShouldBe(1);
        fila.DiasFalta.ShouldBe(0);
    }

    [Fact]
    public async Task El_alcance_por_rango_recorta_los_dias_de_los_extremos()
    {
        var trabajadorId = await CrearTrabajadorAsync(
            "10000009", "PORRANGO", ingreso: new DateOnly(2020, 1, 1));

        await AgregarTareoAsync(trabajadorId, new DateOnly(2026, 7, 9), EstadoTareo.Puntual);
        await AgregarTareoAsync(trabajadorId, new DateOnly(2026, 7, 10), EstadoTareo.Puntual);
        await AgregarTareoAsync(trabajadorId, new DateOnly(2026, 7, 11), EstadoTareo.Puntual);
        await AgregarTareoAsync(trabajadorId, new DateOnly(2026, 7, 12), EstadoTareo.Puntual);

        var resumen = await servicio.ResumenTareoAsync(
            new FiltroReporte
            {
                Alcance = AlcanceReporte.Rango,
                Desde = new DateOnly(2026, 7, 10),
                Hasta = new DateOnly(2026, 7, 11)
            },
            CancellationToken.None);

        resumen.ShouldHaveSingleItem().DiasPuntual.ShouldBe(2);
    }

    /// <summary>
    /// El tareo final se niega a salir sobre un alcance que no es un periodo.
    /// </summary>
    /// <remarks>
    /// Sus reintegros se cargan por periodo y su encabezado declara el estado de cierre:
    /// emitirlo sobre medio mes daría un entregable de planilla incompleto con aspecto de
    /// completo.
    /// </remarks>
    [Fact]
    public async Task El_tareo_final_rechaza_un_alcance_que_no_sea_periodo()
    {
        await CrearTrabajadorAsync("10000010", "TAREOFINAL", ingreso: new DateOnly(2020, 1, 1));

        var error = await Should.ThrowAsync<ReglaNegocioException>(() => servicio.TareoFinalAsync(
            new FiltroReporte { Alcance = AlcanceReporte.Mes, Anio = 2026, Mes = 7 },
            CancellationToken.None));

        error.Message.ShouldContain("por periodo de planilla");
    }

    /// <summary>Un periodo inventado falla en los tres reportes, no solo en dos.</summary>
    [Fact]
    public async Task Excepciones_con_un_periodo_inexistente_falla_en_vez_de_decir_que_no_hay_nada()
    {
        await Should.ThrowAsync<RecursoNoEncontradoException>(() => servicio.ExcepcionesAsync(
            new FiltroReporte { Alcance = AlcanceReporte.Periodo, PeriodoId = 9_999 },
            CancellationToken.None));
    }

    // ------------------------------------------------------------------
    // Punto 4: vigencia del cálculo
    // ------------------------------------------------------------------

    [Fact]
    public async Task Sin_tareo_calculado_los_metadatos_lo_declaran()
    {
        var metadatos = await servicio.ObtenerMetadatosAsync(
            FiltroPeriodo(), "prueba", CancellationToken.None);

        metadatos.Calculo.SinCalcular.ShouldBeTrue();
        metadatos.Calculo.Advertencia.ShouldNotBeNull();
        metadatos.Calculo.Advertencia!.ShouldContain("No hay tareo calculado");
    }

    /// <remarks>
    /// ⚠️ Aquí no se pueden usar fechas literales para las <b>marcas de auditoría</b>, a
    /// diferencia del resto de la suite: <c>MarcacionDbContext.SaveChangesAsync</c> sella
    /// <c>CreadoEn = DateTime.Now</c> en toda inserción y descarta el valor que traiga la
    /// entidad —a propósito, para que la auditoría no dependa de que cada servicio se
    /// acuerde—. Por eso el helper devuelve la marca real y el <c>CalculadoEn</c> del tareo
    /// se fija <b>relativo a ella</b>. Lo que se prueba es la comparación entre las dos, que
    /// es la lógica; el instante absoluto es irrelevante y no lo elige el sistema.
    ///
    /// Las fechas de negocio (las jornadas) siguen siendo literales, como manda la
    /// convención.
    /// </remarks>
    [Fact]
    public async Task Un_tareo_posterior_a_la_ultima_marcacion_esta_al_dia()
    {
        var trabajadorId = await CrearTrabajadorAsync(
            "10000011", "ALDIA", ingreso: new DateOnly(2020, 1, 1));

        var cargadaEn = await AgregarMarcacionAsync(trabajadorId, new DateOnly(2026, 7, 10));

        await AgregarTareoAsync(trabajadorId, new DateOnly(2026, 7, 10), EstadoTareo.Puntual,
            calculadoEn: cargadaEn.AddHours(1));

        var metadatos = await servicio.ObtenerMetadatosAsync(
            FiltroPeriodo(), "prueba", CancellationToken.None);

        metadatos.Calculo.EstaAlDia.ShouldBeTrue();
        metadatos.Calculo.Advertencia.ShouldBeNull();
    }

    /// <summary>
    /// Una marcación cargada después del último cálculo desactualiza el reporte.
    /// </summary>
    /// <remarks>
    /// Es el caso real: se calcula el tareo, llega el archivo que faltaba, y el reporte sigue
    /// exportándose con las cifras de antes sin distinguirse en nada de uno bueno.
    /// </remarks>
    [Fact]
    public async Task Una_marcacion_cargada_despues_del_calculo_desactualiza_el_reporte()
    {
        var trabajadorId = await CrearTrabajadorAsync(
            "10000012", "DESACTUALIZADO", ingreso: new DateOnly(2020, 1, 1));

        // La carga llega después del cálculo: es el caso real —se calcula el tareo y luego
        // aparece el archivo que faltaba—, y hasta ahora el reporte seguía saliendo igual.
        var cargadaEn = await AgregarMarcacionAsync(trabajadorId, new DateOnly(2026, 7, 12));

        await AgregarTareoAsync(trabajadorId, new DateOnly(2026, 7, 10), EstadoTareo.Puntual,
            calculadoEn: cargadaEn.AddHours(-2));

        var metadatos = await servicio.ObtenerMetadatosAsync(
            FiltroPeriodo(), "prueba", CancellationToken.None);

        metadatos.Calculo.Desactualizado.ShouldBeTrue();
        metadatos.Calculo.MotivoUltimoCambio.ShouldBe("una carga de marcaciones");
        metadatos.Calculo.Advertencia.ShouldNotBeNull();
        metadatos.Calculo.Advertencia!.ShouldContain("Recalcule el periodo");
    }

    [Fact]
    public async Task Una_justificacion_posterior_al_calculo_tambien_lo_desactualiza()
    {
        var trabajadorId = await CrearTrabajadorAsync(
            "10000013", "CONJUSTIFICACION", ingreso: new DateOnly(2020, 1, 1));

        var justificacion = new Justificacion
        {
            TrabajadorId = trabajadorId,
            PeriodoId = PeriodoJulio2026,
            Fecha = new DateOnly(2026, 7, 10),
            Origen = OrigenJustificacion.Manual,
            Tipo = TipoJustificacion.Ausencia,
            Motivo = "Permiso verbal del supervisor",
            RegistradoPorUsuario = "prueba",
            RegistradoEn = new DateTime(2026, 8, 5, 11, 0, 0)
        };

        await contexto.Justificaciones.AddAsync(justificacion, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        // Mismo motivo que arriba: CreadoEn lo sella el contexto, así que el cálculo se fija
        // relativo a la marca real en vez de a un literal que el guardado descartaría.
        await AgregarTareoAsync(trabajadorId, new DateOnly(2026, 7, 10), EstadoTareo.Falta,
            calculadoEn: justificacion.CreadoEn.AddHours(-2));

        var metadatos = await servicio.ObtenerMetadatosAsync(
            FiltroPeriodo(), "prueba", CancellationToken.None);

        metadatos.Calculo.Desactualizado.ShouldBeTrue();
        metadatos.Calculo.MotivoUltimoCambio.ShouldBe("una justificación nueva o modificada");
    }

    /// <summary>
    /// Lo que pasó fuera del rango no ensucia el aviso.
    /// </summary>
    /// <remarks>
    /// Sin esto, cargar agosto marcaría julio como desactualizado y el aviso se volvería
    /// ruido permanente que nadie mira — que es como muere cualquier advertencia.
    /// </remarks>
    [Fact]
    public async Task Una_marcacion_de_otro_mes_no_desactualiza_este_periodo()
    {
        var trabajadorId = await CrearTrabajadorAsync(
            "10000014", "OTROMES", ingreso: new DateOnly(2020, 1, 1));

        // Marcación de agosto cargada después del cálculo de julio: cambia el tareo de
        // agosto, no el de julio, así que el reporte de julio sigue estando al día.
        var cargadaEn = await AgregarMarcacionAsync(trabajadorId, new DateOnly(2026, 8, 12));

        await AgregarTareoAsync(trabajadorId, new DateOnly(2026, 7, 10), EstadoTareo.Puntual,
            calculadoEn: cargadaEn.AddHours(-2));

        var metadatos = await servicio.ObtenerMetadatosAsync(
            FiltroPeriodo(), "prueba", CancellationToken.None);

        metadatos.Calculo.EstaAlDia.ShouldBeTrue();
    }

    // ------------------------------------------------------------------
    // Metadatos según el alcance
    // ------------------------------------------------------------------

    [Fact]
    public async Task Los_metadatos_de_un_periodo_traen_su_codigo_y_su_estado()
    {
        var metadatos = await servicio.ObtenerMetadatosAsync(
            FiltroPeriodo(), "prueba", CancellationToken.None);

        metadatos.PeriodoCodigo.ShouldBe("2026-07");
        metadatos.PeriodoAbierto.ShouldBe(true);
        metadatos.FechaInicio.ShouldBe(InicioJulio);
        metadatos.FechaFin.ShouldBe(FinJulio);
        metadatos.SufijoArchivo.ShouldBe("2026-07-01_a_2026-07-31");
    }

    /// <summary>
    /// Un alcance por fechas no inventa un estado de apertura que no tiene.
    /// </summary>
    [Fact]
    public async Task Los_metadatos_de_un_dia_no_traen_estado_de_periodo()
    {
        var metadatos = await servicio.ObtenerMetadatosAsync(
            new FiltroReporte { Alcance = AlcanceReporte.Dia, Desde = new DateOnly(2026, 7, 15) },
            "prueba",
            CancellationToken.None);

        metadatos.PeriodoCodigo.ShouldBeNull();
        metadatos.PeriodoAbierto.ShouldBeNull();
        metadatos.Alcance.ShouldBe("Día 15/07/2026");
        metadatos.SufijoArchivo.ShouldBe("2026-07-15");
    }

    // ------------------------------------------------------------------

    private static FiltroReporte FiltroPeriodo() =>
        new() { Alcance = AlcanceReporte.Periodo, PeriodoId = PeriodoJulio2026 };

    private async Task<long> CrearTrabajadorAsync(
        string dni, string apellido, DateOnly ingreso, DateOnly? cese = null)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = dni,
            ApellidoPaterno = apellido,
            ApellidoMaterno = "TEST",
            Nombres = "PRUEBA",
            Sexo = Sexo.Masculino,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            AreaId = AreaProduccion,
            SeccionId = SeccionGeneral,
            PuestoId = PuestoOperario,
            FechaIngreso = ingreso,
            FechaCese = cese,
            MotivoCese = cese is null ? null : MotivoCese.RenunciaVoluntaria
        };

        await contexto.Trabajadores.AddAsync(trabajador, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }

    private async Task AgregarTareoAsync(
        long trabajadorId,
        DateOnly fecha,
        EstadoTareo estado,
        DateTime? calculadoEn = null)
    {
        await contexto.TareosDiarios.AddAsync(new TareoDiario
        {
            PeriodoId = PeriodoJulio2026,
            TrabajadorId = trabajadorId,
            Fecha = fecha,
            EntradaReal = fecha.ToDateTime(new TimeOnly(7, 0)),
            SalidaReal = fecha.ToDateTime(new TimeOnly(19, 0)),
            MinutosTrabajados = 720,
            MinutosOrdinarios = 480,
            Estado = estado,
            TipoDiaNoLaborable = TipoDiaNoLaborable.Ninguno,
            CalculadoEn = calculadoEn ?? new DateTime(2026, 8, 1, 9, 0, 0)
        }, CancellationToken.None);

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Carga una marcación y devuelve <b>la marca de creación que le puso el contexto</b>.
    /// </summary>
    /// <remarks>
    /// El valor de <c>CreadoEn</c> que traiga la entidad se descarta en el guardado, así que
    /// devolverlo es la única forma de que el test compare contra el instante real. Ver la
    /// nota de <see cref="Un_tareo_posterior_a_la_ultima_marcacion_esta_al_dia"/>.
    /// </remarks>
    private async Task<DateTime> AgregarMarcacionAsync(long trabajadorId, DateOnly fecha)
    {
        var importacion = new ImportacionMarcador
        {
            NombreArchivo = "attlog.txt",
            HashContenido = Guid.NewGuid().ToString("N"),
            FechaCarga = fecha.ToDateTime(TimeOnly.MinValue),
            CargadoPorUsuario = "prueba"
        };

        await contexto.Importaciones.AddAsync(importacion, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        var marcacion = new MarcacionCruda
        {
            TrabajadorId = trabajadorId,
            IdMarcador = "001",
            FechaHora = fecha.ToDateTime(new TimeOnly(7, 0)),
            ImportacionId = importacion.Id
        };

        await contexto.MarcacionesCrudas.AddAsync(marcacion, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return marcacion.CreadoEn;
    }
}
