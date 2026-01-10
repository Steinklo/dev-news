using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.NewsItem.Commands;

public record DiscoverArticlesCommand : IRequest<ResultResponse<IReadOnlyList<CrawledArticle>>>;

public class DiscoverArticlesHandler : IRequestHandler<DiscoverArticlesCommand, ResultResponse<IReadOnlyList<CrawledArticle>>>
{
    private readonly ICrawlService _crawlService;
    private readonly ILogger<DiscoverArticlesHandler> _logger;

    public DiscoverArticlesHandler(
        ICrawlService crawlService,
        ILogger<DiscoverArticlesHandler> logger)
    {
        _crawlService = crawlService;
        _logger = logger;
    }

    public async ValueTask<ResultResponse<IReadOnlyList<CrawledArticle>>> Handle(
        DiscoverArticlesCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Discovering articles from configured sources");

        var result = await _crawlService.DiscoverArticlesAsync(cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to discover articles: {Error}", result.ErrorMessage);
            return ResultResponse<IReadOnlyList<CrawledArticle>>.Failure(result.ErrorMessage);
        }

        var articles = result.Data!.ToList();
        _logger.LogInformation("Discovered {Count} articles", articles.Count);

        return ResultResponse<IReadOnlyList<CrawledArticle>>.Success(articles);
    }
}
