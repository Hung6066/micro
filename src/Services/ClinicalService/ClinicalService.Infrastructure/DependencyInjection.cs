using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Infrastructure.Persistence;
using His.Hope.ClinicalService.Infrastructure.Persistence.Repositories;
using His.Hope.Infrastructure.Outbox;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.ClinicalService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddClinicalInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ClinicalDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("ClinicalDb"),
                b =>
                {
                    b.MigrationsAssembly(typeof(ClinicalDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                })
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new OutboxDomainEventInterceptor()));

        services.AddScoped<IEncounterRepository, EncounterRepository>();
        services.AddScoped<DomainEventDispatcher>();
        services.AddOutbox<ClinicalDbContext>();

        return services;
    }
}
