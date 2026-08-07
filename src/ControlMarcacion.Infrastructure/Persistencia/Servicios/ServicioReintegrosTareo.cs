using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Reportes;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>Captura manual de los reintegros y descuentos del bloque AO-BF del tareo final.</summary>
internal sealed class ServicioReintegrosTareo(MarcacionDbContext contexto) : IServicioReintegrosTareo
{
    public async Task<ReintegroTareoDto?> ObtenerAsync(
        long trabajadorId, long periodoId, CancellationToken cancelacion = default) =>
        await contexto.ReintegrosTareo
            .AsNoTracking()
            .Where(r => r.TrabajadorId == trabajadorId && r.PeriodoId == periodoId)
            .Select(r => new ReintegroTareoDto(
                r.TrabajadorId,
                r.PeriodoId,
                r.HorasNormales,
                r.HorasExtra25,
                r.HorasExtra35,
                r.HorasNoLaborable,
                r.MontoMovilidad,
                r.HorasNocturnas,
                r.HorasExtra25Nocturnas,
                r.HorasExtra35Nocturnas,
                r.HorasNoLaborableNocturnas,
                r.Viatico,
                r.DiasDescansoMedico,
                r.DiasSubsidioMaternidad,
                r.DiasSubsidioAccidente,
                r.DiasLicenciaPaternidad,
                r.LicenciaConGoce,
                r.ReintegroAsignacionFamiliar,
                r.ReintegroAfecto,
                r.DescuentoPagoIndebido))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task GuardarAsync(
        GuardarReintegroTareoRequest solicitud, string usuario, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var existeTrabajador = await contexto.Trabajadores
            .AsNoTracking()
            .AnyAsync(t => t.Id == solicitud.TrabajadorId, cancelacion)
            .ConfigureAwait(false);

        if (!existeTrabajador)
        {
            throw new RecursoNoEncontradoException("el trabajador", solicitud.TrabajadorId);
        }

        var periodo = await contexto.Periodos
            .FirstOrDefaultAsync(p => p.Id == solicitud.PeriodoId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el periodo", solicitud.PeriodoId);

        // Invariante del sistema: periodo cerrado, nada se escribe. Sin "modo administrador"
        // ni excepciones silenciosas.
        if (!periodo.EstaAbierto)
        {
            throw new PeriodoCerradoException(periodo.Codigo);
        }

        var entidad = await contexto.ReintegrosTareo
            .FirstOrDefaultAsync(
                r => r.TrabajadorId == solicitud.TrabajadorId && r.PeriodoId == solicitud.PeriodoId,
                cancelacion)
            .ConfigureAwait(false);

        if (entidad is null)
        {
            entidad = new ReintegroTareo
            {
                TrabajadorId = solicitud.TrabajadorId,
                PeriodoId = solicitud.PeriodoId,
                CreadoPor = usuario
            };

            await contexto.ReintegrosTareo.AddAsync(entidad, cancelacion).ConfigureAwait(false);
        }
        else
        {
            entidad.ModificadoPor = usuario;
        }

        entidad.HorasNormales = solicitud.HorasNormales;
        entidad.HorasExtra25 = solicitud.HorasExtra25;
        entidad.HorasExtra35 = solicitud.HorasExtra35;
        entidad.HorasNoLaborable = solicitud.HorasNoLaborable;
        entidad.MontoMovilidad = solicitud.MontoMovilidad;
        entidad.HorasNocturnas = solicitud.HorasNocturnas;
        entidad.HorasExtra25Nocturnas = solicitud.HorasExtra25Nocturnas;
        entidad.HorasExtra35Nocturnas = solicitud.HorasExtra35Nocturnas;
        entidad.HorasNoLaborableNocturnas = solicitud.HorasNoLaborableNocturnas;
        entidad.Viatico = solicitud.Viatico;
        entidad.DiasDescansoMedico = solicitud.DiasDescansoMedico;
        entidad.DiasSubsidioMaternidad = solicitud.DiasSubsidioMaternidad;
        entidad.DiasSubsidioAccidente = solicitud.DiasSubsidioAccidente;
        entidad.DiasLicenciaPaternidad = solicitud.DiasLicenciaPaternidad;
        entidad.LicenciaConGoce = solicitud.LicenciaConGoce;
        entidad.ReintegroAsignacionFamiliar = solicitud.ReintegroAsignacionFamiliar;
        entidad.ReintegroAfecto = solicitud.ReintegroAfecto;
        entidad.DescuentoPagoIndebido = solicitud.DescuentoPagoIndebido;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OpcionDto>> TrabajadoresAsync(CancellationToken cancelacion = default) =>
        await contexto.Trabajadores
            .AsNoTracking()
            .Where(t => t.Activo && t.FechaCese == null)
            .OrderBy(t => t.ApellidoPaterno).ThenBy(t => t.ApellidoMaterno)
            .Select(t => new OpcionDto(t.Id, t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
}
