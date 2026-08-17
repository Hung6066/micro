using His.Hope.IdentityService.Infrastructure.OidcStores;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class OpenIddictStoreConfigurationTests
{
    [Fact]
    public void ConfigureEntityFrameworkCoreStores_keeps_service_collection_unchanged()
    {
        var services = new ServiceCollection();
        services.AddSingleton<object>();
        var before = services.Count;

        OpenIddictStoreConfiguration.ConfigureEntityFrameworkCoreStores(services);

        Assert.Equal(before, services.Count);
    }
}
