using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Reportes;

/// <summary>
/// El área del trabajador tiene vigencia por fecha: los reportes muestran la de entonces.
/// </summary>
/// <remarks>
/// <para>
/// El defecto: el área salía de <c>Trabajador.AreaId</c>, que es la de <b>hoy</b>. Mover a
/// alguien de área reescribía el pasado — un resumen de julio emitido en setiembre lo mostraba
/// ya en su área nueva, y la columna <c>AREA</c> del tareo final, que va a planilla, cambiaba
/// sola en un mes ya entregado. Es el mismo error que el invariante 5 previene para el ID de
/// marcador.
/// </para>
/// <para>
/// Sobre SQLite real, como las demás pruebas de reportes: parte de la garantía es el índice
/// único filtrado de una sola vigencia abierta por trabajador, que InMemory ignora. Áreas
/// sembradas: 3 y 4. Periodo 1 = julio de 2026.
/// </para>
/// </remarks>
public sealed class VigenciaDelAreaEnReportesTests : IDisposable
{
    private const long PlantaP01 = 3;
    private const long PuestoOperario = 1;
    private const long EmpresaOutsourcing = 2;
    private const long TipoDocumentoDni = 1;
    private const long PeriodoJulio2026 = 1;

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioReportes servicio;

    private readonly long areaOrigen;
    private readonly long areaDestino;
    private readonly string nombreOrigen;
    private readonly string nombreDestino;

    public VigenciaDelAreaEnReportesTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        servicio = new ServicioReportes(contexto, new AlcanceSinRestriccion());

        // Dos áreas con NOMBRE distinto. No vale tomar las dos primeras por Id: el mismo
        // nombre de área existe en varias plantas —por eso el formulario solo las ofrece con
        // planta elegida—, y con dos «Producción» la prueba pasaría sin comprobar nada.
        var areas = contexto.Areas.AsNoTracking()
            .OrderBy(a => a.Id)
            .AsEnumerable()
            .DistinctBy(a => a.Nombre)
            .Take(2)
            .ToList();

        areaOrigen = areas[0].Id;
        areaDestino = areas[1].Id;
        nombreOrigen = areas[0].Nombre;
        nombreDestino = areas[1].Nombre;
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    /// <summary>
    /// El hallazgo: la columna mostraba el área de hoy, no la del alcance.
    /// </summary>
    /// <remarks>
    /// RAMOS estuvo en la primera área todo julio y pasó a la segunda en agosto. El resumen
    /// de julio tiene que seguir mostrándolo donde estaba en julio.
    /// </remarks>
    [Fact]
    public async Task El_resumen_de_julio_muestra_el_area_de_julio_y_no_la_de_hoy()
    {
        var id = await CrearTrabajadorAsync("41300010", "RAMOS", areaDestino);

        // En julio estaba en la otra área; desde agosto, en la que la ficha tiene hoy.
        await AsignarAreaAsync(id, areaOrigen, new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 31));
        await AsignarAreaAsync(id, areaDestino, new DateOnly(2026, 8, 1), null);

        await CrearFaltaAsync(id, new DateOnly(2026, 7, 13));

        var fila = await AreaDelTareoFinalAsync();

        fila.ShouldBe(nombreOrigen);
        fila.ShouldNotBe(nombreDestino);
    }

    /// <summary>
    /// Quien cambió de área dentro del rango sale con las dos en la misma celda.
    /// </summary>
    /// <remarks>
    /// Igual que el marcador («14 → 27»), y por lo mismo: la fila TOTAL del Excel va en
    /// columnas fijas, así que una columna más correría los datos y dejaría cada total bajo
    /// el título equivocado.
    /// </remarks>
    [Fact]
    public async Task Un_cambio_de_area_dentro_del_rango_muestra_las_dos()
    {
        var id = await CrearTrabajadorAsync("41300011", "SOTO", areaDestino);

        await AsignarAreaAsync(id, areaOrigen, new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 15));
        await AsignarAreaAsync(id, areaDestino, new DateOnly(2026, 7, 16), null);

        await CrearFaltaAsync(id, new DateOnly(2026, 7, 13));

        var fila = await AreaDelTareoFinalAsync();

        fila.ShouldBe($"{nombreOrigen} → {nombreDestino}");
    }

    /// <summary>
    /// Sin vigencias registradas se conserva el área de la ficha.
    /// </summary>
    /// <remarks>
    /// Es el caso de las fichas anteriores a esta vigencia cuyo relleno no alcanzó. El valor
    /// de hoy es mejor respaldo que un «-», que rompería el cruce con SAP — y es exactamente
    /// como se venía mostrando.
    /// </remarks>
    [Fact]
    public async Task Sin_vigencia_registrada_se_conserva_el_area_de_la_ficha()
    {
        var id = await CrearTrabajadorAsync("41300012", "VEGA", areaOrigen);
        await CrearFaltaAsync(id, new DateOnly(2026, 7, 13));

        var fila = await AreaDelTareoFinalAsync();

        fila.ShouldBe(nombreOrigen);
    }

    // ------------------------------------------------------------------

    private static FiltroReporte Filtro() => new() { PeriodoId = PeriodoJulio2026 };

    /// <summary>
    /// El área tal como sale en la columna <c>AREA</c> del tareo final, que es el archivo que
    /// va a planilla y el único sitio donde el área llega al usuario.
    /// </summary>
    private async Task<string?> AreaDelTareoFinalAsync() =>
        (await servicio.TareoFinalAsync(Filtro(), CancellationToken.None)).Tareo.Single().Area;

    private async Task<long> CrearTrabajadorAsync(string dni, string apellido, long areaId)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = dni,
            ApellidoPaterno = apellido,
            ApellidoMaterno = "TEST",
            Nombres = "AREA",
            Sexo = Sexo.Masculino,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            AreaId = areaId,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        trabajador.AbrirVinculo("prueba");
        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }

    private async Task AsignarAreaAsync(long trabajadorId, long areaId, DateOnly desde, DateOnly? hasta)
    {
        contexto.AsignacionesArea.Add(new AsignacionArea
        {
            TrabajadorId = trabajadorId,
            AreaId = areaId,
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
            Observacion = "Sin marcaciones."
        });

        await contexto.SaveChangesAsync(CancellationToken.None);
    }
}
