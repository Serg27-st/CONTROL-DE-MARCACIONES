using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Infrastructure.Marcaciones;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

public sealed class LectorUsuariosZktecoTests
{
    private static readonly EquipoOrigenUsuariosZkteco Equipo = new(17, "EQ-PLANTA-01");
    private readonly LectorUsuariosZkteco lector = new();

    [Fact]
    public void Lee_archivo_de_referencia_anonimizado_y_preserva_identificadores()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Recursos", "user-u-anonimizado.b64");
        var bytes = Convert.FromBase64String(File.ReadAllText(ruta).Trim());

        var resultado = Leer(bytes);

        resultado.Estado.ShouldBe(EstadoArchivoUsuariosZkteco.ValidoConAdvertencias);
        resultado.Equipo.ShouldBe(Equipo);
        resultado.Usuarios.Count.ShouldBe(1);
        resultado.Usuarios[0].IndiceRegistro.ShouldBe(1);
        resultado.Usuarios[0].Uid.ShouldBe(1UL);
        resultado.Usuarios[0].Pin.ShouldBe("00001234");
        resultado.Usuarios[0].NombreOriginal.ShouldBe("87654321");
        resultado.Usuarios[0].EstadoCandidatoIdentidad.ShouldBe(EstadoCandidatoIdentidadZkteco.NoVerificado);
        resultado.TamanoBytes.ShouldBe(bytes.Length);
        resultado.HuellaContenidoSha256.ShouldBe(Convert.ToHexString(SHA256.HashData(bytes)));
        resultado.LeidoEnUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void Huella_cambia_si_cambia_un_byte_y_no_expone_contenido_sensible()
    {
        var primero = Archivo(new Registro(1, "1", "12345678", Password: "secreto-a"));
        var segundo = Archivo(new Registro(1, "1", "12345678", Password: "secreto-b"));

        var lecturaA = Leer(primero);
        var lecturaB = Leer(segundo);

        lecturaA.HuellaContenidoSha256.ShouldNotBe(lecturaB.HuellaContenidoSha256);
        lecturaA.ToString().ShouldNotContain("secreto-a");
        typeof(ResultadoLecturaUsuariosZkteco).GetProperties()
            .ShouldNotContain(p => p.Name.Contains("Contenido", StringComparison.OrdinalIgnoreCase)
                && p.PropertyType != typeof(string));
    }

    [Fact]
    public void Lee_secuencia_uid_y_pin_de_siete_y_ocho_digitos()
    {
        var resultado = Leer(Archivo(
            new Registro(41, "1234567", "12345678"),
            new Registro(42, "12345678", "87654321")));

        resultado.Usuarios.Select(x => x.Uid).ShouldBe([41UL, 42UL]);
        resultado.Usuarios.Select(x => x.Pin).ShouldBe(["1234567", "12345678"]);
        resultado.Usuarios.Select(x => x.IndiceRegistro).ShouldBe([1, 2]);
    }

    [Theory]
    [InlineData(null, EstadoCandidatoIdentidadZkteco.Ausente, IncidenciaUsuarioZkteco.NombreAusente)]
    [InlineData("1234567", EstadoCandidatoIdentidadZkteco.NoVerificado, IncidenciaUsuarioZkteco.NombreNoTieneOchoCaracteres)]
    [InlineData("123456789", EstadoCandidatoIdentidadZkteco.NoVerificado, IncidenciaUsuarioZkteco.NombreNoTieneOchoCaracteres)]
    public void Name_no_se_declara_dni_y_sus_variantes_quedan_explicitas(
        string? nombre,
        EstadoCandidatoIdentidadZkteco estado,
        IncidenciaUsuarioZkteco incidencia)
    {
        var usuario = Leer(Archivo(new Registro(1, "7", nombre))).Usuarios.Single();

        usuario.EstadoCandidatoIdentidad.ShouldBe(estado);
        usuario.Observaciones.ShouldContain(incidencia);
    }

    [Fact]
    public void Marca_pin_y_name_duplicados_sin_resolver_automaticamente()
    {
        var resultado = Leer(Archivo(
            new Registro(1, "0007", "12345678"),
            new Registro(2, "0007", "12345678")));

        resultado.Usuarios.ShouldAllBe(x => x.Observaciones.Contains(IncidenciaUsuarioZkteco.PinDuplicado));
        resultado.Usuarios.ShouldAllBe(x => x.Observaciones.Contains(IncidenciaUsuarioZkteco.NombreDuplicado));
        resultado.Usuarios.ShouldAllBe(x => x.EstadoCandidatoIdentidad == EstadoCandidatoIdentidadZkteco.Ambiguo);
    }

    [Fact]
    public void Expone_administrador_pero_no_password_ni_datos_biometricos()
    {
        var usuario = Leer(Archivo(new Registro(1, "1", "ADMIN", Privilegio: 14, Password: "clave-de-prueba")))
            .Usuarios.Single();

        usuario.EsAdministrador.ShouldBeTrue();
        usuario.Observaciones.ShouldContain(IncidenciaUsuarioZkteco.UsuarioAdministrador);
        var nombres = typeof(UsuarioZktecoLeido).GetProperties().Select(x => x.Name).ToArray();
        nombres.ShouldNotContain(x => x.Contains("Password", StringComparison.OrdinalIgnoreCase));
        nombres.ShouldNotContain(x => x.Contains("Huella", StringComparison.OrdinalIgnoreCase));
        usuario.ToString().ShouldNotContain("clave-de-prueba");
    }

    [Theory]
    [MemberData(nameof(ArchivosInvalidos))]
    public void Rechaza_errores_de_formato_de_forma_controlada(byte[] archivo, string fragmento)
    {
        var resultado = Leer(archivo);

        resultado.Estado.ShouldBe(EstadoArchivoUsuariosZkteco.Rechazado);
        resultado.Usuarios.ShouldBeEmpty();
        resultado.Errores.Single().ShouldContain(fragmento, Case.Insensitive);
    }

    public static TheoryData<byte[], string> ArchivosInvalidos => new()
    {
        { [], "vacío" },
        { [1, 11, 1], "truncado" },
        { [2, 11, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11], "no corresponde" },
        { [1, 11, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 2, 1], "truncado" }
    };

    [Fact]
    public void Bytes_de_texto_invalidos_invalidan_solo_el_registro()
    {
        var archivo = Archivo(new Registro(1, "123", "12345678"));
        // Encabezado 13, UID = longitud+valor (2 bytes), CardNo = longitud (1),
        // PIN empieza con su longitud en 16 y su primer byte está en 17.
        archivo[17] = 0;

        var resultado = Leer(archivo);
        var usuario = resultado.Usuarios.Single();

        usuario.Estado.ShouldBe(EstadoRegistroUsuarioZkteco.Invalido);
        usuario.Observaciones.ShouldContain(IncidenciaUsuarioZkteco.BytesDeTextoInvalidos);
    }

    [Fact]
    public void Respeta_limites_configurables_de_bytes_y_registros()
    {
        var archivo = Archivo(new Registro(1, "1", "12345678"), new Registro(2, "2", "87654321"));

        Leer(archivo, new(archivo.Length, 2)).Estado.ShouldNotBe(EstadoArchivoUsuariosZkteco.Rechazado);
        Leer(archivo, new(archivo.Length - 1, 2)).Errores.Single().ShouldContain("límite");
        Leer(archivo, new(archivo.Length, 1)).Errores.Single().ShouldContain("registros");
    }

    [Fact]
    public void Lee_archivo_grande_dentro_del_limite_configurado()
    {
        var registros = Enumerable.Range(1, 1_000)
            .Select(i => new Registro(
                (ulong)i,
                i.ToString("D7", CultureInfo.InvariantCulture),
                i.ToString("D8", CultureInfo.InvariantCulture)))
            .ToArray();
        var archivo = Archivo(registros);

        var resultado = Leer(archivo, new OpcionesLecturaUsuariosZkteco(100_000, 1_000));

        resultado.Estado.ShouldNotBe(EstadoArchivoUsuariosZkteco.Rechazado);
        resultado.Usuarios.Count.ShouldBe(1_000);
        resultado.Usuarios[^1].Uid.ShouldBe(1_000UL);
    }

    [Fact]
    public void Exige_equipo_y_no_tiene_dependencias_de_persistencia()
    {
        using var flujo = new MemoryStream(Archivo(new Registro(1, "1", null)));

        Should.Throw<ArgumentException>(() => lector.Leer(flujo, new(0, string.Empty)));
        typeof(LectorUsuariosZkteco).GetConstructors().Single().GetParameters().ShouldBeEmpty();
        typeof(LectorUsuariosZkteco).Assembly.GetReferencedAssemblies()
            .ShouldNotContain(x => x.Name == "ControlMarcacion.Web");
    }

    private ResultadoLecturaUsuariosZkteco Leer(byte[] datos, OpcionesLecturaUsuariosZkteco? opciones = null)
    {
        using var flujo = new MemoryStream(datos);
        return lector.Leer(flujo, Equipo, opciones);
    }

    private static byte[] Archivo(params Registro[] registros)
    {
        using var flujo = new MemoryStream();
        flujo.Write([1, 11, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]);
        foreach (var registro in registros)
        {
            Campo(flujo, Entero(registro.Uid));
            Campo(flujo, []);
            Campo(flujo, Encoding.ASCII.GetBytes(registro.Pin));
            Campo(flujo, Encoding.ASCII.GetBytes(registro.Password ?? string.Empty));
            Campo(flujo, [0]);
            Campo(flujo, []);
            Campo(flujo, []);
            Campo(flujo, Encoding.Latin1.GetBytes(registro.Name ?? string.Empty));
            Campo(flujo, registro.Privilegio == 0 ? [] : [registro.Privilegio]);
            Campo(flujo, []);
            Campo(flujo, []);
        }

        return flujo.ToArray();
    }

    private static byte[] Entero(ulong valor) => valor <= byte.MaxValue
        ? [(byte)valor]
        : BitConverter.GetBytes(valor);

    private static void Campo(Stream flujo, byte[] valor)
    {
        flujo.WriteByte(checked((byte)valor.Length));
        flujo.Write(valor);
    }

    private sealed record Registro(
        ulong Uid,
        string Pin,
        string? Name,
        byte Privilegio = 0,
        string? Password = null);
}
