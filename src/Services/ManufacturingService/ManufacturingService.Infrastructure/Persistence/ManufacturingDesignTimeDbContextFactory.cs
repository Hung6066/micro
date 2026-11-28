using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace His.Hope.ManufacturingService.Infrastructure.Persistence;

public sealed class ManufacturingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ManufacturingDbContext>
{
    public ManufacturingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MANUFACTURING_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=manufacturing_design;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<ManufacturingDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(ManufacturingDbContext).Assembly.GetName().Name))
            .Options;

        return new ManufacturingDbContext(options);
    }
}
