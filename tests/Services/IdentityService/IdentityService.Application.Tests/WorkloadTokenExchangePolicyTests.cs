using System.Text.Json;
using FluentAssertions;
using His.Hope.IdentityService.Application.OpenIddict;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class WorkloadTokenExchangePolicyTests
{
    [Fact]
    public void TrustPolicy_requires_exact_actor_match()
    {
        using var document = JsonDocument.Parse("{\"principals\":[\"svc-a\",\"svc-b\"]}");

        WorkloadTokenExchangePolicy.TrustsActor(document, "svc-a").Should().BeTrue();
        WorkloadTokenExchangePolicy.TrustsActor(document, "svc-a-extra").Should().BeFalse();
    }

    [Fact]
    public void Permission_intersection_cannot_expand_role_permissions()
    {
        var requested = new HashSet<string>(new[] { "patients.view", "patients.export", "billing.view" }, StringComparer.Ordinal);

        WorkloadTokenExchangePolicy.IntersectPermissions(new[] { "patients.view", "billing.view" }, requested)
            .Should().Equal("patients.view", "billing.view");
    }

    [Fact]
    public void Empty_requested_permissions_preserves_server_role_set()
    {
        WorkloadTokenExchangePolicy.IntersectPermissions(new[] { "patients.view", "patients.view", "" }, new HashSet<string>())
            .Should().Equal("patients.view");
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"principals\":\"svc-a\"}")]
    [InlineData("{\"other\":[]}")]
    public void TrustPolicy_fails_closed_for_malformed_or_wrong_shape(string json)
    {
        WorkloadTokenExchangePolicy.TrustsActor(json, "svc-a").Should().BeFalse();
    }

    [Fact]
    public void TrustPolicy_requires_exact_case_sensitive_actor_match()
    {
        WorkloadTokenExchangePolicy.TrustsActor("{\"principals\":[\"svc-a\"]}", "SVC-A")
            .Should().BeFalse();
    }

    [Fact]
    public void Permission_intersection_returns_empty_when_requested_set_has_no_overlap()
    {
        WorkloadTokenExchangePolicy.IntersectPermissions(
                new[] { "patients.view", "billing.view" },
                new HashSet<string>(new[] { "admin.root" }, StringComparer.Ordinal))
            .Should().BeEmpty();
    }
}
