using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.MarcacionesProvisionales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlMarcacion.Infrastructure.Persistencia.Configuraciones;

// ──────────────────────────────────────────────────────────────────────────────
// Módulo provisional — eliminar este archivo y las tablas prov_* cuando el
// motor principal cubra este flujo.
// ──────────────────────────────────────────────────────────────────────────────

public class ImportacionProvisionalConfiguracion : IEntityTypeConfiguration<ImportacionProvisional>
{
    public void Configure(EntityTypeBuilder<ImportacionProvisional> builder)
    {
        builder.ToTable("prov_importacion");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.NombreArchivo).HasMaxLength(260).IsRequired();
        builder.Property(i => i.HashContenido).HasMaxLength(64).IsRequired();
        builder.Property(i => i.CargadoPorUsuario).HasMaxLength(60).IsRequired();

        builder.HasIndex(i => i.HashContenido)
            .HasDatabaseName("IX_prov_importacion_hash");

        builder.HasIndex(i => new { i.EquipoId, i.FechaCarga })
            .HasDatabaseName("IX_prov_importacion_equipo_fecha");

        builder.HasMany(i => i.Errores)
            .WithOne()
            .HasForeignKey(e => e.ImportacionProvisionalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Equipo)
            .WithMany()
            .HasForeignKey(i => i.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MarcacionProvisionalConfiguracion : IEntityTypeConfiguration<MarcacionProvisional>
{
    public void Configure(EntityTypeBuilder<MarcacionProvisional> builder)
    {
        builder.ToTable("prov_marcacion");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.IdMarcador).HasMaxLength(30).IsRequired();
        builder.Property(m => m.ModoVerificacion).HasMaxLength(30);
        builder.Property(m => m.LineaOriginal).HasMaxLength(500);

        builder.HasIndex(m => new { m.EquipoId, m.IdMarcador, m.FechaHora })
            .IsUnique()
            .HasDatabaseName("UQ_prov_marcacion_equipo_marcador_fecha");

        builder.HasIndex(m => m.FechaHora)
            .HasDatabaseName("IX_prov_marcacion_fecha");

        builder.HasIndex(m => m.ImportacionProvisionalId)
            .HasDatabaseName("IX_prov_marcacion_importacion");

        builder.HasOne<ImportacionProvisional>()
            .WithMany()
            .HasForeignKey(m => m.ImportacionProvisionalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EquipoMarcador>()
            .WithMany()
            .HasForeignKey(m => m.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ErrorImportacionProvisionalConfiguracion : IEntityTypeConfiguration<ErrorImportacionProvisional>
{
    public void Configure(EntityTypeBuilder<ErrorImportacionProvisional> builder)
    {
        builder.ToTable("prov_error_importacion");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ContenidoLinea).HasMaxLength(500);
        builder.Property(e => e.Motivo).HasMaxLength(300).IsRequired();

        builder.HasIndex(e => e.ImportacionProvisionalId)
            .HasDatabaseName("IX_prov_error_importacion");
    }
}
