using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using DevNews.Domain.Common.Models;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.NewsItem.Commands;

public record CurateArticleCommand(CrawledArticle Article) : IRequest<ResultResponse<CleanedArticle>>;

public class CurateArticleHandler : IRequestHandler<CurateArticleCommand, ResultResponse<CleanedArticle>>
{
    private readonly ICurationService _curationService;
    private readonly ILogger<CurateArticleHandler> _logger;

    public CurateArticleHandler(
        ICurationService curationService,
        ILogger<CurateArticleHandler> logger)
    {
        _curationService = curationService;
        _logger = logger;
    }

    public async ValueTask<ResultResponse<CleanedArticle>> Handle(
        CurateArticleCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Curating article: {Url}", request.Article.Url);

        var result = await _curationService.CurateAsync(request.Article, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Failed to curate article {Url}: {Error}",
                request.Article.Url,
                result.ErrorMessage);
            return result;
        }

        _logger.LogInformation(
            "Curated article: {Title} [{Category}] (relevance: {Score})",
            result.Data!.Title,
            result.Data.Category,
            result.Data.RelevanceScore);

        return result;
    }
}
