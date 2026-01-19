using DevNews.Domain.Common;
using DevNews.Domain.VideoContent.Enums;
using DevNews.Domain.VideoContent.Events;
using DevNews.Domain.VideoContent.ValueObjects;

namespace DevNews.Domain.VideoContent;

/// <summary>
/// VideoContent Aggregate Root - represents a video generated from a NewsItem.
/// Lifecycle: Draft → VideoGenerated → AIValidated → Published (or Rejected/Failed).
/// Videos are created for social media platforms (TikTok, Instagram, YouTube Shorts, Twitter/X).
/// </summary>
public class VideoContent : AggregateRoot<Guid>
{
    private readonly List<DomainEvent> _domainEvents = new();
    private readonly List<PlatformPublishResult> _publishResults = new();

    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    // Reference to source NewsItem
    public Guid NewsItemId { get; private set; }

    // Video content
    public VideoScript Script { get; private set; } = null!;
    public VideoDuration Duration { get; private set; } = null!;

    // Status tracking
    public VideoStatusEnum Status { get; private set; }
    public string? BlobStorageUrl { get; private set; }
    public string? ValidationReason { get; private set; }

    // Publishing tracking
    public IReadOnlyCollection<PlatformPublishResult> PublishResults => _publishResults.AsReadOnly();

    // Timestamps
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ValidatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    private VideoContent(
        Guid id,
        Guid newsItemId,
        VideoScript script,
        VideoDuration duration) : base(id)
    {
        NewsItemId = newsItemId;
        Script = script;
        Duration = duration;
        Status = VideoStatusEnum.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new VideoContent with a script.
    /// </summary>
    public static ResultResponse<VideoContent> Create(
        Guid newsItemId,
        string scriptContent,
        int durationSeconds)
    {
        if (newsItemId == Guid.Empty)
            return ResultResponse<VideoContent>.Failure("NewsItemId cannot be empty");

        var scriptResult = VideoScript.Create(scriptContent);
        if (!scriptResult.IsSuccess)
            return ResultResponse<VideoContent>.Failure(scriptResult.ErrorMessage);

        var durationResult = VideoDuration.Create(durationSeconds);
        if (!durationResult.IsSuccess)
            return ResultResponse<VideoContent>.Failure(durationResult.ErrorMessage);

        var videoContent = new VideoContent(
            id: Guid.CreateVersion7(),
            newsItemId: newsItemId,
            script: scriptResult.Data!,
            duration: durationResult.Data!);

        videoContent._domainEvents.Add(new VideoGenerationRequestedEvent(newsItemId));

        return ResultResponse<VideoContent>.Success(videoContent);
    }

    /// <summary>
    /// Marks the video as generated and sets the blob storage URL.
    /// </summary>
    public ResultResponse<bool> SetBlobUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return ResultResponse<bool>.Failure("Blob storage URL cannot be empty");

        if (Status != VideoStatusEnum.Draft)
            return ResultResponse<bool>.Failure($"Cannot set blob URL when status is {Status}");

        BlobStorageUrl = url;
        Status = VideoStatusEnum.VideoGenerated;

        return ResultResponse<bool>.Success(true);
    }

    /// <summary>
    /// Marks the video as validated by AI.
    /// </summary>
    public ResultResponse<bool> MarkAsValidated(string? validationReason = null)
    {
        if (Status != VideoStatusEnum.VideoGenerated)
            return ResultResponse<bool>.Failure($"Cannot validate when status is {Status}. Expected VideoGenerated.");

        if (string.IsNullOrWhiteSpace(BlobStorageUrl))
            return ResultResponse<bool>.Failure("Cannot validate video without blob storage URL");

        Status = VideoStatusEnum.AIValidated;
        ValidationReason = validationReason ?? "Passed all validation criteria";
        ValidatedAt = DateTimeOffset.UtcNow;

        _domainEvents.Add(new VideoValidatedEvent(Id, isValid: true, ValidationReason));

        return ResultResponse<bool>.Success(true);
    }

    /// <summary>
    /// Marks the video as rejected by AI validation.
    /// </summary>
    public ResultResponse<bool> MarkAsRejected(string rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
            return ResultResponse<bool>.Failure("Rejection reason is required");

        if (Status != VideoStatusEnum.VideoGenerated)
            return ResultResponse<bool>.Failure($"Cannot reject when status is {Status}. Expected VideoGenerated.");

        Status = VideoStatusEnum.Rejected;
        ValidationReason = rejectionReason;
        ValidatedAt = DateTimeOffset.UtcNow;

        _domainEvents.Add(new VideoValidatedEvent(Id, isValid: false, rejectionReason));

        return ResultResponse<bool>.Success(true);
    }

    /// <summary>
    /// Marks the video as failed due to technical error.
    /// </summary>
    public ResultResponse<bool> MarkAsFailed(string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            return ResultResponse<bool>.Failure("Failure reason is required");

        Status = VideoStatusEnum.Failed;
        ValidationReason = failureReason;

        return ResultResponse<bool>.Success(true);
    }

    /// <summary>
    /// Adds a publish result for a platform.
    /// </summary>
    public ResultResponse<bool> AddPublishResult(
        PublishPlatformEnum platform,
        bool success,
        string? externalId,
        string? errorMessage)
    {
        if (Status != VideoStatusEnum.AIValidated && Status != VideoStatusEnum.Published)
            return ResultResponse<bool>.Failure($"Cannot add publish result when status is {Status}");

        // Check if already published to this platform
        if (_publishResults.Any(r => r.Platform == platform))
            return ResultResponse<bool>.Failure($"Already published to {platform}");

        var result = success
            ? PlatformPublishResult.CreateSuccess(platform, externalId!)
            : PlatformPublishResult.CreateFailure(platform, errorMessage!);

        _publishResults.Add(result);

        return ResultResponse<bool>.Success(true);
    }

    /// <summary>
    /// Marks the video as published after all platform publish attempts.
    /// </summary>
    public ResultResponse<bool> MarkAsPublished()
    {
        if (Status != VideoStatusEnum.AIValidated && Status != VideoStatusEnum.Published)
            return ResultResponse<bool>.Failure($"Cannot mark as published when status is {Status}");

        if (_publishResults.Count == 0)
            return ResultResponse<bool>.Failure("Cannot mark as published without any publish results");

        Status = VideoStatusEnum.Published;
        PublishedAt = DateTimeOffset.UtcNow;

        var successCount = _publishResults.Count(r => r.Success);
        _domainEvents.Add(new VideoPublishedEvent(Id, successCount));

        return ResultResponse<bool>.Success(true);
    }

    /// <summary>
    /// Reconstitutes a VideoContent from persistence. Bypasses validation.
    /// </summary>
    internal static VideoContent Reconstitute(
        Guid id,
        Guid newsItemId,
        string script,
        int durationSeconds,
        VideoStatusEnum status,
        string? blobStorageUrl,
        string? validationReason,
        IEnumerable<PlatformPublishResult>? publishResults,
        DateTimeOffset createdAt,
        DateTimeOffset? validatedAt,
        DateTimeOffset? publishedAt)
    {
        var videoContent = new VideoContent(
            id: id,
            newsItemId: newsItemId,
            script: VideoScript.Reconstitute(script),
            duration: VideoDuration.Reconstitute(durationSeconds));

        videoContent.Status = status;
        videoContent.BlobStorageUrl = blobStorageUrl;
        videoContent.ValidationReason = validationReason;

        if (publishResults != null)
            videoContent._publishResults.AddRange(publishResults);

        videoContent.CreatedAt = createdAt;
        videoContent.ValidatedAt = validatedAt;
        videoContent.PublishedAt = publishedAt;

        return videoContent;
    }
}
