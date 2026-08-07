using System.Globalization;
using System.Text;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Personal;

/// <summary>
/// Alta masiva de trabajadores desde CSV.
/// </summary>
/// <remarks>
/// Resuelve los catálogos por su código o su nombre — el usuario escribe "DNI",
/// "Detergentes" u "OPER", no claves numéricas que no conoce — arma la misma solicitud
/// que envía el formulario y llama a <see cref="ServicioTrabajadores"/>. Las reglas del
/// alta viven en un solo sitio: si mañana cambia una validación, cambia para las dos
/// puertas de entrada.
///
/// Todo corre dentro de una transacción. En modo validación se deshace al final, así que
/// la revisión previa es exacta y no una aproximación: detecta hasta los documentos
/// repetidos <b>dentro del propio archivo</b>, porque la fila 40 se valida contra una
/// base donde la fila 12 ya está insertada.
/// </remarks>
internal sealed class CargaMasivaTrabajadores(
    MarcacionDbContext contexto,
    ServicioTrabajadores servicio) : ICargaMasivaTrabajadores
{
    /// <summary>
    /// Tope de filas por archivo. El padrón completo de Intradevco no llega a mil; un
    /// archivo de cien mil es un error de quien lo generó, no una carga legítima, y
    /// procesarlo dejaría la pantalla colgada media hora.
    /// </summary>
    private const int MaximoFilas = 5000;

    private static readonly string[] FormatosFecha =
        ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy"];

    public async Task<ResultadoCargaMasiva> ProcesarAsync(
        Stream contenido,
        string nombreArchivo,
        string usuario,
        bool soloValidar,
        CancellationToken cancelacion = default)
    {
        var filas = LectorCsv.Leer(contenido);

        if (filas.Count == 0)
        {
            throw new ReglaNegocioException("El archivo está vacío.");
        }

        var columnas = MapaColumnas.Construir(filas[0]);
        var datos = filas.Skip(1).ToList();

        if (datos.Count == 0)
        {
            throw new ReglaNegocioException(
                "El archivo solo tiene el encabezado: no hay ningún trabajador que registrar.");
        }

        if (datos.Count > MaximoFilas)
        {
            throw new ReglaNegocioException(
                $"El archivo tiene {datos.Count} filas y el máximo por carga es {MaximoFilas}. " +
                "Pártalo en varios archivos.");
        }

        var catalogos = await CatalogosResueltos.CargarAsync(contexto, cancelacion).ConfigureAwait(false);

        var errores = new List<ErrorCargaMasiva>();
        var creados = 0;

        var transaccion = await contexto.Database
            .BeginTransactionAsync(cancelacion)
            .ConfigureAwait(false);

        await using (transaccion.ConfigureAwait(false))
        {
            for (var i = 0; i < datos.Count; i++)
            {
                // +2: la fila 1 del archivo es el encabezado, así que el primer trabajador
                // es la fila 2 tal como la ve el usuario en Excel.
                var numeroFila = i + 2;
                var fila = datos[i];
                var referencia = columnas.Valor(fila, Columna.NumeroDocumento) ?? $"fila {numeroFila}";

                try
                {
                    var solicitud = Construir(fila, columnas, catalogos);

                    await servicio.CrearAsync(solicitud, usuario, cancelacion).ConfigureAwait(false);

                    creados++;
                }
                catch (ReglaNegocioException excepcion)
                {
                    errores.Add(new ErrorCargaMasiva(numeroFila, referencia, excepcion.Message));
                }
                catch (RecursoNoEncontradoException excepcion)
                {
                    errores.Add(new ErrorCargaMasiva(numeroFila, referencia, excepcion.Message));
                }
            }

            if (soloValidar)
            {
                await transaccion.RollbackAsync(cancelacion).ConfigureAwait(false);
            }
            else
            {
                await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);
            }
        }

        return new ResultadoCargaMasiva(
            nombreArchivo,
            datos.Count,
            soloValidar ? 0 : creados,
            errores,
            soloValidar);
    }

    public byte[] GenerarPlantillaCsv()
    {
        var lineas = new StringBuilder();

        lineas.AppendLine(string.Join(';', MapaColumnas.EncabezadoCanonico));
        lineas.AppendLine(
            "DNI;09876543;PEREZ;QUISPE;JUAN CARLOS;M;987654321;AV. LOS OLIVOS 123;" +
            "jperez@ejemplo.pe;SI;CONSULTING ADVISERS;DETERGENTES;OBRERO;OPER;01/08/2026;;;;1200.00");

        // Con BOM: sin él, Excel en español abre el archivo como Windows-1252 y la
        // plantilla de ejemplo aparece con los acentos rotos desde el primer momento.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(lineas.ToString())];
    }

    // ------------------------------------------------------------------

    private static GuardarTrabajadorRequest Construir(
        string[] fila,
        MapaColumnas columnas,
        CatalogosResueltos catalogos)
    {
        var tipoDocumento = catalogos.TipoDocumento(
            Obligatorio(columnas, fila, Columna.TipoDocumento, "el tipo de documento"));

        var empresa = catalogos.Empresa(
            Obligatorio(columnas, fila, Columna.Empresa, "la empresa"));

        var planta = catalogos.Planta(
            Obligatorio(columnas, fila, Columna.Unidad, "la unidad (planta)"),
            empresa);

        var puesto = catalogos.Puesto(
            Obligatorio(columnas, fila, Columna.Puesto, "el puesto"));

        return new GuardarTrabajadorRequest
        {
            TipoDocumentoId = tipoDocumento,
            NumeroDocumento = Obligatorio(columnas, fila, Columna.NumeroDocumento, "el número de documento"),
            ApellidoPaterno = Obligatorio(columnas, fila, Columna.ApellidoPaterno, "el apellido paterno"),
            ApellidoMaterno = Obligatorio(columnas, fila, Columna.ApellidoMaterno, "el apellido materno"),
            Nombres = Obligatorio(columnas, fila, Columna.Nombres, "los nombres"),
            Sexo = LeerSexo(Obligatorio(columnas, fila, Columna.Sexo, "el sexo")),
            Telefono = columnas.Valor(fila, Columna.Telefono),
            Direccion = columnas.Valor(fila, Columna.Direccion),
            CorreoElectronico = columnas.Valor(fila, Columna.Correo),
            TieneHijos = LeerBooleano(columnas.Valor(fila, Columna.TieneHijos)),
            EmpresaId = empresa,
            TipoPlanilla = LeerTipoPlanilla(Obligatorio(columnas, fila, Columna.TipoPlanilla, "el tipo de planilla")),
            PlantaId = planta,
            PuestoId = puesto,
            FechaIngreso = LeerFecha(Obligatorio(columnas, fila, Columna.FechaIngreso, "la fecha de ingreso")),
            CentroCostoId = catalogos.CentroCostoOpcional(columnas.Valor(fila, Columna.CentroCosto)),
            IdMarcador = columnas.Valor(fila, Columna.IdMarcador),
            TurnoId = catalogos.TurnoOpcional(columnas.Valor(fila, Columna.Turno)),
            MontoSueldoBasico = LeerDecimalOpcional(columnas.Valor(fila, Columna.SueldoBasico))
        };
    }

    private static string Obligatorio(MapaColumnas columnas, string[] fila, Columna columna, string queEs)
    {
        var valor = columnas.Valor(fila, columna);

        return string.IsNullOrWhiteSpace(valor)
            ? throw new ReglaNegocioException($"Falta {queEs} (columna '{MapaColumnas.Nombre(columna)}').")
            : valor;
    }

    private static Sexo LeerSexo(string valor) => Normalizar(valor) switch
    {
        "m" or "masculino" or "h" or "hombre" => Sexo.Masculino,
        "f" or "femenino" or "mujer" => Sexo.Femenino,
        _ => throw new ReglaNegocioException(
            $"El sexo '{valor}' no es válido. Use M o F (también se aceptan MASCULINO y FEMENINO).")
    };

    private static TipoPlanilla LeerTipoPlanilla(string valor) => Normalizar(valor) switch
    {
        "obrero" or "obm" or "o" => TipoPlanilla.Obrero,
        "empleado" or "emp" or "e" => TipoPlanilla.Empleado,
        _ => throw new ReglaNegocioException(
            $"El tipo de planilla '{valor}' no es válido. Use OBRERO (OBM) o EMPLEADO (EMP).")
    };

    private static bool LeerBooleano(string? valor) => Normalizar(valor) switch
    {
        "si" or "s" or "1" or "true" or "verdadero" or "x" => true,
        _ => false
    };

    private static DateOnly LeerFecha(string valor) =>
        DateOnly.TryParseExact(valor, FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha
            : throw new ReglaNegocioException(
                $"La fecha '{valor}' no es válida. Use el formato dd/mm/aaaa (por ejemplo 01/08/2026).");

    /// <summary>
    /// Lee un importe tolerando las dos convenciones que salen de Excel: "1200.50" y
    /// "1,200.50" de una máquina en inglés, "1200,50" y "1.200,50" de una en español.
    /// </summary>
    /// <remarks>
    /// La regla es que el último separador que aparece es el decimal y el otro es de
    /// miles. Es lo que distingue "1.200" (mil doscientos) de "1,200" en cada
    /// convención — y confundirlos son mil soles de diferencia en un sueldo básico.
    /// </remarks>
    private static decimal? LeerDecimalOpcional(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var limpio = valor.Replace(" ", string.Empty, StringComparison.Ordinal);

        var ultimaComa = limpio.LastIndexOf(',');
        var ultimoPunto = limpio.LastIndexOf('.');

        if (ultimaComa >= 0 && ultimoPunto >= 0)
        {
            var separadorMiles = ultimaComa > ultimoPunto ? '.' : ',';
            limpio = limpio.Replace(separadorMiles.ToString(), string.Empty, StringComparison.Ordinal);
        }

        limpio = limpio.Replace(',', '.');

        if (!decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out var monto))
        {
            throw new ReglaNegocioException($"El sueldo básico '{valor}' no es un importe válido.");
        }

        if (monto < 0)
        {
            throw new ReglaNegocioException("El sueldo básico no puede ser negativo.");
        }

        return monto;
    }

    /// <summary>
    /// Deja el texto comparable: sin tildes, sin mayúsculas y sin espacios de sobra.
    /// El usuario escribe "Detergentes", "DETERGENTES" o "detergentes " y las tres cosas
    /// son la misma planta.
    /// </summary>
    internal static string Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var descompuesto = valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                resultado.Append(caracter);
            }
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }
}
