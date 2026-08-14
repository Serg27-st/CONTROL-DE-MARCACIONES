using ClosedXML.Excel;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using ControlMarcacion.Infrastructure.Reportes;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Reportes;

/// <summary>
/// Verificación de extremo a extremo pedida al cerrar la segunda mitad de la Fase 5:
/// genera un Tareo Final real (con el flujo normal: <see cref="ServicioReportes"/> +
/// <see cref="ExportadorReportesExcel"/>) para un trabajador con hijos y sueldo bajo el
/// piso RMV×1,35 en un día con marcaciones nocturnas, y abre el <c>.xlsx</c> resultante
/// para confirmar a mano que las columnas de esta fase ya no están vacías.
/// </summary>
/// <remarks>
/// Escribe el archivo en disco (no solo en memoria) porque parte de lo pedido es
/// literalmente "abrir el archivo generado": queda en el scratchpad de la sesión, no en el
/// repositorio, y sirve para inspeccionarlo con Excel si hace falta además de la
/// verificación programática de celdas que hace este mismo test.
/// </remarks>
public sealed class GeneracionTareoFinalEndToEndTests : IDisposable
{
    private readonly MarcacionDbContext contexto;
    private readonly ServicioReportes servicioReportes;
    private readonly ExportadorReportesExcel exportador = new();
    private long trabajadorId;

    public GeneracionTareoFinalEndToEndTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();

        servicioReportes = new ServicioReportes(contexto);
    }

    public void Dispose() => contexto.Dispose();

    [Fact]
    public async Task El_tareo_final_y_el_resumen_generados_no_estan_corruptos_y_traen_los_montos_nuevos()
    {
        await PrepararEscenarioAsync();

        var filtro = new FiltroReporte { PeriodoId = 1 };
        var metadatos = await servicioReportes.ObtenerMetadatosAsync(filtro, "coordinador@gca.local", CancellationToken.None);

        // --- Tareo Final ---
        var tareoFinal = await servicioReportes.TareoFinalAsync(filtro, CancellationToken.None);
        var bytesTareoFinal = exportador.ExportarTareoFinal(metadatos, tareoFinal);

        var rutaTareoFinal = Path.Combine(RutaScratchpad(), "verificacion_tareo_final_fase5.xlsx");
        await File.WriteAllBytesAsync(rutaTareoFinal, bytesTareoFinal);

        // "Abrir" el archivo: releerlo desde disco con ClosedXML y no como los bytes que
        // ya teníamos en memoria, para confirmar que lo que quedó grabado es un .xlsx válido.
        using (var libro = new XLWorkbook(rutaTareoFinal))
        {
            var hoja = libro.Worksheet("Tareo");

            // Un solo trabajador en el escenario: su fila es la primera de datos (7).
            const int fila = 7;

            hoja.Cell("A1").GetString().ShouldNotBeNullOrEmpty();
            hoja.Cell(fila, 2).GetString().ShouldBe("12345678");

            // Punto 3: el trabajador del escenario no tiene centro de costo asignado, así
            // que las tres columnas de CECO salen vacías — ya no se aproximan con
            // Área/Sección, porque eso daría un código que no existe en SAP.
            hoja.Cell(fila, 5).GetString().ShouldBe(string.Empty);
            hoja.Cell(fila, 6).GetString().ShouldBe(string.Empty);
            hoja.Cell(fila, 7).GetString().ShouldBe(string.Empty);

            // AREA sí sale: es un dato propio del trabajador, ajeno al catálogo de SAP.
            hoja.Cell(fila, 8).GetString().ShouldBe("Producción");

            // Punto 4: movilidad (AH, col 34) y asignación familiar (AM, col 39) con número.
            hoja.Cell(fila, 34).GetDouble().ShouldBe(3.00); // 1 día asistido × 3.00
            hoja.Cell(fila, 34).DataType.ShouldBe(XLDataType.Number);
            hoja.Cell(fila, 39).GetDouble().ShouldBe(113.00); // 10 % de 1130, tiene hijos
            hoja.Cell(fila, 39).DataType.ShouldBe(XLDataType.Number);
        }

        // --- Resumen mensual: ahí vive el bono nocturno, no en la hoja Tareo ---
        var resumen = await servicioReportes.ResumenTareoAsync(filtro, CancellationToken.None);
        var bytesResumen = exportador.ExportarResumenTareo(metadatos, resumen);

        var rutaResumen = Path.Combine(RutaScratchpad(), "verificacion_resumen_fase5.xlsx");
        await File.WriteAllBytesAsync(rutaResumen, bytesResumen);

        using (var libro = new XLWorkbook(rutaResumen))
        {
            var hoja = libro.Worksheets.First();

            // Fila 9 es la primera de datos: el encabezado de trazabilidad ocupa 1-7 (la 6
            // es el estado del cálculo, que se agregó al detectar el tareo desactualizado)
            // y los títulos de columna van en la 8.
            var bonoNocturnoEsperado = CalculadoraBonoNocturno.BonoDiario(1130m, 35m, 1200m, 31);
            bonoNocturnoEsperado.ShouldBeGreaterThan(0m); // el sueldo (1200) está bajo el piso (1525.50)

            hoja.Cell(9, 19).GetDouble().ShouldBe((double)bonoNocturnoEsperado);
        }
    }

    // ------------------------------------------------------------------

    private async Task PrepararEscenarioAsync()
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = 1,
            NumeroDocumento = "12345678",
            ApellidoPaterno = "VERIFICACION",
            ApellidoMaterno = "FASE5",
            Nombres = "TRABAJADOR PRUEBA",
            Sexo = Sexo.Masculino,
            TieneHijos = true, // dispara la asignación familiar
            EmpresaId = 2,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = 3, // Planta 01
            AreaId = 3, // PROD
            SeccionId = 3, // GEN
            PuestoId = 1,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        await contexto.Trabajadores.AddAsync(trabajador);
        await contexto.SaveChangesAsync();
        trabajadorId = trabajador.Id;

        // Sueldo básico bajo el piso legal: RMV(1130) × 1,35 = 1525.50. Con 1200, el bono
        // nocturno debe salir mayor que cero.
        await contexto.AsignacionesSueldo.AddAsync(new AsignacionSueldo
        {
            TrabajadorId = trabajadorId,
            SueldoBasicoMensual = 1200m,
            FechaInicio = new DateOnly(2026, 1, 1),
            FechaFin = null
        });

        // Un día con marcación real completa (cuenta para movilidad) y minutos en franja
        // nocturna (dispara el bono nocturno del Resumen).
        await contexto.TareosDiarios.AddAsync(new TareoDiario
        {
            PeriodoId = 1,
            TrabajadorId = trabajadorId,
            Fecha = new DateOnly(2026, 7, 15),
            EntradaReal = new DateTime(2026, 7, 15, 19, 0, 0),
            SalidaReal = new DateTime(2026, 7, 16, 7, 0, 0),
            MinutosTrabajados = 660,
            MinutosOrdinarios = 480,
            MinutosExtra25 = 120,
            MinutosExtra35 = 60,
            MinutosNocturnos = 480,
            Estado = EstadoTareo.Puntual,
            TipoDiaNoLaborable = TipoDiaNoLaborable.Ninguno,
            CalculadoEn = DateTime.Now
        });

        await contexto.SaveChangesAsync();
    }

    /// <summary>
    /// Carpeta temporal del sistema, no el repositorio: es un archivo de verificación de un
    /// test, no un insumo del sistema. Se puede abrir a mano después de correr el test.
    /// </summary>
    private static string RutaScratchpad()
    {
        var ruta = Path.Combine(Path.GetTempPath(), "gca-verificacion-fase5");

        Directory.CreateDirectory(ruta);

        return ruta;
    }
}
