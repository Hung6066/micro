using FluentAssertions;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.SharedKernel.Tests;

public class AggregateRootTests
{
    private class TestAggregateRoot : AggregateRoot<Guid>
    {
        public string Name { get; }

        public TestAggregateRoot(Guid id, string name) : base(id)
        {
            Name = name;
        }

        public void TestAddDomainEvent(IDomainEvent evt) => AddDomainEvent(evt);
        public void TestRemoveDomainEvent(IDomainEvent evt) => RemoveDomainEvent(evt);
    }

    private class TestDomainEvent : DomainEvent
    {
        public string Data { get; }
        public TestDomainEvent(string data) => Data = data;
    }

    [Fact]
    public void AggregateRoot_ShouldInheritFromEntity()
    {
        var ar = new TestAggregateRoot(Guid.NewGuid(), "Test");
        ar.Should().BeAssignableTo<Entity<Guid>>();
    }

    [Fact]
    public void AggregateRoot_ShouldImplementIAggregateRoot()
    {
        var ar = new TestAggregateRoot(Guid.NewGuid(), "Test");
        ar.Should().BeAssignableTo<IAggregateRoot>();
    }

    [Fact]
    public void AggregateRoot_CanAddAndRetrieveDomainEvents()
    {
        var ar = new TestAggregateRoot(Guid.NewGuid(), "Test");
        var evt = new TestDomainEvent("test-data");

        ar.TestAddDomainEvent(evt);

        ar.DomainEvents.Should().ContainSingle().Which.Should().Be(evt);
    }

    [Fact]
    public void AggregateRoot_CanClearDomainEvents()
    {
        var ar = new TestAggregateRoot(Guid.NewGuid(), "Test");
        ar.TestAddDomainEvent(new TestDomainEvent("data1"));
        ar.TestAddDomainEvent(new TestDomainEvent("data2"));

        ar.ClearDomainEvents();

        ar.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AggregateRoot_CanRemoveDomainEvent()
    {
        var ar = new TestAggregateRoot(Guid.NewGuid(), "Test");
        var evt1 = new TestDomainEvent("keep");
        var evt2 = new TestDomainEvent("remove");

        ar.TestAddDomainEvent(evt1);
        ar.TestAddDomainEvent(evt2);
        ar.TestRemoveDomainEvent(evt2);

        ar.DomainEvents.Should().ContainSingle().Which.Should().Be(evt1);
    }

    [Fact]
    public void AggregateRoot_Equality_ByIdentity()
    {
        var id = Guid.NewGuid();
        var ar1 = new TestAggregateRoot(id, "First");
        var ar2 = new TestAggregateRoot(id, "Second");

        ar1.Should().Be(ar2);
        ar1.GetHashCode().Should().Be(ar2.GetHashCode());
    }

    [Fact]
    public void AggregateRoot_Equality_ByIdentityOnly()
    {
        var ar1 = new TestAggregateRoot(Guid.NewGuid(), "Same");
        var ar2 = new TestAggregateRoot(Guid.NewGuid(), "Same");

        ar1.Should().NotBe(ar2);
    }
}
