using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Domain.Entities;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class SupportElevationPermissionsTests
{
    [Fact]
    public void Empty_permission_document_allows_any_action()
    {
        var elevation = new SupportElevation { PermissionsJson = "[]" };
        Assert.True(SupportElevationPermissions.Allows(elevation, "admin.users.write"));
    }

    [Fact]
    public void Scoped_permission_document_is_case_insensitive_and_denies_other_actions()
    {
        var elevation = new SupportElevation { PermissionsJson = "[\"admin.users.write\"]" };
        Assert.True(SupportElevationPermissions.Allows(elevation, "ADMIN.USERS.WRITE"));
        Assert.False(SupportElevationPermissions.Allows(elevation, "admin.roles.write"));
    }
}
