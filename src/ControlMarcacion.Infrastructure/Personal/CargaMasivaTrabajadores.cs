using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Catalogos;
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
    IServicioMarcadores servicioMarcadores,
    IServicioCatalogos servicioCatalogos,
    IServicioEquipos servicioEquipos) : ICargaMasivaTrabajadores
{
    private static readonly SemaphoreSlim ExclusividadCreacionCatalogos = new(1, 1);

    /// <summary>
    /// Tope de filas por archivo. El padrón completo de Intradevco no llega a mil; un
    /// archivo de cien mil es un error de quien lo generó, no una carga legítima, y
    /// procesarlo dejaría la pantalla colgada media hora.
    /// </summary>
    private const int MaximoFilas = 5000;

    private static readonly string[] FormatosFecha =
        ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy"];

    public Task<ResultadoCargaMasiva> PrevisualizarAsync(
        Stream contenido,
        string nombreArchivo,
        string usuario,
        bool actualizarExistentes = false,
        bool registrarMarcadores = true,
        CancellationToken cancelacion = default) =>
        ProcesarNucleoAsync(
            contenido, nombreArchivo, usuario, soloValidar: true, actualizarExistentes,
            registrarMarcadores, huellaPuestosEsperada: null, cancelacion);

    public Task<ResultadoCargaMasiva> ConfirmarAsync(
        Stream contenido,
        string nombreArchivo,
        string usuario,
        string huellaPuestosEsperada,
        bool actualizarExistentes = false,
        bool registrarMarcadores = true,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(huellaPuestosEsperada))
        {
            throw new ReglaNegocioException(
                "La previsualización no es válida. Vuelva a analizar el archivo antes de confirmar.");
        }

        return ProcesarNucleoAsync(
            contenido, nombreArchivo, usuario, soloValidar: false, actualizarExistentes,
            registrarMarcadores, huellaPuestosEsperada, cancelacion);
    }

    public Task<ResultadoCargaMasiva> ProcesarAsync(
        Stream contenido,
        string nombreArchivo,
        string usuario,
        bool soloValidar,
        bool actualizarExistentes = false,
        bool registrarMarcadores = true,
        CancellationToken cancelacion = default) =>
        ProcesarNucleoAsync(
            contenido, nombreArchivo, usuario, soloValidar, actualizarExistentes,
            registrarMarcadores, huellaPuestosEsperada: null, cancelacion);

    private async Task<ResultadoCargaMasiva> ProcesarNucleoAsync(
        Stream contenido,
        string nombreArchivo,
        string usuario,
        bool soloValidar,
        bool actualizarExistentes,
        bool registrarMarcadores,
        string? huellaPuestosEsperada,
        CancellationToken cancelacion)
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

        await ExclusividadCreacionCatalogos.WaitAsync(cancelacion).ConfigureAwait(false);

        try
        {
            var catalogos = await CatalogosResueltos.CargarAsync(contexto, cancelacion).ConfigureAwait(false);
            var puestos = AnalizarPuestos(datos, columnas, catalogos);

            var errores = new List<ErrorCargaMasiva>();
            var creados = 0;
            var marcadoresRegistrados = new List<MarcadorRegistradoEnCarga>();
            var filasSinMarcadorEnCatalogo = 0;
            var unidadesCreadas = new List<CatalogoCreadoEnCarga>();
            var equiposCreados = new List<CatalogoCreadoEnCarga>();
            var conciliador = new ConciliadorCarga();
            ResumenConciliacionCargaMasiva? conciliacion = null;

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
                unidadesCreadas.Clear();
                equiposCreados.Clear();
                conciliador = new ConciliadorCarga();

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
                        var puntoGuardado = $"fila_import_{i}";
                        Puesto? puestoNuevo = null;
                        CatalogoCreadoEnCarga? unidadNueva = null;
                        CatalogoCreadoEnCarga? equipoNuevo = null;
                        (long Id, long EquipoId, string Codigo, string Equipo)? marcadorNuevo = null;

                        await transaccion
                            .CreateSavepointAsync(puntoGuardado, cancelacion)
                            .ConfigureAwait(false);

                        try
                        {
                            var empresaTexto = Obligatorio(columnas, fila, Columna.Empresa, "la empresa");
                            var empresaId = catalogos.Empresa(empresaTexto);
                            var unidadTexto = Obligatorio(
                                columnas, fila, Columna.Unidad, "la unidad");
                            var resolucionUnidad = catalogos.ResolverPlanta(
                                unidadTexto,
                                empresaId,
                                columnas.Valor(fila, Columna.CodigoUnidad));
                            conciliador.Unidad(numeroFila, empresaTexto, resolucionUnidad);
                            ExigirResolucion(resolucionUnidad, "unidad");

                            long unidadId;
                            if (resolucionUnidad.Estado == EstadoResolucionCatalogo.Nuevo)
                            {
                                unidadId = await servicioCatalogos.GuardarAsync(
                                    "plantas",
                                    new GuardarCatalogoRequest
                                    {
                                        Codigo = resolucionUnidad.Codigo,
                                        Nombre = resolucionUnidad.Nombre,
                                        PadreId = empresaId,
                                        Activo = true
                                    },
                                    usuario,
                                    cancelacion).ConfigureAwait(false);
                                unidadNueva = new CatalogoCreadoEnCarga(
                                    numeroFila, unidadId, resolucionUnidad.Codigo, resolucionUnidad.Nombre);
                            }
                            else
                            {
                                unidadId = resolucionUnidad.Id!.Value;
                            }

                            var codigoMarcador = columnas.Valor(fila, Columna.IdMarcador);
                            var equipoDeclarado = columnas.Valor(fila, Columna.Equipo);
                            var codigoEquipo = columnas.Valor(fila, Columna.CodigoEquipo);
                            var nombreEquipo = columnas.Valor(fila, Columna.NombreEquipo);
                            var serieEquipo = columnas.Valor(fila, Columna.SerieEquipo);
                            var requiereEquipo = !string.IsNullOrWhiteSpace(codigoMarcador)
                                || !string.IsNullOrWhiteSpace(equipoDeclarado)
                                || !string.IsNullOrWhiteSpace(codigoEquipo)
                                || !string.IsNullOrWhiteSpace(nombreEquipo)
                                || !string.IsNullOrWhiteSpace(serieEquipo);
                            var resolucionEquipo = catalogos.ResolverEquipo(
                                equipoDeclarado,
                                codigoEquipo,
                                nombreEquipo,
                                serieEquipo,
                                unidadId,
                                requiereEquipo);
                            conciliador.Equipo(numeroFila, unidadTexto, resolucionEquipo);
                            ExigirResolucion(resolucionEquipo, "equipo");

                            long? equipoId = null;
                            if (resolucionEquipo.Estado == EstadoResolucionCatalogo.Nuevo)
                            {
                                equipoId = await servicioEquipos.GuardarAsync(
                                    new GuardarEquipoRequest
                                    {
                                        Codigo = resolucionEquipo.Codigo,
                                        Nombre = resolucionEquipo.Nombre,
                                        Serie = resolucionEquipo.Serie,
                                        PlantaId = unidadId,
                                        Activo = true,
                                        Descripcion = $"Registrado por la carga masiva de {nombreArchivo}"
                                    },
                                    usuario,
                                    cancelacion).ConfigureAwait(false);
                                equipoNuevo = new CatalogoCreadoEnCarga(
                                    numeroFila,
                                    equipoId.Value,
                                    resolucionEquipo.Codigo,
                                    resolucionEquipo.Nombre);
                            }
                            else if (resolucionEquipo.Estado == EstadoResolucionCatalogo.Existente)
                            {
                                equipoId = resolucionEquipo.Id;
                            }

                            var pendiente = Construir(
                                fila, columnas, catalogos, empresaId, unidadId, equipoId);
                            var resolucion = catalogos.ResolverPuesto(
                                pendiente.ValorPuesto,
                                pendiente.Solicitud.EmpresaId);

                            if (resolucion.Estado == EstadoResolucionPuesto.Ambiguo)
                            {
                                throw new ReglaNegocioException(
                                    $"El puesto '{resolucion.Nombre}' es ambiguo: coincide con " +
                                    string.Join(", ", resolucion.Candidatos) + ".");
                            }

                            long puestoId;

                            if (resolucion.Estado == EstadoResolucionPuesto.Nuevo)
                            {
                                puestoNuevo = new Puesto
                                {
                                    EmpresaId = resolucion.EmpresaId,
                                    Codigo = resolucion.Codigo!,
                                    Nombre = resolucion.Nombre,
                                    Activo = true,
                                    CreadoEn = DateTime.Now,
                                    CreadoPor = usuario
                                };
                                contexto.Puestos.Add(puestoNuevo);
                                await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
                                puestoId = puestoNuevo.Id;
                            }
                            else
                            {
                                puestoId = resolucion.PuestoId!.Value;
                            }

                            var solicitud = pendiente.Solicitud with { PuestoId = puestoId };

                            if (!string.IsNullOrWhiteSpace(solicitud.IdMarcador))
                            {
                                if (equipoId is null)
                                {
                                    throw new ReglaNegocioException(
                                        "La fila declara un ID de marcador, pero no permite determinar su equipo.");
                                }

                                var codigo = solicitud.IdMarcador.Trim();
                                if (catalogos.ExisteMarcador(equipoId.Value, codigo))
                                {
                                    conciliador.Marcador(numeroFila, equipoId.Value, codigo, resolucionEquipo.Nombre, existe: true);
                                }
                                else
                                {
                                    conciliador.Marcador(numeroFila, equipoId.Value, codigo, resolucionEquipo.Nombre, existe: false);
                                    if (!registrarMarcadores)
                                    {
                                        filasSinMarcadorEnCatalogo++;
                                        throw new ReglaNegocioException(
                                            $"El ID de marcador '{codigo}' no existe en el equipo indicado. " +
                                            "Active el registro de marcadores faltantes y vuelva a previsualizar.");
                                    }

                                    var idMarcador = await servicioMarcadores.GuardarAsync(
                                        new GuardarMarcadorRequest
                                        {
                                            Codigo = codigo,
                                            EquipoId = equipoId.Value,
                                            Descripcion = $"Registrado por la carga masiva de {nombreArchivo}",
                                            Activo = true
                                        },
                                        usuario,
                                        cancelacion).ConfigureAwait(false);
                                    marcadorNuevo = (
                                        idMarcador,
                                        equipoId.Value,
                                        codigo,
                                        resolucionEquipo.Nombre);
                                }
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

                            await transaccion
                                .ReleaseSavepointAsync(puntoGuardado, cancelacion)
                                .ConfigureAwait(false);

                            if (unidadNueva is not null)
                            {
                                catalogos.RegistrarPlanta(
                                    unidadNueva.Id, empresaId, unidadNueva.Codigo, unidadNueva.Nombre);
                                unidadesCreadas.Add(unidadNueva);
                            }

                            if (equipoNuevo is not null)
                            {
                                catalogos.RegistrarEquipo(
                                    equipoNuevo.Id,
                                    unidadId,
                                    equipoNuevo.Codigo,
                                    equipoNuevo.Nombre,
                                    serieEquipo);
                                equiposCreados.Add(equipoNuevo);
                            }

                            if (marcadorNuevo is { } nuevo)
                            {
                                catalogos.RegistrarMarcador(nuevo.Id, nuevo.EquipoId, nuevo.Codigo);
                                marcadoresRegistrados.Add(new MarcadorRegistradoEnCarga(
                                    numeroFila, nuevo.Codigo, nuevo.Equipo));
                            }

                            if (puestoNuevo is not null)
                            {
                                catalogos.RegistrarPuesto(
                                    puestoNuevo.Id,
                                    puestoNuevo.EmpresaId!.Value,
                                    puestoNuevo.Codigo,
                                    puestoNuevo.Nombre);
                            }

                            creados++;
                            conciliador.Fila(
                                numeroFila,
                                referencia,
                                empresaTexto,
                                unidadTexto,
                                pendiente.ValorPuesto,
                                resolucionEquipo.Estado == EstadoResolucionCatalogo.NoAplica
                                    ? null
                                    : resolucionEquipo.Nombre,
                                codigoMarcador,
                                solicitud.FechaIngreso,
                                EstadoConciliacionCargaMasiva.Existente,
                                existente is null ? "Crear trabajador y dependencias" : "Actualizar trabajador");
                        }
                        catch (ReglaNegocioException excepcion)
                        {
                            await RevertirFilaAsync(contexto, transaccion, puntoGuardado, cancelacion)
                                .ConfigureAwait(false);
                            errores.Add(new ErrorCargaMasiva(numeroFila, referencia, excepcion.Message));
                            conciliador.FilaInvalida(numeroFila, referencia, excepcion.Message);
                        }
                        catch (RecursoNoEncontradoException excepcion)
                        {
                            await RevertirFilaAsync(contexto, transaccion, puntoGuardado, cancelacion)
                                .ConfigureAwait(false);
                            errores.Add(new ErrorCargaMasiva(numeroFila, referencia, excepcion.Message));
                            conciliador.FilaInvalida(numeroFila, referencia, excepcion.Message);
                        }
                    }

                    conciliacion = conciliador.Construir(puestos);

                    if (huellaPuestosEsperada is not null
                        && !string.Equals(
                            huellaPuestosEsperada,
                            conciliacion.Huella,
                            StringComparison.Ordinal))
                    {
                        throw new ReglaNegocioException(
                            "Los catálogos cambiaron después de la previsualización. " +
                            "Vuelva a analizar el archivo antes de confirmar.");
                    }

                    if (soloValidar)
                    {
                        await transaccion.RollbackAsync(cancelacion).ConfigureAwait(false);
                        // Las claves temporales aceptadas por SaveChanges ya recibieron IDs,
                        // aunque la transacción se haya revertido. No pueden quedar seguidas:
                        // una confirmación en el mismo alcance chocaría con esas identidades.
                        contexto.ChangeTracker.Clear();
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
                filasSinMarcadorEnCatalogo,
                puestos,
                conciliacion ?? conciliador.Construir(puestos),
                soloValidar ? [] : unidadesCreadas,
                soloValidar ? [] : equiposCreados);
        }
        finally
        {
            ExclusividadCreacionCatalogos.Release();
        }
    }

    private static ResumenPuestosCargaMasiva AnalizarPuestos(
        IReadOnlyList<string[]> datos,
        MapaColumnas columnas,
        CatalogosResueltos catalogos)
    {
        var existentes = new Dictionary<(long EmpresaId, long PuestoId), (ResolucionPuesto Puesto, int Filas)>();
        var nuevos = new Dictionary<(long EmpresaId, string Clave), (ResolucionPuesto Puesto, int Filas)>();
        var ambiguos = new Dictionary<(long EmpresaId, string Clave), (ResolucionPuesto Puesto, int Filas)>();

        foreach (var fila in datos)
        {
            try
            {
                var empresaId = catalogos.Empresa(
                    Obligatorio(columnas, fila, Columna.Empresa, "la empresa"));
                var valor = EquivalentePuesto(
                    Obligatorio(columnas, fila, Columna.Puesto, "el puesto"));
                var resolucion = catalogos.ResolverPuesto(valor, empresaId);

                switch (resolucion.Estado)
                {
                    case EstadoResolucionPuesto.Existente:
                        Incrementar(existentes, (empresaId, resolucion.PuestoId!.Value), resolucion);
                        break;
                    case EstadoResolucionPuesto.Nuevo:
                        Incrementar(nuevos, (empresaId, resolucion.Clave), resolucion);
                        break;
                    case EstadoResolucionPuesto.Ambiguo:
                        Incrementar(ambiguos, (empresaId, resolucion.Clave), resolucion);
                        break;
                    default:
                        throw new InvalidOperationException("Estado de conciliación de puesto desconocido.");
                }
            }
            catch (ReglaNegocioException)
            {
                // La fila se reporta con su número durante el procesamiento normal. No se
                // inventa una categoría de puesto cuando ni empresa ni valor son válidos.
            }
        }

        var puestosExistentes = existentes.Values
            .Select(v => new PuestoCargaMasiva(
                v.Puesto.EmpresaId,
                catalogos.NombreEmpresa(v.Puesto.EmpresaId),
                v.Puesto.Nombre,
                v.Filas,
                v.Puesto.PuestoId))
            .OrderBy(p => p.Empresa, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var puestosNuevos = nuevos.Values
            .Select(v => new PuestoCargaMasiva(
                v.Puesto.EmpresaId,
                catalogos.NombreEmpresa(v.Puesto.EmpresaId),
                v.Puesto.Nombre,
                v.Filas))
            .OrderBy(p => p.Empresa, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var puestosAmbiguos = ambiguos.Values
            .Select(v => new PuestoAmbiguoCargaMasiva(
                v.Puesto.EmpresaId,
                catalogos.NombreEmpresa(v.Puesto.EmpresaId),
                v.Puesto.Nombre,
                v.Filas,
                v.Puesto.Candidatos))
            .OrderBy(p => p.Empresa, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Valor, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var materialHuella = string.Join(
            '\n',
            existentes.Values
                .Select(v => $"E|{v.Puesto.EmpresaId}|{v.Puesto.Clave}|{v.Puesto.PuestoId}|{v.Filas}")
                .Concat(nuevos.Values.Select(
                    v => $"N|{v.Puesto.EmpresaId}|{v.Puesto.Clave}|{v.Puesto.Codigo}|{v.Filas}"))
                .Concat(ambiguos.Values.Select(
                    v => $"A|{v.Puesto.EmpresaId}|{v.Puesto.Clave}|{string.Join(',', v.Puesto.Candidatos)}|{v.Filas}"))
                .OrderBy(v => v, StringComparer.Ordinal));
        var huella = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(materialHuella)));

        return new ResumenPuestosCargaMasiva(
            puestosExistentes,
            puestosNuevos,
            puestosAmbiguos,
            huella);
    }

    private static void Incrementar<TKey>(
        IDictionary<TKey, (ResolucionPuesto Puesto, int Filas)> destino,
        TKey clave,
        ResolucionPuesto puesto)
        where TKey : notnull
    {
        destino[clave] = destino.TryGetValue(clave, out var actual)
            ? (actual.Puesto, actual.Filas + 1)
            : (puesto, 1);
    }

    private static async Task RevertirFilaAsync(
        MarcacionDbContext contexto,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaccion,
        string puntoGuardado,
        CancellationToken cancelacion)
    {
        await transaccion
            .RollbackToSavepointAsync(puntoGuardado, cancelacion)
            .ConfigureAwait(false);
        contexto.ChangeTracker.Clear();
    }

    private static void ExigirResolucion(ResolucionCatalogo resolucion, string entidad)
    {
        if (resolucion.Estado is EstadoResolucionCatalogo.Existente
            or EstadoResolucionCatalogo.Nuevo
            or EstadoResolucionCatalogo.NoAplica)
        {
            return;
        }

        var candidatos = resolucion.Candidatos.Count == 0
            ? string.Empty
            : " Candidatos: " + string.Join(", ", resolucion.Candidatos) + ".";
        throw new ReglaNegocioException(
            resolucion.Motivo ?? $"No se pudo resolver {entidad} '{resolucion.Nombre}'.{candidatos}");
    }

    private sealed class ConciliadorCarga
    {
        private readonly Dictionary<string, AcumuladoEntidad> _unidades = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AcumuladoEntidad> _equipos = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AcumuladoEntidad> _marcadores = new(StringComparer.Ordinal);
        private readonly List<DetalleFilaCargaMasiva> _filas = [];

        public void Unidad(int fila, string empresa, ResolucionCatalogo resolucion) =>
            Agregar(_unidades, empresa, resolucion, fila);

        public void Equipo(int fila, string unidad, ResolucionCatalogo resolucion)
        {
            if (resolucion.Estado != EstadoResolucionCatalogo.NoAplica)
            {
                Agregar(_equipos, unidad, resolucion, fila);
            }
        }

        public void Marcador(
            int fila, long equipoId, string codigo, string equipo, bool existe)
        {
            var resolucion = existe
                ? ResolucionCatalogo.Existente(0, codigo, codigo)
                : ResolucionCatalogo.Nueva(codigo, codigo);
            Agregar(_marcadores, $"{equipoId.ToString(CultureInfo.InvariantCulture)} · {equipo}", resolucion, fila);
        }

        public void Fila(
            int fila,
            string referencia,
            string empresa,
            string unidad,
            string? puesto,
            string? equipo,
            string? codigoMarcador,
            DateOnly? vigenteDesde,
            EstadoConciliacionCargaMasiva estado,
            string accion,
            string? motivo = null) =>
            _filas.Add(new DetalleFilaCargaMasiva(
                fila, referencia, empresa, unidad, puesto, equipo, codigoMarcador,
                vigenteDesde, estado, accion, motivo));

        public void FilaInvalida(int fila, string referencia, string motivo) =>
            Fila(
                fila,
                referencia,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                null,
                EstadoConciliacionCargaMasiva.Invalido,
                "Rechazar fila",
                motivo);

        public ResumenConciliacionCargaMasiva Construir(ResumenPuestosCargaMasiva puestos)
        {
            var unidades = Proyectar(_unidades);
            var equipos = Proyectar(_equipos);
            var marcadores = Proyectar(_marcadores);
            var material = string.Join(
                '\n',
                new[] { $"P|{puestos.Huella}" }
                    .Concat(Material("U", unidades))
                    .Concat(Material("E", equipos))
                    .Concat(Material("M", marcadores))
                    .Concat(_filas.Select(f =>
                        $"F|{f.Fila}|{f.Referencia}|{f.Estado}|{f.Accion}|{f.Motivo}"))
                    .OrderBy(v => v, StringComparer.Ordinal));
            var huella = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
            return new ResumenConciliacionCargaMasiva(
                unidades,
                equipos,
                marcadores,
                _filas.OrderBy(f => f.Fila).ToArray(),
                huella);
        }

        private static IEnumerable<string> Material(
            string prefijo,
            IEnumerable<EntidadConciliadaCargaMasiva> entidades) =>
            entidades.Select(e =>
                $"{prefijo}|{e.Clave}|{e.Nombre}|{e.Contexto}|{e.Estado}|{e.FilasAfectadas}|{e.Id}|{e.Motivo}");

        private static EntidadConciliadaCargaMasiva[] Proyectar(
            Dictionary<string, AcumuladoEntidad> origen) =>
            origen.Values
                .Select(a => new EntidadConciliadaCargaMasiva(
                    a.Clave,
                    a.Nombre,
                    a.Contexto,
                    a.Estado,
                    a.Filas.Count,
                    a.Id,
                    a.Motivo))
                .OrderBy(e => e.Contexto, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Nombre, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        private static void Agregar(
            Dictionary<string, AcumuladoEntidad> destino,
            string contexto,
            ResolucionCatalogo resolucion,
            int fila)
        {
            var estado = Convertir(resolucion.Estado);
            var claveVisible = string.IsNullOrWhiteSpace(resolucion.Codigo)
                ? resolucion.Nombre
                : resolucion.Codigo;
            var clave = $"{NormalizarCatalogo(contexto)}|{NormalizarCatalogo(claveVisible)}";
            if (!destino.TryGetValue(clave, out var acumulado))
            {
                acumulado = new AcumuladoEntidad(
                    claveVisible,
                    resolucion.Nombre,
                    contexto,
                    estado,
                    resolucion.Estado == EstadoResolucionCatalogo.Existente && resolucion.Id > 0
                        ? resolucion.Id
                        : null,
                    resolucion.Motivo,
                    []);
                destino.Add(clave, acumulado);
            }
            else if (acumulado.Estado == EstadoConciliacionCargaMasiva.Existente
                && estado == EstadoConciliacionCargaMasiva.NuevoPropuesto)
            {
                acumulado.Estado = estado;
                acumulado.Id = null;
            }

            acumulado.Filas.Add(fila);
        }

        private static EstadoConciliacionCargaMasiva Convertir(EstadoResolucionCatalogo estado) => estado switch
        {
            EstadoResolucionCatalogo.Existente => EstadoConciliacionCargaMasiva.Existente,
            EstadoResolucionCatalogo.Nuevo => EstadoConciliacionCargaMasiva.NuevoPropuesto,
            EstadoResolucionCatalogo.Ambiguo => EstadoConciliacionCargaMasiva.Ambiguo,
            EstadoResolucionCatalogo.Invalido => EstadoConciliacionCargaMasiva.Invalido,
            EstadoResolucionCatalogo.NoAplica => EstadoConciliacionCargaMasiva.NoAplica,
            EstadoResolucionCatalogo.PendienteDatos => EstadoConciliacionCargaMasiva.PendienteDatos,
            _ => throw new InvalidOperationException("Estado de conciliación desconocido.")
        };

        private sealed class AcumuladoEntidad(
            string clave,
            string nombre,
            string contexto,
            EstadoConciliacionCargaMasiva estado,
            long? id,
            string? motivo,
            HashSet<int> filas)
        {
            public string Clave { get; } = clave;
            public string Nombre { get; } = nombre;
            public string Contexto { get; } = contexto;
            public EstadoConciliacionCargaMasiva Estado { get; set; } = estado;
            public long? Id { get; set; } = id;
            public string? Motivo { get; } = motivo;
            public HashSet<int> Filas { get; } = filas;
        }
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
        var ejemplo = new Dictionary<Columna, string>
        {
            [Columna.TipoDocumento] = "DNI",
            [Columna.NumeroDocumento] = "09876543",
            [Columna.ApellidoPaterno] = "PEREZ",
            [Columna.ApellidoMaterno] = "QUISPE",
            [Columna.Nombres] = "JUAN CARLOS",
            [Columna.Sexo] = "M",
            [Columna.Telefono] = "987654321",
            [Columna.Direccion] = "AV. LOS OLIVOS 123",
            [Columna.Correo] = "jperez@ejemplo.pe",
            [Columna.TieneHijos] = "SI",
            [Columna.Empresa] = "CONSULTING ADVISERS S.A.C",
            [Columna.Unidad] = "PLANTA DETERGENTES LURÍN",
            [Columna.CodigoUnidad] = "DET",
            [Columna.TipoPlanilla] = "OBRERO",
            [Columna.Puesto] = "OPERARIO",
            [Columna.FechaIngreso] = "01/08/2026",
            [Columna.CentroCosto] = "PE21400307",
            [Columna.IdMarcador] = "0014",
            [Columna.Equipo] = "PRINCIPAL",
            [Columna.CodigoEquipo] = "EQ-DET-01",
            [Columna.NombreEquipo] = "Reloj principal",
            [Columna.SerieEquipo] = "SERIE-OPCIONAL",
            [Columna.SueldoBasico] = "1200.00",
            [Columna.Situacion] = "ACTIVO",
            [Columna.Area] = "Envasado Detergentes Lurín"
        };
        lineas.AppendLine(string.Join(
            ';',
            Enum.GetValues<Columna>().Select(c => ejemplo.GetValueOrDefault(c, string.Empty))));

        // Con BOM: sin él, Excel en español abre el archivo como Windows-1252 y la
        // plantilla de ejemplo aparece con los acentos rotos desde el primer momento.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(lineas.ToString())];
    }

    // ------------------------------------------------------------------

    private static SolicitudConPuestoPendiente Construir(
        string[] fila,
        MapaColumnas columnas,
        CatalogosResueltos catalogos,
        long empresa,
        long planta,
        long? equipo)
    {
        var fechaCese = LeerFechaOpcional(columnas.Valor(fila, Columna.FechaCese));

        RechazarSiNoEstaActivoNiCesado(columnas.Valor(fila, Columna.Situacion), fechaCese);

        var tipoDocumento = catalogos.TipoDocumento(
            EquivalenteTipoDocumento(
                Obligatorio(columnas, fila, Columna.TipoDocumento, "el tipo de documento")));

        var puesto = EquivalentePuesto(
            Obligatorio(columnas, fila, Columna.Puesto, "el puesto"));

        var solicitud = new GuardarTrabajadorRequest
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
            // Se completa después de conciliar el puesto. Cero nunca llega al servicio.
            PuestoId = 0,
            FechaIngreso = LeerFecha(Obligatorio(columnas, fila, Columna.FechaIngreso, "la fecha de ingreso")),
            FechaCese = fechaCese,
            MotivoCese = LeerMotivoCese(columnas.Valor(fila, Columna.MotivoCese)),
            CentroCostoId = catalogos.CentroCostoOpcional(columnas.Valor(fila, Columna.CentroCosto), empresa, planta),
            IdMarcador = columnas.Valor(fila, Columna.IdMarcador),
            EquipoId = equipo,
            TurnoId = catalogos.TurnoOpcional(columnas.Valor(fila, Columna.Turno)),
            MontoSueldoBasico = LeerDecimalOpcional(columnas.Valor(fila, Columna.SueldoBasico))
        };

        return new SolicitudConPuestoPendiente(solicitud, puesto);
    }

    private sealed record SolicitudConPuestoPendiente(
        GuardarTrabajadorRequest Solicitud,
        string ValorPuesto);

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

    /// <summary>
    /// Clave exclusiva de puestos: recorta, colapsa espacios y compara sin mayúsculas,
    /// pero conserva tildes y los demás caracteres significativos.
    /// </summary>
    internal static string NormalizarPuesto(string? valor) =>
        LimpiarPuestoVisible(valor).ToUpperInvariant().Normalize(NormalizationForm.FormC);

    /// <summary>
    /// Identidad estable de catálogos creados desde una plantilla. Conserva tildes y
    /// caracteres significativos; solo normaliza mayúsculas y espacios.
    /// </summary>
    internal static string NormalizarCatalogo(string? valor) =>
        LimpiarCatalogoVisible(valor).ToUpperInvariant().Normalize(NormalizationForm.FormC);

    internal static string LimpiarCatalogoVisible(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? string.Empty
            : string.Join(' ', valor.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    internal static string LimpiarPuestoVisible(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? string.Empty
            : string.Join(' ', valor.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    internal static bool EsPuestoSentinela(string clave) =>
        clave is "SIN PUESTO" or "VACÍO" or "VACIO" or "N/A" or "DESCONOCIDO";

    /// <summary>
    /// El índice físico actual es global por código. Una clave determinista que incluye
    /// empresa permite que ese mismo índice arbitre reintentos y concurrencia sin migrar.
    /// </summary>
    internal static string CodigoPuestoImportado(long empresaId, string clave)
    {
        var material = $"{empresaId.ToString(CultureInfo.InvariantCulture)}\u001F{clave}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));

        return $"PUE-{Convert.ToHexString(hash.AsSpan(0, 8))}";
    }
}
