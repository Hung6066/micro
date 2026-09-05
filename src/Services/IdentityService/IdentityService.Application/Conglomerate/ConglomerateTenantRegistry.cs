using System.Text.Json;
using His.Hope.Authorization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace His.Hope.IdentityService.Application.Conglomerate;

public interface IConglomerateTenantRegistry : ICrossTenantTenantMetadata
{
    bool IsEnabled { get; }

    string HqCustomerVisibility { get; }

    bool IsConglomerateClient(string? clientId);

    string? GetClientTenant(string? clientId);

    string GetPortalClass(string? clientId);

    new string GetTenantClass(string tenantKey);

    new string? GetOperatorHome(string tenantKey);

    IReadOnlyList<string> GetClientIdsForTenant(string tenantKey);

    IReadOnlyList<string> GetCustomerTenantsForOperator(string operatorTenantKey);

    bool IsCustomerTenant(string tenantKey);

    IReadOnlyList<CrossTenantAllowedPairOptions> AllowedCrossTenantPairs { get; }

    ConglomerateTenantOptions? GetTenantProfile(string tenantKey);
}

public sealed class ConglomerateTenantRegistry : IConglomerateTenantRegistry, ICrossTenantTenantMetadata
{
    private readonly ConglomerateOptions _options;
    private readonly Dictionary<string, ConglomerateTenantOptions> _tenantProfiles;

    public ConglomerateTenantRegistry(
        IOptions<ConglomerateOptions> options,
        IHostEnvironment environment,
        ILogger<ConglomerateTenantRegistry> logger)
    {
        _options = options.Value;
        _tenantProfiles = _options.Tenants
            .Where(tenant => !string.IsNullOrWhiteSpace(tenant.Key))
            .ToDictionary(tenant => tenant.Key, tenant => tenant, StringComparer.OrdinalIgnoreCase);
        MergeExternalConfiguration(environment, logger);
    }

    public bool IsEnabled => _options.Enabled;

    public string HqCustomerVisibility => _options.HqCustomerVisibility;

    public bool IsConglomerateClient(string? clientId) =>
        !string.IsNullOrWhiteSpace(clientId) && _options.OidcClientTenants.ContainsKey(clientId);

    public string? GetClientTenant(string? clientId) =>
        string.IsNullOrWhiteSpace(clientId) || !_options.OidcClientTenants.TryGetValue(clientId, out var tenant)
            ? null
            : tenant;

    public string GetPortalClass(string? clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return ConglomerateConstants.PortalClassOperator;

        return _options.OidcClientPortalClasses.TryGetValue(clientId, out var portalClass) &&
               !string.IsNullOrWhiteSpace(portalClass)
            ? portalClass
            : ConglomerateConstants.PortalClassOperator;
    }

    public string GetTenantClass(string tenantKey) =>
        _tenantProfiles.TryGetValue(tenantKey, out var profile)
            ? profile.TenantClass
            : ConglomerateConstants.TenantClassInternal;

    public string? GetOperatorHome(string tenantKey) =>
        _tenantProfiles.TryGetValue(tenantKey, out var profile)
            ? profile.OperatorHome
            : null;

    public IReadOnlyList<string> GetClientIdsForTenant(string tenantKey) =>
        string.IsNullOrWhiteSpace(tenantKey)
            ? []
            : _options.OidcClientTenants
                .Where(pair => string.Equals(pair.Value, tenantKey, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .ToArray();

    public IReadOnlyList<string> GetCustomerTenantsForOperator(string operatorTenantKey) =>
        _tenantProfiles.Values
            .Where(profile =>
                string.Equals(profile.TenantClass, ConglomerateConstants.TenantClassCustomer, StringComparison.Ordinal) &&
                string.Equals(profile.OperatorHome, operatorTenantKey, StringComparison.OrdinalIgnoreCase))
            .Select(profile => profile.Key)
            .ToArray();

    public bool IsCustomerTenant(string tenantKey) =>
        string.Equals(GetTenantClass(tenantKey), ConglomerateConstants.TenantClassCustomer, StringComparison.Ordinal);

    public IReadOnlyList<CrossTenantAllowedPairOptions> AllowedCrossTenantPairs =>
        _options.CrossTenantPolicy.AllowedPairs;

    public ConglomerateTenantOptions? GetTenantProfile(string tenantKey) =>
        _tenantProfiles.TryGetValue(tenantKey, out var profile) ? profile : null;

    private void MergeExternalConfiguration(IHostEnvironment environment, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(_options.CustomerTenantsPath))
            MergeCustomerTenantsFile(_options.CustomerTenantsPath, environment, logger);

        if (!string.IsNullOrWhiteSpace(_options.OidcClientsPath))
            MergeOidcClientsFile(_options.OidcClientsPath, environment, logger);

        if (!string.IsNullOrWhiteSpace(_options.IamScopesPath))
            MergeIamScopesFile(_options.IamScopesPath, environment, logger);
    }

    private void MergeCustomerTenantsFile(string relativePath, IHostEnvironment environment, ILogger logger)
    {
        var path = ResolvePath(relativePath, environment);
        if (!File.Exists(path))
        {
            logger.LogWarning("Customer tenants file not found at {Path}.", path);
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.TryGetProperty("customers", out var customers) &&
            customers.ValueKind == JsonValueKind.Array)
        {
            foreach (var customer in customers.EnumerateArray())
            {
                var key = customer.GetProperty("key").GetString();
                var displayName = customer.GetProperty("displayName").GetString();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(displayName))
                    continue;

                var profile = new ConglomerateTenantOptions
                {
                    Key = key,
                    DisplayName = displayName,
                    TenantClass = customer.TryGetProperty("tenantClass", out var tenantClass)
                        ? tenantClass.GetString() ?? ConglomerateConstants.TenantClassCustomer
                        : ConglomerateConstants.TenantClassCustomer,
                    OperatorHome = customer.TryGetProperty("operatorHome", out var operatorHome)
                        ? operatorHome.GetString()
                        : null,
                    AccountKey = customer.TryGetProperty("accountKey", out var accountKey) ? accountKey.GetString() : null,
                    AccountDisplayName = customer.TryGetProperty("accountDisplayName", out var accountName)
                        ? accountName.GetString()
                        : null,
                    EnvironmentKey = customer.TryGetProperty("environmentKey", out var environmentKey)
                        ? environmentKey.GetString()
                        : null,
                    EnvironmentDisplayName = customer.TryGetProperty("environmentDisplayName", out var environmentName)
                        ? environmentName.GetString()
                        : null,
                    ContractId = customer.TryGetProperty("contractId", out var contractId) ? contractId.GetString() : null,
                    DataRegion = customer.TryGetProperty("dataRegion", out var dataRegion) ? dataRegion.GetString() : null
                };

                _tenantProfiles[key] = profile;
                if (!_options.Tenants.Any(tenant => string.Equals(tenant.Key, key, StringComparison.OrdinalIgnoreCase)))
                    _options.Tenants.Add(profile);

                if (!customer.TryGetProperty("portalClients", out var portalClients) ||
                    portalClients.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var portalClient in portalClients.EnumerateArray())
                {
                    var clientId = portalClient.GetProperty("clientId").GetString();
                    if (string.IsNullOrWhiteSpace(clientId))
                        continue;

                    _options.OidcClientTenants[clientId] = key;
                    if (portalClient.TryGetProperty("displayName", out var clientDisplayName) &&
                        !string.IsNullOrWhiteSpace(clientDisplayName.GetString()))
                        _options.OidcClientDisplayNames[clientId] = clientDisplayName.GetString()!;

                    if (portalClient.TryGetProperty("portalClass", out var portalClass) &&
                        !string.IsNullOrWhiteSpace(portalClass.GetString()))
                        _options.OidcClientPortalClasses[clientId] = portalClass.GetString()!;
                }
            }
        }

        if (document.RootElement.TryGetProperty("crossTenantPolicy", out var policy))
            MergeCrossTenantPolicy(policy);
    }

    private void MergeOidcClientsFile(string relativePath, IHostEnvironment environment, ILogger logger)
    {
        var path = ResolvePath(relativePath, environment);
        if (!File.Exists(path))
        {
            logger.LogWarning("Conglomerate OIDC clients file not found at {Path}.", path);
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("clients", out var clients) ||
            clients.ValueKind != JsonValueKind.Array)
            return;

        foreach (var client in clients.EnumerateArray())
        {
            var clientId = client.GetProperty("clientId").GetString();
            var tenantKey = client.GetProperty("tenantKey").GetString();
            var displayName = client.TryGetProperty("displayName", out var name) ? name.GetString() : clientId;
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(tenantKey))
                continue;

            _options.OidcClientTenants[clientId] = tenantKey;
            if (!string.IsNullOrWhiteSpace(displayName))
                _options.OidcClientDisplayNames[clientId] = displayName!;

            if (client.TryGetProperty("portalClass", out var portalClass) &&
                !string.IsNullOrWhiteSpace(portalClass.GetString()))
                _options.OidcClientPortalClasses[clientId] = portalClass.GetString()!;
            else if (!_options.OidcClientPortalClasses.ContainsKey(clientId))
                _options.OidcClientPortalClasses[clientId] = ConglomerateConstants.PortalClassOperator;
        }
    }

    private void MergeIamScopesFile(string relativePath, IHostEnvironment environment, ILogger logger)
    {
        var path = ResolvePath(relativePath, environment);
        if (!File.Exists(path))
        {
            logger.LogWarning("Conglomerate IAM scopes file not found at {Path}.", path);
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("crossTenantPolicy", out var policy) ||
            policy.ValueKind != JsonValueKind.Object)
            return;

        _options.CrossTenantPolicy.AllowedPairs.Clear();
        MergeCrossTenantPolicy(policy);
    }

    private void MergeCrossTenantPolicy(JsonElement policy)
    {
        _options.CrossTenantPolicy.DefaultDeny =
            !policy.TryGetProperty("defaultDeny", out var defaultDeny) || defaultDeny.GetBoolean();

        if (!policy.TryGetProperty("allowedPairs", out var pairs) || pairs.ValueKind != JsonValueKind.Array)
            return;

        foreach (var pair in pairs.EnumerateArray())
        {
            var source = pair.GetProperty("source").GetString();
            if (string.IsNullOrWhiteSpace(source))
                continue;

            var target = pair.TryGetProperty("target", out var targetElement) ? targetElement.GetString() : null;
            var targetClass = pair.TryGetProperty("targetClass", out var targetClassElement)
                ? targetClassElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(target) && string.IsNullOrWhiteSpace(targetClass))
                continue;

            var reason = pair.TryGetProperty("reason", out var reasonElement)
                ? reasonElement.GetString() ?? string.Empty
                : string.Empty;

            var permissions = new List<string>();
            if (pair.TryGetProperty("permissions", out var permissionsElement) &&
                permissionsElement.ValueKind == JsonValueKind.Array)
            {
                permissions.AddRange(permissionsElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()!)
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            }

            _options.CrossTenantPolicy.AllowedPairs.Add(new CrossTenantAllowedPairOptions
            {
                Source = source,
                Target = target,
                TargetClass = targetClass,
                OperatorHomeMatch = pair.TryGetProperty("operatorHomeMatch", out var operatorHomeMatch) &&
                                    operatorHomeMatch.GetBoolean(),
                RequiresJit = pair.TryGetProperty("requiresJit", out var requiresJit) && requiresJit.GetBoolean(),
                MaxDurationMinutes = pair.TryGetProperty("maxDurationMinutes", out var maxDuration) &&
                                     maxDuration.TryGetInt32(out var minutes)
                    ? minutes
                    : 60,
                Reason = reason,
                Permissions = permissions.Count > 0 ? permissions : ["admin.audit.read"]
            });
        }
    }

    private static string ResolvePath(string relativePath, IHostEnvironment environment) =>
        Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, relativePath));
}
