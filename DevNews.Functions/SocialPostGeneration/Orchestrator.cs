using DevNews.Application.SocialPost.Dtos;
using DevNews.Functions.Common;
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
            return new SocialPostGenerationResult(0, 0, TimeSpan.Zero);
        }

        if (eligibleItems.Count < MinItemsForSocialPosts)
        {
            logger.LogInformation("Not enough items for social posts ({Count} < {Min}), skipping",
                eligibleItems.Count, MinItemsForSocialPosts);
            return new SocialPostGenerationResult(eligibleItems.Count, 0, context.CurrentUtcDateTime - startTime);
        }

        // Step 2: For each article, generate (and validate) text, publish, and persist
        var postsPublished = 0;

        foreach (var item in eligibleItems)
        {
            // Step 2a: Generate social post text — already validated against the SocialPostText
            // invariant inside the handler, so anything that reaches here is safe to publish.
            var postText = await context.CallActivityAsync<string?>(
                nameof(Activities.GenerateSocialPostActivity),
                item,
                OrchestrationDefaults.RetryOptions);

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);

            if (postText == null)
            {
                logger.LogWarning("Social post generation failed/invalid for {Title}, skipping", item.Title);
                continue;
            }

            // Step 2b: Publish to LinkedIn
            var publishResult = await context.CallActivityAsync<SocialPostPublishOutput?>(
                nameof(Activities.PublishSocialPostActivity),
                postText,
                OrchestrationDefaults.RetryOptions);

            if (publishResult != null)
                postsPublished++;

            // Step 2c: Persist SocialPost (Published when the LinkedIn call succeeded, else Failed)
            await context.CallActivityAsync<Guid?>(
                nameof(Activities.PersistSocialPostActivity),
                new PersistSocialPostInput(
                    item.NewsItemId,
                    postText,
                    item.SourceUrl,
                    publishResult?.ExternalId,
                    publishResult?.PublishedUrl,
                    publishResult != null),
                OrchestrationDefaults.RetryOptions);

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);
        }

        var duration = context.CurrentUtcDateTime - startTime;

        logger.LogInformation(
            "Social post orchestration completed. Items: {Items}, PostsPublished: {Posts}, Duration: {Duration}",
            eligibleItems.Count, postsPublished, duration);

        return new SocialPostGenerationResult(eligibleItems.Count, postsPublished, duration);
    }
}
