using FluentAssertions;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.SharedKernel.Tests;

public class IHasDomainEventsInterfaceTests
{
    private class TestEntityWithEvents : Entity<Guid>
    {
        public TestEntityWithEvents(Guid id) : base(id) { }
        public void AddEvent(IDomainEvent evt) => AddDomainEvent(evt);
    }

    [Fact]
    public void Entity_ShouldImplementIHasDomainEvents()
    {
        var entity = new TestEntityWithEvents(Guid.NewGuid());
        entity.Should().BeAssignableTo<IHasDomainEvents>();
    }

    [Fact]
    public void IHasDomainEvents_ShouldExposeDomainEvents()
    {
        var entity = new TestEntityWithEvents(Guid.NewGuid());
        var hasEvents = (IHasDomainEvents)entity;

        hasEvents.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearDomainEvents_ThroughInterface_ShouldClear()
    {
        var entity = new TestEntityWithEvents(Guid.NewGuid());
        entity.AddEvent(new TestEvent());
        entity.AddEvent(new TestEvent());

        var hasEvents = (IHasDomainEvents)entity;
        hasEvents.ClearDomainEvents();

        hasEvents.DomainEvents.Should().BeEmpty();
    }

    private class TestEvent : DomainEvent { }
}
