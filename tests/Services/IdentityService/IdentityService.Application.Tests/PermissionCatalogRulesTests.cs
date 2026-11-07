using His.Hope.IdentityService.Application.Authorization;
using Xunit;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class PermissionCatalogRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Empty_permission_codes_are_rejected(string? code) =>
        Assert.False(PermissionCatalogRules.IsValid(code, ["patients"]));

    [Fact]
    public void Registered_builtin_permissions_are_accepted_without_a_prefix_registration()
    {
        Assert.True(PermissionCatalogRules.IsValid("admin.users.read", []));
    }

    [Theory]
    [InlineData("patients")]
    [InlineData("patients.")]
    [InlineData("patients.view!")]
    [InlineData("patients.View")]
    public void Dynamic_permissions_require_lowercase_safe_segments(string code)
    {
        Assert.False(PermissionCatalogRules.IsValid(code, ["patients"]));
    }

    [Fact]
    public void Dynamic_permission_requires_a_registered_prefix_case_insensitively()
    {
        Assert.True(PermissionCatalogRules.IsValid("patients.view", ["PATIENTS"]));
        Assert.False(PermissionCatalogRules.IsValid("unregistered.view", ["patients"]));
    }
}
