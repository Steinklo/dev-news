namespace DevNews.Functions.NightlyCrawl;

/// <summary>
/// Result of the nightly crawl orchestration
/// </summary>
public record NightlyCrawlResult(
    int Discovered,
    int Curated,
    int Duplicates,
    int Persisted,
    int Failed,
    TimeSpan Duration);
