using DevNews.Domain.Common;

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

        Assert.Equal(id, entity.Id);
        Assert.True(entity.Created >= beforeCreation);
        Assert.True(entity.Created <= DateTime.UtcNow);
    }

    [Fact]
    public void Equals_SameId_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id, "Name1");
        var entity2 = new TestEntity(id, "Name2");

        Assert.True(entity1.Equals(entity2));
    }

    [Fact]
    public void Equals_DifferentId_ReturnsFalse()
    {
        var entity1 = new TestEntity(Guid.NewGuid(), "Name");
        var entity2 = new TestEntity(Guid.NewGuid(), "Name");

        Assert.False(entity1.Equals(entity2));
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var entity = new TestEntity(Guid.NewGuid(), "Name");

        Assert.False(entity.Equals(null));
    }

    [Fact]
    public void GetHashCode_SameId_ReturnsSameHash()
    {
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id, "Name1");
        var entity2 = new TestEntity(id, "Name2");

        Assert.Equal(entity1.GetHashCode(), entity2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentId_ReturnsDifferentHash()
    {
        var entity1 = new TestEntity(Guid.NewGuid(), "Name");
        var entity2 = new TestEntity(Guid.NewGuid(), "Name");

        Assert.NotEqual(entity1.GetHashCode(), entity2.GetHashCode());
    }
}
