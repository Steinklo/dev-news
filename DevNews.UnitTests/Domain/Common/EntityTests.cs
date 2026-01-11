using DevNews.Domain.Common;
using FluentAssertions;

namespace DevNews.UnitTests.Domain.Common;

public class EntityTests
{
    private class TestEntity : Entity<Guid>
    {
        public string Name { get; set; }

        public TestEntity(Guid id, string name) : base(id)
        {
            Name = name;
        }
    }

    [Fact]
    public void Constructor_SetsIdAndCreatedTimestamp()
    {
        var id = Guid.NewGuid();
        var beforeCreation = DateTime.UtcNow;

        var entity = new TestEntity(id, "Test");

        entity.Id.Should().Be(id);
        entity.Created.Should().BeOnOrAfter(beforeCreation);
        entity.Created.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void Equals_SameId_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id, "Name1");
        var entity2 = new TestEntity(id, "Name2");

        entity1.Equals(entity2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentId_ReturnsFalse()
    {
        var entity1 = new TestEntity(Guid.NewGuid(), "Name");
        var entity2 = new TestEntity(Guid.NewGuid(), "Name");

        entity1.Equals(entity2).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var entity = new TestEntity(Guid.NewGuid(), "Name");

        entity.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameId_ReturnsSameHash()
    {
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id, "Name1");
        var entity2 = new TestEntity(id, "Name2");

        entity1.GetHashCode().Should().Be(entity2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentId_ReturnsDifferentHash()
    {
        var entity1 = new TestEntity(Guid.NewGuid(), "Name");
        var entity2 = new TestEntity(Guid.NewGuid(), "Name");

        entity1.GetHashCode().Should().NotBe(entity2.GetHashCode());
    }
}
