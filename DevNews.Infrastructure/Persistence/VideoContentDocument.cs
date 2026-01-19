using System.Text.Json.Serialization;
using DevNews.Domain.VideoContent.Enums;

namespace DevNews.Infrastructure.Persistence;

/// <summary>
/// Cosmos DB document representation of VideoContent aggregate.
/// </summary>
public class VideoContentDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("newsItemId")]
    public string NewsItemId { get; set; } = string.Empty;

    [JsonPropertyName("script")]
    public string Script { get; set; } = string.Empty;

    [JsonPropertyName("durationSeconds")]
    public int DurationSeconds { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("blobStorageUrl")]
    public string? BlobStorageUrl { get; set; }

    [JsonPropertyName("validationReason")]
    public string? ValidationReason { get; set; }

    [JsonPropertyName("publishResults")]
    public List<PublishResultDocument> PublishResults { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("validatedAt")]
    public DateTimeOffset? ValidatedAt { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTimeOffset? PublishedAt { get; set; }

    public static VideoContentDocument FromDomain(Domain.VideoContent.VideoContent videoContent)
    {
        return new VideoContentDocument
        {
            Id = videoContent.Id.ToString(),
            NewsItemId = videoContent.NewsItemId.ToString(),
            Script = videoContent.Script.Value,
            DurationSeconds = videoContent.Duration.Seconds,
            Status = videoContent.Status.ToString(),
            BlobStorageUrl = videoContent.BlobStorageUrl,
            ValidationReason = videoContent.ValidationReason,
            PublishResults = videoContent.PublishResults
                .Select(PublishResultDocument.FromDomain)
                .ToList(),
            CreatedAt = videoContent.CreatedAt,
            ValidatedAt = videoContent.ValidatedAt,
            PublishedAt = videoContent.PublishedAt
        };
    }

    public Domain.VideoContent.VideoContent ToDomain()
    {
        var videoContent = Domain.VideoContent.VideoContent.Reconstitute(
            id: Guid.Parse(Id),
            newsItemId: Guid.Parse(NewsItemId),
            script: Script,
            durationSeconds: DurationSeconds,
            status: Enum.Parse<VideoStatusEnum>(Status),
            blobStorageUrl: BlobStorageUrl,
            validationReason: ValidationReason,
            publishResults: PublishResults.Select(pr => pr.ToDomain()).ToList(),
            createdAt: CreatedAt,
            validatedAt: ValidatedAt,
            publishedAt: PublishedAt);

        return videoContent;
    }
}

public class PublishResultDocument
{
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTimeOffset PublishedAt { get; set; }

    public static PublishResultDocument FromDomain(Domain.VideoContent.ValueObjects.PlatformPublishResult result)
    {
        return new PublishResultDocument
        {
            Platform = result.Platform.ToString(),
            Success = result.Success,
            ExternalId = result.ExternalId,
            ErrorMessage = result.ErrorMessage,
            PublishedAt = result.PublishedAt
        };
    }

    public Domain.VideoContent.ValueObjects.PlatformPublishResult ToDomain()
    {
        return Domain.VideoContent.ValueObjects.PlatformPublishResult.Reconstitute(
            platform: Enum.Parse<PublishPlatformEnum>(Platform),
            success: Success,
            externalId: ExternalId,
            errorMessage: ErrorMessage,
            publishedAt: PublishedAt);
    }
}
