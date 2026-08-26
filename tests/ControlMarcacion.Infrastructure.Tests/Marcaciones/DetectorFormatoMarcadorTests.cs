using System.Text;
using ControlMarcacion.Infrastructure.Marcaciones;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

/// <summary>
/// Pruebas del detector que distingue el archivo de asistencia del respaldo del equipo.
/// </summary>
/// <remarks>
/// El detector es el que decide qué lector se usa, así que un falso positivo mandaría un
/// attlog perfectamente bueno al parser binario. Por eso hay tantas pruebas de lo que
/// <b>no</b> debe reconocer como de lo que sí.
/// </remarks>
public class DetectorFormatoMarcadorTests
{
    private const string LineaAttlog = "       73\t2025-12-01 06:06:38\t1\t0\t1\t0\r\n";

    // ------------------------------------------------------------------
    // Reconocer el respaldo
    // ------------------------------------------------------------------

    [Fact]
    public void Reconoce_el_respaldo_y_extrae_serie_y_modelo()
    {
        var respaldo = DetectorFormatoMarcador.Detectar(RespaldoDePrueba.Cabecera());

        respaldo.ShouldNotBeNull();
        respaldo.Serie.ShouldBe(RespaldoDePrueba.SerieReal);
        respaldo.Modelo.ShouldBe(RespaldoDePrueba.ModeloReal);
    }

    /// <summary>
    /// El esquema es lo que permite leer los registros sin adivinar: si el detector no lo
    /// devolviera entero, el parser tendría que llevar los anchos grabados a mano.
    /// </summary>
    [Fact]
    public void Devuelve_el_esquema_completo_de_los_registros()
    {
        var respaldo = DetectorFormatoMarcador.Detectar(RespaldoDePrueba.Cabecera());

        respaldo.ShouldNotBeNull();
        respaldo.TamanoRegistro.ShouldBe(28);
        respaldo.Campos.ShouldBe(7);
        respaldo.AnchosDeCampo.ShouldBe([4, 4, 1, 1, 1, 1, 4]);
        respaldo.BytesDeclarados.ShouldBe(16);

        // 1 + 13 (serie) + 1 + 22 (modelo) + 4 (tamaño) + 1 (nº campos) + 21 (descriptores)
        respaldo.InicioRegistros.ShouldBe(63);
    }

    [Fact]
    public void Reconoce_el_respaldo_leyendo_desde_un_stream()
    {
        using var flujo = new MemoryStream(RespaldoDePrueba.Cabecera());

        DetectorFormatoMarcador.Detectar(flujo).ShouldNotBeNull();
    }

    /// <summary>
    /// Quien llama sigue leyendo el archivo después de preguntar: si el detector se dejara
    /// el cursor movido, el lector de texto se comería la primera línea.
    /// </summary>
    [Fact]
    public void No_mueve_la_posicion_del_stream()
    {
        using var flujo = new MemoryStream(Encoding.ASCII.GetBytes(LineaAttlog));

        DetectorFormatoMarcador.Detectar(flujo);

        flujo.Position.ShouldBe(0);
    }

    // ------------------------------------------------------------------
    // No confundirse con el archivo bueno
    // ------------------------------------------------------------------

    /// <summary>
    /// El caso que obliga a validar los caracteres: un attlog empieza con espacios (0x20),
    /// y 32 es una longitud de cadena perfectamente plausible.
    /// </summary>
    [Fact]
    public void No_confunde_un_attlog_de_texto_con_un_respaldo()
    {
        var attlog = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat(LineaAttlog, 5)));

        DetectorFormatoMarcador.Detectar(attlog).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("       73\t2025-12-01 06:06:38\t1\t0\t1\t0")]
    [InlineData("cualquier texto suelto")]
    public void No_ve_respaldos_donde_no_los_hay(string contenido)
    {
        DetectorFormatoMarcador.Detectar(Encoding.ASCII.GetBytes(contenido)).ShouldBeNull();
    }

    [Fact]
    public void Un_tamano_de_registro_absurdo_no_es_un_respaldo()
    {
        DetectorFormatoMarcador
            .Detectar(RespaldoDePrueba.Cabecera(tamanoRegistro: 999_999))
            .ShouldBeNull();
    }

    [Fact]
    public void Sin_campos_no_es_un_respaldo()
    {
        DetectorFormatoMarcador.Detectar(RespaldoDePrueba.Cabecera(anchos: [])).ShouldBeNull();
    }

    /// <summary>
    /// Campos que no caben en el registro significan que lo leído no es un esquema.
    /// Dejarlo pasar haría que el parser interpretara relleno como marcaciones.
    /// </summary>
    [Fact]
    public void Campos_que_no_caben_en_el_registro_no_son_un_esquema()
    {
        DetectorFormatoMarcador
            .Detectar(RespaldoDePrueba.Cabecera(tamanoRegistro: 8, anchos: [4, 4, 4]))
            .ShouldBeNull();
    }

    [Fact]
    public void Un_ancho_de_campo_en_cero_no_es_un_esquema()
    {
        DetectorFormatoMarcador
            .Detectar(RespaldoDePrueba.Cabecera(anchos: [4, 0, 4]))
            .ShouldBeNull();
    }

    [Fact]
    public void Un_tipo_de_campo_desconocido_no_es_un_esquema()
    {
        DetectorFormatoMarcador
            .Detectar(RespaldoDePrueba.Cabecera(tipoDeCampo: (byte)'x'))
            .ShouldBeNull();
    }

    /// <summary>
    /// Una cabecera cortada a la mitad —transferencia interrumpida, USB retirado a
    /// destiempo— no debe pasar por buena.
    /// </summary>
    [Fact]
    public void Una_cabecera_incompleta_no_es_un_respaldo()
    {
        var truncada = RespaldoDePrueba.Cabecera()[..30];

        DetectorFormatoMarcador.Detectar(truncada).ShouldBeNull();
    }
}
