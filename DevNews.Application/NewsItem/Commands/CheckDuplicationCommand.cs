using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using DevNews.Domain.Common.Models;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.NewsItem.Commands;

public record CheckDuplicationCommand(CleanedArticle Article) : IRequest<ResultResponse<bool>>;

public class CheckDuplicationHandler : IRequestHandler<CheckDuplicationCommand, ResultResponse<bool>>
{
    private readonly IDuplicationService _duplicationService;
    private readonly ILogger<CheckDuplicationHandler> _logger;

    public CheckDuplicationHandler(
        IDuplicationService duplicationService,
        ILogger<CheckDuplicationHandler> logger)
    {
        _duplicationService = duplicationService;
        _logger = logger;
    }

    public async ValueTask<ResultResponse<bool>> Handle(
        CheckDuplicationCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Checking duplication for: {Title}", request.Article.Title);

        var result = await _duplicationService.IsDuplicateAsync(request.Article, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Duplication check failed for {Url}: {Error}",
                request.Article.Url,
                result.ErrorMessage);
            return result;
        }

        if (result.Data)
        {
            _logger.LogInformation("Duplicate detected: {Title}", request.Article.Title);
        }

        return result;
    }
}
