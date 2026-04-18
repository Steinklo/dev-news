using DevNews.Functions.NightlyCrawl;
using DevNews.Functions.SocialPostGeneration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.DailyPipeline;

public class Orchestrator
{
    [Function(nameof(DailyPipelineOrchestrator))]
    public async Task<DailyPipelineResult> DailyPipelineOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<Orchestrator>();
        var startTime = context.CurrentUtcDateTime;

        logger.LogInformation("Starting daily pipeline orchestration");

        // Step 1: Run nightly crawl
        NightlyCrawlResult crawlResult;
        try
        {
            crawlResult = await context.CallSubOrchestratorAsync<NightlyCrawlResult>(
                nameof(NightlyCrawl.Orchestrator.NightlyCrawlOrchestrator), (object?)null);

            logger.LogInformation(
                "Crawl completed. Persisted: {Persisted}, Failed: {Failed}",
                crawlResult.Persisted, crawlResult.Failed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Crawl orchestration failed");
            return new DailyPipelineResult(
                new NightlyCrawlResult(0, 0, 0, 0, 0, 1, TimeSpan.Zero), null,
                context.CurrentUtcDateTime - startTime);
        }

        // Step 2: Run social post generation if crawl produced new items
        SocialPostGenerationResult? socialPostResult = null;

        if (crawlResult.Persisted > 0)
        {
            try
            {
                socialPostResult = await context.CallSubOrchestratorAsync<SocialPostGenerationResult>(
                    nameof(SocialPostGeneration.Orchestrator.SocialPostOrchestrator), (object?)null);

                logger.LogInformation(
                    "Social post generation completed. PostsPublished: {PostsPublished}",
                    socialPostResult.PostsPublished);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Social post generation failed, but crawl succeeded");
            }
        }

        var duration = context.CurrentUtcDateTime - startTime;

        logger.LogInformation("Daily pipeline completed in {Duration}", duration);

        return new DailyPipelineResult(crawlResult, socialPostResult, duration);
    }
}
