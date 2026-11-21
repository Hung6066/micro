using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;

namespace His.Hope.IdentityService.Infrastructure.Services;

public sealed class LdapSyncService(
    ExternalIdentityProviderRuntime runtime,
    UserManager<User> userManager,
    ILogger<LdapSyncService> logger)
{
    public async Task SyncAsync(CancellationToken ct = default)
    {
        var config = await runtime.GetLdapAsync(ct);
        Validate(config);
        if (!config.Enabled)
        {
            logger.LogInformation("LDAP sync is disabled");
            return;
        }

        try
        {
            using var connection = Connect(config);
            var synced = await SearchAndSyncUsers(connection, config, ct);
            if (config.SearchBase.Contains("OU=", StringComparison.OrdinalIgnoreCase))
                await DeactivateMissingUsers(synced, ct);
            logger.LogInformation("LDAP sync complete. Synced {Count} users", synced.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LDAP sync failed");
        }
    }

    public async Task<bool> AuthenticateAsync(string userName, string password, CancellationToken ct = default) =>
        await AuthenticateAndGetProfileAsync(userName, password, ct) is not null;

    public async Task<LdapUserProfile?> AuthenticateAndGetProfileAsync(
        string userName, string password, CancellationToken ct = default)
    {
        var config = await runtime.GetLdapAsync(ct);
        Validate(config);
        if (!config.Enabled || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            return null;

        try
        {
            using var serviceConnection = Connect(config);
            var escaped = EscapeFilterValue(userName);
            var filter = $"(&{config.SearchFilter}(|({config.UserNameAttribute}={escaped})({config.EmailAttribute}={escaped})))";
            var results = serviceConnection.Search(config.SearchBase, LdapConnection.ScopeSub, filter,
                config.Attributes.Concat(new[] { "distinguishedName" }).Distinct().ToArray(), false);
            if (!results.HasMore())
                return null;

            var entry = results.Next();
            using var userConnection = new LdapConnection();
            var port = config.UseSsl ? 636 : config.Port;
            userConnection.Connect(config.Server, port);
            if (!config.UseSsl && config.RequireStartTls)
                userConnection.StartTls();
            userConnection.Bind(entry.Dn, password);

            var profile = ToProfile(entry.GetAttributeSet(), entry.Dn, config);
            return profile with { UserName = profile.UserName ?? userName };
        }
        catch (Exception ex)
        {
            logger.LogInformation("LDAP authentication failed: {Reason}", ex.Message);
            return null;
        }
    }

    public async Task<User> ProvisionUserAsync(LdapUserProfile profile, CancellationToken ct = default)
    {
        var config = await runtime.GetLdapAsync(ct);
        var user = await userManager.FindByNameAsync(profile.UserName ?? string.Empty)
            ?? (!string.IsNullOrWhiteSpace(profile.Email) ? await userManager.FindByEmailAsync(profile.Email) : null);
        var isNew = user is null;
        user ??= new User
        {
            UserName = profile.UserName,
            Email = profile.Email ?? $"{profile.UserName}@his-hope.local",
            FirstName = profile.FirstName ?? profile.UserName ?? "Directory",
            LastName = profile.LastName ?? string.Empty,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
        user.Email = profile.Email ?? user.Email;
        user.FirstName = profile.FirstName ?? user.FirstName;
        user.LastName = profile.LastName ?? user.LastName;
        user.IsActive = profile.IsActive;

        var result = isNew ? await userManager.CreateAsync(user) : await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException("Unable to provision LDAP user.");

        await ApplyRolesAsync(user, MapGroupsToRoles(profile.MemberOf, config));
        return user;
    }

    private async Task<HashSet<string>> SearchAndSyncUsers(LdapConnection connection, LdapConfig config, CancellationToken ct)
    {
        var synced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = connection.Search(config.SearchBase, LdapConnection.ScopeSub, config.SearchFilter, config.Attributes, false);
        while (results.HasMore() && !ct.IsCancellationRequested)
        {
            try
            {
                var entry = results.Next();
                var profile = ToProfile(entry.GetAttributeSet(), entry.Dn, config);
                if (string.IsNullOrWhiteSpace(profile.UserName))
                    continue;
                var user = await ProvisionUserAsync(profile, ct);
                synced.Add(user.UserName!);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing LDAP entry");
            }
        }
        return synced;
    }

    private async Task DeactivateMissingUsers(HashSet<string> synced, CancellationToken ct)
    {
        var users = await userManager.Users
            .Where(u => u.EmailConfirmed && u.UserName != null && !synced.Contains(u.UserName))
            .Take(1000).ToListAsync(ct);
        foreach (var user in users)
        {
            user.IsActive = false;
            await userManager.UpdateAsync(user);
        }
    }

    private async Task ApplyRolesAsync(User user, IReadOnlyCollection<string> roles)
    {
        var existing = await userManager.GetRolesAsync(user);
        foreach (var role in roles.Except(existing))
            await userManager.AddToRoleAsync(user, role);
        foreach (var role in existing.Except(roles))
            if (role != "Provider")
                await userManager.RemoveFromRoleAsync(user, role);
    }

    private static List<string> MapGroupsToRoles(string[]? groups, LdapConfig config)
    {
        var roles = new List<string>();
        foreach (var group in groups ?? [])
            foreach (var (pattern, role) in config.GroupRoleMapping)
                if (group.Contains(pattern, StringComparison.OrdinalIgnoreCase) && !roles.Contains(role))
                    roles.Add(role);
        return roles;
    }

    private LdapConnection Connect(LdapConfig config)
    {
        var connection = new LdapConnection();
        var port = config.UseSsl ? 636 : config.Port;
        connection.Connect(config.Server, port);
        if (!config.UseSsl && config.RequireStartTls)
            connection.StartTls();
        connection.Bind(config.BindDn, config.BindPassword);
        return connection;
    }

    private static void Validate(LdapConfig config)
    {
        var errors = config.Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));
    }

    private static LdapUserProfile ToProfile(LdapAttributeSet attrs, string dn, LdapConfig config) => new(
        Get(attrs, config.UserNameAttribute), Get(attrs, config.EmailAttribute),
        Get(attrs, config.FirstNameAttribute), Get(attrs, config.LastNameAttribute),
        GetMany(attrs, config.MemberOfAttribute),
        !int.TryParse(Get(attrs, config.UserAccountControlAttribute), out var uac) || (uac & 0x2) == 0, dn);

    private static string? Get(LdapAttributeSet attrs, string name)
    {
        try { return attrs.GetAttribute(name)?.StringValue; } catch { return null; }
    }

    private static string[]? GetMany(LdapAttributeSet attrs, string name)
    {
        try { return attrs.GetAttribute(name)?.StringValueArray; } catch { return null; }
    }

    private static string EscapeFilterValue(string value) => value
        .Replace("\\", "\\5c", StringComparison.Ordinal)
        .Replace("*", "\\2a", StringComparison.Ordinal)
        .Replace("(", "\\28", StringComparison.Ordinal)
        .Replace(")", "\\29", StringComparison.Ordinal)
        .Replace("\0", "\\00", StringComparison.Ordinal);
}

public sealed record LdapUserProfile(
    string? UserName,
    string? Email,
    string? FirstName,
    string? LastName,
    string[]? MemberOf,
    bool IsActive,
    string DistinguishedName);
