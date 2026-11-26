using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TentacionSana.Infrastructure.Persistence;

// El tooling no arranca Program.cs ni sus migraciones automáticas. No carga secretos.
public sealed class ApplicationDbContextDesignFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("TENTACION_DESIGN_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Configura TENTACION_DESIGN_CONNECTION para el entorno aislado del tooling EF.");
        return new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    }
}
