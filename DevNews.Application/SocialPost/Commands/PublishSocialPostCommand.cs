using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using DevNews.Domain.Common.Enums;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.SocialPost.Commands;

public record PublishSocialPostCommand(
    string Text,
    Platform Platform) : IRequest<ResultResponse<PlatformPublishResult>>;

public class PublishSocialPostHandler(
    ISocialPostPublisher socialPostPublisher,
    ILogger<PublishSocialPostHandler> logger)
    : IRequestHandler<PublishSocialPostCommand, ResultResponse<PlatformPublishResult>>
{
    public async ValueTask<ResultResponse<PlatformPublishResult>> Handle(
        PublishSocialPostCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Publishing social post to {Platform}", request.Platform);

        var result = await socialPostPublisher.PublishTextAsync(request.Text, request.Platform, cancellationToken);

        if (!result.IsSuccess)
            logger.LogWarning("Social post publishing to {Platform} failed: {Error}", request.Platform, result.ErrorMessage);
        else
            logger.LogInformation("Social post published to {Platform}: {Url}", request.Platform, result.Data!.PublishedUrl);

        return result;
    }
}
