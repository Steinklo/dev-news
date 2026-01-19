using DevNews.Application.Common.Repositories;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.VideoContent.Commands;

/// <summary>
/// Command to mark a video as rejected.
/// </summary>
public record MarkVideoRejectedCommand(Guid VideoContentId, string Reason) : IRequest<ResultResponse<bool>>;

public class MarkVideoRejectedHandler(
    IVideoContentRepository repository,
    ILogger<MarkVideoRejectedHandler> logger) : IRequestHandler<MarkVideoRejectedCommand, ResultResponse<bool>>
{
    public async ValueTask<ResultResponse<bool>> Handle(MarkVideoRejectedCommand request, CancellationToken ct)
    {
        var videoContent = await repository.GetByIdAsync(request.VideoContentId, ct);
        if (videoContent == null)
            return ResultResponse<bool>.Failure($"VideoContent with ID {request.VideoContentId} not found");

        var markResult = videoContent.MarkAsRejected(request.Reason);
        if (!markResult.IsSuccess)
        {
            logger.LogError("Failed to mark VideoContent {VideoContentId} as rejected: {Error}",
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

        logger.LogInformation("VideoContent {VideoContentId} marked as rejected: {Reason}",
            request.VideoContentId, request.Reason);

        return ResultResponse<bool>.Success(true);
    }
}
