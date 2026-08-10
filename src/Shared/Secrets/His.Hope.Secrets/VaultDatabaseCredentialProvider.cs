using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data.Common;

namespace His.Hope.Secrets;

public sealed class VaultDatabaseOptions
{
    public const string SectionName = "Vault:Database";
    public bool Enabled { get; set; }
    public string Role { get; set; } = string.Empty;
    public int LeaseRenewalThresholdSeconds { get; set; } = 60;
    public int MinimumLeaseSeconds { get; set; } = 900;
}

public interface IVaultDatabaseConnectionStringResolver
{
    string Resolve(string configuredConnectionString, string connectionName);
}

public sealed class VaultDatabaseConnectionStringResolver(
    IHttpClientFactory factory,
    IOptionsMonitor<VaultOptions> vaultOptions,
    IOptionsMonitor<VaultDatabaseOptions> databaseOptions,
    IVaultTokenProvider tokenProvider,
    IHostEnvironment environment,
    ILogger<VaultDatabaseConnectionStringResolver> logger) : IVaultDatabaseConnectionStringResolver
{
    private readonly object sync = new();
    private readonly Dictionary<string, CachedCredential> cache = new(StringComparer.OrdinalIgnoreCase);

    public string Resolve(string configuredConnectionString, string connectionName)
    {
        var settings = databaseOptions.CurrentValue;
        if (!settings.Enabled && !environment.IsProduction())
            return configuredConnectionString;

        var role = string.IsNullOrWhiteSpace(settings.Role)
            ? DefaultRoleFor(connectionName)
            : settings.Role.Trim();
        if (string.IsNullOrWhiteSpace(role))
        {
            if (environment.IsProduction())
                throw new InvalidOperationException($"Vault database role is required in Production for '{connectionName}'.");
            return configuredConnectionString;
        }

        CachedCredential? cached;
        lock (sync)
            cache.TryGetValue(role, out cached);

        if (cached is not null && cached.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(settings.LeaseRenewalThresholdSeconds))
            return ApplyCredential(configuredConnectionString, cached.Username, cached.Password);

        var credential = FetchCredentialAsync(role).GetAwaiter().GetResult();
        lock (sync)
            cache[role] = credential;
        logger.LogDebug("Resolved leased database credential for connection {ConnectionName} using Vault role {Role}; lease expires at {ExpiresAt}", connectionName, role, credential.ExpiresAt);
        return ApplyCredential(configuredConnectionString, credential.Username, credential.Password);
    }

    private async Task<CachedCredential> FetchCredentialAsync(string role)
    {
        var vault = vaultOptions.CurrentValue;
        var token = await tokenProvider.GetTokenAsync();
        var client = factory.CreateClient("vault");
        client.BaseAddress = new Uri(vault.Address.TrimEnd('/'));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/database/creds/{Uri.EscapeDataString(role)}");
        request.Headers.Add("X-Vault-Token", token);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");
        var username = data.GetProperty("username").GetString();
        var password = data.GetProperty("password").GetString();
        var ttl = document.RootElement.TryGetProperty("lease_duration", out var lease)
            ? lease.GetInt32()
            : 300;
        if (environment.IsProduction() && ttl < databaseOptions.CurrentValue.MinimumLeaseSeconds)
            throw new InvalidOperationException($"Vault database role '{role}' returned a lease of {ttl}s, below the configured production minimum of {databaseOptions.CurrentValue.MinimumLeaseSeconds}s.");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException($"Vault database role '{role}' returned incomplete credentials.");
        return new CachedCredential(username, password, DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, ttl)));
    }

    private static string ApplyCredential(string configured, string username, string password)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = configured };
        builder["Username"] = username;
        builder["Password"] = password;
        return builder.ConnectionString;
    }

    private static string DefaultRoleFor(string connectionName) => connectionName switch
    {
        "IdentityDb" => "identity-service-db",
        "PatientDb" => "patient-service-db",
        "ClinicalDb" => "clinical-service-db",
        "AppointmentDb" => "appointment-service-db",
        "LabDb" => "lab-service-db",
        "BillingDb" => "billing-service-db",
        "PharmacyDb" => "pharmacy-service-db",
        _ => connectionName.ToLowerInvariant()
    };

    private sealed record CachedCredential(string Username, string Password, DateTimeOffset ExpiresAt);
}
