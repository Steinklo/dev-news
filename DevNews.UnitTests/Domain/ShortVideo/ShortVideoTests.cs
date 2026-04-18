using DevNews.Domain.Common.Enums;
using DevNews.Domain.ShortVideo.Enums;
using DevNews.Domain.ShortVideo.Events;

namespace DevNews.UnitTests.Domain.ShortVideo;

public class ShortVideoTests
{
    private static readonly Guid ValidNewsItemId = Guid.NewGuid();
    private static readonly string ValidScript = new('a', 200); // Min length
    private const int ValidDuration = 30;
    private const string ValidVideoUrl = "https://storage.blob.core.windows.net/videos/test.mp4";

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = DevNews.Domain.ShortVideo.ShortVideo.Create(
            ValidNewsItemId, ValidScript, ValidDuration, ValidVideoUrl);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public void Create_SetsPropertiesCorrectly()
    {
        var result = DevNews.Domain.ShortVideo.ShortVideo.Create(
            ValidNewsItemId, ValidScript, ValidDuration, ValidVideoUrl);

        var video = result.Data!;
        Assert.Equal(ValidNewsItemId, video.NewsItemId);
        Assert.Equal(ValidScript, video.Script.Value);
        Assert.Equal(ValidDuration, video.Duration.Seconds);
        Assert.Equal(ValidVideoUrl, video.VideoUrl.Value);
        Assert.Null(video.ThumbnailUrl);
        Assert.Equal(VideoStatus.VideoGenerated, video.Status);
        Assert.Empty(video.Publications);
        Assert.NotEqual(Guid.Empty, video.Id);
    }

    [Fact]
    public void Create_RaisesVideoCreatedEvent()
    {
        var result = DevNews.Domain.ShortVideo.ShortVideo.Create(
            ValidNewsItemId, ValidScript, ValidDuration, ValidVideoUrl);

        Assert.Single(result.Data!.DomainEvents);
        Assert.IsType<VideoCreatedEvent>(result.Data.DomainEvents.First());
    }

    [Fact]
    public void Create_WithThumbnail_SetsThumbnailUrl()
    {
        var thumbnailUrl = "https://storage.blob.core.windows.net/thumbnails/test.jpg";

        var result = DevNews.Domain.ShortVideo.ShortVideo.Create(
            ValidNewsItemId, ValidScript, ValidDuration, ValidVideoUrl, thumbnailUrl);

        Assert.True(result.IsSuccess);
        Assert.Equal(thumbnailUrl, result.Data!.ThumbnailUrl!.Value);
    }

    [Fact]
    public void Create_WithEmptyNewsItemId_ReturnsFailure()
    {
        var result = DevNews.Domain.ShortVideo.ShortVideo.Create(
            Guid.Empty, ValidScript, ValidDuration, ValidVideoUrl);

        Assert.False(result.IsSuccess);
        Assert.Contains("NewsItemId", result.ErrorMessage);
    }

    [Fact]
    public void Create_WithInvalidScript_ReturnsFailure()
    {
        var result = DevNews.Domain.ShortVideo.ShortVideo.Create(
            ValidNewsItemId, "too short", ValidDuration, ValidVideoUrl);

        Assert.False(result.IsSuccess);
        Assert.Contains("Script", result.ErrorMessage);
    }

    [Fact]
    public void Create_WithInvalidDuration_ReturnsFailure()
    {
        var result = DevNews.Domain.ShortVideo.ShortVideo.Create(
            ValidNewsItemId, ValidScript, 5, ValidVideoUrl); // Below min 15s

        Assert.False(result.IsSuccess);
        Assert.Contains("15", result.ErrorMessage);
    }

    [Fact]
    public void Create_WithInvalidVideoUrl_ReturnsFailure()
    {
        var result = DevNews.Domain.ShortVideo.ShortVideo.Create(
            ValidNewsItemId, ValidScript, ValidDuration, "not-a-url");

        Assert.False(result.IsSuccess);
        Assert.Contains("URL", result.ErrorMessage);
    }

    [Fact]
    public void AddPublication_ValidData_ReturnsSuccess()
    {
        var video = DevNews.Domain.ShortVideo.ShortVideo.Create(
            ValidNewsItemId, ValidScript, ValidDuration, ValidVideoUrl).Data!;

        var result = video.AddPublication(
            Platform.YouTube, "abc123", "https://youtube.com/shorts/abc123");

        Assert.True(result.IsSuccess);
        Assert.Single(video.Publications);
        Assert.Equal(VideoStatus.Published, video.Status);
    }

    [Fact]
    public void AddPublication_RaisesVideoPublishedEvent()
    {
        var video = DevNews.Domain.ShortVideo.ShortVideo.Create(
            ValidNewsItemId, ValidScript, ValidDuration, ValidVideoUrl).Data!;
        video.ClearDomainEvents();

        video.AddPublication(Platform.YouTube, "abc123", "https://youtube.com/shorts/abc123");

        Assert.Single(video.DomainEvents);
        Assert.IsType<VideoPublishedEvent>(video.DomainEvents.First());
    }

    [Fact]
    public void AddPublication_DuplicatePlatform_ReturnsFailure()
    {
        var video = DevNews.Domain.ShortVideo.ShortVideo.Create(
            ValidNewsItemId, ValidScript, ValidDuration, ValidVideoUrl).Data!;

        video.AddPublication(Platform.YouTube, "abc123", "https://youtube.com/shorts/abc123");
        var result = video.AddPublication(Platform.YouTube, "def456", "https://youtube.com/shorts/def456");

        Assert.False(result.IsSuccess);
        Assert.Contains("Already published", result.ErrorMessage);
    }

    [Fact]
    public void AddPublication_MultiplePlatforms_AllSucceed()
    {
        var video = DevNews.Domain.ShortVideo.ShortVideo.Create(
            ValidNewsItemId, ValidScript, ValidDuration, ValidVideoUrl).Data!;

        video.AddPublication(Platform.YouTube, "yt123", "https://youtube.com/shorts/yt123");
        video.AddPublication(Platform.LinkedIn, "li123", "https://linkedin.com/feed/li123");

        Assert.Equal(2, video.Publications.Count);
    }

    [Fact]
    public void MarkFailed_SetsStatusToFailed()
    {
        var video = DevNews.Domain.ShortVideo.ShortVideo.Create(
            ValidNewsItemId, ValidScript, ValidDuration, ValidVideoUrl).Data!;

        video.MarkFailed();

        Assert.Equal(VideoStatus.Failed, video.Status);
        Assert.NotNull(video.UpdatedAt);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var video = DevNews.Domain.ShortVideo.ShortVideo.Create(
            ValidNewsItemId, ValidScript, ValidDuration, ValidVideoUrl).Data!;

        video.ClearDomainEvents();

        Assert.Empty(video.DomainEvents);
    }
}
