using ControlMarcacion.Domain.Marcaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlMarcacion.Infrastructure.Persistencia.Configuraciones;

public sealed class ConciliacionBiometricaConfiguracion : IEntityTypeConfiguration<ConciliacionBiometrica>
{
    public void Configure(EntityTypeBuilder<ConciliacionBiometrica> builder)
    {
        builder.ToTable("ConciliacionBiometrica");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.HuellaEstado).HasMaxLength(64).IsRequired();
        builder.Property(x => x.HuellaRespaldo).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FormatoRespaldo).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.IdentificadorPublico).IsUnique();
        builder.HasIndex(x => new { x.EquipoId, x.ExpiraEn });
        builder.HasOne(x => x.Equipo).WithMany().HasForeignKey(x => x.EquipoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Planta).WithMany().HasForeignKey(x => x.PlantaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DecisionVinculacionBiometricaConfiguracion : IEntityTypeConfiguration<DecisionVinculacionBiometrica>
{
    public void Configure(EntityTypeBuilder<DecisionVinculacionBiometrica> builder)
    {
        builder.ToTable("DecisionVinculacionBiometrica");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Pin).HasMaxLength(30).IsRequired();
        builder.Property(x => x.CategoriaEvidencia).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ReferenciaEvidencia).HasMaxLength(160).IsRequired();
        builder.Property(x => x.HuellaEvidencia).HasMaxLength(64).IsRequired();
        builder.Property(x => x.HuellaEstado).HasMaxLength(64).IsRequired();
        builder.Property(x => x.BaseConfirmacion).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.NaturalezaVigencia).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ClaveIdempotencia).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Resultado).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.MotivoResultado).HasMaxLength(500);
        builder.Property(x => x.Diagnosticos).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ConfirmadaPor).HasMaxLength(120);
        builder.HasIndex(x => new { x.ConciliacionId, x.IndiceRegistro }).IsUnique();
        builder.HasIndex(x => x.ClaveIdempotencia).IsUnique();
        builder.HasOne(x => x.Conciliacion).WithMany(x => x.Decisiones).HasForeignKey(x => x.ConciliacionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Trabajador).WithMany().HasForeignKey(x => x.TrabajadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Equipo).WithMany().HasForeignKey(x => x.EquipoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AsignacionMarcador).WithMany().HasForeignKey(x => x.AsignacionMarcadorId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("CK_decision_vigencia", "[fecha_fin] IS NULL OR [fecha_fin] >= [fecha_inicio]"));
    }
}
