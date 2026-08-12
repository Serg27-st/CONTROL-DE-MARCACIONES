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

/// <summary>
/// Centro SAP. Es un catálogo hermano de <see cref="Planta"/> (ambos cuelgan de
/// <see cref="Empresa"/>), no un reemplazo: la relación con las 4 plantas
/// sembradas no es 1 a 1 y no debe forzarse.
/// </summary>
public class CentroConfiguracion : IEntityTypeConfiguration<Centro>
{
    public void Configure(EntityTypeBuilder<Centro> builder)
    {
        builder.ToTable("Centro");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Descripcion).HasMaxLength(150);

        // El código de centro SAP es único dentro de cada empresa, mismo criterio
        // que el código de planta: no es único a nivel global.
        builder.HasIndex(c => new { c.EmpresaId, c.Codigo }).IsUnique();

        // Sin colección inversa en Empresa: relación unidireccional, igual que
        // AsignacionTurno → Turno.
        builder.HasOne(c => c.Empresa)
            .WithMany()
            .HasForeignKey(c => c.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.CentrosCosto)
            .WithOne(cc => cc.Centro)
            .HasForeignKey(cc => cc.CentroId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CentroCostoConfiguracion : IEntityTypeConfiguration<CentroCosto>
{
    public void Configure(EntityTypeBuilder<CentroCosto> builder)
    {
        builder.ToTable("CentroCosto");
        builder.HasKey(cc => cc.Id);

        builder.Property(cc => cc.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(cc => cc.Descripcion).HasMaxLength(200).IsRequired();

        // El código (co_secc de SAP) NO es único a nivel global: 9 códigos se
        // repiten bajo distintos centros en el catálogo real entregado por GCA.
        // El índice ayuda a la cascada Centro → Centro de costo sin exigir
        // unicidad, que sería un dato falso.
        builder.HasIndex(cc => new { cc.CentroId, cc.Codigo });
    }
}
