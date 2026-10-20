using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Reportes;

/// <summary>
/// El filtro por aparato de los reportes, y de qué fecha sale el número que muestran.
/// </summary>
/// <remarks>
/// <para>
/// Con dos aparatos por planta hacía falta poder pedir «el resumen del A-L», pero un filtro
/// por la asignación abierta hoy habría sido el invariante 5 roto de nuevo: quien cambió de
/// aparato en agosto desaparecería del reporte de julio, donde sus marcaciones sí salieron
/// del aparato pedido. Todo se resuelve por <b>solapamiento con el rango</b>, igual que el
/// padrón.
/// </para>
/// <para>
/// Sobre <b>SQLite real</b>: el filtro es una subconsulta <c>Any</c> con comparación de
/// fechas y vigencia abierta, que InMemory no traduce a SQL.
/// </para>
/// </remarks>
public sealed class FiltroPorAparatoEnReportesTests : IAsyncLifetime, IDisposable
{
    private const long PlantaP01 = 3;
    private const long AreaProduccion = 3;
    private const long SeccionGeneral = 3;
    private const long PuestoOperario = 1;
    private const long EmpresaOutsourcing = 2;
    private const long TipoDocumentoDni = 1;

    /// <summary>Periodo sembrado «2026-07»: del 1 al 31 de julio de 2026.</summary>
    private const long PeriodoJulio2026 = 1;

    private static readonly DateOnly InicioJulio = new(2026, 7, 1);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioReportes servicio;

    private long aparatoAL;
    private long aparatoMZ;

    public FiltroPorAparatoEnReportesTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        servicio = new ServicioReportes(contexto, new AlcanceSinRestriccion());
    }

    public async Task InitializeAsync()
    {
        aparatoAL = await CrearEquipoAsync("P01-AL", "Planta 01 A-L");
        aparatoMZ = await CrearEquipoAsync("P01-MZ", "Planta 01 M-Z");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task El_filtro_trae_solo_a_quienes_tuvieron_ese_aparato_en_el_rango()
    {
        await CrearTrabajadorAsync("41300001", "FLORES", aparatoAL, "14");
        await CrearTrabajadorAsync("41300002", "QUISPE", aparatoMZ, "14");

        var resumen = await servicio.ResumenTareoAsync(FiltroDe(aparatoAL), CancellationToken.None);

        // El «14» del M-Z es otra persona y no entra, que es lo que el filtro viene a hacer.
        resumen.Select(f => f.Trabajador).ShouldAllBe(n => n.StartsWith("FLORES", StringComparison.Ordinal));
        resumen.Count.ShouldBe(1);

        // Sin filtro salen los dos, y el «14» a secas no los distingue: por eso la celda
        // lleva también el aparato.
        var todos = await servicio.ResumenTareoAsync(FiltroDe(null), CancellationToken.None);
        todos.Count.ShouldBe(2);
        todos.ShouldAllBe(f => f.IdMarcador == "14");
        todos.Select(f => f.Equipo).ShouldBe(["Planta 01 A-L", "Planta 01 M-Z"], ignoreOrder: true);
    }

    /// <summary>
    /// El hallazgo: la columna mostraba el número de <b>hoy</b>, no el del alcance.
    /// </summary>
    /// <remarks>
    /// TORRES tuvo el A-L todo julio y pasó al M-Z en agosto. Un resumen de julio emitido en
    /// setiembre lo mostraba con su número del M-Z: el mismo trabajador salía con un aparato
    /// en el reporte y con otro en la pantalla de Tareo, que sí atribuye por fecha.
    /// </remarks>
    [Fact]
    public async Task La_columna_del_marcador_muestra_el_del_rango_y_no_el_de_hoy()
    {
        var torres = await CrearTrabajadorAsync("41300003", "TORRES", aparatoAL, "27", hasta: new DateOnly(2026, 7, 31));
        await AsignarAsync(torres, aparatoMZ, "31", desde: new DateOnly(2026, 8, 1));

        var resumen = await servicio.ResumenTareoAsync(FiltroDe(null), CancellationToken.None);

        var fila = resumen.Single();
        fila.IdMarcador.ShouldBe("27");
        fila.Equipo.ShouldBe("Planta 01 A-L");

        // Y el filtro de julio por el A-L lo sigue trayendo, aunque hoy tenga el M-Z.
        (await servicio.ResumenTareoAsync(FiltroDe(aparatoAL), CancellationToken.None))
            .Count.ShouldBe(1);
        (await servicio.ResumenTareoAsync(FiltroDe(aparatoMZ), CancellationToken.None))
            .ShouldBeEmpty();
    }

    /// <summary>
    /// Quien cambia de aparato a mitad de mes sale entero en los dos reportes.
    /// </summary>
    /// <remarks>
    /// Partir su mes por aparato daría dos medias jornadas que no le corresponden a nadie: el
    /// tareo es por persona. La celda avisa del cambio con los dos números.
    /// </remarks>
    [Fact]
    public async Task Quien_cambio_de_aparato_dentro_del_rango_sale_en_los_dos_con_ambos_numeros()
    {
        var ramos = await CrearTrabajadorAsync("41300004", "RAMOS", aparatoAL, "27", hasta: new DateOnly(2026, 7, 15));
        await AsignarAsync(ramos, aparatoMZ, "31", desde: new DateOnly(2026, 7, 16));

        foreach (var equipo in new[] { aparatoAL, aparatoMZ })
        {
            var resumen = await servicio.ResumenTareoAsync(FiltroDe(equipo), CancellationToken.None);

            var fila = resumen.ShouldHaveSingleItem();
            fila.IdMarcador.ShouldBe("27 → 31");
            fila.Equipo.ShouldBe("Planta 01 A-L → Planta 01 M-Z");
        }
    }

    /// <summary>
    /// Excepciones filtra igual que el resumen: son la misma pantalla de trabajo.
    /// </summary>
    [Fact]
    public async Task Las_excepciones_tambien_se_filtran_por_aparato()
    {
        var flores = await CrearTrabajadorAsync("41300005", "FLORES", aparatoAL, "14");
        var quispe = await CrearTrabajadorAsync("41300006", "QUISPE", aparatoMZ, "14");

        await CrearFaltaAsync(flores, new DateOnly(2026, 7, 6));
        await CrearFaltaAsync(quispe, new DateOnly(2026, 7, 6));

        var delAL = await servicio.ExcepcionesAsync(FiltroDe(aparatoAL), CancellationToken.None);

        var fila = delAL.ShouldHaveSingleItem();
        fila.TrabajadorId.ShouldBe(flores);
        fila.Equipo.ShouldBe("Planta 01 A-L");

        (await servicio.ExcepcionesAsync(FiltroDe(null), CancellationToken.None)).Count.ShouldBe(2);
    }

    /// <summary>
    /// El aparato filtrado se nombra en la trazabilidad, que viaja dentro del Excel.
    /// </summary>
    /// <remarks>
    /// El archivo se reenvía por correo, donde ya no hay pantalla: sin esto, el resumen de un
    /// solo aparato se ve idéntico al de la planta entera.
    /// </remarks>
    [Fact]
    public async Task La_trazabilidad_nombra_el_aparato_filtrado()
    {
        var metadatos = await servicio.ObtenerMetadatosAsync(
            FiltroDe(aparatoMZ), "coordinador", CancellationToken.None);

        metadatos.FiltrosDescripcion.ShouldContain("Aparato: Planta 01 M-Z");

        (await servicio.ObtenerMetadatosAsync(FiltroDe(null), "coordinador", CancellationToken.None))
            .FiltrosDescripcion.ShouldBe("Todos los trabajadores");
    }

    // ------------------------------------------------------------------

    private static FiltroReporte FiltroDe(long? equipoId) =>
        new() { PeriodoId = PeriodoJulio2026, EquipoId = equipoId };

    private async Task<long> CrearEquipoAsync(string codigo, string nombre)
    {
        var equipo = new EquipoMarcador { Codigo = codigo, Nombre = nombre, PlantaId = PlantaP01, Activo = true };
        contexto.EquiposMarcador.Add(equipo);
        await contexto.SaveChangesAsync(CancellationToken.None);
        return equipo.Id;
    }

    private async Task<long> CrearTrabajadorAsync(
        string dni,
        string apellido,
        long equipoId,
        string idMarcador,
        DateOnly? hasta = null)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = dni,
            ApellidoPaterno = apellido,
            ApellidoMaterno = "TEST",
            Nombres = "APARATO",
            Sexo = Sexo.Masculino,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            AreaId = AreaProduccion,
            SeccionId = SeccionGeneral,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        trabajador.AbrirVinculo("prueba");
        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync(CancellationToken.None);

        // Empieza antes del periodo: el aparato no cambia porque empiece el mes.
        await AsignarAsync(trabajador.Id, equipoId, idMarcador, InicioJulio.AddMonths(-1), hasta);

        return trabajador.Id;
    }

    private async Task AsignarAsync(
        long trabajadorId,
        long equipoId,
        string idMarcador,
        DateOnly desde,
        DateOnly? hasta = null)
    {
        contexto.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            TrabajadorId = trabajadorId,
            EquipoId = equipoId,
            IdMarcador = idMarcador,
            FechaInicio = desde,
            FechaFin = hasta
        });

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private async Task CrearFaltaAsync(long trabajadorId, DateOnly fecha)
    {
        contexto.TareosDiarios.Add(new TareoDiario
        {
            TrabajadorId = trabajadorId,
            PeriodoId = PeriodoJulio2026,
            Fecha = fecha,
            Estado = EstadoTareo.Falta,
            Observacion = "Sin marcaciones.",
            CalculadoEn = new DateTime(2026, 8, 1, 9, 0, 0)
        });

        await contexto.SaveChangesAsync(CancellationToken.None);
    }
}
