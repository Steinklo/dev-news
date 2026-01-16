using System.Text.Json.Serialization;

namespace DevNews.Functions.NightlyCrawl;

/// <summary>
/// Result of the nightly crawl orchestration
/// </summary>
public record NightlyCrawlResult(
    [property: JsonPropertyName("discovered")] int Discovered,
    [property: JsonPropertyName("curated")] int Curated,
    [property: JsonPropertyName("filteredLowRelevance")] int FilteredLowRelevance,
    [property: JsonPropertyName("duplicates")] int Duplicates,
    [property: JsonPropertyName("persisted")] int Persisted,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("duration")] TimeSpan Duration);

/// <summary>
/// Configuration for crawl filtering thresholds
/// </summary>
public static class CrawlThresholds
{
    /// <summary>
    /// Minimum relevance score (0-100) for an article to be considered for deduplication and storage.
    /// Articles below this threshold are filtered out early to save API costs.
    /// Per CLAUDE.md: 90+ = critical, 70-89 = important, 50-69 = notable, &lt;50 = reject
    /// </summary>
    public const int MinRelevanceScore = 50;
}
