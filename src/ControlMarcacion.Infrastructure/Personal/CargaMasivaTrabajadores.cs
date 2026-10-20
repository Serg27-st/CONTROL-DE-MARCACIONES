using System.Globalization;
using System.Text;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
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
    ServicioTrabajadores servicio,
    ITrabajadorRepository repositorio,
    IConsultaCatalogos consultaCatalogos,
    IServicioMarcadores servicioMarcadores) : ICargaMasivaTrabajadores
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
        bool actualizarExistentes = false,
        bool registrarMarcadores = true,
        CancellationToken cancelacion = default)
    {
        // Se decide por el contenido y no por la extensión: un .xlsx renombrado a .csv, o al
        // revés, es un error de quien sube el archivo, no un motivo para rechazarlo.
        var filas = LectorExcel.EsLibroExcel(contenido)
            ? LectorExcel.Leer(contenido)
            : LectorCsv.Leer(contenido);

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

        // Equipos por planta, una sola vez por archivo: deciden a qué aparato va un ID que
        // la fila no declara. Con uno solo en la planta no hay nada que preguntar.
        var equiposPorPlanta = (await consultaCatalogos.EquiposActivosAsync(cancelacion).ConfigureAwait(false))
            .GroupBy(e => e.PlantaId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<EquipoOpcionDto>)g.ToList());

        var errores = new List<ErrorCargaMasiva>();
        var creados = 0;
        var marcadoresRegistrados = new List<MarcadorRegistradoEnCarga>();
        var filasSinMarcadorEnCatalogo = 0;

        // La transacción va DENTRO de la estrategia de ejecución, no al revés.
        //
        // El DbContext se registra con EnableRetryOnFailure(), y esa estrategia se niega a
        // convivir con una transacción abierta a mano: hay que entregarle el bloque entero
        // —abrir, recorrer, confirmar— como una unidad que ella pueda reintentar completa.
        // Llamar a BeginTransactionAsync por fuera lanza InvalidOperationException en la
        // primera consulta, así que la carga masiva fallaba contra SQL Server aunque
        // funcionara con el proveedor en memoria de las pruebas, que no lleva estrategia.
        var estrategia = contexto.Database.CreateExecutionStrategy();

        await estrategia.ExecuteAsync(async () =>
        {
            // Se reinician en cada intento: si la estrategia reintenta tras un fallo
            // transitorio, arrastrar el conteo anterior duplicaría los creados y repetiría
            // los errores de las filas que ya se habían procesado.
            errores.Clear();
            creados = 0;
            marcadoresRegistrados.Clear();
            filasSinMarcadorEnCatalogo = 0;

            var transaccion = await contexto.Database
                .BeginTransactionAsync(cancelacion)
                .ConfigureAwait(false);

            await using (transaccion.ConfigureAwait(false))
            {
                for (var i = 0; i < datos.Count; i++)
                {
                    // +2: la fila 1 del archivo es el encabezado, así que el primer
                    // trabajador es la fila 2 tal como la ve el usuario en Excel.
                    var numeroFila = i + 2;
                    var fila = datos[i];
                    var referencia = columnas.Valor(fila, Columna.NumeroDocumento) ?? $"fila {numeroFila}";

                    try
                    {
                        var solicitud = Construir(fila, columnas, catalogos);

                        // Antes del alta: el ID tiene que existir en el catálogo de su
                        // aparato para que ServicioTrabajadores lo acepte.
                        var marcador = await AsegurarMarcadorEnCatalogoAsync(
                                solicitud, numeroFila, registrarMarcadores, equiposPorPlanta,
                                usuario, nombreArchivo, cancelacion)
                            .ConfigureAwait(false);

                        if (marcador.Registrado is { } registrado)
                        {
                            marcadoresRegistrados.Add(registrado);
                        }

                        if (marcador.Faltaba)
                        {
                            filasSinMarcadorEnCatalogo++;
                        }

                        // Con la opción activa, un documento que ya existe actualiza esa
                        // ficha en vez de rechazarse. Se resuelve el Id aquí porque el
                        // archivo trae el documento, no la clave interna.
                        var existente = actualizarExistentes
                            ? await repositorio
                                .IdPorDocumentoAsync(
                                    solicitud.TipoDocumentoId,
                                    solicitud.NumeroDocumento.Trim(),
                                    cancelacion)
                                .ConfigureAwait(false)
                            : null;

                        if (existente is { } id)
                        {
                            await servicio
                                .ActualizarAsync(solicitud with { Id = id }, usuario, cancelacion)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            await servicio.CrearAsync(solicitud, usuario, cancelacion).ConfigureAwait(false);
                        }

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
        }).ConfigureAwait(false);

        return new ResultadoCargaMasiva(
            nombreArchivo,
            datos.Count,
            soloValidar ? 0 : creados,
            errores,
            soloValidar,
            marcadoresRegistrados,
            filasSinMarcadorEnCatalogo);
    }

    /// <summary>
    /// Registra en el catálogo el ID de marcador de la fila si su aparato todavía no lo tiene.
    /// </summary>
    /// <remarks>
    /// Sin esto, el primer padrón exigía dar de alta a mano, uno por uno, los cien ID de cada
    /// aparato antes de subir el archivo —doscientos en una planta con dos—. Y no hay otra
    /// fuente de donde tomarlos: sin acceso físico a los marcadores, el padrón es lo único
    /// que dice qué ID lleva cada persona en cada aparato.
    ///
    /// Solo agrega: nunca modifica ni reactiva un código existente, y lo guarda tal cual, con
    /// sus ceros a la izquierda (<c>0014</c> y <c>14</c> siguen siendo códigos distintos).
    /// Si la fila no declara el aparato y la planta tiene más de uno, no se adivina: se
    /// rechaza la fila. Registrar el código en el aparato equivocado sería asignarle a esta
    /// persona las marcaciones de otra.
    ///
    /// ⚠️ El código se registra antes del alta de la persona, así que si la fila falla
    /// después —un documento repetido, por ejemplo— queda en el catálogo, libre. Es
    /// deliberado: es el código que esa persona tendrá cuando se corrija la fila. En
    /// «Validar sin guardar» la transacción lo deshace junto con todo lo demás.
    /// </remarks>
    private async Task<(MarcadorRegistradoEnCarga? Registrado, bool Faltaba)> AsegurarMarcadorEnCatalogoAsync(
        GuardarTrabajadorRequest solicitud,
        int numeroFila,
        bool registrar,
        IReadOnlyDictionary<long, IReadOnlyList<EquipoOpcionDto>> equiposPorPlanta,
        string usuario,
        string nombreArchivo,
        CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(solicitud.IdMarcador))
        {
            return (null, false);
        }

        var codigo = solicitud.IdMarcador.Trim();
        var equiposDeLaPlanta = equiposPorPlanta.GetValueOrDefault(solicitud.PlantaId, []);

        // El aparato lo decide la fila; si no lo dice, solo se infiere cuando no hay nada
        // entre qué elegir.
        var equipo = solicitud.EquipoId is { } declarado
            ? equiposDeLaPlanta.FirstOrDefault(e => e.Id == declarado)
            : equiposDeLaPlanta.Count == 1 ? equiposDeLaPlanta[0] : null;

        var candidatos = await consultaCatalogos
            .MarcadoresPorCodigoAsync(codigo, solicitud.PlantaId, cancelacion)
            .ConfigureAwait(false);

        var yaExiste = equipo is not null
            ? candidatos.Any(c => c.EquipoId == equipo.Id)
            : candidatos.Count > 0;

        if (yaExiste)
        {
            return (null, false);
        }

        if (!registrar)
        {
            return (null, true);
        }

        if (equiposDeLaPlanta.Count == 0)
        {
            throw new ReglaNegocioException(
                "La unidad no tiene ningún aparato registrado. Regístrelo en Equipos antes de " +
                "cargar a su personal.");
        }

        if (equipo is null)
        {
            throw new ReglaNegocioException(
                $"Para registrar el ID de marcador '{codigo}' hay que saber de qué aparato es: la " +
                $"unidad tiene {equiposDeLaPlanta.Count} marcadores " +
                $"({string.Join(", ", equiposDeLaPlanta.Select(e => e.Nombre))}). Complete la " +
                "columna «equipo» de esta fila.");
        }

        if (codigo.Length > 30)
        {
            throw new ReglaNegocioException(
                $"El ID de marcador '{codigo}' supera los 30 caracteres.");
        }

        var descripcion = $"Registrado por la carga masiva de {nombreArchivo}";

        await servicioMarcadores
            .GuardarAsync(
                new GuardarMarcadorRequest
                {
                    Codigo = codigo,
                    EquipoId = equipo.Id,
                    Descripcion = descripcion.Length > 150 ? descripcion[..150] : descripcion,
                    Activo = true
                },
                usuario,
                cancelacion)
            .ConfigureAwait(false);

        return (new MarcadorRegistradoEnCarga(numeroFila, codigo, equipo.Nombre), false);
    }

    public byte[] GenerarPlantillaCsv()
    {
        var lineas = new StringBuilder();

        lineas.AppendLine(string.Join(';', MapaColumnas.EncabezadoCanonico));

        // La fila de ejemplo tiene que valer para una carga real: sus valores de empresa,
        // unidad y puesto son los que existen de verdad en el catálogo. Si aquí dijera
        // "CONSULTING ADVISERS" a secas, quien copie la plantilla vería fallar las 250
        // filas por una razón social que no coincide con ninguna.
        //
        // El orden y la cantidad de campos los dicta EncabezadoCanonico: si se agrega una
        // columna al enum y no se agrega aquí, la plantilla sale descuadrada.
        // El campo vacío tras el id de marcador es `equipo`: solo hace falta cuando la
        // planta tiene más de un aparato, y con uno solo el sistema lo resuelve. Hay un
        // test que comprueba que esta fila tenga tantos campos como el encabezado.
        lineas.AppendLine(
            "DNI;09876543;PEREZ;QUISPE;JUAN CARLOS;M;987654321;AV. LOS OLIVOS 123;" +
            "jperez@ejemplo.pe;SI;CONSULTING ADVISERS S.A.C;PLANTA DETERGENTES LURÍN;OBRERO;OPERARIO;" +
            "01/08/2026;PE21400307;;;;1200.00;ACTIVO;Envasado Detergentes Lurín;;");

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
        var fechaCese = LeerFechaOpcional(columnas.Valor(fila, Columna.FechaCese));

        RechazarSiNoEstaActivoNiCesado(columnas.Valor(fila, Columna.Situacion), fechaCese);

        var tipoDocumento = catalogos.TipoDocumento(
            EquivalenteTipoDocumento(
                Obligatorio(columnas, fila, Columna.TipoDocumento, "el tipo de documento")));

        var empresa = catalogos.Empresa(
            Obligatorio(columnas, fila, Columna.Empresa, "la empresa"));

        var planta = catalogos.Planta(
            Obligatorio(columnas, fila, Columna.Unidad, "la unidad (planta)"),
            empresa);

        var puesto = catalogos.Puesto(
            EquivalentePuesto(Obligatorio(columnas, fila, Columna.Puesto, "el puesto")),
            empresa);

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
            AreaId = catalogos.AreaOpcional(columnas.Valor(fila, Columna.Area), planta),
            TipoPlanilla = LeerTipoPlanilla(Obligatorio(columnas, fila, Columna.TipoPlanilla, "el tipo de planilla")),
            PlantaId = planta,
            PuestoId = puesto,
            FechaIngreso = LeerFecha(Obligatorio(columnas, fila, Columna.FechaIngreso, "la fecha de ingreso")),
            FechaCese = fechaCese,
            MotivoCese = LeerMotivoCese(columnas.Valor(fila, Columna.MotivoCese)),
            CentroCostoId = catalogos.CentroCostoOpcional(columnas.Valor(fila, Columna.CentroCosto), empresa, planta),
            IdMarcador = columnas.Valor(fila, Columna.IdMarcador),
            EquipoId = catalogos.EquipoOpcional(columnas.Valor(fila, Columna.Equipo), planta),
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

    /// <summary>
    /// Rechaza la fila solo si el trabajador no está vigente <b>y</b> tampoco trae la fecha
    /// en que dejó de estarlo.
    /// </summary>
    /// <remarks>
    /// La exportación de planilla de GCA incluye al personal retirado (situación
    /// <c>ANULADO</c>) junto al vigente. Con fecha de cese se dan de alta igual, ya cesados:
    /// es la única forma de que sus marcaciones del mes —que existen, porque marcaron antes
    /// de irse— se puedan atribuir a alguien en vez de quedar huérfanas en la ingesta.
    ///
    /// Sin fecha sí se rechazan: darlos de alta como vigentes los haría contar en el tareo
    /// como presentes todo el mes, y adivinar la fecha sería peor que no cargarlos.
    ///
    /// Si la columna de situación no viene en el archivo no se comprueba nada: quien arma
    /// una plantilla a mano ya está listando solo a quien quiere dar de alta.
    /// </remarks>
    private static void RechazarSiNoEstaActivoNiCesado(string? situacion, DateOnly? fechaCese)
    {
        if (string.IsNullOrWhiteSpace(situacion))
        {
            return;
        }

        var normalizada = Normalizar(situacion);

        if (normalizada is "activo" or "activa" or "vigente" or "alta" or "a")
        {
            return;
        }

        if (fechaCese is not null)
        {
            return;
        }

        throw new ReglaNegocioException(
            $"El trabajador figura como '{situacion}' en el archivo y no como activo, pero la " +
            $"fila no trae fecha de cese (columna '{MapaColumnas.Nombre(Columna.FechaCese)}'). " +
            "Agréguela para darlo de alta ya cesado, o quite la fila si no debe registrarse.");
    }

    /// <summary>Fecha opcional. Nula si la columna no está o viene vacía.</summary>
    private static DateOnly? LeerFechaOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : LeerFecha(valor);

    /// <summary>
    /// Causal del cese. Nula si no viene: aquí es opcional a propósito.
    /// </summary>
    /// <remarks>
    /// Se aceptan tanto el nombre del enum como la forma en que se escribe en un archivo
    /// ("RENUNCIA VOLUNTARIA", "TERMINO DE CONTRATO"). Un valor que no se reconozca sí
    /// rechaza la fila: guardar una causal equivocada es peor que no guardar ninguna,
    /// porque nadie vuelve a mirarla.
    /// </remarks>
    private static MotivoCese? LeerMotivoCese(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        // Se aceptan tanto la descripción como el código corto de la planilla de GCA
        // (REN, ATR, TCO, TPP). El código es lo más estable de los dos: la descripción
        // cambia de redacción entre exportaciones, el código no.
        return Normalizar(valor).Replace(" ", string.Empty, StringComparison.Ordinal) switch
        {
            "ren" or "renunciavoluntaria" or "renuncia" =>
                MotivoCese.RenunciaVoluntaria,

            "tco" or "terminocontrato" or "terminodecontrato" or "fincontrato" =>
                MotivoCese.TerminoContrato,

            "tpp" or "periodoprueba" or "periododeprueba" or "nosuperoperiodoprueba"
                or "nosuperoelperiododeprueba" =>
                MotivoCese.NoSuperoPeriodoPrueba,

            "atr" or "abandonodetrabajo" or "abandonotrabajo" or "abandonolaboral" or "abandono" =>
                MotivoCese.AbandonoLaboral,

            "des" or "despido" => MotivoCese.Despido,
            "dst" or "destitucion" => MotivoCese.Destitucion,

            _ => throw new ReglaNegocioException(
                $"El motivo de cese '{valor}' no se reconoce. Use renuncia voluntaria (REN), " +
                "término de contrato (TCO), periodo de prueba (TPP), abandono de trabajo (ATR), " +
                "despido o destitución.")
        };
    }

    /// <summary>
    /// Traduce las abreviaturas de tipo de documento de otros sistemas a las del catálogo.
    /// </summary>
    /// <remarks>
    /// Mismo criterio que los alias de columna: la planilla de GCA escribe <c>CEX</c> donde
    /// el catálogo dice <c>CE</c>, y rechazar veinticuatro filas por dos letras solo
    /// consigue que alguien edite el archivo a mano cada mes. Lo que no está aquí se
    /// resuelve tal cual contra el catálogo, así que un tipo nuevo sigue siendo un INSERT.
    /// </remarks>
    private static string EquivalenteTipoDocumento(string valor) => Normalizar(valor) switch
    {
        // "C.E" es como lo escribe el padrón de Planta 1.
        "cex" or "ce_x" or "c.e" or "c.e." or "carnet de extranjeria" or "carne extranjeria" => "CE",
        "doc. nacional de identidad" or "d.n.i." => "DNI",
        _ => valor
    };

    /// <summary>
    /// Traduce al código del catálogo general los sinónimos de puesto que traen los
    /// archivos: la grafía de cada padrón de planta y los códigos de la planilla de GCA.
    /// </summary>
    /// <remarks>
    /// El catálogo tiene una sola fila por puesto (ver <c>DatosSemilla</c>): cuando cada
    /// grafía era un puesto aparte, "O. MAQUINA", "MAQUINISTA" y "Operario de máquina" se
    /// veían como tres puestos distintos. Lo que no está aquí se resuelve tal cual contra
    /// el catálogo, así que un puesto nuevo sigue siendo un INSERT.
    /// </remarks>
    private static string EquivalentePuesto(string valor) => Normalizar(valor) switch
    {
        "operario de produccion" or "26001" => "OPER",
        "o. maquina" or "o.maquina" or "o maquina" or "maquinista" or "operario de maquina" or "26006" => "OPEMAQ",
        "o. traspalet" or "o.traspalet" or "o traspalet" or "traspaletista" or "operario transpalet"
            or "operador transpalet" or "operador traspalet" or "26002" or "26007" => "OPETRA",
        "26216" => "SUPV",
        "26223" => "SUPPRO",
        "26229" => "SUPSSO",
        "ssoma" or "inspector de ssoma" or "26148" => "INSSSO",
        "coordinador de produccion" or "26104" => "COORD",
        "coord. rrhh" or "coord rrhh" or "coordinador de rrhh" => "CORRHH",
        _ => valor
    };

    private static Sexo LeerSexo(string valor) => Normalizar(valor) switch
    {
        "m" or "masculino" or "h" or "hombre" => Sexo.Masculino,
        "f" or "femenino" or "mujer" => Sexo.Femenino,
        _ => throw new ReglaNegocioException(
            $"El sexo '{valor}' no es válido. Use M o F (también se aceptan MASCULINO y FEMENINO).")
    };

    private static TipoPlanilla LeerTipoPlanilla(string valor) => Normalizar(valor) switch
    {
        // En plural porque así lo exporta la planilla de GCA ("OBREROS", "EMPLEADOS").
        "obrero" or "obreros" or "obm" or "o" => TipoPlanilla.Obrero,
        "empleado" or "empleados" or "emp" or "e" => TipoPlanilla.Empleado,
        _ => throw new ReglaNegocioException(
            $"El tipo de planilla '{valor}' no es válido. Use OBRERO (OBM) o EMPLEADO (EMP).")
    };

    /// <remarks>
    /// Un número mayor que cero es verdadero: los padrones de planta escriben en
    /// "tiene hijos" la cantidad ("2", "3"), y leerla como NO le quitaba la asignación
    /// familiar a quien más hijos tiene.
    /// </remarks>
    private static bool LeerBooleano(string? valor) => Normalizar(valor) switch
    {
        "si" or "s" or "true" or "verdadero" or "x" => true,
        var texto when int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out var cantidad) => cantidad > 0,
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
