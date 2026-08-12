using ControlMarcacion.Domain.Marcaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlMarcacion.Infrastructure.Persistencia.Configuraciones;

public class MarcacionCrudaConfiguracion : IEntityTypeConfiguration<MarcacionCruda>
{
    public void Configure(EntityTypeBuilder<MarcacionCruda> builder)
    {
        builder.ToTable("MarcacionCruda");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.IdMarcador).HasMaxLength(30).IsRequired();
        builder.Property(m => m.ModoVerificacion).HasMaxLength(30);
        builder.Property(m => m.TipoMarcacion).HasMaxLength(30);
        builder.Property(m => m.LineaOriginal).HasMaxLength(500);

        // ---------------------------------------------------------------------------
        // ÍNDICE ÚNICO CRÍTICO.
        // Es la defensa REAL contra recargar dos veces el mismo TXT. No lo sustituyas
        // por una validación en C#: entre la comprobación y el INSERT hay una ventana,
        // y una carga concurrente duplicaría marcaciones que luego inflan horas extras.
        // ---------------------------------------------------------------------------
        builder.HasIndex(m => new { m.TrabajadorId, m.FechaHora, m.IdMarcador })
            .IsUnique()
            .HasDatabaseName("UQ_marcacion_cruda_trabajador_fecha_marcador");

        // El motor de cálculo siempre consulta así: trabajador + rango de fechas.
        builder.HasIndex(m => new { m.TrabajadorId, m.FechaHora });

        // Barridos por rango para reportes de marcaciones crudas.
        builder.HasIndex(m => m.FechaHora);

        // Qué días trae cada archivo cargado. El historial lo pregunta por MIN/MAX sobre
        // hasta 50 importaciones a la vez; sin este índice cada una obliga a recorrer la
        // tabla entera de marcaciones, que es la más grande del sistema.
        builder.HasIndex(m => new { m.ImportacionId, m.FechaHora });

        builder.HasOne<ImportacionMarcador>()
            .WithMany()
            .HasForeignKey(m => m.ImportacionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ImportacionMarcadorConfiguracion : IEntityTypeConfiguration<ImportacionMarcador>
{
    public void Configure(EntityTypeBuilder<ImportacionMarcador> builder)
    {
        builder.ToTable("ImportacionMarcador");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.NombreArchivo).HasMaxLength(260).IsRequired();
        builder.Property(i => i.HashContenido).HasMaxLength(64).IsRequired();
        builder.Property(i => i.CargadoPorUsuario).HasMaxLength(60).IsRequired();
        builder.Property(i => i.MotivoAnulacion).HasMaxLength(300);
        builder.Property(i => i.AnuladaPorUsuario).HasMaxLength(60);

        // El cálculo del tareo filtra por esta columna en cada recálculo: sin índice,
        // descartar las cargas anuladas obligaría a recorrer la tabla entera.
        builder.HasIndex(i => i.Anulada);

        // Permite avisar "este archivo ya se cargó el 12/07 a las 14:30" ANTES de
        // procesarlo, en vez de descubrirlo por los duplicados rechazados.
        builder.HasIndex(i => i.HashContenido);
        builder.HasIndex(i => i.FechaCarga);

        builder.HasMany(i => i.Errores)
            .WithOne()
            .HasForeignKey(e => e.ImportacionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ErrorImportacionConfiguracion : IEntityTypeConfiguration<ErrorImportacion>
{
    public void Configure(EntityTypeBuilder<ErrorImportacion> builder)
    {
        builder.ToTable("ErrorImportacion");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ContenidoLinea).HasMaxLength(500);
        builder.Property(e => e.Motivo).HasMaxLength(300).IsRequired();

        builder.HasIndex(e => e.ImportacionId);
    }
}
