using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace His.Hope.Infrastructure.Outbox;

public static class OutboxServiceExtensions
{
    public static IServiceCollection AddOutbox<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddHostedService<OutboxProcessor<TDbContext>>();
        return services;
    }
}
