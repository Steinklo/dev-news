using System.Text.Json.Serialization;

namespace DevNews.Functions.DailyVideo;

public record DailyVideoResult(
    [property: JsonPropertyName("eligibleItems")] int EligibleItems,
    [property: JsonPropertyName("videoGenerated")] bool VideoGenerated,
    [property: JsonPropertyName("videoPublished")] bool VideoPublished,
    [property: JsonPropertyName("duration")] TimeSpan Duration);
