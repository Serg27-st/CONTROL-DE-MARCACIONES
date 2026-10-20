namespace ControlMarcacion.Application.MarcacionesProvisionales.Dto;

// ──────────────────────────────────────────────────────────────────────────────
// Módulo provisional — eliminar cuando el motor principal cubra este flujo.
// ──────────────────────────────────────────────────────────────────────────────

public sealed record ResultadoCargaProvisional(
    long ImportacionId,
    string NombreArchivo,
    int FilasLeidas,
    int FilasInsertadas,
    int FilasRechazadas,
    DateOnly? FechaMinima,
    DateOnly? FechaMaxima,
    IReadOnlyList<ErrorCargaProvisional> Errores);

public sealed record ErrorCargaProvisional(
    int NumeroLinea,
    string? ContenidoLinea,
    string Motivo);

public sealed record ImportacionProvisionalListaDto(
    long Id,
    string NombreArchivo,
    long EquipoId,
    string? EquipoDescripcion,
    DateTime FechaCarga,
    string CargadoPorUsuario,
    int FilasLeidas,
    int FilasInsertadas,
    int FilasRechazadas);
