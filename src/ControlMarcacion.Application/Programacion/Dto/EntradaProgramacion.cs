namespace ControlMarcacion.Application.Programacion.Dto;

public enum TipoDiaProgramacion { Trabajo, Descanso }

/// <summary>
/// Representación de una jornada programada independiente del origen (Excel, UI, API).
/// </summary>
public sealed record EntradaProgramacion(
    int Fila,
    string TipoDocumento,
    string Documento,
    string Empresa,
    string Unidad,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    DateOnly? FechaEspecifica,
    string? CodigoTurno,
    string? NombreTurno,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFin,
    int? MinutosRefrigerio,
    bool Nocturno,
    TipoDiaProgramacion Tipo,
    string Modalidad)
{
    public bool DefinicionCompleta => !string.IsNullOrWhiteSpace(NombreTurno) && HoraInicio is not null
        && HoraFin is not null && MinutosRefrigerio is not null && HoraInicio != HoraFin;
    public bool DefinicionInformada => NombreTurno is not null || HoraInicio is not null || HoraFin is not null
        || MinutosRefrigerio is not null;
}
