using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ControlMarcacion.Infrastructure.Persistencia;

/// <summary>
/// Crea el contexto exclusivamente para herramientas EF. Evita depender de que la
/// aplicación Web esté detenida para inspeccionar o generar migraciones; no abre ni migra
/// la base canónica.
/// </summary>
public sealed class MarcacionDbContextFactory : IDesignTimeDbContextFactory<MarcacionDbContext>
{
    public MarcacionDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite("Data Source=ControlMarcacion.design-time.db")
            .Options;

        return new MarcacionDbContext(opciones);
    }
}
