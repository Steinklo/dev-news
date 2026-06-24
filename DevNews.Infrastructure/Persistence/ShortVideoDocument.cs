using DevNews.Domain.Common.Enums;
using DevNews.Domain.ShortVideo;
using DevNews.Domain.ShortVideo.Enums;
using DevNews.Domain.ShortVideo.ValueObjects;

namespace DevNews.Infrastructure.Persistence;

/// <summary>
/// Cosmos DB persistence model for ShortVideo.
/// Keeps domain model clean from persistence concerns.
/// </summary>
public class ShortVideoDocument
{
    public string id { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string NewsItemId { get; set; } = null!;
    public string Script { get; set; } = null!;
    public int DurationSeconds { get; set; }
    public string VideoUrl { get; set; } = null!;
    public string? ThumbnailUrl { get; set; }
    public int Status { get; set; }
    public List<PlatformPublicationDocument> Publications { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public static ShortVideoDocument FromDomain(ShortVideo shortVideo)
    {
        var keyDate = shortVideo.CreatedAt;
        var partitionKey = $"{shortVideo.NewsItemId}_{keyDate:yyyy-MM}";

        return new ShortVideoDocument
        {
            id = shortVideo.Id.ToString(),
            Key = partitionKey,
            NewsItemId = shortVideo.NewsItemId.ToString(),
            Script = shortVideo.Script.Value,
            DurationSeconds = shortVideo.Duration.Seconds,
            VideoUrl = shortVideo.VideoUrl.Value,
            ThumbnailUrl = shortVideo.ThumbnailUrl?.Value,
            Status = (int)shortVideo.Status,
            Publications = shortVideo.Publications.Select(p => new PlatformPublicationDocument
            {
                Platform = (int)p.Platform,
                ExternalId = p.ExternalId,
                PublishedUrl = p.PublishedUrl,
                PublishedAt = p.PublishedAt
            }).ToList(),
            CreatedAt = shortVideo.CreatedAt,
            UpdatedAt = shortVideo.UpdatedAt
        };
    }

    public ShortVideo ToDomain()
    {
        var publications = Publications.Select(p =>
            PlatformPublication.Reconstitute(
                (Platform)p.Platform,
                p.ExternalId,
                p.PublishedUrl,
                p.PublishedAt));

        return ShortVideo.Reconstitute(
            id: Guid.Parse(id),
            newsItemId: Guid.Parse(NewsItemId),
            script: Script,
            durationSeconds: DurationSeconds,
            videoUrl: VideoUrl,
            thumbnailUrl: ThumbnailUrl,
            status: (VideoStatus)Status,
            publications: publications,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt);
    }
}

public class PlatformPublicationDocument
{
    public int Platform { get; set; }
    public string ExternalId { get; set; } = null!;
    public string PublishedUrl { get; set; } = null!;
    public DateTimeOffset PublishedAt { get; set; }
}
