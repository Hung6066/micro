using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace His.Hope.Configuration;

public static class RuntimeConfigurationExtensions
{
    private static readonly Regex ServiceUrlPattern =
        new("^SERVICE_(?<name>[A-Z0-9_]+)_URL$", RegexOptions.Compiled);

    private static readonly Regex DatabaseUrlPattern =
        new("^DATABASE_(?<name>[A-Z0-9_]+)_URL$", RegexOptions.Compiled);

    public static IServiceCollection AddHisHopeRuntimeConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var endpoints = BindServiceEndpoints(configuration, serviceName);
        services.AddSingleton(endpoints);
        services.AddSingleton<IOptions<ServiceEndpointOptions>>(_ => Options.Create(endpoints));
        return services;
    }

    public static ServiceEndpointOptions BindServiceEndpoints(
        IConfiguration configuration,
        string serviceName)
    {
        var endpoints = new ServiceEndpointOptions();

        foreach (var (key, value) in configuration.AsEnumerable())
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var serviceMatch = ServiceUrlPattern.Match(key);
            if (serviceMatch.Success)
            {
                BindEndpoint(
                    endpoints,
                    configuration,
                    ServiceEndpointOptions.NormalizeLogicalName(serviceMatch.Groups["name"].Value),
                    key,
                    value);
                continue;
            }

            var databaseMatch = DatabaseUrlPattern.Match(key);
            if (databaseMatch.Success)
            {
                BindEndpoint(
                    endpoints,
                    configuration,
                    $"database-{ServiceEndpointOptions.NormalizeLogicalName(databaseMatch.Groups["name"].Value)}",
                    key,
                    value);
            }
        }

        BindEndpoint(endpoints, configuration, "redis", "REDIS_URL", configuration["REDIS_URL"], requiredByDefault: true);
        BindEndpoint(endpoints, configuration, "rabbitmq", "RABBITMQ_URL", configuration["RABBITMQ_URL"], requiredByDefault: false);

        var errors = Validate(endpoints, configuration, serviceName);
        if (errors.Count > 0)
        {
            throw new OptionsValidationException(
                nameof(ServiceEndpointOptions),
                typeof(ServiceEndpointOptions),
                errors);
        }

        return endpoints;
    }

    public static string ToRedisConnectionString(Uri redisUrl)
    {
        var parts = new List<string>();
        parts.Add(redisUrl.IsDefaultPort ? redisUrl.Host : $"{redisUrl.Host}:{redisUrl.Port}");

        if (!string.IsNullOrWhiteSpace(redisUrl.UserInfo))
        {
            // REDIS_URL is a URI, so passwords containing '+', '/', or '=' are
            // percent-encoded in UserInfo. Decode only after URI parsing and
            // before handing the value to StackExchange.Redis.
            var credentials = Uri.UnescapeDataString(redisUrl.UserInfo).Split(':', 2);
            if (credentials.Length == 2 && !string.IsNullOrWhiteSpace(credentials[1]))
            {
                parts.Add($"password={credentials[1]}");
            }
        }

        var database = redisUrl.AbsolutePath.Trim('/');
        if (int.TryParse(database, out var databaseIndex))
        {
            parts.Add($"defaultDatabase={databaseIndex}");
        }

        if (redisUrl.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("ssl=True");
        }

        return string.Join(',', parts);
    }

    private static void BindEndpoint(
        ServiceEndpointOptions endpoints,
        IConfiguration configuration,
        string logicalName,
        string urlKey,
        string? rawValue,
        bool requiredByDefault = true)
    {
        var required = configuration.GetValue<bool?>($"{urlKey[..^4]}_REQUIRED") ?? requiredByDefault;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            endpoints.Set(logicalName, null, required, urlKey);
            return;
        }

        if (!Uri.TryCreate(rawValue, UriKind.Absolute, out var endpointUri))
        {
            endpoints.Set($"{logicalName}-invalid", null, true, $"{urlKey}={rawValue}");
            return;
        }

        // Redis URLs may carry a percent-encoded password in UserInfo. UriBuilder
        // can reject that form while normalizing the path, so preserve the URI
        // exactly and let ToRedisConnectionString decode the credentials later.
        var normalized = urlKey.Equals("REDIS_URL", StringComparison.OrdinalIgnoreCase)
            ? endpointUri
            : Normalize(endpointUri);
        endpoints.Set(logicalName, normalized, required, urlKey);
    }

    private static List<string> Validate(
        ServiceEndpointOptions endpoints,
        IConfiguration configuration,
        string serviceName)
    {
        var errors = new List<string>();
        var environmentName = configuration["HIS_HOPE_ENVIRONMENT"]
            ?? configuration["ASPNETCORE_ENVIRONMENT"]
            ?? "development";
        var isProduction = environmentName.Equals("production", StringComparison.OrdinalIgnoreCase);

        foreach (var endpoint in endpoints.Endpoints.Values)
        {
            if (endpoint.LogicalName.EndsWith("-invalid", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"{serviceName} runtime configuration contains a malformed absolute URI at {endpoint.SourceKey}.");
                continue;
            }

            if (endpoint.Required && endpoint.Uri is null)
            {
                errors.Add(
                    $"{serviceName} runtime configuration is missing required endpoint {endpoint.SourceKey}.");
                continue;
            }

            if (endpoint.Uri is null)
            {
                continue;
            }

            if (isProduction && IsLoopback(endpoint.Uri))
            {
                errors.Add(
                    $"{serviceName} runtime endpoint {endpoint.SourceKey} cannot use localhost in production.");
            }
        }

        return errors;
    }

    private static Uri Normalize(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Path = string.IsNullOrWhiteSpace(uri.AbsolutePath) || uri.AbsolutePath == "/"
                ? "/"
                : $"{uri.AbsolutePath.TrimEnd('/')}/"
        };
        return builder.Uri;
    }

    private static bool IsLoopback(Uri uri) =>
        uri.IsLoopback
        || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase);
}
