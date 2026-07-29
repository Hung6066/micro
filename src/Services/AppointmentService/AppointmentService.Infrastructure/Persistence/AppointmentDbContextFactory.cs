using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace His.Hope.AppointmentService.Infrastructure.Persistence;

public sealed class AppointmentDbContextFactory : IDesignTimeDbContextFactory<AppointmentDbContext>
{
    public AppointmentDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HISHOPE_APPOINTMENT_DB")
            ?? "Host=localhost;Database=his_hope_appointment;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<AppointmentDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppointmentDbContext).Assembly.FullName))
            .Options;
        return new AppointmentDbContext(options);
    }
}
