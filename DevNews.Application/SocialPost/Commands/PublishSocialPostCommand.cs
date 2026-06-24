using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.SocialPost.Commands;

public record PublishSocialPostCommand(
    string Text) : IRequest<ResultResponse<PlatformPublishResult>>;

public class PublishSocialPostHandler(
    ISocialPostPublisher socialPostPublisher,
    ILogger<PublishSocialPostHandler> logger)
    : IRequestHandler<PublishSocialPostCommand, ResultResponse<PlatformPublishResult>>
{
    public async ValueTask<ResultResponse<PlatformPublishResult>> Handle(
        PublishSocialPostCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Publishing social post to LinkedIn");

        var result = await socialPostPublisher.PublishTextAsync(request.Text, cancellationToken);

        if (!result.IsSuccess)
            logger.LogWarning("Social post publishing to LinkedIn failed: {Error}", result.ErrorMessage);
        else
            logger.LogInformation("Social post published to LinkedIn: {Url}", result.Data!.PublishedUrl);

        return result;
    }
}
