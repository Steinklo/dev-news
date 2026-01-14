using System.Text.Json.Serialization;

namespace DevNews.Functions.NightlyCrawl;

/// <summary>
/// Result of the nightly crawl orchestration
/// </summary>
public record NightlyCrawlResult(
    [property: JsonPropertyName("discovered")] int Discovered,
    [property: JsonPropertyName("curated")] int Curated,
    [property: JsonPropertyName("duplicates")] int Duplicates,
    [property: JsonPropertyName("persisted")] int Persisted,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("duration")] TimeSpan Duration);
