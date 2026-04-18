using System.Text.Json.Serialization;
using DevNews.Domain.Common.Enums;

namespace DevNews.Functions.SocialPostGeneration;

public record SocialPostGenerationResult(
    [property: JsonPropertyName("eligibleItems")] int EligibleItems,
    [property: JsonPropertyName("postsPublished")] int PostsPublished,
    [property: JsonPropertyName("videoGenerated")] bool VideoGenerated,
    [property: JsonPropertyName("videoPublished")] bool VideoPublished,
    [property: JsonPropertyName("duration")] TimeSpan Duration);

public record SocialPostPublishInput(
    string Text,
    Platform Platform);

public record PersistSocialPostInput(
    Guid NewsItemId,
    string Content,
    string? SourceUrl,
    string? ExternalId,
    string? PublishedUrl);

public record SocialPostPublishOutput(
    string ExternalId,
    string PublishedUrl);
