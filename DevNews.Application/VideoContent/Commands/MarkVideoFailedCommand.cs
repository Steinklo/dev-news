using DevNews.Application.Common.Repositories;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.VideoContent.Commands;

/// <summary>
/// Command to mark VideoContent as failed due to technical error.
/// </summary>
public record MarkVideoFailedCommand(Guid VideoContentId, string ErrorReason) : IRequest<ResultResponse<bool>>;

public class MarkVideoFailedHandler(
    IVideoContentRepository repository,
    ILogger<MarkVideoFailedHandler> logger) : IRequestHandler<MarkVideoFailedCommand, ResultResponse<bool>>
{
    public async ValueTask<ResultResponse<bool>> Handle(MarkVideoFailedCommand request, CancellationToken ct)
    {
        var videoContent = await repository.GetByIdAsync(request.VideoContentId, ct);
        if (videoContent == null)
            return ResultResponse<bool>.Failure($"VideoContent with ID {request.VideoContentId} not found");

        var markResult = videoContent.MarkAsFailed(request.ErrorReason);
        if (!markResult.IsSuccess)
        {
            logger.LogError("Failed to mark VideoContent {VideoContentId} as failed: {Error}",
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

        logger.LogInformation("VideoContent {VideoContentId} marked as failed: {Reason}",
            request.VideoContentId, request.ErrorReason);

        return ResultResponse<bool>.Success(true);
    }
}
