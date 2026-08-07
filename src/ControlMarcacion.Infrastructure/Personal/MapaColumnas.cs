using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Infrastructure.Personal;

/// <summary>Columnas que entiende la carga masiva.</summary>
internal enum Columna
{
    TipoDocumento,
    NumeroDocumento,
    ApellidoPaterno,
    ApellidoMaterno,
    Nombres,
    Sexo,
    Telefono,
    Direccion,
    Correo,
    TieneHijos,
    Empresa,
    Unidad,
    TipoPlanilla,
    Puesto,
    FechaIngreso,
    CentroCosto,
    IdMarcador,
    Turno,
    SueldoBasico
}

/// <summary>
/// Relaciona cada columna del archivo con su posición.
/// </summary>
/// <remarks>
/// Se busca por nombre de encabezado y no por posición fija: un archivo al que alguien le
/// insertó una columna al medio seguiría cargando "bien", con los apellidos en la columna
/// del teléfono, y nadie lo notaría hasta ver el padrón. Además así el orden de las
/// columnas deja de importar.
///
/// Cada columna acepta varios nombres porque quien llena la planilla escribe "DNI",
/// "documento" o "nro documento" para lo mismo, y rechazar el archivo por eso solo
/// consigue que lo intente cinco veces.
/// </remarks>
internal sealed class MapaColumnas
{
    private static readonly Dictionary<Columna, string[]> Alias = new()
    {
        [Columna.TipoDocumento] = ["tipo_documento", "tipodocumento", "tipo doc", "tipo de documento"],
        [Columna.NumeroDocumento] = ["numero_documento", "documento", "dni", "nro_documento", "numero de documento"],
        [Columna.ApellidoPaterno] = ["apellido_paterno", "apellidopaterno", "ap_paterno", "apellido paterno"],
        [Columna.ApellidoMaterno] = ["apellido_materno", "apellidomaterno", "ap_materno", "apellido materno"],
        [Columna.Nombres] = ["nombres", "nombre"],
        [Columna.Sexo] = ["sexo", "genero"],
        [Columna.Telefono] = ["telefono", "celular", "telefono_celular"],
        [Columna.Direccion] = ["direccion", "domicilio"],
        [Columna.Correo] = ["correo", "correo_electronico", "email", "e-mail"],
        [Columna.TieneHijos] = ["tiene_hijos", "hijos", "con_hijos"],
        [Columna.Empresa] = ["empresa", "razon_social", "ruc", "razon social"],
        // "Unidad" es el nombre que usa GCA para lo que el sistema llama Planta.
        [Columna.Unidad] = ["unidad", "planta", "unidad_planta", "unidad (planta)"],
        [Columna.TipoPlanilla] = ["tipo_planilla", "planilla", "tipo de planilla"],
        [Columna.Puesto] = ["puesto", "cargo"],
        [Columna.FechaIngreso] = ["fecha_ingreso", "ingreso", "fecha de ingreso", "fecha_de_ingreso"],
        [Columna.CentroCosto] = ["centro_costo", "centro_de_costo", "ceco", "centro de costo"],
        [Columna.IdMarcador] = ["id_marcador", "marcador", "id marcador", "codigo_marcador"],
        [Columna.Turno] = ["turno"],
        [Columna.SueldoBasico] = ["sueldo_basico", "sueldo", "basico", "sueldo basico"]
    };

    /// <summary>
    /// Columnas sin las que no se puede dar de alta a nadie. El resto son opcionales y
    /// pueden faltar del archivo por completo.
    /// </summary>
    private static readonly Columna[] Obligatorias =
    [
        Columna.TipoDocumento,
        Columna.NumeroDocumento,
        Columna.ApellidoPaterno,
        Columna.ApellidoMaterno,
        Columna.Nombres,
        Columna.Sexo,
        Columna.Empresa,
        Columna.Unidad,
        Columna.TipoPlanilla,
        Columna.Puesto,
        Columna.FechaIngreso
    ];

    /// <summary>Encabezado que genera la plantilla de ejemplo, en orden.</summary>
    public static IReadOnlyList<string> EncabezadoCanonico { get; } =
        [.. Enum.GetValues<Columna>().Select(c => Alias[c][0])];

    private readonly Dictionary<Columna, int> _posiciones;

    private MapaColumnas(Dictionary<Columna, int> posiciones) => _posiciones = posiciones;

    public static string Nombre(Columna columna) => Alias[columna][0];

    public static MapaColumnas Construir(string[] encabezado)
    {
        ArgumentNullException.ThrowIfNull(encabezado);

        var posiciones = new Dictionary<Columna, int>();

        for (var i = 0; i < encabezado.Length; i++)
        {
            var titulo = CargaMasivaTrabajadores.Normalizar(encabezado[i]).Replace(' ', '_');

            foreach (var (columna, alias) in Alias)
            {
                if (posiciones.ContainsKey(columna))
                {
                    continue;
                }

                if (alias.Any(a => string.Equals(
                        CargaMasivaTrabajadores.Normalizar(a).Replace(' ', '_'),
                        titulo,
                        StringComparison.Ordinal)))
                {
                    posiciones[columna] = i;
                    break;
                }
            }
        }

        var faltantes = Obligatorias.Where(c => !posiciones.ContainsKey(c)).ToList();

        if (faltantes.Count > 0)
        {
            throw new ReglaNegocioException(
                "Al archivo le faltan columnas obligatorias: " +
                string.Join(", ", faltantes.Select(Nombre)) +
                ". Descargue la plantilla de ejemplo para ver el encabezado esperado.");
        }

        return new MapaColumnas(posiciones);
    }

    /// <summary>
    /// Valor de una columna en una fila. Nulo si la columna no está en el archivo, si la
    /// fila viene más corta que el encabezado o si el campo está vacío.
    /// </summary>
    public string? Valor(string[] fila, Columna columna)
    {
        ArgumentNullException.ThrowIfNull(fila);

        if (!_posiciones.TryGetValue(columna, out var indice) || indice >= fila.Length)
        {
            return null;
        }

        var valor = fila[indice].Trim();

        return string.IsNullOrEmpty(valor) ? null : valor;
    }
}
