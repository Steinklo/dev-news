using DevNews.Application.Common.Repositories;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.VideoContent.Commands;

/// <summary>
/// Command to update VideoContent with blob storage URL after video generation.
/// </summary>
public record UpdateVideoUrlCommand(Guid VideoContentId, string BlobUrl) : IRequest<ResultResponse<bool>>;

public class UpdateVideoUrlHandler(
    IVideoContentRepository repository,
    ILogger<UpdateVideoUrlHandler> logger) : IRequestHandler<UpdateVideoUrlCommand, ResultResponse<bool>>
{
    public async ValueTask<ResultResponse<bool>> Handle(UpdateVideoUrlCommand request, CancellationToken ct)
    {
        var videoContent = await repository.GetByIdAsync(request.VideoContentId, ct);
        if (videoContent == null)
            return ResultResponse<bool>.Failure($"VideoContent with ID {request.VideoContentId} not found");

        var setBlobResult = videoContent.SetBlobUrl(request.BlobUrl);
        if (!setBlobResult.IsSuccess)
        {
            logger.LogError("Failed to set blob URL for VideoContent {VideoContentId}: {Error}",
                request.VideoContentId, setBlobResult.ErrorMessage);
            return setBlobResult;
        }

        var updateResult = await repository.UpdateAsync(videoContent, ct);
        if (!updateResult.IsSuccess)
        {
            logger.LogError("Failed to update VideoContent {VideoContentId}: {Error}",
                request.VideoContentId, updateResult.ErrorMessage);
            return ResultResponse<bool>.Failure($"Update failed: {updateResult.ErrorMessage}");
        }

        logger.LogInformation("Successfully updated VideoContent {VideoContentId} with blob URL",
            request.VideoContentId);

        return ResultResponse<bool>.Success(true);
    }
}
