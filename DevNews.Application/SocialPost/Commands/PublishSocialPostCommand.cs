using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.SocialPost.Commands;

public record PublishSocialPostCommand(
    string Text) : IRequest<ResultResponse<PlatformPublishResult>>;

public class PublishSocialPostHandler(
    IEnumerable<ISocialPostPublisher> publishers,
    ILogger<PublishSocialPostHandler> logger)
    : IRequestHandler<PublishSocialPostCommand, ResultResponse<PlatformPublishResult>>
{
    public async ValueTask<ResultResponse<PlatformPublishResult>> Handle(
        PublishSocialPostCommand request,
        CancellationToken cancellationToken)
    {
        // Fan out to every configured text platform. Unconfigured ones return a Failure (graceful)
        // and are simply skipped. We return the first success as the representative result for
        // persistence (the SocialPost stores a single external id/url).
        ResultResponse<PlatformPublishResult>? firstSuccess = null;

        foreach (var publisher in publishers)
        {
            var result = await publisher.PublishTextAsync(request.Text, cancellationToken);

            if (result.IsSuccess)
            {
                logger.LogInformation("Social post published to {Platform}: {Url}",
                    publisher.PlatformName, result.Data!.PublishedUrl);
                firstSuccess ??= result;
            }
            else
            {
                logger.LogWarning("Social post not published to {Platform}: {Error}",
                    publisher.PlatformName, result.ErrorMessage);
            }
        }

        return firstSuccess
            ?? ResultResponse<PlatformPublishResult>.Failure("Social post was not published to any configured platform");
    }
}
