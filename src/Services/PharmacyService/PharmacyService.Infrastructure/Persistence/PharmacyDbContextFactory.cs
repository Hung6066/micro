using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace His.Hope.PharmacyService.Infrastructure.Persistence;

public sealed class PharmacyDbContextFactory : IDesignTimeDbContextFactory<PharmacyDbContext>
{
    public PharmacyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HISHOPE_PHARMACY_DB")
            ?? "Host=localhost;Database=his_hope_pharmacy;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(PharmacyDbContext).Assembly.FullName))
            .Options;
        return new PharmacyDbContext(options);
    }
}
