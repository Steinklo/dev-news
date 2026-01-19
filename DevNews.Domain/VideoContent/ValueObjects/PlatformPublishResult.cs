using DevNews.Domain.Common;
using DevNews.Domain.VideoContent.Enums;

namespace DevNews.Domain.VideoContent.ValueObjects;

/// <summary>
/// Value object representing the result of publishing to a single platform.
/// </summary>
public class PlatformPublishResult : ValueObject
{
    public PublishPlatformEnum Platform { get; private set; }
    public bool Success { get; private set; }
    public string? ExternalId { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }

    private PlatformPublishResult(
        PublishPlatformEnum platform,
        bool success,
        string? externalId,
        string? errorMessage,
        DateTimeOffset publishedAt)
    {
        Platform = platform;
        Success = success;
        ExternalId = externalId;
        ErrorMessage = errorMessage;
        PublishedAt = publishedAt;
    }

    public static PlatformPublishResult CreateSuccess(
        PublishPlatformEnum platform,
        string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("External ID is required for successful publish", nameof(externalId));

        return new PlatformPublishResult(
            platform: platform,
            success: true,
            externalId: externalId,
            errorMessage: null,
            publishedAt: DateTimeOffset.UtcNow);
    }

    public static PlatformPublishResult CreateFailure(
        PublishPlatformEnum platform,
        string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message is required for failed publish", nameof(errorMessage));

        return new PlatformPublishResult(
            platform: platform,
            success: false,
            externalId: null,
            errorMessage: errorMessage,
            publishedAt: DateTimeOffset.UtcNow);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Platform;
        yield return Success;
        yield return ExternalId ?? string.Empty;
        yield return ErrorMessage ?? string.Empty;
    }

    internal static PlatformPublishResult Reconstitute(
        PublishPlatformEnum platform,
        bool success,
        string? externalId,
        string? errorMessage,
        DateTimeOffset publishedAt)
    {
        return new PlatformPublishResult(platform, success, externalId, errorMessage, publishedAt);
    }
}
