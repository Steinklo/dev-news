using System.Text.Json.Serialization;

namespace DevNews.Application.VideoContent.Dtos;

/// <summary>
/// DTO for VideoContent returned from queries.
/// </summary>
public record VideoContentDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("newsItemId")] string NewsItemId,
    [property: JsonPropertyName("script")] string Script,
    [property: JsonPropertyName("durationSeconds")] int DurationSeconds,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("videoUrl")] string? VideoUrl,
    [property: JsonPropertyName("validationReason")] string? ValidationReason,
    [property: JsonPropertyName("publishResults")] IReadOnlyList<PublishResultDto> PublishResults,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("validatedAt")] DateTimeOffset? ValidatedAt,
    [property: JsonPropertyName("publishedAt")] DateTimeOffset? PublishedAt)
{
    public static VideoContentDto FromDomain(Domain.VideoContent.VideoContent videoContent)
    {
        return new VideoContentDto(
            Id: videoContent.Id.ToString(),
            NewsItemId: videoContent.NewsItemId.ToString(),
            Script: videoContent.Script.Value,
            DurationSeconds: videoContent.Duration.Seconds,
            Status: videoContent.Status.ToString(),
            VideoUrl: videoContent.BlobStorageUrl,
            ValidationReason: videoContent.ValidationReason,
            PublishResults: videoContent.PublishResults
                .Select(pr => new PublishResultDto(
                    Platform: pr.Platform.ToString(),
                    Success: pr.Success,
                    ExternalId: pr.ExternalId,
                    Error: pr.ErrorMessage))
                .ToList(),
            CreatedAt: videoContent.CreatedAt,
            ValidatedAt: videoContent.ValidatedAt,
            PublishedAt: videoContent.PublishedAt);
    }
}

/// <summary>
/// DTO for platform publish result.
/// </summary>
public record PublishResultDto(
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("externalId")] string? ExternalId,
    [property: JsonPropertyName("error")] string? Error);
