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
    Equipo,
    Turno,
    SueldoBasico,

    /// <summary>
    /// Situación del trabajador en el sistema de origen. Opcional.
    /// </summary>
    /// <remarks>
    /// La trae la exportación de planilla de GCA, que incluye tanto al personal vigente
    /// como al ya retirado. Si la columna está, las filas que no figuren activas se
    /// rechazan con su motivo: darlas de alta las convertiría en trabajadores vigentes,
    /// y el archivo no trae fecha de cese con la que registrarles la baja.
    /// </remarks>
    Situacion,

    /// <summary>
    /// Área interna del trabajador dentro de su planta. Opcional.
    /// </summary>
    /// <remarks>
    /// Es la columna <c>AREA</c> del tareo final, y no se deriva del centro de costo: en el
    /// archivo de GCA un mismo CECO aparece con varias áreas. Sin esta columna el área
    /// quedaba en blanco y había que asignarla a mano trabajador por trabajador.
    /// </remarks>
    Area,

    /// <summary>
    /// Fecha de cese. Opcional, pero obligatoria si el trabajador no figura activo.
    /// </summary>
    /// <remarks>
    /// Permite dar de alta personal ya retirado sin inventarle una baja: la ficha nace
    /// cesada en esa fecha y sus asignaciones se cierran ahí mismo. Es lo que hace que sus
    /// marcaciones del mes se puedan atribuir —marcó de verdad antes de irse— en vez de
    /// quedar huérfanas en la ingesta.
    /// </remarks>
    FechaCese,

    /// <summary>
    /// Causal del cese. Opcional incluso cuando hay fecha.
    /// </summary>
    /// <remarks>
    /// La pantalla de baja sí la exige, y con razón: ahí alguien está dando de baja a una
    /// persona y debe decir por qué. Aquí es distinto — se está <b>registrando historia</b>
    /// que ya ocurrió, y la exportación de planilla no trae la causal. Entre inventar un
    /// "término de contrato" que nadie afirmó y dejar constancia de que no se sabe, lo
    /// segundo es lo honesto: la fecha es un hecho, el motivo sería una suposición con
    /// consecuencias laborales.
    /// </remarks>
    MotivoCese
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
        [Columna.TipoDocumento] =
            ["tipo_documento", "tipodocumento", "tipo doc", "tipo de documento",
             // Nombre en la exportación de planilla de GCA.
             "tipo_doc_identidad", "tipo doc identidad"],
        [Columna.NumeroDocumento] =
            ["numero_documento", "documento", "dni", "nro_documento", "numero de documento",
             "nro_doc_identidad", "nro doc identidad", "numero_doc_identidad"],
        [Columna.ApellidoPaterno] = ["apellido_paterno", "apellidopaterno", "ap_paterno", "apellido paterno"],
        [Columna.ApellidoMaterno] = ["apellido_materno", "apellidomaterno", "ap_materno", "apellido materno"],
        [Columna.Nombres] = ["nombres", "nombre"],
        [Columna.Sexo] = ["sexo", "genero"],
        [Columna.Telefono] = ["telefono", "celular", "telefono_celular", "telefono1"],
        [Columna.Direccion] = ["direccion", "domicilio", "direccion_trabajador"],
        [Columna.Correo] = ["correo", "correo_electronico", "email", "e-mail", "mail1", "mail"],
        [Columna.TieneHijos] = ["tiene_hijos", "hijos", "con_hijos"],
        [Columna.Empresa] = ["empresa", "razon_social", "ruc", "razon social"],
        // "Unidad" es el nombre que usa GCA para lo que el sistema llama Planta.
        [Columna.Unidad] = ["unidad", "planta", "unidad_planta", "unidad (planta)"],
        [Columna.TipoPlanilla] = ["tipo_planilla", "planilla", "tipo de planilla"],
        // Cuando el archivo trae código Y nombre del puesto —como la exportación de GCA—
        // gana el código, porque en el archivo aparece en una columna anterior y quien
        // decide es el orden de las columnas, no el de esta lista. Es lo correcto: dos
        // puestos pueden llamarse igual, sus códigos no.
        [Columna.Puesto] = ["puesto", "cargo", "codigo_de_puesto", "codigo_puesto", "puesto_trabajador"],
        [Columna.FechaIngreso] = ["fecha_ingreso", "ingreso", "fecha de ingreso", "fecha_de_ingreso"],
        [Columna.CentroCosto] = ["centro_costo", "centro_de_costo", "ceco", "centro de costo"],
        [Columna.IdMarcador] = ["id_marcador", "marcador", "id marcador", "codigo_marcador"],
        // El APARATO del que sale ese id, no el id en sí. Solo hace falta cuando la
        // planta tiene más de un equipo; con uno solo el sistema lo resuelve.
        [Columna.Equipo] = ["equipo", "equipo_marcador", "aparato", "reloj", "dispositivo"],
        [Columna.Turno] = ["turno"],
        [Columna.SueldoBasico] = ["sueldo_basico", "sueldo", "basico", "sueldo basico"],
        [Columna.Situacion] = ["situacion", "situacion_trabajador", "estado", "estado_trabajador"],
        [Columna.Area] = ["area", "area_trabajador", "seccion_area"],
        [Columna.FechaCese] = ["fecha_cese", "cese", "fecha de cese", "fecha_de_cese", "fecha_retiro"],
        [Columna.MotivoCese] = ["motivo_cese", "motivo de cese", "causal_cese", "motivo_retiro"]
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
            var titulo = Titulo(encabezado[i]);

            foreach (var (columna, alias) in Alias)
            {
                if (posiciones.ContainsKey(columna))
                {
                    continue;
                }

                if (alias.Any(a => string.Equals(
                        Titulo(a),
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
    /// Encabezado comparable: cualquier tramo de espacios —incluido el salto de línea de
    /// una celda escrita con Alt+Enter, como "tipo⏎documento"— cuenta como un guion bajo.
    /// </summary>
    private static string Titulo(string texto) =>
        string.Join('_', CargaMasivaTrabajadores.Normalizar(texto)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

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
