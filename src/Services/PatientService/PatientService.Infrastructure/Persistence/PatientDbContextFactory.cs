using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace His.Hope.PatientService.Infrastructure.Persistence;

public sealed class PatientDbContextFactory : IDesignTimeDbContextFactory<PatientDbContext>
{
    public PatientDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HISHOPE_PATIENT_DB")
            ?? "Host=localhost;Database=his_hope_patient;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<PatientDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(PatientDbContext).Assembly.FullName))
            .Options;
        return new PatientDbContext(options);
    }
}
