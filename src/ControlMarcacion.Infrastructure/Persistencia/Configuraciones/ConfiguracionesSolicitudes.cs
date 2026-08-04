using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Solicitudes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlMarcacion.Infrastructure.Persistencia.Configuraciones;

public class SolicitudAusenciaConfiguracion : IEntityTypeConfiguration<SolicitudAusencia>
{
    public void Configure(EntityTypeBuilder<SolicitudAusencia> builder)
    {
        builder.ToTable("SolicitudAusencia");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Sustento).HasMaxLength(500);
        builder.Property(s => s.RutaDocumentoSustento).HasMaxLength(400);
        builder.Property(s => s.Observacion).HasMaxLength(500);
        builder.Property(s => s.MotivoAnulacion).HasMaxLength(500);

        // Se consulta por trabajador y rango al calcular el tareo, y al validar solapamientos.
        builder.HasIndex(s => new { s.TrabajadorId, s.FechaInicio, s.FechaFin });
        builder.HasIndex(s => new { s.Estado, s.FechaInicio });

        builder.HasOne(s => s.TipoAusencia)
            .WithMany()
            .HasForeignKey(s => s.TipoAusenciaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class JustificacionConfiguracion : IEntityTypeConfiguration<Justificacion>
{
    public void Configure(EntityTypeBuilder<Justificacion> builder)
    {
        builder.ToTable("Justificacion");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Origen).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(j => j.Tipo).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(j => j.EstadoAnterior).HasMaxLength(30);
        builder.Property(j => j.Motivo).HasMaxLength(500).IsRequired();
        builder.Property(j => j.RutaDocumentoSustento).HasMaxLength(400);
        builder.Property(j => j.RegistradoPorUsuario).HasMaxLength(60).IsRequired();

        // El cálculo cruza justificaciones por trabajador y fecha.
        builder.HasIndex(j => new { j.TrabajadorId, j.Fecha });
        builder.HasIndex(j => j.PeriodoId);

        builder.HasOne<SolicitudAusencia>()
            .WithMany()
            .HasForeignKey(j => j.SolicitudAusenciaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
