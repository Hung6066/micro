using His.Hope.Infrastructure.Outbox;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Infrastructure.Persistence;
using His.Hope.PharmacyService.Infrastructure.Persistence.Repositories;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.PharmacyService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPharmacyInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<PharmacyDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("PharmacyDb"),
                b =>
                {
                    b.MigrationsAssembly(typeof(PharmacyDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                })
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new OutboxDomainEventInterceptor()));

        services.AddScoped<IMedicationRepository, MedicationRepository>();
        services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
        services.AddScoped<DomainEventDispatcher>();
        services.AddOutbox<PharmacyDbContext>();

        return services;
    }
}
