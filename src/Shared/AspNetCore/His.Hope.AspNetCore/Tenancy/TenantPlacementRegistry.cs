using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace His.Hope.AspNetCore.Tenancy;

public interface ITenantPlacementRegistry
{
    bool IsRoutingEnabled { get; }

    string DefaultTier { get; }

    string GetDefaultConnectionName(string serviceName);

    TenantPlacementEntryOptions? GetPlacement(string tenantKey);

    bool UsesDedicatedDataStore(string serviceName, string tenantKey);

    string ResolveConnectionName(string serviceName, string tenantKey);

    IReadOnlyList<TenantPlacementEntryOptions> GetDedicatedPlacements();

    IReadOnlyList<string> GetServiceConnectionNames(string serviceName);
}

public sealed class TenantPlacementRegistry : ITenantPlacementRegistry
{
    private readonly TenantPlacementOptions _options;
    private readonly Dictionary<string, TenantPlacementEntryOptions> _placements;
    private readonly IConfiguration? _configuration;

    public TenantPlacementRegistry(
        IOptions<TenantPlacementOptions> options,
        IHostEnvironment environment,
        ILogger<TenantPlacementRegistry> logger,
        IConfiguration? configuration = null)
    {
        _configuration = configuration;
        _options = CloneOptions(options.Value);
        MergeFileConfiguration(_options, environment, logger);
        _placements = _options.Placements
            .Where(placement => !string.IsNullOrWhiteSpace(placement.TenantKey))
            .ToDictionary(
                placement => placement.TenantKey.Trim(),
                placement => placement,
                StringComparer.OrdinalIgnoreCase);
    }

    public bool IsRoutingEnabled => _options.Enabled;

    public string DefaultTier => string.IsNullOrWhiteSpace(_options.DefaultTier)
        ? TenantPlacementTier.Shared
        : _options.DefaultTier;

    public string GetDefaultConnectionName(string serviceName)
    {
        if (_options.Services.TryGetValue(serviceName, out var service) &&
            !string.IsNullOrWhiteSpace(service.DefaultConnectionName))
            return service.DefaultConnectionName;

        throw new InvalidOperationException(
            $"Tenant placement config does not define a default connection for service '{serviceName}'.");
    }

    public TenantPlacementEntryOptions? GetPlacement(string tenantKey)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
            return null;

        return _placements.TryGetValue(tenantKey.Trim(), out var placement) ? placement : null;
    }

    public bool UsesDedicatedDataStore(string serviceName, string tenantKey)
    {
        if (!_options.Enabled)
            return false;

        var placement = GetPlacement(tenantKey);
        return placement is { Active: true } &&
               string.Equals(placement.Tier, TenantPlacementTier.Dedicated, StringComparison.OrdinalIgnoreCase) &&
               ResolveDedicatedConnectionName(serviceName, placement) is not null;
    }

    public string ResolveConnectionName(string serviceName, string tenantKey)
    {
        if (UsesDedicatedDataStore(serviceName, tenantKey))
            return ResolveDedicatedConnectionName(serviceName, GetPlacement(tenantKey)!)!;

        return GetDefaultConnectionName(serviceName);
    }

    public IReadOnlyList<TenantPlacementEntryOptions> GetDedicatedPlacements() =>
        _placements.Values
            .Where(placement =>
                placement.Active &&
                string.Equals(placement.Tier, TenantPlacementTier.Dedicated, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public IReadOnlyList<string> GetServiceConnectionNames(string serviceName)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            GetDefaultConnectionName(serviceName)
        };

        if (!_options.Enabled)
            return names.ToArray();

        foreach (var placement in GetDedicatedPlacements())
        {
            if (ResolveDedicatedConnectionName(serviceName, placement) is { } dedicatedName)
            {
                // A dedicated binding is operational only when its secret is
                // present. Production startup validation still fails fast for
                // a missing required secret; non-production environments can
                // safely continue on the shared connection.
                if (_configuration is null ||
                    !string.IsNullOrWhiteSpace(_configuration.GetConnectionString(dedicatedName)))
                    names.Add(dedicatedName);
            }
        }

        return names.ToArray();
    }

    private static string? ResolveDedicatedConnectionName(
        string serviceName,
        TenantPlacementEntryOptions placement)
    {
        if (placement.Services.TryGetValue(serviceName, out var binding) &&
            !string.IsNullOrWhiteSpace(binding.ConnectionName))
            return binding.ConnectionName.Trim();

        return null;
    }

    private static TenantPlacementOptions CloneOptions(TenantPlacementOptions source) =>
        new()
        {
            Enabled = source.Enabled,
            ConfigPath = source.ConfigPath,
            Version = source.Version,
            DefaultTier = source.DefaultTier,
            Services = source.Services.ToDictionary(
                pair => pair.Key,
                pair => new TenantPlacementServiceOptions
                {
                    DefaultConnectionName = pair.Value.DefaultConnectionName
                },
                StringComparer.OrdinalIgnoreCase),
            Placements = source.Placements
                .Select(placement => new TenantPlacementEntryOptions
                {
                    TenantKey = placement.TenantKey,
                    Tier = placement.Tier,
                    DataRegion = placement.DataRegion,
                    Active = placement.Active,
                    Reason = placement.Reason,
                    Services = placement.Services.ToDictionary(
                        pair => pair.Key,
                        pair => new TenantPlacementServiceBindingOptions
                        {
                            ConnectionName = pair.Value.ConnectionName
                        },
                        StringComparer.OrdinalIgnoreCase)
                })
                .ToList()
        };

    private static void MergeFileConfiguration(
        TenantPlacementOptions options,
        IHostEnvironment environment,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(options.ConfigPath))
            return;

        var path = ResolvePath(options.ConfigPath, environment);
        if (!File.Exists(path))
        {
            logger.LogWarning("Tenant placement file not found at {Path}. Using bound options only.", path);
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        if (root.TryGetProperty("enabled", out var enabled))
            options.Enabled = enabled.GetBoolean();

        if (root.TryGetProperty("defaultTier", out var defaultTier) &&
            !string.IsNullOrWhiteSpace(defaultTier.GetString()))
            options.DefaultTier = defaultTier.GetString()!;

        if (root.TryGetProperty("services", out var services) && services.ValueKind == JsonValueKind.Object)
        {
            foreach (var service in services.EnumerateObject())
            {
                options.Services[service.Name] = new TenantPlacementServiceOptions
                {
                    DefaultConnectionName = service.Value.GetProperty("defaultConnectionName").GetString() ?? string.Empty
                };
            }
        }

        if (!root.TryGetProperty("placements", out var placements) ||
            placements.ValueKind != JsonValueKind.Array)
            return;

        options.Placements = [];
        foreach (var placement in placements.EnumerateArray())
        {
            var tenantKey = placement.GetProperty("tenantKey").GetString();
            if (string.IsNullOrWhiteSpace(tenantKey))
                continue;

            var entry = new TenantPlacementEntryOptions
            {
                TenantKey = tenantKey,
                Tier = placement.TryGetProperty("tier", out var tier)
                    ? tier.GetString() ?? TenantPlacementTier.Shared
                    : TenantPlacementTier.Shared,
                DataRegion = placement.TryGetProperty("dataRegion", out var dataRegion)
                    ? dataRegion.GetString()
                    : null,
                Active = !placement.TryGetProperty("active", out var active) || active.GetBoolean(),
                Reason = placement.TryGetProperty("reason", out var reason) ? reason.GetString() : null
            };

            if (placement.TryGetProperty("services", out var placementServices) &&
                placementServices.ValueKind == JsonValueKind.Object)
            {
                foreach (var service in placementServices.EnumerateObject())
                {
                    entry.Services[service.Name] = new TenantPlacementServiceBindingOptions
                    {
                        ConnectionName = service.Value.GetProperty("connectionName").GetString() ?? string.Empty
                    };
                }
            }

            options.Placements.Add(entry);
        }
    }

    private static string ResolvePath(string relativePath, IHostEnvironment environment)
    {
        if (Path.IsPathRooted(relativePath))
            return relativePath;

        var contentRoot = environment.ContentRootPath;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(contentRoot, relativePath)),
            Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "..", "..", relativePath)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativePath))
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }
}

public sealed class TenantPlacementConnectionResolver(
    ITenantPlacementRegistry registry,
    IConfiguration configuration)
{
    public string ResolveConnectionString(string serviceName, string? tenantKey)
    {
        var connectionName = string.IsNullOrWhiteSpace(tenantKey)
            ? registry.GetDefaultConnectionName(serviceName)
            : registry.ResolveConnectionName(serviceName, tenantKey);

        return ResolveConnectionStringByName(connectionName, serviceName);
    }

    public string ResolveConnectionStringByName(string connectionName, string serviceName)
    {
        var connectionString = configuration.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{connectionName}' is not configured for service '{serviceName}'.");
        }

        return connectionString;
    }
}

public static class TenantPlacementStartupValidation
{
    public static void Validate(
        ITenantPlacementRegistry registry,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        var dedicated = registry.GetDedicatedPlacements();
        if (dedicated.Count == 0)
            return;

        if (!registry.IsRoutingEnabled)
        {
            logger.LogWarning(
                "Tenant placement defines {Count} dedicated tenant(s) but TenantPlacement:Enabled=false. " +
                "All tenants continue to use shared databases until routing is explicitly enabled.",
                dedicated.Count);
            return;
        }

        foreach (var placement in dedicated)
        {
            foreach (var service in placement.Services.Keys)
            {
                var connectionName = registry.ResolveConnectionName(service, placement.TenantKey);
                if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(connectionName)) &&
                    environment.IsProduction())
                {
                    throw new InvalidOperationException(
                        $"Dedicated tenant '{placement.TenantKey}' requires connection '{connectionName}' " +
                        $"for service '{service}', but it is not configured.");
                }
            }
        }
    }
}
