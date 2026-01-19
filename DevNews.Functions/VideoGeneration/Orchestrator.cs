using DevNews.Domain.VideoContent.Enums;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.VideoGeneration;

public class Orchestrator
{
    private static TaskOptions CreateRetryOptions()
    {
        return TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 2,
            firstRetryInterval: TimeSpan.FromSeconds(5),
            backoffCoefficient: 2.0));
    }

    [Function(nameof(GenerateVideoOrchestration))]
    public async Task<VideoGenerationResult> GenerateVideoOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<Orchestrator>();
        var newsItemId = context.GetInput<Guid>();

        logger.LogInformation("Starting video generation orchestration for NewsItem {NewsItemId}", newsItemId);

        try
        {
            // Stage 1: Generate script and create VideoContent aggregate
            var scriptResult = await context.CallActivityAsync<GenerateScriptResult>(
                nameof(Activities.GenerateScriptActivity),
                newsItemId,
                CreateRetryOptions());

            if (!scriptResult.Success || scriptResult.VideoContentId == null)
            {
                logger.LogError("Script generation failed: {Error}", scriptResult.Error);
                return new VideoGenerationResult(
                    Success: false,
                    Stage: "ScriptGeneration",
                    Error: scriptResult.Error);
            }

            logger.LogInformation("Script generated for VideoContent {VideoContentId}", scriptResult.VideoContentId);

            // Stage 2: Generate video file
            var videoResult = await context.CallActivityAsync<GenerateVideoResult>(
                nameof(Activities.GenerateVideoActivity),
                scriptResult,
                CreateRetryOptions());

            if (!videoResult.Success || videoResult.VideoData == null)
            {
                logger.LogError("Video generation failed: {Error}", videoResult.Error);
                return new VideoGenerationResult(
                    Success: false,
                    Stage: "VideoGeneration",
                    Error: videoResult.Error);
            }

            logger.LogInformation("Video generated successfully");

            // Stage 3: Upload to Azure Blob Storage
            var uploadResult = await context.CallActivityAsync<UploadResult>(
                nameof(Activities.UploadVideoActivity),
                videoResult,
                CreateRetryOptions());

            if (!uploadResult.Success || uploadResult.VideoUrl == null)
            {
                logger.LogError("Upload failed: {Error}", uploadResult.Error);
                return new VideoGenerationResult(
                    Success: false,
                    Stage: "Upload",
                    Error: uploadResult.Error);
            }

            logger.LogInformation("Video uploaded to {VideoUrl}", uploadResult.VideoUrl);

            // Stage 4: AI Validation
            var validationInput = (
                VideoContentId: videoResult.VideoContentId,
                VideoUrl: uploadResult.VideoUrl,
                Script: scriptResult.Script ?? string.Empty);

            var validationResult = await context.CallActivityAsync<ValidationResult>(
                nameof(Activities.ValidateVideoActivity),
                validationInput,
                CreateRetryOptions());

            if (!validationResult.IsValid)
            {
                logger.LogWarning("Video validation failed: {Reason}", validationResult.Reason);
                return new VideoGenerationResult(
                    Success: false,
                    Stage: "Validation",
                    Error: $"Video rejected: {validationResult.Reason}");
            }

            logger.LogInformation("Video validated successfully");

            // Stage 5: Publish to all platforms in parallel
            var platforms = new[]
            {
                PublishPlatformEnum.Twitter,
                PublishPlatformEnum.Instagram,
                PublishPlatformEnum.TikTok,
                PublishPlatformEnum.YouTubeShorts
            };

            var scriptLength = scriptResult.Script?.Length ?? 0;
            var captionLength = Math.Min(100, scriptLength);
            var caption = $"Latest dev news: {scriptResult.Script?.Substring(0, captionLength)}...";

            var publishTasks = platforms.Select(platform =>
                context.CallActivityAsync<PlatformPublishResult>(
                    nameof(Activities.PublishToPlatformActivity),
                    new PublishRequest(
                        VideoContentId: videoResult.VideoContentId,
                        Platform: platform.ToString(),
                        VideoUrl: uploadResult.VideoUrl,
                        Caption: caption),
                    CreateRetryOptions()));

            var publishResults = await Task.WhenAll(publishTasks);

            var successCount = publishResults.Count(r => r.Success);
            logger.LogInformation("Publishing complete: {Success}/{Total} platforms succeeded",
                successCount, platforms.Length);

            // Stage 6: Mark VideoContent as published
            await context.CallActivityAsync(
                nameof(Activities.MarkVideoPublishedActivity),
                videoResult.VideoContentId,
                CreateRetryOptions());

            logger.LogInformation("Video generation orchestration completed successfully");

            return new VideoGenerationResult(
                Success: true,
                Stage: "Completed",
                PublishedCount: successCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Video generation orchestration failed with exception");
            return new VideoGenerationResult(
                Success: false,
                Stage: "Unknown",
                Error: ex.Message);
        }
    }
}
