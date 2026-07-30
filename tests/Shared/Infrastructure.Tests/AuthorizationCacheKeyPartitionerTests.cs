using System.Security.Claims;
using His.Hope.Infrastructure.Caching;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

public sealed class AuthorizationCacheKeyPartitionerTests
{
    [Fact]
    public void Same_resource_key_is_partitioned_for_different_subjects()
    {
        var first = Partitioner("user-a", "facility-a");
        var second = Partitioner("user-b", "facility-a");

        var firstKey = first.Partition("patients:search:all");
        var secondKey = second.Partition("patients:search:all");

        Assert.NotEqual(firstKey, secondKey);
    }

    [Fact]
    public void Partitioning_is_idempotent_for_hybrid_cache_tiers()
    {
        var partitioner = Partitioner("user-a", "facility-a");
        var key = partitioner.Partition("patients:42");

        Assert.Equal(key, partitioner.Partition(key));
    }

    [Fact]
    public void Security_version_change_invalidates_the_partition()
    {
        var first = Partitioner("user-a", "facility-a", "v1").Partition("patients:search:all");
        var second = Partitioner("user-a", "facility-a", "v2").Partition("patients:search:all");

        Assert.NotEqual(first, second);
    }

    private static AuthorizationCacheKeyPartitioner Partitioner(
        string subject, string facility, string securityVersion = "v1")
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", subject),
            new Claim("facility_id", facility),
            new Claim("jti", $"token-{subject}"),
            new Claim("securityVersion", securityVersion)
        ], "test");
        context.User = new ClaimsPrincipal(identity);

        return new AuthorizationCacheKeyPartitioner(new TestHttpContextAccessor(context));
    }

    private sealed class TestHttpContextAccessor(HttpContext context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }
}
