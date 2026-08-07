using System.Text;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Auditoria;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Normativa;
using ControlMarcacion.Domain.Organizacion;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Domain.Solicitudes;
using ControlMarcacion.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia;

/// <summary>
/// Contexto de acceso a datos del sistema.
/// </summary>
/// <remarks>
/// Extiende <see cref="IdentityDbContext{TUser}"/> en vez de <see cref="DbContext"/> plano:
/// las tablas de usuarios, roles y sus relaciones entran al mismo modelo, y la conversión
/// a snake_case de <see cref="AplicarNomenclaturaSnakeCase"/> las alcanza igual que a
/// cualquier otra entidad, sin configuración aparte.
/// </remarks>
public class MarcacionDbContext(DbContextOptions<MarcacionDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    // Organización
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Planta> Plantas => Set<Planta>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Seccion> Secciones => Set<Seccion>();
    public DbSet<Centro> Centros => Set<Centro>();
    public DbSet<CentroCosto> CentrosCosto => Set<CentroCosto>();

    // Personal
    public DbSet<Trabajador> Trabajadores => Set<Trabajador>();
    public DbSet<AsignacionMarcador> AsignacionesMarcador => Set<AsignacionMarcador>();
    public DbSet<AsignacionTurno> AsignacionesTurno => Set<AsignacionTurno>();
    public DbSet<AsignacionSueldo> AsignacionesSueldo => Set<AsignacionSueldo>();
    public DbSet<PeriodoLaboral> PeriodosLaborales => Set<PeriodoLaboral>();

    // Catálogos
    public DbSet<TipoDocumento> TiposDocumento => Set<TipoDocumento>();
    public DbSet<Puesto> Puestos => Set<Puesto>();
    public DbSet<Marcador> Marcadores => Set<Marcador>();
    public DbSet<Feriado> Feriados => Set<Feriado>();
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<TurnoPlanta> TurnosPlanta => Set<TurnoPlanta>();
    public DbSet<Periodo> Periodos => Set<Periodo>();
    public DbSet<TipoAusencia> TiposAusencia => Set<TipoAusencia>();

    // Marcaciones
    public DbSet<MarcacionCruda> MarcacionesCrudas => Set<MarcacionCruda>();
    public DbSet<ImportacionMarcador> Importaciones => Set<ImportacionMarcador>();
    public DbSet<ErrorImportacion> ErroresImportacion => Set<ErrorImportacion>();

    // Solicitudes y justificaciones
    public DbSet<SolicitudAusencia> SolicitudesAusencia => Set<SolicitudAusencia>();
    public DbSet<Justificacion> Justificaciones => Set<Justificacion>();

    // Asistencia y normativa
    public DbSet<TareoDiario> TareosDiarios => Set<TareoDiario>();
    public DbSet<TasaSobretasa> TasasSobretasa => Set<TasaSobretasa>();
    public DbSet<ParametroLegal> ParametrosLegales => Set<ParametroLegal>();
    public DbSet<ReintegroTareo> ReintegrosTareo => Set<ReintegroTareo>();

    // Seguridad. Los usuarios y roles los aporta IdentityDbContext; esta es la única
    // tabla propia del módulo.
    public DbSet<UsuarioPlanta> UsuariosPlanta => Set<UsuarioPlanta>();

    // Auditoría. La escribe InterceptorAuditoria al guardar; nadie más la toca.
    public DbSet<RegistroAuditoria> RegistrosAuditoria => Set<RegistroAuditoria>();

    /// <summary>
    /// Convenciones por defecto de tipos.
    /// </summary>
    /// <remarks>
    /// Sin un largo por defecto, EF genera NVARCHAR(MAX) para toda cadena, que no se puede
    /// indexar y desperdicia espacio. Y sin precisión explícita, los decimales quedan en
    /// (18,2), insuficiente para tasas como 25.0000.
    /// </remarks>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<string>().HaveMaxLength(200);
        configurationBuilder.Properties<decimal>().HavePrecision(12, 4);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Registra primero las tablas propias de Identity (usuarios, roles y sus
        // relaciones): sin esta llamada no existirían en el modelo.
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(MarcacionDbContext).Assembly);

        DatosSemilla.Aplicar(builder);

        // Se aplica al final para que alcance también a lo que definieron las
        // configuraciones y a las tablas de Identity recién registradas.
        AplicarNomenclaturaSnakeCase(builder);
    }

    /// <summary>
    /// Completa los campos de auditoría en cada guardado.
    /// </summary>
    /// <remarks>
    /// El usuario se resuelve desde el contexto de la petición en la capa Web; aquí solo
    /// se manejan las marcas de tiempo para no dejar la auditoría a criterio de cada servicio.
    /// </remarks>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var ahora = DateTime.Now;

        foreach (var entrada in ChangeTracker.Entries<EntidadBase>())
        {
            switch (entrada.State)
            {
                case EntityState.Added:
                    entrada.Entity.CreadoEn = ahora;
                    break;

                case EntityState.Modified:
                    entrada.Entity.ModificadoEn = ahora;
                    // La fecha de creación no se reescribe nunca.
                    entrada.Property(e => e.CreadoEn).IsModified = false;
                    entrada.Property(e => e.CreadoPor).IsModified = false;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Convierte tablas, columnas, claves e índices a snake_case.
    /// </summary>
    /// <remarks>
    /// Se hace de forma global en vez de nombrar cada columna a mano en las configuraciones:
    /// son más de veinte entidades y el nombrado manual se desincroniza a la primera
    /// propiedad que alguien agregue sin acordarse.
    /// </remarks>
    private static void AplicarNomenclaturaSnakeCase(ModelBuilder modelBuilder)
    {
        foreach (var entidad in modelBuilder.Model.GetEntityTypes())
        {
            var tabla = entidad.GetTableName();
            if (tabla is not null)
            {
                entidad.SetTableName(ASnakeCase(tabla));
            }

            foreach (var propiedad in entidad.GetProperties())
            {
                propiedad.SetColumnName(ASnakeCase(propiedad.Name));
            }

            foreach (var clave in entidad.GetKeys())
            {
                var nombre = clave.GetName();
                if (nombre is not null)
                {
                    clave.SetName(ASnakeCase(nombre));
                }
            }

            foreach (var foranea in entidad.GetForeignKeys())
            {
                var nombre = foranea.GetConstraintName();
                if (nombre is not null)
                {
                    foranea.SetConstraintName(ASnakeCase(nombre));
                }
            }

            foreach (var indice in entidad.GetIndexes())
            {
                indice.SetDatabaseName(ASnakeCase(indice.GetDatabaseName()!));
            }
        }
    }

    private static string ASnakeCase(string nombre)
    {
        if (string.IsNullOrEmpty(nombre))
        {
            return nombre;
        }

        var resultado = new StringBuilder(nombre.Length + 8);
        resultado.Append(char.ToLowerInvariant(nombre[0]));

        for (var i = 1; i < nombre.Length; i++)
        {
            var caracter = nombre[i];

            if (!char.IsUpper(caracter))
            {
                resultado.Append(caracter);
                continue;
            }

            var anterior = nombre[i - 1];
            var siguienteEsMinuscula = i + 1 < nombre.Length && char.IsLower(nombre[i + 1]);

            // Se separa cuando venimos de minúscula o dígito ("turnoId" → "turno_id"),
            // o cuando termina un acrónimo y arranca una palabra ("IDMarcador" → "id_marcador").
            // No se separa dentro del acrónimo, o los prefijos que genera EF quedarían
            // como "i_x_turno_planta" en lugar de "ix_turno_planta".
            var necesitaSeparador =
                anterior != '_' && (!char.IsUpper(anterior) || siguienteEsMinuscula);

            if (necesitaSeparador)
            {
                resultado.Append('_');
            }

            resultado.Append(char.ToLowerInvariant(caracter));
        }

        return resultado.ToString();
    }
}
