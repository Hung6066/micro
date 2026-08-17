using FluentAssertions;
using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Domain.Tests;

public sealed class ControlPlaneEntityDefaultsTests
{
    [Fact]
    public void New_control_plane_entities_are_safe_and_auditable_by_default()
    {
        var accessRequest = new AccessRequest();
        accessRequest.Status.Should().Be("pending");
        accessRequest.RoleIdsJson.Should().Be("[]");
        accessRequest.Id.Should().NotBeEmpty();
        accessRequest.ExpiresAt.Should().BeAfter(accessRequest.RequestedAt);

        var accessReview = new AccessReview();
        accessReview.Status.Should().Be("pending");
        accessReview.RoleIdsJson.Should().Be("[]");
        accessReview.DueAt.Should().BeAfter(accessReview.CreatedAt);

        var policy = new AuthorizationPolicyDefinition();
        policy.Owner.Should().Be("identity-service");
        policy.Version.Should().Be(1);
        policy.LifecycleStatus.Should().Be("draft");
        policy.RulesJson.Should().Be("{}");

        var devicePolicy = new DevicePosturePolicy();
        devicePolicy.Id.Should().Be("default");
        devicePolicy.Mode.Should().Be("observe");
        devicePolicy.EvidenceTtlSeconds.Should().Be(900);
        devicePolicy.ProvidersJson.Should().Contain("chrome-enterprise");

        var posture = new DevicePostureAssessment();
        posture.Id.Should().NotBeEmpty();
        posture.PolicyVersion.Should().Be("1");
        posture.Decision.Should().Be("observe");

        var binding = new DirectoryProvisioningBinding();
        binding.Id.Should().NotBeEmpty();
        binding.CreatedAt.Should().BeOnOrBefore(binding.UpdatedAt);

        var outbox = new DirectoryProvisioningOutbox();
        outbox.Id.Should().NotBeEmpty();
        outbox.PayloadJson.Should().Be("{}");
        outbox.AvailableAt.Should().BeOnOrAfter(outbox.CreatedAt);
        outbox.Attempts.Should().Be(0);

        var consent = new ClientConsent();
        consent.IsActive.Should().BeTrue();
        consent.Scopes.Should().BeEmpty();

        var breakGlass = new BreakGlassRequest();
        breakGlass.Status.Should().Be("pending");
        breakGlass.PermissionCode.Should().BeEmpty();

        var scope = new IamScope();
        scope.Kind.Should().Be("tenant");
        scope.IsActive.Should().BeTrue();

        var workload = new IamWorkloadRole();
        workload.TrustPolicyJson.Should().Be("{}");
        workload.PermissionsJson.Should().Be("[]");
        workload.MaxSessionSeconds.Should().Be(900);
        workload.IsActive.Should().BeTrue();

        var boundary = new IamPermissionBoundary();
        boundary.AllowedPermissionsJson.Should().Be("[]");
        boundary.ResourceConstraintsJson.Should().Be("{}");
        boundary.IsActive.Should().BeTrue();
    }
}
