using DevNews.Domain.NewsItem.ValueObjects;
using FluentAssertions;

namespace DevNews.UnitTests.Domain.NewsItem.ValueObjects;

public class NewsTitleTests
{
    [Fact]
    public void Create_ValidTitle_ReturnsSuccess()
    {
        var result = NewsTitle.Create("Valid Title");

        result.IsSuccess.Should().BeTrue();
        result.Data!.Value.Should().Be("Valid Title");
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var result = NewsTitle.Create("  Title with spaces  ");

        result.IsSuccess.Should().BeTrue();
        result.Data!.Value.Should().Be("Title with spaces");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhitespace_ReturnsFailure(string? title)
    {
        var result = NewsTitle.Create(title!);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Title cannot be empty");
    }

    [Fact]
    public void Create_ExceedsMaxLength_ReturnsFailure()
    {
        var longTitle = new string('a', NewsTitle.MaxLength + 1);

        var result = NewsTitle.Create(longTitle);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"{NewsTitle.MaxLength}");
    }

    [Fact]
    public void Create_ExactlyMaxLength_ReturnsSuccess()
    {
        var exactTitle = new string('a', NewsTitle.MaxLength);

        var result = NewsTitle.Create(exactTitle);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Value.Length.Should().Be(NewsTitle.MaxLength);
    }

    [Fact]
    public void Create_MinimumLength_ReturnsSuccess()
    {
        var minTitle = new string('a', NewsTitle.MinLength);

        var result = NewsTitle.Create(minTitle);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var result = NewsTitle.Create("Test Title");
        string value = result.Data!;

        value.Should().Be("Test Title");
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var result = NewsTitle.Create("Test Title");

        result.Data!.ToString().Should().Be("Test Title");
    }

    [Fact]
    public void Equals_SameTitle_ReturnsTrue()
    {
        var title1 = NewsTitle.Create("Same Title").Data!;
        var title2 = NewsTitle.Create("Same Title").Data!;

        title1.Equals(title2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentTitle_ReturnsFalse()
    {
        var title1 = NewsTitle.Create("Title One").Data!;
        var title2 = NewsTitle.Create("Title Two").Data!;

        title1.Equals(title2).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameTitle_ReturnsSameHash()
    {
        var title1 = NewsTitle.Create("Same Title").Data!;
        var title2 = NewsTitle.Create("Same Title").Data!;

        title1.GetHashCode().Should().Be(title2.GetHashCode());
    }
}
