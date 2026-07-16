using FluentAssertions;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.SharedKernel.Tests;

public class EntityEqualityTests
{
    private class TestEntity : Entity<Guid>
    {
        public string Name { get; }

        public TestEntity(Guid id, string name) : base(id)
        {
            Name = name;
        }
    }

    private class AnotherEntity : Entity<Guid>
    {
        public string Value { get; }

        public AnotherEntity(Guid id, string value) : base(id)
        {
            Value = value;
        }
    }

    [Fact]
    public void SameId_ShouldBeEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id, "First");
        var entity2 = new TestEntity(id, "Second");

        // Act & Assert
        entity1.Should().Be(entity2);
        (entity1 == entity2).Should().BeTrue();
        entity1.Equals(entity2).Should().BeTrue();
    }

    [Fact]
    public void SameId_DifferentProperties_ShouldBeEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var e1 = new TestEntity(id, "Alice");
        var e2 = new TestEntity(id, "Bob");

        // Assert - Entity equality is by identity, not by values
        e1.Should().Be(e2);
    }

    [Fact]
    public void SameId_ShouldHaveSameHashCode()
    {
        // Arrange
        var id = Guid.NewGuid();
        var e1 = new TestEntity(id, "Test");
        var e2 = new TestEntity(id, "Test");

        // Assert
        e1.GetHashCode().Should().Be(e2.GetHashCode());
    }

    [Fact]
    public void DifferentIds_ShouldNotBeEqual()
    {
        // Arrange
        var e1 = new TestEntity(Guid.NewGuid(), "Entity1");
        var e2 = new TestEntity(Guid.NewGuid(), "Entity2");

        // Assert
        e1.Should().NotBe(e2);
        (e1 != e2).Should().BeTrue();
    }

    [Fact]
    public void DifferentIds_SameData_ShouldNotBeEqual()
    {
        // Arrange
        var e1 = new TestEntity(Guid.NewGuid(), "Same");
        var e2 = new TestEntity(Guid.NewGuid(), "Same");

        // Assert
        e1.Should().NotBe(e2);
    }

    [Fact]
    public void NullComparison_ShouldWork()
    {
        // Arrange
        var entity = new TestEntity(Guid.NewGuid(), "Test");

        // Act & Assert
        (entity == null!).Should().BeFalse();
        (null! == entity).Should().BeFalse();
        entity.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void BothNull_OperatorShouldReturnTrue()
    {
        // Arrange
        TestEntity? e1 = null;
        TestEntity? e2 = null;

        // Act & Assert
        (e1 == e2).Should().BeTrue();
        (e1 != e2).Should().BeFalse();
    }

    [Fact]
    public void DifferentTypes_SameId_ShouldBeEqualByIdentity()
    {
        // Arrange
        var id = Guid.NewGuid();
        var e1 = new TestEntity(id, "Test");
        var e2 = new AnotherEntity(id, "Another");

        // Act & Assert
        // Entity equality is based solely on Id, not on runtime type
        // Both are Entity<Guid> with the same Id, so they are considered equal
        e1.Equals(e2).Should().BeTrue();
    }

    [Fact]
    public void EntityEqualsObject_WithDifferentType_ShouldReturnFalse()
    {
        // Arrange
        var entity = new TestEntity(Guid.NewGuid(), "Test");

        // Act & Assert
        entity.Equals("not an entity").Should().BeFalse();
        entity.Equals(42).Should().BeFalse();
    }

    [Fact]
    public void HashCode_ShouldBeDeterministic()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity = new TestEntity(id, "Test");

        // Act
        var hash1 = entity.GetHashCode();
        var hash2 = entity.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Entity_ShouldImplementIEquatable()
    {
        // Arrange
        var entity = new TestEntity(Guid.NewGuid(), "Test");

        // Assert
        entity.Should().BeAssignableTo<IEquatable<Entity<Guid>>>();
    }
}
