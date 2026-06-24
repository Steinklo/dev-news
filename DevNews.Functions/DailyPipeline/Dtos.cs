using DevNews.Functions.DailyVideo;
using DevNews.Functions.NightlyCrawl;
using DevNews.Functions.SocialPostGeneration;

namespace DevNews.Functions.DailyPipeline;

public record DailyPipelineResult(
    NightlyCrawlResult Crawl,
    SocialPostGenerationResult? SocialPosts,
    DailyVideoResult? DailyVideo,
    TimeSpan Duration);
