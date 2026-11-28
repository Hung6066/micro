using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace His.Hope.AspNetCore.Tenancy;

public static class TenantPlacementServiceCollectionExtensions
{
    public static IServiceCollection AddHisHopeTenantPlacement(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TenantPlacementOptions>(options =>
        {
            configuration.GetSection(TenantPlacementOptions.SectionName).Bind(options);
            options.ConfigPath ??= "config/conglomerate/tenant-placement.v1.json";
        });
        services.AddSingleton<ITenantPlacementRegistry, TenantPlacementRegistry>();
        services.AddSingleton<TenantPlacementConnectionResolver>();
        return services;
    }

    public static void ValidateHisHopeTenantPlacement(this IHost app)
    {
        using var scope = app.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ITenantPlacementRegistry>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("HisHope.TenantPlacement");
        TenantPlacementStartupValidation.Validate(registry, configuration, environment, logger);
    }
}
