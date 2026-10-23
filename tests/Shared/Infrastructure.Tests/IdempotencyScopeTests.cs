using FluentAssertions;
using His.Hope.Infrastructure.Idempotency;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

public sealed class IdempotencyScopeTests
{
    [Fact]
    public void Same_request_scope_produces_a_stable_bounded_key()
    {
        var first = IdempotencyScope.CreateStorageKey("gateway", "tenant-a", "subject-a", "POST", "/orders", "client-key");
        var second = IdempotencyScope.CreateStorageKey("gateway", "tenant-a", "subject-a", "post", "/orders", "client-key");

        first.Should().Be(second);
        first.Should().HaveLength(64);
    }

    [Fact]
    public void Different_tenant_subject_or_operation_cannot_reuse_the_same_storage_key()
    {
        var baseline = IdempotencyScope.CreateStorageKey("gateway", "tenant-a", "subject-a", "POST", "/orders", "client-key");

        IdempotencyScope.CreateStorageKey("gateway", "tenant-b", "subject-a", "POST", "/orders", "client-key").Should().NotBe(baseline);
        IdempotencyScope.CreateStorageKey("gateway", "tenant-a", "subject-b", "POST", "/orders", "client-key").Should().NotBe(baseline);
        IdempotencyScope.CreateStorageKey("gateway", "tenant-a", "subject-a", "POST", "/payments", "client-key").Should().NotBe(baseline);
    }
}
