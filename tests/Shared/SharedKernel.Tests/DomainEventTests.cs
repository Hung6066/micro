using FluentAssertions;
using His.Hope.SharedKernel.Domain.Common;
using Moq;

namespace His.Hope.SharedKernel.Tests;

public class DomainEventTests
{
    private class TestDomainEvent : DomainEvent
    {
        public string Data { get; }
        public TestDomainEvent(string data) => Data = data;
    }

    [Fact]
    public void DomainEvent_ShouldImplementIDomainEvent()
    {
        var evt = new TestDomainEvent("test");
        evt.Should().BeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void DomainEvent_ShouldSetOccurredOn()
    {
        var evt = new TestDomainEvent("test");

        evt.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DomainEvent_ShouldImplementINotification()
    {
        var evt = new TestDomainEvent("test");
        evt.Should().BeAssignableTo<MediatR.INotification>();
    }

    [Fact]
    public void DomainEvent_OccurredOnShouldBeUtc()
    {
        var evt = new TestDomainEvent("test");

        evt.OccurredOn.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void DifferentEvents_ShouldHaveDifferentTimestamps()
    {
        var evt1 = new TestDomainEvent("data1");
        Thread.Sleep(1);
        var evt2 = new TestDomainEvent("data2");

        evt1.OccurredOn.Should().BeBefore(evt2.OccurredOn);
    }

    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var evt = new TestDomainEvent("my-data");

        evt.Data.Should().Be("my-data");
    }

    [Fact]
    public void IDomainEvent_Interface_ShouldRequireOccurredOn()
    {
        var evt = new TestDomainEvent("test");
        var idEvent = (IDomainEvent)evt;

        idEvent.OccurredOn.Should().Be(evt.OccurredOn);
    }

    [Fact]
    public async Task DomainEventDispatcher_ShouldPublishEvents()
    {
        var mediator = new Mock<MediatR.IMediator>();
        var dispatcher = new DomainEventDispatcher(mediator.Object);
        var evt = new TestDomainEvent("dispatch-test");

        await dispatcher.DispatchAsync(new[] { evt }, CancellationToken.None);

        mediator.Verify(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DomainEventDispatcher_WithMultipleEvents_ShouldPublishAll()
    {
        var mediator = new Mock<MediatR.IMediator>();
        var dispatcher = new DomainEventDispatcher(mediator.Object);
        var evt1 = new TestDomainEvent("first");
        var evt2 = new TestDomainEvent("second");

        await dispatcher.DispatchAsync(new[] { evt1, evt2 }, CancellationToken.None);

        mediator.Verify(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DomainEventDispatcher_WithNoEvents_ShouldNotPublish()
    {
        var mediator = new Mock<MediatR.IMediator>();
        var dispatcher = new DomainEventDispatcher(mediator.Object);

        await dispatcher.DispatchAsync(Array.Empty<IDomainEvent>(), CancellationToken.None);

        mediator.Verify(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void DomainEvent_Equality_ByReference()
    {
        var evt1 = new TestDomainEvent("same");
        var evt2 = new TestDomainEvent("same");

        evt1.Should().NotBe(evt2);
    }
}
