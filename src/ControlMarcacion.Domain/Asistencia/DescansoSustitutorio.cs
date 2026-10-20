using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Asistencia;

/// <summary>
/// Hecho explícito que relaciona un feriado efectivamente trabajado con el día completo
/// de descanso que lo sustituye. No es permiso, licencia, feriado ni descanso de rotación.
/// </summary>
public sealed class DescansoSustitutorio : EntidadBase
{
    public long TrabajadorId { get; private set; }

    public DateOnly FechaFeriadoOrigen { get; private set; }

    public DateOnly FechaDescanso { get; private set; }

    public EstadoDescansoSustitutorio Estado { get; private set; }

    /// <summary>
    /// En el flujo vigente registrar equivale a otorgar: los roles autorizados aprueban
    /// directamente y ambos datos quedan explícitos aunque hoy correspondan al mismo actor.
    /// </summary>
    public string AprobadoPorUsuario { get; private set; } = null!;

    public DateTime AprobadoEn { get; private set; }

    public string? AnuladoPorUsuario { get; private set; }

    public DateTime? AnuladoEn { get; private set; }

    public string? MotivoAnulacion { get; private set; }

    /// <summary>Token de concurrencia optimista compatible con SQLite.</summary>
    public int Version { get; private set; }

    private DescansoSustitutorio()
    {
    }

    public static DescansoSustitutorio Otorgar(
        long trabajadorId,
        DateOnly fechaFeriadoOrigen,
        DateOnly fechaDescanso,
        string usuario,
        DateTime ahora)
    {
        if (trabajadorId <= 0)
        {
            throw new ReglaNegocioException("Seleccione un trabajador válido.");
        }

        if (fechaDescanso <= fechaFeriadoOrigen)
        {
            throw new ReglaNegocioException(
                "El descanso sustitutorio debe ser posterior al feriado trabajado.");
        }

        if (string.IsNullOrWhiteSpace(usuario))
        {
            throw new ReglaNegocioException("No se pudo identificar al usuario que otorga el descanso.");
        }

        return new DescansoSustitutorio
        {
            TrabajadorId = trabajadorId,
            FechaFeriadoOrigen = fechaFeriadoOrigen,
            FechaDescanso = fechaDescanso,
            Estado = EstadoDescansoSustitutorio.Otorgado,
            AprobadoPorUsuario = usuario.Trim(),
            AprobadoEn = ahora,
            CreadoPor = usuario.Trim(),
            Activo = true
        };
    }

    public void Anular(string motivo, string usuario, DateTime ahora)
    {
        if (Estado != EstadoDescansoSustitutorio.Otorgado || !Activo)
        {
            throw new ReglaNegocioException("El descanso sustitutorio ya no está vigente.");
        }

        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 5)
        {
            throw new ReglaNegocioException("Indique un motivo de anulación de al menos 5 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(usuario))
        {
            throw new ReglaNegocioException("No se pudo identificar al usuario que anula el descanso.");
        }

        Estado = EstadoDescansoSustitutorio.Anulado;
        Activo = false;
        AnuladoPorUsuario = usuario.Trim();
        AnuladoEn = ahora;
        MotivoAnulacion = motivo.Trim();
        ModificadoPor = usuario.Trim();
        Version++;
    }
}

public enum EstadoDescansoSustitutorio
{
    Otorgado = 1,
    Anulado = 2
}
