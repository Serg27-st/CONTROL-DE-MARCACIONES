using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Asistencia;

/// <summary>
/// Reintegros y descuentos manuales de un trabajador para un periodo: el bloque
/// <c>REINTEGROS</c> (columnas AO-BF) del tareo final.
/// </summary>
/// <remarks>
/// Son correcciones de periodos anteriores y conceptos de dinero que el sistema no calcula
/// —viáticos, subsidios en trámite, reintegros de asignación familiar, descuentos por pago
/// indebido—, así que los carga planilla a mano desde su propia pantalla. El motor de
/// cálculo del tareo (<c>CalculadoraTareo</c>) no los toca ni los conoce: son un insumo
/// puramente manual que el exportador lee al armar la hoja <c>Tareo</c>, igual que las
/// columnas <c>Ausencias</c> / <c>Dias Efectivas</c> / <c>Dias Pendientes</c> /
/// <c>Dias Trabajados</c> que ya vienen vacías del lado del motor.
///
/// Los 4+1+4 primeros campos (horas normales/25 %/35 %/100 % diurnas, movilidad, y su
/// espejo nocturno) reproducen la misma estructura del bloque <c>R E S U M E N</c>
/// (AE-AM); los 9 últimos son los conceptos nombrados de <c>docs/04-formato-tareo-final.md</c>,
/// en el mismo orden en que aparecen en el archivo modelo de GCA (VIATICO … DESC. PAGO
/// INDEBIDO). Solo se edita mientras el periodo esté abierto, igual que el resto del sistema.
/// </remarks>
public class ReintegroTareo : EntidadBase
{
    public long PeriodoId { get; set; }

    public long TrabajadorId { get; set; }

    // ---------- Espejo del bloque "R E S U M E N", horario diurno ----------

    public decimal HorasNormales { get; set; }

    public decimal HorasExtra25 { get; set; }

    public decimal HorasExtra35 { get; set; }

    public decimal HorasNoLaborable { get; set; }

    public decimal MontoMovilidad { get; set; }

    // ---------- Espejo del bloque "R E S U M E N", horario nocturno ----------

    public decimal HorasNocturnas { get; set; }

    public decimal HorasExtra25Nocturnas { get; set; }

    public decimal HorasExtra35Nocturnas { get; set; }

    public decimal HorasNoLaborableNocturnas { get; set; }

    // ---------- Conceptos nombrados (AX-BF) ----------

    public decimal Viatico { get; set; }

    public int DiasDescansoMedico { get; set; }

    public int DiasSubsidioMaternidad { get; set; }

    public int DiasSubsidioAccidente { get; set; }

    public int DiasLicenciaPaternidad { get; set; }

    public decimal LicenciaConGoce { get; set; }

    public decimal ReintegroAsignacionFamiliar { get; set; }

    public decimal ReintegroAfecto { get; set; }

    public decimal DescuentoPagoIndebido { get; set; }
}
