using System.Globalization;
using ClosedXML.Excel;
using ControlMarcacion.Application.Reportes;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Asistencia;

namespace ControlMarcacion.Infrastructure.Reportes;

/// <summary>
/// Exporta los reportes a Excel con ClosedXML.
/// </summary>
/// <remarks>
/// ClosedXML es MIT. No usar EPPlus: su licencia dejó de ser gratuita para uso comercial
/// desde la versión 5.
///
/// Toda exportación lleva el mismo encabezado de trazabilidad: periodo, filtros, quién y
/// cuándo la generó, y si el periodo seguía abierto. Sin eso, un número en un Excel no
/// tiene procedencia.
/// </remarks>
internal sealed class ExportadorReportesExcel : IExportadorReportesExcel
{
    /// <summary>
    /// Fila de los títulos de columna: justo debajo de las 7 del bloque de trazabilidad.
    /// </summary>
    private const int FilaEncabezadoDatos = 8;

    public byte[] ExportarResumenTareo(MetadatosReporte metadatos, IReadOnlyList<ResumenTareoDto> filas)
    {
        ArgumentNullException.ThrowIfNull(metadatos);
        ArgumentNullException.ThrowIfNull(filas);

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Resumen de tareo");

        EscribirEncabezado(
            hoja, metadatos, "Resumen de tareo mensual",
            "Este reporte entrega minutos, horas y conteos de días. La columna \"Bono nocturno (S/.)\" " +
            "es la única cifra en soles que calcula el sistema aquí (D.S. 007-2002-TR, art. 8); el resto " +
            "de la remuneración sigue a cargo de planilla.");

        string[] columnas =
        [
            "Trabajador", "ID marcador", "Empresa", "Planta", "Centro", "Centro de costo", "Puesto",
            "Días puntual", "Días tardanza", "Días falta", "Días ausencia justificada",
            "Días incompleto", "Días no laborable trabajado",
            "Horas ordinarias", "Horas extra 25%", "Horas extra 35%", "Horas nocturnas",
            "Horas de tardanza acumuladas", "Bono nocturno (S/.)", "Movilidad (S/.)"
        ];

        EscribirTitulosColumna(hoja, columnas);

        var fila = FilaEncabezadoDatos + 1;

        foreach (var r in filas)
        {
            var col = 1;
            hoja.Cell(fila, col++).Value = r.Trabajador;
            hoja.Cell(fila, col++).Value = r.IdMarcador ?? "—";
            hoja.Cell(fila, col++).Value = r.Empresa;
            hoja.Cell(fila, col++).Value = r.Planta;
            hoja.Cell(fila, col++).Value = r.Centro ?? "—";
            hoja.Cell(fila, col++).Value = r.CentroCosto ?? "—";
            hoja.Cell(fila, col++).Value = r.Puesto;
            hoja.Cell(fila, col++).Value = r.DiasPuntual;
            hoja.Cell(fila, col++).Value = r.DiasTardanza;
            hoja.Cell(fila, col++).Value = r.DiasFalta;
            hoja.Cell(fila, col++).Value = r.DiasAusenciaJustificada;
            hoja.Cell(fila, col++).Value = r.DiasIncompleto;
            hoja.Cell(fila, col++).Value = r.DiasNoLaborableTrabajado;
            hoja.Cell(fila, col++).Value = r.HorasOrdinarias;
            hoja.Cell(fila, col++).Value = r.HorasExtra25;
            hoja.Cell(fila, col++).Value = r.HorasExtra35;
            hoja.Cell(fila, col++).Value = r.HorasNocturnas;
            hoja.Cell(fila, col++).Value = r.HorasTardanza;
            hoja.Cell(fila, col).Value = r.MontoBonoNocturno;
            hoja.Cell(fila, col).Style.NumberFormat.Format = "#,##0.00";
            col++;
            hoja.Cell(fila, col).Value = r.MontoMovilidad;
            hoja.Cell(fila, col).Style.NumberFormat.Format = "#,##0.00";
            fila++;
        }

        if (filas.Count > 0)
        {
            EscribirTotales(hoja, fila, filas);
        }

        TerminarHojaDeDatos(hoja, columnas.Length, filas.Count);

        return Guardar(libro);
    }

    public byte[] ExportarExcepciones(MetadatosReporte metadatos, IReadOnlyList<ExcepcionDto> filas)
    {
        ArgumentNullException.ThrowIfNull(metadatos);
        ArgumentNullException.ThrowIfNull(filas);

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Excepciones");

        EscribirEncabezado(hoja, metadatos, "Excepciones del periodo");

        string[] columnas =
        [
            "Trabajador", "ID marcador", "Fecha", "Turno", "Entrada", "Salida", "Estado",
            "Minutos tardanza", "Minutos salida anticipada", "Observación"
        ];

        EscribirTitulosColumna(hoja, columnas);

        var fila = FilaEncabezadoDatos + 1;

        foreach (var e in filas)
        {
            var col = 1;
            hoja.Cell(fila, col++).Value = e.Trabajador;
            hoja.Cell(fila, col++).Value = e.IdMarcador ?? "—";
            hoja.Cell(fila, col++).Value = e.Fecha.ToDateTime(TimeOnly.MinValue);
            hoja.Cell(fila, col).Style.DateFormat.Format = "dd/MM/yyyy";
            col++;
            hoja.Cell(fila, col++).Value = e.Turno ?? "—";

            if (e.EntradaReal is { } entrada)
            {
                hoja.Cell(fila, col).Value = entrada;
                hoja.Cell(fila, col).Style.DateFormat.Format = "HH:mm";
            }
            else
            {
                hoja.Cell(fila, col).Value = "—";
            }

            col++;

            if (e.SalidaReal is { } salida)
            {
                hoja.Cell(fila, col).Value = salida;
                hoja.Cell(fila, col).Style.DateFormat.Format = "HH:mm";
            }
            else
            {
                hoja.Cell(fila, col).Value = "—";
            }

            col++;
            hoja.Cell(fila, col++).Value = NombreEstado(e.Estado);
            hoja.Cell(fila, col++).Value = e.MinutosTardanza;
            hoja.Cell(fila, col++).Value = e.MinutosSalidaAnticipada;
            hoja.Cell(fila, col).Value = e.Observacion ?? string.Empty;
            fila++;
        }

        TerminarHojaDeDatos(hoja, columnas.Length, filas.Count);
        hoja.Column(10).Width = Math.Min(hoja.Column(10).Width, 80);

        return Guardar(libro);
    }

    /// <summary>
    /// Deja una hoja de datos lista para trabajarla: autofiltro, títulos congelados y anchos.
    /// </summary>
    /// <remarks>
    /// El autofiltro cubre los títulos y las filas de datos, pero <b>no</b> la de totales:
    /// incluirla haría que Excel la tratara como un registro más y la escondiera al filtrar,
    /// o peor, la sumara al ordenar. Con esto quien recibe el archivo puede filtrar por
    /// planta o por estado sin rehacer la tabla a mano, que es lo primero que termina
    /// haciendo con cualquier exportación.
    /// </remarks>
    private static void TerminarHojaDeDatos(IXLWorksheet hoja, int columnas, int filasDeDatos)
    {
        if (filasDeDatos > 0)
        {
            hoja.Range(FilaEncabezadoDatos, 1, FilaEncabezadoDatos + filasDeDatos, columnas)
                .SetAutoFilter();
        }

        hoja.SheetView.FreezeRows(FilaEncabezadoDatos);
        hoja.Columns().AdjustToContents();
    }

    // ------------------------------------------------------------------
    // Tareo final: el libro de tres hojas que GCA entrega a planilla
    // ------------------------------------------------------------------

    public byte[] ExportarTareoFinal(MetadatosReporte metadatos, TareoFinalDto tareo)
    {
        ArgumentNullException.ThrowIfNull(metadatos);
        ArgumentNullException.ThrowIfNull(tareo);

        using var libro = new XLWorkbook();

        EscribirHojaTareo(libro.Worksheets.Add("Tareo"), tareo);
        EscribirHojaDescansoMedico(libro.Worksheets.Add("Descanso Medico"), tareo);
        EscribirHojaVacacionesLicencias(libro.Worksheets.Add("Vacaciones-Licencias"), tareo);
        EscribirHojaTrazabilidad(libro.Worksheets.Add("Trazabilidad"), metadatos, tareo);

        return Guardar(libro);
    }

    /// <summary>
    /// Cuarta hoja del libro: de dónde salió este tareo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Va en hoja aparte y <b>al final</b> por una razón concreta: las tres primeras
    /// reproducen el archivo modelo de GCA celda por celda y se pegan en la plantilla que
    /// planilla ya tiene armada. Meter dos filas de procedencia arriba de <c>Tareo</c>
    /// correría todo el bloque y rompería esa plantilla; dejarlas fuera del archivo, en
    /// cambio, dejaba el entregable más importante del sistema sin decir quién lo generó,
    /// cuándo, con qué recorte ni sobre un tareo calculado en qué momento.
    /// </para>
    /// <para>
    /// Una hoja añadida al final es aditiva: quien copia las tres de siempre no se entera,
    /// y quien tiene que auditar el archivo seis meses después encuentra la respuesta dentro
    /// del propio archivo en vez de tener que reconstruirla.
    /// </para>
    /// </remarks>
    private static void EscribirHojaTrazabilidad(
        IXLWorksheet hoja, MetadatosReporte metadatos, TareoFinalDto tareo)
    {
        hoja.Cell("A1").Value = "Procedencia de este tareo";
        hoja.Cell("A1").Style.Font.Bold = true;
        hoja.Cell("A1").Style.Font.FontSize = 14;

        (string Etiqueta, string Valor)[] datos =
        [
            ("Razón social", tareo.RazonSocial),
            ("Alcance", metadatos.Alcance),
            ("Desde", metadatos.FechaInicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
            ("Hasta", metadatos.FechaFin.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
            ("Periodo", metadatos.PeriodoCodigo is null
                ? "—"
                : $"{metadatos.PeriodoCodigo} — {metadatos.PeriodoNombre}"),
            ("Estado del periodo", metadatos.PeriodoAbierto switch
            {
                true => "ABIERTO — el tareo puede volver a recalcularse",
                false => "CERRADO — el tareo está congelado",
                null => "—"
            }),
            ("Filtros aplicados", metadatos.FiltrosDescripcion),
            ("Trabajadores en el libro", tareo.Tareo.Count.ToString(CultureInfo.InvariantCulture)),
            ("Generado el", metadatos.GeneradoEn.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)),
            ("Generado por", metadatos.GeneradoPor)
        ];

        var fila = 3;

        foreach (var (etiqueta, valor) in datos)
        {
            hoja.Cell(fila, 1).Value = etiqueta;
            hoja.Cell(fila, 1).Style.Font.Bold = true;
            hoja.Cell(fila, 2).Value = valor;
            fila++;
        }

        fila++;
        hoja.Cell(fila, 1).Value = "Estado del cálculo";
        hoja.Cell(fila, 1).Style.Font.Bold = true;
        EscribirEstadoDelCalculo(hoja.Cell(fila + 1, 1), metadatos.Calculo);

        hoja.Columns(1, 2).AdjustToContents();
        hoja.Column(2).Width = Math.Min(hoja.Column(2).Width, 90);
    }

    /// <summary>Fila donde arrancan los datos en la hoja <c>Tareo</c>, como en el modelo.</summary>
    private const int FilaDatosTareo = 7;

    /// <summary>
    /// Reproduce la hoja <c>Tareo</c> del archivo modelo: 57 columnas (A:BE) con títulos en
    /// tres niveles y los datos desde la fila 7.
    /// </summary>
    /// <remarks>
    /// CENTRO / CENTRO DE COSTO / DESCRIP. CECO salen del catálogo real de SAP que entregó
    /// GCA, y quedan <b>vacías</b> si el trabajador no tiene centro de costo asignado: no se
    /// aproximan con Área/Sección, porque eso produciría un código que en SAP no existe.
    /// Movilidad, asignación familiar y los 18 campos de reintegros ya se alimentan.
    ///
    /// El bloque manual <c>Ausencias</c>/<c>Tardanza</c>/<c>Dias Efectivas</c>/
    /// <c>Dias Pendientes</c>/<c>Dias Trabajados</c> se emite <b>vacío pero presente</b>, en
    /// su posición exacta. En el archivo de GCA también viene vacío: lo carga planilla a
    /// mano. Quitar la columna obligaría a rehacer su proceso; dejarla en cero mentiría
    /// sobre un dato que nadie calculó.
    /// </remarks>
    private static void EscribirHojaTareo(IXLWorksheet hoja, TareoFinalDto tareo)
    {
        hoja.Cell("A1").Value = tareo.RazonSocial;
        hoja.Cell("A1").Style.Font.Bold = true;
        hoja.Cell("A1").Style.Font.FontSize = 12;

        hoja.Cell("A2").Value = "TAREO MENSUAL (OPERARIOS)";
        hoja.Cell("A2").Style.Font.Bold = true;

        hoja.Cell("A3").Value = $"MES: {tareo.NombreMes}";
        hoja.Cell("AF3").Value = "DEL";
        EscribirFecha(hoja.Cell("AG3"), tareo.FechaInicio);
        hoja.Cell("AI3").Value = "AL";
        EscribirFecha(hoja.Cell("AJ3"), tareo.FechaFin);

        // --- Fila 4: identificación y títulos de grupo ---
        // Bloque de identificación según el formato vigente de GCA ("RESUMEN FIN DE MES ... -
        // PLANTA ..."): CENTRO / CENTRO DE COSTO / DESCRIP. CECO salen las tres de la misma
        // fila del catálogo real de SAP (CO_AREA / CO_SECC / DE_SECC), y AREA es el área
        // interna del operario, que no se deriva del CECO. El formato anterior tenía además
        // "Texto Centro" y "C. Costo"; ninguna de las dos existe ya, así que el resto de la
        // hoja corre una columna a la izquierda y el ancho total pasa de 58 a 57.
        string[] identificacion =
        [
            "N°", "DNI", "NOMBRE", "PLANTA", "CENTRO", "CENTRO DE COSTO", "DESCRIP. CECO",
            "AREA", "FECHA INGRESO", "FECHA CESE", "OBS"
        ];

        for (var i = 0; i < identificacion.Length; i++)
        {
            hoja.Cell(4, i + 1).Value = identificacion[i];
            hoja.Range(4, i + 1, 6, i + 1).Merge();
        }

        Grupo(hoja, "L4:X4", "REPORTE DE MOVIMIENTO DE PERSONAL EN DIAS");

        // Columnas de llenado manual: vienen vacías también en el archivo de GCA.
        string[] manuales = ["Ausencias", "Tardanza", "Dias Efectivas", "Dias Pendientes", "Dias Trabajados"];

        for (var i = 0; i < manuales.Length; i++)
        {
            hoja.Cell(4, 25 + i).Value = manuales[i];
            hoja.Range(4, 25 + i, 6, 25 + i).Merge();
        }

        Grupo(hoja, "AD4:AL4", "R E S U M E N");
        Grupo(hoja, "AN4:BE4", "REINTEGROS");

        hoja.Cell("AM4").Value = "ASIG. FAMILIAR";
        hoja.Range("AM4:AM6").Merge();

        // --- Fila 5: subgrupos horario ---
        Grupo(hoja, "AD5:AG5", "HORARIO DIURNO");
        Grupo(hoja, "AI5:AL5", "HORARIO NOCTURNO");
        Grupo(hoja, "AN5:AQ5", "HORARIO DIURNO");
        Grupo(hoja, "AS5:AV5", "HORARIO NOCTURNO");

        hoja.Cell("AH5").Value = "MOVIL S/.";
        hoja.Range("AH5:AH6").Merge();
        hoja.Cell("AR5").Value = "MOVIL S/.";
        hoja.Range("AR5:AR6").Merge();

        // --- Fila 6: títulos de hoja ---
        string[] enDias =
        [
            "FALTAS", "SUSPENSION", "FALTAS JUS.", "TOTAL FALTAS", "DM", "VACACI.",
            "LIC. S/ GOC", "LIC. C/ GOC", "LIC. PAT.", "SUB. MAT", "SUB. ENF", "PERMISOS",
            "TARDANZA"
        ];

        for (var i = 0; i < enDias.Length; i++)
        {
            hoja.Cell(6, 12 + i).Value = enDias[i];
        }

        // Los tramos van como número con formato de porcentaje, igual que en el modelo.
        EscribirTramos(hoja, columnaBase: 30, titulo: "HRS NORMALES");   // AD
        EscribirTramos(hoja, columnaBase: 35, titulo: "B.NOCT. (HORAS)"); // AI
        EscribirTramos(hoja, columnaBase: 40, titulo: "HRS NORMALES");   // AN
        EscribirTramos(hoja, columnaBase: 45, titulo: "B.NOCT. (HORAS)"); // AS

        string[] conceptosReintegro =
        [
            "VIATICO", "D.M. (DIAS)", "SUB. MATERNIDAD (DIAS)", "SUB. X ACCIDENTE (DIAS)",
            "LIC. PATER. (DIAS)", "LICENCIA CON GOCE", "REINT. ASIG. FAMILI", "REINT. AFECTO",
            "DESC. PAGO INDEBIDO"
        ];

        for (var i = 0; i < conceptosReintegro.Length; i++)
        {
            hoja.Cell(6, 49 + i).Value = conceptosReintegro[i];
        }

        EstilarTitulos(hoja.Range(4, 1, 6, 57));

        // --- Datos ---
        var fila = FilaDatosTareo;

        foreach (var f in tareo.Tareo)
        {
            hoja.Cell(fila, 1).Value = f.Numero;

            // El DNI va como texto: si Excel lo toma por número, un documento que empieza
            // en cero pierde el dígito y deja de cruzar con la planilla.
            hoja.Cell(fila, 2).Value = f.Dni;
            hoja.Cell(fila, 2).Style.NumberFormat.Format = "@";

            hoja.Cell(fila, 3).Value = f.ApellidosYNombres;
            hoja.Cell(fila, 4).Value = f.Planta;

            // CENTRO / CENTRO DE COSTO / DESCRIP. CECO: las tres del catálogo real de SAP
            // (CO_AREA / CO_SECC / DE_SECC), siempre de la misma fila. AREA es el área
            // interna del operario. Si el trabajador todavía no tiene centro de costo
            // asignado en Personal, las tres de CECO salen vacías: no se aproximan con
            // Área/Sección, porque eso daría un código que no existe en SAP.
            hoja.Cell(fila, 5).Value = f.Centro ?? string.Empty;
            hoja.Cell(fila, 6).Value = f.CentroCosto ?? string.Empty;
            hoja.Cell(fila, 7).Value = f.DescripcionCeco ?? string.Empty;
            hoja.Cell(fila, 8).Value = f.Area ?? string.Empty;
            EscribirFecha(hoja.Cell(fila, 9), f.FechaIngreso);

            if (f.FechaCese is { } cese)
            {
                EscribirFecha(hoja.Cell(fila, 10), cese);
                hoja.Cell(fila, 10).Style.Font.FontColor = XLColor.FromHtml("#c0392b");
            }

            hoja.Cell(fila, 11).Value = f.Observacion ?? string.Empty;

            int[] enDiasValores =
            [
                f.DiasFalta, f.DiasSuspension, f.DiasFaltaJustificada, f.DiasTotalFaltas,
                f.DiasDescansoMedico, f.DiasVacaciones, f.DiasLicenciaSinGoce,
                f.DiasLicenciaConGoce, f.DiasLicenciaPaternidad, f.DiasSubsidioMaternidad,
                f.DiasSubsidioEnfermedad, f.DiasPermiso
            ];

            for (var i = 0; i < enDiasValores.Length; i++)
            {
                EscribirNumero(hoja.Cell(fila, 12 + i), enDiasValores[i], "#,##0.00;;\"-\"");
            }

            EscribirNumero(hoja.Cell(fila, 24), f.HorasTardanza, "#,##0.00;;\"-\"");

            decimal[] resumen =
            [
                f.HorasOrdinarias, f.HorasExtra25, f.HorasExtra35, f.HorasNoLaborable
            ];

            for (var i = 0; i < resumen.Length; i++)
            {
                EscribirNumero(hoja.Cell(fila, 30 + i), resumen[i], "#,##0.00;;\"-\"");
            }

            // AH: movilidad calculada (días asistidos × valor diario vigente en el periodo).
            EscribirNumero(hoja.Cell(fila, 34), f.MontoMovilidad, "#,##0.00;;\"-\"");

            decimal[] nocturno =
            [
                f.HorasNocturnas, f.HorasExtra25Nocturnas, f.HorasExtra35Nocturnas,
                f.HorasNoLaborableNocturnas
            ];

            for (var i = 0; i < nocturno.Length; i++)
            {
                EscribirNumero(hoja.Cell(fila, 35 + i), nocturno[i], "#,##0.00;;\"-\"");
            }

            // AM: asignación familiar (10 % de la RMV, fija si el trabajador tiene hijos).
            EscribirNumero(hoja.Cell(fila, 39), f.MontoAsignacionFamiliar, "#,##0.00;;\"-\"");

            // AN-AQ: reintegro de horas normales/25 %/35 %/100 %, cargado a mano.
            decimal[] reintegroDiurno =
            [
                f.ReintegroHorasNormales, f.ReintegroHorasExtra25, f.ReintegroHorasExtra35,
                f.ReintegroHorasNoLaborable
            ];

            for (var i = 0; i < reintegroDiurno.Length; i++)
            {
                EscribirNumero(hoja.Cell(fila, 40 + i), reintegroDiurno[i], "#,##0.00;;\"-\"");
            }

            // AR: reintegro de movilidad.
            EscribirNumero(hoja.Cell(fila, 44), f.ReintegroMontoMovilidad, "#,##0.00;;\"-\"");

            // AS-AV: reintegro de horas nocturnas/25 %/35 %/100 %.
            decimal[] reintegroNocturno =
            [
                f.ReintegroHorasNocturnas, f.ReintegroHorasExtra25Nocturnas,
                f.ReintegroHorasExtra35Nocturnas, f.ReintegroHorasNoLaborableNocturnas
            ];

            for (var i = 0; i < reintegroNocturno.Length; i++)
            {
                EscribirNumero(hoja.Cell(fila, 45 + i), reintegroNocturno[i], "#,##0.00;;\"-\"");
            }

            // AW-BE: viático, días de DM/subsidios/licencia paternal, licencia con goce y los
            // tres reintegros/descuentos nombrados. Cargados a mano en ReintegroTareo; cero
            // se ve como "-", igual que el resto de la hoja.
            decimal[] conceptosReintegroValores =
            [
                f.ReintegroViatico, f.ReintegroDiasDescansoMedico, f.ReintegroDiasSubsidioMaternidad,
                f.ReintegroDiasSubsidioAccidente, f.ReintegroDiasLicenciaPaternidad,
                f.ReintegroLicenciaConGoce, f.ReintegroAsignacionFamiliar, f.ReintegroAfecto,
                f.ReintegroDescuentoPagoIndebido
            ];

            for (var i = 0; i < conceptosReintegroValores.Length; i++)
            {
                EscribirNumero(hoja.Cell(fila, 49 + i), conceptosReintegroValores[i], "#,##0.00;;\"-\"");
            }

            fila++;
        }

        if (tareo.Tareo.Count > 0)
        {
            EscribirTotalesTareo(hoja, fila, tareo.Tareo);
        }

        hoja.SheetView.FreezeRows(6);
        hoja.SheetView.FreezeColumns(3);
        hoja.Columns(1, 57).AdjustToContents();
        hoja.Column(3).Width = Math.Min(hoja.Column(3).Width, 38);
    }

    /// <summary>Escribe un bloque de cuatro columnas: base, 25 %, 35 % y 100 %.</summary>
    private static void EscribirTramos(IXLWorksheet hoja, int columnaBase, string titulo)
    {
        hoja.Cell(6, columnaBase).Value = titulo;

        decimal[] tasas = [0.25m, 0.35m, 1m];

        for (var i = 0; i < tasas.Length; i++)
        {
            var celda = hoja.Cell(6, columnaBase + 1 + i);
            celda.Value = tasas[i];
            celda.Style.NumberFormat.Format = "0%";
        }
    }

    private static void EscribirTotalesTareo(
        IXLWorksheet hoja, int fila, IReadOnlyList<FilaTareoFinalDto> filas)
    {
        hoja.Cell(fila, 1).Value = "TOTAL";

        decimal[] totales =
        [
            filas.Sum(f => f.DiasFalta), filas.Sum(f => f.DiasSuspension),
            filas.Sum(f => f.DiasFaltaJustificada), filas.Sum(f => f.DiasTotalFaltas),
            filas.Sum(f => f.DiasDescansoMedico), filas.Sum(f => f.DiasVacaciones),
            filas.Sum(f => f.DiasLicenciaSinGoce), filas.Sum(f => f.DiasLicenciaConGoce),
            filas.Sum(f => f.DiasLicenciaPaternidad), filas.Sum(f => f.DiasSubsidioMaternidad),
            filas.Sum(f => f.DiasSubsidioEnfermedad), filas.Sum(f => f.DiasPermiso),
            filas.Sum(f => f.HorasTardanza)
        ];

        for (var i = 0; i < totales.Length; i++)
        {
            EscribirNumero(hoja.Cell(fila, 12 + i), totales[i], "#,##0.00;;\"-\"");
        }

        decimal[] horas =
        [
            filas.Sum(f => f.HorasOrdinarias), filas.Sum(f => f.HorasExtra25),
            filas.Sum(f => f.HorasExtra35), filas.Sum(f => f.HorasNoLaborable)
        ];

        for (var i = 0; i < horas.Length; i++)
        {
            EscribirNumero(hoja.Cell(fila, 30 + i), horas[i], "#,##0.00;;\"-\"");
        }

        EscribirNumero(hoja.Cell(fila, 34), filas.Sum(f => f.MontoMovilidad), "#,##0.00;;\"-\"");

        decimal[] nocturnas =
        [
            filas.Sum(f => f.HorasNocturnas), filas.Sum(f => f.HorasExtra25Nocturnas),
            filas.Sum(f => f.HorasExtra35Nocturnas), filas.Sum(f => f.HorasNoLaborableNocturnas)
        ];

        for (var i = 0; i < nocturnas.Length; i++)
        {
            EscribirNumero(hoja.Cell(fila, 35 + i), nocturnas[i], "#,##0.00;;\"-\"");
        }

        EscribirNumero(hoja.Cell(fila, 39), filas.Sum(f => f.MontoAsignacionFamiliar), "#,##0.00;;\"-\"");

        decimal[] reintegroDiurno =
        [
            filas.Sum(f => f.ReintegroHorasNormales), filas.Sum(f => f.ReintegroHorasExtra25),
            filas.Sum(f => f.ReintegroHorasExtra35), filas.Sum(f => f.ReintegroHorasNoLaborable)
        ];

        for (var i = 0; i < reintegroDiurno.Length; i++)
        {
            EscribirNumero(hoja.Cell(fila, 40 + i), reintegroDiurno[i], "#,##0.00;;\"-\"");
        }

        EscribirNumero(hoja.Cell(fila, 44), filas.Sum(f => f.ReintegroMontoMovilidad), "#,##0.00;;\"-\"");

        decimal[] reintegroNocturno =
        [
            filas.Sum(f => f.ReintegroHorasNocturnas), filas.Sum(f => f.ReintegroHorasExtra25Nocturnas),
            filas.Sum(f => f.ReintegroHorasExtra35Nocturnas), filas.Sum(f => f.ReintegroHorasNoLaborableNocturnas)
        ];

        for (var i = 0; i < reintegroNocturno.Length; i++)
        {
            EscribirNumero(hoja.Cell(fila, 45 + i), reintegroNocturno[i], "#,##0.00;;\"-\"");
        }

        decimal[] conceptosReintegroTotales =
        [
            filas.Sum(f => f.ReintegroViatico), filas.Sum(f => f.ReintegroDiasDescansoMedico),
            filas.Sum(f => f.ReintegroDiasSubsidioMaternidad), filas.Sum(f => f.ReintegroDiasSubsidioAccidente),
            filas.Sum(f => f.ReintegroDiasLicenciaPaternidad), filas.Sum(f => f.ReintegroLicenciaConGoce),
            filas.Sum(f => f.ReintegroAsignacionFamiliar), filas.Sum(f => f.ReintegroAfecto),
            filas.Sum(f => f.ReintegroDescuentoPagoIndebido)
        ];

        for (var i = 0; i < conceptosReintegroTotales.Length; i++)
        {
            EscribirNumero(hoja.Cell(fila, 49 + i), conceptosReintegroTotales[i], "#,##0.00;;\"-\"");
        }

        var rango = hoja.Range(fila, 1, fila, 57);
        rango.Style.Font.Bold = true;
        rango.Style.Border.TopBorder = XLBorderStyleValues.Double;
    }

    private static void EscribirHojaDescansoMedico(IXLWorksheet hoja, TareoFinalDto tareo)
    {
        string[] columnas =
        [
            "MES", "RAZÓN SOCIAL", "EMPRESA", "DNI / CE", "APELLIDOS Y NOMBRES",
            "FECHA INICIO", "FECHA FIN", "DIAS EN EL MES", "CONTINGENCIA", "DIAGNÓSTICO",
            "CENTRO DE ATENCIÓN", "OBSERVACION"
        ];

        for (var i = 0; i < columnas.Length; i++)
        {
            hoja.Cell(4, i + 1).Value = columnas[i];
        }

        EstilarTitulos(hoja.Range(4, 1, 4, columnas.Length));

        var fila = 5;

        foreach (var d in tareo.DescansosMedicos)
        {
            hoja.Cell(fila, 1).Value = d.Mes;
            hoja.Cell(fila, 2).Value = d.RazonSocial;
            hoja.Cell(fila, 3).Value = d.Empresa;
            hoja.Cell(fila, 4).Value = d.Dni;
            hoja.Cell(fila, 4).Style.NumberFormat.Format = "@";
            hoja.Cell(fila, 5).Value = d.ApellidosYNombres;
            EscribirFecha(hoja.Cell(fila, 6), d.FechaInicio);
            EscribirFecha(hoja.Cell(fila, 7), d.FechaFin);
            hoja.Cell(fila, 8).Value = d.DiasEnElMes;
            hoja.Cell(fila, 9).Value = d.Contingencia ?? string.Empty;
            hoja.Cell(fila, 10).Value = d.Diagnostico ?? string.Empty;
            hoja.Cell(fila, 11).Value = d.CentroAtencion ?? string.Empty;
            hoja.Cell(fila, 12).Value = d.Observacion;
            fila++;
        }

        hoja.Columns(1, columnas.Length).AdjustToContents();
    }

    /// <summary>
    /// Hoja <c>Vacaciones-Licencias</c>: dos tablas apiladas, como en el modelo.
    /// </summary>
    /// <remarks>
    /// El bloque de licencias arranca dos filas después del último registro de vacaciones,
    /// no en una fila fija: en el modelo está en la 11 porque ahí había 7 vacaciones.
    /// </remarks>
    private static void EscribirHojaVacacionesLicencias(IXLWorksheet hoja, TareoFinalDto tareo)
    {
        hoja.Cell("A1").Value = "VACACIONES";
        hoja.Cell("A1").Style.Font.Bold = true;

        // Los títulos de las dos columnas de reparto son el nombre del mes, así que cambian
        // en cada emisión: el descanso que cruza el fin de mes se parte entre dos planillas.
        var mesSiguiente = tareo.FechaInicio.AddMonths(1);
        var cultura = CultureInfo.GetCultureInfo("es-PE");

        string[] columnasVacaciones =
        [
            "N°", "DNI", "NOMBRE", "DIAS", "FEC_INICIO", "FEC_TERMINO",
            cultura.TextInfo.ToTitleCase(cultura.DateTimeFormat.GetMonthName(tareo.FechaInicio.Month)),
            cultura.TextInfo.ToTitleCase(cultura.DateTimeFormat.GetMonthName(mesSiguiente.Month)),
            "RETORNO"
        ];

        for (var i = 0; i < columnasVacaciones.Length; i++)
        {
            hoja.Cell(2, i + 1).Value = columnasVacaciones[i];
        }

        EstilarTitulos(hoja.Range(2, 1, 2, columnasVacaciones.Length));

        var fila = 3;

        foreach (var v in tareo.Vacaciones)
        {
            hoja.Cell(fila, 1).Value = v.Numero;
            hoja.Cell(fila, 2).Value = v.Dni;
            hoja.Cell(fila, 2).Style.NumberFormat.Format = "@";
            hoja.Cell(fila, 3).Value = v.Nombre;
            hoja.Cell(fila, 4).Value = v.Dias;
            EscribirFecha(hoja.Cell(fila, 5), v.FechaInicio);
            EscribirFecha(hoja.Cell(fila, 6), v.FechaTermino);
            hoja.Cell(fila, 7).Value = v.DiasMesPeriodo;
            hoja.Cell(fila, 8).Value = v.DiasMesSiguiente;
            EscribirFecha(hoja.Cell(fila, 9), v.FechaRetorno);
            fila++;
        }

        var filaLicencias = fila + 1;

        hoja.Cell(filaLicencias, 1).Value = "LICENCIAS";
        hoja.Cell(filaLicencias, 1).Style.Font.Bold = true;

        string[] columnasLicencias =
        [
            "MES", "DNI / CE", "APELLIDOS Y NOMBRES", "FECHA INICIO", "FECHA FIN",
            "DIAS EN EL MES", "CONTINGENCIA", "DIAGNÓSTICO", "CENTRO DE ATENCIÓN",
            "OBSERVACION"
        ];

        for (var i = 0; i < columnasLicencias.Length; i++)
        {
            hoja.Cell(filaLicencias + 1, i + 1).Value = columnasLicencias[i];
        }

        EstilarTitulos(hoja.Range(filaLicencias + 1, 1, filaLicencias + 1, columnasLicencias.Length));

        fila = filaLicencias + 2;

        foreach (var l in tareo.Licencias)
        {
            hoja.Cell(fila, 1).Value = l.Mes;
            hoja.Cell(fila, 2).Value = l.Dni;
            hoja.Cell(fila, 2).Style.NumberFormat.Format = "@";
            hoja.Cell(fila, 3).Value = l.ApellidosYNombres;
            EscribirFecha(hoja.Cell(fila, 4), l.FechaInicio);
            EscribirFecha(hoja.Cell(fila, 5), l.FechaFin);
            hoja.Cell(fila, 6).Value = l.DiasEnElMes;
            hoja.Cell(fila, 7).Value = l.Contingencia;
            hoja.Cell(fila, 8).Value = l.Diagnostico ?? string.Empty;
            hoja.Cell(fila, 9).Value = l.CentroAtencion ?? string.Empty;
            hoja.Cell(fila, 10).Value = l.Observacion ?? string.Empty;
            fila++;
        }

        hoja.Columns(1, columnasLicencias.Length).AdjustToContents();
    }

    // ------------------------------------------------------------------

    private static void Grupo(IXLWorksheet hoja, string rango, string titulo)
    {
        var celdas = hoja.Range(rango);
        celdas.Merge();
        celdas.FirstCell().Value = titulo;
        celdas.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void EstilarTitulos(IXLRange rango)
    {
        rango.Style.Font.Bold = true;
        rango.Style.Font.FontSize = 8;
        rango.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        rango.Style.Alignment.WrapText = true;
        rango.Style.Fill.BackgroundColor = XLColor.FromHtml("#fde9a9");
        rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        rango.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void EscribirFecha(IXLCell celda, DateOnly fecha)
    {
        celda.Value = fecha.ToDateTime(TimeOnly.MinValue);
        celda.Style.DateFormat.Format = "dd/MM/yyyy";
    }

    private static void EscribirNumero(IXLCell celda, decimal valor, string formato)
    {
        celda.Value = valor;
        celda.Style.NumberFormat.Format = formato;
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// El bloque de trazabilidad que encabeza toda exportación.
    /// </summary>
    /// <remarks>
    /// Seis filas fijas, siempre las mismas y en el mismo orden, porque el archivo circula
    /// fuera del sistema: quien lo abre tres meses después tiene que poder responder de qué
    /// fechas es, con qué recorte salió, quién lo sacó y —lo que faltaba— <b>si el tareo
    /// del que salió estaba al día</b>. Ese aviso va aquí y no solo en pantalla justamente
    /// porque el Excel se reenvía por correo, donde ya no hay pantalla.
    ///
    /// Cuando el alcance no es un periodo (un mes, un día, un rango libre) la fila de estado
    /// dice eso mismo en vez de inventar un abierto/cerrado que no aplica.
    /// </remarks>
    private static void EscribirEncabezado(
        IXLWorksheet hoja,
        MetadatosReporte metadatos,
        string titulo,
        string notaDinero = "Este reporte entrega minutos, horas y conteos de días — no calcula remuneración.")
    {
        hoja.Cell(1, 1).Value = titulo;
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;

        hoja.Cell(2, 1).Value = $"Alcance: {metadatos.Alcance} " +
                                $"({metadatos.FechaInicio:dd/MM/yyyy} al {metadatos.FechaFin:dd/MM/yyyy})";

        hoja.Cell(3, 1).Value = metadatos.PeriodoAbierto switch
        {
            true => $"Periodo {metadatos.PeriodoCodigo} ABIERTO — el tareo puede volver a recalcularse.",
            false => $"Periodo {metadatos.PeriodoCodigo} CERRADO — el tareo está congelado.",
            null => "Alcance por fechas: no corresponde a un periodo de planilla, "
                    + "así que no tiene estado de apertura."
        };

        hoja.Cell(4, 1).Value = $"Filtros: {metadatos.FiltrosDescripcion}";
        hoja.Cell(5, 1).Value = $"Generado el {metadatos.GeneradoEn:dd/MM/yyyy HH:mm} por {metadatos.GeneradoPor}";

        EscribirEstadoDelCalculo(hoja.Cell(6, 1), metadatos.Calculo);

        // El sistema no calcula la remuneración completa: lo dice en el propio Excel, para
        // que quien lo abra en planilla no asuma que las horas equivalentes ya son un monto.
        // El Resumen mensual pasa una nota distinta porque ahí sí hay una columna en soles.
        hoja.Cell(7, 1).Value = notaDinero;
        hoja.Cell(7, 1).Style.Font.Italic = true;
        hoja.Cell(7, 1).Style.Font.FontColor = XLColor.FromHtml("#64748b");
    }

    /// <summary>
    /// La fila que dice de qué momento son las cifras.
    /// </summary>
    /// <remarks>
    /// Un tareo desactualizado se ve idéntico a uno al día: mismos encabezados, mismas
    /// columnas, cifras viejas. Por eso el aviso se pinta en rojo y en negrita en vez de
    /// quedarse en el gris del resto del encabezado — es la única señal de que el archivo no
    /// se puede entregar todavía.
    /// </remarks>
    private static void EscribirEstadoDelCalculo(IXLCell celda, EstadoCalculoTareo calculo)
    {
        if (calculo.Advertencia is { } advertencia)
        {
            celda.Value = advertencia;
            celda.Style.Font.Bold = true;
            celda.Style.Font.FontColor = XLColor.FromHtml("#c0392b");

            return;
        }

        celda.Value = $"Tareo al día: calculado el {calculo.CalculadoMasReciente:dd/MM/yyyy HH:mm}, "
                      + "sin cargas ni justificaciones posteriores.";
        celda.Style.Font.FontColor = XLColor.FromHtml("#64748b");
    }

    private static void EscribirTitulosColumna(IXLWorksheet hoja, string[] columnas)
    {
        for (var i = 0; i < columnas.Length; i++)
        {
            var celda = hoja.Cell(FilaEncabezadoDatos, i + 1);
            celda.Value = columnas[i];
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
            celda.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }
    }

    private static void EscribirTotales(IXLWorksheet hoja, int fila, IReadOnlyList<ResumenTareoDto> filas)
    {
        hoja.Cell(fila, 1).Value = "TOTAL";
        hoja.Cell(fila, 1).Style.Font.Bold = true;

        hoja.Cell(fila, 8).Value = filas.Sum(r => r.DiasPuntual);
        hoja.Cell(fila, 9).Value = filas.Sum(r => r.DiasTardanza);
        hoja.Cell(fila, 10).Value = filas.Sum(r => r.DiasFalta);
        hoja.Cell(fila, 11).Value = filas.Sum(r => r.DiasAusenciaJustificada);
        hoja.Cell(fila, 12).Value = filas.Sum(r => r.DiasIncompleto);
        hoja.Cell(fila, 13).Value = filas.Sum(r => r.DiasNoLaborableTrabajado);
        hoja.Cell(fila, 14).Value = filas.Sum(r => r.HorasOrdinarias);
        hoja.Cell(fila, 15).Value = filas.Sum(r => r.HorasExtra25);
        hoja.Cell(fila, 16).Value = filas.Sum(r => r.HorasExtra35);
        hoja.Cell(fila, 17).Value = filas.Sum(r => r.HorasNocturnas);
        hoja.Cell(fila, 18).Value = filas.Sum(r => r.HorasTardanza);
        hoja.Cell(fila, 19).Value = filas.Sum(r => r.MontoBonoNocturno);
        hoja.Cell(fila, 19).Style.NumberFormat.Format = "#,##0.00";
        hoja.Cell(fila, 20).Value = filas.Sum(r => r.MontoMovilidad);
        hoja.Cell(fila, 20).Style.NumberFormat.Format = "#,##0.00";

        var rango = hoja.Range(fila, 1, fila, 20);
        rango.Style.Font.Bold = true;
        rango.Style.Border.TopBorder = XLBorderStyleValues.Thin;
    }

    private static string NombreEstado(EstadoTareo estado) => estado switch
    {
        EstadoTareo.Falta => "Falta",
        EstadoTareo.Tardanza => "Tardanza",
        EstadoTareo.AusenciaJustificada => "Ausencia justificada",
        EstadoTareo.Incompleto => "Marcación incompleta",
        _ => estado.ToString()
    };

    private static byte[] Guardar(XLWorkbook libro)
    {
        using var flujo = new MemoryStream();
        libro.SaveAs(flujo);
        return flujo.ToArray();
    }
}
