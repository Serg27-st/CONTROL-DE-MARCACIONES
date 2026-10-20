using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Infrastructure.Marcaciones;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Auditoria;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

/// <summary>
/// IMP-AT-01..07: la unidad persistente de una importación principal se confirma completa
/// o se revierte completa ante un fallo técnico. Los errores funcionales de línea siguen
/// siendo parte válida de una importación exitosa.
/// </summary>
public sealed class AtomicidadImportacionAcceptanceTests : IDisposable
{
    private const long Planta = 1;
    private const long Equipo = 1;

    private readonly SqliteConnection conexion;
    private readonly FalloAlGuardarMarcacionesInterceptor fallo = new();
    private readonly FuenteControlada fuente = new();
    private readonly MarcacionDbContext contexto;
    private readonly ServicioImportacion servicio;

    public AtomicidadImportacionAcceptanceTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(Opciones(conAuditoria: true, conFallo: true));
        contexto.Database.EnsureCreated();

        contexto.EquiposMarcador.Add(new EquipoMarcador
        {
            Id = Equipo,
            Codigo = "DET-AT",
            Nombre = "Equipo atomicidad",
            Serie = "SERIE-AT",
            PlantaId = Planta,
            Activo = true
        });
        contexto.SaveChanges();

        var opcionesMarcador = Options.Create(new OpcionesMarcador
        {
            DiasAntiguedadMaxima = 100_000
        });

        servicio = new ServicioImportacion(
            contexto,
            fuente,
            opcionesMarcador,
            new ServicioEquipos(contexto));
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    /// <summary>
    /// IMP-AT-01: reproduce el corte real entre el guardado de cabecera/errores y el de
    /// marcaciones. Un interceptor falla el segundo SaveChanges, no una rama de test en
    /// producción.
    /// </summary>
    [Fact]
    public async Task IMP_AT_01_un_fallo_entre_etapas_revierte_cabecera_errores_marcas_y_auditoria()
    {
        var antes = await ConteosAsync();
        fallo.Activo = true;

        await Should.ThrowAsync<InvalidOperationException>(() => ImportarAsync("fallo-etapas"));

        (await ConteosAsync()).ShouldBe(antes);
    }

    [Fact]
    public async Task IMP_AT_02_una_importacion_exitosa_confirma_toda_la_unidad()
    {
        var resultado = await ImportarAsync("exitosa");
        await using var verificacion = Verificacion();

        (await verificacion.Importaciones.CountAsync()).ShouldBe(1);
        (await verificacion.MarcacionesCrudas.CountAsync()).ShouldBe(2);
        (await verificacion.ErroresImportacion.CountAsync()).ShouldBe(2);
        resultado.FilasInsertadas.ShouldBe(2);
    }

    [Fact]
    public async Task IMP_AT_03_una_linea_invalida_convive_con_las_marcas_validas()
    {
        var resultado = await ImportarAsync("con-error-funcional");
        await using var verificacion = Verificacion();

        resultado.FilasLeidas.ShouldBe(3);
        resultado.FilasRechazadas.ShouldBe(1);
        resultado.FilasInsertadas.ShouldBe(2);
        (await verificacion.MarcacionesCrudas.CountAsync()).ShouldBe(2);
        (await verificacion.ErroresImportacion.CountAsync(e => e.Motivo == "línea inválida"))
            .ShouldBe(1);
    }

    /// <summary>
    /// El servicio vigente permite repetir el mismo archivo en el mismo equipo y conserva
    /// una cabecera de trazabilidad, pero el hash lo detecta y no duplica marcas.
    /// </summary>
    [Fact]
    public async Task IMP_AT_04_el_mismo_archivo_se_detecta_y_no_duplica_marcaciones()
    {
        await ImportarAsync("duplicado");

        await using (var contenido = Contenido("duplicado"))
        {
            var previa = await servicio.BuscarImportacionPreviaAsync(contenido);
            previa.ShouldNotBeNull();
            previa.EquipoId.ShouldBe(Equipo);
        }

        var repetida = await ImportarAsync("duplicado");
        await using var verificacion = Verificacion();

        repetida.FilasInsertadas.ShouldBe(0);
        repetida.FilasDuplicadas.ShouldBe(2);
        (await verificacion.MarcacionesCrudas.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task IMP_AT_05_una_marcacion_ya_existente_se_contabiliza_como_duplicada()
    {
        await SembrarMarcaPreexistenteAsync();

        var resultado = await ImportarAsync("marca-existente");
        await using var verificacion = Verificacion();

        resultado.FilasInsertadas.ShouldBe(1);
        resultado.FilasDuplicadas.ShouldBe(1);
        (await verificacion.MarcacionesCrudas.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task IMP_AT_06_una_excepcion_al_insertar_marcaciones_no_deja_estado_parcial()
    {
        var equipo = await contexto.EquiposMarcador.SingleAsync(e => e.Id == Equipo);
        equipo.Serie = null;
        await contexto.SaveChangesAsync();
        fuente.SerieEquipo = "SERIE-APRENDIDA";

        var antes = await ConteosAsync();
        fallo.Activo = true;

        var error = await Should.ThrowAsync<InvalidOperationException>(
            () => ImportarAsync("fallo-insercion"));

        error.Message.ShouldContain("MarcacionCruda");
        (await ConteosAsync()).ShouldBe(antes);

        await using var verificacion = Verificacion();
        (await verificacion.EquiposMarcador.AsNoTracking().SingleAsync(e => e.Id == Equipo))
            .Serie.ShouldBeNull();
    }

    [Fact]
    public async Task IMP_AT_07_los_contadores_coinciden_con_las_filas_confirmadas()
    {
        var resultado = await ImportarAsync("contadores");
        await using var verificacion = Verificacion();
        var cabecera = await verificacion.Importaciones.AsNoTracking().SingleAsync();

        cabecera.FilasLeidas.ShouldBe(3);
        cabecera.FilasInsertadas.ShouldBe(2);
        cabecera.FilasDuplicadas.ShouldBe(0);
        cabecera.FilasRechazadas.ShouldBe(1);
        (await verificacion.MarcacionesCrudas.CountAsync(m => m.ImportacionId == cabecera.Id))
            .ShouldBe(cabecera.FilasInsertadas);
        resultado.FilasInsertadas.ShouldBe(cabecera.FilasInsertadas);
    }

    private DbContextOptions<MarcacionDbContext> Opciones(bool conAuditoria, bool conFallo)
    {
        var constructor = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexion);

        if (conAuditoria)
        {
            constructor.AddInterceptors(new InterceptorAuditoria(new UsuarioDePrueba()));
        }

        if (conFallo)
        {
            constructor.AddInterceptors(fallo);
        }

        return constructor.Options;
    }

    private MarcacionDbContext Verificacion() => new(Opciones(conAuditoria: false, conFallo: false));

    private async Task<ResultadoImportacion> ImportarAsync(string contenido)
    {
        await using var archivo = Contenido(contenido);
        return await servicio.ImportarAsync(
            archivo, $"{contenido}.txt", Planta, Equipo, "coordinador", CancellationToken.None);
    }

    private static MemoryStream Contenido(string valor) =>
        new(System.Text.Encoding.UTF8.GetBytes(valor));

    private async Task SembrarMarcaPreexistenteAsync()
    {
        var importacion = new ImportacionMarcador
        {
            NombreArchivo = "anterior.txt",
            EquipoId = Equipo,
            HashContenido = new string('a', 64),
            FechaCarga = new DateTime(2026, 6, 30, 8, 0, 0),
            CargadoPorUsuario = "test",
            FilasLeidas = 1,
            FilasInsertadas = 1
        };

        contexto.Importaciones.Add(importacion);
        await contexto.SaveChangesAsync();
        contexto.MarcacionesCrudas.Add(new MarcacionCruda
        {
            EquipoId = Equipo,
            IdMarcador = "14",
            FechaHora = FuenteControlada.Primera,
            ImportacionId = importacion.Id,
            LineaOriginal = "14 primera",
            CreadoPor = "test"
        });
        await contexto.SaveChangesAsync();
    }

    private async Task<Conteos> ConteosAsync()
    {
        await using var verificacion = Verificacion();
        return new Conteos(
            await verificacion.Importaciones.CountAsync(),
            await verificacion.ErroresImportacion.CountAsync(),
            await verificacion.MarcacionesCrudas.CountAsync(),
            await verificacion.RegistrosAuditoria.CountAsync());
    }

    private sealed record Conteos(int Importaciones, int Errores, int Marcaciones, int Auditorias);

    private sealed class FuenteControlada : IFuenteDeMarcaciones
    {
        public static readonly DateTime Primera = new(2026, 7, 1, 7, 0, 0);

        public string? SerieEquipo { get; set; }

        public ResultadoLectura Leer(Stream contenido) => new(
            [
                new MarcacionLeida("14", Primera, "huella", "entrada", 1, "14 primera"),
                new MarcacionLeida("14", new DateTime(2026, 7, 1, 17, 22, 0), "huella", "salida", 2, "14 segunda")
            ],
            [new LineaRechazada(3, "ilegible", "línea inválida")],
            SerieEquipo);
    }

    private sealed class FalloAlGuardarMarcacionesInterceptor : SaveChangesInterceptor
    {
        public bool Activo { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Activo
                && eventData.Context is { } contextoIntervenido
                && contextoIntervenido.ChangeTracker.Entries<MarcacionCruda>()
                    .Any(e => e.State == EntityState.Added))
            {
                throw new InvalidOperationException(
                    "Fallo técnico controlado al insertar MarcacionCruda.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class UsuarioDePrueba : IProveedorUsuarioActual
    {
        public string Usuario => "test-atomicidad";
    }
}
