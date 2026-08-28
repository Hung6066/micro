using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.Events;
using His.Hope.BillingService.Infrastructure.Persistence;
using His.Hope.BillingService.Infrastructure.Persistence.Repositories;
using His.Hope.Infrastructure.Outbox;
using His.Hope.Infrastructure.DataLifecycle;
using His.Hope.Infrastructure.Events;
using His.Hope.IntegrationEvents.Billing;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.BillingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBillingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<SoftDeleteInterceptor>();
        services.AddDbContext<BillingDbContext>((serviceProvider, options) =>
            options.UseHisHopeNpgsql(
                serviceProvider,
                configuration,
                "BillingDb",
                b =>
                {
                    b.MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                })
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new OutboxDomainEventInterceptor(), serviceProvider.GetRequiredService<SoftDeleteInterceptor>()));

        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<DomainEventDispatcher>();
        services.AddOutbox<BillingDbContext>();
        services.AddIntegrationEventMapping<InvoiceCreatedDomainEvent, InvoiceCreatedIntegrationEvent>(
            domainEvent => new InvoiceCreatedIntegrationEvent(domainEvent.InvoiceId, domainEvent.PatientId, domainEvent.InvoiceNumber, domainEvent.TotalAmount));
        services.AddIntegrationEventMapping<InvoicePaidDomainEvent, InvoicePaidIntegrationEvent>(
            domainEvent => new InvoicePaidIntegrationEvent(domainEvent.InvoiceId, domainEvent.PatientId, domainEvent.AmountPaid, domainEvent.TotalAmount));

        return services;
    }
}
