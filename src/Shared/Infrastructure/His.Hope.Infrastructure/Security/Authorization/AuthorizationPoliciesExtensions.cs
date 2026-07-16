using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using His.Hope.Infrastructure.Security.Authorization.Requirements;
using His.Hope.Infrastructure.Security.Authorization.Handlers;
using His.Hope.SharedKernel.Authorization;

namespace His.Hope.Infrastructure.Security.Authorization;

/// <summary>
/// Extension methods for registering His.Hope's permission-based authorization policies.
///
/// Usage in Program.cs:
/// <code>
/// builder.Services.AddHisHopeAuthorization();
/// </code>
///
/// This replaces the standard AddAuthorization() call and registers:
/// - Individual permission policies (e.g., "Permission:patients.view")
/// - Role-based policies (e.g., "RequireRole:Admin")
/// - The PermissionHandler that evaluates permission claims from JWT tokens
/// </summary>
public static class AuthorizationPoliciesExtensions
{
    /// <summary>
    /// Adds His.Hope's permission-based authorization policies to the service collection.
    /// Registers a policy for every known permission code in the system
    /// under the naming convention "Permission:{code}".
    /// </summary>
    public static IServiceCollection AddHisHopeAuthorization(this IServiceCollection services)
    {
        // Register the handler for PermissionRequirement
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

        // Build the authorization policy registry using the .NET 8 AddAuthorizationBuilder
        // pattern. All policies are registered in a single builder chain.
        var builder = services.AddAuthorizationBuilder();

        // Add fallback policy requiring authenticated user
        builder.AddFallbackPolicy("default", new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());

        // Register individual permission policies
        foreach (var permissionCode in HisHopePermissions.All)
        {
            // Capture variable for closure
            var code = permissionCode;
            builder.AddPolicy($"Permission:{code}", policy =>
            {
                policy.AddRequirements(new PermissionRequirement(code));
            });
        }

        // Register convenience role policies
        builder
            .AddPolicy("RequireRole:Admin", policy => policy.RequireRole("Admin"))
            .AddPolicy("RequireRole:Provider", policy => policy.RequireRole("Provider"))
            .AddPolicy("RequireRole:Nurse", policy => policy.RequireRole("Nurse"))
            .AddPolicy("RequireRole:Receptionist", policy => policy.RequireRole("Receptionist"))
            .AddPolicy("RequireRole:LabTechnician", policy => policy.RequireRole("LabTechnician"))
            .AddPolicy("RequireRole:Pharmacist", policy => policy.RequireRole("Pharmacist"))
            .AddPolicy("RequireRole:BillingClerk", policy => policy.RequireRole("BillingClerk"));

        return services;
    }
}
