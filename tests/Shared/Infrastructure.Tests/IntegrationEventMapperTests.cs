using FluentAssertions;
using His.Hope.EventBus.Abstractions;
using His.Hope.Infrastructure.Events;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.Infrastructure.Tests;

public sealed class IntegrationEventMapperTests
{
    [Fact]
    public void Maps_registered_domain_event_to_integration_event()
    {
        var mapper = new IntegrationEventMapper([
            new IntegrationEventMapping<TestDomainEvent, TestIntegrationEvent>(
                domainEvent => new TestIntegrationEvent(domainEvent.Value))]);

        var result = mapper.Map(new TestDomainEvent("patient-1"));

        result.Should().BeOfType<TestIntegrationEvent>()
            .Which.Value.Should().Be("patient-1");
    }

    [Fact]
    public void Returns_null_for_unmapped_domain_event()
    {
        var mapper = new IntegrationEventMapper([
            new IntegrationEventMapping<TestDomainEvent, TestIntegrationEvent>(
                domainEvent => new TestIntegrationEvent(domainEvent.Value))]);

        mapper.Map(new OtherDomainEvent()).Should().BeNull();
    }

    private sealed class TestDomainEvent(string value) : DomainEvent
    {
        public string Value { get; } = value;
    }

    private sealed class OtherDomainEvent : DomainEvent;

    private sealed class TestIntegrationEvent(string value) : IntegrationEvent
    {
        public string Value { get; } = value;
    }
}
