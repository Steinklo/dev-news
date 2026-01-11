using DevNews.Domain.NewsItem.ValueObjects;
using FluentAssertions;

namespace DevNews.UnitTests.Domain.NewsItem.ValueObjects;

public class RelevanceScoreTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    public void Create_ValidScore_ReturnsSuccess(int score)
    {
        var result = RelevanceScore.Create(score);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Value.Should().Be(score);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void Create_NegativeScore_ReturnsFailure(int score)
    {
        var result = RelevanceScore.Create(score);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Relevance score must be between 0 and 100");
    }

    [Theory]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(int.MaxValue)]
    public void Create_ScoreAbove100_ReturnsFailure(int score)
    {
        var result = RelevanceScore.Create(score);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Relevance score must be between 0 and 100");
    }

    [Fact]
    public void ImplicitConversion_ToInt_ReturnsValue()
    {
        var result = RelevanceScore.Create(75);
        int value = result.Data!;

        value.Should().Be(75);
    }

    [Fact]
    public void ToString_ReturnsStringValue()
    {
        var result = RelevanceScore.Create(42);

        result.Data!.ToString().Should().Be("42");
    }

    [Fact]
    public void Equals_SameScore_ReturnsTrue()
    {
        var score1 = RelevanceScore.Create(85).Data!;
        var score2 = RelevanceScore.Create(85).Data!;

        score1.Equals(score2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentScore_ReturnsFalse()
    {
        var score1 = RelevanceScore.Create(85).Data!;
        var score2 = RelevanceScore.Create(15).Data!;

        score1.Equals(score2).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameScore_ReturnsSameHash()
    {
        var score1 = RelevanceScore.Create(50).Data!;
        var score2 = RelevanceScore.Create(50).Data!;

        score1.GetHashCode().Should().Be(score2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentScore_ReturnsDifferentHash()
    {
        var score1 = RelevanceScore.Create(25).Data!;
        var score2 = RelevanceScore.Create(75).Data!;

        score1.GetHashCode().Should().NotBe(score2.GetHashCode());
    }
}
