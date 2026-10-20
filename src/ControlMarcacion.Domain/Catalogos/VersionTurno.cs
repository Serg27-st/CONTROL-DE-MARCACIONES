using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Catalogos;

/// <summary>
/// El horario que rigió en un turno durante un rango de fechas.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Turno"/> es la <b>identidad</b> del turno —su código, su nombre, en qué
/// plantas existe— y guarda los valores <b>vigentes hoy</b>, que son los que se editan en el
/// catálogo. Esta entidad guarda los que rigieron <b>en cada fecha</b>, que son los que usa
/// el cálculo.
/// </para>
/// <para>
/// Sin ella, editar el horario del turno DIU en octubre y recalcular julio —por una
/// justificación tardía, por un archivo que faltaba— recalculaba julio con el horario de
/// octubre: las tardanzas y el sobretiempo de un mes ya pagado cambiaban sin que nada en ese
/// mes hubiera cambiado. Todo lo demás en el sistema ya tenía vigencia por fecha —el número
/// de marcador, la asignación de turno, el sueldo, las tasas, la programación de la
/// cuadrilla—; el contenido del turno era el único que no.
/// </para>
/// <para>
/// Se comporta como las demás vigencias del sistema: <b>no se edita, se cierra la vigente y
/// se abre otra</b>. Ver <see cref="ValidadorVigencias"/>.
/// </para>
/// </remarks>
public class VersionTurno : EntidadBase, IVigenciaPorFecha
{
    /// <summary>
    /// Desde cuándo rigen los turnos que ya existían cuando se introdujo la vigencia.
    /// </summary>
    /// <remarks>
    /// El relleno de la migración abre la primera versión de cada turno en esta fecha, muy
    /// anterior a cualquier dato real (el padrón más antiguo ingresa en 2020). Así el día de
    /// la migración <b>ningún recálculo cambia de resultado</b>: todo el historial sigue
    /// cayendo dentro de la única versión, que lleva exactamente los valores de hoy.
    /// </remarks>
    public static readonly DateOnly VigenciaOriginal = new(2000, 1, 1);

    public long TurnoId { get; set; }

    public Turno? Turno { get; set; }

    public DateOnly FechaInicio { get; set; }

    /// <summary>Nulo mientras siga vigente.</summary>
    public DateOnly? FechaFin { get; set; }

    // ------------------------------------------------------------------
    // El contenido que decide el cálculo. Ver Turno para qué mide cada uno.
    // ------------------------------------------------------------------

    public TimeOnly HoraInicio { get; set; }

    public TimeOnly HoraFin { get; set; }

    public int MinutosRefrigerio { get; set; }

    public int MinutosTolerancia { get; set; }

    public int MinutosAnticipacionEntrada { get; set; }

    public int MinutosAnticipacionSalida { get; set; }

    public int MinutosGraciaSalida { get; set; }

    public int MinutosJornadaOrdinaria { get; set; }

    public DiasSemana DiasAplicables { get; set; }

    public bool EstaVigenteEn(DateOnly fecha) =>
        Activo && fecha >= FechaInicio && (FechaFin is null || fecha <= FechaFin.Value);

    /// <summary>Copia del turno con el horario de esta versión.</summary>
    /// <remarks>
    /// Devuelve una <b>instancia nueva</b> y nunca toca la que recibe. El recálculo comparte
    /// un solo <see cref="Turno"/> entre todos los trabajadores y todos los días del periodo:
    /// escribirle encima los valores de una versión contaminaría el cálculo de los demás
    /// días, y el resultado dependería del orden en que se recorrió el periodo.
    ///
    /// <para>La copia conserva la <b>identidad</b> —Id, código, nombre, plantas— porque el
    /// motor la usa para explicar el día («Cuadrilla A: turno NOC.») y el orquestador para
    /// guardar el <c>TurnoId</c> del tareo.</para>
    /// </remarks>
    public Turno AplicarA(Turno turno)
    {
        ArgumentNullException.ThrowIfNull(turno);

        return new Turno
        {
            Id = turno.Id,
            Codigo = turno.Codigo,
            Nombre = turno.Nombre,
            Activo = turno.Activo,
            PlantasHabilitadas = turno.PlantasHabilitadas,
            CreadoEn = turno.CreadoEn,
            CreadoPor = turno.CreadoPor,
            ModificadoEn = turno.ModificadoEn,
            ModificadoPor = turno.ModificadoPor,

            HoraInicio = HoraInicio,
            HoraFin = HoraFin,
            MinutosRefrigerio = MinutosRefrigerio,
            MinutosTolerancia = MinutosTolerancia,
            MinutosAnticipacionEntrada = MinutosAnticipacionEntrada,
            MinutosAnticipacionSalida = MinutosAnticipacionSalida,
            MinutosGraciaSalida = MinutosGraciaSalida,
            MinutosJornadaOrdinaria = MinutosJornadaOrdinaria,
            DiasAplicables = DiasAplicables
        };
    }

    /// <summary>
    /// El turno lleva hoy exactamente este horario: editarlo no cambió nada que el cálculo
    /// mire, así que no hace falta abrir una versión.
    /// </summary>
    /// <remarks>
    /// Es lo que evita que renombrar un turno —o guardar el formulario sin tocar nada— parta
    /// su historia en dos. Solo los campos que decide el cálculo cuentan: el código, el
    /// nombre y las plantas no entran.
    /// </remarks>
    public bool MismoHorarioQue(Turno turno)
    {
        ArgumentNullException.ThrowIfNull(turno);

        return HoraInicio == turno.HoraInicio
               && HoraFin == turno.HoraFin
               && MinutosRefrigerio == turno.MinutosRefrigerio
               && MinutosTolerancia == turno.MinutosTolerancia
               && MinutosAnticipacionEntrada == turno.MinutosAnticipacionEntrada
               && MinutosAnticipacionSalida == turno.MinutosAnticipacionSalida
               && MinutosGraciaSalida == turno.MinutosGraciaSalida
               && MinutosJornadaOrdinaria == turno.MinutosJornadaOrdinaria
               && DiasAplicables == turno.DiasAplicables;
    }

    /// <summary>Versión abierta con el horario que el turno tiene ahora.</summary>
    public static VersionTurno De(Turno turno, DateOnly desde, string usuario)
    {
        ArgumentNullException.ThrowIfNull(turno);

        var version = new VersionTurno
        {
            TurnoId = turno.Id,
            FechaInicio = desde,
            FechaFin = null,
            CreadoPor = usuario
        };

        version.TomarHorarioDe(turno);

        return version;
    }

    /// <summary>Copia a esta versión el horario que el turno tiene ahora.</summary>
    /// <remarks>
    /// Se usa al abrir una versión y al <b>corregir en sitio</b> la vigente, que es el caso
    /// de «el horario estaba mal desde el principio»: abrir otra dejaría una vigencia de cero
    /// días. Quién lo cambió y desde qué valor lo guarda la auditoría.
    /// </remarks>
    public void TomarHorarioDe(Turno turno)
    {
        ArgumentNullException.ThrowIfNull(turno);

        HoraInicio = turno.HoraInicio;
        HoraFin = turno.HoraFin;
        MinutosRefrigerio = turno.MinutosRefrigerio;
        MinutosTolerancia = turno.MinutosTolerancia;
        MinutosAnticipacionEntrada = turno.MinutosAnticipacionEntrada;
        MinutosAnticipacionSalida = turno.MinutosAnticipacionSalida;
        MinutosGraciaSalida = turno.MinutosGraciaSalida;
        MinutosJornadaOrdinaria = turno.MinutosJornadaOrdinaria;
        DiasAplicables = turno.DiasAplicables;
    }
}
