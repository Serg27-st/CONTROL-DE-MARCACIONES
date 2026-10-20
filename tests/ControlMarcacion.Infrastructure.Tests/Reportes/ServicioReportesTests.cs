using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Domain.Solicitudes;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Reportes;

/// <summary>
/// Cubre lo que se agregó en la segunda mitad de la Fase 5: Centro/Ceco desde Área/Sección,
/// movilidad, asignación familiar, bono nocturno y reintegros manuales.
/// </summary>
/// <remarks>
/// Usa el proveedor InMemory de EF Core: <see cref="MarcacionDbContext.OnModelCreating"/>
/// siembra la organización, los catálogos, las tasas y los parámetros legales igual que en
/// SQL Server (<c>HasData</c> es independiente del proveedor), así que estas pruebas
/// arrancan con el mismo Periodo "2026-07", Planta 01 / Área PROD / Sección GEN, RMV,
/// % asignación familiar y sobretasa nocturna que ya están sembrados en <c>DatosSemilla</c>.
/// Cada test usa una base nueva (<see cref="Guid.NewGuid"/>) para no arrastrar datos de otro.
/// </remarks>
public sealed class ServicioReportesTests : IDisposable
{
    private const long PlantaP01 = 3;
    private const long AreaProduccion = 3;
    private const long SeccionGeneral = 3;
    private const long PuestoOperario = 1;
    private const long EmpresaOutsourcing = 2;
    private const long TipoDocumentoDni = 1;
    private const long PeriodoJulio2026 = 1;

    /// <summary>Ids de los tipos subsidiados en el catálogo sembrado por <c>DatosSemilla</c>.</summary>
    private const long TipoDescansoMedico = 2;
    private const long TipoSubsidioEnfermedad = 8;
    private const long TipoSubsidioAccidente = 9;

    private readonly MarcacionDbContext contexto;
    private readonly ServicioReportes servicio;

    public ServicioReportesTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();

        servicio = new ServicioReportes(contexto, new AlcanceSinRestriccion());
    }

    public void Dispose() => contexto.Dispose();

    // ------------------------------------------------------------------
    // Punto 3: Centro/Ceco desde Área/Sección
    // ------------------------------------------------------------------

    [Fact]
    public async Task Las_tres_columnas_de_ceco_salen_del_catalogo_real_de_sap()
    {
        // CentroCosto 1 del catálogo sembrado: PE21400700 / PRODUCCIÓN PET RECICLADO,
        // colgando del Centro 066D de CA Outsourcing.
        var trabajadorId = await CrearTrabajadorAsync(
            "11111111", "PRUEBA", "CENTRO", centroCostoId: 1);

        var tareo = await servicio.TareoFinalAsync(Filtro(), CancellationToken.None);

        var fila = tareo.Tareo.Single(f => f.Dni == "11111111");

        // CENTRO = CO_AREA, CENTRO DE COSTO = CO_SECC, DESCRIP. CECO = DE_SECC: las tres de
        // la misma fila del catálogo, nunca de fuentes distintas.
        fila.Centro.ShouldBe("066D");
        fila.CentroCosto.ShouldBe("PE21400700");
        fila.DescripcionCeco.ShouldBe("PRODUCCIÓN PET RECICLADO");

        // AREA es columna propia y no se deriva del CECO: sigue siendo el área interna.
        fila.Area.ShouldBe("Producción");

        trabajadorId.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Sin_centro_de_costo_asignado_el_ceco_sale_vacio_y_no_se_aproxima_con_area()
    {
        var trabajadorId = await CrearTrabajadorAsync("11111112", "PRUEBA", "SINCECO");

        var tareo = await servicio.TareoFinalAsync(Filtro(), CancellationToken.None);

        var fila = tareo.Tareo.Single(f => f.Dni == "11111112");

        // Rellenar el CECO con Área/Sección daría un código que no existe en SAP: el dato
        // falta y así se ve, en vez de salir un PROD/GEN que planilla tomaría por real.
        fila.Centro.ShouldBeNull();
        fila.CentroCosto.ShouldBeNull();
        fila.DescripcionCeco.ShouldBeNull();

        // AREA sí se llena: es un dato propio del trabajador, ajeno al catálogo de SAP.
        fila.Area.ShouldBe("Producción");

        trabajadorId.ShouldBeGreaterThan(0);
    }

    // ------------------------------------------------------------------
    // Punto 4: movilidad y asignación familiar
    // ------------------------------------------------------------------

    [Fact]
    public async Task Movilidad_cuenta_solo_dias_con_marcacion_real_no_minutos_trabajados()
    {
        var trabajadorId = await CrearTrabajadorAsync("22222222", "PRUEBA", "MOVILIDAD");

        // Día asistido de verdad: marcación real de entrada y salida, Puntual.
        await AgregarTareoDiarioAsync(trabajadorId, new DateOnly(2026, 7, 6), EstadoTareo.Puntual,
            conMarcacionReal: true, minutosTrabajados: 480);

        // Día de vacaciones: CalculadoraTareo.Aplicar() rellena MinutosTrabajados para que se
        // pague en horas, pero el trabajador no viajó a la planta — no debe contar.
        await AgregarTareoDiarioAsync(trabajadorId, new DateOnly(2026, 7, 7), EstadoTareo.AusenciaJustificada,
            conMarcacionReal: false, minutosTrabajados: 480);

        var tareo = await servicio.TareoFinalAsync(Filtro(), CancellationToken.None);

        var fila = tareo.Tareo.Single(f => f.Dni == "22222222");

        // ValorMovilidadDiaria sembrado = 3.00; solo 1 día asistido de verdad.
        fila.MontoMovilidad.ShouldBe(3.00m);
    }

    [Fact]
    public async Task Asignacion_familiar_es_monto_fijo_mensual_solo_si_tiene_hijos()
    {
        var conHijos = await CrearTrabajadorAsync("33333333", "PRUEBA", "CONHIJOS", tieneHijos: true);
        var sinHijos = await CrearTrabajadorAsync("33333334", "PRUEBA", "SINHIJOS", tieneHijos: false);

        var tareo = await servicio.TareoFinalAsync(Filtro(), CancellationToken.None);

        // 10 % de 1130 = 113.00, sembrados ambos en DatosSemilla.
        tareo.Tareo.Single(f => f.Dni == "33333333").MontoAsignacionFamiliar.ShouldBe(113.00m);
        tareo.Tareo.Single(f => f.Dni == "33333334").MontoAsignacionFamiliar.ShouldBe(0m);

        conHijos.ShouldNotBe(sinHijos);
    }

    [Fact]
    public async Task Sin_rmv_vigente_para_el_periodo_falla_en_vez_de_devolver_cero()
    {
        // Un periodo de 2024 no tiene ningún ParametroLegal vigente: la RMV sembrada arranca
        // el 2025-01-01. La REGLA DURA dice que esto debe fallar a la vista, no dar 0.
        var periodoAntiguoId = await CrearPeriodoAsync("2024-01", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31));
        await CrearTrabajadorAsync("44444444", "PRUEBA", "SINRMV");

        var excepcion = await Should.ThrowAsync<ReglaNegocioException>(
            () => servicio.TareoFinalAsync(Filtro(periodoAntiguoId), CancellationToken.None));

        // No importa cuál de los tres parámetros (RMV, % asignación familiar o movilidad
        // diaria) se resuelva primero: lo que importa es que ninguno se deje pasar en
        // silencio con un 0, y que el mensaje diga qué hacer.
        excepcion.Message.ShouldContain("vigente para el 31/01/2024");
        excepcion.Message.ShouldContain("cargue el parámetro legal");
    }

    // ------------------------------------------------------------------
    // Punto 5: bono nocturno, resuelto día por día
    // ------------------------------------------------------------------

    [Fact]
    public async Task Bono_nocturno_usa_el_sueldo_vigente_de_cada_dia_no_uno_fijo_del_periodo()
    {
        var trabajadorId = await CrearTrabajadorAsync("55555555", "PRUEBA", "BONONOCTURNO");

        // El sueldo cambia a mitad de mes: el día 10 debe usar 1000, el día 20 debe usar 1300.
        await contexto.AsignacionesSueldo.AddRangeAsync(
            new AsignacionSueldo
            {
                TrabajadorId = trabajadorId, SueldoBasicoMensual = 1000m,
                FechaInicio = new DateOnly(2026, 7, 1), FechaFin = new DateOnly(2026, 7, 15)
            },
            new AsignacionSueldo
            {
                TrabajadorId = trabajadorId, SueldoBasicoMensual = 1300m,
                FechaInicio = new DateOnly(2026, 7, 16), FechaFin = null
            });
        await contexto.SaveChangesAsync(CancellationToken.None);

        await AgregarTareoDiarioAsync(trabajadorId, new DateOnly(2026, 7, 10), EstadoTareo.Puntual,
            conMarcacionReal: true, minutosTrabajados: 480, minutosNocturnos: 60);
        await AgregarTareoDiarioAsync(trabajadorId, new DateOnly(2026, 7, 20), EstadoTareo.Puntual,
            conMarcacionReal: true, minutosTrabajados: 480, minutosNocturnos: 120);

        var resumen = await servicio.ResumenTareoAsync(Filtro(), CancellationToken.None);

        var esperado = CalculadoraBonoNocturno.BonoDiario(1130m, 35m, 1000m, 31)
                       + CalculadoraBonoNocturno.BonoDiario(1130m, 35m, 1300m, 31);

        resumen.Single(r => r.TrabajadorId == trabajadorId).MontoBonoNocturno.ShouldBe(esperado);
    }

    [Fact]
    public async Task Bono_nocturno_falla_si_el_trabajador_no_tiene_sueldo_basico_asignado()
    {
        var trabajadorId = await CrearTrabajadorAsync("66666666", "PRUEBA", "SINSUELDO");

        // Minutos en franja nocturna, pero SIN ninguna AsignacionSueldo: no hay con qué
        // calcular el piso. Debe fallar, no asumir sueldo 0 (que dispararía el bono al alza).
        await AgregarTareoDiarioAsync(trabajadorId, new DateOnly(2026, 7, 10), EstadoTareo.Puntual,
            conMarcacionReal: true, minutosTrabajados: 480, minutosNocturnos: 60);

        var excepcion = await Should.ThrowAsync<ReglaNegocioException>(
            () => servicio.ResumenTareoAsync(Filtro(), CancellationToken.None));

        excepcion.Message.ShouldContain("sueldo básico");
    }

    // ------------------------------------------------------------------
    // Punto 6: reintegros manuales
    // ------------------------------------------------------------------

    [Fact]
    public async Task Los_reintegros_cargados_se_reflejan_en_la_fila_y_los_no_cargados_salen_en_cero()
    {
        var conReintegro = await CrearTrabajadorAsync("77777777", "PRUEBA", "CONREINTEGRO");
        var sinReintegro = await CrearTrabajadorAsync("77777778", "PRUEBA", "SINREINTEGRO");

        await contexto.ReintegrosTareo.AddAsync(new ReintegroTareo
        {
            TrabajadorId = conReintegro,
            PeriodoId = PeriodoJulio2026,
            HorasNormales = 8m,
            Viatico = 50m,
            DiasDescansoMedico = 2,
            ReintegroAsignacionFamiliar = 113m,
            DescuentoPagoIndebido = 25m
        }, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        var tareo = await servicio.TareoFinalAsync(Filtro(), CancellationToken.None);

        var filaConReintegro = tareo.Tareo.Single(f => f.Dni == "77777777");
        filaConReintegro.ReintegroHorasNormales.ShouldBe(8m);
        filaConReintegro.ReintegroViatico.ShouldBe(50m);
        filaConReintegro.ReintegroDiasDescansoMedico.ShouldBe(2);
        filaConReintegro.ReintegroAsignacionFamiliar.ShouldBe(113m);
        filaConReintegro.ReintegroDescuentoPagoIndebido.ShouldBe(25m);

        var filaSinReintegro = tareo.Tareo.Single(f => f.Dni == "77777778");
        filaSinReintegro.ReintegroHorasNormales.ShouldBe(0m);
        filaSinReintegro.ReintegroViatico.ShouldBe(0m);
        filaSinReintegro.ReintegroDescuentoPagoIndebido.ShouldBe(0m);

        sinReintegro.ShouldNotBe(conReintegro);
    }

    // ------------------------------------------------------------------
    // Detalle clínico de la hoja "Descanso Medico"
    // ------------------------------------------------------------------

    [Fact]
    public async Task El_detalle_clinico_de_la_solicitud_llega_a_la_hoja_de_descanso_medico()
    {
        var trabajadorId = await CrearTrabajadorAsync("88888888", "PRUEBA", "CLINICO");

        await CrearSolicitudAprobadaAsync(
            trabajadorId,
            tipoAusenciaId: TipoDescansoMedico,
            new DateOnly(2026, 7, 6),
            new DateOnly(2026, 7, 8),
            Contingencia.AccidenteLaboral,
            "Fractura de radio distal",
            CentroAtencion.Particular);

        var tareo = await servicio.TareoFinalAsync(Filtro(), CancellationToken.None);

        var fila = tareo.DescansosMedicos.Single(d => d.Dni == "88888888");

        // Texto exacto del archivo de GCA: en mayúsculas y sin tildes, no el ToString() del
        // enum ("AccidenteLaboral"), que rompería los filtros de su plantilla.
        fila.Contingencia.ShouldBe("ACCIDENTE LABORAL");
        fila.CentroAtencion.ShouldBe("PARTICULAR");
        fila.Diagnostico.ShouldBe("Fractura de radio distal");
        fila.DiasEnElMes.ShouldBe(3);
    }

    [Fact]
    public async Task Una_solicitud_sin_detalle_clinico_deja_las_tres_columnas_vacias()
    {
        // Es el caso de las solicitudes registradas antes de que existieran estos campos:
        // salen vacías, nunca con un valor inventado por defecto.
        var trabajadorId = await CrearTrabajadorAsync("88888889", "PRUEBA", "SINCLINICO");

        await CrearSolicitudAprobadaAsync(
            trabajadorId,
            tipoAusenciaId: TipoDescansoMedico,
            new DateOnly(2026, 7, 6),
            new DateOnly(2026, 7, 7),
            contingencia: null,
            diagnostico: null,
            centroAtencion: null);

        var tareo = await servicio.TareoFinalAsync(Filtro(), CancellationToken.None);

        var fila = tareo.DescansosMedicos.Single(d => d.Dni == "88888889");

        fila.Contingencia.ShouldBeNull();
        fila.Diagnostico.ShouldBeNull();
        fila.CentroAtencion.ShouldBeNull();
    }

    [Fact]
    public async Task La_hoja_lista_los_tres_tipos_subsidiados_con_su_observacion()
    {
        // El formato de GCA define la hoja como el detalle de descansos médicos Y
        // subsidios: el descanso corto va como DESCANSO MEDICO y los dos subsidios como
        // SUBSIDIO. Dejar fuera al de accidente hacía que su detalle clínico —siempre
        // ACCIDENTE LABORAL— se capturara para no llegar a ninguna parte.
        var dm = await CrearTrabajadorAsync("90000001", "PRUEBA", "DM");
        var sube = await CrearTrabajadorAsync("90000002", "PRUEBA", "SUBE");
        var suba = await CrearTrabajadorAsync("90000003", "PRUEBA", "SUBA");

        await CrearSolicitudAprobadaAsync(dm, TipoDescansoMedico, new DateOnly(2026, 7, 6),
            new DateOnly(2026, 7, 7), Contingencia.EnfermedadComun, "Gripe", CentroAtencion.Essalud);
        await CrearSolicitudAprobadaAsync(sube, TipoSubsidioEnfermedad, new DateOnly(2026, 7, 6),
            new DateOnly(2026, 7, 9), Contingencia.EnfermedadComun, "Neumonía", CentroAtencion.Essalud);
        await CrearSolicitudAprobadaAsync(suba, TipoSubsidioAccidente, new DateOnly(2026, 7, 6),
            new DateOnly(2026, 7, 10), Contingencia.AccidenteLaboral, "Fractura", CentroAtencion.Particular);

        var tareo = await servicio.TareoFinalAsync(Filtro(), CancellationToken.None);

        tareo.DescansosMedicos.Count.ShouldBe(3);
        tareo.DescansosMedicos.Single(d => d.Dni == "90000001").Observacion.ShouldBe("DESCANSO MEDICO");
        tareo.DescansosMedicos.Single(d => d.Dni == "90000002").Observacion.ShouldBe("SUBSIDIO");

        var accidente = tareo.DescansosMedicos.Single(d => d.Dni == "90000003");
        accidente.Observacion.ShouldBe("SUBSIDIO");
        accidente.Contingencia.ShouldBe("ACCIDENTE LABORAL");
    }

    // ------------------------------------------------------------------

    private static FiltroReporte Filtro(long periodoId = PeriodoJulio2026) => new() { PeriodoId = periodoId };

    private async Task CrearSolicitudAprobadaAsync(
        long trabajadorId,
        long tipoAusenciaId,
        DateOnly inicio,
        DateOnly fin,
        Contingencia? contingencia,
        string? diagnostico,
        CentroAtencion? centroAtencion)
    {
        await contexto.SolicitudesAusencia.AddAsync(new SolicitudAusencia
        {
            TrabajadorId = trabajadorId,
            TipoAusenciaId = tipoAusenciaId,
            FechaInicio = inicio,
            FechaFin = fin,
            Estado = EstadoSolicitud.Aprobada,
            Sustento = "Certificado médico",
            Contingencia = contingencia,
            Diagnostico = diagnostico,
            CentroAtencion = centroAtencion
        }, CancellationToken.None);

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<long> CrearTrabajadorAsync(
        string dni, string apellido, string nombres, bool tieneHijos = false,
        long? centroCostoId = null)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = dni,
            ApellidoPaterno = apellido,
            ApellidoMaterno = "TEST",
            Nombres = nombres,
            Sexo = Sexo.Masculino,
            TieneHijos = tieneHijos,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            AreaId = AreaProduccion,
            SeccionId = SeccionGeneral,
            PuestoId = PuestoOperario,
            CentroCostoId = centroCostoId,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        trabajador.AbrirVinculo("prueba");
        await contexto.Trabajadores.AddAsync(trabajador, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }

    private async Task<long> CrearPeriodoAsync(string codigo, DateOnly inicio, DateOnly fin)
    {
        var periodo = new Periodo
        {
            Codigo = codigo,
            Nombre = codigo,
            FechaInicio = inicio,
            FechaFin = fin,
            Estado = EstadoPeriodo.Abierto
        };

        await contexto.Periodos.AddAsync(periodo, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return periodo.Id;
    }

    private async Task AgregarTareoDiarioAsync(
        long trabajadorId,
        DateOnly fecha,
        EstadoTareo estado,
        bool conMarcacionReal,
        int minutosTrabajados,
        int minutosNocturnos = 0)
    {
        await contexto.TareosDiarios.AddAsync(new TareoDiario
        {
            PeriodoId = PeriodoJulio2026,
            TrabajadorId = trabajadorId,
            Fecha = fecha,
            EntradaReal = conMarcacionReal ? fecha.ToDateTime(new TimeOnly(7, 0)) : null,
            SalidaReal = conMarcacionReal ? fecha.ToDateTime(new TimeOnly(19, 0)) : null,
            MinutosTrabajados = minutosTrabajados,
            MinutosOrdinarios = minutosTrabajados,
            MinutosNocturnos = minutosNocturnos,
            Estado = estado,
            TipoDiaNoLaborable = TipoDiaNoLaborable.Ninguno,
            CalculadoEn = DateTime.Now
        }, CancellationToken.None);

        await contexto.SaveChangesAsync(CancellationToken.None);
    }
}
