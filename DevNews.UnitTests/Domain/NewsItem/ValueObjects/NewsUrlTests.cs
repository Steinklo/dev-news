using DevNews.Domain.NewsItem.ValueObjects;

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

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhitespace_ReturnsFailure(string? url)
    {
        var result = NewsUrl.Create(url!);

        Assert.False(result.IsSuccess);
        Assert.Equal("URL cannot be empty", result.ErrorMessage);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("just some text")]
    [InlineData("example.com")]
    public void Create_InvalidUrl_ReturnsFailure(string url)
    {
        var result = NewsUrl.Create(url);

        Assert.False(result.IsSuccess);
        Assert.Equal("URL must be a valid absolute URL", result.ErrorMessage);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///path/to/file")]
    [InlineData("mailto:test@example.com")]
    public void Create_NonHttpScheme_ReturnsFailure(string url)
    {
        var result = NewsUrl.Create(url);

        Assert.False(result.IsSuccess);
        Assert.Equal("URL must use HTTP or HTTPS protocol", result.ErrorMessage);
    }

    [Fact]
    public void Create_RemovesTrailingSlash()
    {
        var result = NewsUrl.Create("https://example.com/");

        Assert.True(result.IsSuccess);
        Assert.Equal("https://example.com", result.Data!.Value);
    }

    [Fact]
    public void Create_PreservesPathWithoutTrailingSlash()
    {
        var result = NewsUrl.Create("https://example.com/path/");

        Assert.True(result.IsSuccess);
        Assert.Equal("https://example.com/path", result.Data!.Value);
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var result = NewsUrl.Create("https://example.com/article");
        string value = result.Data!;

        Assert.Equal("https://example.com/article", value);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var result = NewsUrl.Create("https://example.com");

        Assert.Equal("https://example.com", result.Data!.ToString());
    }

    [Fact]
    public void Equals_SameUrl_ReturnsTrue()
    {
        var url1 = NewsUrl.Create("https://example.com").Data!;
        var url2 = NewsUrl.Create("https://example.com").Data!;

        Assert.True(url1.Equals(url2));
    }

    [Fact]
    public void Equals_SameUrlDifferentCase_ReturnsTrue()
    {
        var url1 = NewsUrl.Create("https://EXAMPLE.com").Data!;
        var url2 = NewsUrl.Create("https://example.COM").Data!;

        Assert.True(url1.Equals(url2));
    }

    [Fact]
    public void Equals_DifferentUrl_ReturnsFalse()
    {
        var url1 = NewsUrl.Create("https://example.com").Data!;
        var url2 = NewsUrl.Create("https://different.com").Data!;

        Assert.False(url1.Equals(url2));
    }

    [Fact]
    public void GetHashCode_SameUrl_ReturnsSameHash()
    {
        var url1 = NewsUrl.Create("https://example.com").Data!;
        var url2 = NewsUrl.Create("https://example.com").Data!;

        Assert.Equal(url1.GetHashCode(), url2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_SameUrlDifferentCase_ReturnsSameHash()
    {
        var url1 = NewsUrl.Create("https://EXAMPLE.com").Data!;
        var url2 = NewsUrl.Create("https://example.com").Data!;

        Assert.Equal(url1.GetHashCode(), url2.GetHashCode());
    }
}
