using His.Hope.EventBus.Abstractions;
using His.Hope.Messaging;

namespace His.Hope.Infrastructure.Tests;

public sealed class MessagingReliabilityContractTests
{
    [Fact]
    public void Event_envelope_rejects_invalid_schema_and_future_timestamp()
    {
        var invalidSchema = new EventEnvelope(
            Guid.NewGuid(), "PatientRegistered", "{}", DateTimeOffset.UtcNow, SchemaVersion: 2);
        var future = invalidSchema with
        {
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(6)
        };

        FluentAssertions.FluentActions.Invoking(() => invalidSchema.Validate())
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentAssertions.FluentActions.Invoking(() => future.Validate())
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Schema_registry_rejects_newer_consumer_schema()
    {
        var registry = new EventSchemaRegistry();
        registry.Register("PatientRegistered", 1);
        var envelope = EventEnvelope.Create(new { PatientId = Guid.NewGuid() }, "PatientRegistered") with
        {
            SchemaVersion = 2
        };

        registry.IsCompatible(envelope).Should().BeFalse();
        FluentAssertions.FluentActions.Invoking(() => registry.Validate(envelope))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Integration_event_has_transport_metadata_for_correlation_and_schema()
    {
        var integrationEvent = new TestIntegrationEvent
        {
            CorrelationId = "corr-1",
            CausationId = "cause-1",
            SchemaVersion = 1,
            Headers = new Dictionary<string, string> { [EventEnvelopeHeaders.PartitionKey] = "patient-1" }
        };

        integrationEvent.Id.Should().NotBeEmpty();
        integrationEvent.SchemaVersion.Should().Be(1);
        integrationEvent.Headers![EventEnvelopeHeaders.PartitionKey].Should().Be("patient-1");
    }

    private sealed class TestIntegrationEvent : IntegrationEvent
    {
    }
}
