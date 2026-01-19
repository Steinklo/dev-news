using DevNews.Application.Common.Repositories;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.VideoContent.Commands;

/// <summary>
/// Command to generate a video script for a NewsItem and create VideoContent aggregate.
/// </summary>
public record GenerateVideoCommand(Guid NewsItemId) : IRequest<ResultResponse<Guid>>;

public class GenerateVideoHandler(
    INewsItemRepository newsRepo,
    IVideoContentRepository videoRepo,
    IVideoScriptGenerator scriptGenerator,
    ILogger<GenerateVideoHandler> logger) : IRequestHandler<GenerateVideoCommand, ResultResponse<Guid>>
{
    private const int DefaultDurationSeconds = 30;

    public async ValueTask<ResultResponse<Guid>> Handle(GenerateVideoCommand request, CancellationToken ct)
    {
        // 1. Fetch NewsItem
        var newsItemResult = await newsRepo.GetByIdAsync(request.NewsItemId, ct);
        if (!newsItemResult.IsSuccess || newsItemResult.Data == null)
            return ResultResponse<Guid>.Failure($"NewsItem with ID {request.NewsItemId} not found");

        var newsItem = newsItemResult.Data;

        // 2. Check if video already exists
        var existingVideo = await videoRepo.GetByNewsItemIdAsync(request.NewsItemId, ct);
        if (existingVideo != null)
        {
            logger.LogInformation("Video already exists for NewsItem {NewsItemId}, returning existing ID", request.NewsItemId);
            return ResultResponse<Guid>.Success(existingVideo.Id);
        }

        // 3. Generate script using AI
        var scriptResult = await scriptGenerator.GenerateScriptAsync(
            title: newsItem.Title.Value,
            summary: newsItem.Summary.Value,
            category: newsItem.Category.Value.ToString(),
            targetDurationSeconds: DefaultDurationSeconds,
            ct: ct);

        if (!scriptResult.IsSuccess)
        {
            logger.LogError("Failed to generate script for NewsItem {NewsItemId}: {Error}",
                request.NewsItemId, scriptResult.ErrorMessage);
            return ResultResponse<Guid>.Failure($"Script generation failed: {scriptResult.ErrorMessage}");
        }

        // 4. Create VideoContent aggregate
        var videoContentResult = Domain.VideoContent.VideoContent.Create(
            newsItemId: request.NewsItemId,
            scriptContent: scriptResult.Data!,
            durationSeconds: DefaultDurationSeconds);

        if (!videoContentResult.IsSuccess)
        {
            logger.LogError("Failed to create VideoContent for NewsItem {NewsItemId}: {Error}",
                request.NewsItemId, videoContentResult.ErrorMessage);
            return ResultResponse<Guid>.Failure($"VideoContent creation failed: {videoContentResult.ErrorMessage}");
        }

        // 5. Persist VideoContent
        var persistResult = await videoRepo.AddAsync(videoContentResult.Data!, ct);
        if (!persistResult.IsSuccess)
        {
            logger.LogError("Failed to persist VideoContent for NewsItem {NewsItemId}: {Error}",
                request.NewsItemId, persistResult.ErrorMessage);
            return ResultResponse<Guid>.Failure($"Persistence failed: {persistResult.ErrorMessage}");
        }

        logger.LogInformation("Successfully created VideoContent {VideoContentId} for NewsItem {NewsItemId}",
            persistResult.Data!.Id, request.NewsItemId);

        return ResultResponse<Guid>.Success(persistResult.Data!.Id);
    }
}
