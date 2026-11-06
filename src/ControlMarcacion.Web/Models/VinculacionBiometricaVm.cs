using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Application.Catalogos.Dto;
using Microsoft.AspNetCore.Http;

namespace ControlMarcacion.Web.Models;

public sealed class VinculacionBiometricaIndexVm
{
    public IReadOnlyList<EquipoOpcionDto> Equipos { get; set; } = [];

    [Range(1, long.MaxValue, ErrorMessage = "Seleccione la unidad.")]
    public long PlantaId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Seleccione el equipo biométrico.")]
    public long EquipoId { get; set; }

    [Required(ErrorMessage = "Seleccione el respaldo user.u.")]
    public IFormFile? Archivo { get; set; }

    [Required(ErrorMessage = "Indique la fecha efectiva.")]
    public DateOnly FechaInicio { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateOnly? FechaFin { get; set; }
}

public sealed record FilaVinculacionBiometricaVm(
    long DecisionId,
    int IndiceRegistro,
    string Pin,
    string Documento,
    string Trabajador,
    string BaseConfirmacion,
    string NaturalezaVigencia,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    string Resultado,
    string? Motivo,
    IReadOnlyList<string> Diagnosticos,
    bool EsConfirmable);

public sealed class PrevisualizacionVinculacionBiometricaVm
{
    public Guid ConciliacionId { get; init; }
    public int Version { get; init; }
    public DateTime ExpiraEn { get; init; }
    public string Equipo { get; init; } = string.Empty;
    public string Planta { get; init; } = string.Empty;
    public string Formato { get; init; } = string.Empty;
    public string HuellaSha256 { get; init; } = string.Empty;
    public long TamanoBytes { get; init; }
    public int TotalUsuarios { get; init; }
    public IReadOnlyList<FilaVinculacionBiometricaVm> Filas { get; init; } = [];
    public int Confirmables => Filas.Count(f => f.EsConfirmable);
    public int YaVinculados => Filas.Count(f => f.Resultado == "YaVinculada");
    public int Conflictos => Filas.Count(f => f.Resultado == "ConflictoVigencia");
    public int RevisionManual => Filas.Count(f => !f.EsConfirmable && f.Resultado != "YaVinculada" && f.Resultado != "ConflictoVigencia");
}

public sealed class RevisarVinculacionesBiometricasVm
{
    public Guid ConciliacionId { get; set; }
    public int Version { get; set; }
    public long[] Decisiones { get; set; } = [];
}

public sealed record FilaResultadoVinculacionBiometricaVm(
    long DecisionId,
    string Resultado,
    string? Motivo,
    long? AsignacionMarcadorId);

public sealed class ResultadoVinculacionBiometricaVm
{
    public Guid ConciliacionId { get; init; }
    public int Version { get; init; }
    public IReadOnlyList<FilaResultadoVinculacionBiometricaVm> Resultados { get; init; } = [];
    public int Creadas => Resultados.Count(r => r.Resultado == "Creada");
    public int SinCambios => Resultados.Count(r => r.Resultado == "YaVinculada");
    public int NoCreadas => Resultados.Count - Creadas - SinCambios;
}
