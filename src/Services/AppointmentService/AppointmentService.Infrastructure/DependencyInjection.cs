using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Infrastructure.Persistence;
using His.Hope.AppointmentService.Infrastructure.Persistence.Repositories;
using His.Hope.Infrastructure.Outbox;
using His.Hope.Infrastructure.Events;
using His.Hope.IntegrationEvents.Appointment;
using His.Hope.AppointmentService.Domain.Events;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.AppointmentService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAppointmentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppointmentDbContext>((serviceProvider, options) =>
            options.UseHisHopeNpgsql(
                serviceProvider,
                configuration,
                "AppointmentDb",
                b =>
                {
                    b.MigrationsAssembly(typeof(AppointmentDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                })
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new OutboxDomainEventInterceptor()));

        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<DomainEventDispatcher>();
        services.AddOutbox<AppointmentDbContext>();
        services.AddIntegrationEventMapping<AppointmentScheduledDomainEvent, AppointmentScheduledIntegrationEvent>(
            domainEvent => new AppointmentScheduledIntegrationEvent(
                domainEvent.AppointmentId, domainEvent.PatientId, domainEvent.ProviderId,
                domainEvent.ScheduledDate, domainEvent.StartTime, domainEvent.EndTime));

        return services;
    }
}
