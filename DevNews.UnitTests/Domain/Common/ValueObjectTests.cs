using DevNews.Domain.Common;

namespace DevNews.UnitTests.Domain.Common;

public class ValueObjectTests
{
    private class TestValueObject : ValueObject
    {
        public string Value1 { get; }
        public int Value2 { get; }

        public TestValueObject(string value1, int value2)
        {
            Value1 = value1;
            Value2 = value2;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value1;
            yield return Value2;
        }
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var vo1 = new TestValueObject("test", 42);
        var vo2 = new TestValueObject("test", 42);

        Assert.True(vo1.Equals(vo2));
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var vo1 = new TestValueObject("test", 42);
        var vo2 = new TestValueObject("different", 42);

        Assert.False(vo1.Equals(vo2));
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var vo = new TestValueObject("test", 42);

        Assert.False(vo.Equals(null));
    }

    [Fact]
    public void Equals_SameReference_ReturnsTrue()
    {
        var vo = new TestValueObject("test", 42);

        Assert.True(vo.Equals(vo));
    }

    [Fact]
    public void Equals_ObjectOverload_SameValues_ReturnsTrue()
    {
        var vo1 = new TestValueObject("test", 42);
        object vo2 = new TestValueObject("test", 42);

        Assert.True(vo1.Equals(vo2));
    }

    [Fact]
    public void Equals_ObjectOverload_NullObject_ReturnsFalse()
    {
        var vo = new TestValueObject("test", 42);
        object? nullObj = null;

        Assert.False(vo.Equals(nullObj));
    }

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHash()
    {
        var vo1 = new TestValueObject("test", 42);
        var vo2 = new TestValueObject("test", 42);

        Assert.Equal(vo1.GetHashCode(), vo2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_ReturnsDifferentHash()
    {
        var vo1 = new TestValueObject("test", 42);
        var vo2 = new TestValueObject("different", 99);

        Assert.NotEqual(vo1.GetHashCode(), vo2.GetHashCode());
    }
}
