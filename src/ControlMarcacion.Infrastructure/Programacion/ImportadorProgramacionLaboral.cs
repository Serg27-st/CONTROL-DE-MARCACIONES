using System.Globalization;
using ClosedXML.Excel;
using ControlMarcacion.Application.Programacion;
using ControlMarcacion.Application.Programacion.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Infrastructure.Personal;

#pragma warning disable IDE0011

namespace ControlMarcacion.Infrastructure.Programacion;

internal sealed class ImportadorProgramacionLaboral(
    IConciliadorProgramacion conciliador) : IImportadorProgramacionLaboral
{
    private static readonly string[] FormatosFecha = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"];
    private static readonly string[] FormatosHora = ["HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss"];

    public byte[] GenerarPlantillaExcel()
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Programacion");
        var encabezados = Encabezados.Canonicos;

        for (var i = 0; i < encabezados.Length; i++)
        {
            hoja.Cell(1, i + 1).Value = encabezados[i];
        }

        var ejemplo = new object?[]
        {
            "DNI", "00000001", "EMPRESA DE PRUEBA", "UNIDAD A",
            new DateTime(2026, 11, 3), new DateTime(2026, 11, 7), null,
            "DIU", "Turno diurno", new TimeSpan(7, 0, 0), new TimeSpan(19, 0, 0),
            "NO", 0, "TRABAJO", "FIJO", "Ejemplo; reemplace todos los datos", "APROBACION-RRHH"
        };
        for (var i = 0; i < ejemplo.Length; i++) hoja.Cell(2, i + 1).Value = XLCellValue.FromObject(ejemplo[i], CultureInfo.InvariantCulture);

        var descanso = new object?[]
        {
            "DNI", "00000001", "EMPRESA DE PRUEBA", "UNIDAD A",
            null, null, new DateTime(2026, 11, 8), null, null, null, null,
            null, null, "DESCANSO", "ROTATIVO", "Descanso explícito", "APROBACION-RRHH"
        };
        for (var i = 0; i < descanso.Length; i++) hoja.Cell(3, i + 1).Value = XLCellValue.FromObject(descanso[i], CultureInfo.InvariantCulture);

        hoja.SheetView.FreezeRows(1);
        hoja.Range(1, 1, 1, encabezados.Length).Style
            .Fill.SetBackgroundColor(XLColor.FromHtml("#1F4E78"))
            .Font.SetFontColor(XLColor.White).Font.SetBold();
        hoja.Columns().AdjustToContents(10, 34);
        hoja.Column(2).Style.NumberFormat.Format = "@";
        hoja.Columns(5, 7).Style.DateFormat.Format = "dd/mm/yyyy";
        hoja.Columns(9, 10).Style.NumberFormat.Format = "hh:mm";
        hoja.Range(2, 14, 5001, 14).CreateDataValidation().List("\"TRABAJO,DESCANSO\"");
        hoja.Range(2, 15, 5001, 15).CreateDataValidation().List("\"FIJO,ROTATIVO\"");
        hoja.Range(2, 12, 5001, 12).CreateDataValidation().List("\"SI,NO\"");

        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return salida.ToArray();
    }

    public async Task<ResultadoPrevisualizacionProgramacion> PrevisualizarAsync(
        Stream contenido,
        string nombreArchivo,
        IReadOnlyCollection<long>? plantasAutorizadas,
        CancellationToken cancelacion = default)
    {
        var entradas = LeerEntradas(contenido, nombreArchivo);
        return await conciliador.PrevisualizarAsync(entradas, nombreArchivo, plantasAutorizadas, cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<ResultadoConfirmacionProgramacion> ConfirmarAsync(
        Stream contenido,
        string nombreArchivo,
        string huellaEsperada,
        string usuario,
        IReadOnlyCollection<long>? plantasAutorizadas,
        CancellationToken cancelacion = default)
    {
        var entradas = LeerEntradas(contenido, nombreArchivo);
        return await conciliador.ConfirmarAsync(entradas, nombreArchivo, huellaEsperada, usuario, plantasAutorizadas, cancelacion)
            .ConfigureAwait(false);
    }

    internal static IReadOnlyList<EntradaProgramacion> LeerEntradas(Stream contenido, string nombreArchivo)
    {
        if (!LectorExcel.EsLibroExcel(contenido))
            throw new ReglaNegocioException("La programación debe cargarse desde un archivo Excel .xlsx válido.");
        var filas = LectorExcelProgramacion.Leer(contenido);
        if (filas.Count < 2) throw new ReglaNegocioException("El Excel no contiene filas de programación.");
        var mapa = Encabezados.Crear(filas[0]);
        return filas.Skip(1).Select((f, i) => LeerFila(f, i + 2, mapa)).ToList();
    }

    private static EntradaProgramacion LeerFila(string[] fila, int numero, Encabezados mapa)
    {
        var tipoDiaTexto = mapa.Valor(fila, "tipo dia");
        var tipo = Normalizar(tipoDiaTexto) switch
        {
            "trabajo" => TipoDiaProgramacion.Trabajo,
            "descanso" => TipoDiaProgramacion.Descanso,
            _ => throw new ReglaNegocioException($"Fila {numero}: Tipo de día debe ser TRABAJO o DESCANSO.")
        };
        var fechaEspecifica = FechaOpcional(mapa.Valor(fila, "fecha especifica"), numero, "Fecha específica");
        var inicio = FechaOpcional(mapa.Valor(fila, "fecha inicio"), numero, "Fecha de inicio");
        var fin = FechaOpcional(mapa.Valor(fila, "fecha fin"), numero, "Fecha de fin");
        if (fechaEspecifica is null && (inicio is null || fin is null))
            throw new ReglaNegocioException($"Fila {numero}: indique una fecha específica o un intervalo completo.");
        if (fechaEspecifica is not null && (inicio is not null || fin is not null))
            throw new ReglaNegocioException($"Fila {numero}: no mezcle fecha específica e intervalo.");
        if (inicio is not null && fin < inicio)
            throw new ReglaNegocioException($"Fila {numero}: la fecha final es anterior a la inicial.");
        if (inicio is not null && fin!.Value.DayNumber - inicio.Value.DayNumber > 366)
            throw new ReglaNegocioException($"Fila {numero}: un intervalo no puede superar 367 días.");

        var codigo = VacioANulo(mapa.Valor(fila, "codigo turno"))?.ToUpperInvariant();
        var nombre = VacioANulo(mapa.Valor(fila, "nombre turno"));
        var horaInicio = HoraOpcional(mapa.Valor(fila, "hora entrada"), numero);
        var horaFin = HoraOpcional(mapa.Valor(fila, "hora salida"), numero);
        var refrigerio = EnteroOpcional(mapa.Valor(fila, "minutos refrigerio"), numero);
        var nocturno = horaInicio is not null && horaFin is not null && horaFin <= horaInicio;
        var siguiente = BooleanoOpcional(mapa.Valor(fila, "salida dia siguiente"), numero);
        if (siguiente is not null && horaInicio is not null && horaFin is not null && siguiente != nocturno)
            throw new ReglaNegocioException($"Fila {numero}: el indicador de salida al día siguiente contradice las horas.");
        if (tipo == TipoDiaProgramacion.Descanso && new object?[] { codigo, nombre, horaInicio, horaFin, refrigerio }.Any(v => v is not null))
            throw new ReglaNegocioException($"Fila {numero}: un descanso explícito no debe declarar turno.");
        if (tipo == TipoDiaProgramacion.Trabajo && string.IsNullOrWhiteSpace(codigo))
            throw new ReglaNegocioException($"Fila {numero}: el trabajo requiere código de turno.");

        return new EntradaProgramacion(numero,
            Obligatorio(mapa, fila, "tipo documento", numero), Obligatorio(mapa, fila, "numero documento", numero),
            Obligatorio(mapa, fila, "empresa", numero), Obligatorio(mapa, fila, "unidad", numero),
            inicio, fin, fechaEspecifica, codigo, nombre, horaInicio, horaFin, refrigerio, nocturno, tipo,
            VacioANulo(mapa.Valor(fila, "modalidad")) ?? "FIJO");
    }

    private static DateOnly? FechaOpcional(string? valor, int fila, string columna) => string.IsNullOrWhiteSpace(valor) ? null
        : DateOnly.TryParseExact(valor, FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha : throw new ReglaNegocioException(
                $"Fila {fila}, columna '{columna}': el valor '{valor}' no es una fecha válida. " +
                "Use yyyy-MM-dd o dd/MM/yyyy, sin hora.");
    private static TimeOnly? HoraOpcional(string? valor, int fila) => string.IsNullOrWhiteSpace(valor) ? null
        : TimeOnly.TryParseExact(valor, FormatosHora, CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora)
            ? hora : throw new ReglaNegocioException($"Fila {fila}: la hora '{valor}' no es válida.");
    private static int? EnteroOpcional(string? valor, int fila) => string.IsNullOrWhiteSpace(valor) ? null
        : int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero) && numero is >= 0 and <= 240
            ? numero : throw new ReglaNegocioException($"Fila {fila}: los minutos de refrigerio no son válidos.");
    private static bool? BooleanoOpcional(string? valor, int fila) => Normalizar(valor) switch
    {
        "" => null, "si" or "s" or "true" or "1" => true, "no" or "n" or "false" or "0" => false,
        _ => throw new ReglaNegocioException($"Fila {fila}: use SI o NO para salida al día siguiente.")
    };
    private static string Obligatorio(Encabezados mapa, string[] fila, string clave, int numero) =>
        VacioANulo(mapa.Valor(fila, clave)) ?? throw new ReglaNegocioException($"Fila {numero}: falta {clave}.");
    private static string? VacioANulo(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    private static string Normalizar(string? valor)
    {
        var texto = (valor ?? string.Empty).Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        return new string(texto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(System.Text.NormalizationForm.FormC).Replace('_', ' ');
    }

    internal sealed class Encabezados(Dictionary<string, int> indices)
    {
        public static readonly string[] Canonicos =
        [
            "Tipo de documento", "Número de documento", "Empresa", "Unidad", "Fecha de inicio", "Fecha de fin",
            "Fecha específica", "Código de turno", "Nombre del turno", "Hora de entrada", "Hora de salida",
            "Salida al día siguiente", "Minutos de refrigerio", "Tipo de día", "Modalidad", "Observaciones",
            "Referencia de aprobación"
        ];
        public static Encabezados Crear(string[] fila)
        {
            var mapa = fila.Select((v, i) => (Clave: Normalizar(v).Replace(" de ", " ").Replace(" del ", " "), i))
                .Where(x => x.Clave.Length > 0).GroupBy(x => x.Clave).ToDictionary(g => g.Key, g => g.First().i);
            foreach (var requerido in new[] { "tipo documento", "numero documento", "empresa", "unidad", "tipo dia" })
                if (!mapa.ContainsKey(requerido)) throw new ReglaNegocioException($"Falta la columna obligatoria '{requerido}'.");
            return new(mapa);
        }
        public string? Valor(string[] fila, string clave) => indices.TryGetValue(clave, out var i) && i < fila.Length ? fila[i] : null;
    }
}

#pragma warning restore IDE0011
