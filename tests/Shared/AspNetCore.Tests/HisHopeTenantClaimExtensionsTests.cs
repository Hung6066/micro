using System.Security.Claims;
using FluentAssertions;
using His.Hope.AspNetCore.Tenancy;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace His.Hope.AspNetCore.Tests;

public sealed class HisHopeTenantClaimExtensionsTests
{
    [Fact]
    public void ResolveActiveTenant_uses_token_tenant_when_query_is_missing()
    {
        var context = CreateContext(
            new Claim("tenant_id", "manufacturing"),
            new Claim("portal_class", "operator"));

        context.ResolveActiveTenant().Should().Be("manufacturing");
    }

    [Fact]
    public void ResolveActiveTenant_allows_operator_membership_cross_tenant()
    {
        var context = CreateContext(
            new Claim("tenant_id", "manufacturing"),
            new Claim("tenant_membership", "customer-factory-x"),
            new Claim("portal_class", "operator"));
        context.Request.QueryString = new QueryString("?tenantKey=customer-factory-x");

        context.ResolveActiveTenant().Should().Be("customer-factory-x");
    }

    [Fact]
    public void ResolveActiveTenant_rejects_conflicting_canonical_header_and_legacy_query_parameter()
    {
        var context = CreateContext(
            new Claim("tenant_id", "manufacturing"),
            new Claim("tenant_membership", "customer-factory-x"),
            new Claim("portal_class", "operator"));
        context.Request.Headers["X-HisHope-Tenant"] = "customer-factory-x";
        context.Request.QueryString = new QueryString("?tenantKey=other-tenant");

        context.ResolveActiveTenant().Should().BeNull();
    }

    [Fact]
    public void GetRequestedTenant_prefers_canonical_header_and_trims_context_when_selectors_agree_or_query_is_absent()
    {
        var context = CreateContext(new Claim("tenant_id", "manufacturing"));
        context.Request.Headers["X-HisHope-Tenant"] = " customer-factory-x ";
        context.Request.QueryString = new QueryString("?tenantKey=customer-factory-x");

        context.GetRequestedTenant().Should().Be("customer-factory-x");
    }

    [Fact]
    public void HasConflictingTenantSelectors_detects_mismatched_header_and_query()
    {
        var context = CreateContext(new Claim("tenant_id", "manufacturing"));
        context.Request.Headers["X-HisHope-Tenant"] = "customer-factory-x";
        context.Request.QueryString = new QueryString("?tenantKey=other-tenant");

        context.HasConflictingTenantSelectors().Should().BeTrue();
    }

    [Fact]
    public void ResolveActiveTenant_rejects_unclaimed_canonical_header()
    {
        var context = CreateContext(
            new Claim("tenant_id", "manufacturing"),
            new Claim("tenant_membership", "customer-factory-x"),
            new Claim("portal_class", "operator"));
        context.Request.Headers["X-HisHope-Tenant"] = "other-tenant";

        context.ResolveActiveTenant().Should().BeNull();
    }

    [Fact]
    public void ResolveActiveTenant_denies_unclaimed_cross_tenant()
    {
        var context = CreateContext(
            new Claim("tenant_id", "manufacturing"),
            new Claim("tenant_membership", "manufacturing"),
            new Claim("portal_class", "operator"));
        context.Request.QueryString = new QueryString("?tenantKey=other-tenant");

        context.ResolveActiveTenant().Should().BeNull();
    }

    [Fact]
    public void TryResolveActiveTenant_uses_custom_cross_tenant_evaluator()
    {
        var user = CreatePrincipal(
            new Claim("tenant_id", "buyer-tenant"),
            new Claim("portal_class", "operator"));

        user.TryResolveActiveTenant(
            "managed-tenant",
            out var tenantKey,
            (_, requested, _) => requested == "managed-tenant").Should().BeTrue();

        tenantKey.Should().Be("managed-tenant");
    }

    [Fact]
    public void TenantMatches_accepts_claimed_cross_tenant_without_query_override()
    {
        var context = CreateContext(
            new Claim("tenant_id", "manufacturing"),
            new Claim("tenant_membership", "selector-tenant"),
            new Claim("portal_class", "operator"));

        context.TenantMatches("selector-tenant").Should().BeTrue();
    }

    private static DefaultHttpContext CreateContext(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "test");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));
}
