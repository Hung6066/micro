using His.Hope.Infrastructure.Outbox;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.PatientService.Infrastructure.Persistence;
using His.Hope.PatientService.Infrastructure.Persistence.Repositories;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.PatientService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPatientInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<PatientDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("PatientDb"),
                b =>
                {
                    b.MigrationsAssembly(typeof(PatientDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                })
            .AddInterceptors(new OutboxDomainEventInterceptor()));

        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<DomainEventDispatcher>();
        services.AddOutbox<PatientDbContext>();

        return services;
    }
}
