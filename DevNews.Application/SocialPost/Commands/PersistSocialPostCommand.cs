using DevNews.Application.Common.Repositories;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.SocialPost.Commands;

public record PersistSocialPostCommand(
    Guid NewsItemId,
    string Content,
    string? SourceUrl,
    string? ExternalId,
    string? PublishedUrl,
    bool Published) : IRequest<ResultResponse<Guid>>;

public class PersistSocialPostHandler(
    ISocialPostRepository repository,
    ILogger<PersistSocialPostHandler> logger)
    : IRequestHandler<PersistSocialPostCommand, ResultResponse<Guid>>
{
    public async ValueTask<ResultResponse<Guid>> Handle(
        PersistSocialPostCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Persisting SocialPost for news item {NewsItemId}", request.NewsItemId);

        var socialPostResult = Domain.SocialPost.SocialPost.Create(request.NewsItemId, request.Content, request.SourceUrl);

        if (!socialPostResult.IsSuccess)
        {
            logger.LogWarning("Failed to create SocialPost: {Error}", socialPostResult.ErrorMessage);
            return ResultResponse<Guid>.Failure(socialPostResult.ErrorMessage);
        }

        var socialPost = socialPostResult.Data!;

        if (request.Published && request.ExternalId != null && request.PublishedUrl != null)
            socialPost.MarkPublished(request.ExternalId, request.PublishedUrl);
        else
            socialPost.MarkFailed();

        var persistResult = await repository.AddAsync(socialPost, cancellationToken);

        if (!persistResult.IsSuccess)
        {
            logger.LogError("Failed to persist SocialPost: {Error}", persistResult.ErrorMessage);
            return ResultResponse<Guid>.Failure(persistResult.ErrorMessage);
        }

        logger.LogInformation("Persisted SocialPost {Id} with status {Status}", persistResult.Data!.Id, socialPost.Status);
        return ResultResponse<Guid>.Success(persistResult.Data.Id);
    }
}
