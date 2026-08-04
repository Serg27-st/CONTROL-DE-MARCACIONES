using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;

namespace ControlMarcacion.Application.Personal;

/// <summary>
/// Casos de uso del mantenimiento de personal.
/// </summary>
/// <remarks>
/// Es la única puerta de entrada al módulo Personal: ningún otro módulo debe tocar
/// <see cref="ITrabajadorRepository"/> directamente.
/// </remarks>
public sealed class ServicioTrabajadores(
    ITrabajadorRepository repositorio,
    IConsultaTrabajadores consulta)
{
    public Task<ResultadoPaginado<TrabajadorListaDto>> ListarAsync(
        FiltroTrabajadores filtro,
        CancellationToken cancelacion = default) =>
        consulta.ListarAsync(filtro, cancelacion);

    public async Task<TrabajadorDetalleDto> ObtenerAsync(long id, CancellationToken cancelacion = default)
    {
        var detalle = await consulta.ObtenerDetalleAsync(id, cancelacion).ConfigureAwait(false);

        return detalle ?? throw new RecursoNoEncontradoException("el trabajador", id);
    }

    public async Task<long> CrearAsync(
        GuardarTrabajadorRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        await ValidarAsync(solicitud, null, cancelacion).ConfigureAwait(false);

        var trabajador = new Trabajador
        {
            TipoDocumentoId = solicitud.TipoDocumentoId,
            NumeroDocumento = solicitud.NumeroDocumento.Trim(),
            ApellidoPaterno = solicitud.ApellidoPaterno.Trim(),
            ApellidoMaterno = solicitud.ApellidoMaterno.Trim(),
            Nombres = solicitud.Nombres.Trim(),
            Sexo = solicitud.Sexo,
            Telefono = solicitud.Telefono?.Trim(),
            Direccion = solicitud.Direccion?.Trim(),
            CorreoElectronico = solicitud.CorreoElectronico?.Trim(),
            TieneHijos = solicitud.TieneHijos,
            EmpresaId = solicitud.EmpresaId,
            Unidad = solicitud.Unidad?.Trim(),
            TipoPlanilla = solicitud.TipoPlanilla,
            PlantaId = solicitud.PlantaId,
            AreaId = solicitud.AreaId,
            SeccionId = solicitud.SeccionId,
            FechaIngreso = solicitud.FechaIngreso,
            FechaCese = solicitud.FechaCese,
            PuestoId = solicitud.PuestoId,
            CreadoPor = usuario
        };

        AplicarAsignaciones(trabajador, solicitud, usuario);

        await repositorio.AgregarAsync(trabajador, cancelacion).ConfigureAwait(false);
        await repositorio.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);

        return trabajador.Id;
    }

    public async Task ActualizarAsync(
        GuardarTrabajadorRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (solicitud.Id is not { } id)
        {
            throw new ReglaNegocioException("No se indicó qué trabajador se va a actualizar.");
        }

        var trabajador = await repositorio.ObtenerPorIdAsync(id, cancelacion).ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el trabajador", id);

        await ValidarAsync(solicitud, id, cancelacion).ConfigureAwait(false);

        trabajador.TipoDocumentoId = solicitud.TipoDocumentoId;
        trabajador.NumeroDocumento = solicitud.NumeroDocumento.Trim();
        trabajador.ApellidoPaterno = solicitud.ApellidoPaterno.Trim();
        trabajador.ApellidoMaterno = solicitud.ApellidoMaterno.Trim();
        trabajador.Nombres = solicitud.Nombres.Trim();
        trabajador.Sexo = solicitud.Sexo;
        trabajador.Telefono = solicitud.Telefono?.Trim();
        trabajador.Direccion = solicitud.Direccion?.Trim();
        trabajador.CorreoElectronico = solicitud.CorreoElectronico?.Trim();
        trabajador.TieneHijos = solicitud.TieneHijos;
        trabajador.EmpresaId = solicitud.EmpresaId;
        trabajador.Unidad = solicitud.Unidad?.Trim();
        trabajador.TipoPlanilla = solicitud.TipoPlanilla;
        trabajador.PlantaId = solicitud.PlantaId;
        trabajador.AreaId = solicitud.AreaId;
        trabajador.SeccionId = solicitud.SeccionId;
        trabajador.FechaIngreso = solicitud.FechaIngreso;
        trabajador.FechaCese = solicitud.FechaCese;
        trabajador.PuestoId = solicitud.PuestoId;
        trabajador.ModificadoPor = usuario;

        AplicarAsignaciones(trabajador, solicitud, usuario);

        repositorio.Actualizar(trabajador);
        await repositorio.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);
    }

    private async Task ValidarAsync(
        GuardarTrabajadorRequest solicitud,
        long? idExistente,
        CancellationToken cancelacion)
    {
        if (solicitud.FechaCese is { } cese && cese < solicitud.FechaIngreso)
        {
            throw new ReglaNegocioException(
                "La fecha de cese no puede ser anterior a la fecha de ingreso.");
        }

        var documento = solicitud.NumeroDocumento.Trim();

        var documentoRepetido = await repositorio
            .ExisteDocumentoAsync(solicitud.TipoDocumentoId, documento, idExistente, cancelacion)
            .ConfigureAwait(false);

        if (documentoRepetido)
        {
            throw new ReglaNegocioException(
                $"Ya existe un trabajador registrado con el documento {documento}.");
        }

        if (string.IsNullOrWhiteSpace(solicitud.IdMarcador))
        {
            return;
        }

        // El ID del marcador se reasigna con el tiempo, pero dos personas no pueden
        // tenerlo a la vez: las marcaciones de ese periodo serían inatribuibles.
        var marcadorOcupado = await repositorio
            .IdMarcadorEnUsoAsync(
                solicitud.IdMarcador.Trim(),
                solicitud.FechaIngreso,
                solicitud.FechaCese,
                idExistente,
                cancelacion)
            .ConfigureAwait(false);

        if (marcadorOcupado)
        {
            throw new ReglaNegocioException(
                $"El identificador de marcador '{solicitud.IdMarcador.Trim()}' ya está asignado " +
                "a otro trabajador en un periodo que se cruza con este. Cierre la asignación " +
                "anterior antes de reutilizarlo.");
        }
    }

    /// <summary>
    /// Crea las asignaciones de marcador y de turno cuando cambian.
    /// </summary>
    /// <remarks>
    /// Las asignaciones no se editan: se cierra la vigente y se abre una nueva. Reescribir
    /// la asignación anterior haría que las marcaciones históricas se reinterpreten con
    /// datos que no estaban vigentes cuando ocurrieron.
    /// </remarks>
    private static void AplicarAsignaciones(
        Trabajador trabajador,
        GuardarTrabajadorRequest solicitud,
        string usuario)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        if (!string.IsNullOrWhiteSpace(solicitud.IdMarcador))
        {
            var idMarcador = solicitud.IdMarcador.Trim();
            var vigente = trabajador.AsignacionesMarcador.FirstOrDefault(a => a.FechaFin is null);

            if (vigente is null)
            {
                trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
                {
                    IdMarcador = idMarcador,
                    FechaInicio = trabajador.FechaIngreso,
                    CreadoPor = usuario
                });
            }
            else if (!string.Equals(vigente.IdMarcador, idMarcador, StringComparison.Ordinal))
            {
                vigente.FechaFin = hoy;
                vigente.ModificadoPor = usuario;

                trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
                {
                    IdMarcador = idMarcador,
                    FechaInicio = hoy.AddDays(1),
                    CreadoPor = usuario
                });
            }
        }

        if (solicitud.TurnoId is not { } turnoId)
        {
            return;
        }

        var turnoVigente = trabajador.AsignacionesTurno.FirstOrDefault(a => a.FechaFin is null);

        if (turnoVigente is null)
        {
            trabajador.AsignacionesTurno.Add(new AsignacionTurno
            {
                TurnoId = turnoId,
                FechaInicio = trabajador.FechaIngreso,
                CreadoPor = usuario
            });
        }
        else if (turnoVigente.TurnoId != turnoId)
        {
            turnoVigente.FechaFin = hoy;
            turnoVigente.ModificadoPor = usuario;

            trabajador.AsignacionesTurno.Add(new AsignacionTurno
            {
                TurnoId = turnoId,
                FechaInicio = hoy.AddDays(1),
                CreadoPor = usuario
            });
        }
    }
}
