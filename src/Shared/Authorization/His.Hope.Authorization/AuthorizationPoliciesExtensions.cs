using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using His.Hope.SharedKernel.Authorization;
using His.Hope.Authorization.Handlers;
using His.Hope.Authorization.Requirements;

namespace His.Hope.Authorization;

public static class AuthorizationPoliciesExtensions
{
    public static IServiceCollection AddHisHopeAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        var builder = services.AddAuthorizationBuilder();
        builder.AddFallbackPolicy("default", new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());

        foreach (var permissionCode in HisHopePermissions.All)
        {
            var code = permissionCode;
            builder.AddPolicy($"Permission:{code}", policy =>
                policy.AddRequirements(new PermissionRequirement(code)));
        }

        foreach (var role in new[] { "Admin", "Provider", "Nurse", "Receptionist", "LabTechnician", "Pharmacist", "BillingClerk" })
        {
            builder.AddPolicy($"RequireRole:{role}", policy => policy.RequireRole(role));
        }

        return services;
    }
}
