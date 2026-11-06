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
        builder.Property(s => s.SubtipoVacaciones).HasMaxLength(10);
        builder.Property(s => s.PeriodoVacacional).HasMaxLength(9);
        builder.Property(s => s.Sustento).HasMaxLength(500);
        builder.Property(s => s.RutaDocumentoSustento).HasMaxLength(400);
        builder.Property(s => s.Observacion).HasMaxLength(500);
        builder.Property(s => s.MotivoAnulacion).HasMaxLength(500);

        // Detalle clínico de los tipos subsidiados. Los dos enums se persisten como texto,
        // igual que Estado: un número en la base obliga a mirar el código para saber qué
        // significa un 2, y quien audita esta tabla no siempre tiene el código delante.
        builder.Property(s => s.Contingencia).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.CentroAtencion).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Diagnostico).HasMaxLength(ValidadorDetalleClinico.LargoMaximoDiagnostico);

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
        builder.Property(j => j.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(j => j.TipoIncidencia).HasConversion<string>().HasMaxLength(30);
        builder.Property(j => j.EstadoAnterior).HasMaxLength(30);
        builder.Property(j => j.Motivo).HasMaxLength(500).IsRequired();
        builder.Property(j => j.MotivoResolucion).HasMaxLength(500);
        builder.Property(j => j.RutaDocumentoSustento).HasMaxLength(400);
        builder.Property(j => j.RegistradoPorUsuario).HasMaxLength(60).IsRequired();
        builder.Property(j => j.ResueltaPorUsuario).HasMaxLength(60);

        // Concurrencia optimista: SQLite no tiene rowversion, así que el token es un entero
        // que incrementa quien modifica la fila. EF lo mete en el WHERE del UPDATE y la
        // segunda escritura no encuentra a quién actualizar.
        builder.Property(j => j.Version).IsConcurrencyToken();

        // El cálculo cruza justificaciones por trabajador y fecha, y resuelve UNA sola por
        // día. La unicidad se comprobaba únicamente en C#, con un patrón consultar-e-insertar
        // sin transacción: dos altas simultáneas —o una aprobación corriendo junto a un alta
        // manual— insertaban las dos, y el siguiente recálculo del periodo entero moría al
        // indexar por fecha. El índice es la garantía; la validación en Application se queda
        // porque es la que da el mensaje legible.
        //
        // ⚠️ Ahora es FILTRADO: una regularización rechazada o cancelada se conserva —es la
        // evidencia de que se pidió y de por qué no se concedió— pero no puede bloquear el
        // día para siempre. Sin el filtro, un rechazo dejaría a esa persona sin poder volver
        // a pedir la regularización de ese día nunca más.
        //
        // ⚠️ El texto del filtro viaja LITERAL al SQL: van los nombres de COLUMNA en
        // snake_case, no los de las propiedades. Con el nombre de la propiedad el índice
        // simplemente no llega a crearse, y ningún test con InMemory lo nota.
        builder.HasIndex(j => new { j.TrabajadorId, j.Fecha })
            .IsUnique()
            .HasFilter("estado NOT IN ('Rechazada', 'Cancelada')");

        builder.HasIndex(j => j.PeriodoId);

        // La bandeja de incidencias pregunta "qué hay esperando decisión", que sobre la
        // tabla que más crece del módulo —una fila por trabajador y día de ausencia— es un
        // barrido completo sin este índice.
        builder.HasIndex(j => j.Estado);

        builder.HasOne<SolicitudAusencia>()
            .WithMany()
            .HasForeignKey(j => j.SolicitudAusenciaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class SolicitudCambioTurnoConfiguracion : IEntityTypeConfiguration<SolicitudCambioTurno>
{
    public void Configure(EntityTypeBuilder<SolicitudCambioTurno> builder)
    {
        builder.ToTable("SolicitudCambioTurno");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Motivo).HasMaxLength(500).IsRequired();
        builder.Property(s => s.SolicitadoPorUsuario).HasMaxLength(60).IsRequired();
        builder.Property(s => s.ResueltoPorUsuario).HasMaxLength(60);
        builder.Property(s => s.MotivoResolucion).HasMaxLength(500);
        builder.Property(s => s.Version).IsConcurrencyToken();
        builder.HasIndex(s => new { s.TrabajadorId, s.FechaEfectiva })
            .IsUnique()
            .HasFilter("estado IN ('Pendiente', 'Aprobada')");
        builder.HasIndex(s => s.Estado);
        builder.HasOne(s => s.TurnoSolicitado)
            .WithMany()
            .HasForeignKey(s => s.TurnoSolicitadoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

