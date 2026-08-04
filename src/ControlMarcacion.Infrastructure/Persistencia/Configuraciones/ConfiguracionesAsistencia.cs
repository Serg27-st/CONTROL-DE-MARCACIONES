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
