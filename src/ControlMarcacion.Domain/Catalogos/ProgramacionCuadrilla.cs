using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Catalogos;

public enum TipoTramoRotacion
{
    Trabajo = 1,
    Descanso = 2
}

public enum EstadoProgramacionDiaria
{
    Trabajo = 1,
    DescansoProgramado = 2,
    SinProgramacion = 3,
    ErrorConfiguracion = 4
}

/// <summary>Resultado explícito de resolver una programación para un día.</summary>
public sealed record ResultadoProgramacionDiaria(
    EstadoProgramacionDiaria Estado,
    long? TurnoId,
    long? ProgramacionId,
    long? TramoId,
    int? DiaDentroDelTramo,
    string? Error = null);

/// <summary>Secuencia finita de trabajo y descansos de una cuadrilla.</summary>
public class ProgramacionCuadrilla : EntidadBase, IVigenciaPorFecha
{
    public long CuadrillaId { get; set; }

    public Cuadrilla? Cuadrilla { get; set; }

    public DateOnly FechaInicio { get; set; }

    /// <summary>Último día de la secuencia finita.</summary>
    public DateOnly? FechaFin { get; set; }

    /// <summary>Día en que empieza el primer tramo.</summary>
    public DateOnly FechaAncla { get; set; }

    public ICollection<TramoRotacion> Tramos { get; set; } = [];

    public int DiasDelCiclo => Tramos.Sum(t => t.Dias);

    public bool EstaVigenteEn(DateOnly fecha) =>
        Activo && fecha >= FechaInicio && (FechaFin is null || fecha <= FechaFin.Value);

    public IReadOnlyList<TramoRotacion> TramosOrdenados() => [.. Tramos.OrderBy(t => t.Orden)];

    /// <summary>Valida la estructura completa sin degradar combinaciones inválidas.</summary>
    public string? ErrorDeConfiguracion()
    {
        var tramos = TramosOrdenados();

        if (tramos.Count == 0)
        {
            return "La programación no contiene tramos.";
        }

        if (tramos.Any(t => t.Orden <= 0)
            || tramos.Select(t => t.Orden).Distinct().Count() != tramos.Count)
        {
            return "Los tramos deben tener un orden positivo y único.";
        }

        foreach (var tramo in tramos)
        {
            if (tramo.ErrorDeConfiguracion() is { } error)
            {
                return $"Tramo {tramo.Orden}: {error}";
            }
        }

        var finEsperado = FechaAncla.AddDays(DiasDelCiclo - 1);
        if (FechaFin is { } fechaFin && fechaFin > finEsperado)
        {
            return $"La fecha fin no puede superar {finEsperado:dd/MM/yyyy}, último día de la secuencia.";
        }

        return null;
    }

    public ResultadoProgramacionDiaria ResolverEn(DateOnly fecha)
    {
        if (ErrorDeConfiguracion() is { } error)
        {
            return new(EstadoProgramacionDiaria.ErrorConfiguracion, null, Id, null, null, error);
        }

        var desplazamiento = fecha.DayNumber - FechaAncla.DayNumber;
        if (!EstaVigenteEn(fecha) || desplazamiento < 0 || desplazamiento >= DiasDelCiclo)
        {
            return new(EstadoProgramacionDiaria.SinProgramacion, null, Id, null, null);
        }

        var posicion = desplazamiento;
        foreach (var tramo in TramosOrdenados())
        {
            if (posicion < tramo.Dias)
            {
                return tramo.Tipo == TipoTramoRotacion.Descanso
                    ? new(EstadoProgramacionDiaria.DescansoProgramado, null, Id, tramo.Id, posicion)
                    : new(EstadoProgramacionDiaria.Trabajo, tramo.TurnoId, Id, tramo.Id, posicion);
            }

            posicion -= tramo.Dias;
        }

        return new(
            EstadoProgramacionDiaria.ErrorConfiguracion,
            null,
            Id,
            null,
            null,
            "La posición calculada excede la duración de la secuencia.");
    }

    /// <summary>Compatibilidad para consumidores que requieren el tramo material.</summary>
    public (TramoRotacion Tramo, int DiaDentroDelTramo) TramoEn(DateOnly fecha)
    {
        var resultado = ResolverEn(fecha);

        if (resultado.Estado == EstadoProgramacionDiaria.ErrorConfiguracion)
        {
            throw new ReglaNegocioException(resultado.Error ?? "La programación es inválida.");
        }

        if (resultado.Estado == EstadoProgramacionDiaria.SinProgramacion)
        {
            throw new ReglaNegocioException("La fecha está fuera de la secuencia programada.");
        }

        var posicion = fecha.DayNumber - FechaAncla.DayNumber;
        foreach (var tramo in TramosOrdenados())
        {
            if (posicion < tramo.Dias)
            {
                return (tramo, posicion);
            }

            posicion -= tramo.Dias;
        }

        throw new InvalidOperationException("No se encontró el tramo resuelto.");
    }

    /// <summary>Turno laboral del día; nulo para descanso o fuera de programación.</summary>
    public long? TurnoEn(DateOnly fecha)
    {
        var resultado = ResolverEn(fecha);
        return resultado.Estado switch
        {
            EstadoProgramacionDiaria.Trabajo => resultado.TurnoId,
            EstadoProgramacionDiaria.DescansoProgramado or EstadoProgramacionDiaria.SinProgramacion => null,
            _ => throw new ReglaNegocioException(resultado.Error ?? "La programación es inválida.")
        };
    }

    public IReadOnlyList<(DateOnly Fecha, long TurnoId)> ProximosCambios(DateOnly desde, int cantidad)
    {
        var cambios = new List<(DateOnly, long)>(cantidad);
        var anterior = ResolverEn(desde);
        var fin = FechaAncla.AddDays(DiasDelCiclo - 1);

        for (var fecha = desde.AddDays(1); fecha <= fin && cambios.Count < cantidad; fecha = fecha.AddDays(1))
        {
            var actual = ResolverEn(fecha);
            if (actual.Estado == EstadoProgramacionDiaria.Trabajo
                && actual.TurnoId != anterior.TurnoId)
            {
                cambios.Add((fecha, actual.TurnoId!.Value));
            }

            anterior = actual;
        }

        return cambios;
    }
}

/// <summary>Tramo laboral o descanso explícito dentro de una secuencia.</summary>
public class TramoRotacion : EntidadBase
{
    public long ProgramacionId { get; set; }

    public ProgramacionCuadrilla? Programacion { get; set; }

    public int Orden { get; set; }

    public TipoTramoRotacion Tipo { get; set; } = TipoTramoRotacion.Trabajo;

    public long? TurnoId { get; set; }

    public Turno? Turno { get; set; }

    public int Dias { get; set; }

    public string? ErrorDeConfiguracion()
    {
        if (Dias <= 0)
        {
            return "la duración debe ser mayor que cero.";
        }

        return Tipo switch
        {
            TipoTramoRotacion.Trabajo when TurnoId is null => "un tramo de trabajo requiere turno.",
            TipoTramoRotacion.Descanso when TurnoId is not null => "un descanso no puede tener turno.",
            TipoTramoRotacion.Descanso when Dias != 1 =>
                "un descanso programado debe durar exactamente un día.",
            TipoTramoRotacion.Trabajo or TipoTramoRotacion.Descanso => null,
            _ => "el tipo de tramo no es válido."
        };
    }

    public void Validar()
    {
        if (Orden <= 0)
        {
            throw new ReglaNegocioException("El orden del tramo debe ser positivo.");
        }

        if (ErrorDeConfiguracion() is { } error)
        {
            throw new ReglaNegocioException($"Tramo {Orden}: {error}");
        }
    }
}
