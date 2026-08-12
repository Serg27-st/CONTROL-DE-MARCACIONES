using ControlMarcacion.Domain.Organizacion;
using ControlMarcacion.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlMarcacion.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// Datos personales de la cuenta de usuario.
/// </summary>
/// <remarks>
/// El resto del mapeo de <c>ApplicationUser</c> lo aporta Identity; aquí solo se acotan las
/// columnas propias. <c>TipoDocumentoId</c> se deja sin clave foránea a propósito: la tabla
/// de usuarios es de Identity y no debe quedar amarrada al catálogo, así que la existencia
/// del tipo de documento se valida en <c>ServicioUsuarios</c>, donde además se puede
/// devolver un mensaje entendible en vez de un error de motor.
/// </remarks>
public class ApplicationUserConfiguracion : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Property(u => u.NombreCompleto).HasMaxLength(200).IsRequired();
        builder.Property(u => u.ApellidoPaterno).HasMaxLength(100);
        builder.Property(u => u.ApellidoMaterno).HasMaxLength(100);
        builder.Property(u => u.Nombres).HasMaxLength(120);

        // Documento de identidad: NVARCHAR, nunca numérico — los ceros a la izquierda
        // se pierden y el dato deja de coincidir con el de la ficha de personal.
        builder.Property(u => u.NumeroDocumento).HasMaxLength(20);
    }
}

/// <summary>
/// Plantas que gestiona una cuenta con rol ADMINISTRADOR.
/// </summary>
public class UsuarioPlantaConfiguracion : IEntityTypeConfiguration<UsuarioPlanta>
{
    public void Configure(EntityTypeBuilder<UsuarioPlanta> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UsuarioPlanta");

        // Clave compuesta: la misma planta no se asigna dos veces a la misma cuenta, y no
        // hace falta un Id sustituto para una tabla de cruce.
        builder.HasKey(up => new { up.UsuarioId, up.PlantaId });

        builder.Property(up => up.UsuarioId).HasMaxLength(200).IsRequired();

        builder.HasOne(up => up.Usuario)
            .WithMany(u => u.PlantasGestionadas)
            .HasForeignKey(up => up.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, como el resto del sistema: una planta no se borra físicamente, y si
        // alguien lo intentara, tener cuentas colgando de ella es motivo suficiente para
        // que el motor lo impida.
        builder.HasOne<Planta>()
            .WithMany()
            .HasForeignKey(up => up.PlantaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(up => up.PlantaId);
    }
}
