using System.Text;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using ControlMarcacion.Infrastructure.Persistencia.Repositorios;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using ControlMarcacion.Infrastructure.Personal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Personal;

/// <summary>
/// Los padrones que enviaron los coordinadores de planta el 18/09/2026 se resuelven contra
/// los catálogos sembrados: unidad, puesto, área y centro de costo tal como ellos los
/// escriben.
/// </summary>
/// <remarks>
/// Corre sobre <b>SQLite en memoria</b> con la semilla real, por lo mismo que
/// <see cref="CargaMasivaRegistroDeMarcadoresTests"/>: la carga abre una transacción.
/// Los ids esperados son los de <c>DatosSemilla</c>.
/// </remarks>
public sealed class CargaMasivaPadronesDePlantaTests : IDisposable
{
    private const string Encabezado =
        "tipo_documento;numero_documento;apellido_paterno;apellido_materno;nombres;sexo;" +
        "tiene_hijos;empresa;unidad;tipo_planilla;puesto;fecha_ingreso;centro_costo;area";

    private const string CaOutsourcing = "CA OUTSOURCING SAC";
    private const string ConsultingAdvisers = "CONSULTING ADVISERS S.A.C";

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly CargaMasivaTrabajadores carga;

    public CargaMasivaPadronesDePlantaTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        var catalogos = new ConsultaCatalogos(contexto, new AlcanceSinRestriccion());
        var alcance = new AlcanceSinRestriccion();
        var repositorio = new TrabajadorRepository(contexto);

        carga = new CargaMasivaTrabajadores(
            contexto,
            new ServicioTrabajadores(
                repositorio, new ConsultaTrabajadores(contexto, alcance), catalogos, alcance,
                new ServicioMarcadores(contexto), new ServicioPeriodos(contexto)),
            repositorio,
            catalogos,
            new ServicioMarcadores(contexto));
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    /// <summary>
    /// <c>PE21430001</c> existe bajo tres centros de CA Outsourcing. Antes ganaba el
    /// primero (Planta Intracloro); ahora gana el que atiende a la planta de la fila.
    /// </summary>
    [Fact]
    public async Task Planta_1_resuelve_el_centro_de_costo_repetido_en_su_propio_centro()
    {
        var resultado = await CargarAsync(
            Fila("41300001", CaOutsourcing, "PLANTA 1", "OPERARIO", "PE21430001", "Gerencia de Produccion"));

        resultado.Errores.ShouldBeEmpty();

        var trabajador = await TrabajadorAsync("41300001");
        trabajador.CentroCostoId.ShouldBe(49);  // PE21430001 bajo 066E CENTRO LURÍN
        trabajador.PuestoId.ShouldBe(1);        // OPERARIO, catálogo general
        trabajador.AreaId.ShouldBe(48);         // Gerencia de Produccion, Planta 1
        trabajador.PlantaId.ShouldBe(3);
    }

    /// <summary>
    /// Cada grafía de los padrones y cada código de planilla cae en el mismo puesto del
    /// catálogo general, sea cual sea la empresa de la fila.
    /// </summary>
    [Theory]
    [InlineData("O. MAQUINA", ConsultingAdvisers, "PLANTA DETERGENTES LURÍN", 40)]
    [InlineData("MAQUINISTA", CaOutsourcing, "PLANTA 1", 40)]
    [InlineData("26006", ConsultingAdvisers, "PLANTA DETERGENTES LURÍN", 40)]
    [InlineData("Operario", CaOutsourcing, "PLANTA 1", 1)]
    [InlineData("26001", ConsultingAdvisers, "PLANTA DETERGENTES LURÍN", 1)]
    [InlineData("TRASPALETISTA", CaOutsourcing, "PLANTA 1", 41)]
    [InlineData("O. TRASPALET", ConsultingAdvisers, "PLANTA DETERGENTES LURÍN", 41)]
    [InlineData("SSOMA", ConsultingAdvisers, "PLANTA DETERGENTES LURÍN", 44)]
    [InlineData("INSPECTOR SSOMA", CaOutsourcing, "PLANTA 1", 44)]
    [InlineData("COORD. RRHH", ConsultingAdvisers, "PLANTA DETERGENTES LURÍN", 46)]
    [InlineData("Supervisor", CaOutsourcing, "PLANTA 1", 2)]
    public async Task Los_sinonimos_de_puesto_caen_en_el_catalogo_general(
        string puesto, string empresa, string unidad, long esperado)
    {
        var resultado = await CargarAsync(
            Fila("41300006", empresa, unidad, puesto, string.Empty, string.Empty));

        resultado.Errores.ShouldBeEmpty();
        (await TrabajadorAsync("41300006")).PuestoId.ShouldBe(esperado);
    }

    [Fact]
    public async Task Intracan_toma_su_propio_centro_de_costo()
    {
        var resultado = await CargarAsync(
            Fila("41300002", ConsultingAdvisers, "PLANTA INTRACAN/FEM", "OPERARIO", "PE21400607", "Producción Etiquetado/Codificado"),
            Fila("41300003", ConsultingAdvisers, "PLANTA INTRACAN/FEM", "O. MAQUINA", "PE21400506", "Acabados fibras, paños y otros"));

        resultado.Errores.ShouldBeEmpty();

        var etiquetado = await TrabajadorAsync("41300002");
        etiquetado.CentroCostoId.ShouldBe(107);  // agregado bajo 066H
        etiquetado.PuestoId.ShouldBe(1);         // OPERARIO, catálogo general
        etiquetado.AreaId.ShouldBe(33);

        // PE21400506 está en Detergentes (79) y en Intracan (96).
        (await TrabajadorAsync("41300003")).CentroCostoId.ShouldBe(96);
    }

    [Fact]
    public async Task Un_centro_de_costo_de_la_otra_empresa_rechaza_la_fila()
    {
        // PE21400602 solo existe bajo Centro Lurín, de CA Outsourcing.
        var resultado = await CargarAsync(
            Fila("41300004", ConsultingAdvisers, "PLANTA DETERGENTES LURÍN", "OPERARIO", "PE21400602", string.Empty));

        resultado.Creados.ShouldBe(0);
        resultado.Errores.Single().Motivo.ShouldContain("no existe en el catálogo de la empresa");
    }

    /// <summary>
    /// Los padrones escriben en "tiene hijos" la cantidad. Leer "3" como NO le quitaba la
    /// asignación familiar a quien más hijos tiene.
    /// </summary>
    [Theory]
    [InlineData("3", true)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("SI", true)]
    [InlineData("NO", false)]
    public async Task Tiene_hijos_acepta_la_cantidad(string valor, bool esperado)
    {
        var resultado = await CargarAsync(
            Fila("41300005", CaOutsourcing, "PLANTA 1", "MAQUINISTA", string.Empty, string.Empty, hijos: valor));

        resultado.Errores.ShouldBeEmpty();
        (await TrabajadorAsync("41300005")).TieneHijos.ShouldBe(esperado);
    }

    [Fact]
    public async Task C_E_con_puntos_es_carne_de_extranjeria()
    {
        var resultado = await CargarAsync(
            Fila("007703217", CaOutsourcing, "PLANTA 1", "OPERARIO", string.Empty, string.Empty, tipoDocumento: "C.E"));

        resultado.Errores.ShouldBeEmpty();
        (await TrabajadorAsync("007703217")).TipoDocumentoId.ShouldBe(2);
    }

    // ------------------------------------------------------------------

    private static string Fila(
        string documento,
        string empresa,
        string unidad,
        string puesto,
        string centroCosto,
        string area,
        string hijos = "NO",
        string tipoDocumento = "DNI") =>
        $"{tipoDocumento};{documento};PRUEBA;CARGA;PERSONA;F;{hijos};{empresa};{unidad};OBRERO;{puesto};" +
        $"01/06/2026;{centroCosto};{area}";

    private async Task<ResultadoCargaMasiva> CargarAsync(params string[] filas)
    {
        var lineas = new List<string> { Encabezado };
        lineas.AddRange(filas);

        using var archivo = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", lineas)));

        return await carga.ProcesarAsync(
            archivo,
            "padron.csv",
            "tester",
            soloValidar: false,
            actualizarExistentes: false,
            registrarMarcadores: false,
            CancellationToken.None);
    }

    private async Task<Domain.Personal.Trabajador> TrabajadorAsync(string documento) =>
        await contexto.Trabajadores
            .AsNoTracking()
            .SingleAsync(t => t.NumeroDocumento == documento, CancellationToken.None);
}
