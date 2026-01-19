using DevNews.Application.Common.Repositories;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using DevNews.Domain.VideoContent.Enums;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.VideoContent.Commands;

/// <summary>
/// Command to add a publish result for a specific platform.
/// </summary>
public record AddPublishResultCommand(
    Guid VideoContentId,
    PublishPlatformEnum Platform,
    PublishResult Result) : IRequest<ResultResponse<bool>>;

public class AddPublishResultHandler(
    IVideoContentRepository repository,
    ILogger<AddPublishResultHandler> logger) : IRequestHandler<AddPublishResultCommand, ResultResponse<bool>>
{
    public async ValueTask<ResultResponse<bool>> Handle(AddPublishResultCommand request, CancellationToken ct)
    {
        var videoContent = await repository.GetByIdAsync(request.VideoContentId, ct);
        if (videoContent == null)
            return ResultResponse<bool>.Failure($"VideoContent with ID {request.VideoContentId} not found");

        var addResult = videoContent.AddPublishResult(
            platform: request.Platform,
            success: request.Result.Success,
            externalId: request.Result.ExternalId,
            errorMessage: request.Result.ErrorMessage);

        if (!addResult.IsSuccess)
        {
            logger.LogError("Failed to add publish result for VideoContent {VideoContentId}, platform {Platform}: {Error}",
                request.VideoContentId, request.Platform, addResult.ErrorMessage);
            return addResult;
        }

        var updateResult = await repository.UpdateAsync(videoContent, ct);
        if (!updateResult.IsSuccess)
        {
            logger.LogError("Failed to update VideoContent {VideoContentId}: {Error}",
                request.VideoContentId, updateResult.ErrorMessage);
            return ResultResponse<bool>.Failure($"Update failed: {updateResult.ErrorMessage}");
        }

        logger.LogInformation("Added publish result for VideoContent {VideoContentId}, platform {Platform}: {Success}",
            request.VideoContentId, request.Platform, request.Result.Success);

        return ResultResponse<bool>.Success(true);
    }
}
