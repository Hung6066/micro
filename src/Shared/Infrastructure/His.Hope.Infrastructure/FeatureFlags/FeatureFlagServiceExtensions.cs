using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using His.Hope.Configuration;
using Microsoft.Extensions.Logging;
using Unleash;

namespace His.Hope.Infrastructure.FeatureFlags;

/// <summary>
/// Extension methods for registering <see cref="IFeatureFlagService"/> in the DI container.
/// </summary>
public static class FeatureFlagServiceExtensions
{
    private const string DefaultUnleashUrl = "http://unleash:4242";
    private const string DefaultAppName = "his-hope";

    /// <summary>
    /// Registers the Unleash feature flag service.
    /// Configuration is read from the <c>FeatureManagement:Unleash</c> section:
    /// <list type="bullet">
    ///   <item><c>ApiUrl</c> — Unleash server URL (default: <c>http://unleash:4242</c>)</item>
    ///   <item><c>ApiToken</c> — Client API token for authentication</item>
    ///   <item><c>AppName</c> — Application name reported to Unleash (default: <c>his-hope</c>)</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddFeatureFlags(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var unleashSection = configuration.GetSection(HisHopeConfigurationKeys.FeatureManagementUnleash);
        if (!unleashSection.Exists())
        {
            var environment = configuration[HisHopeConfigurationKeys.RuntimeEnvironment]
                ?? configuration["ASPNETCORE_ENVIRONMENT"]
                ?? configuration["DOTNET_ENVIRONMENT"];
            if (string.Equals(environment, "production", StringComparison.OrdinalIgnoreCase) &&
                configuration.GetValue<bool>("FeatureManagement:Required"))
            {
                throw new InvalidOperationException(
                    "FeatureManagement:Unleash configuration is required when feature management is enabled in production.");
            }

            return services;
        }

        var unleashUrl = unleashSection["ApiUrl"];
        var unleashApiToken = unleashSection["ApiToken"] ?? string.Empty;
        var appName = unleashSection["AppName"];
        var configuredEnvironment = configuration[HisHopeConfigurationKeys.RuntimeEnvironment]
            ?? configuration["ASPNETCORE_ENVIRONMENT"]
            ?? configuration["DOTNET_ENVIRONMENT"];

        if (string.Equals(configuredEnvironment, "production", StringComparison.OrdinalIgnoreCase) &&
            configuration.GetValue<bool>("FeatureManagement:Required") &&
            (string.IsNullOrWhiteSpace(unleashUrl) || string.IsNullOrWhiteSpace(appName)))
        {
            throw new InvalidOperationException(
                "FeatureManagement:Unleash:ApiUrl and AppName are required in production.");
        }

        unleashUrl ??= DefaultUnleashUrl;
        appName ??= DefaultAppName;

        services.AddSingleton<IUnleash>(sp =>
        {
            var settings = new UnleashSettings
            {
                AppName = appName,
                UnleashApi = new Uri(unleashUrl),
            };

            if (!string.IsNullOrEmpty(unleashApiToken))
            {
                settings.CustomHttpHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = unleashApiToken
                };
            }

            return new DefaultUnleash(settings);
        });

        services.AddSingleton<IFeatureFlagService, UnleashFeatureFlagService>();

        return services;
    }
}
