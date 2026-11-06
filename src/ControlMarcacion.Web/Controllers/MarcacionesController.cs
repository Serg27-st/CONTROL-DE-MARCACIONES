using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Carga de marcaciones desde el archivo del marcador.
/// </summary>
/// <remarks>
/// Las marcaciones crudas son inmutables: esta pantalla solo inserta. No hay edición ni
/// borrado, y no debe agregarse: toda corrección va por Justificaciones.
///
/// Lo que sí hay es <b>anular una carga</b>: cuando se sube el archivo equivocado, sus
/// marcaciones se conservan y dejan de contar en el tareo. Es lo contrario de borrar —el
/// dato y su trazabilidad quedan— y es reversible.
///
/// La carga del TXT es una superficie de ataque real (archivo de origen externo), así que
/// además del filtro por rol el servicio valida tipo y tamaño antes de abrir nada.
/// </remarks>
[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador}")]
public sealed class MarcacionesController(
    IServicioImportacion servicio,
    IConsultaCatalogos catalogos) : Controller
{
    /// <summary>Tamaño máximo aceptado. El archivo real de un mes pesa unos 100 KB.</summary>
    private const long TamanioMaximoBytes = 20 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> Index(int? anio, int? mes, CancellationToken cancelacion) =>
        View(await ArmarVistaAsync(anio, mes, cancelacion: cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Reúne historial y cobertura para la pantalla de carga.
    /// </summary>
    /// <remarks>
    /// El año y el mes llegan por la URL y son de origen externo: se acotan a un rango
    /// válido antes de usarlos. Sin esto, <c>?mes=13</c> revienta el
    /// <c>DateTime.DaysInMonth</c> del servicio con un 500 en vez de mostrar una pantalla.
    /// </remarks>
    private async Task<CargaMarcacionesVm> ArmarVistaAsync(
        int? anio,
        int? mes,
        ImportacionPrevia? previa = null,
        string? archivoPendiente = null,
        CancellationToken cancelacion = default)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        var anioElegido = Math.Clamp(anio ?? hoy.Year, 2000, 2100);
        var mesElegido = Math.Clamp(mes ?? hoy.Month, 1, 12);
        var inicioMes = new DateOnly(anioElegido, mesElegido, 1);

        return new CargaMarcacionesVm
        {
            Historial = await servicio.HistorialAsync(cancelacion: cancelacion).ConfigureAwait(false),
            Cobertura = await servicio
                .CoberturaAsync(anioElegido, mesElegido, cancelacion)
                .ConfigureAwait(false),
            NumerosSinAsignar = await servicio
                .NumerosSinAsignarAsync(inicioMes, inicioMes.AddMonths(1).AddDays(-1), cancelacion)
                .ConfigureAwait(false),
            Hoy = hoy,
            Equipos = await catalogos.EquiposActivosAsync(cancelacion).ConfigureAwait(false),
            Previa = previa,
            ArchivoPendiente = archivoPendiente
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(TamanioMaximoBytes)]
    public async Task<IActionResult> Cargar(
        IFormFile? archivo,
        long plantaId,
        long equipoId,
        bool confirmarRecarga,
        int? anio,
        int? mes,
        CancellationToken cancelacion)
    {
        if (archivo is null || archivo.Length == 0)
        {
            TempData["Error"] = "Seleccione un archivo del marcador antes de cargar.";
            return RedirectToAction(nameof(Index), new { anio, mes });
        }

        // Planta y marcador son obligatorios y se comprueban aquí ADEMÁS de en el servicio:
        // esto evita subir 100 KB para que la validación falle al final, pero el control
        // real es el del servicio — un formulario manipulado no puede saltárselo.
        if (plantaId <= 0 || equipoId <= 0)
        {
            TempData["Error"] =
                "Indique la unidad y el marcador del que se extrajo el archivo. " +
                "Sin ese dato las marcaciones no se pueden atribuir: en una unidad con dos " +
                "equipos, el mismo código identifica a dos personas distintas.";
            return RedirectToAction(nameof(Index), new { anio, mes });
        }

        if (archivo.Length > TamanioMaximoBytes)
        {
            TempData["Error"] =
                $"El archivo pesa {archivo.Length / 1024d / 1024d:0.0} MB y el máximo es " +
                $"{TamanioMaximoBytes / 1024 / 1024} MB.";
            return RedirectToAction(nameof(Index), new { anio, mes });
        }

        // Se copia a memoria porque hay que recorrerlo dos veces: una para el hash y otra
        // para leerlo. El archivo de un mes pesa unos 100 KB, así que no es un problema.
        using var contenido = new MemoryStream();
        await archivo.CopyToAsync(contenido, cancelacion).ConfigureAwait(false);

        if (!confirmarRecarga)
        {
            var previa = await servicio
                .BuscarImportacionPreviaAsync(contenido, cancelacion)
                .ConfigureAwait(false);

            // Vigente en otro aparato no se ofrece a confirmar: el servicio lo rechaza con o
            // sin casilla, y pedir confirmación invitaría a algo que no se va a poder hacer.
            // Se sigue hasta ImportarAsync para que el motivo salga de un solo lugar.
            if (previa is not null && (previa.Anulada || previa.EquipoId == equipoId))
            {
                return View(
                    "Index",
                    await ArmarVistaAsync(anio, mes, previa, archivo.FileName, cancelacion)
                        .ConfigureAwait(false));
            }
        }

        var usuario = User.Identity?.Name ?? "sistema";

        ResultadoImportacion resultado;

        try
        {
            resultado = await servicio
                .ImportarAsync(contenido, archivo.FileName, plantaId, equipoId, usuario, cancelacion)
                .ConfigureAwait(false);
        }
        catch (ReglaNegocioException excepcion)
        {
            // Planta y equipo que no concuerdan, equipo de baja, o serie del archivo que
            // contradice lo declarado. Son errores del operador, no del sistema: vuelven a
            // la pantalla con el motivo en vez de una página de error.
            TempData["Error"] = excepcion.Message;
            return RedirectToAction(nameof(Index), new { anio, mes });
        }

        TempData["Mensaje"] =
            $"Archivo procesado: {resultado.FilasInsertadas} marcación(es) nueva(s) de " +
            $"{resultado.FilasLeidas} registro(s) detectado(s)" +
            (resultado.FilasIgnoradas > 0
                ? $"; {resultado.FilasIgnoradas} evento(s) de equipo ignorado(s)."
                : ".");

        return RedirectToAction(nameof(Resultado), new { id = resultado.ImportacionId });
    }

    [HttpGet]
    public async Task<IActionResult> Resultado(long id, CancellationToken cancelacion)
    {
        var resultado = await servicio.ObtenerResultadoAsync(id, cancelacion).ConfigureAwait(false);

        return resultado is null
            ? NotFound()
            : View(new ResultadoImportacionVm { Resultado = resultado });
    }

    /// <summary>
    /// Anula una carga: sus marcaciones dejan de contar, pero no se borra ninguna.
    /// </summary>
    /// <remarks>
    /// Solo el coordinador. Un administrador carga archivos; decidir que un mes entero de
    /// marcaciones deja de contar es otra cosa, y debe quedar en menos manos.
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RolesSistema.Coordinador)]
    public async Task<IActionResult> Anular(
        long id,
        string? motivo,
        int? anio,
        int? mes,
        CancellationToken cancelacion)
    {
        try
        {
            await servicio
                .AnularAsync(id, motivo ?? string.Empty, User.Identity?.Name ?? "sistema", cancelacion)
                .ConfigureAwait(false);

            TempData["Mensaje"] =
                "Carga anulada. Sus marcaciones se conservan pero ya no cuentan para el tareo: " +
                "recalcule el periodo para que el cambio se refleje.";
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }

        return RedirectToAction(nameof(Index), new { anio, mes });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RolesSistema.Coordinador)]
    public async Task<IActionResult> Reactivar(long id, int? anio, int? mes, CancellationToken cancelacion)
    {
        try
        {
            await servicio
                .ReactivarAsync(id, User.Identity?.Name ?? "sistema", cancelacion)
                .ConfigureAwait(false);

            TempData["Mensaje"] =
                "Carga reactivada. Sus marcaciones vuelven a contar: recalcule el periodo.";
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }

        return RedirectToAction(nameof(Index), new { anio, mes });
    }
}
