using ControlMarcacion.Domain.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlMarcacion.Infrastructure.Persistencia.Configuraciones;

public class TipoDocumentoConfiguracion : IEntityTypeConfiguration<TipoDocumento>
{
    public void Configure(EntityTypeBuilder<TipoDocumento> builder)
    {
        builder.ToTable("TipoDocumento");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Codigo).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Nombre).HasMaxLength(80).IsRequired();

        builder.HasIndex(t => t.Codigo).IsUnique();
    }
}

public class PuestoConfiguracion : IEntityTypeConfiguration<Puesto>
{
    public void Configure(EntityTypeBuilder<Puesto> builder)
    {
        builder.ToTable("Puesto");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Nombre).HasMaxLength(120).IsRequired();

        builder.HasIndex(p => p.Codigo).IsUnique();
    }
}

public class FeriadoConfiguracion : IEntityTypeConfiguration<Feriado>
{
    public void Configure(EntityTypeBuilder<Feriado> builder)
    {
        builder.ToTable("Feriado");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Descripcion).HasMaxLength(150).IsRequired();

        // El motor consulta feriados por fecha para cada día calculado.
        builder.HasIndex(f => new { f.Fecha, f.PlantaId });
    }
}

public class TurnoConfiguracion : IEntityTypeConfiguration<Turno>
{
    public void Configure(EntityTypeBuilder<Turno> builder)
    {
        builder.ToTable("Turno");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Codigo).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Nombre).HasMaxLength(80).IsRequired();

        // Enum persistido como texto: un "2" en la base no le dice nada a quien
        // depura una planilla a las once de la noche.
        builder.Property(t => t.DiasAplicables).HasConversion<string>().HasMaxLength(60);

        builder.HasIndex(t => t.Codigo).IsUnique();

        // Ventanas para decidir si una marcación es la entrada o la salida de este turno.
        // Son configurables por turno, nunca constantes: el equipo no distingue entrada de
        // salida y cada planta tiene su propia costumbre de anticipación.
        builder.Property(t => t.MinutosAnticipacionEntrada).IsRequired().HasDefaultValue(15);
        builder.Property(t => t.MinutosAnticipacionSalida).IsRequired().HasDefaultValue(5);

        // Propiedades calculadas del dominio: no se persisten.
        builder.Ignore(t => t.CruzaMedianoche);
        builder.Ignore(t => t.DuracionBrutaMinutos);
        builder.Ignore(t => t.DuracionEfectivaMinutos);
        builder.Ignore(t => t.InicioVentanaEntrada);
        builder.Ignore(t => t.InicioVentanaSalida);
        builder.Ignore(t => t.LimiteTolerancia);

        builder.HasMany(t => t.PlantasHabilitadas)
            .WithOne(tp => tp.Turno)
            .HasForeignKey(tp => tp.TurnoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TurnoPlantaConfiguracion : IEntityTypeConfiguration<TurnoPlanta>
{
    public void Configure(EntityTypeBuilder<TurnoPlanta> builder)
    {
        builder.ToTable("TurnoPlanta");
        builder.HasKey(tp => tp.Id);

        builder.HasIndex(tp => new { tp.TurnoId, tp.PlantaId }).IsUnique();

        builder.HasOne(tp => tp.Planta)
            .WithMany()
            .HasForeignKey(tp => tp.PlantaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PeriodoConfiguracion : IEntityTypeConfiguration<Periodo>
{
    public void Configure(EntityTypeBuilder<Periodo> builder)
    {
        builder.ToTable("Periodo");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Nombre).HasMaxLength(80).IsRequired();
        builder.Property(p => p.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.MotivoReapertura).HasMaxLength(500);

        builder.HasIndex(p => p.Codigo).IsUnique();

        // El sistema pregunta constantemente "¿en qué periodo cae esta fecha?".
        builder.HasIndex(p => new { p.FechaInicio, p.FechaFin });

        builder.Ignore(p => p.EstaAbierto);
    }
}

public class TipoAusenciaConfiguracion : IEntityTypeConfiguration<TipoAusencia>
{
    public void Configure(EntityTypeBuilder<TipoAusencia> builder)
    {
        builder.ToTable("TipoAusencia");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Nombre).HasMaxLength(120).IsRequired();
        builder.Property(t => t.Comportamiento).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasIndex(t => t.Codigo).IsUnique();
    }
}

public class MarcadorConfiguracion : IEntityTypeConfiguration<Marcador>
{
    public void Configure(EntityTypeBuilder<Marcador> builder)
    {
        builder.ToTable("Marcador");
        builder.HasKey(m => m.Id);

        // Texto, para conservar los ceros a la izquierda ("00123").
        builder.Property(m => m.Codigo).HasMaxLength(30).IsRequired();
        builder.Property(m => m.Descripcion).HasMaxLength(150);

        // Único en TODO el sistema, no por planta. El TXT del G3 no trae ninguna columna
        // que diga de qué planta salió cada marcación: la ingesta resuelve ID → trabajador
        // solo por el texto del ID y la fecha. Dos plantas con el código "001" harían esas
        // marcaciones inatribuibles. Ver Marcador.
        builder.HasIndex(m => m.Codigo).IsUnique();

        // El formulario de Personal filtra los marcadores por la planta del trabajador.
        builder.HasIndex(m => new { m.PlantaId, m.Activo });

        builder.HasOne(m => m.Planta)
            .WithMany()
            .HasForeignKey(m => m.PlantaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
