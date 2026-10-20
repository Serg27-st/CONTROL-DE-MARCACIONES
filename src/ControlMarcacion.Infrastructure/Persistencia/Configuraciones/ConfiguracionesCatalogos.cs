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

        // Minutos tras el fin que no cuentan como sobretiempo. Sin HasDefaultValue: el 0 es
        // a la vez el valor de la columna para los turnos existentes y el de CLR, y es la
        // decisión por omisión correcta —nada cambia hasta que GCA fije otra cosa—.
        builder.Property(t => t.MinutosGraciaSalida).IsRequired();

        // Propiedades calculadas del dominio: no se persisten.
        builder.Ignore(t => t.CruzaMedianoche);
        builder.Ignore(t => t.DuracionBrutaMinutos);
        builder.Ignore(t => t.DuracionEfectivaMinutos);
        builder.Ignore(t => t.InicioVentanaEntrada);
        builder.Ignore(t => t.InicioVentanaSalida);
        builder.Ignore(t => t.LimiteTolerancia);
        builder.Ignore(t => t.LimiteGraciaSalida);

        builder.HasMany(t => t.PlantasHabilitadas)
            .WithOne(tp => tp.Turno)
            .HasForeignKey(tp => tp.TurnoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VersionTurnoConfiguracion : IEntityTypeConfiguration<VersionTurno>
{
    public void Configure(EntityTypeBuilder<VersionTurno> builder)
    {
        builder.ToTable("VersionTurno");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.DiasAplicables).HasConversion<string>().HasMaxLength(60);

        builder.HasOne(v => v.Turno)
            .WithMany(t => t.Versiones)
            .HasForeignKey(v => v.TurnoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Como lo lee el recálculo: las versiones de estos turnos que se solapan con el
        // periodo.
        builder.HasIndex(v => new { v.TurnoId, v.FechaInicio });

        // Una sola versión abierta por turno, garantizado por la base y no solo por el
        // servicio. ⚠️ El filtro va con el nombre de la COLUMNA —el SQL viaja literal—, y
        // los tests con InMemory no lo detectan porque ese proveedor ignora los índices.
        builder.HasIndex(v => v.TurnoId)
            .IsUnique()
            .HasFilter("[fecha_fin] IS NULL AND [activo] = 1")
            .HasDatabaseName("UQ_version_turno_abierta");
    }
}

public class CuadrillaConfiguracion : IEntityTypeConfiguration<Cuadrilla>
{
    public void Configure(EntityTypeBuilder<Cuadrilla> builder)
    {
        builder.ToTable("Cuadrilla");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Nombre).HasMaxLength(80).IsRequired();

        // Único por planta y no en todo el sistema: «A» y «B» son los nombres naturales en
        // cada planta.
        builder.HasIndex(c => new { c.PlantaId, c.Codigo }).IsUnique();

        builder.HasOne(c => c.Planta)
            .WithMany()
            .HasForeignKey(c => c.PlantaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Programaciones)
            .WithOne(p => p.Cuadrilla)
            .HasForeignKey(p => p.CuadrillaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProgramacionCuadrillaConfiguracion : IEntityTypeConfiguration<ProgramacionCuadrilla>
{
    public void Configure(EntityTypeBuilder<ProgramacionCuadrilla> builder)
    {
        builder.ToTable("ProgramacionCuadrilla");
        builder.HasKey(p => p.Id);

        // El motor busca la programación vigente de cada cuadrilla en cada fecha.
        builder.HasIndex(p => new { p.CuadrillaId, p.FechaInicio, p.FechaFin });

        builder.HasMany(p => p.Tramos)
            .WithOne(t => t.Programacion)
            .HasForeignKey(t => t.ProgramacionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Propiedad calculada: no se persiste.
        builder.Ignore(p => p.DiasDelCiclo);
    }
}

public class TramoRotacionConfiguracion : IEntityTypeConfiguration<TramoRotacion>
{
    public void Configure(EntityTypeBuilder<TramoRotacion> builder)
    {
        builder.ToTable("TramoRotacion", tabla =>
        {
            tabla.HasCheckConstraint(
                "CK_tramo_rotacion_tipo_turno",
                "(tipo = 'Trabajo' AND turno_id IS NOT NULL) OR " +
                "(tipo = 'Descanso' AND turno_id IS NULL)");
            tabla.HasCheckConstraint(
                "CK_tramo_rotacion_descanso_un_dia",
                "tipo <> 'Descanso' OR dias = 1");
            tabla.HasCheckConstraint("CK_tramo_rotacion_dias_positivos", "dias > 0");
            tabla.HasCheckConstraint("CK_tramo_rotacion_orden_positivo", "orden > 0");
        });
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Tipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(t => new { t.ProgramacionId, t.Orden }).IsUnique();

        builder.HasOne(t => t.Turno)
            .WithMany()
            .HasForeignKey(t => t.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);
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

        // Concurrencia optimista: lo que hace atómica la regla «periodo cerrado, nada se
        // escribe». Quien escribe dentro del periodo afirma esta versión en el WHERE de su
        // UPDATE, así que un cierre que se cuele en medio invalida la escritura entera.
        builder.Property(p => p.Version).IsConcurrencyToken();

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

        // ---------------------------------------------------------------------------
        // Único DENTRO DE SU EQUIPO, no en todo el sistema.
        // Antes era global, porque el aparato no se modelaba y el código era la única
        // identidad disponible. Eso impedía registrar a la segunda mitad del personal
        // cuando una planta tiene dos equipos que numeran a su gente desde el 1 — el caso
        // real de Planta Detergentes, con un marcador para apellidos A-L y otro para M-Z.
        // ---------------------------------------------------------------------------
        builder.HasIndex(m => new { m.EquipoId, m.Codigo })
            .IsUnique()
            .HasDatabaseName("UQ_marcador_equipo_codigo");

        // El formulario de Personal filtra los marcadores por la planta del trabajador.
        builder.HasIndex(m => new { m.PlantaId, m.Activo });

        builder.HasOne(m => m.Equipo)
            .WithMany()
            .HasForeignKey(m => m.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Planta)
            .WithMany()
            .HasForeignKey(m => m.PlantaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// El aparato ZKTeco físico. Ver <see cref="EquipoMarcador"/>.
/// </summary>
public class EquipoMarcadorConfiguracion : IEntityTypeConfiguration<EquipoMarcador>
{
    public void Configure(EntityTypeBuilder<EquipoMarcador> builder)
    {
        builder.ToTable("EquipoMarcador");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Codigo).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Serie).HasMaxLength(60);
        builder.Property(e => e.Descripcion).HasMaxLength(150);

        builder.HasIndex(e => e.Codigo).IsUnique();

        // La serie identifica físicamente al aparato, así que no puede repetirse. El
        // filtro deja fuera los equipos sin serie declarada: son varios y todos con NULL,
        // y un único sin filtro los tomaría por duplicados entre sí.
        // ⚠️ El filtro va con el nombre de la COLUMNA (serie), no el de la propiedad:
        // el SQL viaja literal.
        builder.HasIndex(e => e.Serie)
            .IsUnique()
            .HasFilter("[serie] IS NOT NULL")
            .HasDatabaseName("UQ_equipo_marcador_serie");

        builder.HasIndex(e => new { e.PlantaId, e.Activo });

        builder.HasOne(e => e.Planta)
            .WithMany()
            .HasForeignKey(e => e.PlantaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
