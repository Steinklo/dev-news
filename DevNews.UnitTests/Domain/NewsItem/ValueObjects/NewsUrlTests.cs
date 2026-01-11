using DevNews.Domain.NewsItem.ValueObjects;
using FluentAssertions;

namespace DevNews.UnitTests.Domain.NewsItem.ValueObjects;

public class NewsUrlTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/path/to/article")]
    [InlineData("https://example.com/path?query=value")]
    public void Create_ValidUrl_ReturnsSuccess(string url)
    {
        var result = NewsUrl.Create(url);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhitespace_ReturnsFailure(string? url)
    {
        var result = NewsUrl.Create(url!);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("URL cannot be empty");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("just some text")]
    [InlineData("example.com")]
    public void Create_InvalidUrl_ReturnsFailure(string url)
    {
        var result = NewsUrl.Create(url);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("URL must be a valid absolute URL");
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///path/to/file")]
    [InlineData("mailto:test@example.com")]
    public void Create_NonHttpScheme_ReturnsFailure(string url)
    {
        var result = NewsUrl.Create(url);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("URL must use HTTP or HTTPS protocol");
    }

    [Fact]
    public void Create_RemovesTrailingSlash()
    {
        var result = NewsUrl.Create("https://example.com/");

        result.IsSuccess.Should().BeTrue();
        result.Data!.Value.Should().Be("https://example.com");
    }

    [Fact]
    public void Create_PreservesPathWithoutTrailingSlash()
    {
        var result = NewsUrl.Create("https://example.com/path/");

        result.IsSuccess.Should().BeTrue();
        result.Data!.Value.Should().Be("https://example.com/path");
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var result = NewsUrl.Create("https://example.com/article");
        string value = result.Data!;

        value.Should().Be("https://example.com/article");
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var result = NewsUrl.Create("https://example.com");

        result.Data!.ToString().Should().Be("https://example.com");
    }

    [Fact]
    public void Equals_SameUrl_ReturnsTrue()
    {
        var url1 = NewsUrl.Create("https://example.com").Data!;
        var url2 = NewsUrl.Create("https://example.com").Data!;

        url1.Equals(url2).Should().BeTrue();
    }

    [Fact]
    public void Equals_SameUrlDifferentCase_ReturnsTrue()
    {
        var url1 = NewsUrl.Create("https://EXAMPLE.com").Data!;
        var url2 = NewsUrl.Create("https://example.COM").Data!;

        url1.Equals(url2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentUrl_ReturnsFalse()
    {
        var url1 = NewsUrl.Create("https://example.com").Data!;
        var url2 = NewsUrl.Create("https://different.com").Data!;

        url1.Equals(url2).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameUrl_ReturnsSameHash()
    {
        var url1 = NewsUrl.Create("https://example.com").Data!;
        var url2 = NewsUrl.Create("https://example.com").Data!;

        url1.GetHashCode().Should().Be(url2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_SameUrlDifferentCase_ReturnsSameHash()
    {
        var url1 = NewsUrl.Create("https://EXAMPLE.com").Data!;
        var url2 = NewsUrl.Create("https://example.com").Data!;

        url1.GetHashCode().Should().Be(url2.GetHashCode());
    }
}
