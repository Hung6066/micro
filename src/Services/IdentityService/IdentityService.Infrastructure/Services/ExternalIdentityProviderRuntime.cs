using System.Text.Json;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace His.Hope.IdentityService.Infrastructure.Services;

public sealed record SamlRuntimeSettings(
    bool Enabled,
    string Issuer,
    string SingleSignOnDestination,
    string IdpMetadata,
    string AllowedIssuer,
    string EmailClaim,
    string GroupClaim,
    IReadOnlyDictionary<string, string> GroupRoleMapping);

/// <summary>
/// Resolves external identity provider settings at runtime. Database settings
/// are suitable for non-secret provider configuration; credentials and keys
/// remain in environment/Vault configuration.
/// </summary>
public sealed class ExternalIdentityProviderRuntime(
    IConfiguration configuration,
    IdentityDbContext db)
{
    public async Task<LdapConfig> GetLdapAsync(CancellationToken ct = default)
    {
        var options = new LdapConfig();
        configuration.GetSection("Ldap").Bind(options);

        var settings = await ReadAsync("Ldap:", ct);
        options.Enabled = ToBool(settings, "Ldap:Enabled", options.Enabled);
        options.Server = ToString(settings, "Ldap:Server", options.Server);
        options.Port = ToInt(settings, "Ldap:Port", options.Port);
        options.UseSsl = ToBool(settings, "Ldap:UseSsl", options.UseSsl);
        options.RequireStartTls = ToBool(settings, "Ldap:RequireStartTls", options.RequireStartTls);
        options.SearchBase = ToString(settings, "Ldap:SearchBase", options.SearchBase);
        options.SearchFilter = ToString(settings, "Ldap:SearchFilter", options.SearchFilter);
        options.SyncIntervalMinutes = ToInt(settings, "Ldap:SyncIntervalMinutes", options.SyncIntervalMinutes);
        options.UserNameAttribute = ToString(settings, "Ldap:UserNameAttribute", options.UserNameAttribute);
        options.EmailAttribute = ToString(settings, "Ldap:EmailAttribute", options.EmailAttribute);
        options.FirstNameAttribute = ToString(settings, "Ldap:FirstNameAttribute", options.FirstNameAttribute);
        options.LastNameAttribute = ToString(settings, "Ldap:LastNameAttribute", options.LastNameAttribute);
        options.MemberOfAttribute = ToString(settings, "Ldap:MemberOfAttribute", options.MemberOfAttribute);
        if (settings.TryGetValue("Ldap:GroupRoleMapping", out var ldapMappingJson) && !string.IsNullOrWhiteSpace(ldapMappingJson))
        {
            try
            {
                options.GroupRoleMapping = JsonSerializer.Deserialize<Dictionary<string, string>>(ldapMappingJson)
                    ?? options.GroupRoleMapping;
            }
            catch (JsonException)
            {
                // Preserve the last valid/default mapping on malformed input.
            }
        }

        // Bind credentials are intentionally configuration-only. Do not store
        // or echo them through the admin settings table.
        return options;
    }

    public async Task<SamlRuntimeSettings> GetSamlAsync(CancellationToken ct = default)
    {
        var section = configuration.GetSection("Saml2");
        var settings = await ReadAsync("Saml2:", ct);
        var mappings = section.GetSection("GroupRoleMapping").Get<Dictionary<string, string>>()
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (settings.TryGetValue("Saml2:GroupRoleMapping", out var mappingJson) && !string.IsNullOrWhiteSpace(mappingJson))
        {
            try
            {
                mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(mappingJson)
                    ?? mappings;
            }
            catch (JsonException)
            {
                // Keep the last valid configuration; malformed admin input must
                // not disable an otherwise valid SAML provider.
            }
        }

        var issuer = ToString(settings, "Saml2:Issuer", section["Issuer"] ?? string.Empty);
        if (issuer.StartsWith("Issuer:", StringComparison.OrdinalIgnoreCase))
            issuer = issuer["Issuer:".Length..].Trim();

        return new SamlRuntimeSettings(
            ToBool(settings, "Saml2:Enabled", section.GetValue("Enabled", false)),
            issuer,
            ToString(settings, "Saml2:SingleSignOnDestination", section["SingleSignOnDestination"] ?? string.Empty),
            ToString(settings, "Saml2:IdPMetadata", section["IdPMetadata"] ?? string.Empty),
            ToString(settings, "Saml2:AllowedIssuer", section["AllowedIssuer"] ?? string.Empty),
            ToString(settings, "Saml2:EmailClaim", section["EmailClaim"] ?? "email"),
            ToString(settings, "Saml2:GroupClaim", section["GroupClaim"] ?? "groups"),
            mappings);
    }

    private async Task<Dictionary<string, string>> ReadAsync(string prefix, CancellationToken ct)
    {
        return await db.SystemSettings.AsNoTracking()
            .Where(x => x.Key.StartsWith(prefix))
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase, ct);
    }

    private static string ToString(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static int ToInt(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static bool ToBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;
}
