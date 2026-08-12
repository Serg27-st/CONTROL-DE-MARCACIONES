using System.Text;
using ControlMarcacion.Infrastructure.Personal;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Personal;

/// <summary>
/// Lectura del CSV de carga masiva.
/// </summary>
/// <remarks>
/// Los archivos reales salen de Excel en español, así que las variantes que se prueban
/// aquí no son hipotéticas: punto y coma o coma según la configuración regional de quien
/// exportó, comillas alrededor de los campos con delimitador dentro, y codificación
/// Windows-1252 salvo que alguien elija "CSV UTF-8" a propósito.
/// </remarks>
public class LectorCsvTests
{
    private static MemoryStream Contenido(string texto, Encoding? codificacion = null) =>
        new((codificacion ?? Encoding.UTF8).GetBytes(texto));

    [Fact]
    public void Detecta_el_punto_y_coma_como_delimitador()
    {
        var filas = LectorCsv.Leer(Contenido("a;b;c\n1;2;3"));

        filas.Count.ShouldBe(2);
        filas[1].ShouldBe(["1", "2", "3"]);
    }

    [Fact]
    public void Detecta_la_coma_como_delimitador()
    {
        // Un archivo exportado desde una máquina en inglés. Fijar el punto y coma
        // devolvería una sola columna con todo el contenido dentro.
        var filas = LectorCsv.Leer(Contenido("a,b,c\n1,2,3"));

        filas[1].ShouldBe(["1", "2", "3"]);
    }

    [Fact]
    public void Respeta_el_delimitador_dentro_de_comillas()
    {
        var filas = LectorCsv.Leer(Contenido("nombre;direccion\nPEREZ;\"AV. LOS OLIVOS 123, DPTO 4\""));

        filas[1].ShouldBe(["PEREZ", "AV. LOS OLIVOS 123, DPTO 4"]);
    }

    [Fact]
    public void Interpreta_las_comillas_duplicadas_como_una_comilla_literal()
    {
        var filas = LectorCsv.Leer(Contenido("a;b\n\"dice \"\"hola\"\"\";2"));

        filas[1][0].ShouldBe("dice \"hola\"");
    }

    [Fact]
    public void Lee_los_acentos_de_un_archivo_en_windows_1252()
    {
        // Excel en español guarda así por defecto. Leerlo como UTF-8 metería apellidos
        // con rombos negros al padrón.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var filas = LectorCsv.Leer(Contenido("nombre\nMUÑOZ GARCÍA", Encoding.GetEncoding(1252)));

        filas[1][0].ShouldBe("MUÑOZ GARCÍA");
    }

    [Fact]
    public void Lee_los_acentos_de_un_archivo_en_utf8()
    {
        var filas = LectorCsv.Leer(Contenido("nombre\nMUÑOZ GARCÍA"));

        filas[1][0].ShouldBe("MUÑOZ GARCÍA");
    }

    [Fact]
    public void Descarta_el_bom_del_utf8_para_no_ensuciar_el_primer_encabezado()
    {
        // Con el BOM pegado, "tipo_documento" no coincidiría con ningún alias y la carga
        // se rechazaría diciendo que falta una columna que está a la vista.
        var filas = LectorCsv.Leer(Contenido("﻿tipo_documento;nombres\nDNI;JUAN"));

        filas[0][0].ShouldBe("tipo_documento");
    }

    [Fact]
    public void Ignora_las_lineas_en_blanco()
    {
        // Excel deja una línea final al guardar; una fila vacía no es un trabajador.
        var filas = LectorCsv.Leer(Contenido("a;b\n1;2\n\n"));

        filas.Count.ShouldBe(2);
    }

    [Fact]
    public void Acepta_los_saltos_de_linea_de_windows()
    {
        var filas = LectorCsv.Leer(Contenido("a;b\r\n1;2\r\n"));

        filas.Count.ShouldBe(2);
        filas[1].ShouldBe(["1", "2"]);
    }

    [Fact]
    public void Un_archivo_vacio_no_devuelve_filas()
    {
        LectorCsv.Leer(Contenido(string.Empty)).ShouldBeEmpty();
    }
}
