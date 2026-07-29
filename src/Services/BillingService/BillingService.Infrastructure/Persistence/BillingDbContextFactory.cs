using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace His.Hope.BillingService.Infrastructure.Persistence;

public sealed class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HISHOPE_BILLING_DB")
            ?? "Host=localhost;Database=his_hope_billing;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName))
            .Options;
        return new BillingDbContext(options);
    }
}
