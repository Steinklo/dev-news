using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.ShortVideo.Commands;

public record GenerateVideoCommand(
    Guid NewsItemId,
    string Script,
    string Title) : IRequest<ResultResponse<GeneratedVideoResult>>;

public record GeneratedVideoResult(
    string VideoUrl,
    int DurationSeconds);

public class GenerateVideoHandler(
    IVideoGenerationService videoGenerationService,
    IVideoStorageService videoStorageService,
    ILogger<GenerateVideoHandler> logger)
    : IRequestHandler<GenerateVideoCommand, ResultResponse<GeneratedVideoResult>>
{
    public async ValueTask<ResultResponse<GeneratedVideoResult>> Handle(
        GenerateVideoCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Generating video for NewsItem {NewsItemId}", request.NewsItemId);

        // Generate the video
        var videoResult = await videoGenerationService.GenerateVideoAsync(
            request.Script, request.Title, cancellationToken);

        if (!videoResult.IsSuccess)
        {
            logger.LogWarning("Video generation failed for {NewsItemId}: {Error}",
                request.NewsItemId, videoResult.ErrorMessage);
            return ResultResponse<GeneratedVideoResult>.Failure(videoResult.ErrorMessage);
        }

        // Upload to blob storage
        var fileName = $"{DateTimeOffset.UtcNow:yyyy/MM/dd}/{request.NewsItemId}.mp4";
        var uploadResult = await videoStorageService.UploadVideoAsync(
            videoResult.Data!.VideoData, fileName, videoResult.Data.ContentType, cancellationToken);

        if (!uploadResult.IsSuccess)
        {
            logger.LogError("Failed to upload video for {NewsItemId}: {Error}",
                request.NewsItemId, uploadResult.ErrorMessage);
            return ResultResponse<GeneratedVideoResult>.Failure(uploadResult.ErrorMessage);
        }

        logger.LogInformation("Generated and uploaded video for {NewsItemId} ({Duration}s)",
            request.NewsItemId, videoResult.Data.DurationSeconds);

        return ResultResponse<GeneratedVideoResult>.Success(
            new GeneratedVideoResult(uploadResult.Data!, videoResult.Data.DurationSeconds));
    }
}
