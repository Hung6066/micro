using FluentAssertions;
using His.Hope.IdentityService.Application.Authorization;

namespace His.Hope.IdentityService.Application.Tests;

public class RoleGovernanceRulesTests
{
    [Fact]
    public void NormalizePermissionCodes_removes_blanks_and_duplicates_case_insensitively()
    {
        var result = RoleGovernanceRules.NormalizePermissionCodes([" clinical.read ", "clinical.read", "", "billing.view"]);

        result.Should().Equal("clinical.read", "billing.view");
    }

    [Fact]
    public void FindPermissionOutsideScope_rejects_permission_not_held_by_delegated_admin()
    {
        var result = RoleGovernanceRules.FindPermissionOutsideScope(
            ["clinical.read", "clinical.sign"],
            ["clinical.read"],
            unrestricted: false);

        result.Should().Be("clinical.sign");
    }

    [Fact]
    public void FindPermissionOutsideScope_allows_all_permissions_for_permission_admin()
    {
        var result = RoleGovernanceRules.FindPermissionOutsideScope(
            ["clinical.sign", "billing.void"],
            [],
            unrestricted: true);

        result.Should().BeNull();
    }

    [Fact]
    public void IsFacilityScopeAllowed_requires_target_facilities_to_be_owned_by_actor()
    {
        RoleGovernanceRules.IsFacilityScopeAllowed(["facility-a"], ["facility-a"], crossFacility: false)
            .Should().BeTrue();
        RoleGovernanceRules.IsFacilityScopeAllowed(["facility-b"], ["facility-a"], crossFacility: false)
            .Should().BeFalse();
        RoleGovernanceRules.IsFacilityScopeAllowed(["facility-b"], [], crossFacility: true)
            .Should().BeTrue();
    }

    [Fact]
    public void NormalizePermissionCodes_accepts_null_as_empty_and_trims_case_variants()
    {
        RoleGovernanceRules.NormalizePermissionCodes(null).Should().BeEmpty();
        RoleGovernanceRules.NormalizePermissionCodes([" READ ", "read", " "]).Should().Equal("READ");
    }

    [Fact]
    public void FindPermissionOutsideScope_matches_actor_permissions_case_insensitively()
    {
        RoleGovernanceRules.FindPermissionOutsideScope(["Clinical.Read"], ["clinical.read"], unrestricted: false)
            .Should().BeNull();
    }

    [Fact]
    public void Facility_scope_ignores_blank_facility_values()
    {
        RoleGovernanceRules.IsFacilityScopeAllowed([" ", "facility-a"], ["facility-a"], crossFacility: false)
            .Should().BeTrue();
        RoleGovernanceRules.IsFacilityScopeAllowed([" ", "facility-b"], ["facility-a"], crossFacility: false)
            .Should().BeFalse();
    }
}
