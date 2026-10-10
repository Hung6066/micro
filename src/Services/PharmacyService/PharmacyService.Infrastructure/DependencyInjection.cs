using His.Hope.Infrastructure.Outbox;
using His.Hope.Infrastructure.DataLifecycle;
using His.Hope.Infrastructure.Events;
using His.Hope.IntegrationEvents.Pharmacy;
using His.Hope.PharmacyService.Domain.Events;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Infrastructure.Persistence;
using His.Hope.PharmacyService.Infrastructure.Persistence.Repositories;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.Persistence;
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
        services.AddHttpContextAccessor();
        services.AddSingleton<SoftDeleteInterceptor>();
        services.AddDbContext<PharmacyDbContext>((serviceProvider, options) =>
            options.UseHisHopeNpgsql(
                serviceProvider,
                configuration,
                "PharmacyDb",
                b =>
                {
                    b.MigrationsAssembly(typeof(PharmacyDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                })
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new OutboxDomainEventInterceptor(), serviceProvider.GetRequiredService<SoftDeleteInterceptor>()));

        services.AddScoped<IMedicationRepository, MedicationRepository>();
        services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
        services.AddScoped<DomainEventDispatcher>();
        services.AddOutbox<PharmacyDbContext>();
        services.AddIntegrationEventMapping<PrescriptionCreatedDomainEvent, PrescriptionCreatedIntegrationEvent>(
            domainEvent => new PrescriptionCreatedIntegrationEvent(
                domainEvent.PrescriptionId, domainEvent.PatientId, domainEvent.ProviderId,
                domainEvent.MedicationName, domainEvent.Strength, domainEvent.DosageForm,
                domainEvent.DosageInstructions, domainEvent.Quantity, domainEvent.Refills,
                domainEvent.PrescribedDate));

        return services;
    }
}
