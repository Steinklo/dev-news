using DevNews.Application.Common.Models;
using DevNews.Application.ShortVideo.Dtos;
using DevNews.Domain.Common.Enums;
using DevNews.Functions.Common;
using DevNews.Functions.VideoGeneration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.DailyVideo;

public class Orchestrator
{
    private const int MinQualityScore = 70;
    private static readonly Platform[] TargetPlatforms = [Platform.YouTube, Platform.LinkedIn];

    [Function(nameof(DailyVideoOrchestrator))]
    public async Task<DailyVideoResult> DailyVideoOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<Orchestrator>();
        var startTime = context.CurrentUtcDateTime;

        logger.LogInformation("Starting daily video orchestration");

        // Step 1: Select the single highest-relevance item of the day (0 or 1)
        List<DailyVideoItem> items;
        try
        {
            items = await context.CallActivityAsync<List<DailyVideoItem>>(
                nameof(Activities.SelectDailyVideoItemsActivity),
                null,
                OrchestrationDefaults.RetryOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Daily video selection failed");
            return new DailyVideoResult(0, false, false, TimeSpan.Zero);
        }

        if (items.Count == 0)
        {
            logger.LogInformation("No item cleared the relevance floor, skipping daily video");
            return new DailyVideoResult(0, false, false, context.CurrentUtcDateTime - startTime);
        }

        var item = items[0];
        logger.LogInformation("Daily video for top item '{Title}' (score {Score})", item.Title, item.RelevanceScore);

        // Step 2: Generate a single-article script (reuses the per-article script path)
        var script = await context.CallActivityAsync<string?>(
            nameof(VideoGeneration.Activities.GenerateScriptActivity),
            new ScriptGenerationInput(item.NewsItemId, item.Title, item.Summary, item.Category, Array.Empty<string>()),
            OrchestrationDefaults.RetryOptions);

        await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);

        if (script == null)
        {
            logger.LogWarning("Daily video script generation returned null, skipping");
            return new DailyVideoResult(items.Count, false, false, context.CurrentUtcDateTime - startTime);
        }

        // Step 3: Validate the script (quality gate)
        var validationResult = await context.CallActivityAsync<ScriptValidationResult?>(
            nameof(VideoGeneration.Activities.ValidateScriptActivity),
            new ScriptValidationInput(script, item.Summary),
            OrchestrationDefaults.RetryOptions);

        await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);

        if (validationResult is not { IsValid: true, QualityScore: >= MinQualityScore })
        {
            logger.LogWarning(
                "Daily video script rejected: IsValid={IsValid}, QualityScore={Score}, Reason={Reason}",
                validationResult?.IsValid, validationResult?.QualityScore, validationResult?.Reason);
            return new DailyVideoResult(items.Count, false, false, context.CurrentUtcDateTime - startTime);
        }

        // Step 4: Render the video
        var videoResult = await context.CallActivityAsync<GeneratedVideoOutput?>(
            nameof(VideoGeneration.Activities.GenerateVideoActivity),
            new VideoGenerationInput(item.NewsItemId, script, item.Title),
            OrchestrationDefaults.RetryOptions);

        if (videoResult == null)
        {
            logger.LogWarning("Daily video render returned null");
            return new DailyVideoResult(items.Count, false, false, context.CurrentUtcDateTime - startTime);
        }

        // Step 5: Publish to platforms (fan-out)
        var publishTasks = TargetPlatforms
            .Select(platform => context.CallActivityAsync<PublishOutput?>(
                nameof(VideoGeneration.Activities.PublishVideoActivity),
                new PublishInput(videoResult.VideoUrl, item.Title, script, Array.Empty<string>(), platform),
                OrchestrationDefaults.RetryOptions));

        var publishResults = await Task.WhenAll(publishTasks);
        var successfulPublications = publishResults
            .Where(r => r != null)
            .Cast<PublishOutput>()
            .ToList();

        var videoPublished = successfulPublications.Count > 0;

        // Step 6: Persist the ShortVideo
        await context.CallActivityAsync<Guid?>(
            nameof(VideoGeneration.Activities.PersistShortVideoActivity),
            new PersistVideoInput(
                item.NewsItemId,
                script,
                videoResult.DurationSeconds,
                videoResult.VideoUrl,
                successfulPublications),
            OrchestrationDefaults.RetryOptions);

        var duration = context.CurrentUtcDateTime - startTime;

        logger.LogInformation(
            "Daily video orchestration completed. Item: '{Title}', VideoPublished: {Published}, Duration: {Duration}",
            item.Title, videoPublished, duration);

        return new DailyVideoResult(items.Count, true, videoPublished, duration);
    }
}
