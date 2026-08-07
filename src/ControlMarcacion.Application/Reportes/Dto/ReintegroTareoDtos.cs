using System.ComponentModel.DataAnnotations;

namespace ControlMarcacion.Application.Reportes.Dto;

/// <summary>
/// Los 18 reintegros y descuentos manuales de un trabajador para un periodo: el bloque
/// <c>REINTEGROS</c> (AO-BF) del tareo final.
/// </summary>
public sealed record ReintegroTareoDto(
    long TrabajadorId,
    long PeriodoId,
    decimal HorasNormales,
    decimal HorasExtra25,
    decimal HorasExtra35,
    decimal HorasNoLaborable,
    decimal MontoMovilidad,
    decimal HorasNocturnas,
    decimal HorasExtra25Nocturnas,
    decimal HorasExtra35Nocturnas,
    decimal HorasNoLaborableNocturnas,
    decimal Viatico,
    int DiasDescansoMedico,
    int DiasSubsidioMaternidad,
    int DiasSubsidioAccidente,
    int DiasLicenciaPaternidad,
    decimal LicenciaConGoce,
    decimal ReintegroAsignacionFamiliar,
    decimal ReintegroAfecto,
    decimal DescuentoPagoIndebido);

/// <summary>
/// Alta y edición de los reintegros de un trabajador para un periodo.
/// </summary>
/// <remarks>
/// Una sola fila por (TrabajadorId, PeriodoId): guardar sobre uno existente lo actualiza,
/// nunca inserta un segundo. Solo se admite mientras el periodo esté abierto, igual que el
/// resto de las escrituras del sistema.
/// </remarks>
public sealed record GuardarReintegroTareoRequest
{
    [Required(ErrorMessage = "Seleccione el trabajador.")]
    public long TrabajadorId { get; init; }

    [Required(ErrorMessage = "Seleccione el periodo.")]
    public long PeriodoId { get; init; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal HorasNormales { get; init; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal HorasExtra25 { get; init; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal HorasExtra35 { get; init; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal HorasNoLaborable { get; init; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal MontoMovilidad { get; init; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal HorasNocturnas { get; init; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal HorasExtra25Nocturnas { get; init; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal HorasExtra35Nocturnas { get; init; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal HorasNoLaborableNocturnas { get; init; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal Viatico { get; init; }

    [Range(0, 366, ErrorMessage = "El valor no puede ser negativo.")]
    public int DiasDescansoMedico { get; init; }

    [Range(0, 366, ErrorMessage = "El valor no puede ser negativo.")]
    public int DiasSubsidioMaternidad { get; init; }

    [Range(0, 366, ErrorMessage = "El valor no puede ser negativo.")]
    public int DiasSubsidioAccidente { get; init; }

    [Range(0, 366, ErrorMessage = "El valor no puede ser negativo.")]
    public int DiasLicenciaPaternidad { get; init; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal LicenciaConGoce { get; init; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal ReintegroAsignacionFamiliar { get; init; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal ReintegroAfecto { get; init; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal DescuentoPagoIndebido { get; init; }
}
