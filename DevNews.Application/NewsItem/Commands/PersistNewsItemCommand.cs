using DevNews.Application.Common.Repositories;
using DevNews.Domain.Common;
using DevNews.Application.Common.Models;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.NewsItem.Commands;

public record PersistNewsItemCommand(CleanedArticle Article) : IRequest<ResultResponse<Guid>>;

public class PersistNewsItemHandler(
    INewsItemRepository repository,
    ILogger<PersistNewsItemHandler> logger) : IRequestHandler<PersistNewsItemCommand, ResultResponse<Guid>>
{
    public async ValueTask<ResultResponse<Guid>> Handle(
        PersistNewsItemCommand request,
        CancellationToken cancellationToken)
    {
        var article = request.Article;

        logger.LogDebug("Creating NewsItem for: {Title}", article.Title);

        // Create domain object
        var newsItemResult = Domain.NewsItem.NewsItem.Create(
            title: article.Title,
            summary: article.Summary,
            url: article.Url.ToString(),
            category: article.Category,
            relevanceScore: article.RelevanceScore,
            source: article.Source,
            author: article.Author,
            publishedAt: article.PublishedAt,
            severity: article.Severity,
            tags: article.Tags);

        if (!newsItemResult.IsSuccess)
        {
            logger.LogWarning(
                "Failed to create NewsItem for {Url}: {Error}",
                article.Url,
                newsItemResult.ErrorMessage);
            return ResultResponse<Guid>.Failure(newsItemResult.ErrorMessage);
        }

        // Persist
        var persistResult = await repository.AddAsync(newsItemResult.Data!, cancellationToken);

        if (!persistResult.IsSuccess)
        {
            logger.LogError(
                "Failed to persist NewsItem {Url}: {Error}",
                article.Url,
                persistResult.ErrorMessage);
            return ResultResponse<Guid>.Failure(persistResult.ErrorMessage);
        }

        logger.LogInformation(
            "Persisted NewsItem {Id}: {Title} [{Category}]",
            persistResult.Data!.Id,
            article.Title,
            article.Category);

        return ResultResponse<Guid>.Success(persistResult.Data.Id);
    }
}
