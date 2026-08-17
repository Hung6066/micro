using His.Hope.IdentityService.Domain.Entities;

namespace IdentityService.Domain.Tests;

public sealed class IamControlPlaneTests
{
    [Fact]
    public void PermissionSet_defaults_to_draft_version_one_and_empty_policy()
    {
        var set = new IamPermissionSet();

        Assert.Equal(1, set.Version);
        Assert.Equal("draft", set.LifecycleStatus);
        Assert.Equal("[]", set.PermissionsJson);
    }

    [Fact]
    public void Assignment_defaults_to_human_active_principal()
    {
        var assignment = new IamPermissionSetAssignment();

        Assert.Equal("human", assignment.PrincipalType);
        Assert.Equal("active", assignment.Status);
        Assert.Null(assignment.ExpiresAt);
    }

    [Fact]
    public void Workload_role_is_separate_and_short_lived_by_default()
    {
        var role = new IamWorkloadRole();

        Assert.Equal(900, role.MaxSessionSeconds);
        Assert.Equal("{}", role.TrustPolicyJson);
        Assert.Equal("[]", role.PermissionsJson);
    }
}
