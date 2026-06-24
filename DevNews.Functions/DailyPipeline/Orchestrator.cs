using DevNews.Functions.DailyVideo;
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
                new NightlyCrawlResult(0, 0, 0, 0, 0, 1, TimeSpan.Zero), null, null,
                context.CurrentUtcDateTime - startTime);
        }

        SocialPostGenerationResult? socialPostResult = null;
        DailyVideoResult? dailyVideoResult = null;

        if (crawlResult.Persisted > 0)
        {
            // Step 2: Publish social posts for the top stories
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

            // Step 3: Generate the single daily video (independent of social posts)
            try
            {
                dailyVideoResult = await context.CallSubOrchestratorAsync<DailyVideoResult>(
                    nameof(DailyVideo.Orchestrator.DailyVideoOrchestrator), (object?)null);

                logger.LogInformation(
                    "Daily video completed. VideoPublished: {VideoPublished}",
                    dailyVideoResult.VideoPublished);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Daily video generation failed, but earlier stages succeeded");
            }
        }

        var duration = context.CurrentUtcDateTime - startTime;

        logger.LogInformation("Daily pipeline completed in {Duration}", duration);

        return new DailyPipelineResult(crawlResult, socialPostResult, dailyVideoResult, duration);
    }
}
