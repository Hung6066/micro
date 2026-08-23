using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Infrastructure.Audit;
using His.Hope.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class SiemWormAuditForwarderTests
{
    [Fact]
    public async Task ForwardAsync_noops_when_siem_and_worm_are_unconfigured()
    {
        var factory = new Mock<IHttpClientFactory>();
        var configuration = new ConfigurationBuilder().Build();
        var forwarder = new SiemWormAuditForwarder(factory.Object, configuration, NullLogger<SiemWormAuditForwarder>.Instance);

        await forwarder.ForwardAsync(new AuditRecord { Action = "READ", Resource = "Patient" });

        factory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ForwardAsync_records_dead_letter_when_siem_endpoint_is_unreachable()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(nameof(SiemWormAuditForwarder)))
            .Returns(new HttpClient(new BrokenHttpHandler()));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AUDIT_SIEM_URL"] = "http://127.0.0.1:1/unreachable" })
            .Build();
        var forwarder = new SiemWormAuditForwarder(factory.Object, configuration, NullLogger<SiemWormAuditForwarder>.Instance);

        await forwarder.ForwardAsync(new AuditRecord { Action = "READ", Resource = "Patient" });

        Assert.NotEmpty(forwarder.DeadLetter);
        Assert.Equal(1, forwarder.ConsecutiveDeliveryFailures);
    }

    [Fact]
    public async Task IdentityDurableAuditSink_persists_and_forwards()
    {
        var audit = new Mock<IAuditService>();
        var inner = new IdentityObservabilityAuditSink(audit.Object);
        var factory = new Mock<IHttpClientFactory>();
        var configuration = new ConfigurationBuilder().Build();
        var forwarder = new SiemWormAuditForwarder(factory.Object, configuration, NullLogger<SiemWormAuditForwarder>.Instance);
        var sink = new IdentityDurableAuditSink(inner, forwarder);

        await sink.WriteAsync(new AuditRecord { Action = "UPDATE", Resource = "Role", SubjectId = "admin-1" });

        audit.Verify(x => x.LogPhiAccess(It.Is<PhiAuditEntry>(entry => entry.Action == "UPDATE")), Times.Once);
    }

    private sealed class BrokenHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("sink unavailable");
    }
}
