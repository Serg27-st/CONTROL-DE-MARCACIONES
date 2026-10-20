using ControlMarcacion.Application.Justificaciones.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Justificaciones;

/// <summary>
/// Un trabajador no puede tener dos justificaciones el mismo día, y ahora lo garantiza la
/// base y no solo el servicio.
/// </summary>
/// <remarks>
/// La comprobación vivía únicamente en C#, con un consultar-e-insertar sin transacción: dos
/// usuarios simultáneos —o una aprobación de solicitud corriendo junto a un alta manual—
/// la atravesaban los dos e insertaban dos filas. El recálculo del periodo entero moría
/// después al indexar las justificaciones por fecha, con un error que no decía nada.
///
/// Va sobre <b>SQLite real</b> porque lo que se prueba es un índice, y el proveedor
/// InMemory los ignora: esta prueba pasaría siempre ahí sin que el índice existiera.
/// </remarks>
public sealed class UnaJustificacionPorTrabajadorYDiaTests : IDisposable
{
    private const long PlantaP01 = 3;
    private const long EmpresaOutsourcing = 2;
    private const long PuestoOperario = 1;
    private const long TipoDocumentoDni = 1;
    private const long PeriodoJulio2026 = 1;
    private const long TipoVacaciones = 1;

    /// <summary>Miércoles 1 de julio de 2026, dentro del periodo sembrado y abierto.</summary>
    private static readonly DateOnly Miercoles = new(2026, 7, 1);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioJustificaciones servicio;

    public UnaJustificacionPorTrabajadorYDiaTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        servicio = new ServicioJustificaciones(contexto, new AlcanceSinRestriccion());
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task La_primera_justificacion_manual_del_dia_se_registra()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444411");

        var id = await servicio.CrearManualAsync(Peticion(trabajadorId), "admin", CancellationToken.None);

        id.ShouldBeGreaterThan(0);
        (await contexto.Justificaciones.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task La_segunda_justificacion_del_mismo_dia_se_rechaza_con_un_mensaje_legible()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444412");

        await servicio.CrearManualAsync(Peticion(trabajadorId), "admin", CancellationToken.None);

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            servicio.CrearManualAsync(Peticion(trabajadorId), "admin", CancellationToken.None));

        error.Message.ShouldContain("Ya existe una justificación");
        (await contexto.Justificaciones.CountAsync()).ShouldBe(1);
    }

    /// <summary>
    /// La garantía de verdad: aunque alguien esquive el servicio, la base rechaza la
    /// segunda fila. Es lo que cierra la ventana entre la comprobación y el INSERT.
    /// </summary>
    [Fact]
    public async Task La_base_rechaza_un_duplicado_insertado_por_fuera_del_servicio()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444413");

        contexto.Justificaciones.Add(Fila(trabajadorId));
        await contexto.SaveChangesAsync(CancellationToken.None);

        contexto.Justificaciones.Add(Fila(trabajadorId));

        await Should.ThrowAsync<DbUpdateException>(
            contexto.SaveChangesAsync(CancellationToken.None));
    }

    /// <summary>
    /// Dos trabajadores distintos sí pueden tener justificación el mismo día: el índice es
    /// por el par, no por la fecha.
    /// </summary>
    [Fact]
    public async Task Dos_trabajadores_pueden_justificar_el_mismo_dia()
    {
        var uno = await CrearTrabajadorAsync("44444414");
        var otro = await CrearTrabajadorAsync("44444415");

        await servicio.CrearManualAsync(Peticion(uno), "admin", CancellationToken.None);
        await servicio.CrearManualAsync(Peticion(otro), "admin", CancellationToken.None);

        (await contexto.Justificaciones.CountAsync()).ShouldBe(2);
    }

    /// <summary>
    /// El permiso por horas necesita su ventana para que el motor sepa de qué extremo de la
    /// jornada descontar, y esa ventana solo la captura la solicitud de ausencia. Admitirlo
    /// a mano crearía justificaciones que el motor solo puede aplicar suponiendo.
    /// </summary>
    [Fact]
    public async Task Un_permiso_parcial_no_se_da_de_alta_a_mano()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444416");

        var peticion = Peticion(trabajadorId) with
        {
            Tipo = TipoJustificacion.PermisoParcial,
            TipoAusenciaId = null
        };

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            servicio.CrearManualAsync(peticion, "admin", CancellationToken.None));

        error.Message.ShouldContain("solicitud de ausencia");
        (await contexto.Justificaciones.CountAsync()).ShouldBe(0);
    }

    // ------------------------------------------------------------------

    private static CrearJustificacionManualRequest Peticion(long trabajadorId) =>
        new()
        {
            TrabajadorId = trabajadorId,
            Fecha = Miercoles,
            Tipo = TipoJustificacion.Ausencia,
            TipoAusenciaId = TipoVacaciones,
            Motivo = "Vacaciones acordadas con jefatura"
        };

    private static Justificacion Fila(long trabajadorId) =>
        new()
        {
            TrabajadorId = trabajadorId,
            PeriodoId = PeriodoJulio2026,
            Fecha = Miercoles,
            Origen = OrigenJustificacion.Manual,
            TipoAusenciaId = TipoVacaciones,
            Tipo = TipoJustificacion.Ausencia,
            Motivo = "Alta por fuera del servicio",
            RegistradoPorUsuario = "test",
            RegistradoEn = new DateTime(2026, 8, 1, 10, 0, 0)
        };

    private async Task<long> CrearTrabajadorAsync(string documento)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "UNICIDAD",
            Nombres = "JUSTIFICACION",
            Sexo = Sexo.Femenino,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }
}
