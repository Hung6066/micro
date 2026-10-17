using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using His.Hope.SharedKernel.Authorization;
using His.Hope.SharedKernel.Protocol;
using His.Hope.Authorization.Handlers;
using His.Hope.Authorization.Requirements;

namespace His.Hope.Authorization;

public static class AuthorizationPoliciesExtensions
{
    public static IServiceCollection AddHisHopeAuthorization(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddHttpClient<OpenFgaClient>((serviceProvider, client) =>
        {
            var url = serviceProvider.GetService<Microsoft.Extensions.Configuration.IConfiguration>()?["AUTHZ_OPENFGA_URL"];
            if (Uri.TryCreate(url, UriKind.Absolute, out var baseAddress)) client.BaseAddress = new Uri(baseAddress.ToString().TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMilliseconds(500);
        });
        services.AddSingleton<IOpenFgaClient>(serviceProvider => serviceProvider.GetRequiredService<OpenFgaClient>());
        services.AddSingleton<OpenFgaCanaryAuthorizer>();
        services.AddSingleton<IAuthorizationDecisionSink, LoggingAuthorizationDecisionSink>();
        services.AddSingleton<IAuthorizationShadowProbe, LoggingAuthorizationShadowProbe>();
        services.AddSingleton<ICrossTenantAccessPolicy, DefaultDenyCrossTenantAccessPolicy>();
        services.AddScoped<IResourceAuthorizationEvaluator, AuthorizationEvaluator>();

        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        services.AddSingleton<IAuthorizationHandler, ScopeHandler>();
        services.AddSingleton<IAuthorizationHandler, PortalClassHandler>();
        services.AddSingleton<IAuthorizationHandler, CommerceScopeOrPermissionHandler>();
        services.AddSingleton<IAuthorizationHandler, PrincipalTypeHandler>();
        var builder = services.AddAuthorizationBuilder();
        builder.AddFallbackPolicy("default", new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());

        // Administrative APIs are human-operated surfaces.  Workload tokens
        // must use purpose-built integration policies (for example SCIM or
        // Continuity) rather than inheriting interactive admin permissions.
        builder.AddPolicy(AuthorizationConstants.Policies.HumanAdmin, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PrincipalTypeRequirement(AuthorizationConstants.PrincipalTypes.Human)));

        // The bootstrap/platform administrator is a separate trust tier. Keep
        // this policy role-based so ordinary tenant operators cannot reach
        // Identity control-plane surfaces even when they are human principals.
        builder.AddPolicy(AuthorizationConstants.Policies.HumanSuperAdmin, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole("Admin")
            .RequireClaim(HisHopeProtocolConstants.Claims.SuperAdmin, "true")
            .AddRequirements(new PrincipalTypeRequirement(AuthorizationConstants.PrincipalTypes.Human)));

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
