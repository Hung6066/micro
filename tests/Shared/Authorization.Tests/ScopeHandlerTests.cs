using System.Security.Claims;
using FluentAssertions;
using His.Hope.Authorization.Handlers;
using His.Hope.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace His.Hope.Authorization.Tests;

public sealed class ScopeHandlerTests
{
    [Fact]
    public async Task Allows_when_required_scope_is_space_delimited()
    {
        var context = Context(new Claim("scope", "openid fhir.patient.read"));

        await new ScopeHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Allows_scp_claim_and_rejects_wrong_scope()
    {
        var context = Context(new Claim("scp", "fhir.encounter.read"));

        await new ScopeHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Denies_unauthenticated_principal()
    {
        var context = new AuthorizationHandlerContext(
            [new ScopeRequirement("fhir.patient.read")],
            new ClaimsPrincipal(new ClaimsIdentity()),
            null);

        await new ScopeHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static AuthorizationHandlerContext Context(params Claim[] claims) =>
        new(
            [new ScopeRequirement("fhir.patient.read")],
            new ClaimsPrincipal(new ClaimsIdentity(
                [.. claims, new Claim("sub", "client-1")], "test")),
            null);
}
