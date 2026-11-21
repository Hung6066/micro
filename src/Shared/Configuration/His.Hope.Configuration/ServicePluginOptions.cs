using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.Configuration;

public sealed class ServicePluginOptions
{
    public const string SectionName = HisHopeConfigurationKeys.Plugins;

    public List<ServicePluginDefinition> Items { get; init; } = [];
}

public sealed class ServicePluginDefinition
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public bool Enabled { get; init; } = true;
    public string? Endpoint { get; init; }
    public string? RoutePrefix { get; init; }
    public string? DashboardRoute { get; init; }
    public string? Icon { get; init; }
    public string[] Permissions { get; init; } = [];
}

public interface IServicePluginRegistry
{
    IReadOnlyList<ServicePluginDefinition> All { get; }
    IReadOnlyList<ServicePluginDefinition> Enabled { get; }
    ServicePluginDefinition? Get(string key);
    bool IsEnabled(string key);
}

public sealed class ServicePluginRegistry : IServicePluginRegistry
{
    private readonly IReadOnlyDictionary<string, ServicePluginDefinition> _plugins;

    public ServicePluginRegistry(IConfiguration configuration)
    {
        var options = configuration.GetSection(ServicePluginOptions.SectionName)
            .Get<ServicePluginOptions>() ?? new ServicePluginOptions();

        _plugins = options.Items
            .Where(plugin => !string.IsNullOrWhiteSpace(plugin.Key))
            .GroupBy(plugin => plugin.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToDictionary(
                plugin => plugin.Key.Trim(),
                plugin => new ServicePluginDefinition
                {
                    Key = plugin.Key.Trim(),
                    DisplayName = plugin.DisplayName?.Trim() ?? string.Empty,
                    Enabled = plugin.Enabled,
                    Endpoint = NormalizePathOrUri(plugin.Endpoint),
                    RoutePrefix = NormalizePath(plugin.RoutePrefix),
                    DashboardRoute = NormalizePath(plugin.DashboardRoute),
                    Permissions = plugin.Permissions
                        .Where(permission => !string.IsNullOrWhiteSpace(permission))
                        .Select(permission => permission.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                },
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ServicePluginDefinition> All => _plugins.Values.ToArray();

    public IReadOnlyList<ServicePluginDefinition> Enabled =>
        _plugins.Values.Where(plugin => plugin.Enabled).ToArray();

    public ServicePluginDefinition? Get(string key) =>
        !string.IsNullOrWhiteSpace(key) && _plugins.TryGetValue(key.Trim(), out var plugin)
            ? plugin
            : null;

    public bool IsEnabled(string key) => Get(key)?.Enabled == true;

    private static string? NormalizePath(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : $"/{value.Trim().Trim('/')}";

    private static string? NormalizePathOrUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out _) ? trimmed : NormalizePath(trimmed);
    }
}

public static class ServicePluginRegistration
{
    public static IServiceCollection AddHisHopeServicePlugins(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IServicePluginRegistry>(
            new ServicePluginRegistry(configuration));
        return services;
    }

}
