using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class LdapSyncService
{
    private readonly IConfiguration _config;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<LdapSyncService> _logger;
    private readonly LdapConfig _ldapConfig;

    public LdapSyncService(
        IConfiguration config,
        UserManager<User> userManager,
        ILogger<LdapSyncService> logger)
    {
        _config = config;
        _userManager = userManager;
        _logger = logger;

        _ldapConfig = new LdapConfig();
        config.GetSection("Ldap").Bind(_ldapConfig);
    }

    public async Task SyncAsync(CancellationToken ct = default)
    {
        if (!_ldapConfig.Enabled)
        {
            _logger.LogInformation("LDAP sync is disabled");
            return;
        }

        _logger.LogInformation("Starting LDAP sync from {Server}:{Port}", _ldapConfig.Server, _ldapConfig.Port);

        try
        {
            using var connection = Connect();
            var syncedUsers = await SearchAndSyncUsers(connection, ct);

            if (_ldapConfig.SearchBase.Contains("OU="))
            {
                await DeactivateMissingUsers(syncedUsers, ct);
            }

            _logger.LogInformation("LDAP sync complete. Synced {Count} users", syncedUsers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LDAP sync failed");
        }
    }

    private LdapConnection Connect()
    {
        var port = _ldapConfig.UseSsl ? 636 : _ldapConfig.Port;
        var connection = new LdapConnection();
        connection.Connect(_ldapConfig.Server, port);

        if (_ldapConfig.UseSsl)
            connection.StartTls();

        connection.Bind(_ldapConfig.BindDn, _ldapConfig.BindPassword);
        _logger.LogDebug("Connected to LDAP {Server}", _ldapConfig.Server);
        return connection;
    }

    private async Task<HashSet<string>> SearchAndSyncUsers(LdapConnection connection, CancellationToken ct)
    {
        var syncedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var results = connection.Search(
            _ldapConfig.SearchBase,
            LdapConnection.ScopeSub,
            _ldapConfig.SearchFilter,
            _ldapConfig.Attributes,
            false);

        while (results.HasMore() && !ct.IsCancellationRequested)
        {
            try
            {
                var entry = results.Next();
                var attrs = entry.GetAttributeSet();

                var userName = GetStringAttribute(attrs, _ldapConfig.UserNameAttribute);
                var email = GetStringAttribute(attrs, _ldapConfig.EmailAttribute);
                var firstName = GetStringAttribute(attrs, _ldapConfig.FirstNameAttribute);
                var lastName = GetStringAttribute(attrs, _ldapConfig.LastNameAttribute);
                var memberOf = GetStringArrayAttribute(attrs, _ldapConfig.MemberOfAttribute);
                var userAccountControl = GetStringAttribute(attrs, _ldapConfig.UserAccountControlAttribute);

                if (string.IsNullOrEmpty(userName))
                {
                    userName = GetStringAttribute(attrs, "userPrincipalName")?.Split('@')[0];
                }

                if (string.IsNullOrEmpty(userName))
                {
                    _logger.LogWarning("Skipping LDAP entry without username");
                    continue;
                }

                bool isActive = true;
                if (int.TryParse(userAccountControl, out var uac))
                {
                    isActive = (uac & 0x2) == 0;
                }

                var roles = MapGroupsToRoles(memberOf);

                var user = await _userManager.FindByNameAsync(userName);
                var isNew = user is null;

                if (isNew)
                {
                    user = new User
                    {
                        UserName = userName,
                        Email = email ?? $"{userName}@his-hope.local",
                        FirstName = firstName ?? userName,
                        LastName = lastName ?? "",
                        IsActive = isActive,
                        EmailConfirmed = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        _logger.LogWarning("Failed to create LDAP user {UserName}: {Errors}",
                            userName, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                        continue;
                    }
                }
                else
                {
                    user.Email = email ?? user.Email;
                    user.FirstName = firstName ?? user.FirstName;
                    user.LastName = lastName ?? user.LastName;
                    user.IsActive = isActive;
                    await _userManager.UpdateAsync(user);
                }

                var existingRoles = await _userManager.GetRolesAsync(user);
                var rolesToAdd = roles.Except(existingRoles).ToList();
                var rolesToRemove = existingRoles.Except(roles).ToList();

                foreach (var role in rolesToAdd)
                    await _userManager.AddToRoleAsync(user, role);
                foreach (var role in rolesToRemove)
                    if (role != "Provider")
                        await _userManager.RemoveFromRoleAsync(user, role);

                syncedUsers.Add(userName);
                _logger.LogDebug("{Action} LDAP user: {UserName} ({Email}), roles: {Roles}",
                    isNew ? "Created" : "Updated", userName, email, string.Join(", ", roles));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing LDAP entry");
            }
        }

        return syncedUsers;
    }

    private async Task DeactivateMissingUsers(HashSet<string> syncedUsers, CancellationToken ct)
    {
        var ldapUsers = await _userManager.Users
            .Where(u => u.EmailConfirmed && u.UserName != null && !syncedUsers.Contains(u.UserName))
            .Take(1000)
            .ToListAsync(ct);

        foreach (var user in ldapUsers)
        {
            user.IsActive = false;
            await _userManager.UpdateAsync(user);
            _logger.LogWarning("Deactivated LDAP user {UserName} — not found in directory", user.UserName);
        }
    }

    private List<string> MapGroupsToRoles(string[]? memberOf)
    {
        if (memberOf is null) return new List<string>();

        var roles = new List<string>();
        foreach (var group in memberOf)
        {
            foreach (var (groupPattern, role) in _ldapConfig.GroupRoleMapping)
            {
                if (group.Contains(groupPattern, StringComparison.OrdinalIgnoreCase))
                {
                    if (!roles.Contains(role))
                        roles.Add(role);
                }
            }
        }
        return roles;
    }

    private static string? GetStringAttribute(LdapAttributeSet attrs, string name)
    {
        try { return attrs.GetAttribute(name)?.StringValue; }
        catch { return null; }
    }

    private static string[]? GetStringArrayAttribute(LdapAttributeSet attrs, string name)
    {
        try { return attrs.GetAttribute(name)?.StringValueArray; }
        catch { return null; }
    }
}
