using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

/// <summary>
/// Service for publishing videos to social media platforms.
/// </summary>
public interface IVideoPublishingService
{
    Task<ResultResponse<PublishResult>> PublishToTwitterAsync(string videoUrl, string caption, CancellationToken ct = default);
    Task<ResultResponse<PublishResult>> PublishToInstagramAsync(string videoUrl, string caption, CancellationToken ct = default);
    Task<ResultResponse<PublishResult>> PublishToTikTokAsync(string videoUrl, string caption, CancellationToken ct = default);
    Task<ResultResponse<PublishResult>> PublishToYouTubeShortsAsync(string videoUrl, string title, string description, CancellationToken ct = default);
}

/// <summary>
/// Result of publishing to a platform.
/// </summary>
public record PublishResult(bool Success, string? ExternalId, string? ErrorMessage);
