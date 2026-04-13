using DevNews.Functions.NightlyCrawl;
using DevNews.Functions.VideoGeneration;

namespace DevNews.Functions.DailyPipeline;

public record DailyPipelineResult(
    NightlyCrawlResult Crawl,
    VideoGenerationResult? Video,
    TimeSpan Duration);
