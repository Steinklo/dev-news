using DevNews.Application.Common.Repositories;
using DevNews.Domain.Common;
using DevNews.Domain.Common.Models;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.NewsItem.Commands;

public record PersistNewsItemCommand(CleanedArticle Article) : IRequest<ResultResponse<Guid>>;

public class PersistNewsItemHandler : IRequestHandler<PersistNewsItemCommand, ResultResponse<Guid>>
{
    private readonly INewsItemRepository _repository;
    private readonly ILogger<PersistNewsItemHandler> _logger;

    public PersistNewsItemHandler(
        INewsItemRepository repository,
        ILogger<PersistNewsItemHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<ResultResponse<Guid>> Handle(
        PersistNewsItemCommand request,
        CancellationToken cancellationToken)
    {
        var article = request.Article;

        _logger.LogDebug("Creating NewsItem for: {Title}", article.Title);

        // Create domain object
        var newsItemResult = Domain.NewsItem.NewsItem.Create(
            title: article.Title,
            summary: article.Summary,
            url: article.Url.ToString(),
            category: article.Category,
            relevanceScore: article.RelevanceScore,
            publishedAt: article.PublishedAt,
            severity: article.Severity,
            tags: article.Tags);

        if (!newsItemResult.IsSuccess)
        {
            _logger.LogWarning(
                "Failed to create NewsItem for {Url}: {Error}",
                article.Url,
                newsItemResult.ErrorMessage);
            return ResultResponse<Guid>.Failure(newsItemResult.ErrorMessage);
        }

        // Persist
        var persistResult = await _repository.AddAsync(newsItemResult.Data!, cancellationToken);

        if (!persistResult.IsSuccess)
        {
            _logger.LogError(
                "Failed to persist NewsItem {Url}: {Error}",
                article.Url,
                persistResult.ErrorMessage);
            return ResultResponse<Guid>.Failure(persistResult.ErrorMessage);
        }

        _logger.LogInformation(
            "Persisted NewsItem {Id}: {Title} [{Category}]",
            persistResult.Data!.Id,
            article.Title,
            article.Category);

        return ResultResponse<Guid>.Success(persistResult.Data.Id);
    }
}
