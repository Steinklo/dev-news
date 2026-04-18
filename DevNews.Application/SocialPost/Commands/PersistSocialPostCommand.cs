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
    string? PublishedUrl) : IRequest<ResultResponse<Guid>>;

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

        if (request.ExternalId != null && request.PublishedUrl != null)
            socialPost.MarkPublished(request.ExternalId, request.PublishedUrl);

        var persistResult = await repository.AddAsync(socialPost, cancellationToken);

        if (!persistResult.IsSuccess)
        {
            logger.LogError("Failed to persist SocialPost: {Error}", persistResult.ErrorMessage);
            return ResultResponse<Guid>.Failure(persistResult.ErrorMessage);
        }

        logger.LogInformation("Persisted SocialPost {Id}", persistResult.Data!.Id);
        return ResultResponse<Guid>.Success(persistResult.Data.Id);
    }
}
