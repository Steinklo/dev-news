using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using DevNews.Domain.Common.Enums;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.ShortVideo.Commands;

public record PublishVideoCommand(
    string VideoUrl,
    string Title,
    string Description,
    string[] Tags,
    Platform Platform) : IRequest<ResultResponse<PlatformPublishResult>>;

public class PublishVideoHandler(
    IPlatformPublishingService publishingService,
    ILogger<PublishVideoHandler> logger)
    : IRequestHandler<PublishVideoCommand, ResultResponse<PlatformPublishResult>>
{
    public async ValueTask<ResultResponse<PlatformPublishResult>> Handle(
        PublishVideoCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Publishing video to {Platform}", request.Platform);

        var result = await publishingService.PublishAsync(
            request.VideoUrl, request.Title, request.Description,
            request.Tags, request.Platform, cancellationToken);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Failed to publish to {Platform}: {Error}",
                request.Platform, result.ErrorMessage);
            return result;
        }

        logger.LogInformation("Published video to {Platform}: {Url}",
            request.Platform, result.Data!.PublishedUrl);

        return result;
    }
}
