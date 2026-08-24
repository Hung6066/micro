using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace His.Hope.CommerceService.Infrastructure.Persistence;

public sealed class CommerceDbContextFactory : IDesignTimeDbContextFactory<CommerceDbContext>
{
    public CommerceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=commercedb;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsAssembly(typeof(CommerceDbContext).Assembly.GetName().Name))
            .Options;
        return new CommerceDbContext(options);
    }
}
