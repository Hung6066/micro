using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.SharedKernel.Authorization;
using His.Hope.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenIddict.Abstractions;

namespace His.Hope.IdentityService.Infrastructure.Persistence;

/// <summary>
/// Seeds the Identity database with default permissions, roles, and admin user.
/// Uses the canonical permission codes from <see cref="HisHopePermissions"/> 
/// to guarantee consistency between authorization policies and seed data.
/// </summary>
public static class IdentityDbInitializer
{
    /// <summary>
    /// Synchronous entry point called from Program.cs startup.
    /// Wraps the async implementation for compatibility with non-async startup code.
    /// </summary>
    public static void Initialize(IServiceProvider serviceProvider)
    {
        Task.Run(async () => await InitializeAsync(serviceProvider)).GetAwaiter().GetResult();
    }

    private static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var configuration = scope.ServiceProvider.GetService<IConfiguration>();
        var hostEnvironment = scope.ServiceProvider.GetService<IHostEnvironment>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("IdentityDbInitializer");
        var ct = CancellationToken.None;

        var migrationRunner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        await migrationRunner.MigrateAsync(ct);

        // ──────────────────────────────────────────────
        // Step 0: Clean up old/incorrect permission codes
        // Removes any permission codes that don't match the canonical set
        // (e.g., old codes like "Patients".read that were seeded with wrong format)
        // ──────────────────────────────────────────────
        var canonicalCodes = HisHopePermissions.All;
        var obsoletePermissions = await context.Permissions
            .Where(p => !canonicalCodes.Contains(p.Code))
            .ToListAsync(ct);

        if (obsoletePermissions.Count > 0)
        {
            var obsoleteCodes = obsoletePermissions.Select(p => p.Code).ToList();
            logger.LogInformation("Removing {Count} obsolete permission codes: {Codes}",
                obsoleteCodes.Count, string.Join(", ", obsoleteCodes));

            // Remove all RolePermission entries referencing obsolete codes
            var obsoleteRolePerms = await context.RolePermissions
                .Where(rp => obsoleteCodes.Contains(rp.PermissionCode))
                .ToListAsync(ct);
            context.RolePermissions.RemoveRange(obsoleteRolePerms);

            // Remove the obsolete permissions themselves
            context.Permissions.RemoveRange(obsoletePermissions);
            await context.SaveChangesAsync(ct);
            logger.LogInformation("Obsolete permissions cleaned up.");
        }

        // ──────────────────────────────────────────────
        // Step 1: Seed Permissions (idempotent)
        // ──────────────────────────────────────────────
        logger.LogInformation("Seeding permissions...");
        foreach (var descriptor in HisHopePermissions.AllDescriptors)
        {
            if (!await context.Permissions.AnyAsync(p => p.Code == descriptor.Code, ct))
            {
                context.Permissions.Add(new Permission
                {
                    Code = descriptor.Code,
                    Name = descriptor.Name,
                    Group = descriptor.Group,
                    Description = descriptor.Description,
                    IsSystem = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Permissions seeded successfully.");

        // ──────────────────────────────────────────────
        // Step 2: Seed Roles (idempotent)
        // ──────────────────────────────────────────────
        var roleConfigs = new (string Name, string Description)[]
        {
            ("Admin", "Quản trị viên hệ thống — toàn quyền trên tất cả modules"),
            ("Provider", "Bác sĩ — khám và điều trị"),
            ("Nurse", "Điều dưỡng — hỗ trợ khám bệnh"),
            ("Receptionist", "Lễ tân — tiếp nhận bệnh nhân"),
            ("LabTechnician", "Kỹ thuật viên xét nghiệm"),
            ("Pharmacist", "Dược sĩ — cấp phát thuốc"),
            ("BillingClerk", "Nhân viên thanh toán"),
        };

        logger.LogInformation("Seeding roles...");
        foreach (var (name, description) in roleConfigs)
        {
            if (!await roleManager.RoleExistsAsync(name))
            {
                await roleManager.CreateAsync(new Role
                {
                    Name = name,
                    Description = description,
                    IsSystem = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        logger.LogInformation("Roles seeded successfully.");

        // ──────────────────────────────────────────────
        // Step 3: Assign Permissions to Roles (idempotent)
        // ──────────────────────────────────────────────
        logger.LogInformation("Assigning permissions to roles...");

        // Mapping: role name -> set of permission codes
        var rolePermissionMap = new Dictionary<string, HashSet<string>>
        {
            ["Admin"] = HisHopePermissions.All.ToHashSet(),

            ["Provider"] = new HashSet<string>
            {
                HisHopePermissions.Patients.View,
                HisHopePermissions.Patients.Create,
                HisHopePermissions.Patients.Update,
                HisHopePermissions.Appointments.View,
                HisHopePermissions.Appointments.Create,
                HisHopePermissions.Appointments.Update,
                HisHopePermissions.Appointments.Cancel,
                HisHopePermissions.Clinical.View,
                HisHopePermissions.Clinical.Create,
                HisHopePermissions.Clinical.Update,
                HisHopePermissions.Clinical.Sign,
                HisHopePermissions.LabOrders.View,
                HisHopePermissions.LabOrders.Create,
                HisHopePermissions.Pharmacy.View,
                HisHopePermissions.Pharmacy.Create,
                HisHopePermissions.Pharmacy.Dispense,
            },

            ["Nurse"] = new HashSet<string>
            {
                HisHopePermissions.Patients.View,
                HisHopePermissions.Patients.Update,
                HisHopePermissions.Appointments.View,
                HisHopePermissions.Appointments.CheckIn,
                HisHopePermissions.Clinical.View,
                HisHopePermissions.Clinical.Create,
                HisHopePermissions.Clinical.Update,
                HisHopePermissions.LabOrders.View,
            },

            ["Receptionist"] = new HashSet<string>
            {
                HisHopePermissions.Patients.View,
                HisHopePermissions.Patients.Create,
                HisHopePermissions.Appointments.View,
                HisHopePermissions.Appointments.Create,
                HisHopePermissions.Appointments.CheckIn,
                HisHopePermissions.Billing.View,
                HisHopePermissions.Billing.Create,
            },

            ["LabTechnician"] = new HashSet<string>
            {
                HisHopePermissions.LabOrders.View,
                HisHopePermissions.LabOrders.Create,
                HisHopePermissions.LabOrders.Update,
                HisHopePermissions.LabOrders.Result,
                HisHopePermissions.Patients.View,
            },

            ["Pharmacist"] = new HashSet<string>
            {
                HisHopePermissions.Pharmacy.View,
                HisHopePermissions.Pharmacy.Update,
                HisHopePermissions.Pharmacy.Dispense,
                HisHopePermissions.Patients.View,
            },

            ["BillingClerk"] = new HashSet<string>
            {
                HisHopePermissions.Billing.View,
                HisHopePermissions.Billing.Create,
                HisHopePermissions.Billing.Update,
                HisHopePermissions.Billing.Void,
                HisHopePermissions.Patients.View,
            },
        };

        foreach (var (roleName, permissions) in rolePermissionMap)
        {
            var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
            if (role is null) continue;

            foreach (var permissionCode in permissions)
            {
                // Verify the permission exists
                var permissionExists = await context.Permissions.AnyAsync(p => p.Code == permissionCode, ct);
                if (!permissionExists)
                {
                    logger.LogWarning("Permission {PermissionCode} not found in database, creating it.", permissionCode);
                    context.Permissions.Add(new Permission
                    {
                        Code = permissionCode,
                        Name = permissionCode,
                        Group = "Auto-created",
                        IsSystem = true,
                        CreatedAt = DateTime.UtcNow
                    });
                    await context.SaveChangesAsync(ct);
                }

                if (!await context.RolePermissions.AnyAsync(
                    rp => rp.RoleId == role.Id && rp.PermissionCode == permissionCode, ct))
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionCode = permissionCode
                    });
                }
            }
        }
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Permissions assigned to roles successfully.");

        // ──────────────────────────────────────────────
        // Step 4: Seed Admin User (idempotent)
        // ──────────────────────────────────────────────
        logger.LogInformation("Seeding admin user...");

        var adminUser = await userManager.FindByNameAsync(AdminBootstrapConfiguration.DefaultUserName);
        var adminBootstrap = ResolveAdminBootstrapConfiguration(
            configuration,
            hostEnvironment?.EnvironmentName,
            adminUser is not null);

        if (adminBootstrap.SkipUserSeed)
        {
            logger.LogWarning(
                "Admin bootstrap user was not created because Identity:BootstrapAdmin:Password is not configured. Configure it with a one-time secret when admin seeding is required.");
        }

        if (adminUser is null && adminBootstrap.SkipUserSeed)
        {
            logger.LogInformation("Admin user seed skipped.");
        }
        else if (adminUser is null)
        {
            adminUser = new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                UserName = AdminBootstrapConfiguration.DefaultUserName,
                Email = AdminBootstrapConfiguration.DefaultEmail,
                FirstName = "Quản Trị",
                LastName = "Viên",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(adminUser, adminBootstrap.Password!);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                logger.LogError("Failed to create admin user: {Errors}", errors);
            }
            else
            {
                logger.LogInformation("Admin user created successfully.");
            }
        }
        else
        {
            logger.LogInformation("Admin user already exists.");
        }

        if (adminUser is not null && !adminBootstrap.SkipUserSeed)
        {
            // Ensure admin user is in Admin role
            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Admin user assigned to Admin role.");
            }

            // Ensure admin user is NOT in Provider role (clean up if needed)
            if (await userManager.IsInRoleAsync(adminUser, "Provider"))
            {
                await userManager.RemoveFromRoleAsync(adminUser, "Provider");
                logger.LogInformation("Admin user removed from Provider role.");
            }
        }

        // ──────────────────────────────────────────────
        // Step 5: Seed OpenIddict Application (idempotent)
        // ──────────────────────────────────────────────
        logger.LogInformation("Seeding OIDC application...");

        var appManager = scope.ServiceProvider.GetRequiredService<
            OpenIddict.Abstractions.IOpenIddictApplicationManager>();
        var oidcClients = ResolveOidcClientUris(
            configuration,
            hostEnvironment?.EnvironmentName);

        const string spaClientId = "his-hope-spa";
        var existingSpaClient = await appManager.FindByClientIdAsync(spaClientId, ct);
        if (existingSpaClient is null)
        {
            var spaUris = oidcClients[spaClientId];
            var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor
            {
                ClientId = spaClientId,
                ClientType = OpenIddict.Abstractions.OpenIddictConstants.ClientTypes.Public,
                DisplayName = "His.Hope SPA (BFF)",
                Permissions =
                {
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Logout,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Revocation,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "profile",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "email",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "hishop:permissions",
                },
                Requirements =
                {
                    OpenIddict.Abstractions.OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
                }
            };
            AddClientUris(descriptor, spaUris);
            await appManager.CreateAsync(descriptor, ct);
            logger.LogInformation("OIDC application '{ClientId}' created.", spaClientId);
        }
        else
        {
            await UpdateClientUrisAsync(appManager, existingSpaClient, oidcClients[spaClientId], ct);
        }

        var dashboardClientId = "his-hope-dashboard";
        var existingDashboardClient = await appManager.FindByClientIdAsync(dashboardClientId, ct);
        if (existingDashboardClient is null)
        {
            var dashboardUris = oidcClients[dashboardClientId];
            var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor
            {
                ClientId = dashboardClientId,
                ClientType = OpenIddict.Abstractions.OpenIddictConstants.ClientTypes.Public,
                DisplayName = "His.Hope System Dashboard",
                Permissions =
                {
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Logout,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Revocation,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "profile",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "email",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "hishop:permissions",
                },
                Requirements =
                {
                    OpenIddict.Abstractions.OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
                }
            };
            AddClientUris(descriptor, dashboardUris);
            await appManager.CreateAsync(descriptor, ct);
            logger.LogInformation("OIDC application '{ClientId}' created.", dashboardClientId);
        }
        else
        {
            await UpdateClientUrisAsync(
                appManager,
                existingDashboardClient,
                oidcClients[dashboardClientId],
                ct);
        }

        // Seed M2M confidential clients for service-to-service auth
        const string adminClientId = "his-hope-admin";
        var existingAdminClient = await appManager.FindByClientIdAsync(adminClientId, ct);
        if (existingAdminClient is null)
        {
            var adminUris = oidcClients[adminClientId];
            var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor
            {
                ClientId = adminClientId,
                ClientType = OpenIddict.Abstractions.OpenIddictConstants.ClientTypes.Public,
                DisplayName = "His.Hope Admin App",
                Permissions =
                {
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Logout,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Revocation,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "profile",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "email",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "hishop:permissions",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "hishop:admin",
                },
                Requirements =
                {
                    OpenIddict.Abstractions.OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
                }
            };
            AddClientUris(descriptor, adminUris);
            await appManager.CreateAsync(descriptor, ct);
            logger.LogInformation("OIDC application '{ClientId}' created.", adminClientId);
        }
        else
        {
            await UpdateClientUrisAsync(appManager, existingAdminClient, oidcClients[adminClientId], ct);
        }

        const string mobileClientId = "his-hope-mobile";
        var existingMobileClient = await appManager.FindByClientIdAsync(mobileClientId, ct);
        if (existingMobileClient is null)
        {
            var mobileUris = oidcClients[mobileClientId];
            var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor
            {
                ClientId = mobileClientId,
                ClientType = OpenIddict.Abstractions.OpenIddictConstants.ClientTypes.Public,
                DisplayName = "His.Hope Mobile Admin",
                Permissions =
                {
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Logout,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Revocation,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "profile",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "email",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "hishop:permissions",
                    OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "hishop:admin",
                },
                Requirements =
                {
                    OpenIddict.Abstractions.OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
                }
            };
            AddClientUris(descriptor, mobileUris);
            await appManager.CreateAsync(descriptor, ct);
            logger.LogInformation("OIDC application '{ClientId}' created.", mobileClientId);
        }
        else
        {
            await UpdateClientUrisAsync(appManager, existingMobileClient, oidcClients[mobileClientId], ct);
        }

        var m2mClients = new[]
        {
            new { ClientId = "patient-service", DisplayName = "Patient Service (M2M)", Scopes = "hishop:patients hishop:appointments" },
            new { ClientId = "lab-service", DisplayName = "Lab Service (M2M)", Scopes = "hishop:lab hishop:patients" },
            new { ClientId = "pharmacy-service", DisplayName = "Pharmacy Service (M2M)", Scopes = "hishop:pharmacy hishop:patients" },
            new { ClientId = "billing-service", DisplayName = "Billing Service (M2M)", Scopes = "hishop:billing hishop:patients" },
            new { ClientId = "clinical-service", DisplayName = "Clinical Service (M2M)", Scopes = "hishop:clinical hishop:patients" },
            new { ClientId = "appointment-service", DisplayName = "Appointment Service (M2M)", Scopes = "hishop:appointments hishop:patients" },
        };

        var vaultStore = scope.ServiceProvider.GetRequiredService<VaultClientSecretStore>();

        foreach (var m2m in m2mClients)
        {
            if (await appManager.FindByClientIdAsync(m2m.ClientId, ct) is null)
            {
                var secret = vaultStore.GenerateSecret(m2m.ClientId);
                await vaultStore.StoreSecretAsync(m2m.ClientId, secret, ct);

                await appManager.CreateAsync(new OpenIddict.Abstractions.OpenIddictApplicationDescriptor
                {
                    ClientId = m2m.ClientId,
                    ClientSecret = secret,
                    ClientType = OpenIddict.Abstractions.OpenIddictConstants.ClientTypes.Confidential,
                    DisplayName = m2m.DisplayName,
                    Permissions =
                    {
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Introspection,
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                    }
                }, ct);

                logger.LogInformation("M2M client '{ClientId}' created with scopes: {Scopes}", m2m.ClientId, m2m.Scopes);
            }
        }

        // ──────────────────────────────────────────────
        // Step 6: Seed OIDC Scopes (idempotent)
        // ──────────────────────────────────────────────
        logger.LogInformation("Seeding OIDC scopes...");

        var scopeManager = scope.ServiceProvider.GetRequiredService<
            OpenIddict.Abstractions.IOpenIddictScopeManager>();

        var scopeNames = new[] { "hishop:permissions", "hishop:patients", "hishop:appointments", "hishop:clinical", "hishop:lab", "hishop:billing", "hishop:pharmacy", "hishop:admin" };
        foreach (var scopeName in scopeNames)
        {
            if (await scopeManager.FindByNameAsync(scopeName, ct) is null)
            {
                await scopeManager.CreateAsync(new OpenIddict.Abstractions.OpenIddictScopeDescriptor
                {
                    Name = scopeName,
                    DisplayName = $"His.Hope - {scopeName.Replace("hishop:", "").ToUpperInvariant()}",
                    Resources = { "his-hope-services" }
                }, ct);
            }
        }

        logger.LogInformation("OIDC scopes seeded successfully.");
        logger.LogInformation("Database seeding completed successfully.");
    }

    public static IReadOnlyDictionary<string, OidcClientUris> ResolveOidcClientUris(
        IConfiguration? configuration,
        string? environmentName)
    {
        var clientSections = configuration?
            .GetSection("Authentication:OidcClients")
            .GetChildren()
            .ToArray() ?? [];

        if (clientSections.Length == 0)
        {
            throw new InvalidOperationException(
                "Identity OIDC client registrations require Authentication:OidcClients configuration.");
        }

        var clients = new Dictionary<string, OidcClientUris>(StringComparer.Ordinal);
        foreach (var clientSection in clientSections)
        {
            var clientId = clientSection.Key;
            var redirectUris = ReadUris(clientSection, "RedirectUris", clientId);
            var postLogoutRedirectUris = ReadUris(clientSection, "PostLogoutRedirectUris", clientId);

            if (string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase))
            {
                ValidateProductionUris(clientId, redirectUris, postLogoutRedirectUris);
            }

            clients.Add(clientId, new OidcClientUris(redirectUris, postLogoutRedirectUris));
        }

        return clients;
    }

    private static Uri[] ReadUris(IConfiguration clientSection, string settingName, string clientId)
    {
        var values = clientSection
            .GetSection(settingName)
            .GetChildren()
            .Select(child => child.Value)
            .ToArray();

        if (values.Length == 0 || values.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                $"Identity OIDC client '{clientId}' requires configured {settingName}.");
        }

        return values.Select(value => Uri.TryCreate(value, UriKind.Absolute, out var uri)
                ? uri
                : throw new InvalidOperationException(
                    $"Identity OIDC client '{clientId}' contains an invalid {settingName} URI '{value}'."))
            .ToArray();
    }

    private static void ValidateProductionUris(
        string clientId,
        IEnumerable<Uri> redirectUris,
        IEnumerable<Uri> postLogoutRedirectUris)
    {
        foreach (var uri in redirectUris.Concat(postLogoutRedirectUris))
        {
            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                uri.IsLoopback ||
                uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Identity OIDC client '{clientId}' contains a non-production redirect URI '{uri}'. Production registrations require HTTPS non-localhost URIs.");
            }
        }
    }

    private static void AddClientUris(
        OpenIddict.Abstractions.OpenIddictApplicationDescriptor descriptor,
        OidcClientUris uris)
    {
        foreach (var uri in uris.RedirectUris)
        {
            descriptor.RedirectUris.Add(uri);
        }

        foreach (var uri in uris.PostLogoutRedirectUris)
        {
            descriptor.PostLogoutRedirectUris.Add(uri);
        }
    }

    private static async Task UpdateClientUrisAsync(
        OpenIddict.Abstractions.IOpenIddictApplicationManager appManager,
        object application,
        OidcClientUris uris,
        CancellationToken cancellationToken)
    {
        var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor();
        await appManager.PopulateAsync(descriptor, application, cancellationToken);
        descriptor.RedirectUris.Clear();
        descriptor.PostLogoutRedirectUris.Clear();
        AddClientUris(descriptor, uris);
        await appManager.UpdateAsync(application, descriptor, cancellationToken);
    }

    public static AdminBootstrapConfiguration ResolveAdminBootstrapConfiguration(
        IConfiguration? configuration,
        string? environmentName,
        bool adminUserExists)
    {
        var password = configuration?["Identity:BootstrapAdmin:Password"]
            ?? configuration?["IDENTITY_BOOTSTRAP_ADMIN_PASSWORD"];
        var hasPassword = !string.IsNullOrWhiteSpace(password);

        if (adminUserExists)
        {
            return new AdminBootstrapConfiguration(password, SkipUserSeed: false);
        }

        if (hasPassword)
        {
            return new AdminBootstrapConfiguration(password, SkipUserSeed: false);
        }

        if (string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Identity admin bootstrap requires configuration key 'Identity:BootstrapAdmin:Password' or 'IDENTITY_BOOTSTRAP_ADMIN_PASSWORD' in Production when the admin user does not exist.");
        }

        return new AdminBootstrapConfiguration(null, SkipUserSeed: true);
    }

    public sealed record AdminBootstrapConfiguration(string? Password, bool SkipUserSeed)
    {
        public const string DefaultUserName = "admin";
        public const string DefaultEmail = "admin@hishop.com";
    }

    public sealed record OidcClientUris(
        IReadOnlyList<Uri> RedirectUris,
        IReadOnlyList<Uri> PostLogoutRedirectUris);
}
