using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace His.Hope.LabService.Infrastructure.Persistence;

public sealed class LabDbContextFactory : IDesignTimeDbContextFactory<LabDbContext>
{
    public LabDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HISHOPE_LAB_DB")
            ?? "Host=localhost;Database=his_hope_lab;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<LabDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(LabDbContext).Assembly.FullName))
            .Options;
        return new LabDbContext(options);
    }
}
