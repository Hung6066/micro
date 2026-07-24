using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.IdentityService.Infrastructure.OidcStores;

/// <summary>
/// OpenIddict EF Core store configuration helpers.
/// Custom audit-enriched stores will be added in a future iteration.
/// </summary>
public static class OpenIddictStoreConfiguration
{
    /// <summary>
    /// Configures OpenIddict Core to use EF Core with the IdentityDbContext.
    /// </summary>
    public static void ConfigureEntityFrameworkCoreStores(
        IServiceCollection services)
    {
        // OpenIddict EF Core stores are configured via AddOpenIddict().AddCore() in Program.cs
        // This helper exists for future custom store overrides.
    }
}
