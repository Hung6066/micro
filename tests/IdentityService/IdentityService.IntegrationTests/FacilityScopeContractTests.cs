using System.Reflection;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Infrastructure.Facility;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class FacilityScopeContractTests
{
    [Theory]
    [InlineData(typeof(DevicePostureEndpoints))]
    [InlineData(typeof(MtlsEndpoints))]
    [InlineData(typeof(DirectoryProvisioningEndpoints))]
    public void SingleFacilityClaimIsIncludedInAllowedScope(Type endpointType)
    {
        var helper = endpointType.GetMethod("GetAllowedFacilities", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(helper);

        var context = new FacilityContext { FacilityId = "facility-a" };
        var result = Assert.IsType<string[]>(helper!.Invoke(null, [context]));

        Assert.Single(result);
        Assert.Equal("facility-a", result[0]);
    }

    [Fact]
    public void MultiFacilityScopeIsDeduplicatedCaseInsensitively()
    {
        var helper = typeof(DevicePostureEndpoints).GetMethod("GetAllowedFacilities", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(helper);

        var context = new FacilityContext
        {
            FacilityId = "facility-a",
            AuthorizedFacilities = ["FACILITY-A", "facility-b"]
        };
        var result = Assert.IsType<string[]>(helper!.Invoke(null, [context]));

        Assert.Equal(2, result.Length);
        Assert.Contains(result, value => value.Equals("facility-a", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result, value => value.Equals("facility-b", StringComparison.OrdinalIgnoreCase));
    }
}
