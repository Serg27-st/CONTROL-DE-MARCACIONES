using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Domain.Organizacion;

namespace ControlMarcacion.Domain.Marcaciones;

public enum EstadoConciliacionPersistida
{
    Pendiente = 1,
    Parcial = 2,
    Completada = 3,
    Expirada = 4
}

public enum ResultadoDecisionVinculacion
{
    Pendiente = 1,
    Creada = 2,
    YaVinculada = 3,
    Rechazada = 4,
    PropuestaDesactualizada = 5,
    ConflictoVigencia = 6,
    EvidenciaInsuficiente = 7,
    RevisionManual = 8
}

public enum BaseConfirmacionVinculacion
{
    Ninguna = 0,
    ReglaOperativaUserU = 1,
    EvidenciaDocumental = 2
}

public enum NaturalezaVigenciaVinculacion
{
    NoInformada = 0,
    Declarada = 1,
    Acreditada = 2
}

/// <summary>
/// Lote versionado de propuestas biométricas. Conserva la huella del respaldo, nunca su contenido.
/// </summary>
public class ConciliacionBiometrica : EntidadBase
{
    public Guid IdentificadorPublico { get; set; }
    public long EquipoId { get; set; }
    public EquipoMarcador? Equipo { get; set; }
    public long PlantaId { get; set; }
    public Planta? Planta { get; set; }
    public int Version { get; set; }
    public string HuellaEstado { get; set; } = null!;
    public string HuellaRespaldo { get; set; } = null!;
    public long TamanoRespaldo { get; set; }
    public string FormatoRespaldo { get; set; } = null!;
    public DateTime RespaldoLeidoEnUtc { get; set; }
    public int VersionPoliticaIdentidad { get; set; }
    public DateTime ExpiraEn { get; set; }
    public EstadoConciliacionPersistida Estado { get; set; }
    public ICollection<DecisionVinculacionBiometrica> Decisiones { get; set; } = [];
}

/// <summary>Propuesta y resultado auditable de una fila de <c>user.u</c>.</summary>
public class DecisionVinculacionBiometrica : EntidadBase
{
    public long ConciliacionId { get; set; }
    public ConciliacionBiometrica? Conciliacion { get; set; }
    public int IndiceRegistro { get; set; }
    public ulong? UidRegistro { get; set; }
    public long? TrabajadorId { get; set; }
    public Trabajador? Trabajador { get; set; }
    public long EquipoId { get; set; }
    public EquipoMarcador? Equipo { get; set; }
    public string Pin { get; set; } = null!;
    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public BaseConfirmacionVinculacion BaseConfirmacion { get; set; }
    public NaturalezaVigenciaVinculacion NaturalezaVigencia { get; set; }
    public string CategoriaEvidencia { get; set; } = null!;
    public string ReferenciaEvidencia { get; set; } = null!;
    public string HuellaEvidencia { get; set; } = null!;
    public DateTime EvidenciaVerificadaEn { get; set; }
    public DateTime? EvidenciaExpiraEn { get; set; }
    public string HuellaEstado { get; set; } = null!;
    public string ClaveIdempotencia { get; set; } = null!;
    public ResultadoDecisionVinculacion Resultado { get; set; }
    public string? MotivoResultado { get; set; }
    public string Diagnosticos { get; set; } = string.Empty;
    public string? ConfirmadaPor { get; set; }
    public DateTime? ConfirmadaEn { get; set; }
    public long? AsignacionMarcadorId { get; set; }
    public AsignacionMarcador? AsignacionMarcador { get; set; }
}
