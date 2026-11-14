using His.Hope.Infrastructure.Outbox;
using His.Hope.Infrastructure.DataLifecycle;
using His.Hope.Infrastructure.Events;
using His.Hope.IntegrationEvents.Patient;
using His.Hope.PatientService.Domain.Events;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.PatientService.Infrastructure.Persistence;
using His.Hope.PatientService.Infrastructure.Persistence.Repositories;
using His.Hope.PatientService.Infrastructure.Projections;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.Persistence;
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
        services.AddHttpContextAccessor();
        services.AddSingleton<SoftDeleteInterceptor>();
        // Write-side DbContext
        services.AddDbContext<PatientDbContext>((serviceProvider, options) =>
            options.UseHisHopeNpgsql(
                serviceProvider,
                configuration,
                "PatientDb",
                b =>
                {
                    b.MigrationsAssembly(typeof(PatientDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                })
            .AddInterceptors(new OutboxDomainEventInterceptor(), serviceProvider.GetRequiredService<SoftDeleteInterceptor>()));

        // Read-side DbContext (no tracking by default, optimized for queries)
        services.AddDbContext<PatientReadDbContext>((serviceProvider, options) =>
            options.UseHisHopeNpgsql(
                serviceProvider,
                configuration,
                "PatientDb",
                b =>
                {
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                })
            .AddInterceptors(serviceProvider.GetRequiredService<SoftDeleteInterceptor>()));

        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<DomainEventDispatcher>();
        services.AddOutbox<PatientDbContext>();
        services.AddIntegrationEventMapping<PatientRegisteredDomainEvent, PatientRegisteredIntegrationEvent>(
            domainEvent => new PatientRegisteredIntegrationEvent(
                domainEvent.PatientId,
                domainEvent.FullName,
                domainEvent.Phone,
                domainEvent.GenderCode,
                domainEvent.DateOfBirth));
        services.AddIntegrationEventMapping<PatientUpdatedDomainEvent, PatientUpdatedIntegrationEvent>(
            domainEvent => new PatientUpdatedIntegrationEvent(
                domainEvent.PatientId,
                domainEvent.FullName,
                domainEvent.Phone));

        // CQRS read-side projection services
        services.AddScoped<PatientProjector>();

        return services;
    }
}
