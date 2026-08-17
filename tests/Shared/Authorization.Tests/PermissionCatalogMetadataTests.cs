using FluentAssertions;
using His.Hope.SharedKernel.Authorization;
using Xunit;

namespace His.Hope.Authorization.Tests;

public sealed class PermissionCatalogMetadataTests
{
    [Fact]
    public void Every_permission_descriptor_exposes_governance_metadata()
    {
        HisHopePermissions.AllDescriptors.Should().NotBeEmpty();
        HisHopePermissions.AllDescriptors.Should().OnlyContain(permission =>
            !string.IsNullOrWhiteSpace(permission.Owner)
            && permission.Version > 0
            && !string.IsNullOrWhiteSpace(permission.RiskTier)
            && !string.IsNullOrWhiteSpace(permission.RequiredAssurance)
            && !string.IsNullOrWhiteSpace(permission.AuditClass));
    }

    [Fact]
    public void Credential_and_privileged_actions_require_high_assurance()
    {
        var highRisk = HisHopePermissions.AllDescriptors
            .Where(permission => permission.Code.Contains("credential", StringComparison.OrdinalIgnoreCase)
                || permission.Code.Contains("break", StringComparison.OrdinalIgnoreCase)
                || permission.Code.EndsWith(".delete", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        highRisk.Should().NotBeEmpty();
        highRisk.Should().OnlyContain(permission =>
            permission.RiskTier == "high" && permission.RequiredAssurance == "mfa");
    }
}
