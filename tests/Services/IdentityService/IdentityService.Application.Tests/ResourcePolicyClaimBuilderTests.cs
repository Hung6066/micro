using System.Text.Json;
using FluentAssertions;
using His.Hope.IdentityService.Application.OpenIddict;
using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class ResourcePolicyClaimBuilderTests
{
    [Fact]
    public async Task Returns_null_when_principal_set_is_empty()
    {
        await using var db = TestApplicationDbContext.Create();

        var result = await ResourcePolicyClaimBuilder.BuildAsync(
            db, Guid.NewGuid(), new[] { "", "  " }, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Projects_only_published_matching_statements()
    {
        await using var db = TestApplicationDbContext.Create();
        var scopeId = Guid.NewGuid();
        db.IamResourcePolicies.AddRange(
            new IamResourcePolicy
            {
                ScopeId = scopeId,
                ServiceKey = "patients",
                ResourcePattern = "patient/*",
                LifecycleStatus = "published",
                StatementsJson = "[{\"principal\":\"svc-a\",\"actions\":[\"read\",\"write\"],\"effect\":\"ALLOW\"}]"
            },
            new IamResourcePolicy
            {
                ScopeId = scopeId,
                ServiceKey = "ignored",
                ResourcePattern = "*",
                LifecycleStatus = "draft",
                StatementsJson = "[{\"principal\":\"svc-a\",\"actions\":[\"read\"]}]"
            });
        await db.SaveChangesAsync();

        var json = await ResourcePolicyClaimBuilder.BuildAsync(
            db, scopeId, new[] { "svc-a" }, CancellationToken.None);

        json.Should().NotBeNull();
        var claims = JsonSerializer.Deserialize<ResourcePolicyClaim[]>(json!);
        claims.Should().ContainSingle();
        claims![0].ServiceKey.Should().Be("patients");
        claims[0].ResourcePattern.Should().Be("patient/*");
        claims[0].Effect.Should().Be("allow");
        claims[0].Actions.Should().Equal("read", "write");
    }

    [Fact]
    public async Task Skips_nonmatching_and_empty_action_statements()
    {
        await using var db = TestApplicationDbContext.Create();
        var scopeId = Guid.NewGuid();
        db.IamResourcePolicies.Add(new IamResourcePolicy
        {
            ScopeId = scopeId,
            ServiceKey = "patients",
            ResourcePattern = "patient/*",
            LifecycleStatus = "published",
            StatementsJson = "[{\"principal\":\"other\",\"actions\":[\"read\"]},{\"principal\":[\"svc-a\"],\"actions\":[]}]"
        });
        await db.SaveChangesAsync();

        (await ResourcePolicyClaimBuilder.BuildAsync(db, scopeId, new[] { "svc-a" }, CancellationToken.None))
            .Should().BeNull();
    }

    [Fact]
    public async Task Malformed_published_policy_becomes_deny_claim()
    {
        await using var db = TestApplicationDbContext.Create();
        var scopeId = Guid.NewGuid();
        db.IamResourcePolicies.Add(new IamResourcePolicy
        {
            ScopeId = scopeId,
            ServiceKey = "patients",
            ResourcePattern = "patient/*",
            LifecycleStatus = "published",
            StatementsJson = "{malformed"
        });
        await db.SaveChangesAsync();

        var json = await ResourcePolicyClaimBuilder.BuildAsync(db, scopeId, new[] { "svc-a" }, CancellationToken.None);

        var claim = JsonSerializer.Deserialize<ResourcePolicyClaim[]>(json!); 
        claim.Should().ContainSingle();
        claim![0].Effect.Should().Be("deny");
        claim[0].Actions.Should().Equal("*");
    }
}
