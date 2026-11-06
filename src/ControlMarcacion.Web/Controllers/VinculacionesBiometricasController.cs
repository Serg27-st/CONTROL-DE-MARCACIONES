using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

[Authorize(Roles = RolesSistema.Coordinador)]
public sealed class VinculacionesBiometricasController(
    IConsultaCatalogos catalogos,
    IConsultaTrabajadores trabajadores,
    ILectorUsuariosZkteco lector,
    IServicioConfirmacionVinculacionBiometrica confirmacion) : Controller
{
    private const long TamanioMaximoArchivo = 5 * 1024 * 1024;
    private const long TamanioMaximoPeticion = TamanioMaximoArchivo + 64 * 1024;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancelacion) =>
        View(await CargarEquiposAsync(new VinculacionBiometricaIndexVm(), cancelacion).ConfigureAwait(false));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(TamanioMaximoPeticion)]
    public async Task<IActionResult> Previsualizar(
        VinculacionBiometricaIndexVm modelo,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        var equipos = await catalogos.EquiposActivosAsync(cancelacion).ConfigureAwait(false);
        var equipo = equipos.SingleOrDefault(e => e.Id == modelo.EquipoId);

        if (equipo is null || equipo.PlantaId != modelo.PlantaId)
        {
            ModelState.AddModelError(nameof(modelo.EquipoId), "El equipo no pertenece a la unidad autorizada seleccionada.");
        }
        if (modelo.Archivo is { } archivo)
        {
            if (archivo.Length == 0 || archivo.Length > TamanioMaximoArchivo)
            {
                ModelState.AddModelError(nameof(modelo.Archivo), "El respaldo debe tener contenido y no superar 5 MB.");
            }
            if (!string.Equals(Path.GetFileName(archivo.FileName), "user.u", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(modelo.Archivo), "Seleccione el archivo user.u sin renombrarlo.");
            }
        }
        if (modelo.FechaFin is { } fin && fin < modelo.FechaInicio)
        {
            ModelState.AddModelError(nameof(modelo.FechaFin), "La fecha final no puede ser anterior a la efectiva.");
        }

        if (!ModelState.IsValid || equipo is null)
        {
            modelo.Equipos = equipos;
            return View("Index", modelo);
        }

        try
        {
            ResultadoLecturaUsuariosZkteco lectura;
            await using (var flujo = modelo.Archivo!.OpenReadStream())
            {
                lectura = lector.Leer(
                    flujo,
                    new EquipoOrigenUsuariosZkteco(equipo.Id, equipo.Codigo),
                    new OpcionesLecturaUsuariosZkteco(MaximoBytes: (int)TamanioMaximoArchivo));
            }

            if (lectura.Estado == EstadoArchivoUsuariosZkteco.Rechazado)
            {
                foreach (var error in lectura.Errores.DefaultIfEmpty("El respaldo user.u está dañado o no es compatible."))
                {
                    ModelState.AddModelError(nameof(modelo.Archivo), error);
                }
                modelo.Equipos = equipos;
                return View("Index", modelo);
            }

            var propuesta = await confirmacion.PrepararAsync(
                    new PrepararConfirmacionBiometricaRequest(
                        equipo.Id,
                        lectura,
                        modelo.FechaInicio,
                        modelo.FechaFin,
                        []),
                    cancelacion)
                .ConfigureAwait(false);

            var nombres = await ResolverNombresAsync(propuesta, cancelacion).ConfigureAwait(false);
            return View(new PrevisualizacionVinculacionBiometricaVm
            {
                ConciliacionId = propuesta.ConciliacionId,
                Version = propuesta.Version,
                ExpiraEn = propuesta.ExpiraEn,
                Equipo = $"{equipo.Codigo} — {equipo.Nombre}",
                Planta = equipo.Planta,
                Formato = lectura.FormatoDetectado,
                HuellaSha256 = lectura.HuellaContenidoSha256,
                TamanoBytes = lectura.TamanoBytes,
                TotalUsuarios = lectura.Usuarios.Count,
                Filas = propuesta.Decisiones.Select(d => new FilaVinculacionBiometricaVm(
                    d.DecisionId,
                    d.IndiceRegistro,
                    d.Pin,
                    d.DocumentoCandidato ?? "—",
                    d.TrabajadorId is { } id ? nombres.GetValueOrDefault(id, "Trabajador identificado") : "—",
                    d.BaseConfirmacionIdentidad.ToString(),
                    d.NaturalezaVigencia.ToString(),
                    d.FechaInicio,
                    d.FechaFin,
                    d.Resultado,
                    d.Motivo,
                    d.Diagnosticos,
                    d.Resultado == "Pendiente")).ToArray()
            });
        }
        catch (ReglaNegocioException excepcion)
        {
            ModelState.AddModelError(string.Empty, excepcion.Message);
            modelo.Equipos = equipos;
            return View("Index", modelo);
        }
        catch (Exception excepcion) when (excepcion is IOException or InvalidDataException)
        {
            ModelState.AddModelError(nameof(modelo.Archivo), "No se pudo leer el respaldo user.u. Descárguelo nuevamente del equipo.");
            modelo.Equipos = equipos;
            return View("Index", modelo);
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Revisar(RevisarVinculacionesBiometricasVm modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        modelo.Decisiones = modelo.Decisiones.Where(id => id > 0).Distinct().ToArray();
        if (modelo.ConciliacionId == Guid.Empty || modelo.Version <= 0 || modelo.Decisiones.Length == 0)
        {
            TempData["Error"] = "Seleccione al menos una coincidencia confirmable.";
            return RedirectToAction(nameof(Index));
        }
        return View("Confirmar", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar(
        RevisarVinculacionesBiometricasVm modelo,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        var ids = modelo.Decisiones.Where(id => id > 0).Distinct().ToArray();
        if (modelo.ConciliacionId == Guid.Empty || modelo.Version <= 0 || ids.Length == 0)
        {
            TempData["Error"] = "La propuesta es inválida o no contiene decisiones.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var resultado = await confirmacion.ConfirmarLoteAsync(
                    new ConfirmarVinculacionesBiometricasRequest(modelo.ConciliacionId, modelo.Version, ids),
                    cancelacion)
                .ConfigureAwait(false);
            return View("Resultado", new ResultadoVinculacionBiometricaVm
            {
                ConciliacionId = resultado.ConciliacionId,
                Version = resultado.Version,
                Resultados = resultado.Resultados.Select(r => new FilaResultadoVinculacionBiometricaVm(
                    r.DecisionId, r.Resultado, r.Motivo, r.AsignacionMarcadorId)).ToArray()
            });
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }
    }

    private async Task<VinculacionBiometricaIndexVm> CargarEquiposAsync(
        VinculacionBiometricaIndexVm modelo,
        CancellationToken cancelacion)
    {
        modelo.Equipos = await catalogos.EquiposActivosAsync(cancelacion).ConfigureAwait(false);
        return modelo;
    }

    private async Task<Dictionary<long, string>> ResolverNombresAsync(
        PropuestaConfirmacionBiometricaDto propuesta,
        CancellationToken cancelacion)
    {
        var resultado = new Dictionary<long, string>();
        foreach (var id in propuesta.Decisiones.Where(d => d.TrabajadorId.HasValue)
                     .Select(d => d.TrabajadorId!.Value).Distinct())
        {
            var trabajador = await trabajadores.ObtenerDetalleAsync(id, cancelacion).ConfigureAwait(false);
            if (trabajador is not null)
            {
                resultado[id] = $"{trabajador.ApellidoPaterno} {trabajador.ApellidoMaterno}, {trabajador.Nombres}";
            }
        }
        return resultado;
    }
}
