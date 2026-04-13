using DevNews.Application.Common.Models;
using DevNews.Application.ShortVideo.Dtos;
using DevNews.Domain.ShortVideo.Enums;
using DevNews.Functions.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.VideoGeneration;

public class Orchestrator
{
    private static readonly Platform[] TargetPlatforms = [Platform.YouTube, Platform.LinkedIn];

    [Function(nameof(VideoGenerationOrchestrator))]
    public async Task<VideoGenerationResult> VideoGenerationOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<Orchestrator>();
        var startTime = context.CurrentUtcDateTime;

        var scriptsGenerated = 0;
        var videosGenerated = 0;
        var published = 0;
        var failed = 0;

        logger.LogInformation("Starting video generation orchestration");

        // Step 1: Select eligible news items
        List<VideoEligibleItem> eligibleItems;
        try
        {
            eligibleItems = await context.CallActivityAsync<List<VideoEligibleItem>>(
                nameof(Activities.SelectEligibleItemsActivity),
                null,
                OrchestrationDefaults.RetryOptions);

            logger.LogInformation("Found {Count} eligible items for video generation", eligibleItems.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Selection failed");
            return new VideoGenerationResult(0, 0, 0, 0, 1, TimeSpan.Zero);
        }

        if (eligibleItems.Count == 0)
        {
            logger.LogInformation("No eligible items, ending orchestration");
            return new VideoGenerationResult(0, 0, 0, 0, 0, context.CurrentUtcDateTime - startTime);
        }

        // Step 2: Process each item sequentially (rate limiting for AI calls)
        foreach (var item in eligibleItems)
        {
            // Step 2a: Generate script
            var script = await context.CallActivityAsync<string?>(
                nameof(Activities.GenerateScriptActivity),
                new ScriptGenerationInput(item.NewsItemId, item.Title, item.Summary, item.Category, item.Tags),
                OrchestrationDefaults.RetryOptions);

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);

            if (script == null)
            {
                failed++;
                continue;
            }

            // Step 2b: Validate script
            var validationResult = await context.CallActivityAsync<ScriptValidationResult?>(
                nameof(Activities.ValidateScriptActivity),
                new ScriptValidationInput(script, item.Summary),
                OrchestrationDefaults.RetryOptions);

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);

            if (validationResult == null || !validationResult.IsValid)
            {
                failed++;
                continue;
            }

            scriptsGenerated++;

            // Step 2c: Generate video
            var videoResult = await context.CallActivityAsync<GeneratedVideoOutput?>(
                nameof(Activities.GenerateVideoActivity),
                new VideoGenerationInput(item.NewsItemId, script, item.Title),
                OrchestrationDefaults.RetryOptions);

            if (videoResult == null)
            {
                failed++;
                continue;
            }

            videosGenerated++;

            // Step 2d: Publish to platforms (fan-out)
            var publishTasks = TargetPlatforms.Select(platform =>
                context.CallActivityAsync<PublishOutput?>(
                    nameof(Activities.PublishVideoActivity),
                    new PublishInput(
                        videoResult.VideoUrl,
                        item.Title,
                        item.Summary,
                        item.Tags.ToArray(),
                        platform),
                    OrchestrationDefaults.RetryOptions));

            var publishResults = await Task.WhenAll(publishTasks);
            var successfulPublications = publishResults
                .Where(r => r != null)
                .Cast<PublishOutput>()
                .ToList();

            published += successfulPublications.Count;

            // Step 2e: Persist ShortVideo entity
            await context.CallActivityAsync<Guid?>(
                nameof(Activities.PersistShortVideoActivity),
                new PersistVideoInput(
                    item.NewsItemId,
                    script,
                    videoResult.DurationSeconds,
                    videoResult.VideoUrl,
                    successfulPublications),
                OrchestrationDefaults.RetryOptions);
        }

        var duration = context.CurrentUtcDateTime - startTime;

        logger.LogInformation(
            "Video generation completed. Eligible: {Eligible}, Scripts: {Scripts}, Videos: {Videos}, Published: {Published}, Failed: {Failed}, Duration: {Duration}",
            eligibleItems.Count, scriptsGenerated, videosGenerated, published, failed, duration);

        return new VideoGenerationResult(
            eligibleItems.Count, scriptsGenerated, videosGenerated, published, failed, duration);
    }
}
