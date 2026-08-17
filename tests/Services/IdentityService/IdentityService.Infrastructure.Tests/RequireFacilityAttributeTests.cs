using His.Hope.IdentityService.Infrastructure.Facility;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class RequireFacilityAttributeTests
{
    [Fact]
    public void Default_policy_requires_any_facility()
    {
        var attribute = new RequireFacilityAttribute();

        Assert.Equal("Facility", attribute.Policy);
        Assert.False(attribute.Strict);
    }

    [Fact]
    public void Strict_constructor_requires_explicit_facility_match()
    {
        var attribute = new RequireFacilityAttribute(strict: true);

        Assert.Equal("Facility:Strict", attribute.Policy);
        Assert.True(attribute.Strict);
    }

    [Fact]
    public void Strict_init_property_can_be_relaxed()
    {
        var attribute = new RequireFacilityAttribute(strict: true) { Strict = false };

        Assert.Equal("Facility", attribute.Policy);
        Assert.False(attribute.Strict);
    }
}
