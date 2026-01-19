using DevNews.Application.Common.Repositories;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.VideoContent.Commands;

/// <summary>
/// Command to mark a video as published after all platform publish attempts.
/// </summary>
public record MarkVideoPublishedCommand(Guid VideoContentId) : IRequest<ResultResponse<bool>>;

public class MarkVideoPublishedHandler(
    IVideoContentRepository repository,
    ILogger<MarkVideoPublishedHandler> logger) : IRequestHandler<MarkVideoPublishedCommand, ResultResponse<bool>>
{
    public async ValueTask<ResultResponse<bool>> Handle(MarkVideoPublishedCommand request, CancellationToken ct)
    {
        var videoContent = await repository.GetByIdAsync(request.VideoContentId, ct);
        if (videoContent == null)
            return ResultResponse<bool>.Failure($"VideoContent with ID {request.VideoContentId} not found");

        var markResult = videoContent.MarkAsPublished();
        if (!markResult.IsSuccess)
        {
            logger.LogError("Failed to mark VideoContent {VideoContentId} as published: {Error}",
                request.VideoContentId, markResult.ErrorMessage);
            return markResult;
        }

        var updateResult = await repository.UpdateAsync(videoContent, ct);
        if (!updateResult.IsSuccess)
        {
            logger.LogError("Failed to update VideoContent {VideoContentId}: {Error}",
                request.VideoContentId, updateResult.ErrorMessage);
            return ResultResponse<bool>.Failure($"Update failed: {updateResult.ErrorMessage}");
        }

        var successfulPublishes = videoContent.PublishResults.Count(r => r.Success);
        logger.LogInformation("VideoContent {VideoContentId} marked as published with {SuccessCount}/{TotalCount} successful platform publishes",
            request.VideoContentId, successfulPublishes, videoContent.PublishResults.Count);

        return ResultResponse<bool>.Success(true);
    }
}
