using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Domain.Reportes;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Auditoria;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Reportes;

/// <summary>
/// Contratos R-P1-04: «último cálculo» no significa «vigente». El estado solo está al día
/// cuando ninguna entrada que el motor consume cambió después del cálculo más antiguo del
/// alcance consultado.
/// </summary>
public sealed class VigenciaTareoAcceptanceTests : IDisposable
{
    private const long PlantaA = 3;
    private const long PlantaB = 4;
    private const long EmpresaOutsourcing = 2;
    private const long TipoDocumentoDni = 1;
    private const long AreaProduccion = 3;
    private const long SeccionGeneral = 3;
    private const long PuestoOperario = 1;
    private const long PeriodoJulio2026 = 1;
    private const long TurnoDiurno = 1;

    private static readonly DateOnly Dia = new(2026, 7, 10);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly long equipoId;

    public VigenciaTareoAcceptanceTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>()
                .UseSqlite(conexion)
                .AddInterceptors(new InterceptorAuditoria(new UsuarioAuditoriaVigencia()))
                .Options);
        contexto.Database.EnsureCreated();

        var equipo = new EquipoMarcador
        {
            Codigo = "VIG-A",
            Nombre = "Equipo de contratos de vigencia",
            PlantaId = PlantaA
        };
        contexto.EquiposMarcador.Add(equipo);
        contexto.SaveChanges();
        equipoId = equipo.Id;
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task VIG_01_una_marcacion_nueva_desactualiza_el_tareo()
    {
        var trabajadorId = await CrearTrabajadorAsync("71000001", PlantaA);
        await contexto.AsignacionesMarcador.AddAsync(new AsignacionMarcador
        {
            TrabajadorId = trabajadorId,
            EquipoId = equipoId,
            IdMarcador = "VIG-01",
            FechaInicio = new DateOnly(2020, 1, 1)
        });
        await contexto.SaveChangesAsync(CancellationToken.None);
        await AgregarTareoAsync(trabajadorId);
        await EsperarMarcaPosteriorAsync();

        await AgregarMarcacionAsync();

        (await MetadatosAsync(new AlcanceSinRestriccion()))
            .Calculo.Desactualizado.ShouldBeTrue();
    }

    [Fact]
    public async Task VIG_02_una_justificacion_aprobada_nueva_desactualiza_el_tareo()
    {
        var trabajadorId = await CrearTrabajadorAsync("71000002", PlantaA);
        await AgregarTareoAsync(trabajadorId);
        await EsperarMarcaPosteriorAsync();

        await AgregarJustificacionAsync(trabajadorId);

        (await MetadatosAsync(new AlcanceSinRestriccion()))
            .Calculo.Desactualizado.ShouldBeTrue();
    }

    [Fact]
    public async Task VIG_03_el_borrado_fisico_de_una_justificacion_desactualiza_el_tareo()
    {
        var trabajadorId = await CrearTrabajadorAsync("71000003", PlantaA);
        var justificacion = await AgregarJustificacionAsync(trabajadorId);
        await AgregarTareoAsync(trabajadorId);
        await EsperarMarcaPosteriorAsync();

        contexto.Justificaciones.Remove(justificacion);
        await contexto.SaveChangesAsync(CancellationToken.None);

        (await MetadatosAsync(new AlcanceSinRestriccion()))
            .Calculo.Desactualizado.ShouldBeTrue();
    }

    [Fact]
    public async Task VIG_04_un_cambio_de_version_de_turno_aplicable_desactualiza_el_tareo()
    {
        var trabajadorId = await CrearTrabajadorAsync("71000004", PlantaA);
        await contexto.AsignacionesTurno.AddAsync(new AsignacionTurno
        {
            TrabajadorId = trabajadorId,
            TurnoId = TurnoDiurno,
            FechaInicio = new DateOnly(2020, 1, 1)
        });
        await contexto.SaveChangesAsync(CancellationToken.None);
        await AgregarTareoAsync(trabajadorId);
        await EsperarMarcaPosteriorAsync();

        var version = await contexto.VersionesTurno
            .SingleAsync(v => v.TurnoId == TurnoDiurno && v.FechaFin == null);
        version.MinutosTolerancia++;
        await contexto.SaveChangesAsync(CancellationToken.None);

        (await MetadatosAsync(new AlcanceSinRestriccion()))
            .Calculo.Desactualizado.ShouldBeTrue();
    }

    [Fact]
    public async Task VIG_05_alta_modificacion_y_borrado_de_feriado_desactualizan_el_tareo()
    {
        var trabajadorId = await CrearTrabajadorAsync("71000005", PlantaA);
        var tareo = await AgregarTareoAsync(trabajadorId);
        await EsperarMarcaPosteriorAsync();

        var feriado = new Feriado { Fecha = Dia, Descripcion = "Feriado local", PlantaId = PlantaA };
        await contexto.Feriados.AddAsync(feriado);
        await contexto.SaveChangesAsync(CancellationToken.None);
        (await MetadatosAsync(new AlcanceSinRestriccion())).Calculo.Desactualizado.ShouldBeTrue();

        await MarcarTareoComoRecalculadoAsync(tareo);
        await EsperarMarcaPosteriorAsync();
        feriado.Fecha = Dia.AddDays(1);
        await contexto.SaveChangesAsync(CancellationToken.None);
        (await MetadatosAsync(new AlcanceSinRestriccion())).Calculo.Desactualizado.ShouldBeTrue();

        await MarcarTareoComoRecalculadoAsync(tareo);
        await EsperarMarcaPosteriorAsync();
        contexto.Feriados.Remove(feriado);
        await contexto.SaveChangesAsync(CancellationToken.None);
        (await MetadatosAsync(new AlcanceSinRestriccion())).Calculo.Desactualizado.ShouldBeTrue();
    }

    [Fact]
    public async Task VIG_07_un_cambio_del_tramo_de_rotacion_aplicable_desactualiza_el_tareo()
    {
        var trabajadorId = await CrearTrabajadorAsync("71000007", PlantaA);
        var cuadrilla = new Cuadrilla { Codigo = "VIG", Nombre = "Cuadrilla VIG", PlantaId = PlantaA };
        await contexto.Cuadrillas.AddAsync(cuadrilla);
        await contexto.SaveChangesAsync(CancellationToken.None);

        var programacion = new ProgramacionCuadrilla
        {
            CuadrillaId = cuadrilla.Id,
            FechaInicio = new DateOnly(2026, 7, 1),
            FechaAncla = new DateOnly(2026, 7, 1)
        };
        var tramo = new TramoRotacion { Orden = 1, TurnoId = TurnoDiurno, Dias = 14 };
        programacion.Tramos.Add(tramo);
        await contexto.ProgramacionesCuadrilla.AddAsync(programacion);
        await contexto.AsignacionesTurno.AddAsync(new AsignacionTurno
        {
            TrabajadorId = trabajadorId,
            CuadrillaId = cuadrilla.Id,
            FechaInicio = new DateOnly(2026, 7, 1)
        });
        await contexto.SaveChangesAsync(CancellationToken.None);
        await AgregarTareoAsync(trabajadorId);
        await EsperarMarcaPosteriorAsync();

        tramo.Tipo = TipoTramoRotacion.Descanso;
        tramo.TurnoId = null;
        tramo.Dias = 1;
        await contexto.SaveChangesAsync(CancellationToken.None);

        (await MetadatosAsync(new AlcanceSinRestriccion()))
            .Calculo.Desactualizado.ShouldBeTrue();
    }

    [Fact]
    public async Task VIG_08_recalcular_una_planta_no_oculta_que_otra_sigue_desactualizada()
    {
        var trabajadorA = await CrearTrabajadorAsync("71000008", PlantaA);
        var trabajadorB = await CrearTrabajadorAsync("71000009", PlantaB);
        var tareoA = await AgregarTareoAsync(trabajadorA);
        await AgregarTareoAsync(trabajadorB);
        await EsperarMarcaPosteriorAsync();
        await AgregarJustificacionAsync(trabajadorA);
        await AgregarJustificacionAsync(trabajadorB);

        await MarcarTareoComoRecalculadoAsync(tareoA);

        (await MetadatosAsync(new AlcanceDePlantas(PlantaA))).Calculo.EstaAlDia.ShouldBeTrue();
        (await MetadatosAsync(new AlcanceDePlantas(PlantaB))).Calculo.Desactualizado.ShouldBeTrue();
        (await MetadatosAsync(new AlcanceSinRestriccion())).Calculo.Desactualizado.ShouldBeTrue();
    }

    [Fact]
    public async Task VIG_09_un_cambio_descriptivo_no_usado_por_el_calculo_no_invalida_el_tareo()
    {
        var trabajadorId = await CrearTrabajadorAsync("71000010", PlantaA);
        var tareo = await AgregarTareoAsync(trabajadorId);
        await EsperarMarcaPosteriorAsync();

        var trabajador = await contexto.Trabajadores.SingleAsync(t => t.Id == trabajadorId);
        trabajador.Telefono = "999 000 111";
        await contexto.SaveChangesAsync(CancellationToken.None);

        (await MetadatosAsync(new AlcanceSinRestriccion()))
            .Calculo.EstaAlDia.ShouldBeTrue();
        tareo.CalculadoEn.ShouldBeLessThan(trabajador.ModificadoEn!.Value);
    }

    [Fact]
    public async Task La_consulta_de_borrados_usa_el_indice_temporal_de_auditoria()
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = """
            EXPLAIN QUERY PLAN
            SELECT tabla, clave_registro, operacion, fecha_hora, detalle
            FROM registro_auditoria
            WHERE fecha_hora > $calculado
            """;
        comando.Parameters.AddWithValue("$calculado", DateTime.Now.AddDays(-1));

        var detalles = new List<string>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            detalles.Add(lector.GetString(3));
        }

        string.Join(" | ", detalles).ShouldContain(
            "ix_registro_auditoria_fecha_hora", Case.Insensitive);
    }

    private async Task<MetadatosReporte> MetadatosAsync(IProveedorAlcancePlantas alcance) =>
        await new ServicioReportes(contexto, alcance).ObtenerMetadatosAsync(
            new FiltroReporte { Alcance = AlcanceReporte.Periodo, PeriodoId = PeriodoJulio2026 },
            "contrato-vigencia",
            CancellationToken.None);

    private async Task<long> CrearTrabajadorAsync(string dni, long plantaId)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = dni,
            ApellidoPaterno = "VIGENCIA",
            ApellidoMaterno = "PRUEBA",
            Nombres = dni,
            Sexo = Sexo.Masculino,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = plantaId,
            AreaId = AreaProduccion,
            SeccionId = SeccionGeneral,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        trabajador.AbrirVinculo("prueba");
        await contexto.Trabajadores.AddAsync(trabajador);
        await contexto.SaveChangesAsync(CancellationToken.None);
        return trabajador.Id;
    }

    private async Task<TareoDiario> AgregarTareoAsync(long trabajadorId)
    {
        var tareo = new TareoDiario
        {
            PeriodoId = PeriodoJulio2026,
            TrabajadorId = trabajadorId,
            Fecha = Dia,
            Estado = EstadoTareo.Puntual,
            TipoDiaNoLaborable = TipoDiaNoLaborable.Ninguno,
            CalculadoEn = DateTime.Now
        };
        await contexto.TareosDiarios.AddAsync(tareo);
        await contexto.SaveChangesAsync(CancellationToken.None);
        return tareo;
    }

    private async Task MarcarTareoComoRecalculadoAsync(TareoDiario tareo)
    {
        tareo.CalculadoEn = DateTime.Now.AddMilliseconds(10);
        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<Justificacion> AgregarJustificacionAsync(long trabajadorId)
    {
        var justificacion = new Justificacion
        {
            TrabajadorId = trabajadorId,
            PeriodoId = PeriodoJulio2026,
            Fecha = Dia,
            Origen = OrigenJustificacion.Manual,
            Tipo = TipoJustificacion.Ausencia,
            Motivo = "Contrato de vigencia",
            RegistradoPorUsuario = "contrato-vigencia",
            RegistradoEn = DateTime.Now
        };
        await contexto.Justificaciones.AddAsync(justificacion);
        await contexto.SaveChangesAsync(CancellationToken.None);
        return justificacion;
    }

    private async Task AgregarMarcacionAsync()
    {
        var importacion = new ImportacionMarcador
        {
            EquipoId = equipoId,
            NombreArchivo = $"vig-{Guid.NewGuid():N}.txt",
            HashContenido = Guid.NewGuid().ToString("N"),
            FechaCarga = DateTime.Now,
            CargadoPorUsuario = "contrato-vigencia"
        };
        await contexto.Importaciones.AddAsync(importacion);
        await contexto.SaveChangesAsync(CancellationToken.None);

        await contexto.MarcacionesCrudas.AddAsync(new MarcacionCruda
        {
            EquipoId = equipoId,
            IdMarcador = "VIG-01",
            FechaHora = Dia.ToDateTime(new TimeOnly(7, 0)),
            ImportacionId = importacion.Id
        });
        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private static Task EsperarMarcaPosteriorAsync() => Task.Delay(20);

    private sealed class UsuarioAuditoriaVigencia : IProveedorUsuarioActual
    {
        public string Usuario => "contrato-vigencia";
    }
}
