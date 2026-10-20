using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Personal;

/// <summary>
/// Un vínculo laboral del trabajador con la empresa, <b>con rango de fechas</b>:
/// desde que ingresa hasta que cesa.
/// </summary>
/// <remarks>
/// <see cref="Trabajador.FechaIngreso"/> y <see cref="Trabajador.FechaCese"/> siguen
/// existiendo temporalmente como proyección de compatibilidad. Esta entidad es la
/// <b>autoridad temporal</b>: una fila por cada vez que la persona entró y salió de la
/// empresa.
///
/// Sin ella, un reingreso sobrescribiría la fecha de ingreso anterior y el tareo de un
/// periodo ya cerrado pasaría a calcularse contra un vínculo que en esa fecha no existía.
/// Es el mismo motivo por el que el ID de marcador, el turno y el sueldo tampoco son
/// campos planos: ver <see cref="AsignacionMarcador"/>.
/// </remarks>
public class PeriodoLaboral : EntidadBase
{
    public long TrabajadorId { get; set; }

    public Trabajador? Trabajador { get; set; }

    /// <summary>Empresa con la que se tuvo el vínculo en este periodo.</summary>
    /// <remarks>
    /// Se guarda aquí y no solo en <see cref="Trabajador.EmpresaId"/> porque una persona
    /// puede reingresar por la otra razón social: Consulting Advisers y CA Outsourcing
    /// son personas jurídicas distintas, y el periodo trabajado en una no puede quedar
    /// atribuido a la otra cuando alguien edite la ficha.
    /// </remarks>
    public long EmpresaId { get; set; }

    public DateOnly FechaIngreso { get; set; }

    /// <summary>Nulo mientras el vínculo siga vigente.</summary>
    public DateOnly? FechaCese { get; set; }

    /// <summary>Obligatorio al cerrar el periodo. Nulo mientras el vínculo siga vigente.</summary>
    public MotivoCese? MotivoCese { get; set; }

    /// <summary>Detalle libre del cese, opcional.</summary>
    public string? ObservacionCese { get; set; }

    /// <summary>Historia de planta contenida en este vínculo laboral.</summary>
    public ICollection<AsignacionPlanta> AsignacionesPlanta { get; set; } = [];

    public bool EstaVigenteEn(DateOnly fecha) =>
        RangoTemporal.Contiene(FechaIngreso, FechaCese, fecha);

    public bool EstaAbierto => FechaCese is null;

    /// <summary>Valida rango, claves y cobertura completa de la planta del vínculo.</summary>
    public void Validar()
    {
        if (!RangoTemporal.EsValido(FechaIngreso, FechaCese))
        {
            throw new ReglaNegocioException(
                "La fecha de cese no puede ser anterior a la fecha de ingreso.");
        }

        if (EmpresaId <= 0)
        {
            throw new ReglaNegocioException("El periodo laboral debe indicar una empresa válida.");
        }

        var plantas = AsignacionesPlanta
            .OrderBy(a => a.FechaInicio)
            .ThenBy(a => a.FechaFin ?? DateOnly.MaxValue)
            .ToList();

        if (plantas.Count == 0)
        {
            throw new ReglaNegocioException(
                "El periodo laboral debe tener una asignación de planta.");
        }

        foreach (var planta in plantas)
        {
            if (planta.PlantaId <= 0)
            {
                throw new ReglaNegocioException(
                    "La asignación del periodo laboral debe indicar una planta válida.");
            }

            if (planta.FechaInicio < FechaIngreso
                || (FechaCese is { } cese
                    && (planta.FechaFin is null || planta.FechaFin.Value > cese)))
            {
                throw new ReglaNegocioException(
                    "La asignación de planta debe quedar completamente contenida dentro " +
                    "del periodo laboral al que pertenece.");
            }
        }

        ValidadorVigencias.GarantizarLineaDeTiempoSana(plantas, "planta");

        if (plantas[0].FechaInicio != FechaIngreso)
        {
            throw new ReglaNegocioException(
                "La historia de planta debe comenzar en la fecha de ingreso del periodo laboral.");
        }

        for (var i = 1; i < plantas.Count; i++)
        {
            if (plantas[i - 1].FechaFin is not { } finAnterior
                || finAnterior.AddDays(1) != plantas[i].FechaInicio)
            {
                throw new ReglaNegocioException(
                    "La historia de planta no puede contener días sin una planta asignada.");
            }
        }

        var ultima = plantas[^1];
        if (FechaCese is { } finPeriodo)
        {
            if (ultima.FechaFin != finPeriodo)
            {
                throw new ReglaNegocioException(
                    "La historia de planta debe terminar en la fecha de cese del periodo laboral.");
            }
        }
        else if (ultima.FechaFin is not null)
        {
            throw new ReglaNegocioException(
                "Un periodo laboral abierto debe conservar una asignación de planta abierta.");
        }
    }

    /// <summary>
    /// Corrige el inicio del vínculo abierto y mantiene alineada su primera planta.
    /// </summary>
    public void CorregirFechaIngreso(DateOnly nuevaFecha, string usuario)
    {
        if (!EstaAbierto)
        {
            throw new ReglaNegocioException(
                "No se puede modificar la fecha de ingreso de un periodo laboral cerrado.");
        }

        var primera = AsignacionesPlanta.OrderBy(a => a.FechaInicio).FirstOrDefault()
            ?? throw new ReglaNegocioException(
                "El periodo laboral debe tener una asignación de planta.");

        if (primera.FechaFin is { } fin && nuevaFecha > fin)
        {
            throw new ReglaNegocioException(
                "La nueva fecha de ingreso dejaría fuera la primera asignación de planta.");
        }

        FechaIngreso = nuevaFecha;
        ModificadoPor = usuario;
        primera.FechaInicio = nuevaFecha;
        primera.ModificadoPor = usuario;
    }

    /// <summary>Devuelve la planta no ambigua que correspondía en una fecha.</summary>
    public long? PlantaEn(DateOnly fecha)
    {
        var vigentes = AsignacionesPlanta.Where(a => a.EstaVigenteEn(fecha)).ToList();

        return vigentes.Count switch
        {
            0 => null,
            1 => vigentes[0].PlantaId,
            _ => throw new ReglaNegocioException(
                $"El vínculo laboral tiene más de una planta vigente el {fecha:dd/MM/yyyy}.")
        };
    }

    /// <summary>
    /// Agrega una vigencia de planta y protege su validez, contención y no solapamiento.
    /// </summary>
    public AsignacionPlanta AsignarPlanta(
        long plantaId,
        DateOnly desde,
        DateOnly? hasta,
        string usuario)
    {
        if (!RangoTemporal.EsValido(desde, hasta))
        {
            throw new ReglaNegocioException(
                $"La asignación de planta termina el {hasta:dd/MM/yyyy}, antes de empezar " +
                $"el {desde:dd/MM/yyyy}.");
        }

        if (desde < FechaIngreso
            || (FechaCese is { } cese && (hasta is null || hasta.Value > cese)))
        {
            throw new ReglaNegocioException(
                "La asignación de planta debe quedar completamente contenida dentro del " +
                "periodo laboral al que pertenece.");
        }

        var asignacion = new AsignacionPlanta
        {
            PlantaId = plantaId,
            FechaInicio = desde,
            FechaFin = hasta,
            CreadoPor = usuario
        };

        ValidadorVigencias.GarantizarLineaDeTiempoSana(
            AsignacionesPlanta.Append(asignacion),
            "planta");

        AsignacionesPlanta.Add(asignacion);
        return asignacion;
    }

    /// <summary>Cierra la planta anterior y abre la nueva en la fecha efectiva.</summary>
    public AsignacionPlanta CambiarPlanta(long plantaId, DateOnly desde, string usuario)
    {
        if (!EstaVigenteEn(desde))
        {
            throw new ReglaNegocioException(
                "La fecha efectiva del cambio de planta debe pertenecer al periodo laboral.");
        }

        var abierta = AsignacionesPlanta.SingleOrDefault(a => a.FechaFin is null);

        if (abierta is not null)
        {
            if (abierta.PlantaId == plantaId)
            {
                return abierta;
            }

            if (desde <= abierta.FechaInicio)
            {
                throw new ReglaNegocioException(
                    "El cambio de planta debe ser posterior al inicio de la asignación vigente.");
            }

            abierta.FechaFin = desde.AddDays(-1);
            abierta.ModificadoPor = usuario;
        }

        return AsignarPlanta(plantaId, desde, FechaCese, usuario);
    }
}
