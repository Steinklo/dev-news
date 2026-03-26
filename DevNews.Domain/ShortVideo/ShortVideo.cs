using DevNews.Domain.Common;
using DevNews.Domain.ShortVideo.Enums;
using DevNews.Domain.ShortVideo.Events;
using DevNews.Domain.ShortVideo.ValueObjects;

namespace DevNews.Domain.ShortVideo;

/// <summary>
/// ShortVideo Aggregate Root - represents a generated short-form video for a news item.
/// Lifecycle: Draft → ScriptGenerated → VideoGenerated → Published | Failed
/// References a NewsItem but has its own lifecycle.
/// </summary>
public class ShortVideo : AggregateRoot<Guid>
{
    private readonly List<DomainEvent> _domainEvents = new();
    private readonly List<PlatformPublication> _publications = new();

    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    // Reference to the source news item
    public Guid NewsItemId { get; private set; }

    // Script and video content
    public VideoScript Script { get; private set; } = null!;
    public VideoDuration Duration { get; private set; } = null!;
    public VideoAssetUrl VideoUrl { get; private set; } = null!;
    public VideoAssetUrl? ThumbnailUrl { get; private set; }

    // Status tracking
    public VideoStatus Status { get; private set; }

    // Platform publications
    public IReadOnlyCollection<PlatformPublication> Publications => _publications.AsReadOnly();

    // Timestamps
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private ShortVideo(
        Guid id,
        Guid newsItemId,
        VideoScript script,
        VideoDuration duration,
        VideoAssetUrl videoUrl,
        VideoAssetUrl? thumbnailUrl,
        VideoStatus status) : base(id)
    {
        NewsItemId = newsItemId;
        Script = script;
        Duration = duration;
        VideoUrl = videoUrl;
        ThumbnailUrl = thumbnailUrl;
        Status = status;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Factory method to create a ShortVideo after video generation is complete.
    /// </summary>
    public static ResultResponse<ShortVideo> Create(
        Guid newsItemId,
        string script,
        int durationSeconds,
        string videoUrl,
        string? thumbnailUrl = null)
    {
        if (newsItemId == Guid.Empty)
            return ResultResponse<ShortVideo>.Failure("NewsItemId cannot be empty");

        var scriptResult = VideoScript.Create(script);
        if (!scriptResult.IsSuccess)
            return ResultResponse<ShortVideo>.Failure(scriptResult.ErrorMessage);

        var durationResult = VideoDuration.Create(durationSeconds);
        if (!durationResult.IsSuccess)
            return ResultResponse<ShortVideo>.Failure(durationResult.ErrorMessage);

        var videoUrlResult = VideoAssetUrl.Create(videoUrl);
        if (!videoUrlResult.IsSuccess)
            return ResultResponse<ShortVideo>.Failure(videoUrlResult.ErrorMessage);

        VideoAssetUrl? thumbnailAsset = null;
        if (thumbnailUrl != null)
        {
            var thumbnailResult = VideoAssetUrl.Create(thumbnailUrl);
            if (!thumbnailResult.IsSuccess)
                return ResultResponse<ShortVideo>.Failure(thumbnailResult.ErrorMessage);
            thumbnailAsset = thumbnailResult.Data;
        }

        var shortVideo = new ShortVideo(
            id: Guid.CreateVersion7(),
            newsItemId: newsItemId,
            script: scriptResult.Data!,
            duration: durationResult.Data!,
            videoUrl: videoUrlResult.Data!,
            thumbnailUrl: thumbnailAsset,
            status: VideoStatus.VideoGenerated);

        shortVideo._domainEvents.Add(new VideoCreatedEvent(shortVideo));

        return ResultResponse<ShortVideo>.Success(shortVideo);
    }

    /// <summary>
    /// Records a successful publication to a platform.
    /// </summary>
    public ResultResponse<PlatformPublication> AddPublication(
        Platform platform,
        string externalId,
        string publishedUrl)
    {
        if (_publications.Any(p => p.Platform == platform))
            return ResultResponse<PlatformPublication>.Failure($"Already published to {platform}");

        var publicationResult = PlatformPublication.Create(platform, externalId, publishedUrl);
        if (!publicationResult.IsSuccess)
            return ResultResponse<PlatformPublication>.Failure(publicationResult.ErrorMessage);

        _publications.Add(publicationResult.Data!);
        Status = VideoStatus.Published;
        UpdatedAt = DateTimeOffset.UtcNow;

        _domainEvents.Add(new VideoPublishedEvent(this, platform));

        return publicationResult;
    }

    /// <summary>
    /// Marks the video as failed.
    /// </summary>
    public void MarkFailed()
    {
        Status = VideoStatus.Failed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Reconstitutes a ShortVideo from persistence. Bypasses validation since data was already validated on creation.
    /// </summary>
    internal static ShortVideo Reconstitute(
        Guid id,
        Guid newsItemId,
        string script,
        int durationSeconds,
        string videoUrl,
        string? thumbnailUrl,
        VideoStatus status,
        IEnumerable<PlatformPublication>? publications,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt)
    {
        var shortVideo = new ShortVideo(
            id: id,
            newsItemId: newsItemId,
            script: VideoScript.Reconstitute(script),
            duration: VideoDuration.Reconstitute(durationSeconds),
            videoUrl: VideoAssetUrl.Reconstitute(videoUrl),
            thumbnailUrl: thumbnailUrl != null ? VideoAssetUrl.Reconstitute(thumbnailUrl) : null,
            status: status);

        if (publications != null)
            shortVideo._publications.AddRange(publications);

        shortVideo.CreatedAt = createdAt;
        shortVideo.UpdatedAt = updatedAt;

        return shortVideo;
    }
}
