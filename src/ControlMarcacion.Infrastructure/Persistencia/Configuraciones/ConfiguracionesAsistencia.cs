using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Normativa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlMarcacion.Infrastructure.Persistencia.Configuraciones;

public class TareoDiarioConfiguracion : IEntityTypeConfiguration<TareoDiario>
{
    public void Configure(EntityTypeBuilder<TareoDiario> builder)
    {
        builder.ToTable("TareoDiario");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Estado).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(t => t.Observacion).HasMaxLength(500);

        // Distingue descanso semanal de feriado: son artículos legales distintos y cada uno
        // tiene su propia tasa con vigencia. Como texto, para que se lea en la base.
        builder.Property(t => t.TipoDiaNoLaborable)
            .HasConversion<string>().HasMaxLength(20).IsRequired();

        // Derivado de lo anterior: no se persiste.
        builder.Ignore(t => t.EsDiaNoLaborable);

        // La nocturnidad ordinaria es una resta de las otras tres columnas: persistirla
        // sería guardar el mismo dato dos veces y arriesgar que discrepen.
        builder.Ignore(t => t.MinutosOrdinariosNocturnos);

        // ---------------------------------------------------------------------------
        // ÍNDICE ÚNICO CRÍTICO.
        // Un tareo por trabajador y día. Es lo que hace que el recálculo sea idempotente:
        // recalcular el mismo periodo dos veces no puede producir filas duplicadas.
        // ---------------------------------------------------------------------------
        builder.HasIndex(t => new { t.TrabajadorId, t.Fecha })
            .IsUnique()
            .HasDatabaseName("UQ_tareo_diario_trabajador_fecha");

        // Los reportes recorren el tareo por periodo.
        builder.HasIndex(t => new { t.PeriodoId, t.Estado });
        builder.HasIndex(t => t.Fecha);
    }
}

public class TasaSobretasaConfiguracion : IEntityTypeConfiguration<TasaSobretasa>
{
    public void Configure(EntityTypeBuilder<TasaSobretasa> builder)
    {
        builder.ToTable("TasaSobretasa");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Concepto).HasConversion<string>().HasMaxLength(40).IsRequired();

        // DECIMAL(12,4), nunca FLOAT: 25.0000 debe ser exactamente 25.0000.
        builder.Property(t => t.Porcentaje).HasPrecision(12, 4).IsRequired();

        builder.Property(t => t.BaseLegal).HasMaxLength(200);

        // Resolver "qué tasa estaba vigente el 15/07/2026" es la consulta que permite
        // recalcular un periodo antiguo con las reglas de entonces.
        builder.HasIndex(t => new { t.Concepto, t.VigenteDesde, t.VigenteHasta });
    }
}

/// <summary>
/// Valores legales absolutos con vigencia por fecha: RMV, asignación familiar, movilidad.
/// </summary>
/// <remarks>
/// Vive junto a <see cref="TasaSobretasaConfiguracion"/> porque son la misma idea aplicada
/// a dos formas distintas del mismo problema —lo que la ley fija hoy y puede cambiar
/// mañana— y quien vaya a tocar una casi siempre necesita ver la otra.
/// </remarks>
public class ParametroLegalConfiguracion : IEntityTypeConfiguration<ParametroLegal>
{
    public void Configure(EntityTypeBuilder<ParametroLegal> builder)
    {
        builder.ToTable("ParametroLegal");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Tipo).HasConversion<string>().HasMaxLength(40).IsRequired();

        // DECIMAL(12,4), nunca FLOAT: aquí hay soles, y 1130.0000 tiene que ser
        // exactamente 1130.0000 cuando planilla cuadre el bono nocturno a mano.
        builder.Property(p => p.Valor).HasPrecision(12, 4).IsRequired();

        builder.Property(p => p.BaseLegal).HasMaxLength(200);

        // Misma consulta que en las tasas: "qué RMV regía el 15/07/2026".
        builder.HasIndex(p => new { p.Tipo, p.VigenteDesde, p.VigenteHasta });
    }
}

/// <summary>Reintegros y descuentos manuales del bloque AO-BF del tareo final.</summary>
public class ReintegroTareoConfiguracion : IEntityTypeConfiguration<ReintegroTareo>
{
    public void Configure(EntityTypeBuilder<ReintegroTareo> builder)
    {
        builder.ToTable("ReintegroTareo");
        builder.HasKey(r => r.Id);

        // Un solo registro de reintegros por trabajador y periodo: la pantalla de captura
        // edita ese único registro, nunca inserta uno nuevo al lado.
        builder.HasIndex(r => new { r.TrabajadorId, r.PeriodoId })
            .IsUnique()
            .HasDatabaseName("UQ_reintegro_tareo_trabajador_periodo");
    }
}
