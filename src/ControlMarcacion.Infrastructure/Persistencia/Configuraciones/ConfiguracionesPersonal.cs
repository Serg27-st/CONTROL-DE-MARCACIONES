using ControlMarcacion.Domain.Organizacion;
using ControlMarcacion.Domain.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlMarcacion.Infrastructure.Persistencia.Configuraciones;

public class TrabajadorConfiguracion : IEntityTypeConfiguration<Trabajador>
{
    public void Configure(EntityTypeBuilder<Trabajador> builder)
    {
        builder.ToTable("Trabajador");
        builder.HasKey(t => t.Id);

        // ---- Datos personales ----
        // El número de documento es TEXTO: un DNI que empieza en cero pierde el dígito
        // si se guarda como número, y el cruce con el marcador falla.
        builder.Property(t => t.NumeroDocumento).HasMaxLength(20).IsRequired();
        builder.Property(t => t.ApellidoPaterno).HasMaxLength(80).IsRequired();
        builder.Property(t => t.ApellidoMaterno).HasMaxLength(80).IsRequired();
        builder.Property(t => t.Nombres).HasMaxLength(120).IsRequired();
        builder.Property(t => t.Sexo).HasConversion<string>().HasMaxLength(15).IsRequired();
        builder.Property(t => t.Telefono).HasMaxLength(30);
        builder.Property(t => t.Direccion).HasMaxLength(250);
        builder.Property(t => t.CorreoElectronico).HasMaxLength(150);

        // ---- Datos laborales ----
        builder.Property(t => t.Unidad).HasMaxLength(120);
        builder.Property(t => t.TipoPlanilla).HasConversion<string>().HasMaxLength(15).IsRequired();

        // ---- Cese ----
        // Enum como texto, igual que el resto: un 4 en la columna no le dice nada a quien
        // consulte la base directamente, y "AbandonoLaboral" sí.
        builder.Property(t => t.MotivoCese).HasConversion<string>().HasMaxLength(30);
        builder.Property(t => t.ObservacionCese).HasMaxLength(300);

        // Un mismo número puede repetirse entre tipos de documento distintos,
        // así que la unicidad es por la combinación.
        builder.HasIndex(t => new { t.TipoDocumentoId, t.NumeroDocumento }).IsUnique();

        // Los listados de personal siempre filtran por empresa: son personas jurídicas
        // distintas y sus padrones no deben mezclarse.
        builder.HasIndex(t => new { t.EmpresaId, t.Activo });
        builder.HasIndex(t => new { t.PlantaId, t.AreaId, t.SeccionId });

        builder.Ignore(t => t.ApellidosYNombres);

        builder.HasMany(t => t.AsignacionesMarcador)
            .WithOne(a => a.Trabajador)
            .HasForeignKey(a => a.TrabajadorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.AsignacionesTurno)
            .WithOne(a => a.Trabajador)
            .HasForeignKey(a => a.TrabajadorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.AsignacionesSueldo)
            .WithOne(a => a.Trabajador)
            .HasForeignKey(a => a.TrabajadorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.PeriodosLaborales)
            .WithOne(p => p.Trabajador)
            .HasForeignKey(p => p.TrabajadorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(t => t.EstaVigente);
    }
}

public class PeriodoLaboralConfiguracion : IEntityTypeConfiguration<PeriodoLaboral>
{
    public void Configure(EntityTypeBuilder<PeriodoLaboral> builder)
    {
        builder.ToTable("PeriodoLaboral");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.MotivoCese).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.ObservacionCese).HasMaxLength(300);

        // La pantalla de historial pide los vínculos de un trabajador ordenados por fecha.
        builder.HasIndex(p => new { p.TrabajadorId, p.FechaIngreso });

        // Restrict y no Cascade: la empresa es un catálogo con dos filas que nadie borra,
        // y un cascade desde ahí arrastraría el historial laboral completo.
        builder.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(p => p.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(p => p.EstaAbierto);
    }
}

public class AsignacionMarcadorConfiguracion : IEntityTypeConfiguration<AsignacionMarcador>
{
    public void Configure(EntityTypeBuilder<AsignacionMarcador> builder)
    {
        builder.ToTable("AsignacionMarcador");
        builder.HasKey(a => a.Id);

        // Texto, para conservar los ceros a la izquierda ("00123").
        builder.Property(a => a.IdMarcador).HasMaxLength(30).IsRequired();

        // Índice clave de la ingesta: por cada marcación del TXT hay que resolver
        // qué trabajador tenía ese ID en esa fecha.
        builder.HasIndex(a => new { a.IdMarcador, a.FechaInicio, a.FechaFin });
        builder.HasIndex(a => new { a.TrabajadorId, a.FechaInicio });

        builder.HasIndex(a => a.TrabajadorId)
            .IsUnique()
            .HasFilter("[fecha_fin] IS NULL")
            .HasDatabaseName("UQ_asignacion_marcador_abierta");
    }
}

public class AsignacionTurnoConfiguracion : IEntityTypeConfiguration<AsignacionTurno>
{
    public void Configure(EntityTypeBuilder<AsignacionTurno> builder)
    {
        builder.ToTable("AsignacionTurno");
        builder.HasKey(a => a.Id);

        // El motor resuelve el turno vigente para cada trabajador y cada día del periodo:
        // sin este índice, el cálculo mensual se arrastra.
        builder.HasIndex(a => new { a.TrabajadorId, a.FechaInicio, a.FechaFin });

        // ---------------------------------------------------------------------------
        // UNA SOLA ASIGNACIÓN ABIERTA POR TRABAJADOR.
        //
        // La comprobación de solapamiento vive en Application (ValidadorVigencias) y es
        // la que da el mensaje entendible, pero no basta: no ve dos peticiones a la vez
        // ni un UPDATE hecho a mano sobre la tabla. En un sistema donde el turno decide
        // cuántas horas extras se pagan, la última palabra tiene que tenerla la base.
        //
        // Es un índice único FILTRADO: solo aplica a las filas con FechaFin nula, así
        // que un trabajador puede tener toda la historia de rotaciones que quiera —
        // decenas de filas cerradas— pero una sola vigente.
        //
        // ⚠️ El filtro se escribe con el nombre de la COLUMNA ("fecha_fin"), no con el de
        // la propiedad ("FechaFin"): es SQL literal que EF pasa tal cual al CREATE INDEX,
        // y esta base usa snake_case. Con el nombre de la propiedad la migración falla con
        // "Invalid column name 'FechaFin'" — que es exactamente lo que pasaba, y por eso
        // estos tres índices nunca llegaron a existir en la base.
        //
        // ⚠️ El proveedor InMemory de los tests ignora los índices: esta defensa no la
        // ejerce ningún test y solo actúa contra SQL Server real.
        // ---------------------------------------------------------------------------
        builder.HasIndex(a => a.TrabajadorId)
            .IsUnique()
            .HasFilter("[fecha_fin] IS NULL")
            .HasDatabaseName("UQ_asignacion_turno_abierta");

        builder.HasOne(a => a.Turno)
            .WithMany()
            .HasForeignKey(a => a.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AsignacionSueldoConfiguracion : IEntityTypeConfiguration<AsignacionSueldo>
{
    public void Configure(EntityTypeBuilder<AsignacionSueldo> builder)
    {
        builder.ToTable("AsignacionSueldo");
        builder.HasKey(a => a.Id);

        // Igual que con el marcador y el turno: sin este índice, resolver el sueldo
        // vigente de cada trabajador para cada día del periodo se arrastra.
        builder.HasIndex(a => new { a.TrabajadorId, a.FechaInicio, a.FechaFin });

        builder.HasIndex(a => a.TrabajadorId)
            .IsUnique()
            .HasFilter("[fecha_fin] IS NULL")
            .HasDatabaseName("UQ_asignacion_sueldo_abierta");
    }
}
