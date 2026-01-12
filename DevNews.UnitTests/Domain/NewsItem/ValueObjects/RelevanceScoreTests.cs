using DevNews.Domain.NewsItem.ValueObjects;

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

        Assert.True(result.IsSuccess);
        Assert.Equal(score, result.Data!.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void Create_NegativeScore_ReturnsFailure(int score)
    {
        var result = RelevanceScore.Create(score);

        Assert.False(result.IsSuccess);
        Assert.Equal("Relevance score must be between 0 and 100", result.ErrorMessage);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(int.MaxValue)]
    public void Create_ScoreAbove100_ReturnsFailure(int score)
    {
        var result = RelevanceScore.Create(score);

        Assert.False(result.IsSuccess);
        Assert.Equal("Relevance score must be between 0 and 100", result.ErrorMessage);
    }

    [Fact]
    public void ImplicitConversion_ToInt_ReturnsValue()
    {
        var result = RelevanceScore.Create(75);
        int value = result.Data!;

        Assert.Equal(75, value);
    }

    [Fact]
    public void ToString_ReturnsStringValue()
    {
        var result = RelevanceScore.Create(42);

        Assert.Equal("42", result.Data!.ToString());
    }

    [Fact]
    public void Equals_SameScore_ReturnsTrue()
    {
        var score1 = RelevanceScore.Create(85).Data!;
        var score2 = RelevanceScore.Create(85).Data!;

        Assert.True(score1.Equals(score2));
    }

    [Fact]
    public void Equals_DifferentScore_ReturnsFalse()
    {
        var score1 = RelevanceScore.Create(85).Data!;
        var score2 = RelevanceScore.Create(15).Data!;

        Assert.False(score1.Equals(score2));
    }

    [Fact]
    public void GetHashCode_SameScore_ReturnsSameHash()
    {
        var score1 = RelevanceScore.Create(50).Data!;
        var score2 = RelevanceScore.Create(50).Data!;

        Assert.Equal(score1.GetHashCode(), score2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentScore_ReturnsDifferentHash()
    {
        var score1 = RelevanceScore.Create(25).Data!;
        var score2 = RelevanceScore.Create(75).Data!;

        Assert.NotEqual(score1.GetHashCode(), score2.GetHashCode());
    }
}
