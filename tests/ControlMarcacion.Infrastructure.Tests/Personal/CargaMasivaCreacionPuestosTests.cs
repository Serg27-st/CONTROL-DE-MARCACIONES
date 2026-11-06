using System.Text;
using System.Globalization;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Auditoria;
using ControlMarcacion.Domain.Catalogos;
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

/// <summary>Contratos CAT-PUE-IMPORT-001 sobre SQLite real.</summary>
public sealed class CargaMasivaCreacionPuestosTests : IDisposable
{
    private const string Encabezado =
        "tipo_documento;numero_documento;apellido_paterno;apellido_materno;nombres;sexo;" +
        "empresa;unidad;tipo_planilla;puesto;fecha_ingreso";
    private const string Empresa = "CONSULTING ADVISERS S.A.C";
    private const string Unidad = "PLANTA DETERGENTES LURÍN";

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly CargaMasivaTrabajadores carga;

    public CargaMasivaCreacionPuestosTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();
        contexto = CrearContexto(conexion);
        contexto.Database.EnsureCreated();
        carga = CrearCarga(contexto);
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task PUEIMP_01_puesto_existente_se_reutiliza()
    {
        var cantidadInicial = await contexto.Puestos.CountAsync();

        var resultado = await ConfirmarFilaAsync("81000001", "OPERARIO");

        resultado.Errores.ShouldBeEmpty();
        (await TrabajadorAsync("81000001")).PuestoId.ShouldBe(1);
        (await contexto.Puestos.CountAsync()).ShouldBe(cantidadInicial);
    }

    [Fact]
    public async Task PUEIMP_02_puesto_nuevo_se_crea_una_vez_y_se_vincula()
    {
        var resultado = await ConfirmarFilaAsync("81000002", "Jefe de Cocina");

        resultado.Errores.ShouldBeEmpty();
        var puesto = await contexto.Puestos.SingleAsync(p => p.Nombre == "Jefe de Cocina");
        puesto.EmpresaId.ShouldBe(1);
        puesto.Codigo.Length.ShouldBe(20);
        (await TrabajadorAsync("81000002")).PuestoId.ShouldBe(puesto.Id);
    }

    [Fact]
    public async Task PUEIMP_03_repetido_en_el_lote_crea_un_solo_puesto()
    {
        var resultado = await ConfirmarAsync(
            Fila("81000003", "Recepcionista Nocturno"),
            Fila("81000004", "recepcionista   nocturno"));

        resultado.Errores.ShouldBeEmpty();
        var puestos = await contexto.Puestos
            .Where(p => p.Nombre == "Recepcionista Nocturno")
            .ToListAsync();
        puestos.Count.ShouldBe(1);
        (await TrabajadorAsync("81000003")).PuestoId
            .ShouldBe((await TrabajadorAsync("81000004")).PuestoId);
    }

    [Fact]
    public async Task PUEIMP_04_mayusculas_y_espacios_no_duplican_pero_la_tilde_se_conserva()
    {
        await ConfirmarFilaAsync("81000005", "Chef Ejecutivo");
        await ConfirmarFilaAsync("81000006", "  chef   ejecutivo  ");
        await ConfirmarFilaAsync("81000007", "Chéf Ejecutivo");

        var creados = await contexto.Puestos
            .Where(p => p.EmpresaId == 1 && (p.Nombre == "Chef Ejecutivo" || p.Nombre == "Chéf Ejecutivo"))
            .ToListAsync();
        creados.Count.ShouldBe(2);
        (await TrabajadorAsync("81000005")).PuestoId
            .ShouldBe((await TrabajadorAsync("81000006")).PuestoId);
        (await TrabajadorAsync("81000007")).PuestoId.ShouldNotBe(creados.Single(p => p.Nombre == "Chef Ejecutivo").Id);
    }

    [Theory]
    [InlineData("SIN PUESTO")]
    [InlineData("VACÍO")]
    [InlineData("N/A")]
    [InlineData("DESCONOCIDO")]
    public async Task PUEIMP_05_valores_sentinela_se_rechazan(string valor)
    {
        var resultado = await ConfirmarFilaAsync("81000008", valor);

        resultado.Creados.ShouldBe(0);
        resultado.Errores.Single().Motivo.ShouldContain("no identifica un puesto válido");
        (await contexto.Puestos.AnyAsync(p => p.Nombre == valor)).ShouldBeFalse();
    }

    [Fact]
    public async Task PUEIMP_06_huella_invalida_revierte_antes_de_escribir()
    {
        using var archivo = Archivo(Fila("81000009", "Sommelier"));

        await Should.ThrowAsync<ReglaNegocioException>(() => carga.ConfirmarAsync(
            archivo,
            "personal.csv",
            "tester",
            "HUELLA-ALTERADA",
            registrarMarcadores: false));

        (await contexto.Puestos.AnyAsync(p => p.Nombre == "Sommelier")).ShouldBeFalse();
        (await contexto.Trabajadores.AnyAsync(t => t.NumeroDocumento == "81000009")).ShouldBeFalse();
    }

    [Fact]
    public async Task PUEIMP_06_fila_rechazada_no_deja_puesto_huerfano()
    {
        await ConfirmarFilaAsync("81000018", "OPERARIO");

        var resultado = await ConfirmarAsync(Fila("81000018", "Puesto que debe revertirse"));

        resultado.Errores.Count.ShouldBe(1);
        (await contexto.Puestos.AnyAsync(p => p.Nombre == "Puesto que debe revertirse"))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task PUEIMP_07_dos_confirmaciones_concurrentes_no_duplican_el_puesto()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"pueimp-{Guid.NewGuid():N}.db");

        try
        {
            await using (var inicial = CrearContexto(ruta))
            {
                await inicial.Database.EnsureCreatedAsync();
            }

            await using var contextoUno = CrearContexto(ruta);
            await using var contextoDos = CrearContexto(ruta);
            var cargaUno = CrearCarga(contextoUno);
            var cargaDos = CrearCarga(contextoDos);
            var filaUno = Fila("81000010", "Auditor de Noche");
            var filaDos = Fila("81000011", "Auditor de Noche");
            var vistaUno = await PrevisualizarAsync(cargaUno, filaUno);
            var vistaDos = await PrevisualizarAsync(cargaDos, filaDos);

            var intentos = await Task.WhenAll(
                IntentarConfirmarAsync(cargaUno, filaUno, vistaUno.HuellaConfirmacion),
                IntentarConfirmarAsync(cargaDos, filaDos, vistaDos.HuellaConfirmacion));

            intentos.Count(ok => ok).ShouldBe(1);
            await using var verificacion = CrearContexto(ruta);
            (await verificacion.Puestos.CountAsync(p => p.Nombre == "Auditor de Noche")).ShouldBe(1);
            (await verificacion.Trabajadores.CountAsync(t =>
                t.NumeroDocumento == "81000010" || t.NumeroDocumento == "81000011")).ShouldBe(1);
        }
        finally
        {
            if (File.Exists(ruta))
            {
                SqliteConnection.ClearAllPools();
                File.Delete(ruta);
            }
        }
    }

    [Fact]
    public async Task PUEIMP_08_reimportar_es_idempotente_para_el_catalogo()
    {
        await ConfirmarFilaAsync("81000012", "Botones y Equipaje");
        var puesto = await contexto.Puestos.SingleAsync(p => p.Nombre == "Botones y Equipaje");

        var resultado = await ConfirmarAsync(
            actualizarExistentes: true,
            Fila("81000012", "botones y equipaje"));

        resultado.Errores.ShouldBeEmpty();
        (await contexto.Puestos.CountAsync(p => p.Codigo == puesto.Codigo)).ShouldBe(1);
    }

    [Fact]
    public async Task PUEIMP_09_no_modifica_historia_de_otro_trabajador()
    {
        await ConfirmarFilaAsync("81000013", "OPERARIO");
        var existente = await TrabajadorAsync("81000013");
        var periodoAntes = await contexto.PeriodosLaborales
            .AsNoTracking()
            .SingleAsync(p => p.TrabajadorId == existente.Id);
        var plantasAntes = await contexto.AsignacionesPlanta
            .AsNoTracking()
            .Where(a => a.PeriodoLaboralId == periodoAntes.Id)
            .Select(a => new { a.PlantaId, a.FechaInicio, a.FechaFin })
            .ToListAsync();

        await ConfirmarFilaAsync("81000014", "Gobernanta");

        var periodoDespues = await contexto.PeriodosLaborales
            .AsNoTracking()
            .SingleAsync(p => p.TrabajadorId == existente.Id);
        periodoDespues.FechaIngreso.ShouldBe(periodoAntes.FechaIngreso);
        periodoDespues.FechaCese.ShouldBe(periodoAntes.FechaCese);
        (await contexto.AsignacionesPlanta
            .AsNoTracking()
            .Where(a => a.PeriodoLaboralId == periodoAntes.Id)
            .Select(a => new { a.PlantaId, a.FechaInicio, a.FechaFin })
            .ToListAsync()).ShouldBe(plantasAntes);
    }

    [Fact]
    public async Task PUEIMP_10_previsualizacion_no_deja_escrituras()
    {
        var resultado = await PrevisualizarAsync(carga, Fila("81000015", "Conserje Senior"));

        resultado.Puestos.NuevosPropuestos.Single().Nombre.ShouldBe("Conserje Senior");
        (await contexto.Puestos.AnyAsync(p => p.Nombre == "Conserje Senior")).ShouldBeFalse();
        (await contexto.Trabajadores.AnyAsync(t => t.NumeroDocumento == "81000015")).ShouldBeFalse();
        (await contexto.RegistrosAuditoria.AnyAsync(a => a.Tabla == "puesto")).ShouldBeFalse();
    }

    [Fact]
    public async Task PUEIMP_12_creacion_queda_auditada_con_actor_e_identificador()
    {
        await ConfirmarFilaAsync("81000016", "Mayordomo de Hotel");
        var puesto = await contexto.Puestos.SingleAsync(p => p.Nombre == "Mayordomo de Hotel");
        var auditoria = await contexto.RegistrosAuditoria
            .SingleAsync(a => a.Tabla == "puesto"
                && a.ClaveRegistro == puesto.Id.ToString(CultureInfo.InvariantCulture));

        auditoria.Operacion.ShouldBe(OperacionAuditada.Insercion);
        auditoria.Usuario.ShouldBe("tester");
    }

    [Fact]
    public async Task Puesto_ambiguo_bloquea_la_fila_y_no_crea_un_tercero()
    {
        contexto.Puestos.AddRange(
            new Puesto { EmpresaId = 1, Codigo = "AMB-1", Nombre = "Anfitrión", CreadoEn = DateTime.Now },
            new Puesto { EmpresaId = 1, Codigo = "AMB-2", Nombre = "ANFITRIÓN", CreadoEn = DateTime.Now });
        await contexto.SaveChangesAsync();

        var resultado = await PrevisualizarAsync(carga, Fila("81000017", "anfitrión"));

        resultado.Puestos.Ambiguos.Count.ShouldBe(1);
        resultado.Errores.Single().Motivo.ShouldContain("ambiguo");
        (await contexto.Puestos.CountAsync(p => p.Codigo == "AMB-1" || p.Codigo == "AMB-2")).ShouldBe(2);
    }

    private async Task<ResultadoCargaMasiva> ConfirmarFilaAsync(string documento, string puesto) =>
        await ConfirmarAsync(actualizarExistentes: false, Fila(documento, puesto));

    private async Task<ResultadoCargaMasiva> ConfirmarAsync(params string[] filas) =>
        await ConfirmarAsync(actualizarExistentes: false, filas);

    private async Task<ResultadoCargaMasiva> ConfirmarAsync(
        bool actualizarExistentes,
        params string[] filas)
    {
        var vista = await PrevisualizarAsync(carga, actualizarExistentes, filas);
        using var archivo = Archivo(filas);
        return await carga.ConfirmarAsync(
            archivo,
            "personal.csv",
            "tester",
            vista.HuellaConfirmacion,
            actualizarExistentes,
            registrarMarcadores: false);
    }

    private static async Task<ResultadoCargaMasiva> PrevisualizarAsync(
        CargaMasivaTrabajadores importador,
        params string[] filas)
        => await PrevisualizarAsync(importador, actualizarExistentes: false, filas);

    private static async Task<ResultadoCargaMasiva> PrevisualizarAsync(
        CargaMasivaTrabajadores importador,
        bool actualizarExistentes,
        params string[] filas)
    {
        using var archivo = Archivo(filas);
        return await importador.PrevisualizarAsync(
            archivo,
            "personal.csv",
            "tester",
            actualizarExistentes,
            registrarMarcadores: false);
    }

    private static async Task<bool> IntentarConfirmarAsync(
        CargaMasivaTrabajadores importador,
        string fila,
        string huella)
    {
        using var archivo = Archivo(fila);

        try
        {
            await importador.ConfirmarAsync(
                archivo,
                "personal.csv",
                "tester",
                huella,
                registrarMarcadores: false);
            return true;
        }
        catch (ReglaNegocioException)
        {
            return false;
        }
    }

    private static MemoryStream Archivo(params string[] filas) =>
        new(Encoding.UTF8.GetBytes(string.Join('\n', new[] { Encabezado }.Concat(filas))));

    private static string Fila(string documento, string puesto) =>
        $"DNI;{documento};PRUEBA;IMPORTACION;PERSONA;F;{Empresa};{Unidad};EMPLEADO;{puesto};01/10/2026";

    private async Task<Domain.Personal.Trabajador> TrabajadorAsync(string documento) =>
        await contexto.Trabajadores.AsNoTracking().SingleAsync(t => t.NumeroDocumento == documento);

    private static MarcacionDbContext CrearContexto(SqliteConnection conexion) =>
        new(new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexion)
            .AddInterceptors(new InterceptorAuditoria(new UsuarioPrueba()))
            .Options);

    private static MarcacionDbContext CrearContexto(string ruta) =>
        new(new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite($"Data Source={ruta}")
            .AddInterceptors(new InterceptorAuditoria(new UsuarioPrueba()))
            .Options);

    private static CargaMasivaTrabajadores CrearCarga(MarcacionDbContext db)
    {
        var alcance = new AlcanceSinRestriccion();
        var catalogos = new ConsultaCatalogos(db, alcance);
        var repositorio = new TrabajadorRepository(db);

        return new CargaMasivaTrabajadores(
            db,
            new ServicioTrabajadores(
                repositorio,
                new ConsultaTrabajadores(db, alcance),
                catalogos,
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
