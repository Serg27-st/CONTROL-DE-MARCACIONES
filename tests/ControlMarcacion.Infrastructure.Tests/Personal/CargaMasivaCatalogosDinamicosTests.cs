using System.Text;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Auditoria;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using ControlMarcacion.Infrastructure.Persistencia.Repositorios;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using ControlMarcacion.Infrastructure.Personal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Personal;

/// <summary>Contratos CAT-DYN-001 ejecutados contra SQLite relacional real.</summary>
public sealed class CargaMasivaCatalogosDinamicosTests : IDisposable
{
    private const string Empresa = "CONSULTING ADVISERS S.A.C";
    private const string UnidadExistente = "PLANTA DETERGENTES LURÍN";
    private const string Encabezado =
        "tipo_documento;numero_documento;apellido_paterno;apellido_materno;nombres;sexo;" +
        "empresa;unidad;codigo_unidad;tipo_planilla;puesto;fecha_ingreso;id_marcador;" +
        "equipo;codigo_equipo;nombre_equipo;serie_equipo";

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly CargaMasivaTrabajadores carga;

    public CargaMasivaCatalogosDinamicosTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();
        contexto = CrearContexto(conexion);
        contexto.Database.EnsureCreated();
        carga = CrearCarga(contexto, new AlcanceSinRestriccion());
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task CATDYN_01_unidad_existente_se_reutiliza()
    {
        var antes = await contexto.Plantas.CountAsync();
        var resultado = await ConfirmarAsync(Fila("82000001"));
        resultado.Errores.ShouldBeEmpty();
        (await contexto.Trabajadores.SingleAsync(t => t.NumeroDocumento == "82000001")).PlantaId.ShouldBe(1);
        (await contexto.Plantas.CountAsync()).ShouldBe(antes);
    }

    [Fact]
    public async Task CATDYN_02_unidad_inexistente_se_crea()
    {
        var resultado = await ConfirmarAsync(Fila("82000002", "UNIDAD QA SUR", "QA-SUR"));
        resultado.Errores.ShouldBeEmpty();
        var unidad = await contexto.Plantas.SingleAsync(p => p.Codigo == "QA-SUR");
        (await contexto.Trabajadores.SingleAsync(t => t.NumeroDocumento == "82000002")).PlantaId.ShouldBe(unidad.Id);
    }

    [Fact]
    public async Task CATDYN_03_unidad_repetida_se_crea_una_sola_vez()
    {
        var resultado = await ConfirmarAsync(
            Fila("82000003", "UNIDAD LOTE", "QA-LOTE"),
            Fila("82000004", " unidad   lote ", "QA-LOTE"));
        resultado.Errores.ShouldBeEmpty();
        (await contexto.Plantas.CountAsync(p => p.Codigo == "QA-LOTE")).ShouldBe(1);
    }

    [Fact]
    public async Task CATDYN_04_normalizacion_no_duplica_unidad()
    {
        await new ServicioCatalogos(contexto).GuardarAsync(
            "plantas",
            new GuardarCatalogoRequest { Codigo = "QA-Ñ", Nombre = "UNIDAD ÁREA Ñ", PadreId = 1 },
            "tester");
        var resultado = await ConfirmarAsync(Fila("82000005", " unidad   área ñ "));
        resultado.Errores.ShouldBeEmpty();
        (await contexto.Plantas.CountAsync(p => p.Codigo == "QA-Ñ")).ShouldBe(1);
    }

    [Fact]
    public async Task CATDYN_05_empresa_es_obligatoria()
    {
        var fila = Fila("82000006").Replace($";{Empresa};", ";;", StringComparison.Ordinal);
        var vista = await PrevisualizarAsync(fila);
        vista.Errores.Single().Motivo.ShouldContain("empresa");
    }

    [Fact]
    public async Task CATDYN_06_unidad_ambigua_bloquea_la_fila()
    {
        var servicio = new ServicioCatalogos(contexto);
        await servicio.GuardarAsync(
            "plantas",
            new GuardarCatalogoRequest { Codigo = "REF-AMB", Nombre = "UNIDAD AMBIGUA A", PadreId = 1 },
            "tester");
        await servicio.GuardarAsync(
            "plantas",
            new GuardarCatalogoRequest { Codigo = "AMB-B", Nombre = "REF-AMB", PadreId = 1 },
            "tester");
        var vista = await PrevisualizarAsync(Fila("82000007", "REF-AMB"));
        vista.Errores.Count.ShouldBe(1);
        vista.Conciliacion.TieneBloqueos.ShouldBeTrue();
    }

    [Fact]
    public async Task CATDYN_07_previsualizacion_no_escribe()
    {
        var vista = await PrevisualizarAsync(Fila("82000008", "UNIDAD PREVIEW", "QA-PREV"));
        vista.Conciliacion.UnidadesNuevas.ShouldBe(1);
        (await contexto.Plantas.AnyAsync(p => p.Codigo == "QA-PREV")).ShouldBeFalse();
        (await contexto.Trabajadores.AnyAsync(t => t.NumeroDocumento == "82000008")).ShouldBeFalse();
    }

    [Fact]
    public async Task CATDYN_08_cancelacion_no_escribe()
    {
        _ = await PrevisualizarAsync(Fila("82000009", "UNIDAD CANCELADA", "QA-CAN"));
        (await contexto.Plantas.AnyAsync(p => p.Codigo == "QA-CAN")).ShouldBeFalse();
        (await contexto.Trabajadores.AnyAsync(t => t.NumeroDocumento == "82000009")).ShouldBeFalse();
    }

    [Fact]
    public async Task CATDYN_09_vinculo_trabajador_unidad_es_correcto()
    {
        var resultado = await ConfirmarAsync(Fila("82000010", "UNIDAD VINCULO", "QA-VIN"));
        resultado.Errores.ShouldBeEmpty();
        var unidad = await contexto.Plantas.SingleAsync(p => p.Codigo == "QA-VIN");
        var trabajador = await contexto.Trabajadores.SingleAsync(t => t.NumeroDocumento == "82000010");
        trabajador.PlantaId.ShouldBe(unidad.Id);
        (await contexto.AsignacionesPlanta.AnyAsync(a =>
            a.PeriodoLaboral!.TrabajadorId == trabajador.Id && a.PlantaId == unidad.Id))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task CATDYN_10_rollback_no_deja_unidad_huerfana()
    {
        var fila = Fila("82000011", "UNIDAD ROLLBACK", "QA-RB")
            .Replace(";F;", ";SEXO_INVALIDO;", StringComparison.Ordinal);
        var resultado = await ConfirmarAsync(fila);
        resultado.Errores.Count.ShouldBe(1);
        (await contexto.Plantas.AnyAsync(p => p.Codigo == "QA-RB")).ShouldBeFalse();
    }

    [Fact]
    public async Task CATDYN_11_equipo_existente_se_reutiliza()
    {
        var equipoId = await CrearEquipoAsync("EQ-QA-11", "Equipo QA 11");
        var resultado = await ConfirmarAsync(Fila("82000012", equipo: "EQ-QA-11"));
        resultado.Errores.ShouldBeEmpty();
        (await contexto.EquiposMarcador.CountAsync(e => e.Id == equipoId)).ShouldBe(1);
    }

    [Fact]
    public async Task CATDYN_12_equipo_nuevo_se_crea_con_identidad_suficiente()
    {
        var resultado = await ConfirmarAsync(Fila(
            "82000013", equipo: "Equipo nuevo 12", codigoEquipo: "EQ-QA-12", nombreEquipo: "Equipo nuevo 12"));
        resultado.EquiposCreados.Count.ShouldBe(1);
        (await contexto.EquiposMarcador.SingleAsync(e => e.Codigo == "EQ-QA-12")).PlantaId.ShouldBe(1);
    }

    [Fact]
    public async Task CATDYN_13_equipo_no_identificado_se_bloquea()
    {
        await CrearEquipoAsync("EQ-QA-13A", "Equipo QA 13 A");
        await CrearEquipoAsync("EQ-QA-13B", "Equipo QA 13 B");
        var vista = await PrevisualizarAsync(Fila("82000014", marcador: "0013"));
        vista.Errores.Count.ShouldBe(1);
        vista.Conciliacion.TieneBloqueos.ShouldBeTrue();
    }

    [Fact]
    public async Task CATDYN_14_marcador_existente_se_reutiliza()
    {
        var equipo = await CrearEquipoAsync("EQ-QA-14", "Equipo QA 14");
        await CrearMarcadorAsync(equipo, "0014");
        var antes = await contexto.Marcadores.CountAsync();
        var resultado = await ConfirmarAsync(Fila("82000015", marcador: "0014", equipo: "EQ-QA-14"));
        resultado.Errores.ShouldBeEmpty();
        (await contexto.Marcadores.CountAsync()).ShouldBe(antes);
    }

    [Fact]
    public async Task CATDYN_15_marcador_inexistente_se_crea()
    {
        var equipo = await CrearEquipoAsync("EQ-QA-15", "Equipo QA 15");
        var resultado = await ConfirmarAsync(Fila("82000016", marcador: "0015", equipo: "EQ-QA-15"));
        resultado.MarcadoresRegistrados.Count.ShouldBe(1);
        (await contexto.Marcadores.AnyAsync(m => m.EquipoId == equipo && m.Codigo == "0015")).ShouldBeTrue();
    }

    [Fact]
    public async Task CATDYN_16_mismo_codigo_en_equipos_distintos_no_cruza_atribucion()
    {
        var equipoA = await CrearEquipoAsync("EQ-QA-16A", "Equipo QA 16 A");
        var equipoB = await CrearEquipoAsync("EQ-QA-16B", "Equipo QA 16 B");
        await CrearMarcadorAsync(equipoA, "0016");
        var resultado = await ConfirmarAsync(Fila("82000017", marcador: "0016", equipo: "EQ-QA-16B"));
        resultado.Errores.ShouldBeEmpty();
        (await contexto.Marcadores.CountAsync(m => m.Codigo == "0016")).ShouldBe(2);
        (await contexto.AsignacionesMarcador.SingleAsync(a => a.IdMarcador == "0016")).EquipoId.ShouldBe(equipoB);
    }

    [Fact]
    public async Task CATDYN_17_asignacion_temporal_usa_fecha_de_ingreso()
    {
        var equipo = await CrearEquipoAsync("EQ-QA-17", "Equipo QA 17");
        await ConfirmarAsync(Fila("82000018", marcador: "0017", equipo: "EQ-QA-17"));
        var asignacion = await contexto.AsignacionesMarcador.SingleAsync(a => a.EquipoId == equipo && a.IdMarcador == "0017");
        asignacion.FechaInicio.ShouldBe(new DateOnly(2026, 10, 1));
        asignacion.FechaFin.ShouldBeNull();
    }

    [Fact]
    public async Task CATDYN_18_solapamiento_de_marcador_se_rechaza()
    {
        var equipo = await CrearEquipoAsync("EQ-QA-18", "Equipo QA 18");
        await ConfirmarAsync(Fila("82000019", marcador: "0018", equipo: "EQ-QA-18"));
        var resultado = await ConfirmarAsync(Fila("82000020", marcador: "0018", equipo: "EQ-QA-18"));
        resultado.Errores.Count.ShouldBe(1);
        (await contexto.AsignacionesMarcador.CountAsync(a => a.EquipoId == equipo && a.IdMarcador == "0018")).ShouldBe(1);
    }

    [Fact]
    public async Task CATDYN_19_codigo_repetido_en_lote_no_duplica_marcador()
    {
        var equipo = await CrearEquipoAsync("EQ-QA-19", "Equipo QA 19");
        var resultado = await ConfirmarAsync(
            Fila("82000021", marcador: "0019", equipo: "EQ-QA-19"),
            Fila("82000022", marcador: "0019", equipo: "EQ-QA-19"));
        resultado.Errores.Count.ShouldBe(1);
        (await contexto.Marcadores.CountAsync(m => m.EquipoId == equipo && m.Codigo == "0019")).ShouldBe(1);
    }

    [Fact]
    public async Task CATDYN_20_equipo_de_otra_unidad_se_rechaza()
    {
        await CrearEquipoAsync("EQ-QA-20", "Equipo QA 20");
        var vista = await PrevisualizarAsync(Fila(
            "82000023", "UNIDAD DISTINTA", "QA-DIST", marcador: "0020", equipo: "EQ-QA-20"));
        vista.Errores.Count.ShouldBe(1);
        (await contexto.Plantas.AnyAsync(p => p.Codigo == "QA-DIST")).ShouldBeFalse();
    }

    [Fact]
    public async Task CATDYN_21_reimportacion_es_idempotente()
    {
        var fila = Fila(
            "82000024", "UNIDAD IDEMPOTENTE", "QA-IDEM", "0021",
            "Equipo idempotente", "EQ-QA-IDEM", "Equipo idempotente")
            .Replace(";OPERARIO;", ";PUESTO IDEMPOTENTE;", StringComparison.Ordinal);
        await ConfirmarAsync(fila);
        var vista = await PrevisualizarAsync(fila, actualizar: true);
        using var archivo = Archivo(fila);
        var resultado = await carga.ConfirmarAsync(
            archivo, "qa.csv", "tester", vista.HuellaConfirmacion, true, true);
        resultado.Errores.ShouldBeEmpty();
        (await contexto.Plantas.CountAsync(p => p.Codigo == "QA-IDEM")).ShouldBe(1);
        (await contexto.EquiposMarcador.CountAsync(e => e.Codigo == "EQ-QA-IDEM")).ShouldBe(1);
        (await contexto.Marcadores.CountAsync(m => m.Codigo == "0021")).ShouldBe(1);
        (await contexto.AsignacionesMarcador.CountAsync(a => a.IdMarcador == "0021")).ShouldBe(1);
        (await contexto.Puestos.CountAsync(p => p.Nombre == "PUESTO IDEMPOTENTE")).ShouldBe(1);
    }

    [Fact]
    public async Task CATDYN_22_concurrencia_no_duplica_catalogos()
    {
        var filaA = Fila("82000025", "UNIDAD CONCURRENTE", "QA-CON");
        var filaB = Fila("82000026", "UNIDAD CONCURRENTE", "QA-CON");
        var vistaA = await PrevisualizarAsync(filaA);
        var vistaB = await PrevisualizarAsync(filaB);
        var resultados = await Task.WhenAll(
            IntentarConfirmarAsync(filaA, vistaA.HuellaConfirmacion),
            IntentarConfirmarAsync(filaB, vistaB.HuellaConfirmacion));
        resultados.Count(r => r).ShouldBe(1);
        (await contexto.Plantas.CountAsync(p => p.Codigo == "QA-CON")).ShouldBe(1);
    }

    [Fact]
    public async Task CATDYN_23_altas_dinamicas_conservan_auditoria()
    {
        await ConfirmarAsync(Fila(
            "82000027", "UNIDAD AUDITADA", "QA-AUD", equipo: "Equipo auditado",
            codigoEquipo: "EQ-QA-AUD", nombreEquipo: "Equipo auditado"));
        var unidad = await contexto.Plantas.SingleAsync(p => p.Codigo == "QA-AUD");
        var equipo = await contexto.EquiposMarcador.SingleAsync(e => e.Codigo == "EQ-QA-AUD");
        unidad.CreadoPor.ShouldBe("tester");
        equipo.CreadoPor.ShouldBe("tester");
    }

    [Fact]
    public async Task CATDYN_24_actualizacion_preserva_historial_laboral()
    {
        var fila = Fila("82000028");
        await ConfirmarAsync(fila);
        var trabajador = await contexto.Trabajadores.SingleAsync(t => t.NumeroDocumento == "82000028");
        var periodosAntes = await contexto.PeriodosLaborales
            .Where(p => p.TrabajadorId == trabajador.Id)
            .Select(p => new { p.FechaIngreso, p.FechaCese })
            .ToListAsync();
        var vista = await PrevisualizarAsync(fila, actualizar: true);
        using var archivo = Archivo(fila);
        _ = await carga.ConfirmarAsync(archivo, "qa.csv", "tester", vista.HuellaConfirmacion, true, true);
        var periodosDespues = await contexto.PeriodosLaborales
            .Where(p => p.TrabajadorId == trabajador.Id)
            .Select(p => new { p.FechaIngreso, p.FechaCese })
            .ToListAsync();
        periodosDespues.ShouldBe(periodosAntes);
    }

    [Fact]
    public async Task CATDYN_25_plantilla_anterior_sigue_siendo_compatible()
    {
        const string encabezadoAnterior =
            "tipo_documento;numero_documento;apellido_paterno;apellido_materno;nombres;sexo;" +
            "empresa;unidad;tipo_planilla;puesto;fecha_ingreso";
        var fila = $"DNI;82000029;PRUEBA;QA;PERSONA;F;{Empresa};{UnidadExistente};EMPLEADO;OPERARIO;01/10/2026";
        using var vistaArchivo = ArchivoCrudo(encabezadoAnterior, fila);
        var vista = await carga.PrevisualizarAsync(vistaArchivo, "anterior.csv", "tester");
        using var confirmarArchivo = ArchivoCrudo(encabezadoAnterior, fila);
        var resultado = await carga.ConfirmarAsync(
            confirmarArchivo, "anterior.csv", "tester", vista.HuellaConfirmacion, false, true);
        resultado.Errores.ShouldBeEmpty();
    }

    [Fact]
    public async Task CATDYN_26_alias_unidad_planta_es_compatible_y_contradictorio_se_bloquea()
    {
        const string encabezadoPlanta =
            "tipo_documento;numero_documento;apellido_paterno;apellido_materno;nombres;sexo;" +
            "empresa;planta;tipo_planilla;puesto;fecha_ingreso";
        var filaValida = $"DNI;82000030;PRUEBA;QA;PERSONA;F;{Empresa};{UnidadExistente};EMPLEADO;OPERARIO;01/10/2026";
        var vistaValida = await PrevisualizarCrudoAsync(encabezadoPlanta, filaValida);
        vistaValida.Errores.ShouldBeEmpty();

        const string encabezadoContradictorio =
            "tipo_documento;numero_documento;apellido_paterno;apellido_materno;nombres;sexo;" +
            "empresa;unidad;planta;tipo_planilla;puesto;fecha_ingreso";
        var filaInvalida = $"DNI;82000031;PRUEBA;QA;PERSONA;F;{Empresa};{UnidadExistente};OTRA UNIDAD;EMPLEADO;OPERARIO;01/10/2026";
        var vistaInvalida = await PrevisualizarCrudoAsync(encabezadoContradictorio, filaInvalida);
        vistaInvalida.Errores.Single().Motivo.ShouldContain("valores distintos");
    }

    [Fact]
    public async Task CATDYN_27_seguridad_por_unidad_no_se_autoconcede()
    {
        var cargaRestringida = CrearCarga(contexto, new AlcanceDePlantas(1));
        var fila = Fila("82000032", "UNIDAD FUERA ALCANCE", "QA-FUERA");
        var vista = await PrevisualizarAsync(cargaRestringida, fila);
        using var archivo = Archivo(fila);
        var resultado = await cargaRestringida.ConfirmarAsync(
            archivo, "qa.csv", "tester", vista.HuellaConfirmacion, false, true);
        resultado.Errores.Count.ShouldBe(1);
        (await contexto.Plantas.AnyAsync(p => p.Codigo == "QA-FUERA")).ShouldBeFalse();
    }

    [Fact]
    public async Task CATDYN_28_creacion_automatica_de_puesto_sigue_funcionando()
    {
        var fila = Fila("82000033").Replace(";OPERARIO;", ";PUESTO CATDYN;", StringComparison.Ordinal);
        var resultado = await ConfirmarAsync(fila);
        resultado.Errores.ShouldBeEmpty();
        var puesto = await contexto.Puestos.SingleAsync(p => p.Nombre == "PUESTO CATDYN");
        (await contexto.Trabajadores.SingleAsync(t => t.NumeroDocumento == "82000033")).PuestoId.ShouldBe(puesto.Id);
    }

    [Fact]
    public async Task CATDYN_29_error_de_fila_no_genera_entidades_huerfanas()
    {
        var fila = Fila("82000034", "UNIDAD HUERFANA", "QA-HUE", "0029", "Equipo huérfano", "EQ-QA-HUE", "Equipo huérfano")
            .Replace(";F;", ";SEXO_INVALIDO;", StringComparison.Ordinal);
        var resultado = await ConfirmarAsync(fila);
        resultado.Errores.Count.ShouldBe(1);
        (await contexto.Plantas.AnyAsync(p => p.Codigo == "QA-HUE")).ShouldBeFalse();
        (await contexto.EquiposMarcador.AnyAsync(e => e.Codigo == "EQ-QA-HUE")).ShouldBeFalse();
        (await contexto.Marcadores.AnyAsync(m => m.Codigo == "0029")).ShouldBeFalse();
    }

    [Fact]
    public async Task CATDYN_30_importacion_no_toca_marcaciones_ni_tareo()
    {
        var marcacionesAntes = await contexto.MarcacionesCrudas.CountAsync();
        var tareosAntes = await contexto.TareosDiarios.CountAsync();
        await ConfirmarAsync(Fila("82000035", "UNIDAD AISLADA", "QA-AIS"));
        (await contexto.MarcacionesCrudas.CountAsync()).ShouldBe(marcacionesAntes);
        (await contexto.TareosDiarios.CountAsync()).ShouldBe(tareosAntes);
    }

    private async Task<long> CrearEquipoAsync(string codigo, string nombre, string? serie = null) =>
        await new ServicioEquipos(contexto).GuardarAsync(
            new GuardarEquipoRequest
            {
                Codigo = codigo,
                Nombre = nombre,
                Serie = serie,
                PlantaId = 1,
                Activo = true
            },
            "tester");

    private async Task CrearMarcadorAsync(long equipoId, string codigo) =>
        _ = await new ServicioMarcadores(contexto).GuardarAsync(
            new GuardarMarcadorRequest { Codigo = codigo, EquipoId = equipoId, Activo = true },
            "tester");

    private Task<ResultadoCargaMasiva> PrevisualizarAsync(
        string fila,
        bool registrarMarcadores = true,
        bool actualizar = false) =>
        PrevisualizarAsync(carga, fila, registrarMarcadores, actualizar);

    private static async Task<ResultadoCargaMasiva> PrevisualizarAsync(
        CargaMasivaTrabajadores servicio,
        string fila,
        bool registrarMarcadores = true,
        bool actualizar = false)
    {
        using var archivo = Archivo(fila);
        return await servicio.PrevisualizarAsync(
            archivo, "qa.csv", "tester", actualizar, registrarMarcadores);
    }

    private async Task<ResultadoCargaMasiva> PrevisualizarCrudoAsync(string encabezado, string fila)
    {
        using var archivo = ArchivoCrudo(encabezado, fila);
        return await carga.PrevisualizarAsync(archivo, "qa.csv", "tester");
    }

    private async Task<ResultadoCargaMasiva> ConfirmarAsync(params string[] filas)
    {
        using var vistaArchivo = Archivo(filas);
        var vista = await carga.PrevisualizarAsync(vistaArchivo, "qa.csv", "tester");
        using var confirmarArchivo = Archivo(filas);
        return await carga.ConfirmarAsync(
            confirmarArchivo, "qa.csv", "tester", vista.HuellaConfirmacion, false, true);
    }

    private async Task<bool> IntentarConfirmarAsync(string fila, string huella)
    {
        try
        {
            using var archivo = Archivo(fila);
            _ = await carga.ConfirmarAsync(
                archivo, "qa.csv", "tester", huella, false, true);
            return true;
        }
        catch (ReglaNegocioException)
        {
            return false;
        }
    }

    private static string Fila(
        string documento,
        string unidad = UnidadExistente,
        string codigoUnidad = "",
        string marcador = "",
        string equipo = "",
        string codigoEquipo = "",
        string nombreEquipo = "",
        string serie = "") =>
        $"DNI;{documento};PRUEBA;QA;PERSONA;F;{Empresa};{unidad};{codigoUnidad};" +
        $"EMPLEADO;OPERARIO;01/10/2026;{marcador};{equipo};{codigoEquipo};{nombreEquipo};{serie}";

    private static MemoryStream Archivo(params string[] filas) =>
        ArchivoCrudo(Encabezado, filas);

    private static MemoryStream ArchivoCrudo(string encabezado, params string[] filas) =>
        new(Encoding.UTF8.GetBytes(string.Join('\n', new[] { encabezado }.Concat(filas))));

    private static MarcacionDbContext CrearContexto(SqliteConnection conexion) =>
        new(new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexion)
            .AddInterceptors(new InterceptorAuditoria(new UsuarioPrueba()))
            .Options);

    private static CargaMasivaTrabajadores CrearCarga(
        MarcacionDbContext db,
        IProveedorAlcancePlantas alcance)
    {
        var consultaCatalogos = new ConsultaCatalogos(db, alcance);
        var repositorio = new TrabajadorRepository(db);
        return new CargaMasivaTrabajadores(
            db,
            new ServicioTrabajadores(
                repositorio,
                new ConsultaTrabajadores(db, alcance),
                consultaCatalogos,
                alcance,
                new ServicioMarcadores(db),
                new ServicioPeriodos(db)),
            repositorio,
            new ServicioMarcadores(db),
            new ServicioCatalogos(db),
            new ServicioEquipos(db));
    }

    private sealed class UsuarioPrueba : IProveedorUsuarioActual
    {
        public string Usuario => "tester";
    }
}
