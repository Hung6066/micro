using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Infrastructure.Persistence;
using His.Hope.BillingService.Infrastructure.Persistence.Repositories;
using His.Hope.Infrastructure.Outbox;
using His.Hope.SharedKernel.Domain.Common;
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
        services.AddDbContext<BillingDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("BillingDb"),
                b =>
                {
                    b.MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                })
            .AddInterceptors(new OutboxDomainEventInterceptor()));

        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<DomainEventDispatcher>();
        services.AddOutbox<BillingDbContext>();

        return services;
    }
}
