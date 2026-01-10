using DevNews.Application.Common.Services;
using DevNews.Application.NewsItem.Commands;
using DevNews.Domain.Common.Models;
using Mediator;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.NightlyCrawl;

public class Activities
{
    private readonly IMediator _mediator;
    private readonly ILogger<Activities> _logger;

    public Activities(IMediator mediator, ILogger<Activities> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [Function(nameof(DiscoverArticlesActivity))]
    public async Task<List<CrawledArticle>> DiscoverArticlesActivity(
        [ActivityTrigger] object? input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Discovering articles");

        var result = await _mediator.Send(new DiscoverArticlesCommand(), cancellationToken);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Discovery failed: {result.ErrorMessage}");
        }

        return result.Data!.ToList();
    }

    [Function(nameof(CurateArticleActivity))]
    public async Task<CleanedArticle?> CurateArticleActivity(
        [ActivityTrigger] CrawledArticle article,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Curating article {Url}", article.Url);

        var result = await _mediator.Send(new CurateArticleCommand(article), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Curation failed for {Url}: {Error}", article.Url, result.ErrorMessage);
            return null;
        }

        return result.Data;
    }

    [Function(nameof(CheckDuplicationActivity))]
    public async Task<bool> CheckDuplicationActivity(
        [ActivityTrigger] CleanedArticle article,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Checking duplication for {Title}", article.Title);

        var result = await _mediator.Send(new CheckDuplicationCommand(article), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Duplication check failed for {Url}, assuming not duplicate", article.Url);
            return false; // Fail-open
        }

        return result.Data;
    }

    [Function(nameof(PersistNewsItemActivity))]
    public async Task<Guid?> PersistNewsItemActivity(
        [ActivityTrigger] CleanedArticle article,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Persisting news item {Title}", article.Title);

        var result = await _mediator.Send(new PersistNewsItemCommand(article), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("Persist failed for {Url}: {Error}", article.Url, result.ErrorMessage);
            return null;
        }

        return result.Data;
    }
}
