using ControlMarcacion.Domain.Catalogos;
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
        //
        // ⚠️ EquipoId forma parte de la clave, y no es opcional. Sin él, dos aparatos que
        // numeran a su gente desde el 1 producen marcaciones legítimamente distintas —el
        // usuario 14 del A-L y el 14 del M-Z, a la misma hora— que el índice tomaría por
        // repetidas y descartaría en silencio, contadas como "duplicadas" en el resumen
        // de la carga. Se pierde media planta sin un solo error a la vista.
        // ---------------------------------------------------------------------------
        builder.HasIndex(m => new { m.EquipoId, m.IdMarcador, m.FechaHora })
            .IsUnique()
            .HasDatabaseName("UQ_marcacion_cruda_equipo_marcador_fecha");

        // El motor de cálculo consulta por el índice único de arriba —aparato, número y
        // rango de fechas—: la marcación ya no guarda el trabajador, se atribuye al calcular
        // (ver AtribucionMarcaciones).

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

        builder.HasOne(m => m.Equipo)
            .WithMany()
            .HasForeignKey(m => m.EquipoId)
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
        builder.Property(i => i.SerieArchivo).HasMaxLength(60);
        builder.Property(i => i.OrigenVerificado).HasConversion<string>().HasMaxLength(20).IsRequired();
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

        builder.HasOne(i => i.Equipo)
            .WithMany()
            .HasForeignKey(i => i.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);

        // El calendario de cobertura y el historial se consultan por equipo cuando hay
        // más de un aparato en la planta: "¿qué días trae el marcador A-L?".
        builder.HasIndex(i => new { i.EquipoId, i.FechaCarga });
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
