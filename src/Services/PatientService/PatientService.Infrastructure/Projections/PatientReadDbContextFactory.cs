using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace His.Hope.PatientService.Infrastructure.Projections;

public sealed class PatientReadDbContextFactory : IDesignTimeDbContextFactory<PatientReadDbContext>
{
    public PatientReadDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HISHOPE_PATIENT_DB")
            ?? "Host=localhost;Database=his_hope_patient;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<PatientReadDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(PatientReadDbContext).Assembly.FullName))
            .Options;
        return new PatientReadDbContext(options);
    }
}
