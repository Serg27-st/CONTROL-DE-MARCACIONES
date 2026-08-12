using ControlMarcacion.Domain.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlMarcacion.Infrastructure.Persistencia.Configuraciones;

public class RegistroAuditoriaConfiguracion : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("RegistroAuditoria");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Tabla).HasMaxLength(80).IsRequired();
        builder.Property(r => r.ClaveRegistro).HasMaxLength(120).IsRequired();
        builder.Property(r => r.Operacion).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Usuario).HasMaxLength(150).IsRequired();

        // El detalle es JSON de largo variable: una modificación de un campo ocupa unos
        // 60 caracteres y el alta de un trabajador pasa de 1 000. Es la única columna del
        // sistema sin largo máximo, y va sin índice a propósito — no se busca por su
        // contenido, se busca por tabla, registro, usuario o fecha.
        builder.Property(r => r.Detalle).HasColumnType("NVARCHAR(MAX)").IsRequired();

        // La pantalla de auditoría lista del más reciente al más antiguo.
        builder.HasIndex(r => r.FechaHora).IsDescending();

        // "Qué le pasó a este registro": la consulta que resuelve la pestaña de historial
        // de un trabajador y la que se usa cuando alguien reclama un dato cambiado.
        builder.HasIndex(r => new { r.Tabla, r.ClaveRegistro, r.FechaHora });

        // "Qué hizo este usuario", para revisar la actividad de una cuenta concreta.
        builder.HasIndex(r => new { r.Usuario, r.FechaHora });
    }
}
