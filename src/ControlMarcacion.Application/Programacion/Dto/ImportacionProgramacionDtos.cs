namespace ControlMarcacion.Application.Programacion.Dto;

public enum EstadoFilaProgramacion
{
    Existente,
    NuevoPropuesto,
    Descanso,
    Incompleto,
    Conflicto,
    NoAplica
}

public sealed record FilaProgramacionPrevisualizada(
    int Fila,
    long? TrabajadorId,
    string Documento,
    string Trabajador,
    string Empresa,
    string Unidad,
    DateOnly? Fecha,
    string TipoDia,
    string? CodigoTurno,
    string? NombreTurno,
    bool Nocturno,
    EstadoFilaProgramacion Estado,
    string Accion,
    string? Motivo = null);

public sealed record TurnoProgramacionConciliado(
    string Codigo,
    string Nombre,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFin,
    int? MinutosRefrigerio,
    EstadoFilaProgramacion Estado,
    long? TurnoId,
    int FechasAfectadas,
    string? Motivo = null);

public sealed record ResultadoPrevisualizacionProgramacion(
    string NombreArchivo,
    int FilasLeidas,
    int FechasExpandidas,
    int TrabajadoresIdentificados,
    int Descansos,
    int Nocturnos,
    IReadOnlyList<TurnoProgramacionConciliado> Turnos,
    IReadOnlyList<FilaProgramacionPrevisualizada> Filas,
    string Huella)
{
    public int Conflictos => Filas.Count(f => f.Estado == EstadoFilaProgramacion.Conflicto);
    public int Incompletos => Filas.Count(f => f.Estado == EstadoFilaProgramacion.Incompleto);
    public bool PuedeConfirmar => Filas.Count > 0 && Conflictos == 0 && Incompletos == 0;
}

public sealed record ResultadoConfirmacionProgramacion(
    string NombreArchivo,
    int TrabajadoresProgramados,
    int FechasProgramadas,
    int TurnosReutilizados,
    int TurnosCreados,
    int DescansosProgramados,
    int RegistrosIdempotentes,
    IReadOnlyList<long> TrabajadoresReevaluados,
    string HuellaConfirmada);
