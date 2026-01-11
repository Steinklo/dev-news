using DevNews.Domain.NewsItem.ValueObjects;
using FluentAssertions;

namespace DevNews.UnitTests.Domain.NewsItem.ValueObjects;

public class NewsSummaryTests
{
    [Fact]
    public void Create_ValidSummary_ReturnsSuccess()
    {
        var validSummary = "This is a valid summary that meets the minimum length requirement.";

        var result = NewsSummary.Create(validSummary);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Value.Should().Be(validSummary);
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var summaryWithSpaces = "  This is a summary with spaces  ";

        var result = NewsSummary.Create(summaryWithSpaces);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Value.Should().Be("This is a summary with spaces");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhitespace_ReturnsFailure(string? summary)
    {
        var result = NewsSummary.Create(summary!);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Summary cannot be empty");
    }

    [Fact]
    public void Create_BelowMinLength_ReturnsFailure()
    {
        var shortSummary = new string('a', NewsSummary.MinLength - 1);

        var result = NewsSummary.Create(shortSummary);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"{NewsSummary.MinLength}");
    }

    [Fact]
    public void Create_ExceedsMaxLength_ReturnsFailure()
    {
        var longSummary = new string('a', NewsSummary.MaxLength + 1);

        var result = NewsSummary.Create(longSummary);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"{NewsSummary.MaxLength}");
    }

    [Fact]
    public void Create_ExactlyMinLength_ReturnsSuccess()
    {
        var minSummary = new string('a', NewsSummary.MinLength);

        var result = NewsSummary.Create(minSummary);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Value.Length.Should().Be(NewsSummary.MinLength);
    }

    [Fact]
    public void Create_ExactlyMaxLength_ReturnsSuccess()
    {
        var maxSummary = new string('a', NewsSummary.MaxLength);

        var result = NewsSummary.Create(maxSummary);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Value.Length.Should().Be(NewsSummary.MaxLength);
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var result = NewsSummary.Create("This is a test summary for conversion");
        string value = result.Data!;

        value.Should().Be("This is a test summary for conversion");
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var summaryText = "This is a test summary";
        var result = NewsSummary.Create(summaryText);

        result.Data!.ToString().Should().Be(summaryText);
    }

    [Fact]
    public void Equals_SameSummary_ReturnsTrue()
    {
        var text = "This is a test summary for equality";
        var summary1 = NewsSummary.Create(text).Data!;
        var summary2 = NewsSummary.Create(text).Data!;

        summary1.Equals(summary2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentSummary_ReturnsFalse()
    {
        var summary1 = NewsSummary.Create("First summary text here").Data!;
        var summary2 = NewsSummary.Create("Second summary text here").Data!;

        summary1.Equals(summary2).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameSummary_ReturnsSameHash()
    {
        var text = "This is a test summary for hashing";
        var summary1 = NewsSummary.Create(text).Data!;
        var summary2 = NewsSummary.Create(text).Data!;

        summary1.GetHashCode().Should().Be(summary2.GetHashCode());
    }
}
