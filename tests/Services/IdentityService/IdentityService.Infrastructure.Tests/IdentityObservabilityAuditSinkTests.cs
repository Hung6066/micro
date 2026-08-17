using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Infrastructure.Audit;
using His.Hope.Observability;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class IdentityObservabilityAuditSinkTests
{
    [Fact]
    public async Task WriteAsync_maps_subject_resource_properties_and_timestamp()
    {
        var audit = new Mock<IAuditService>();
        PhiAuditEntry? captured = null;
        audit.Setup(x => x.LogPhiAccess(It.IsAny<PhiAuditEntry>()))
            .Callback<PhiAuditEntry>(entry => captured = entry);
        var sink = new IdentityObservabilityAuditSink(audit.Object);
        var occurredAt = new DateTimeOffset(2026, 8, 15, 4, 5, 6, TimeSpan.Zero);

        await sink.WriteAsync(new AuditRecord
        {
            Action = "READ",
            Resource = "Patient",
            SubjectId = "user-42",
            OccurredAt = occurredAt,
            Properties = new Dictionary<string, object?>
            {
                ["resourceId"] = 123,
                ["correlationId"] = "corr-7"
            }
        });

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be("user-42");
        captured.ResourceType.Should().Be("Patient");
        captured.ResourceId.Should().Be("123");
        captured.Action.Should().Be("READ");
        captured.CorrelationId.Should().Be("corr-7");
        captured.Timestamp.Should().Be(occurredAt.UtcDateTime);
    }

    [Fact]
    public async Task WriteAsync_uses_safe_defaults_and_honors_cancellation()
    {
        var audit = new Mock<IAuditService>();
        var sink = new IdentityObservabilityAuditSink(audit.Object);

        await sink.WriteAsync(new AuditRecord { Action = "EXPORT", Resource = "AuditLog" });

        audit.Verify(x => x.LogPhiAccess(It.Is<PhiAuditEntry>(entry =>
            entry.UserId == "system" &&
            entry.ResourceType == "AuditLog" &&
            entry.ResourceId == "AuditLog" &&
            entry.CorrelationId == null)), Times.Once);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => sink.WriteAsync(new AuditRecord { Action = "READ", Resource = "Patient" }, cts.Token).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
        audit.VerifyNoOtherCalls();
    }
}
