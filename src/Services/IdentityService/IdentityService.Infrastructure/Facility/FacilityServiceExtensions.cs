using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.IdentityService.Infrastructure.Facility;

/// <summary>
/// Extension methods for registering facility boundary services and authorization policies.
/// </summary>
public static class FacilityServiceExtensions
{
    /// <summary>
    /// Registers facility boundary services: FacilityContext (scoped), authorization handler, policies.
    /// </summary>
    public static IServiceCollection AddFacilityBoundary(this IServiceCollection services)
    {
        // Scoped facility context — one per request
        services.AddScoped<FacilityContext>();
        services.AddHttpContextAccessor();

        // Authorization handler (singleton — resolves FacilityContext via IHttpContextAccessor)
        services.AddSingleton<IAuthorizationHandler, FacilityAuthorizationHandler>();

        // Authorization policies
        services.AddAuthorization(options =>
        {
            // Basic facility policy — user must have a facility assigned
            options.AddPolicy("Facility", policy =>
                policy.Requirements.Add(new FacilityRequirement(strictMode: false)));

            // Strict facility policy — cross-facility users also need explicit match
            options.AddPolicy("Facility:Strict", policy =>
                policy.Requirements.Add(new FacilityRequirement(strictMode: true)));
        });

        return services;
    }
}
