using DevNews.Domain.ShortVideo.Enums;
using DevNews.Domain.ShortVideo.ValueObjects;

namespace DevNews.UnitTests.Domain.ShortVideo.ValueObjects;

public class PlatformPublicationTests
{
    [Fact]
    public void Create_ValidData_ReturnsSuccess()
    {
        var result = PlatformPublication.Create(
            Platform.YouTube, "abc123", "https://youtube.com/shorts/abc123");

        Assert.True(result.IsSuccess);
        Assert.Equal(Platform.YouTube, result.Data!.Platform);
        Assert.Equal("abc123", result.Data.ExternalId);
        Assert.Equal("https://youtube.com/shorts/abc123", result.Data.PublishedUrl);
        Assert.True(Math.Abs((result.Data.PublishedAt - DateTimeOffset.UtcNow).TotalSeconds) < 1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyExternalId_ReturnsFailure(string? externalId)
    {
        var result = PlatformPublication.Create(
            Platform.YouTube, externalId!, "https://youtube.com/shorts/abc");

        Assert.False(result.IsSuccess);
        Assert.Contains("External ID", result.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyPublishedUrl_ReturnsFailure(string? publishedUrl)
    {
        var result = PlatformPublication.Create(
            Platform.YouTube, "abc123", publishedUrl!);

        Assert.False(result.IsSuccess);
        Assert.Contains("Published URL", result.ErrorMessage);
    }

    [Theory]
    [InlineData(Platform.YouTube)]
    [InlineData(Platform.LinkedIn)]
    [InlineData(Platform.Twitter)]
    public void Create_AllPlatforms_AreValid(Platform platform)
    {
        var result = PlatformPublication.Create(
            platform, "id123", "https://example.com/post/123");

        Assert.True(result.IsSuccess);
        Assert.Equal(platform, result.Data!.Platform);
    }

    [Fact]
    public void Equals_SamePlatformAndId_ReturnsTrue()
    {
        var pub1 = PlatformPublication.Create(Platform.YouTube, "abc", "https://a.com").Data!;
        var pub2 = PlatformPublication.Create(Platform.YouTube, "abc", "https://b.com").Data!;

        Assert.True(pub1.Equals(pub2));
    }

    [Fact]
    public void Equals_DifferentPlatform_ReturnsFalse()
    {
        var pub1 = PlatformPublication.Create(Platform.YouTube, "abc", "https://a.com").Data!;
        var pub2 = PlatformPublication.Create(Platform.LinkedIn, "abc", "https://a.com").Data!;

        Assert.False(pub1.Equals(pub2));
    }
}
