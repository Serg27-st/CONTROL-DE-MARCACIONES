using ControlMarcacion.Domain.Organizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlMarcacion.Infrastructure.Persistencia.Configuraciones;

public class ClienteConfiguracion : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Cliente");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(c => c.RazonSocial).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Ruc).HasMaxLength(11);

        builder.HasIndex(c => c.Codigo).IsUnique();

        builder.HasMany(c => c.Empresas)
            .WithOne(e => e.Cliente)
            .HasForeignKey(e => e.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class EmpresaConfiguracion : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.ToTable("Empresa");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(e => e.RazonSocial).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Ruc).HasMaxLength(11).IsRequired();

        builder.HasIndex(e => e.Codigo).IsUnique();
        builder.HasIndex(e => e.Ruc).IsUnique();

        builder.HasMany(e => e.Plantas)
            .WithOne(p => p.Empresa)
            .HasForeignKey(p => p.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PlantaConfiguracion : IEntityTypeConfiguration<Planta>
{
    public void Configure(EntityTypeBuilder<Planta> builder)
    {
        builder.ToTable("Planta");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Nombre).HasMaxLength(120).IsRequired();

        // El código de planta es único dentro de cada empresa, no a nivel global:
        // dos razones sociales podrían usar la misma nomenclatura interna.
        builder.HasIndex(p => new { p.EmpresaId, p.Codigo }).IsUnique();

        builder.HasMany(p => p.Areas)
            .WithOne(a => a.Planta)
            .HasForeignKey(a => a.PlantaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AreaConfiguracion : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Area");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Nombre).HasMaxLength(120).IsRequired();

        builder.HasIndex(a => new { a.PlantaId, a.Codigo }).IsUnique();

        builder.HasMany(a => a.Secciones)
            .WithOne(s => s.Area)
            .HasForeignKey(s => s.AreaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SeccionConfiguracion : IEntityTypeConfiguration<Seccion>
{
    public void Configure(EntityTypeBuilder<Seccion> builder)
    {
        builder.ToTable("Seccion");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Nombre).HasMaxLength(120).IsRequired();

        builder.HasIndex(s => new { s.AreaId, s.Codigo }).IsUnique();
    }
}
