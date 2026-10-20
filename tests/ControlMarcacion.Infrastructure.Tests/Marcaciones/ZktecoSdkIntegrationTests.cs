using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Infrastructure.Marcaciones;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

public sealed class ZktecoSdkIntegrationTests : IDisposable
{
    private static readonly string RespaldoReal =
        Path.Combine(AppContext.BaseDirectory, "Recursos", "BKtransaction.dat");

    private readonly string temporal = Path.Combine(
        Path.GetTempPath(),
        $"control-marcacion-zksdk-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(temporal))
        {
            Directory.Delete(temporal, recursive: true);
        }
    }

    [Fact]
    public void ZKSDK_01_sdk_permanece_fuera_de_domain_y_application()
    {
        var ensamblados = new[]
        {
            typeof(ControlMarcacion.Domain.Marcaciones.MarcacionCruda).Assembly,
            typeof(ControlMarcacion.Application.Marcaciones.IFuenteDeMarcaciones).Assembly
        };

        foreach (var ensamblado in ensamblados)
        {
            var referencias = ensamblado.GetReferencedAssemblies()
                .Select(x => x.Name ?? string.Empty)
                .ToArray();

            referencias.ShouldNotContain(x =>
                x.Contains("zkemkeeper", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("plcommpro", StringComparison.OrdinalIgnoreCase));
            referencias.ShouldNotContain("ControlMarcacion.Infrastructure");
        }
    }

    [Fact]
    public void ZKSDK_02_y_07_interno_preserva_resultado_y_no_invoca_sdk()
    {
        var parser = new ParserSdkDePrueba(CsvSdkValido);
        var lector = CrearAutomatico("Interno", parser);

        using var archivo = File.OpenRead(RespaldoReal);
        var resultado = lector.Leer(archivo);

        resultado.Marcaciones.Count.ShouldBe(7);
        resultado.EventosEquipoIgnorados.Count.ShouldBe(2);
        resultado.Rechazadas.ShouldBeEmpty();
        parser.Invocaciones.ShouldBe(0);
    }

    [Fact]
    public void ZKSDK_03_comparar_devuelve_solo_la_fuente_interna()
    {
        var parser = new ParserSdkDePrueba(CsvSdkValido);
        var lector = CrearAutomatico("Comparar", parser);

        using var archivo = File.OpenRead(RespaldoReal);
        var resultado = lector.Leer(archivo);

        resultado.Marcaciones.Count.ShouldBe(7);
        resultado.SerieEquipo.ShouldBe("VGU6251600671");
        parser.Invocaciones.ShouldBe(1);
        Directory.GetFiles(temporal, "*-summary.json").Length.ShouldBe(1);
    }

    [Fact]
    public void ZKSDK_04_detecta_id_fecha_y_codigo_diferentes()
    {
        Directory.CreateDirectory(temporal);
        var opciones = Options.Create(new OpcionesLectorBkTransaction
        {
            DirectorioDiagnostico = temporal
        });
        var comparador = new ComparadorLecturasBkTransaction(opciones);
        var interno = new ResultadoLectura(
            [new MarcacionLeida("14", new DateTime(2026, 10, 1, 8, 0, 0), "1", "10", 1, "")],
            []);
        var sdk = new ResultadoLectura(
            [new MarcacionLeida("0014", new DateTime(2026, 10, 1, 8, 1, 0), "1", "11", 1, "")],
            []);

        var resumen = comparador.Escribir([1, 2, 3], interno, sdk);

        resumen.IdDiferente.ShouldBe(1);
        resumen.FechaHoraDiferente.ShouldBe(1);
        resumen.CodigoEventoDiferente.ShouldBe(1);
        resumen.Diferencias.ShouldBe(1);
    }

    [Fact]
    public void ZKSDK_05_sdk_no_configurado_da_error_explicito()
    {
        var parser = new ZktecoSdkOfflineParserNativo(
            Options.Create(new OpcionesLectorBkTransaction()));

        var error = Should.Throw<SdkZktecoException>(() => parser.Procesar(new byte[] { 1, 2, 3 }));

        error.Codigo.ShouldBe("SDK_NO_DISPONIBLE");
    }

    [Fact]
    public void ZKSDK_05_comparar_con_sdk_fallido_conserva_interno_y_marca_incompleta()
    {
        var lector = CrearAutomatico("Comparar", new ParserSdkFallido());

        using var archivo = File.OpenRead(RespaldoReal);
        var resultado = lector.Leer(archivo);

        resultado.Marcaciones.Count.ShouldBe(7);
        var resumen = File.ReadAllText(Directory.GetFiles(temporal, "*-summary.json").Single());
        resumen.ShouldContain("\"Estado\": \"INCOMPLETA\"");
        resumen.ShouldContain("\"CodigoErrorSdk\": \"SDK_ERROR\"");
    }

    [Fact]
    public void ZKSDK_06_salida_sdk_se_normaliza_y_conserva_ceros_del_pin()
    {
        var lectura = LectorRespaldoZktecoSdk.InterpretarCsv(CsvSdkValido);

        lectura.Marcaciones.Count.ShouldBe(1);
        lectura.Marcaciones[0].IdMarcador.ShouldBe("0014");
        lectura.Marcaciones[0].FechaHora.ShouldBe(new DateTime(2026, 10, 1, 3, 26, 9));
        lectura.Marcaciones[0].TipoMarcacion.ShouldBe("5");
    }

    [Fact]
    public void ZKSDK_08_acepta_cabecera_pe_x64()
    {
        Directory.CreateDirectory(temporal);
        var ruta = Path.Combine(temporal, "plcommpro.dll");
        var pe = new byte[128];
        BitConverter.GetBytes(64).CopyTo(pe, 0x3c);
        BitConverter.GetBytes(0x00004550u).CopyTo(pe, 64);
        BitConverter.GetBytes((ushort)0x8664).CopyTo(pe, 68);
        File.WriteAllBytes(ruta, pe);

        Should.NotThrow(() => ZktecoSdkOfflineParserNativo.ValidarArquitecturaX64(ruta));
    }

    private LectorMarcadorAutomatico CrearAutomatico(string modo, IZktecoSdkOfflineParser parser)
    {
        Directory.CreateDirectory(temporal);
        var opcionesMarcador = Options.Create(new OpcionesMarcador { DiasAntiguedadMaxima = 100_000 });
        var opcionesSdk = Options.Create(new OpcionesLectorBkTransaction
        {
            LectorBkTransaction = modo,
            DirectorioDiagnostico = temporal
        });
        var lectorSdk = new LectorRespaldoZktecoSdk(parser);
        var comparador = new ComparadorLecturasBkTransaction(opcionesSdk);

        return new LectorMarcadorAutomatico(
            new LectorTxtZkteco(opcionesMarcador),
            new LectorRespaldoZkteco(opcionesMarcador),
            lectorSdk,
            opcionesSdk,
            comparador);
    }

    private const string CsvSdkValido =
        "VGU6251600751,ZAM70-NF28VA-Ver3.0.15\r\n" +
        "transaction\r\n" +
        "Cardno,Pin,Verified,DoorID,EventType,InOutState,Time_second\r\n" +
        "14,0014,1,1,5,0,2026-10-01 03:26:09\r\n";

    private sealed class ParserSdkDePrueba(string csv) : IZktecoSdkOfflineParser
    {
        public int Invocaciones { get; private set; }

        public ResultadoSdkOffline Procesar(ReadOnlyMemory<byte> archivo)
        {
            Invocaciones++;
            archivo.Length.ShouldBeGreaterThan(0);
            return new ResultadoSdkOffline("prueba", "x64", csv, 1);
        }
    }

    private sealed class ParserSdkFallido : IZktecoSdkOfflineParser
    {
        public ResultadoSdkOffline Procesar(ReadOnlyMemory<byte> archivo) =>
            throw new SdkZktecoException("SDK_ERROR", "Fallo controlado de prueba.");
    }
}
