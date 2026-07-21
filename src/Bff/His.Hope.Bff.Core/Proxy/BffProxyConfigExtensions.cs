using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.Bff.Core.Proxy;

public static class BffProxyConfigExtensions
{
    public static IServiceCollection AddBffProxy(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms<JwtTransformProvider>();

        return services;
    }

    public static WebApplication MapBffReverseProxy(this WebApplication app)
    {
        app.MapReverseProxy();
        return app;
    }
}
