using His.Hope.Infrastructure.Outbox;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Infrastructure.Persistence;
using His.Hope.LabService.Infrastructure.Persistence.Repositories;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.LabService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLabInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<LabDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("LabDb"),
                b =>
                {
                    b.MigrationsAssembly(typeof(LabDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                })
            .AddInterceptors(new OutboxDomainEventInterceptor()));

        services.AddScoped<ILabOrderRepository, LabOrderRepository>();
        services.AddScoped<DomainEventDispatcher>();
        services.AddOutbox<LabDbContext>();

        return services;
    }
}
