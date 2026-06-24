using DevNews.Domain.Common;
using DevNews.Domain.Common.Enums;

namespace DevNews.Domain.ShortVideo.ValueObjects;

/// <summary>
/// Value object representing a publication to a specific social media platform.
/// Tracks the external ID and URL for each platform where the video was published.
/// </summary>
public class PlatformPublication : ValueObject
{
    public Platform Platform { get; private set; }
    public string ExternalId { get; private set; }
    public string PublishedUrl { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }

    private PlatformPublication(Platform platform, string externalId, string publishedUrl, DateTimeOffset publishedAt)
    {
        Platform = platform;
        ExternalId = externalId;
        PublishedUrl = publishedUrl;
        PublishedAt = publishedAt;
    }

    public static ResultResponse<PlatformPublication> Create(
        Platform platform,
        string externalId,
        string publishedUrl)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return ResultResponse<PlatformPublication>.Failure("External ID cannot be empty");

        if (string.IsNullOrWhiteSpace(publishedUrl))
            return ResultResponse<PlatformPublication>.Failure("Published URL cannot be empty");

        return ResultResponse<PlatformPublication>.Success(
            new PlatformPublication(platform, externalId.Trim(), publishedUrl.Trim(), DateTimeOffset.UtcNow));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Platform;
        yield return ExternalId;
    }

    internal static PlatformPublication Reconstitute(
        Platform platform,
        string externalId,
        string publishedUrl,
        DateTimeOffset publishedAt) => new(platform, externalId, publishedUrl, publishedAt);
}
