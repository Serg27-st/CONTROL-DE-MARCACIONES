using System.Text;
using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Infrastructure.Marcaciones;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

/// <summary>
/// Pruebas del lector del respaldo binario del marcador.
/// </summary>
/// <remarks>
/// Lo que más se prueba aquí no es que sepa leer, sino que <b>se niegue a adivinar</b>.
/// El formato es propietario y la única muestra real tenía nueve registros: la garantía
/// que sostiene todo esto es que, cuando el archivo no encaja, la carga falla en vez de
/// entregar marcaciones atribuidas a quien no corresponde.
/// </remarks>
public class LectorRespaldoZktecoTests
{
    /// <summary>
    /// La ventana de antigüedad se abre de par en par salvo donde se prueba justamente
    /// eso: con el valor por omisión, estas pruebas empezarían a fallar solas en cuanto la
    /// fecha de la muestra quedara fuera de los 400 días.
    /// </summary>
    private static LectorRespaldoZkteco CrearLector(int diasAntiguedad = 100_000) =>
        new(Options.Create(new OpcionesMarcador { DiasAntiguedadMaxima = diasAntiguedad }));

    private static ResultadoLectura Leer(byte[] datos, int diasAntiguedad = 100_000)
    {
        using var flujo = new MemoryStream(datos);
        return CrearLector(diasAntiguedad).Leer(flujo);
    }

    // ------------------------------------------------------------------
    // La codificación de tiempo de ZKTeco
    // ------------------------------------------------------------------

    /// <summary>
    /// El valor es el del primer registro del archivo real. ZKTeco trata todos los meses
    /// como de 31 días; leerlo como epoch Unix daría una fecha plausible y equivocada,
    /// que es la peor clase de error.
    /// </summary>
    [Fact]
    public void Decodifica_el_tiempo_del_equipo_real()
    {
        LectorRespaldoZkteco.DecodificarTiempo(856_451_503)
            .ShouldBe(new DateTime(2026, 8, 24, 15, 11, 43));
    }

    [Fact]
    public void La_codificacion_y_la_decodificacion_son_inversas()
    {
        var fecha = new DateTime(2026, 7, 31, 19, 14, 25);

        LectorRespaldoZkteco.DecodificarTiempo(RespaldoDePrueba.CodificarTiempo(fecha))
            .ShouldBe(fecha);
    }

    [Theory]
    [InlineData(2026, 1, 31, 23, 59, 59)]
    [InlineData(2026, 2, 1, 0, 0, 0)]
    [InlineData(2026, 12, 31, 23, 59, 59)]
    [InlineData(2027, 1, 1, 0, 0, 0)]
    public void Conserva_segundos_y_limites_de_mes_y_anio(
        int anio,
        int mes,
        int dia,
        int hora,
        int minuto,
        int segundo)
    {
        var fecha = new DateTime(anio, mes, dia, hora, minuto, segundo);

        LectorRespaldoZkteco.DecodificarTiempo(RespaldoDePrueba.CodificarTiempo(fecha))
            .ShouldBe(fecha);
    }

    /// <summary>
    /// El empaquetado permite combinaciones como el 31 de febrero. Que existan es la señal
    /// de que se está leyendo el campo equivocado, así que devolver <c>null</c> es lo que
    /// hace fiable la deducción del layout.
    /// </summary>
    [Fact]
    public void Una_fecha_imposible_no_se_decodifica()
    {
        // 31 de febrero de 2026: día 30 (0-based) del mes 1 (0-based).
        var febrero31 = (uint)(((((2026 - 2000) * 12) + 1) * 31 + 30) * 86400);

        LectorRespaldoZkteco.DecodificarTiempo(febrero31).ShouldBeNull();
    }

    // ------------------------------------------------------------------
    // Extraer marcaciones
    // ------------------------------------------------------------------

    [Fact]
    public void Extrae_las_marcaciones_del_respaldo()
    {
        var datos = RespaldoDePrueba.Archivo(
            new RespaldoDePrueba.Marca(2, new DateTime(2026, 8, 24, 15, 13, 56)),
            new RespaldoDePrueba.Marca(3, new DateTime(2026, 8, 24, 15, 51, 48)),
            new RespaldoDePrueba.Marca(1, new DateTime(2026, 8, 24, 16, 4, 46)));

        var resultado = Leer(datos);

        resultado.Rechazadas.ShouldBeEmpty();
        resultado.Marcaciones.Count.ShouldBe(3);
        resultado.Marcaciones[0].IdMarcador.ShouldBe("2");
        resultado.Marcaciones[0].FechaHora.ShouldBe(new DateTime(2026, 8, 24, 15, 13, 56));
        resultado.Marcaciones[2].IdMarcador.ShouldBe("1");
    }

    [Fact]
    public void Conserva_el_modo_de_verificacion_y_el_tipo()
    {
        var datos = RespaldoDePrueba.Archivo(
            new RespaldoDePrueba.Marca(7, new DateTime(2026, 8, 24, 8, 0, 0), Verificacion: 3, Tipo: 1));

        var marcacion = Leer(datos).Marcaciones[0];

        marcacion.ModoVerificacion.ShouldBe("3");
        marcacion.TipoMarcacion.ShouldBe("1");
    }

    /// <summary>
    /// El identificador viaja como entero, así que <b>los ceros a la izquierda se
    /// pierden</b>: este formato no puede distinguir "0014" de "14", cosa que el attlog de
    /// texto sí hace y que el padrón real usa. Queda fijado en un test para que nadie
    /// suponga lo contrario.
    /// </summary>
    [Fact]
    public void El_identificador_sale_sin_ceros_a_la_izquierda()
    {
        var datos = RespaldoDePrueba.Archivo(
            new RespaldoDePrueba.Marca(14, new DateTime(2026, 8, 24, 8, 0, 0)));

        Leer(datos).Marcaciones[0].IdMarcador.ShouldBe("14");
    }

    // ------------------------------------------------------------------
    // Negarse a adivinar
    // ------------------------------------------------------------------

    /// <summary>
    /// En el archivo real los dos primeros registros tienen identificador cero: son los que
    /// el equipo escribe para sí mismo. Atribuirlos a un trabajador sería inventar una
    /// marcación que después se paga.
    /// </summary>
    [Fact]
    public void BKIGN_01_un_evento_sin_identificador_se_ignora_en_vez_de_atribuirse_o_rechazarse()
    {
        var datos = RespaldoDePrueba.Archivo(
            new RespaldoDePrueba.Marca(0, new DateTime(2026, 8, 24, 15, 11, 43)),
            new RespaldoDePrueba.Marca(2, new DateTime(2026, 8, 24, 15, 13, 56)));

        var resultado = Leer(datos);

        resultado.Marcaciones.Count.ShouldBe(1);
        resultado.Marcaciones[0].IdMarcador.ShouldBe("2");
        resultado.EventosEquipoIgnorados.Count.ShouldBe(1);
        resultado.EventosEquipoIgnorados[0].CodigoEvento.ShouldBe(3u);
        resultado.Rechazadas.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(27)]
    [InlineData(751)]
    [InlineData(752)]
    public void BKIGN_03_04_05_conserva_el_codigo_opaco_del_evento_ignorado(ushort codigo)
    {
        var datos = RespaldoDePrueba.Archivo(
            new RespaldoDePrueba.Marca(
                0,
                new DateTime(2026, 10, 1, 8, 0, 0),
                Verificacion: codigo > byte.MaxValue ? (byte)200 : (byte)1,
                Evento: codigo));

        var resultado = Leer(datos);

        resultado.Marcaciones.ShouldBeEmpty();
        resultado.Rechazadas.ShouldBeEmpty();
        resultado.EventosEquipoIgnorados.ShouldBe(
            [new EventoEquipoIgnorado(1, codigo)]);
    }

    [Fact]
    public void BKIGN_06_un_timestamp_imposible_sigue_siendo_error()
    {
        var fecha = new DateTime(2026, 10, 1, 8, 0, 0);
        var marcas = Enumerable.Range(0, 11)
            .Select(i => new RespaldoDePrueba.Marca((uint)(i + 1), fecha.AddMinutes(i)))
            .ToArray();
        var datos = RespaldoDePrueba.Archivo(marcas);
        var febrero31 = (uint)(((((2026 - 2000) * 12) + 1) * 31 + 30) * 86400);
        BitConverter.GetBytes(febrero31).CopyTo(
            datos,
            RespaldoDePrueba.Cabecera().Length + RespaldoDePrueba.DesplazamientoReal + 12);

        var resultado = Leer(datos);

        resultado.Marcaciones.Count.ShouldBe(10);
        resultado.EventosEquipoIgnorados.ShouldBeEmpty();
        resultado.Rechazadas.Count.ShouldBe(1);
        resultado.Rechazadas[0].Motivo.ShouldContain("no es una fecha real");
    }

    [Fact]
    public void BKIGN_07_un_registro_truncado_sigue_siendo_error_estructural()
    {
        var completo = RespaldoDePrueba.Archivo(
            new RespaldoDePrueba.Marca(1, new DateTime(2026, 10, 1, 8, 0, 0)),
            new RespaldoDePrueba.Marca(2, new DateTime(2026, 10, 1, 8, 1, 0)));

        var resultado = Leer(completo[..^1]);

        resultado.Marcaciones.ShouldBeEmpty();
        resultado.EventosEquipoIgnorados.ShouldBeEmpty();
        resultado.Rechazadas.Count.ShouldBe(1);
        resultado.Rechazadas[0].Motivo.ShouldContain("truncado");
    }

    [Fact]
    public void BKIGN_08_fixture_estructural_981_reparte_851_aceptadas_130_ignoradas_y_cero_errores()
    {
        var inicio = new DateTime(2026, 10, 1, 3, 26, 9);
        var marcas = new List<RespaldoDePrueba.Marca>(981);

        for (var i = 0; i < 851; i++)
        {
            marcas.Add(new RespaldoDePrueba.Marca((uint)((i % 127) + 1), inicio.AddSeconds(i)));
        }

        for (var i = 0; i < 126; i++)
        {
            marcas.Add(new RespaldoDePrueba.Marca(0, inicio.AddSeconds(851 + i), Evento: 27));
        }

        marcas.Add(new RespaldoDePrueba.Marca(0, inicio.AddSeconds(977), Verificacion: 200, Evento: 751));
        marcas.Add(new RespaldoDePrueba.Marca(0, inicio.AddSeconds(978), Verificacion: 200, Evento: 752));
        marcas.Add(new RespaldoDePrueba.Marca(0, inicio.AddSeconds(979), Verificacion: 200, Evento: 751));
        marcas.Add(new RespaldoDePrueba.Marca(0, inicio.AddSeconds(980), Verificacion: 200, Evento: 752));

        var resultado = Leer(RespaldoDePrueba.Archivo([.. marcas]));

        resultado.TotalLineas.ShouldBe(981);
        resultado.Marcaciones.Count.ShouldBe(851);
        resultado.EventosEquipoIgnorados.Count.ShouldBe(130);
        resultado.Rechazadas.ShouldBeEmpty();
        resultado.EventosEquipoIgnorados.Count(x => x.CodigoEvento == 27).ShouldBe(126);
        resultado.EventosEquipoIgnorados.Count(x => x.CodigoEvento == 751).ShouldBe(2);
        resultado.EventosEquipoIgnorados.Count(x => x.CodigoEvento == 752).ShouldBe(2);
    }

    /// <summary>
    /// Misma protección que en el TXT: un equipo con la fecha mal configurada contaminaría
    /// periodos ya cerrados.
    /// </summary>
    [Fact]
    public void Rechaza_marcaciones_mas_viejas_que_la_ventana_configurada()
    {
        var datos = RespaldoDePrueba.Archivo(
            new RespaldoDePrueba.Marca(5, new DateTime(2020, 1, 15, 8, 0, 0)));

        var resultado = Leer(datos, diasAntiguedad: 400);

        resultado.Marcaciones.ShouldBeEmpty();
        resultado.Rechazadas[0].Motivo.ShouldContain("antigüedad máxima");
    }

    /// <summary>
    /// Si el último campo no mide cuatro bytes, este no es el esquema que sabemos leer.
    /// Forzarlo produciría fechas basura con aspecto de fechas.
    /// </summary>
    [Fact]
    public void Un_esquema_sin_fecha_al_final_se_rechaza_entero()
    {
        var datos = new List<byte>(
            RespaldoDePrueba.Cabecera(tamanoRegistro: 28, anchos: [4, 4, 4, 1]));
        datos.AddRange(new byte[28]);

        var resultado = Leer([.. datos]);

        resultado.Marcaciones.ShouldBeEmpty();
        resultado.Rechazadas.Count.ShouldBe(1);
        resultado.Rechazadas[0].Motivo.ShouldContain("esquema desconocido");
    }

    /// <summary>
    /// Registros llenos de ceros decodifican a enero del 2000, que el lector no acepta como
    /// plausible. Sin ningún desplazamiento válido, el archivo entero se rechaza.
    /// </summary>
    [Fact]
    public void Si_ningun_desplazamiento_da_fechas_validas_se_rechaza_el_archivo()
    {
        var datos = new List<byte>(RespaldoDePrueba.Cabecera());
        datos.AddRange(new byte[RespaldoDePrueba.TamanoRegistro * 3]);

        var resultado = Leer([.. datos]);

        resultado.Marcaciones.ShouldBeEmpty();
        resultado.Rechazadas.Count.ShouldBe(1);
        resultado.Rechazadas[0].Motivo.ShouldContain("No se pudo interpretar");
    }

    [Fact]
    public void Un_respaldo_sin_registros_se_rechaza_con_un_motivo_claro()
    {
        var resultado = Leer(RespaldoDePrueba.Cabecera());

        resultado.Marcaciones.ShouldBeEmpty();
        resultado.Rechazadas[0].Motivo.ShouldContain("ningún registro completo");
    }

    // ------------------------------------------------------------------
    // Deducir el layout en vez de darlo por supuesto
    // ------------------------------------------------------------------

    /// <summary>
    /// El equipo real deja 8 bytes de relleno delante del bloque de campos, pero eso no
    /// está declarado en ninguna parte. Aquí el registro mide justo lo que suman los campos
    /// —sin relleno— y el lector tiene que encontrarlos igual: es lo que evita que un
    /// cambio de firmware obligue a tocar el código.
    /// </summary>
    [Fact]
    public void Encuentra_los_campos_aunque_el_registro_no_tenga_relleno()
    {
        var fecha = new DateTime(2026, 8, 24, 9, 30, 15);
        var datos = new List<byte>(RespaldoDePrueba.Cabecera(tamanoRegistro: 16));

        var registro = new byte[16];
        BitConverter.GetBytes(42u).CopyTo(registro, 0);
        BitConverter.GetBytes(0u).CopyTo(registro, 4);
        registro[8] = 1;
        registro[9] = 1;
        registro[10] = 3;
        registro[11] = 0;
        BitConverter.GetBytes(RespaldoDePrueba.CodificarTiempo(fecha)).CopyTo(registro, 12);
        datos.AddRange(registro);

        var resultado = Leer([.. datos]);

        resultado.Rechazadas.ShouldBeEmpty();
        resultado.Marcaciones.Count.ShouldBe(1);
        resultado.Marcaciones[0].IdMarcador.ShouldBe("42");
        resultado.Marcaciones[0].FechaHora.ShouldBe(fecha);
    }

    /// <summary>
    /// Con un solo registro sobran desplazamientos que dan una fecha creíble, y el
    /// equivocado corre el identificador sin que nada lo delate: leer la fecha tres bytes
    /// antes da 2027-02-23 —plausible— y convierte el ID 2 en 33 554 432. La alineación a
    /// cuatro bytes es lo que lo descarta.
    /// </summary>
    [Fact]
    public void Un_solo_registro_no_alcanza_para_confundir_el_desplazamiento()
    {
        var datos = RespaldoDePrueba.Archivo(
            new RespaldoDePrueba.Marca(2, new DateTime(2026, 8, 24, 15, 13, 56)));

        var resultado = Leer(datos);

        resultado.Rechazadas.ShouldBeEmpty();
        resultado.Marcaciones[0].IdMarcador.ShouldBe("2");
        resultado.Marcaciones[0].FechaHora.ShouldBe(new DateTime(2026, 8, 24, 15, 13, 56));
    }

    // ------------------------------------------------------------------
    // El reparto entre los dos formatos
    // ------------------------------------------------------------------

    private static LectorMarcadorAutomatico CrearAutomatico()
    {
        var opciones = Options.Create(new OpcionesMarcador { DiasAntiguedadMaxima = 100_000 });
        return new LectorMarcadorAutomatico(
            new LectorTxtZkteco(opciones), new LectorRespaldoZkteco(opciones));
    }

    [Fact]
    public void El_automatico_manda_el_respaldo_al_lector_binario()
    {
        var datos = RespaldoDePrueba.Archivo(
            new RespaldoDePrueba.Marca(2, new DateTime(2026, 8, 24, 15, 13, 56)));
        using var flujo = new MemoryStream(datos);

        var resultado = CrearAutomatico().Leer(flujo);

        resultado.Rechazadas.ShouldBeEmpty();
        resultado.Marcaciones.Count.ShouldBe(1);
        resultado.Marcaciones[0].IdMarcador.ShouldBe("2");
    }

    /// <summary>
    /// El mismo contenido tabulado que llega como <c>.dat</c> tiene que seguir leyéndose
    /// como texto: <b>la extensión no decide nada</b>, y los ceros significativos del
    /// identificador —que el binario perdería— se conservan.
    /// </summary>
    [Fact]
    public void El_automatico_manda_el_texto_al_lector_de_siempre()
    {
        using var flujo = new MemoryStream(
            Encoding.ASCII.GetBytes("     0044\t2026-07-01 06:53:00\t1\t0\t1\t0\r\n"));

        var resultado = CrearAutomatico().Leer(flujo);

        resultado.Rechazadas.ShouldBeEmpty();
        resultado.Marcaciones.Count.ShouldBe(1);
        resultado.Marcaciones[0].IdMarcador.ShouldBe("0044");
        resultado.Marcaciones[0].FechaHora.ShouldBe(new DateTime(2026, 7, 1, 6, 53, 0));
    }
}
