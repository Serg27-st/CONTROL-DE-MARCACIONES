namespace ControlMarcacion.Application.MarcacionesProvisionales.Dto;

public sealed record JornadaProvisionalDto(
    DateOnly Fecha,
    long EquipoId,
    string EquipoCodigo,
    string IdMarcador,
    long? TrabajadorId,
    string? NumeroDocumento,
    string? ApellidoPaterno,
    string? ApellidoMaterno,
    string? Nombres,
    TimeOnly? HoraIngreso,
    TimeOnly? HoraSalida,
    string? TurnoCodigo,
    string? TurnoNombre,
    string? Planta,
    string? Area,
    string? CentroCosto,
    string? Observacion,
    int CantidadMarcaciones,
    IReadOnlyList<TimeOnly> MarcacionesDelDia);

public sealed record ResultadoConsultaProvisional(
    IReadOnlyList<JornadaProvisionalDto> Jornadas,
    int TotalDias,
    bool AdvertenciaRangoExcesivo,
    bool RangoExcedeMaximoExportacion);
