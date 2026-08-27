using System.Security.Claims;
using System.Text.Json;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.SharedKernel.Authorization;
using His.Hope.IdentityService.Application.Authorization;
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

        var migrationOnly = configuration?.GetValue<bool>("Persistence:MigrationOnly") == true;
        if (configuration?.GetValue<bool>("Persistence:RunMigrationsOnStartup") != true && !migrationOnly)
        {
            logger.LogInformation(
                "Skipping identity migration and seed on API startup; run the dedicated persistence job first.");
            return;
        }

        var migrationRunner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        await MigrateWhenDatabaseReadyAsync(migrationRunner, logger, ct);

        if (migrationOnly)
        {
            logger.LogInformation("Identity migration-only mode completed; skipping seed and API startup.");
            return;
        }

        // ──────────────────────────────────────────────
        // Step 0: Clean up old/incorrect permission codes
        // Removes any permission codes that don't match the canonical set
        // (e.g., old codes like "Patients".read that were seeded with wrong format)
        // ──────────────────────────────────────────────
        var registeredPrefixes = await context.IamServiceDefinitions
            .Where(item => item.IsActive)
            .Select(item => item.PermissionPrefix)
            .ToListAsync(ct);
        var obsoletePermissions = await context.Permissions
            .ToListAsync(ct);
        obsoletePermissions = obsoletePermissions
            .Where(permission => !PermissionCatalogRules.IsValid(permission.Code, registeredPrefixes))
            .ToList();

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
                HisHopePermissions.Dashboard.View,
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
                HisHopePermissions.Dashboard.View,
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
                HisHopePermissions.Dashboard.View,
            },

            ["LabTechnician"] = new HashSet<string>
            {
                HisHopePermissions.LabOrders.View,
                HisHopePermissions.LabOrders.Create,
                HisHopePermissions.LabOrders.Update,
                HisHopePermissions.LabOrders.Result,
                HisHopePermissions.LabOrders.AlertAcknowledge,
                HisHopePermissions.LabOrders.AlertResolve,
                HisHopePermissions.Patients.View,
                HisHopePermissions.Dashboard.View,
            },

            ["Pharmacist"] = new HashSet<string>
            {
                HisHopePermissions.Pharmacy.View,
                HisHopePermissions.Pharmacy.Update,
                HisHopePermissions.Pharmacy.Dispense,
                HisHopePermissions.Patients.View,
                HisHopePermissions.Dashboard.View,
            },

            ["BillingClerk"] = new HashSet<string>
            {
                HisHopePermissions.Billing.View,
                HisHopePermissions.Billing.Create,
                HisHopePermissions.Billing.Update,
                HisHopePermissions.Billing.Void,
                HisHopePermissions.Patients.View,
                HisHopePermissions.Dashboard.View,
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
                // Do not continue with role assignment for an entity that was
                // not persisted. Otherwise UserManager attempts to insert a
                // dangling asp_net_user_roles row and prevents the service
                // from becoming ready on the next restart.
                adminUser = null;
            }
            else
            {
                logger.LogInformation("Admin user created successfully.");
            }
        }
        else
        {
            logger.LogInformation("Admin user already exists.");

            if (ShouldResetAdminPassword(configuration) && !adminBootstrap.SkipUserSeed)
            {
                var resetToken = await userManager.GeneratePasswordResetTokenAsync(adminUser);
                var resetResult = await userManager.ResetPasswordAsync(
                    adminUser,
                    resetToken,
                    adminBootstrap.Password!);

                if (!resetResult.Succeeded)
                {
                    var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to reset the Identity admin bootstrap password: {errors}");
                }

                logger.LogInformation("Admin bootstrap password reset completed.");
            }
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

        await SeedAdditionalConfiguredOidcClientsAsync(appManager, oidcClients, configuration, logger, ct);

        var m2mClients = new[]
        {
            new { ClientId = "patient-service", DisplayName = "Patient Service (M2M)", Scopes = "hishop:patients hishop:appointments" },
            new { ClientId = "lab-service", DisplayName = "Lab Service (M2M)", Scopes = "hishop:lab hishop:patients" },
            new { ClientId = "pharmacy-service", DisplayName = "Pharmacy Service (M2M)", Scopes = "hishop:pharmacy hishop:patients" },
            new { ClientId = "billing-service", DisplayName = "Billing Service (M2M)", Scopes = "hishop:billing hishop:patients" },
            new { ClientId = "clinical-service", DisplayName = "Clinical Service (M2M)", Scopes = "hishop:clinical hishop:patients" },
            new { ClientId = "appointment-service", DisplayName = "Appointment Service (M2M)", Scopes = "hishop:appointments hishop:patients" },
            new { ClientId = "scim-provisioner", DisplayName = "SCIM Provisioner (M2M)", Scopes = "scim.read scim.write" },
        };

        var vaultStore = scope.ServiceProvider.GetRequiredService<VaultClientSecretStore>();
        var isDevelopment = hostEnvironment?.IsDevelopment() == true ||
            string.Equals(configuration?["ASPNETCORE_ENVIRONMENT"], Environments.Development, StringComparison.OrdinalIgnoreCase);

        foreach (var m2m in m2mClients)
        {
            var existingM2m = await appManager.FindByClientIdAsync(m2m.ClientId, ct);
            if (existingM2m is null)
            {
                var secret = vaultStore.GenerateSecret(m2m.ClientId);
                await vaultStore.StoreSecretAsync(m2m.ClientId, secret, ct);

                var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor
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
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "profile",
                    }
                };
                foreach (var requestedScope in m2m.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + requestedScope);
                await appManager.CreateAsync(descriptor, ct);

                logger.LogInformation("M2M client '{ClientId}' created with scopes: {Scopes}", m2m.ClientId, m2m.Scopes);
            }
            else if (isDevelopment && !vaultStore.UsesPersistentStore)
            {
                // In local development the OpenIddict database stores only a
                // hash. Re-derive the configured dev secret on startup so a
                // restart does not strand seeded M2M clients. Production uses
                // Vault/KMS and never enters this branch.
                var secret = vaultStore.GenerateSecret(m2m.ClientId);
                await vaultStore.StoreSecretAsync(m2m.ClientId, secret, ct);
                await appManager.UpdateAsync(existingM2m, secret, ct);
                logger.LogInformation("Synchronized development M2M client secret for '{ClientId}'.", m2m.ClientId);
            }
        }

        // ──────────────────────────────────────────────
        // Step 6: Seed OIDC Scopes (idempotent)
        // ──────────────────────────────────────────────
        logger.LogInformation("Seeding OIDC scopes...");

        var scopeManager = scope.ServiceProvider.GetRequiredService<
            OpenIddict.Abstractions.IOpenIddictScopeManager>();

        var scopeNames = new[]
        {
            "hishop:permissions", "hishop:patients", "hishop:appointments",
            "hishop:clinical", "hishop:lab", "hishop:billing", "hishop:pharmacy",
            "hishop:admin", "fhir.patient.read", "fhir.encounter.read",
            "platform.continuity.write", "scim.read", "scim.write"
        };
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
        await SeedControlPlaneSampleDataAsync(context, configuration, userManager, logger, ct);
        if (configuration?.GetValue("Conglomerate:Enabled", false) == true)
        {
            if (configuration.GetValue("Conglomerate:SeedPilotUsers", true))
            {
                await SeedConglomeratePilotUsersAsync(
                    userManager,
                    configuration,
                    hostEnvironment,
                    logger,
                    ct);
            }

            if (configuration.GetValue("Conglomerate:SkipDemoHospitalScope", true))
            {
                await SeedConglomerateIamGraphAsync(context, userManager, configuration, logger, ct);
            }

            if (configuration.GetValue("Conglomerate:SeedPilotMemberships", true))
            {
                await SeedConglomeratePilotMembershipsAsync(userManager, configuration, logger, ct);
            }
        }
        logger.LogInformation("Database seeding completed successfully.");
    }

    private static async Task SeedConglomeratePilotMembershipsAsync(
        UserManager<User> userManager,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken ct)
    {
        var admin = await userManager.FindByNameAsync(AdminBootstrapConfiguration.DefaultUserName);
        if (admin is null)
            admin = await userManager.FindByEmailAsync(AdminBootstrapConfiguration.DefaultEmail);
        if (admin is null)
        {
            logger.LogWarning("Skipping conglomerate pilot memberships because the admin user does not exist.");
            return;
        }

        var tenantSections = configuration.GetSection("Conglomerate:Tenants").GetChildren().ToArray();
        var existingClaims = await userManager.GetClaimsAsync(admin);
        foreach (var tenantSection in tenantSections)
        {
            var tenantKey = tenantSection["Key"];
            if (string.IsNullOrWhiteSpace(tenantKey))
                continue;

            if (existingClaims.Any(claim =>
                    claim.Type == "tenant_membership" &&
                    string.Equals(claim.Value, tenantKey, StringComparison.OrdinalIgnoreCase)))
                continue;

            await userManager.AddClaimAsync(admin, new Claim("tenant_membership", tenantKey));
            logger.LogInformation("Granted tenant membership '{TenantKey}' to admin user.", tenantKey);
        }
    }

    private static async Task SeedConglomerateIamGraphAsync(
        IdentityDbContext db,
        UserManager<User> userManager,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken ct)
    {
        var actor = await db.Users.AsNoTracking().OrderBy(x => x.UserName).FirstOrDefaultAsync(ct);
        if (actor is null)
        {
            logger.LogWarning("Skipping conglomerate IAM graph seed because no actor user exists.");
            return;
        }

        var actorId = actor.Id;
        var seedDocument = LoadConglomerateSeedDocument(configuration, logger);
        if (seedDocument is null)
            return;

        var serviceDefinitions = new (string Key, string Name, string Prefix)[]
        {
            ("identity", "Identity Service", "identity"), ("patients", "Patient Service", "patients"),
            ("clinical", "Clinical Service", "clinical"), ("appointments", "Appointment Service", "appointments"),
            ("billing", "Billing Service", "billing"), ("pharmacy", "Pharmacy Service", "pharmacy"),
            ("lab", "Laboratory Service", "lab"),
            ("fhir", "FHIR Service", "fhir"),
            ("external-integration", "External Integration Service", "external"),
            ("database-continuity", "Database Continuity Service", "admin"),
            ("remediation", "Remediation Operator", "admin"),
            ("mobile", "Mobile Platform", "admin")
        };
        foreach (var definition in serviceDefinitions)
        {
            if (!await db.IamServiceDefinitions.AnyAsync(x => x.Key == definition.Key, ct))
            {
                db.IamServiceDefinitions.Add(new IamServiceDefinition
                {
                    Key = definition.Key,
                    DisplayName = definition.Name,
                    PermissionPrefix = definition.Prefix,
                    Owner = "identity-service"
                });
            }
        }
        await db.SaveChangesAsync(ct);

        var scopes = await db.IamScopes.AsNoTracking().ToListAsync(ct);
        foreach (var tenantSeed in seedDocument.RootElement.GetProperty("tenants").EnumerateArray())
        {
            var tenantKey = tenantSeed.GetProperty("key").GetString();
            if (string.IsNullOrWhiteSpace(tenantKey))
                continue;

            var tenantScope = scopes.FirstOrDefault(x => x.Kind == "tenant" && x.Key == tenantKey);
            if (tenantScope is null)
            {
                logger.LogWarning("Skipping conglomerate seed for unknown tenant '{TenantKey}'.", tenantKey);
                continue;
            }

            var accountScope = scopes.FirstOrDefault(x => x.Kind == "account" && x.ParentId == tenantScope.Id);
            var environmentScope = accountScope is null
                ? null
                : scopes.FirstOrDefault(x => x.Kind == "environment" && x.ParentId == accountScope.Id);
            if (environmentScope is null)
            {
                logger.LogWarning("Skipping conglomerate seed graph for tenant '{TenantKey}' without environment scope.", tenantKey);
                continue;
            }

            if (tenantSeed.TryGetProperty("groups", out var groups) && groups.ValueKind == JsonValueKind.Array)
            {
                foreach (var groupSeed in groups.EnumerateArray())
                {
                    var groupKey = groupSeed.GetProperty("key").GetString();
                    var groupName = groupSeed.GetProperty("displayName").GetString();
                    if (string.IsNullOrWhiteSpace(groupKey) || string.IsNullOrWhiteSpace(groupName))
                        continue;

                    if (!await db.IamGroups.AnyAsync(x => x.Key == groupKey && x.ScopeId == tenantScope.Id, ct))
                    {
                        db.IamGroups.Add(new IamGroup
                        {
                            Key = groupKey,
                            DisplayName = groupName,
                            ScopeId = tenantScope.Id,
                            CreatedBy = actorId.ToString()
                        });
                    }
                }
            }

            if (tenantSeed.TryGetProperty("permissionSets", out var permissionSets) &&
                permissionSets.ValueKind == JsonValueKind.Array)
            {
                foreach (var setSeed in permissionSets.EnumerateArray())
                {
                    var setKey = setSeed.GetProperty("key").GetString();
                    var setName = setSeed.GetProperty("displayName").GetString();
                    if (string.IsNullOrWhiteSpace(setKey) || string.IsNullOrWhiteSpace(setName))
                        continue;

                    var permissions = setSeed.GetProperty("permissions").EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString()!)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToArray();
                    var permissionsJson = JsonSerializer.Serialize(permissions);

                    var existingPermissionSet = await db.IamPermissionSets.FirstOrDefaultAsync(
                        x => x.Key == setKey && x.ScopeId == environmentScope.Id,
                        ct);
                    if (existingPermissionSet is null)
                    {
                        db.IamPermissionSets.Add(new IamPermissionSet
                        {
                            Key = setKey,
                            DisplayName = setName,
                            ScopeId = environmentScope.Id,
                            PermissionsJson = permissionsJson,
                            LifecycleStatus = "published",
                            CreatedBy = actorId.ToString(),
                            PublishedAt = DateTime.UtcNow
                        });
                    }
                    else if (!string.Equals(existingPermissionSet.PermissionsJson, permissionsJson, StringComparison.Ordinal))
                    {
                        existingPermissionSet.DisplayName = setName;
                        existingPermissionSet.PermissionsJson = permissionsJson;
                        existingPermissionSet.Version++;
                        existingPermissionSet.LifecycleStatus = "published";
                        existingPermissionSet.PublishedAt = DateTime.UtcNow;
                    }
                }
            }

            await db.SaveChangesAsync(ct);

            var permissionSetsByKey = await db.IamPermissionSets.AsNoTracking()
                .Where(set => set.ScopeId == environmentScope.Id)
                .ToDictionaryAsync(set => set.Key, StringComparer.Ordinal, ct);
            var groupsByKey = await db.IamGroups.AsNoTracking()
                .Where(group => group.ScopeId == tenantScope.Id)
                .ToDictionaryAsync(group => group.Key, StringComparer.Ordinal, ct);

            if (tenantSeed.TryGetProperty("groupMemberships", out var memberships) &&
                memberships.ValueKind == JsonValueKind.Array)
            {
                foreach (var membershipSeed in memberships.EnumerateArray())
                {
                    var groupKey = membershipSeed.GetProperty("groupKey").GetString();
                    var userName = membershipSeed.GetProperty("userName").GetString();
                    if (string.IsNullOrWhiteSpace(groupKey) || string.IsNullOrWhiteSpace(userName))
                        continue;
                    if (!groupsByKey.TryGetValue(groupKey, out var group))
                        continue;
                    var user = await userManager.FindByNameAsync(userName);
                    if (user is null)
                        continue;
                    if (!await db.IamGroupMemberships.AnyAsync(
                            membership => membership.GroupId == group.Id && membership.UserId == user.Id, ct))
                    {
                        db.IamGroupMemberships.Add(new IamGroupMembership
                        {
                            GroupId = group.Id,
                            UserId = user.Id,
                            CreatedBy = actorId.ToString()
                        });
                    }
                }
            }

            if (tenantSeed.TryGetProperty("assignments", out var assignments) &&
                assignments.ValueKind == JsonValueKind.Array)
            {
                foreach (var assignmentSeed in assignments.EnumerateArray())
                {
                    var permissionSetKey = assignmentSeed.GetProperty("permissionSetKey").GetString();
                    var principalType = assignmentSeed.TryGetProperty("principalType", out var typeElement)
                        ? typeElement.GetString() ?? "human"
                        : "human";
                    if (string.IsNullOrWhiteSpace(permissionSetKey) ||
                        !permissionSetsByKey.TryGetValue(permissionSetKey, out var permissionSet))
                        continue;

                    Guid principalId;
                    if (string.Equals(principalType, "group", StringComparison.OrdinalIgnoreCase))
                    {
                        var groupKey = assignmentSeed.GetProperty("groupKey").GetString();
                        if (string.IsNullOrWhiteSpace(groupKey) || !groupsByKey.TryGetValue(groupKey, out var group))
                            continue;
                        principalId = group.Id;
                    }
                    else
                    {
                        var userName = assignmentSeed.GetProperty("userName").GetString();
                        if (string.IsNullOrWhiteSpace(userName))
                            continue;
                        var user = await userManager.FindByNameAsync(userName);
                        if (user is null)
                            continue;
                        principalId = user.Id;
                    }

                    if (!await db.IamPermissionSetAssignments.AnyAsync(assignment =>
                            assignment.PermissionSetId == permissionSet.Id &&
                            assignment.PrincipalId == principalId &&
                            assignment.PrincipalType == principalType &&
                            assignment.ScopeId == environmentScope.Id, ct))
                    {
                        db.IamPermissionSetAssignments.Add(new IamPermissionSetAssignment
                        {
                            PermissionSetId = permissionSet.Id,
                            PrincipalId = principalId,
                            PrincipalType = principalType,
                            ScopeId = environmentScope.Id,
                            CreatedBy = actorId.ToString()
                        });
                    }
                }
            }

            if (tenantSeed.TryGetProperty("boundaries", out var boundaries) &&
                boundaries.ValueKind == JsonValueKind.Array)
            {
                foreach (var boundarySeed in boundaries.EnumerateArray())
                {
                    var userName = boundarySeed.GetProperty("userName").GetString();
                    var boundaryTenantKey = boundarySeed.GetProperty("tenantKey").GetString();
                    if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(boundaryTenantKey))
                        continue;
                    var user = await userManager.FindByNameAsync(userName);
                    if (user is null)
                        continue;

                    var allowedPermissions = boundarySeed.GetProperty("allowedPermissions").EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString()!)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToArray();

                    var allowedPermissionsJson = JsonSerializer.Serialize(allowedPermissions);
                    var existingBoundary = await db.IamPermissionBoundaries.FirstOrDefaultAsync(boundary =>
                        boundary.PrincipalId == user.Id &&
                        boundary.PrincipalType == AuthorizationConstants.PrincipalTypes.Human &&
                        boundary.ScopeId == environmentScope.Id, ct);
                    if (existingBoundary is null)
                    {
                        db.IamPermissionBoundaries.Add(new IamPermissionBoundary
                        {
                            PrincipalId = user.Id,
                            PrincipalType = AuthorizationConstants.PrincipalTypes.Human,
                            ScopeId = environmentScope.Id,
                            AllowedPermissionsJson = allowedPermissionsJson,
                            ResourceConstraintsJson = JsonSerializer.Serialize(new { tenant = boundaryTenantKey }),
                            CreatedBy = actorId.ToString()
                        });
                    }
                    else if (!string.Equals(existingBoundary.AllowedPermissionsJson, allowedPermissionsJson, StringComparison.Ordinal))
                    {
                        existingBoundary.AllowedPermissionsJson = allowedPermissionsJson;
                        existingBoundary.ResourceConstraintsJson =
                            JsonSerializer.Serialize(new { tenant = boundaryTenantKey });
                        existingBoundary.IsActive = true;
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Conglomerate IAM graph seeded from seed-data document.");
    }

    private static async Task SeedConglomeratePilotUsersAsync(
        UserManager<User> userManager,
        IConfiguration configuration,
        IHostEnvironment? hostEnvironment,
        ILogger logger,
        CancellationToken ct)
    {
        var pilots = ResolveConglomeratePilotSeeds(configuration, logger);
        if (pilots.Count == 0)
        {
            logger.LogWarning("Skipping conglomerate pilot users because no pilot seed entries were found.");
            return;
        }

        var password = ResolveConglomeratePilotPassword(configuration, hostEnvironment, logger);
        if (string.IsNullOrWhiteSpace(password))
            return;

        foreach (var pilot in pilots)
        {
            var userName = pilot.UserName;
            var email = pilot.Email;
            var tenantKey = pilot.TenantKey;
            var roleName = pilot.RoleName;
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(tenantKey))
                continue;

            var user = await userManager.FindByNameAsync(userName);
            if (user is null)
            {
                user = new User
                {
                    UserName = userName,
                    Email = email,
                    EmailConfirmed = true,
                    IsActive = true,
                    FirstName = pilot.FirstName,
                    LastName = pilot.LastName,
                };
                var createResult = await userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                {
                    logger.LogWarning(
                        "Unable to create conglomerate pilot user '{UserName}': {Errors}",
                        userName,
                        string.Join(", ", createResult.Errors.Select(error => error.Description)));
                    continue;
                }

                logger.LogInformation("Created conglomerate pilot user '{UserName}'.", userName);
            }
            else
            {
                var updated = false;
                if (!user.IsActive)
                {
                    user.IsActive = true;
                    updated = true;
                }

                if (!user.EmailConfirmed)
                {
                    user.EmailConfirmed = true;
                    updated = true;
                }

                if (string.IsNullOrWhiteSpace(user.Email) &&
                    !string.IsNullOrWhiteSpace(email))
                {
                    user.Email = email;
                    updated = true;
                }

                if (updated)
                    await userManager.UpdateAsync(user);

                if (ShouldResetPilotPasswords(configuration))
                {
                    var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                    var resetResult = await userManager.ResetPasswordAsync(user, resetToken, password);
                    if (!resetResult.Succeeded)
                    {
                        logger.LogWarning(
                            "Unable to reset conglomerate pilot password for '{UserName}': {Errors}",
                            userName,
                            string.Join(", ", resetResult.Errors.Select(error => error.Description)));
                    }
                    else
                    {
                        logger.LogInformation("Reset conglomerate pilot password for '{UserName}'.", userName);
                    }
                }
            }

            var claims = await userManager.GetClaimsAsync(user);
            if (!claims.Any(claim =>
                    claim.Type == "tenant_membership" &&
                    string.Equals(claim.Value, tenantKey, StringComparison.OrdinalIgnoreCase)))
            {
                await userManager.AddClaimAsync(user, new Claim("tenant_membership", tenantKey));
            }

            if (!string.IsNullOrWhiteSpace(roleName))
            {
                if (!await userManager.IsInRoleAsync(user, roleName))
                    await userManager.AddToRoleAsync(user, roleName);
            }
            else if (await userManager.IsInRoleAsync(user, "Admin"))
            {
                await userManager.RemoveFromRoleAsync(user, "Admin");
                logger.LogInformation(
                    "Removed global Admin role from conglomerate pilot user '{UserName}'.",
                    userName);
            }
        }

        logger.LogInformation("Conglomerate pilot user seed completed for {PilotCount} entries.", pilots.Count);
    }

    private sealed record ConglomeratePilotSeed(
        string UserName,
        string Email,
        string TenantKey,
        string RoleName,
        string FirstName,
        string LastName);

    private static IReadOnlyList<ConglomeratePilotSeed> ResolveConglomeratePilotSeeds(
        IConfiguration configuration,
        ILogger logger)
    {
        var seedDocument = LoadConglomerateSeedDocument(configuration, logger);
        if (seedDocument is not null &&
            seedDocument.RootElement.TryGetProperty("pilotUsers", out var pilotsFromFile) &&
            pilotsFromFile.ValueKind == JsonValueKind.Array)
        {
            return pilotsFromFile.EnumerateArray()
                .Select(ParseConglomeratePilotSeed)
                .Where(pilot => pilot is not null)
                .Select(pilot => pilot!)
                .ToArray();
        }

        var pilotsFromConfig = configuration.GetSection("Conglomerate:PilotUsers").GetChildren().ToArray();
        if (pilotsFromConfig.Length == 0)
            return [];

        return pilotsFromConfig
            .Select(section => ParseConglomeratePilotSeed(section))
            .Where(pilot => pilot is not null)
            .Select(pilot => pilot!)
            .ToArray();
    }

    private static ConglomeratePilotSeed? ParseConglomeratePilotSeed(JsonElement pilot)
    {
        var userName = pilot.TryGetProperty("userName", out var userNameElement) ? userNameElement.GetString() : null;
        var email = pilot.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null;
        var tenantKey = pilot.TryGetProperty("tenantKey", out var tenantElement) ? tenantElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(tenantKey))
            return null;

        return new ConglomeratePilotSeed(
            userName,
            email,
            tenantKey,
            pilot.TryGetProperty("role", out var roleElement) ? roleElement.GetString() ?? "" : "",
            pilot.TryGetProperty("firstName", out var firstNameElement) ? firstNameElement.GetString() ?? "" : "",
            pilot.TryGetProperty("lastName", out var lastNameElement) ? lastNameElement.GetString() ?? "" : "");
    }

    private static ConglomeratePilotSeed? ParseConglomeratePilotSeed(IConfigurationSection section)
    {
        var userName = section["UserName"] ?? section["userName"];
        var email = section["Email"] ?? section["email"];
        var tenantKey = section["TenantKey"] ?? section["tenantKey"];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(tenantKey))
            return null;

        return new ConglomeratePilotSeed(
            userName,
            email,
            tenantKey,
            section["Role"] ?? section["role"] ?? "",
            section["FirstName"] ?? section["firstName"] ?? "",
            section["LastName"] ?? section["lastName"] ?? "");
    }

    private static string? ResolveConglomeratePilotPassword(
        IConfiguration configuration,
        IHostEnvironment? hostEnvironment,
        ILogger logger)
    {
        var password = configuration["Conglomerate:PilotUserPassword"]
            ?? configuration["IDENTITY_CONGLOMERATE_PILOT_PASSWORD"];
        if (!string.IsNullOrWhiteSpace(password))
            return password.Trim();

        if (hostEnvironment?.IsDevelopment() == true)
        {
            const string developmentFallbackPassword = "ConglomeratePilot@Dev1";
            logger.LogWarning(
                "Conglomerate:PilotUserPassword is not configured; using the Development fallback pilot password.");
            return developmentFallbackPassword;
        }

        logger.LogWarning("Skipping conglomerate pilot users because Conglomerate:PilotUserPassword is not configured.");
        return null;
    }

    private static JsonDocument? LoadConglomerateSeedDocument(IConfiguration configuration, ILogger logger)
    {
        var relativePath = configuration["Conglomerate:SeedDataPath"] ?? "config/conglomerate/seed-data.v1.json";
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, relativePath),
            Path.Combine(Directory.GetCurrentDirectory(), relativePath),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", relativePath))
        };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(candidate))
                continue;

            try
            {
                logger.LogInformation("Loading conglomerate seed document from '{Path}'.", candidate);
                return JsonDocument.Parse(File.ReadAllText(candidate));
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "Conglomerate seed document at '{Path}' is invalid JSON.", candidate);
                return null;
            }
        }

        logger.LogWarning("Conglomerate seed document not found at '{Path}'.", relativePath);
        return null;
    }

    private static async Task SeedControlPlaneSampleDataAsync(
        IdentityDbContext db,
        IConfiguration? configuration,
        UserManager<User> userManager,
        ILogger logger,
        CancellationToken ct)
    {
        if (configuration?.GetValue("Conglomerate:Enabled", false) == true)
        {
            await SeedConglomerateScopesAsync(db, configuration, logger, ct);
            if (configuration.GetValue("Conglomerate:SkipDemoHospitalScope", true))
            {
                logger.LogInformation("Conglomerate scopes seeded; IAM graph runs after pilot users.");
                return;
            }
        }

        // Stable sample graph used by the admin-app local/demo environment.
        // Every lookup is by a canonical key so rerunning startup remains idempotent.
        var actor = await db.Users.AsNoTracking().OrderBy(x => x.UserName).FirstOrDefaultAsync(ct);
        if (actor is null) return;
        var actorId = actor.Id;

        async Task<IamScope> Scope(string key, string name, string kind, Guid? parentId)
        {
            var item = await db.IamScopes.FirstOrDefaultAsync(x => x.Key == key && x.Kind == kind, ct);
            if (item is not null) return item;
            item = new IamScope { Key = key, DisplayName = name, Kind = kind, ParentId = parentId };
            db.IamScopes.Add(item); await db.SaveChangesAsync(ct); return item;
        }

        var organization = await Scope("his-hope", "His.Hope Healthcare", "organization", null);
        var tenant = await Scope("demo-hospital", "Demo Hospital", "tenant", organization.Id);
        var account = await Scope("clinical-account", "Clinical account", "account", tenant.Id);
        var environment = await Scope("local", "Local development", "environment", account.Id);

        var serviceDefinitions = new (string Key, string Name, string Prefix)[]
        {
            ("identity", "Identity Service", "identity"), ("patients", "Patient Service", "patients"),
            ("clinical", "Clinical Service", "clinical"), ("appointments", "Appointment Service", "appointments"),
            ("billing", "Billing Service", "billing"), ("pharmacy", "Pharmacy Service", "pharmacy"),
            ("lab", "Laboratory Service", "lab"),
            ("fhir", "FHIR Service", "fhir"),
            ("external-integration", "External Integration Service", "external"),
            ("database-continuity", "Database Continuity Service", "admin"),
            ("remediation", "Remediation Operator", "admin"),
            ("mobile", "Mobile Platform", "admin")
        };
        foreach (var definition in serviceDefinitions)
        {
            if (!await db.IamServiceDefinitions.AnyAsync(x => x.Key == definition.Key, ct))
                db.IamServiceDefinitions.Add(new IamServiceDefinition { Key = definition.Key, DisplayName = definition.Name, PermissionPrefix = definition.Prefix, Owner = "identity-service" });
        }
        await db.SaveChangesAsync(ct);

        // Publish an auditable template snapshot for each workforce role. The
        // ASP.NET Identity role remains the effective grant; this snapshot is
        // what the IAM workflow reviews and the admin-app renders.
        foreach (var role in await db.Roles.AsNoTracking().Where(x => x.Name != null).ToListAsync(ct))
        {
            if (!await db.RoleTemplateVersions.AnyAsync(x => x.RoleId == role.Id && x.Version == role.AuthorizationVersion, ct))
            {
                var rolePermissions = await db.RolePermissions.AsNoTracking()
                    .Where(x => x.RoleId == role.Id)
                    .Select(x => x.PermissionCode)
                    .ToArrayAsync(ct);
                db.RoleTemplateVersions.Add(new RoleTemplateVersion
                {
                    RoleId = role.Id,
                    Version = role.AuthorizationVersion,
                    Name = role.Name!,
                    Description = role.Description,
                    Owner = role.Owner,
                    RiskTier = role.RiskTier,
                    ReviewCadenceDays = role.ReviewCadenceDays,
                    LifecycleStatus = "published",
                    PermissionsJson = JsonSerializer.Serialize(rolePermissions),
                    CreatedBy = actorId.ToString(),
                    PublishedAt = role.PublishedAt ?? DateTime.UtcNow,
                    PublishedBy = actorId.ToString()
                });
            }
        }
        await db.SaveChangesAsync(ct);

        var permissionSets = new Dictionary<string, IamPermissionSet>(StringComparer.Ordinal);
        async Task<IamPermissionSet> PermissionSet(string key, string name, string[] permissions, string status = "published")
        {
            var item = await db.IamPermissionSets.FirstOrDefaultAsync(x => x.Key == key, ct);
            if (item is null)
            {
                item = new IamPermissionSet { Key = key, DisplayName = name, ScopeId = environment.Id, PermissionsJson = JsonSerializer.Serialize(permissions), LifecycleStatus = status, CreatedBy = actorId.ToString(), PublishedAt = status == "published" ? DateTime.UtcNow : null };
                db.IamPermissionSets.Add(item); await db.SaveChangesAsync(ct);
            }
            permissionSets[key] = item; return item;
        }

        var workforceSet = await PermissionSet("demo-clinical-admin", "Demo clinical administrator", new[] { "patients.view", "patients.update", "clinical.view", "clinical.update", "appointments.view" });
        var readOnlySet = await PermissionSet("demo-audit-readonly", "Demo audit read-only", new[] { "patients.view", "clinical.view", "audit.read" });
        var draftSet = await PermissionSet("demo-billing-draft", "Demo billing draft", new[] { "billing.view", "billing.create" }, "draft");

        if (!await db.IamPermissionSetAssignments.AnyAsync(x => x.PermissionSetId == workforceSet.Id && x.PrincipalId == actorId, ct))
            db.IamPermissionSetAssignments.Add(new IamPermissionSetAssignment { PermissionSetId = workforceSet.Id, PrincipalId = actorId, PrincipalType = "human", ScopeId = environment.Id, CreatedBy = actorId.ToString() });
        if (!await db.IamPermissionSetAssignments.AnyAsync(x => x.PermissionSetId == readOnlySet.Id && x.PrincipalId == actorId, ct))
            db.IamPermissionSetAssignments.Add(new IamPermissionSetAssignment { PermissionSetId = readOnlySet.Id, PrincipalId = actorId, PrincipalType = "human", ScopeId = tenant.Id, CreatedBy = actorId.ToString(), ExpiresAt = DateTime.UtcNow.AddDays(30) });

        var group = await db.IamGroups.FirstOrDefaultAsync(x => x.Key == "clinical-operators", ct);
        if (group is null) { group = new IamGroup { Key = "clinical-operators", DisplayName = "Clinical operators", ScopeId = tenant.Id, CreatedBy = actorId.ToString() }; db.IamGroups.Add(group); await db.SaveChangesAsync(ct); }
        if (!await db.IamGroupMemberships.AnyAsync(x => x.GroupId == group.Id && x.UserId == actorId, ct)) db.IamGroupMemberships.Add(new IamGroupMembership { GroupId = group.Id, UserId = actorId, CreatedBy = actorId.ToString() });
        if (!await db.IamPermissionSetAssignments.AnyAsync(x => x.PermissionSetId == readOnlySet.Id && x.PrincipalId == group.Id && x.PrincipalType == "group", ct))
            db.IamPermissionSetAssignments.Add(new IamPermissionSetAssignment { PermissionSetId = readOnlySet.Id, PrincipalId = group.Id, PrincipalType = "group", ScopeId = tenant.Id, CreatedBy = actorId.ToString(), ExpiresAt = DateTime.UtcNow.AddDays(30) });

        var workloadDefinitions = new (string Key, string Name, string Audience, string[] Permissions)[]
        {
            ("clinical-api-reader", "Clinical API reader", "clinical-service", new[] { "clinical.view", "patients.view" }),
            ("appointment-api-writer", "Appointment API writer", "appointment-service", new[] { "appointments.view", "appointments.update" }),
            ("lab-api-reader", "Laboratory API reader", "lab-service", new[] { "lab.view", "lab.result" }),
            ("billing-api-reader", "Billing API reader", "billing-service", new[] { "billing.view" }),
            ("pharmacy-api-reader", "Pharmacy API reader", "pharmacy-service", new[] { "pharmacy.view" })
        };
        foreach (var definition in workloadDefinitions)
        {
            if (await db.IamWorkloadRoles.AnyAsync(x => x.Key == definition.Key, ct)) continue;
            db.IamWorkloadRoles.Add(new IamWorkloadRole
            {
                Key = definition.Key,
                DisplayName = definition.Name,
                ScopeId = environment.Id,
                Audience = definition.Audience,
                TrustPolicyJson = JsonSerializer.Serialize(new { principals = new[] { definition.Audience }, conditions = new { environment = "local" } }),
                PermissionsJson = JsonSerializer.Serialize(definition.Permissions),
                MaxSessionSeconds = 900
            });
        }
        await db.SaveChangesAsync(ct);
        // Link every workload role to a permission envelope, boundary and
        // resource policy. This makes the demo graph representative of the
        // control-plane relationships rendered by the IAM menu.
        var workloadRoles = await db.IamWorkloadRoles.Where(x => x.ScopeId == environment.Id).ToListAsync(ct);
        foreach (var workloadRole in workloadRoles)
        {
            var permissions = workloadRole.PermissionsJson;
            if (!await db.IamPermissionSetAssignments.AnyAsync(x => x.PermissionSetId == readOnlySet.Id && x.PrincipalId == workloadRole.Id && x.PrincipalType == "workload", ct))
                db.IamPermissionSetAssignments.Add(new IamPermissionSetAssignment { PermissionSetId = readOnlySet.Id, PrincipalId = workloadRole.Id, PrincipalType = "workload", ScopeId = environment.Id, CreatedBy = actorId.ToString(), ExpiresAt = DateTime.UtcNow.AddDays(30) });

            if (!await db.IamPermissionBoundaries.AnyAsync(x => x.PrincipalId == workloadRole.Id && x.PrincipalType == "workload", ct))
                db.IamPermissionBoundaries.Add(new IamPermissionBoundary { PrincipalId = workloadRole.Id, PrincipalType = "workload", ScopeId = environment.Id, AllowedPermissionsJson = permissions, ResourceConstraintsJson = "{\"tenant\":\"demo-hospital\"}", CreatedBy = actorId.ToString() });

            var serviceKey = workloadRole.Audience switch
            {
                "appointment-service" => "appointments",
                "clinical-service" => "clinical",
                "lab-service" => "lab",
                "billing-service" => "billing",
                "pharmacy-service" => "pharmacy",
                _ when workloadRole.Audience.EndsWith("-service", StringComparison.Ordinal) => workloadRole.Audience[..^"-service".Length],
                _ => workloadRole.Audience
            };
            if (!await db.IamResourcePolicies.AnyAsync(x => x.ServiceKey == serviceKey && x.ResourcePattern == $"{serviceKey}/*", ct))
                db.IamResourcePolicies.Add(new IamResourcePolicy { ScopeId = environment.Id, ServiceKey = serviceKey, ResourcePattern = $"{serviceKey}/*", StatementsJson = $"[{{\"effect\":\"allow\",\"actions\":{permissions},\"principal\":\"{workloadRole.Key}\"}}]", LifecycleStatus = "published", PublishedAt = DateTime.UtcNow, CreatedBy = actorId.ToString() });
        }
        if (!await db.AuthorizationPolicies.AnyAsync(x => x.Key == "demo-clinical-hours", ct)) db.AuthorizationPolicies.Add(new AuthorizationPolicyDefinition { Key = "demo-clinical-hours", Description = "Clinical access during hospital hours", Owner = "identity-service", LifecycleStatus = "published", RulesJson = "{\"all\":[{\"attribute\":\"facility\",\"equals\":\"demo-hospital\"}]}", CreatedBy = actorId.ToString(), PublishedAt = DateTime.UtcNow, PublishedBy = actorId.ToString() });

        if (!await db.AccessRequests.AnyAsync(x => x.SubjectUserId == actorId && x.Reason == "Demo JIT access", ct)) db.AccessRequests.Add(new AccessRequest { SubjectUserId = actorId, RequestedBy = actorId.ToString(), RoleIdsJson = JsonSerializer.Serialize(new[] { workforceSet.Id.ToString() }), Reason = "Demo JIT access", Status = "pending", ExpiresAt = DateTime.UtcNow.AddHours(8) });
        if (!await db.AccessReviews.AnyAsync(x => x.SubjectUserId == actorId && x.RoleIdsJson.Contains(workforceSet.Id.ToString()), ct)) db.AccessReviews.Add(new AccessReview { SubjectUserId = actorId, Reviewer = actorId.ToString(), RoleIdsJson = JsonSerializer.Serialize(new[] { workforceSet.Id.ToString() }), Status = "pending", DueAt = DateTime.UtcNow.AddDays(30) });
        if (!await db.BreakGlassRequests.AnyAsync(x => x.SubjectUserId == actorId && x.Reason == "Demo break-glass", ct)) db.BreakGlassRequests.Add(new BreakGlassRequest { Id = Guid.NewGuid(), SubjectUserId = actorId, PermissionCode = "clinical.view", FacilityId = "demo-hospital", Reason = "Demo break-glass", RequestedBy = actorId.ToString(), Status = "pending", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        if (!await db.DevicePosturePolicies.AnyAsync(x => x.Id == "default", ct)) db.DevicePosturePolicies.Add(new DevicePosturePolicy { Id = "default", Mode = "observe", ProvidersJson = "[\"chrome-enterprise\",\"advanced-compliance\",\"windows-local-login\"]", RequiredSignalsJson = "[\"disk-encryption\",\"screen-lock\"]", Version = "1", UpdatedBy = actorId.ToString() });
        if (!await db.DevicePostureAssessments.AnyAsync(x => x.UserId == actorId && x.DeviceId == "demo-device", ct)) db.DevicePostureAssessments.Add(new DevicePostureAssessment { UserId = actorId, DeviceId = "demo-device", Provider = "advanced-compliance", EvidenceHash = "demo-evidence", SignalsJson = "{\"disk-encryption\":true,\"screen-lock\":true}", Decision = "observe", ExpiresAt = DateTime.UtcNow.AddMinutes(15), PolicyVersion = "1", CorrelationId = "seed-demo" });
        if (!await db.DirectoryProvisioningBindings.AnyAsync(x => x.Target == "scim" && x.ResourceId == actorId.ToString(), ct)) db.DirectoryProvisioningBindings.Add(new DirectoryProvisioningBinding { Target = "scim", ResourceType = "User", ResourceId = actorId.ToString(), ExternalId = "scim-demo-admin" });
        if (!await db.DirectoryProvisioningOutbox.AnyAsync(x => x.Target == "scim" && x.ResourceId == actorId.ToString(), ct)) db.DirectoryProvisioningOutbox.Add(new DirectoryProvisioningOutbox { Target = "scim", Operation = "update", ResourceType = "User", ResourceId = actorId.ToString(), PayloadJson = "{\"email\":\"admin@hishop.com\",\"groups\":[\"clinical-operators\"]}", CompletedAt = DateTime.UtcNow, Attempts = 1, ExternalId = "scim-demo-admin" });
        if (!await db.UserClientCertificates.AnyAsync(x => x.UserId == actorId && x.Thumbprint == "DEMO-MTLS-THUMBPRINT", ct)) db.UserClientCertificates.Add(new UserClientCertificate { UserId = actorId, Thumbprint = "DEMO-MTLS-THUMBPRINT", Subject = "CN=admin@hishop.com", NotAfter = DateTime.UtcNow.AddDays(90) });
        if (!await db.AuditLogs.AnyAsync(x => x.Source == "seed-demo" && x.ResourceType == "IamPermissionSet", ct)) db.AuditLogs.Add(new AuditLog { UserId = actorId.ToString(), UserName = actor.UserName, Action = "CREATE", ResourceType = "IamPermissionSet", ResourceId = workforceSet.Id.ToString(), Details = "Seeded demo clinical permission set", Outcome = "success", Source = "seed-demo", CorrelationId = "seed-demo-iam", Timestamp = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded idempotent IAM demo graph for organization {Organization}, tenant {Tenant}, environment {Environment}.", organization.Key, tenant.Key, environment.Key);
    }

    private static async Task MigrateWhenDatabaseReadyAsync(
        IMigrationRunner migrationRunner,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 12;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await migrationRunner.MigrateAsync(cancellationToken);
                return;
            }
            catch (PostgresException exception) when (exception.SqlState == "57P03" && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(5, attempt));
                logger.LogWarning(
                    exception,
                    "Identity database is still starting (attempt {Attempt}/{MaxAttempts}); retrying in {DelaySeconds}s.",
                    attempt,
                    maxAttempts,
                    delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (NpgsqlException exception) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(5, attempt));
                logger.LogWarning(
                    exception,
                    "Identity database connection is not ready (attempt {Attempt}/{MaxAttempts}); retrying in {DelaySeconds}s.",
                    attempt,
                    maxAttempts,
                    delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }

        // Preserve the original exception and startup failure semantics after the bounded retry window.
        await migrationRunner.MigrateAsync(cancellationToken);
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

    private static readonly HashSet<string> KnownOidcClientIds = new(StringComparer.Ordinal)
    {
        "his-hope-spa",
        "his-hope-dashboard",
        "his-hope-admin",
        "his-hope-mobile",
    };

    private static async Task SeedConglomerateScopesAsync(
        IdentityDbContext db,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken ct)
    {
        var orgKey = configuration["Conglomerate:Organization:Key"];
        var orgName = configuration["Conglomerate:Organization:DisplayName"];
        if (string.IsNullOrWhiteSpace(orgKey) || string.IsNullOrWhiteSpace(orgName))
        {
            throw new InvalidOperationException(
                "Conglomerate:Enabled requires Conglomerate:Organization:Key and DisplayName.");
        }

        var tenantSections = configuration.GetSection("Conglomerate:Tenants").GetChildren().ToArray();
        if (tenantSections.Length == 0)
        {
            throw new InvalidOperationException(
                "Conglomerate:Enabled requires at least one Conglomerate:Tenants entry.");
        }

        async Task<IamScope> Scope(string key, string name, string kind, Guid? parentId)
        {
            // Environment keys such as "staging" repeat under every account.
            // Resolve by parent scope as well so each tenant gets its own subtree.
            var item = await db.IamScopes.FirstOrDefaultAsync(
                x => x.Key == key && x.Kind == kind && x.ParentId == parentId,
                ct);
            if (item is not null) return item;
            item = new IamScope { Key = key, DisplayName = name, Kind = kind, ParentId = parentId };
            db.IamScopes.Add(item);
            await db.SaveChangesAsync(ct);
            return item;
        }

        var organization = await Scope(orgKey, orgName, "organization", null);
        foreach (var tenantSection in tenantSections)
        {
            var tenantKey = tenantSection["Key"];
            var tenantName = tenantSection["DisplayName"];
            if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(tenantName))
            {
                throw new InvalidOperationException("Each Conglomerate:Tenants entry requires Key and DisplayName.");
            }

            var tenant = await Scope(tenantKey, tenantName, "tenant", organization.Id);
            var accountKey = tenantSection["AccountKey"] ?? $"{tenantKey}-account";
            var accountName = tenantSection["AccountDisplayName"] ?? $"{tenantName} account";
            var account = await Scope(accountKey, accountName, "account", tenant.Id);
            // iam_scopes enforces UNIQUE(kind, key). Environment display names
            // repeat ("staging") but keys must be tenant-specific.
            var environmentKey = tenantSection["EnvironmentKey"] ?? $"{tenantKey}-staging";
            var environmentName = tenantSection["EnvironmentDisplayName"] ?? "Azure staging";
            await Scope(environmentKey, environmentName, "environment", account.Id);
        }

        logger.LogInformation(
            "Conglomerate IAM scopes seeded for organization '{OrganizationKey}' ({TenantCount} tenants).",
            orgKey,
            tenantSections.Length);
    }

    private static async Task SeedAdditionalConfiguredOidcClientsAsync(
        OpenIddict.Abstractions.IOpenIddictApplicationManager appManager,
        IReadOnlyDictionary<string, OidcClientUris> oidcClients,
        IConfiguration? configuration,
        ILogger logger,
        CancellationToken ct)
    {
        foreach (var (clientId, uris) in oidcClients)
        {
            if (KnownOidcClientIds.Contains(clientId)) continue;

            var displayName = configuration?[$"Conglomerate:OidcClientDisplayNames:{clientId}"] ?? clientId;
            var existing = await appManager.FindByClientIdAsync(clientId, ct);
            if (existing is null)
            {
                var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor
                {
                    ClientId = clientId,
                    ClientType = OpenIddict.Abstractions.OpenIddictConstants.ClientTypes.Public,
                    DisplayName = displayName,
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

                if (clientId is "tech-console" or "group-hq-admin")
                {
                    descriptor.Permissions.Add(
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "hishop:admin");
                }

                AddClientUris(descriptor, uris);
                await appManager.CreateAsync(descriptor, ct);
                logger.LogInformation("OIDC application '{ClientId}' created.", clientId);
            }
            else
            {
                await UpdateClientUrisAsync(appManager, existing, uris, ct);
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

    public static bool ShouldResetAdminPassword(IConfiguration? configuration)
        => configuration?.GetValue<bool>("Identity:BootstrapAdmin:ResetPassword") == true;

    private static bool ShouldResetPilotPasswords(IConfiguration? configuration)
        => configuration?.GetValue("Conglomerate:ResetPilotPasswords", false) == true;

    public sealed record AdminBootstrapConfiguration(string? Password, bool SkipUserSeed)
    {
        public const string DefaultUserName = "admin";
        public const string DefaultEmail = "admin@hishop.com";
    }

    public sealed record OidcClientUris(
        IReadOnlyList<Uri> RedirectUris,
        IReadOnlyList<Uri> PostLogoutRedirectUris);
}
