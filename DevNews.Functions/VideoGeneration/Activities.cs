using DevNews.Application.Common.Services;
using DevNews.Application.VideoContent.Commands;
using DevNews.Application.VideoContent.Queries;
using DevNews.Domain.VideoContent.Enums;
using Mediator;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.VideoGeneration;

public class Activities
{
    private readonly IMediator _mediator;
    private readonly IVideoGenerator _videoGenerator;
    private readonly IVideoBlobStorage _blobStorage;
    private readonly IVideoValidationService _validationService;
    private readonly IVideoPublishingService _publishingService;
    private readonly ILogger<Activities> _logger;

    public Activities(
        IMediator mediator,
        IVideoGenerator videoGenerator,
        IVideoBlobStorage blobStorage,
        IVideoValidationService validationService,
        IVideoPublishingService publishingService,
        ILogger<Activities> logger)
    {
        _mediator = mediator;
        _videoGenerator = videoGenerator;
        _blobStorage = blobStorage;
        _validationService = validationService;
        _publishingService = publishingService;
        _logger = logger;
    }

    /// <summary>
    /// Activity: Generate script and create VideoContent aggregate.
    /// </summary>
    [Function(nameof(GenerateScriptActivity))]
    public async Task<GenerateScriptResult> GenerateScriptActivity(
        [ActivityTrigger] Guid newsItemId,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Generating script for NewsItem {NewsItemId}", newsItemId);

            var result = await _mediator.Send(new GenerateVideoCommand(newsItemId), ct);

            if (!result.IsSuccess)
            {
                _logger.LogError("Script generation failed: {Error}", result.ErrorMessage);
                return new GenerateScriptResult(false, null, null, result.ErrorMessage);
            }

            var videoContentId = result.Data;

            // Fetch the created VideoContent to get the script
            var videoQuery = await _mediator.Send(new GetVideoByNewsItemIdQuery(newsItemId), ct);
            var script = videoQuery.Data?.Script ?? string.Empty;

            _logger.LogInformation("Script generated successfully for VideoContent {VideoContentId}", videoContentId);

            return new GenerateScriptResult(true, videoContentId, script, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating script for NewsItem {NewsItemId}", newsItemId);
            return new GenerateScriptResult(false, null, null, ex.Message);
        }
    }

    /// <summary>
    /// Activity: Generate video file from script.
    /// </summary>
    [Function(nameof(GenerateVideoActivity))]
    public async Task<GenerateVideoResult> GenerateVideoActivity(
        [ActivityTrigger] GenerateScriptResult scriptResult,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Generating video for VideoContent {VideoContentId}", scriptResult.VideoContentId);

            if (scriptResult.Script == null || scriptResult.VideoContentId == null)
            {
                return new GenerateVideoResult(false, Guid.Empty, null, "Invalid script result");
            }

            var result = await _videoGenerator.GenerateVideoAsync(scriptResult.Script, 30, ct);

            if (!result.IsSuccess)
            {
                _logger.LogError("Video generation failed: {Error}", result.ErrorMessage);

                // Mark VideoContent as Failed
                await _mediator.Send(
                    new MarkVideoFailedCommand(scriptResult.VideoContentId.Value, result.ErrorMessage ?? "Unknown error"),
                    ct);

                return new GenerateVideoResult(false, scriptResult.VideoContentId.Value, null, result.ErrorMessage);
            }

            _logger.LogInformation("Video generated successfully, size: {Size} bytes", result.Data?.Length ?? 0);

            return new GenerateVideoResult(true, scriptResult.VideoContentId.Value, result.Data, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating video");

            if (scriptResult.VideoContentId.HasValue)
            {
                await _mediator.Send(
                    new MarkVideoFailedCommand(scriptResult.VideoContentId.Value, ex.Message),
                    ct);
            }

            return new GenerateVideoResult(false, scriptResult.VideoContentId ?? Guid.Empty, null, ex.Message);
        }
    }

    /// <summary>
    /// Activity: Upload video to Azure Blob Storage.
    /// </summary>
    [Function(nameof(UploadVideoActivity))]
    public async Task<UploadResult> UploadVideoActivity(
        [ActivityTrigger] GenerateVideoResult videoResult,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Uploading video for VideoContent {VideoContentId}", videoResult.VideoContentId);

            if (videoResult.VideoData == null)
            {
                return new UploadResult(false, null, "No video data to upload");
            }

            var fileName = $"{videoResult.VideoContentId}.mp4";
            var uploadResult = await _blobStorage.UploadVideoAsync(videoResult.VideoData, fileName, ct);

            if (!uploadResult.IsSuccess)
            {
                _logger.LogError("Upload failed: {Error}", uploadResult.ErrorMessage);
                return new UploadResult(false, null, uploadResult.ErrorMessage);
            }

            // Update VideoContent with blob URL
            var updateResult = await _mediator.Send(
                new UpdateVideoUrlCommand(videoResult.VideoContentId, uploadResult.Data!),
                ct);

            if (!updateResult.IsSuccess)
            {
                _logger.LogError("Failed to update VideoContent with blob URL: {Error}", updateResult.ErrorMessage);
                return new UploadResult(false, null, updateResult.ErrorMessage);
            }

            _logger.LogInformation("Video uploaded successfully to {Url}", uploadResult.Data);

            return new UploadResult(true, uploadResult.Data, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading video");
            return new UploadResult(false, null, ex.Message);
        }
    }

    /// <summary>
    /// Activity: Validate video using AI.
    /// </summary>
    [Function(nameof(ValidateVideoActivity))]
    public async Task<ValidationResult> ValidateVideoActivity(
        [ActivityTrigger] (Guid VideoContentId, string VideoUrl, string Script) input,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Validating video at {VideoUrl}", input.VideoUrl);

            var result = await _validationService.ValidateVideoAsync(input.VideoUrl, input.Script, ct);

            if (!result.IsSuccess)
            {
                _logger.LogError("Validation service failed: {Error}", result.ErrorMessage);
                return new ValidationResult(false, result.ErrorMessage ?? "Validation service error");
            }

            var (isValid, reason) = result.Data;

            // Update VideoContent with validation result
            var updateResult = isValid
                ? await _mediator.Send(new ValidateVideoCommand(input.VideoContentId, input.VideoUrl, input.Script), ct)
                : await _mediator.Send(new MarkVideoRejectedCommand(input.VideoContentId, reason), ct);

            if (!updateResult.IsSuccess)
            {
                _logger.LogWarning("Failed to update VideoContent validation status: {Error}", updateResult.ErrorMessage);
            }

            _logger.LogInformation("Video validation result: {IsValid} - {Reason}", isValid, reason);

            return new ValidationResult(isValid, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during video validation");
            return new ValidationResult(false, $"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Activity: Publish video to a specific platform.
    /// </summary>
    [Function(nameof(PublishToPlatformActivity))]
    public async Task<PlatformPublishResult> PublishToPlatformActivity(
        [ActivityTrigger] PublishRequest request,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Publishing video to {Platform}", request.Platform);

            var platformEnum = Enum.Parse<PublishPlatformEnum>(request.Platform);
            Application.Common.Services.PublishResult? publishResult = null;

            switch (platformEnum)
            {
                case PublishPlatformEnum.Twitter:
                    var twitterResult = await _publishingService.PublishToTwitterAsync(request.VideoUrl, request.Caption, ct);
                    publishResult = twitterResult.IsSuccess ? twitterResult.Data : null;
                    break;

                case PublishPlatformEnum.Instagram:
                    var instaResult = await _publishingService.PublishToInstagramAsync(request.VideoUrl, request.Caption, ct);
                    publishResult = instaResult.IsSuccess ? instaResult.Data : null;
                    break;

                case PublishPlatformEnum.TikTok:
                    var tiktokResult = await _publishingService.PublishToTikTokAsync(request.VideoUrl, request.Caption, ct);
                    publishResult = tiktokResult.IsSuccess ? tiktokResult.Data : null;
                    break;

                case PublishPlatformEnum.YouTubeShorts:
                    var youtubeResult = await _publishingService.PublishToYouTubeShortsAsync(
                        request.VideoUrl,
                        request.Caption,
                        request.Caption,
                        ct);
                    publishResult = youtubeResult.IsSuccess ? youtubeResult.Data : null;
                    break;
            }

            if (publishResult == null || !publishResult.Success)
            {
                var error = publishResult?.ErrorMessage ?? "Unknown error";
                _logger.LogError("Failed to publish to {Platform}: {Error}", request.Platform, error);

                // Still record the failure in VideoContent
                await _mediator.Send(
                    new AddPublishResultCommand(
                        request.VideoContentId,
                        platformEnum,
                        new Application.Common.Services.PublishResult(false, null, error)),
                    ct);

                return new PlatformPublishResult(false, request.Platform, null, error);
            }

            // Record success in VideoContent
            await _mediator.Send(
                new AddPublishResultCommand(
                    request.VideoContentId,
                    platformEnum,
                    publishResult),
                ct);

            _logger.LogInformation("Successfully published to {Platform}, external ID: {ExternalId}",
                request.Platform, publishResult.ExternalId);

            return new PlatformPublishResult(true, request.Platform, publishResult.ExternalId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error publishing to {Platform}", request.Platform);

            // Try to record the failure
            try
            {
                var platformEnum = Enum.Parse<PublishPlatformEnum>(request.Platform);
                await _mediator.Send(
                    new AddPublishResultCommand(
                        request.VideoContentId,
                        platformEnum,
                        new Application.Common.Services.PublishResult(false, null, ex.Message)),
                    ct);
            }
            catch
            {
                // Ignore secondary errors
            }

            return new PlatformPublishResult(false, request.Platform, null, ex.Message);
        }
    }

    /// <summary>
    /// Activity: Mark VideoContent as published after all platforms complete.
    /// </summary>
    [Function(nameof(MarkVideoPublishedActivity))]
    public async Task MarkVideoPublishedActivity(
        [ActivityTrigger] Guid videoContentId,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Marking VideoContent {VideoContentId} as published", videoContentId);

            await _mediator.Send(new MarkVideoPublishedCommand(videoContentId), ct);

            _logger.LogInformation("VideoContent {VideoContentId} marked as published", videoContentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark VideoContent {VideoContentId} as published", videoContentId);
            throw; // Let orchestrator handle retry
        }
    }
}
