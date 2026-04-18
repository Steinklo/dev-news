using DevNews.Application.Common.Models;
using DevNews.Application.SocialPost.Dtos;
using DevNews.Domain.Common.Enums;
using DevNews.Functions.Common;
using DevNews.Functions.VideoGeneration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.SocialPostGeneration;

public class Orchestrator
{
    private const int MinItemsForSocialPosts = 3;

    [Function(nameof(SocialPostOrchestrator))]
    public async Task<SocialPostGenerationResult> SocialPostOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<Orchestrator>();
        var startTime = context.CurrentUtcDateTime;

        logger.LogInformation("Starting social post generation orchestration");

        // Step 1: Select eligible items
        List<SocialPostEligibleItem> eligibleItems;
        try
        {
            eligibleItems = await context.CallActivityAsync<List<SocialPostEligibleItem>>(
                nameof(Activities.SelectSocialPostEligibleItemsActivity),
                null,
                OrchestrationDefaults.RetryOptions);

            logger.LogInformation("Found {Count} eligible items for social posts", eligibleItems.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Social post selection failed");
            return new SocialPostGenerationResult(0, 0, false, false, TimeSpan.Zero);
        }

        if (eligibleItems.Count < MinItemsForSocialPosts)
        {
            logger.LogInformation("Not enough items for social posts ({Count} < {Min}), skipping",
                eligibleItems.Count, MinItemsForSocialPosts);
            return new SocialPostGenerationResult(eligibleItems.Count, 0, false, false,
                context.CurrentUtcDateTime - startTime);
        }

        // Step 2: For each article, generate social post, publish, and persist
        var postsPublished = 0;

        foreach (var item in eligibleItems)
        {
            // Step 2a: Generate social post text
            var postText = await context.CallActivityAsync<string?>(
                nameof(Activities.GenerateSocialPostActivity),
                item,
                OrchestrationDefaults.RetryOptions);

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);

            if (postText == null)
            {
                logger.LogWarning("Social post generation failed for {Title}, skipping", item.Title);
                continue;
            }

            // Step 2b: Publish to LinkedIn
            var publishResult = await context.CallActivityAsync<SocialPostPublishOutput?>(
                nameof(Activities.PublishSocialPostActivity),
                new SocialPostPublishInput(postText, Platform.LinkedIn),
                OrchestrationDefaults.RetryOptions);

            if (publishResult != null)
                postsPublished++;

            // Step 2c: Persist SocialPost
            await context.CallActivityAsync<Guid?>(
                nameof(Activities.PersistSocialPostActivity),
                new PersistSocialPostInput(
                    item.NewsItemId,
                    postText,
                    item.SourceUrl,
                    publishResult?.ExternalId,
                    publishResult?.PublishedUrl),
                OrchestrationDefaults.RetryOptions);

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);
        }

        // Step 3: Video generation (if enabled)
        var videoGenerated = false;
        var videoPublished = false;

        var isVideoEnabled = await context.CallActivityAsync<bool>(
            nameof(Activities.IsVideoEnabledActivity),
            null,
            OrchestrationDefaults.RetryOptions);

        if (isVideoEnabled)
        {
            logger.LogInformation("Video generation enabled, generating video script");

            // Step 3a: Generate video script from all articles
            var script = await context.CallActivityAsync<string?>(
                nameof(Activities.GenerateDailyVideoScriptActivity),
                eligibleItems,
                OrchestrationDefaults.RetryOptions);

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);

            if (script != null)
            {
                // Step 3b: Validate script — use combined summaries for validation
                var combinedSummary = string.Join("\n\n", eligibleItems.Select(i => $"{i.Title}: {i.Summary}"));
                var validationResult = await context.CallActivityAsync<ScriptValidationResult?>(
                    nameof(VideoGeneration.Activities.ValidateScriptActivity),
                    new ScriptValidationInput(script, combinedSummary),
                    OrchestrationDefaults.RetryOptions);

                await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);

                if (validationResult is { IsValid: true, QualityScore: >= 70 })
                {
                    // Step 3c: Generate video
                    var firstItem = eligibleItems[0];
                    var videoResult = await context.CallActivityAsync<GeneratedVideoOutput?>(
                        nameof(VideoGeneration.Activities.GenerateVideoActivity),
                        new VideoGenerationInput(firstItem.NewsItemId, script, "AI Developer News Daily"),
                        OrchestrationDefaults.RetryOptions);

                    if (videoResult != null)
                    {
                        videoGenerated = true;

                        // Step 3d: Publish video to platforms
                        var publishTasks = new[] { Platform.YouTube, Platform.LinkedIn }
                            .Select(platform => context.CallActivityAsync<PublishOutput?>(
                                nameof(VideoGeneration.Activities.PublishVideoActivity),
                                new PublishInput(
                                    videoResult.VideoUrl,
                                    "AI Developer News Daily",
                                    script,
                                    Array.Empty<string>(),
                                    platform),
                                OrchestrationDefaults.RetryOptions));

                        var publishResults = await Task.WhenAll(publishTasks);
                        var successfulPublications = publishResults
                            .Where(r => r != null)
                            .Cast<PublishOutput>()
                            .ToList();

                        videoPublished = successfulPublications.Count > 0;

                        // Step 3e: Persist ShortVideo
                        await context.CallActivityAsync<Guid?>(
                            nameof(VideoGeneration.Activities.PersistShortVideoActivity),
                            new PersistVideoInput(
                                firstItem.NewsItemId,
                                script,
                                videoResult.DurationSeconds,
                                videoResult.VideoUrl,
                                successfulPublications),
                            OrchestrationDefaults.RetryOptions);
                    }
                }
                else
                {
                    logger.LogWarning(
                        "Video script rejected: IsValid={IsValid}, QualityScore={Score}, Reason={Reason}",
                        validationResult?.IsValid, validationResult?.QualityScore, validationResult?.Reason);
                }
            }
        }

        var duration = context.CurrentUtcDateTime - startTime;

        logger.LogInformation(
            "Social post orchestration completed. Items: {Items}, PostsPublished: {Posts}, VideoGenerated: {Video}, VideoPublished: {VideoPublished}, Duration: {Duration}",
            eligibleItems.Count, postsPublished, videoGenerated, videoPublished, duration);

        return new SocialPostGenerationResult(eligibleItems.Count, postsPublished, videoGenerated, videoPublished, duration);
    }
}
