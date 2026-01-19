using DevNews.Application.Common.Repositories;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.VideoContent.Commands;

/// <summary>
/// Command to validate a generated video using AI.
/// </summary>
public record ValidateVideoCommand(Guid VideoContentId, string VideoUrl, string Script) : IRequest<ResultResponse<bool>>;

public class ValidateVideoHandler(
    IVideoContentRepository repository,
    IVideoValidationService validationService,
    ILogger<ValidateVideoHandler> logger) : IRequestHandler<ValidateVideoCommand, ResultResponse<bool>>
{
    public async ValueTask<ResultResponse<bool>> Handle(ValidateVideoCommand request, CancellationToken ct)
    {
        var videoContent = await repository.GetByIdAsync(request.VideoContentId, ct);
        if (videoContent == null)
            return ResultResponse<bool>.Failure($"VideoContent with ID {request.VideoContentId} not found");

        // Call AI validation service
        var validationResult = await validationService.ValidateVideoAsync(
            videoUrl: request.VideoUrl,
            originalScript: request.Script,
            ct: ct);

        if (!validationResult.IsSuccess)
        {
            logger.LogError("Validation service failed for VideoContent {VideoContentId}: {Error}",
                request.VideoContentId, validationResult.ErrorMessage);
            return ResultResponse<bool>.Failure($"Validation service failed: {validationResult.ErrorMessage}");
        }

        var (isValid, reason) = validationResult.Data;

        // Update aggregate based on validation result
        var markResult = isValid
            ? videoContent.MarkAsValidated(reason)
            : videoContent.MarkAsRejected(reason);

        if (!markResult.IsSuccess)
        {
            logger.LogError("Failed to mark VideoContent {VideoContentId} validation status: {Error}",
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

        logger.LogInformation("VideoContent {VideoContentId} validation result: {IsValid} - {Reason}",
            request.VideoContentId, isValid, reason);

        return ResultResponse<bool>.Success(isValid);
    }
}
