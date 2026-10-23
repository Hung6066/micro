using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Domain.Events;
using His.Hope.ClinicalService.Infrastructure.Persistence;
using His.Hope.ClinicalService.Infrastructure.Persistence.Repositories;
using His.Hope.Infrastructure.Outbox;
using His.Hope.Infrastructure.DataLifecycle;
using His.Hope.Infrastructure.Events;
using His.Hope.IntegrationEvents.Clinical;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.Persistence;
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
        services.AddHttpContextAccessor();
        services.AddSingleton<SoftDeleteInterceptor>();
        services.AddDbContext<ClinicalDbContext>((serviceProvider, options) =>
            options.UseHisHopeNpgsql(
                serviceProvider,
                configuration,
                "ClinicalDb",
                b =>
                {
                    b.MigrationsAssembly(typeof(ClinicalDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                })
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new OutboxDomainEventInterceptor(), serviceProvider.GetRequiredService<SoftDeleteInterceptor>()));

        services.AddScoped<IEncounterRepository, EncounterRepository>();
        services.AddScoped<DomainEventDispatcher>();
        services.AddOutbox<ClinicalDbContext>();
        services.AddIntegrationEventMapping<EncounterStartedDomainEvent, EncounterStartedIntegrationEvent>(
            domainEvent => new EncounterStartedIntegrationEvent(
                domainEvent.EncounterId, domainEvent.PatientId, domainEvent.ProviderId,
                domainEvent.AppointmentId, domainEvent.EncounterType.Code, domainEvent.OccurredOn));

        return services;
    }
}
