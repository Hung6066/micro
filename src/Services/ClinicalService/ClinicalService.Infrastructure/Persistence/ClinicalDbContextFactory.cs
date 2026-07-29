using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace His.Hope.ClinicalService.Infrastructure.Persistence;

public sealed class ClinicalDbContextFactory : IDesignTimeDbContextFactory<ClinicalDbContext>
{
    public ClinicalDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HISHOPE_CLINICAL_DB")
            ?? "Host=localhost;Database=his_hope_clinical;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<ClinicalDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(ClinicalDbContext).Assembly.FullName))
            .Options;
        return new ClinicalDbContext(options);
    }
}
