using System.Globalization;
using ControlMarcacion.Application.Auditoria;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Mantenimiento de personal.
/// </summary>
/// <remarks>
/// El controlador valida, delega en el servicio de aplicación y arma el modelo de la
/// vista. Sin lógica de negocio y sin acceso al DbContext.
///
/// El atributo cubre también los endpoints JSON de cascada (plantas, áreas, secciones,
/// turnos): son consultas al padrón organizativo y no deben quedar abiertas por el solo
/// hecho de no tener vista propia.
/// </remarks>
[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador}")]
public sealed class TrabajadoresController(
    ServicioTrabajadores servicio,
    IConsultaCatalogos catalogos,
    ICargaMasivaTrabajadores cargaMasiva,
    IConsultaAuditoria auditoria,
    IMemoryCache cache) : Controller
{
    /// <summary>
    /// Tamaño máximo del CSV de carga masiva. El padrón completo de Intradevco en texto
    /// no llega a 200 KB.
    /// </summary>
    private const long TamanioMaximoCargaBytes = 5 * 1024 * 1024;
    private static readonly TimeSpan VigenciaPrevisualizacion = TimeSpan.FromMinutes(20);

    [HttpGet]
    public async Task<IActionResult> Index(FiltroTrabajadores filtro, CancellationToken cancelacion)
    {
        var resultado = await servicio.ListarAsync(filtro, cancelacion).ConfigureAwait(false);

        // Centro depende de Planta, igual que en el formulario de alta: mientras no
        // haya una planta elegida en el filtro, no hay centros que ofrecer.
        var empresaDePlanta = await ResolverEmpresaDePlantaAsync(filtro.PlantaId, cancelacion).ConfigureAwait(false);

        var modelo = new ListadoTrabajadoresVm
        {
            Filtro = filtro,
            Resultado = resultado,
            Empresas = await catalogos.EmpresasAsync(cancelacion).ConfigureAwait(false),
            Plantas = await catalogos.PlantasAsync(filtro.EmpresaId, cancelacion).ConfigureAwait(false),
            Centros = filtro.PlantaId is null
                ? []
                : await catalogos.CentrosAsync(empresaDePlanta, cancelacion).ConfigureAwait(false),
            CentrosCosto = await catalogos.CentrosCostoAsync(filtro.CentroId, cancelacion).ConfigureAwait(false),
            Equipos = [.. (await catalogos.EquiposActivosAsync(cancelacion).ConfigureAwait(false))
                .Where(e => filtro.PlantaId is null || e.PlantaId == filtro.PlantaId)],
            Puestos = await catalogos.PuestosAsync(filtro.EmpresaId, cancelacion).ConfigureAwait(false),
            Areas = filtro.PlantaId is null
                ? []
                : await catalogos.AreasAsync(filtro.PlantaId, cancelacion).ConfigureAwait(false),
            Turnos = await catalogos.TurnosAsync(filtro.PlantaId, cancelacion).ConfigureAwait(false)
        };

        return View(modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Crear(CancellationToken cancelacion)
    {
        var modelo = new EditarTrabajadorVm
        {
            Datos = new GuardarTrabajadorRequest
            {
                FechaIngreso = DateOnly.FromDateTime(DateTime.Now)
            }
        };

        await CargarCatalogosAsync(modelo, cancelacion).ConfigureAwait(false);

        return View("Editar", modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Editar(long id, CancellationToken cancelacion)
    {
        TrabajadorDetalleDto detalle;
        TrabajadorPanelDto panel;

        try
        {
            detalle = await servicio.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

            // El sueldo básico vigente no está en el detalle: vive en una asignación con
            // vigencia por fecha y lo resuelve la consulta del panel. Sin esta segunda
            // lectura el campo aparecería vacío en un trabajador que sí tiene sueldo, y
            // quien lo viera pensaría que falta capturarlo.
            panel = await servicio.ObtenerPanelAsync(id, cancelacion).ConfigureAwait(false);
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        var modelo = new EditarTrabajadorVm
        {
            Datos = MapearAFormulario(detalle, panel.SueldoBasicoMensual),
            CentroId = detalle.CentroId
        };

        await CargarCatalogosAsync(modelo, cancelacion).ConfigureAwait(false);

        return View(modelo);
    }

    /// <summary>
    /// Búsqueda en vivo del panel flotante: documento, apellidos, nombres o el ID de
    /// marcador con asignación vigente. Máximo 50 resultados.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> BuscarRapido(string? texto, CancellationToken cancelacion) =>
        Json(await servicio.BuscarRapidoAsync(texto, cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Contenido del panel flotante lateral, en modo "ver" o "editar". Carga a la vez
    /// la ficha resuelta y el formulario de edición para que alternar entre los dos
    /// modos no necesite un segundo viaje al servidor.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Panel(long id, string? modo, CancellationToken cancelacion)
    {
        TrabajadorPanelDto panel;
        TrabajadorDetalleDto detalle;

        try
        {
            panel = await servicio.ObtenerPanelAsync(id, cancelacion).ConfigureAwait(false);
            detalle = await servicio.ObtenerAsync(id, cancelacion).ConfigureAwait(false);
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        var modelo = new PanelTrabajadorVm
        {
            Modo = string.Equals(modo, "editar", StringComparison.OrdinalIgnoreCase) ? "editar" : "ver",
            Detalle = panel,
            Datos = MapearAFormulario(detalle, panel.SueldoBasicoMensual),
            CentroId = detalle.CentroId
        };

        await CargarCatalogosAsync(modelo, cancelacion).ConfigureAwait(false);

        return PartialView("_PanelTrabajador", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(EditarTrabajadorVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        // El panel flotante guarda por AJAX y no debe navegar: se detecta la petición
        // por este encabezado (lo agrega panel-trabajador.js) y se responde JSON en vez
        // de redirigir o de devolver la vista completa.
        var esPeticionAjax = string.Equals(
            Request.Headers["X-Requested-With"],
            "XMLHttpRequest",
            StringComparison.Ordinal);

        if (ModelState.IsValid)
        {
            try
            {
                // El usuario autenticado se resolverá con Identity en la fase 2.
                var usuario = User.Identity?.Name ?? "sistema";

                // La ficha escribe el número que el trabajador tiene en el aparato: si no está
                // en el catálogo, se registra en vez de rechazar. Lo fija el servidor, no un
                // campo del formulario.
                var datos = modelo.Datos with { RegistrarMarcadorSiFalta = true };

                if (datos.Id is null)
                {
                    var id = await servicio.CrearAsync(datos, usuario, cancelacion).ConfigureAwait(false);

                    if (esPeticionAjax)
                    {
                        return Json(new { ok = true, mensaje = "Trabajador registrado correctamente." });
                    }

                    TempData["Mensaje"] = "Trabajador registrado correctamente.";

                    return RedirectToAction(nameof(Editar), new { id });
                }

                await servicio.ActualizarAsync(datos, usuario, cancelacion).ConfigureAwait(false);

                if (esPeticionAjax)
                {
                    return Json(new { ok = true, mensaje = "Cambios guardados correctamente." });
                }

                TempData["Mensaje"] = "Cambios guardados correctamente.";

                return RedirectToAction(nameof(Editar), new { id = modelo.Datos.Id });
            }
            catch (ReglaNegocioException excepcion)
            {
                if (esPeticionAjax)
                {
                    return Json(new { ok = false, mensaje = excepcion.Message });
                }

                // Los mensajes de regla de negocio están redactados para el usuario final,
                // así que se muestran tal cual en el formulario.
                ModelState.AddModelError(string.Empty, excepcion.Message);
            }
            catch (RecursoNoEncontradoException)
            {
                if (esPeticionAjax)
                {
                    return Json(new { ok = false, mensaje = "No se encontró el trabajador." });
                }

                return NotFound();
            }
        }

        if (esPeticionAjax)
        {
            var errores = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(m => !string.IsNullOrWhiteSpace(m));

            return Json(new { ok = false, mensaje = string.Join(" ", errores) });
        }

        await CargarCatalogosAsync(modelo, cancelacion).ConfigureAwait(false);

        return View("Editar", modelo);
    }

    /// <summary>
    /// Historial por rangos de fecha: vínculos laborales, sueldo, ID de marcador y turno,
    /// más la bitácora de auditoría de la ficha si quien mira es coordinador.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Historial(long id, CancellationToken cancelacion)
    {
        HistorialTrabajadorDto historial;

        try
        {
            historial = await servicio.ObtenerHistorialAsync(id, cancelacion).ConfigureAwait(false);
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        var esCoordinador = User.IsInRole(RolesSistema.Coordinador);

        return View(new HistorialTrabajadorVm
        {
            Historial = historial,
            MuestraAuditoria = esCoordinador,
            Auditoria = esCoordinador
                ? await auditoria
                    .DeRegistroAsync("trabajador", id.ToString(CultureInfo.InvariantCulture), 100, cancelacion)
                    .ConfigureAwait(false)
                : []
        });
    }

    /// <summary>
    /// Formulario de baja. Es una pantalla propia y no un campo de la ficha: exige
    /// motivo, cierra las asignaciones vigentes y es lo único que habilita mover al
    /// trabajador a la otra razón social.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Baja(long id, CancellationToken cancelacion)
    {
        TrabajadorPanelDto panel;

        try
        {
            panel = await servicio.ObtenerPanelAsync(id, cancelacion).ConfigureAwait(false);
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        if (!panel.EstaActivo)
        {
            TempData["Error"] =
                $"{panel.ApellidosYNombres} ya tiene cese registrado el {panel.FechaCese:dd/MM/yyyy}.";

            return RedirectToAction(nameof(Index));
        }

        return View(new BajaTrabajadorVm
        {
            ApellidosYNombres = panel.ApellidosYNombres,
            NumeroDocumento = panel.NumeroDocumento,
            FechaIngreso = panel.FechaIngreso,
            Datos = new BajaTrabajadorRequest
            {
                TrabajadorId = id,
                FechaCese = DateOnly.FromDateTime(DateTime.Now)
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarBaja(BajaTrabajadorVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "sistema";

                await servicio.DarDeBajaAsync(modelo.Datos, usuario, cancelacion).ConfigureAwait(false);

                TempData["Mensaje"] = "Se registró la baja del trabajador.";

                return RedirectToAction(nameof(Historial), new { id = modelo.Datos.TrabajadorId });
            }
            catch (ReglaNegocioException excepcion)
            {
                ModelState.AddModelError(string.Empty, excepcion.Message);
            }
            catch (RecursoNoEncontradoException)
            {
                return NotFound();
            }
        }

        // Se recarga la cabecera: el formulario la muestra y el POST solo trae los datos
        // de la baja.
        try
        {
            var panel = await servicio
                .ObtenerPanelAsync(modelo.Datos.TrabajadorId, cancelacion)
                .ConfigureAwait(false);

            return View("Baja", new BajaTrabajadorVm
            {
                Datos = modelo.Datos,
                ApellidosYNombres = panel.ApellidosYNombres,
                NumeroDocumento = panel.NumeroDocumento,
                FechaIngreso = panel.FechaIngreso
            });
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Formulario mínimo para abrir un nuevo vínculo. La disponibilidad se consulta en
    /// la historia canónica; <c>FechaCese</c> no decide si se muestra o no esta operación.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Reingresar(long id, CancellationToken cancelacion)
    {
        HistorialTrabajadorDto historial;

        try
        {
            historial = await servicio.ObtenerHistorialAsync(id, cancelacion).ConfigureAwait(false);
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        if (historial.PeriodosLaborales.Any(p => p.EstaAbierto))
        {
            TempData["Error"] =
                $"{historial.ApellidosYNombres} ya tiene un vínculo laboral abierto.";

            return RedirectToAction(nameof(Historial), new { id });
        }

        var ultimoCese = historial.PeriodosLaborales
            .Where(p => p.FechaCese is not null)
            .Max(p => p.FechaCese);
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var fechaSugerida = ultimoCese is { } cese && cese.AddDays(1) > hoy
            ? cese.AddDays(1)
            : hoy;
        var modelo = new ReingresarTrabajadorVm
        {
            ApellidosYNombres = historial.ApellidosYNombres,
            NumeroDocumento = historial.NumeroDocumento,
            UltimoCese = ultimoCese,
            Datos = new ReingresarTrabajadorRequest
            {
                TrabajadorId = id,
                FechaIngreso = fechaSugerida
            }
        };

        await CargarCatalogosAsync(modelo, cancelacion).ConfigureAwait(false);

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarReingreso(
        ReingresarTrabajadorVm modelo,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "sistema";
                await servicio
                    .ReingresarAsync(modelo.Datos, usuario, cancelacion)
                    .ConfigureAwait(false);

                TempData["Mensaje"] = "Se registró el reingreso del trabajador.";

                return RedirectToAction(
                    nameof(Historial),
                    new { id = modelo.Datos.TrabajadorId });
            }
            catch (ReglaNegocioException excepcion)
            {
                ModelState.AddModelError(string.Empty, excepcion.Message);
            }
            catch (RecursoNoEncontradoException)
            {
                return NotFound();
            }
        }

        try
        {
            await CompletarModeloReingresoAsync(modelo, cancelacion).ConfigureAwait(false);
            return View("Reingresar", modelo);
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }
    }

    /// <summary>Pantalla de carga masiva de trabajadores desde CSV.</summary>
    [HttpGet]
    public IActionResult CargaMasiva() => View(new CargaMasivaVm());

    /// <summary>
    /// Plantilla CSV de ejemplo con el encabezado que espera la carga. Es la
    /// documentación del formato que de verdad se lee.
    /// </summary>
    [HttpGet]
    public IActionResult PlantillaCargaMasiva() =>
        File(cargaMasiva.GenerarPlantillaCsv(), "text/csv", "plantilla-trabajadores.csv");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(TamanioMaximoCargaBytes)]
    public async Task<IActionResult> ProcesarCargaMasiva(
        IFormFile? archivo,
        bool actualizarExistentes,
        bool registrarMarcadores,
        CancellationToken cancelacion)
    {
        if (archivo is null || archivo.Length == 0)
        {
            TempData["Error"] = "Seleccione un archivo CSV o Excel antes de continuar.";

            return RedirectToAction(nameof(CargaMasiva));
        }

        if (archivo.Length > TamanioMaximoCargaBytes)
        {
            TempData["Error"] =
                $"El archivo pesa {archivo.Length / 1024d / 1024d:0.0} MB y el máximo es " +
                $"{TamanioMaximoCargaBytes / 1024 / 1024} MB.";

            return RedirectToAction(nameof(CargaMasiva));
        }

        using var contenido = new MemoryStream();
        await archivo.CopyToAsync(contenido, cancelacion).ConfigureAwait(false);

        try
        {
            var usuario = User.Identity?.Name ?? "sistema";

            var resultado = await cargaMasiva
                .PrevisualizarAsync(
                    contenido, archivo.FileName, usuario, actualizarExistentes,
                    registrarMarcadores, cancelacion)
                .ConfigureAwait(false);

            string? token = null;

            if (resultado.Puestos.Ambiguos.Count == 0
                && !resultado.Conciliacion.TieneBloqueos
                && resultado.FilasLeidas > resultado.Rechazados)
            {
                token = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                cache.Set(
                    ClaveCache(token),
                    new CargaMasivaPendiente(
                        contenido.ToArray(),
                        archivo.FileName,
                        usuario,
                        actualizarExistentes,
                        registrarMarcadores,
                        resultado.HuellaConfirmacion),
                    VigenciaPrevisualizacion);
            }

            return View(
                "CargaMasiva",
                new CargaMasivaVm { Resultado = resultado, TokenConfirmacion = token });
        }
        catch (ReglaNegocioException excepcion)
        {
            // Falla del archivo completo (encabezado inválido, vacío, demasiado grande),
            // distinta de los errores por fila que van dentro del resultado.
            TempData["Error"] = excepcion.Message;

            return RedirectToAction(nameof(CargaMasiva));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarCargaMasiva(
        string? token,
        CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(token)
            || !cache.TryGetValue<CargaMasivaPendiente>(ClaveCache(token), out var pendiente)
            || pendiente is null)
        {
            TempData["Error"] =
                "La previsualización venció o ya fue utilizada. Vuelva a analizar el archivo.";
            return RedirectToAction(nameof(CargaMasiva));
        }

        cache.Remove(ClaveCache(token));
        var usuario = User.Identity?.Name ?? "sistema";

        if (!string.Equals(pendiente.Usuario, usuario, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        try
        {
            using var contenido = new MemoryStream(pendiente.Contenido, writable: false);
            var resultado = await cargaMasiva
                .ConfirmarAsync(
                    contenido,
                    pendiente.NombreArchivo,
                    usuario,
                    pendiente.HuellaConfirmacion,
                    pendiente.ActualizarExistentes,
                    pendiente.RegistrarMarcadores,
                    cancelacion)
                .ConfigureAwait(false);

            return View("CargaMasiva", new CargaMasivaVm { Resultado = resultado });
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
            return RedirectToAction(nameof(CargaMasiva));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CancelarCargaMasiva(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            cache.Remove(ClaveCache(token));
        }

        TempData["Mensaje"] = "La importación fue cancelada. No se guardó ningún cambio.";
        return RedirectToAction(nameof(CargaMasiva));
    }

    private static string ClaveCache(string token) => $"carga-masiva:{token}";

    private sealed record CargaMasivaPendiente(
        byte[] Contenido,
        string NombreArchivo,
        string Usuario,
        bool ActualizarExistentes,
        bool RegistrarMarcadores,
        string HuellaConfirmacion);

    /// <summary>Plantas de una empresa, para encadenar los desplegables del formulario.</summary>
    [HttpGet]
    public async Task<IActionResult> PlantasDeEmpresa(long empresaId, CancellationToken cancelacion) =>
        Json(await catalogos.PlantasAsync(Opcional(empresaId), cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Aparatos marcadores activos de una planta, para la cascada Planta → aparato del
    /// formulario.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EquiposDePlanta(long plantaId, CancellationToken cancelacion) =>
        Json(await AparatosDePlantaAsync(Opcional(plantaId), cancelacion).ConfigureAwait(false));

    private async Task<IReadOnlyList<EquipoOpcionDto>> AparatosDePlantaAsync(
        long? plantaId,
        CancellationToken cancelacion) =>
        plantaId is null
            ? []
            : [.. (await catalogos.EquiposActivosAsync(cancelacion).ConfigureAwait(false))
                .Where(e => e.PlantaId == plantaId)];

    /// <summary>Áreas de una planta.</summary>
    [HttpGet]
    public async Task<IActionResult> AreasDePlanta(long plantaId, CancellationToken cancelacion) =>
        Json(await catalogos.AreasAsync(Opcional(plantaId), cancelacion).ConfigureAwait(false));

    /// <summary>Secciones de un área.</summary>
    [HttpGet]
    public async Task<IActionResult> SeccionesDeArea(long areaId, CancellationToken cancelacion) =>
        Json(await catalogos.SeccionesAsync(Opcional(areaId), cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Turnos habilitados en una planta. Depende de la planta porque el turno especial
    /// de sábado solo existe en Planta 01.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TurnosDePlanta(long plantaId, CancellationToken cancelacion) =>
        Json(await catalogos.TurnosAsync(Opcional(plantaId), cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Centros SAP de la planta indicada, para encadenar la cascada Planta → Centro →
    /// Centro de costo del formulario: el usuario elige primero la planta y solo
    /// entonces se habilitan sus centros. La empresa dueña de la planta se resuelve
    /// aquí, en el servidor (<c>Planta.EmpresaId</c>), en vez de pedirle al cliente
    /// que la recuerde.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CentrosDePlanta(long plantaId, CancellationToken cancelacion)
    {
        var empresaId = await catalogos.EmpresaDePlantaAsync(plantaId, cancelacion).ConfigureAwait(false);

        if (empresaId is null)
        {
            return Json(Array.Empty<object>());
        }

        return Json(await catalogos.CentrosAsync(empresaId, cancelacion).ConfigureAwait(false));
    }

    /// <summary>Centros de costo de un centro SAP.</summary>
    [HttpGet]
    public async Task<IActionResult> CentrosCostoDeCentro(long centroId, CancellationToken cancelacion) =>
        Json(await catalogos.CentrosCostoAsync(Opcional(centroId), cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Puestos de una empresa: incluye los universales (sin empresa asignada, como
    /// los 4 sembrados) más los propios de esa empresa si GCA llega a entregarlos.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> PuestosDeEmpresa(long empresaId, CancellationToken cancelacion) =>
        Json(await catalogos.PuestosAsync(Opcional(empresaId), cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Convierte el 0 de un desplegable sin elegir en <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Los catálogos interpretan <c>null</c> como "sin filtro" y cualquier otro valor como
    /// un identificador real. Pasar el 0 tal cual filtraba por un id inexistente y dejaba
    /// las listas vacías en el alta de un trabajador nuevo.
    /// </remarks>
    private static long? Opcional(long id) => id == 0 ? null : id;

    /// <summary>
    /// Resuelve la empresa dueña de una planta, para precargar Centro en la cascada
    /// Planta → Centro al abrir un formulario con una planta ya elegida. Nulo si no
    /// hay planta elegida todavía: en ese caso Centro debe quedar vacío, no mostrar
    /// los centros de las dos empresas mezclados.
    /// </summary>
    private async Task<long?> ResolverEmpresaDePlantaAsync(long? plantaId, CancellationToken cancelacion) =>
        plantaId is null
            ? null
            : await catalogos.EmpresaDePlantaAsync(plantaId.Value, cancelacion).ConfigureAwait(false);

    private async Task CargarCatalogosAsync(EditarTrabajadorVm modelo, CancellationToken cancelacion)
    {
        var empresaId = Opcional(modelo.Datos.EmpresaId);
        var plantaId = Opcional(modelo.Datos.PlantaId);

        modelo.TiposDocumento = await catalogos.TiposDocumentoAsync(cancelacion).ConfigureAwait(false);
        modelo.Empresas = await catalogos.EmpresasAsync(cancelacion).ConfigureAwait(false);
        modelo.Plantas = await catalogos.PlantasAsync(empresaId, cancelacion).ConfigureAwait(false);
        // Puesto depende de Empresa: sin empresa elegida, no hay nada que ofrecer.
        modelo.Puestos = empresaId is null
            ? []
            : await catalogos.PuestosAsync(empresaId, cancelacion).ConfigureAwait(false);
        modelo.Turnos = await catalogos.TurnosAsync(plantaId, cancelacion).ConfigureAwait(false);
        modelo.Cuadrillas = await catalogos.CuadrillasActivasAsync(cancelacion).ConfigureAwait(false);
        // Centro depende de Planta, no de Empresa directamente: sin planta elegida,
        // no hay nada que ofrecer.
        modelo.Centros = plantaId is null
            ? []
            : await catalogos.CentrosAsync(
                await ResolverEmpresaDePlantaAsync(plantaId, cancelacion).ConfigureAwait(false),
                cancelacion).ConfigureAwait(false);
        modelo.CentrosCosto = await catalogos.CentrosCostoAsync(Opcional(modelo.CentroId ?? 0), cancelacion).ConfigureAwait(false);
        // Sin planta elegida no hay aparatos que ofrecer: cada uno pertenece a una.
        modelo.EquiposMarcador = await AparatosDePlantaAsync(plantaId, cancelacion).ConfigureAwait(false);
    }

    private async Task CargarCatalogosAsync(PanelTrabajadorVm modelo, CancellationToken cancelacion)
    {
        var empresaId = Opcional(modelo.Datos.EmpresaId);
        var plantaId = Opcional(modelo.Datos.PlantaId);

        modelo.TiposDocumento = await catalogos.TiposDocumentoAsync(cancelacion).ConfigureAwait(false);
        modelo.Empresas = await catalogos.EmpresasAsync(cancelacion).ConfigureAwait(false);
        modelo.Plantas = await catalogos.PlantasAsync(empresaId, cancelacion).ConfigureAwait(false);
        modelo.Puestos = empresaId is null
            ? []
            : await catalogos.PuestosAsync(empresaId, cancelacion).ConfigureAwait(false);
        modelo.Turnos = await catalogos.TurnosAsync(plantaId, cancelacion).ConfigureAwait(false);
        modelo.Cuadrillas = await catalogos.CuadrillasActivasAsync(cancelacion).ConfigureAwait(false);
        modelo.Centros = plantaId is null
            ? []
            : await catalogos.CentrosAsync(
                await ResolverEmpresaDePlantaAsync(plantaId, cancelacion).ConfigureAwait(false),
                cancelacion).ConfigureAwait(false);
        modelo.CentrosCosto = await catalogos.CentrosCostoAsync(Opcional(modelo.CentroId ?? 0), cancelacion).ConfigureAwait(false);
        // Sin planta elegida no hay aparatos que ofrecer: cada uno pertenece a una.
        modelo.EquiposMarcador = await AparatosDePlantaAsync(plantaId, cancelacion).ConfigureAwait(false);
    }

    private async Task CargarCatalogosAsync(ReingresarTrabajadorVm modelo, CancellationToken cancelacion)
    {
        modelo.Empresas = await catalogos.EmpresasAsync(cancelacion).ConfigureAwait(false);
        modelo.Plantas = await catalogos
            .PlantasAsync(Opcional(modelo.Datos.EmpresaId), cancelacion)
            .ConfigureAwait(false);
    }

    private async Task CompletarModeloReingresoAsync(
        ReingresarTrabajadorVm modelo,
        CancellationToken cancelacion)
    {
        var historial = await servicio
            .ObtenerHistorialAsync(modelo.Datos.TrabajadorId, cancelacion)
            .ConfigureAwait(false);

        modelo.ApellidosYNombres = historial.ApellidosYNombres;
        modelo.NumeroDocumento = historial.NumeroDocumento;
        modelo.UltimoCese = historial.PeriodosLaborales
            .Where(p => p.FechaCese is not null)
            .Max(p => p.FechaCese);
        await CargarCatalogosAsync(modelo, cancelacion).ConfigureAwait(false);
    }

    /// <summary>
    /// Traslada la ficha de solo lectura al formulario editable. La usan tanto la
    /// pantalla de página completa como el panel flotante, para no repetir el mapeo
    /// campo por campo en los dos sitios.
    /// </summary>
    private static GuardarTrabajadorRequest MapearAFormulario(
        TrabajadorDetalleDto detalle,
        decimal? montoSueldoBasico = null) =>
        new()
        {
            Id = detalle.Id,
            TipoDocumentoId = detalle.TipoDocumentoId,
            NumeroDocumento = detalle.NumeroDocumento,
            ApellidoPaterno = detalle.ApellidoPaterno,
            ApellidoMaterno = detalle.ApellidoMaterno,
            Nombres = detalle.Nombres,
            Sexo = detalle.Sexo,
            Telefono = detalle.Telefono,
            Direccion = detalle.Direccion,
            CorreoElectronico = detalle.CorreoElectronico,
            TieneHijos = detalle.TieneHijos,
            EmpresaId = detalle.EmpresaId,
            Unidad = detalle.Unidad,
            TipoPlanilla = detalle.TipoPlanilla,
            PlantaId = detalle.PlantaId,
            AreaId = detalle.AreaId,
            SeccionId = detalle.SeccionId,
            FechaIngreso = detalle.FechaIngreso,
            FechaCese = detalle.FechaCese,
            PuestoId = detalle.PuestoId,
            CentroCostoId = detalle.CentroCostoId,
            IdMarcador = detalle.IdMarcador,
            EquipoId = detalle.EquipoMarcadorId,
            TurnoId = detalle.TurnoId,
            CuadrillaId = detalle.CuadrillaId,
            MontoSueldoBasico = montoSueldoBasico,
            MotivoCese = detalle.MotivoCese
        };
}
