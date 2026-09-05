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
    public void Schema_registry_validates_transport_event_versions()
    {
        var registry = new EventSchemaRegistry();
        registry.Register("PatientRegistered", 1);

        registry.IsCompatible("PatientRegistered", 1).Should().BeTrue();
        registry.IsCompatible("PatientRegistered", 2).Should().BeFalse();
        FluentAssertions.FluentActions.Invoking(() => registry.Validate("PatientRegistered", 2))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Schema_registry_rejects_conflicting_registration()
    {
        var registry = new EventSchemaRegistry();
        registry.Register("PatientRegistered", 1);

        FluentAssertions.FluentActions.Invoking(() => registry.Register("PatientRegistered", 2))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*conflicting version*");
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

    [Fact]
    public void Event_envelope_rejects_untrusted_priority_metadata()
    {
        var envelope = EventEnvelope.Create(new { PatientId = Guid.NewGuid() }, "PatientRegistered") with
        {
            Headers = new Dictionary<string, string>
            {
                [EventEnvelopeHeaders.Priority] = "P0;admin"
            }
        };

        FluentAssertions.FluentActions.Invoking(() => envelope.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*priority*");
    }

    private sealed class TestIntegrationEvent : IntegrationEvent
    {
    }
}
