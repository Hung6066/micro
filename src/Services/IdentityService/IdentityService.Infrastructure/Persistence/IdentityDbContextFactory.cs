using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace His.Hope.IdentityService.Infrastructure.Persistence;

public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder
            .UseNpgsql("Host=localhost;Port=5433;Database=identitydb;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention();
        return new IdentityDbContext(optionsBuilder.Options);
    }
}
