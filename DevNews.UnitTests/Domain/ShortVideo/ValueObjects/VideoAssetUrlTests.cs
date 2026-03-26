using DevNews.Domain.ShortVideo.ValueObjects;

namespace DevNews.UnitTests.Domain.ShortVideo.ValueObjects;

public class VideoAssetUrlTests
{
    [Fact]
    public void Create_ValidHttpsUrl_ReturnsSuccess()
    {
        var url = "https://storage.blob.core.windows.net/videos/test.mp4";

        var result = VideoAssetUrl.Create(url);

        Assert.True(result.IsSuccess);
        Assert.Equal(url, result.Data!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhitespace_ReturnsFailure(string? url)
    {
        var result = VideoAssetUrl.Create(url!);

        Assert.False(result.IsSuccess);
        Assert.Equal("Asset URL cannot be empty", result.ErrorMessage);
    }

    [Fact]
    public void Create_InvalidUrl_ReturnsFailure()
    {
        var result = VideoAssetUrl.Create("not-a-url");

        Assert.False(result.IsSuccess);
        Assert.Contains("valid absolute URL", result.ErrorMessage);
    }

    [Fact]
    public void Create_HttpUrl_ReturnsFailure()
    {
        var result = VideoAssetUrl.Create("http://example.com/video.mp4");

        Assert.False(result.IsSuccess);
        Assert.Contains("HTTPS", result.ErrorMessage);
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var url = "https://example.com/video.mp4";
        var result = VideoAssetUrl.Create(url);
        string value = result.Data!;

        Assert.Equal(url, value);
    }
}
