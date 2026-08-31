using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.CommerceService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCommerceApplication(this IServiceCollection services)
    {
        // Persistence adapters are infrastructure concerns and must be registered
        // by AddCommerceInfrastructure. Deliberate absence here prevents a silent
        // in-memory/no-op fallback from masking a production misconfiguration.
        return services;
    }
}
