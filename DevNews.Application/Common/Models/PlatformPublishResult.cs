namespace DevNews.Application.Common.Models;

/// <summary>
/// Result of publishing a video to a social media platform.
/// </summary>
public record PlatformPublishResult(
    string ExternalId,
    string PublishedUrl);
