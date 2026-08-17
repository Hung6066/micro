using System.Reflection;
using His.Hope.IdentityService.Api.Endpoints;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class DirectoryProvisioningContractTests
{
    [Fact]
    public void Readiness_requires_https_endpoints_and_credentials()
    {
        var method = typeof(DirectoryProvisioningEndpoints).GetMethod("Readiness", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var disabled = method!.Invoke(null, ["scim", false, null, null, null])!;
        Assert.Equal("disabled", disabled.GetType().GetProperty("status")!.GetValue(disabled));

        var invalid = method.Invoke(null, ["scim", true, "http://scim.local", "https://token.local", "secret-ref"])!;
        Assert.Equal("configuration_missing", invalid.GetType().GetProperty("status")!.GetValue(invalid));

        var ready = method.Invoke(null, ["scim", true, "https://scim.local", "https://token.local", "secret-ref"])!;
        Assert.Equal("ready_for_dry_run", ready.GetType().GetProperty("status")!.GetValue(ready));
        Assert.Equal("scim.local", ready.GetType().GetProperty("endpointHost")!.GetValue(ready));
    }

    [Fact]
    public void Facility_allowlist_is_distinct_case_insensitively()
    {
        var method = typeof(DirectoryProvisioningEndpoints).GetMethod("GetAllowedFacilities", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var context = new His.Hope.IdentityService.Infrastructure.Facility.FacilityContext
        {
            FacilityId = "FAC-1",
            AuthorizedFacilities = ["fac-1", "FAC-2", "", "fac-2"]
        };

        var values = (string[])method!.Invoke(null, [context])!;
        Assert.Equal(new[] { "fac-1", "FAC-2" }, values);
    }
}
