using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Normativa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlMarcacion.Infrastructure.Persistencia.Configuraciones;

public class TareoDiarioConfiguracion : IEntityTypeConfiguration<TareoDiario>
{
    public void Configure(EntityTypeBuilder<TareoDiario> builder)
    {
        builder.ToTable("TareoDiario");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Estado).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(t => t.Observacion).HasMaxLength(500);

        // Distingue descanso semanal de feriado: son artículos legales distintos y cada uno
        // tiene su propia tasa con vigencia. Como texto, para que se lea en la base.
        builder.Property(t => t.TipoDiaNoLaborable)
            .HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(t => t.CoincidenciaFeriadoDescanso)
            .HasConversion<string>().HasMaxLength(30).IsRequired();

        // Derivado de lo anterior: no se persiste.
        builder.Ignore(t => t.EsDiaNoLaborable);

        // La nocturnidad ordinaria es una resta de las otras tres columnas: persistirla
        // sería guardar el mismo dato dos veces y arriesgar que discrepen.
        builder.Ignore(t => t.MinutosOrdinariosNocturnos);

        // ---------------------------------------------------------------------------
        // ÍNDICE ÚNICO CRÍTICO.
        // Un tareo por trabajador y día. Es lo que hace que el recálculo sea idempotente:
        // recalcular el mismo periodo dos veces no puede producir filas duplicadas.
        // ---------------------------------------------------------------------------
        builder.HasIndex(t => new { t.TrabajadorId, t.Fecha })
            .IsUnique()
            .HasDatabaseName("UQ_tareo_diario_trabajador_fecha");

        // Qué hay que resolver en el día. Como texto, igual que el estado: una columna que
        // se lee en la base vale más que un número que hay que traducir a mano.
        builder.Property(t => t.TipoIncidencia).HasConversion<string>().HasMaxLength(30);

        // Los reportes recorren el tareo por periodo.
        builder.HasIndex(t => new { t.PeriodoId, t.Estado });
        builder.HasIndex(t => t.Fecha);

        // La bandeja de incidencias pregunta por el periodo lo que espera decisión, que son
        // unas decenas de filas dentro de decenas de miles. Filtrado, para que el índice
        // pese lo que pesa la bandeja y no lo que pesa el tareo entero.
        //
        // ⚠️ El filtro va con el nombre de la COLUMNA, no el de la propiedad: el texto
        // viaja literal al SQL y con el nombre equivocado el índice no llega a existir.
        builder.HasIndex(t => new { t.PeriodoId, t.RequiereValidacion })
            .HasFilter("requiere_validacion = 1")
            .HasDatabaseName("ix_tareo_diario_pendientes_de_validacion");
    }
}

public class TasaSobretasaConfiguracion : IEntityTypeConfiguration<TasaSobretasa>
{
    public void Configure(EntityTypeBuilder<TasaSobretasa> builder)
    {
        builder.ToTable("TasaSobretasa");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Concepto).HasConversion<string>().HasMaxLength(40).IsRequired();

        // DECIMAL(12,4), nunca FLOAT: 25.0000 debe ser exactamente 25.0000.
        builder.Property(t => t.Porcentaje).HasPrecision(12, 4).IsRequired();

        builder.Property(t => t.BaseLegal).HasMaxLength(200);

        // Resolver "qué tasa estaba vigente el 15/07/2026" es la consulta que permite
        // recalcular un periodo antiguo con las reglas de entonces.
        builder.HasIndex(t => new { t.Concepto, t.VigenteDesde, t.VigenteHasta });
    }
}

public sealed class DescansoSustitutorioConfiguracion
    : IEntityTypeConfiguration<DescansoSustitutorio>
{
    public void Configure(EntityTypeBuilder<DescansoSustitutorio> builder)
    {
        builder.ToTable("DescansoSustitutorio");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.AprobadoPorUsuario).HasMaxLength(200).IsRequired();
        builder.Property(d => d.MotivoAnulacion).HasMaxLength(500);
        builder.Property(d => d.Version).IsConcurrencyToken();

        builder.HasOne<ControlMarcacion.Domain.Personal.Trabajador>()
            .WithMany()
            .HasForeignKey(d => d.TrabajadorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Un feriado trabajado admite como máximo una sustitución vigente. Las anuladas se
        // conservan para auditoría y dejan libre la relación para corregir el registro.
        builder.HasIndex(d => new { d.TrabajadorId, d.FechaFeriadoOrigen })
            .IsUnique()
            .HasFilter("activo = 1")
            .HasDatabaseName("UQ_descanso_sustitutorio_feriado_activo");

        // Un mismo día físico no puede compensar dos feriados del mismo trabajador.
        builder.HasIndex(d => new { d.TrabajadorId, d.FechaDescanso })
            .IsUnique()
            .HasFilter("activo = 1")
            .HasDatabaseName("UQ_descanso_sustitutorio_fecha_activa");

        builder.HasIndex(d => d.FechaDescanso);
    }
}

/// <summary>
/// Valores legales absolutos con vigencia por fecha: RMV, asignación familiar, movilidad.
/// </summary>
/// <remarks>
/// Vive junto a <see cref="TasaSobretasaConfiguracion"/> porque son la misma idea aplicada
/// a dos formas distintas del mismo problema —lo que la ley fija hoy y puede cambiar
/// mañana— y quien vaya a tocar una casi siempre necesita ver la otra.
/// </remarks>
public class ParametroLegalConfiguracion : IEntityTypeConfiguration<ParametroLegal>
{
    public void Configure(EntityTypeBuilder<ParametroLegal> builder)
    {
        builder.ToTable("ParametroLegal");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Tipo).HasConversion<string>().HasMaxLength(40).IsRequired();

        // DECIMAL(12,4), nunca FLOAT: aquí hay soles, y 1130.0000 tiene que ser
        // exactamente 1130.0000 cuando planilla cuadre el bono nocturno a mano.
        builder.Property(p => p.Valor).HasPrecision(12, 4).IsRequired();

        builder.Property(p => p.BaseLegal).HasMaxLength(200);

        // Misma consulta que en las tasas: "qué RMV regía el 15/07/2026".
        builder.HasIndex(p => new { p.Tipo, p.VigenteDesde, p.VigenteHasta });
    }
}

/// <summary>Reintegros y descuentos manuales del bloque AO-BF del tareo final.</summary>
public class ReintegroTareoConfiguracion : IEntityTypeConfiguration<ReintegroTareo>
{
    public void Configure(EntityTypeBuilder<ReintegroTareo> builder)
    {
        builder.ToTable("ReintegroTareo");
        builder.HasKey(r => r.Id);

        // Un solo registro de reintegros por trabajador y periodo: la pantalla de captura
        // edita ese único registro, nunca inserta uno nuevo al lado.
        builder.HasIndex(r => new { r.TrabajadorId, r.PeriodoId })
            .IsUnique()
            .HasDatabaseName("UQ_reintegro_tareo_trabajador_periodo");
    }
}
