using DevNews.Application.Common.Models;
using DevNews.Application.Common.Repositories;
using DevNews.Domain.Common;
using DevNews.Domain.Common.Enums;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.ShortVideo.Commands;

public record PersistShortVideoCommand(
    Guid NewsItemId,
    string Script,
    int DurationSeconds,
    string VideoUrl,
    IReadOnlyList<PublicationInput>? Publications) : IRequest<ResultResponse<Guid>>;

public record PublicationInput(
    Platform Platform,
    string ExternalId,
    string PublishedUrl);

public class PersistShortVideoHandler(
    IShortVideoRepository repository,
    ILogger<PersistShortVideoHandler> logger)
    : IRequestHandler<PersistShortVideoCommand, ResultResponse<Guid>>
{
    public async ValueTask<ResultResponse<Guid>> Handle(
        PersistShortVideoCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Persisting ShortVideo for NewsItem {NewsItemId}", request.NewsItemId);

        var videoResult = Domain.ShortVideo.ShortVideo.Create(
            request.NewsItemId,
            request.Script,
            request.DurationSeconds,
            request.VideoUrl);

        if (!videoResult.IsSuccess)
        {
            logger.LogWarning("Failed to create ShortVideo for {NewsItemId}: {Error}",
                request.NewsItemId, videoResult.ErrorMessage);
            return ResultResponse<Guid>.Failure(videoResult.ErrorMessage);
        }

        var shortVideo = videoResult.Data!;

        // Add platform publications
        if (request.Publications != null)
        {
            foreach (var pub in request.Publications)
            {
                var pubResult = shortVideo.AddPublication(pub.Platform, pub.ExternalId, pub.PublishedUrl);
                if (!pubResult.IsSuccess)
                    logger.LogWarning("Failed to add publication for {Platform}: {Error}",
                        pub.Platform, pubResult.ErrorMessage);
            }
        }

        var persistResult = await repository.AddAsync(shortVideo, cancellationToken);

        if (!persistResult.IsSuccess)
        {
            logger.LogError("Failed to persist ShortVideo for {NewsItemId}: {Error}",
                request.NewsItemId, persistResult.ErrorMessage);
            return ResultResponse<Guid>.Failure(persistResult.ErrorMessage);
        }

        logger.LogInformation("Persisted ShortVideo {Id} for NewsItem {NewsItemId}",
            persistResult.Data!.Id, request.NewsItemId);

        return ResultResponse<Guid>.Success(persistResult.Data.Id);
    }
}
