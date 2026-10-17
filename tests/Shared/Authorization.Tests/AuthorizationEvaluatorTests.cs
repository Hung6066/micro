using System.Security.Claims;
using FluentAssertions;
using His.Hope.Authorization;
using Xunit;

namespace His.Hope.Authorization.Tests;

public sealed class AuthorizationEvaluatorTests
{
    [Fact]
    public async Task Denies_when_resource_is_required_but_missing()
    {
        var evaluator = new AuthorizationEvaluator();
        var decision = await evaluator.EvaluateAsync(new AuthorizationContext(
            Principal("patients.view", "facility-a"), "patients.view", RequireResource: true));

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("resource_required");
    }

    [Fact]
    public async Task Denies_resource_outside_facility_scope()
    {
        var evaluator = new AuthorizationEvaluator();
        var decision = await evaluator.EvaluateAsync(new AuthorizationContext(
            Principal("patients.view", "facility-a"),
            "patients.view",
            new AuthorizationResource("patient", "patient-2", FacilityId: "facility-b"),
            RequireResource: true));

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("facility_scope_denied");
    }

    [Fact]
    public async Task Allows_permission_and_matching_facility()
    {
        var evaluator = new AuthorizationEvaluator();
        var decision = await evaluator.EvaluateAsync(new AuthorizationContext(
            Principal("patients.view", "facility-a"),
            "patients.view",
            new AuthorizationResource("patient", "patient-1", FacilityId: "facility-a"),
            RequireResource: true));

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Denies_missing_permission_even_when_facility_matches()
    {
        var evaluator = new AuthorizationEvaluator();
        var decision = await evaluator.EvaluateAsync(new AuthorizationContext(
            Principal("patients.view", "facility-a"),
            "patients.update",
            new AuthorizationResource("patient", "patient-1", FacilityId: "facility-a"),
            RequireResource: true));

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("permission_missing");
    }

    [Fact]
    public async Task Denies_resource_outside_issued_permission_boundary_constraints()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "workload-1"),
            new Claim("permissions", "patients.view"),
            new Claim("facility_id", "facility-a"),
            new Claim("tenant_id", "tenant-a"),
            new Claim("authorization_constraints", "{\"tenant\":\"tenant-b\"}")
        ], "test");
        var evaluator = new AuthorizationEvaluator();

        var decision = await evaluator.EvaluateAsync(new AuthorizationContext(
            new ClaimsPrincipal(identity),
            "patients.view",
            new AuthorizationResource("patient", "patient-1", FacilityId: "facility-a"),
            RequireResource: true));

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("resource_constraint_denied");
    }

    [Fact]
    public async Task Denies_malformed_issued_permission_boundary_constraints()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "workload-1"),
            new Claim("permissions", "patients.view"),
            new Claim("facility_id", "facility-a"),
            new Claim("authorization_constraints", "not-json")
        ], "test");
        var evaluator = new AuthorizationEvaluator();

        var decision = await evaluator.EvaluateAsync(new AuthorizationContext(
            new ClaimsPrincipal(identity),
            "patients.view",
            new AuthorizationResource("patient", "patient-1", FacilityId: "facility-a"),
            RequireResource: true));

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("resource_constraint_invalid");
    }

    [Fact]
    public async Task Applies_published_resource_policy_deny_before_allow()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "workload-1"),
            new Claim("permissions", "patients.view"),
            new Claim("facility_id", "facility-a"),
            new Claim("resource_policies", "[{\"ServiceKey\":\"patients\",\"ResourcePattern\":\"patient/*\",\"Effect\":\"deny\",\"Actions\":[\"patients.view\"]}]")
        ], "test");
        var evaluator = new AuthorizationEvaluator();

        var decision = await evaluator.EvaluateAsync(new AuthorizationContext(
            new ClaimsPrincipal(identity),
            "patients.view",
            new AuthorizationResource("patient", "patient-1", FacilityId: "facility-a"),
            RequireResource: true));

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("resource_policy_denied");
    }

    [Fact]
    public async Task Allows_matching_published_resource_policy()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "workload-1"),
            new Claim("permissions", "patients.view"),
            new Claim("facility_id", "facility-a"),
            new Claim("resource_policies", "[{\"ServiceKey\":\"patients\",\"ResourcePattern\":\"patient/*\",\"Effect\":\"allow\",\"Actions\":[\"patients.view\"]}]")
        ], "test");
        var evaluator = new AuthorizationEvaluator();

        var decision = await evaluator.EvaluateAsync(new AuthorizationContext(
            new ClaimsPrincipal(identity),
            "patients.view",
            new AuthorizationResource("patient", "patient-1", FacilityId: "facility-a"),
            RequireResource: true));

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Ignores_resource_policy_when_string_equals_condition_does_not_match()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "workload-1"),
            new Claim("tenant_id", "tenant-a"),
            new Claim("permissions", "patients.view"),
            new Claim("resource_policies", "[{\"ServiceKey\":\"patients\",\"ResourcePattern\":\"patient/*\",\"Effect\":\"allow\",\"Actions\":[\"patients.view\"],\"Condition\":{\"StringEquals\":{\"tenant_id\":\"tenant-b\"}}}]")
        ], "test");

        var decision = await new AuthorizationEvaluator().EvaluateAsync(new AuthorizationContext(
            new ClaimsPrincipal(identity), "patients.view",
            new AuthorizationResource("patient", "patient-1", TenantId: "tenant-a"), RequireResource: true));

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Allows_resource_policy_when_string_equals_condition_matches()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "workload-1"),
            new Claim("tenant_id", "tenant-a"),
            new Claim("permissions", "patients.view"),
            new Claim("resource_policies", "[{\"ServiceKey\":\"patients\",\"ResourcePattern\":\"patient/*\",\"Effect\":\"allow\",\"Actions\":[\"patients.view\"],\"Condition\":{\"StringEquals\":{\"tenant_id\":\"tenant-a\"}}}]")
        ], "test");

        var decision = await new AuthorizationEvaluator().EvaluateAsync(new AuthorizationContext(
            new ClaimsPrincipal(identity), "patients.view",
            new AuthorizationResource("patient", "patient-1", TenantId: "tenant-a"), RequireResource: true));

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Supports_case_insensitive_string_like_condition()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "workload-1"),
            new Claim("tenant_id", "tenant-a"),
            new Claim("permissions", "patients.view"),
            new Claim("resource_policies", "[{\"serviceKey\":\"patients\",\"resourcePattern\":\"patient/*\",\"effect\":\"allow\",\"actions\":[\"patients.view\"],\"condition\":{\"StringLike\":{\"tenant_id\":\"tenant-*\"}}}]")
        ], "test");

        var decision = await new AuthorizationEvaluator().EvaluateAsync(new AuthorizationContext(
            new ClaimsPrincipal(identity), "patients.view",
            new AuthorizationResource("patient", "patient-1", TenantId: "tenant-a"), RequireResource: true));

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Supports_numeric_condition_operator()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "workload-1"),
            new Claim("permissions", "patients.view"),
            new Claim("risk_score", "7"),
            new Claim("resource_policies", "[{\"ServiceKey\":\"patients\",\"ResourcePattern\":\"patient/*\",\"Effect\":\"allow\",\"Actions\":[\"patients.view\"],\"Condition\":{\"NumericGreaterThanEquals\":{\"risk_score\":\"5\"}}}]")
        ], "test");

        var decision = await new AuthorizationEvaluator().EvaluateAsync(new AuthorizationContext(
            new ClaimsPrincipal(identity), "patients.view",
            new AuthorizationResource("patient", "patient-1"), RequireResource: true));

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Emits_redacted_decision_metadata_to_sink()
    {
        var sink = new RecordingSink();
        var evaluator = new AuthorizationEvaluator(sink);

        await evaluator.EvaluateAsync(new AuthorizationContext(
            Principal("patients.view", "facility-a", "tenant-a"),
            "patients.view",
            new AuthorizationResource("patient", "patient-1", TenantId: "tenant-a", FacilityId: "facility-a"),
            RequireResource: true));

        sink.Audits.Should().ContainSingle();
        sink.Audits[0].Decision.Allowed.Should().BeTrue();
        sink.Audits[0].Decision.ResourceType.Should().Be("patient");
        sink.Audits[0].Decision.Action.Should().Be("patients.view");
    }

    [Fact]
    public async Task Audits_resource_lookup_failure_as_denial()
    {
        var sink = new RecordingSink();
        var evaluator = new AuthorizationEvaluator(sink);

        var decision = await evaluator.EvaluateAsync(new AuthorizationContext(
            Principal("patients.view", "facility-a"),
            "patients.view",
            new AuthorizationResource("patient", "patient-missing"),
            RequireResource: true,
            ResourceLookupFailed: true));

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("resource_not_found");
        sink.Audits.Should().ContainSingle(audit => audit.Decision.ReasonCode == "resource_not_found");
    }

    [Fact]
    public async Task Shadow_probe_is_advisory_and_cannot_change_local_decision()
    {
        var probe = new RecordingProbe();
        var evaluator = new AuthorizationEvaluator(shadowProbe: probe);

        var decision = await evaluator.EvaluateAsync(new AuthorizationContext(
            Principal("patients.view", "facility-a"),
            "patients.view",
            new AuthorizationResource("patient", "patient-2", FacilityId: "facility-b"),
            RequireResource: true));

        decision.Allowed.Should().BeFalse();
        probe.Decisions.Should().ContainSingle().Which.ReasonCode.Should().Be("facility_scope_denied");
    }

    [Fact]
    public async Task Shadow_probe_failure_does_not_change_fail_closed_decision()
    {
        var evaluator = new AuthorizationEvaluator(shadowProbe: new ThrowingProbe());

        var decision = await evaluator.EvaluateAsync(new AuthorizationContext(
            Principal("patients.view", "facility-a"), "patients.view",
            new AuthorizationResource("patient", "patient-2", FacilityId: "facility-b"),
            RequireResource: true));

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("facility_scope_denied");
    }

    [Fact]
    public async Task Denies_cross_tenant_resource_access_by_default()
    {
        var evaluator = new AuthorizationEvaluator();
        var decision = await evaluator.EvaluateAsync(new AuthorizationContext(
            Principal("patients.view", "facility-a", "tech-vendor"),
            "patients.view",
            new AuthorizationResource("patient", "patient-1", TenantId: "manufacturing", FacilityId: "facility-a"),
            RequireResource: true));

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("tenant_scope_denied");
    }

    [Fact]
    public async Task Allows_group_hq_cross_tenant_audit_read_when_policy_permits()
    {
        var policy = new ConfigurableCrossTenantAccessPolicy(
        [
            new CrossTenantAllowedPair("group-hq", "manufacturing", "group-audit-read", ["admin.audit.read"])
        ]);
        var evaluator = new AuthorizationEvaluator(crossTenantPolicy: policy);
        var decision = await evaluator.EvaluateAsync(new AuthorizationContext(
            Principal("admin.audit.read", "facility-a", "group-hq"),
            "admin.audit.read",
            new AuthorizationResource("audit-log", "entry-1", TenantId: "manufacturing", FacilityId: "facility-a"),
            RequireResource: true));

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Denies_cross_tenant_when_policy_does_not_cover_action()
    {
        var policy = new ConfigurableCrossTenantAccessPolicy(
        [
            new CrossTenantAllowedPair("group-hq", "manufacturing", "group-audit-read", ["admin.audit.read"])
        ]);
        var evaluator = new AuthorizationEvaluator(crossTenantPolicy: policy);
        var decision = await evaluator.EvaluateAsync(new AuthorizationContext(
            Principal("patients.view", "facility-a", "group-hq"),
            "patients.view",
            new AuthorizationResource("patient", "patient-1", TenantId: "manufacturing", FacilityId: "facility-a"),
            RequireResource: true));

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("tenant_scope_denied");
    }

    private static ClaimsPrincipal Principal(string permission, string facility, string? tenantId = null)
    {
        var claims = new List<Claim>
        {
            new("sub", "user-1"),
            new("permissions", permission),
            new("facility_id", facility)
        };
        if (!string.IsNullOrWhiteSpace(tenantId))
            claims.Add(new Claim("tenant_id", tenantId));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private sealed class RecordingSink : IAuthorizationDecisionSink
    {
        public List<AuthorizationDecisionAudit> Audits { get; } = [];

        public ValueTask WriteAsync(AuthorizationDecisionAudit audit, CancellationToken cancellationToken = default)
        {
            Audits.Add(audit);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingProbe : IAuthorizationShadowProbe
    {
        public List<AuthorizationDecision> Decisions { get; } = [];

        public ValueTask ObserveAsync(AuthorizationContext context, AuthorizationDecision localDecision,
            CancellationToken cancellationToken = default)
        {
            Decisions.Add(localDecision);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingProbe : IAuthorizationShadowProbe
    {
        public ValueTask ObserveAsync(AuthorizationContext context, AuthorizationDecision localDecision,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new InvalidOperationException("synthetic PDP outage"));
    }
}
