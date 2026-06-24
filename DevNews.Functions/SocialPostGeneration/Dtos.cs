using System.Text.Json.Serialization;

namespace DevNews.Functions.SocialPostGeneration;

public record SocialPostGenerationResult(
    [property: JsonPropertyName("eligibleItems")] int EligibleItems,
    [property: JsonPropertyName("postsPublished")] int PostsPublished,
    [property: JsonPropertyName("duration")] TimeSpan Duration);

public record PersistSocialPostInput(
    Guid NewsItemId,
    string Content,
    string? SourceUrl,
    string? ExternalId,
    string? PublishedUrl,
    bool Published);

public record SocialPostPublishOutput(
    string ExternalId,
    string PublishedUrl);
