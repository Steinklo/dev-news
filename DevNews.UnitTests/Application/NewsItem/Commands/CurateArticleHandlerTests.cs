using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Application.NewsItem.Commands;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Application.NewsItem.Commands;

public class CurateArticleHandlerTests
{
    private readonly ICurationService _curationService = Substitute.For<ICurationService>();
    private readonly CurateArticleHandler _handler;

    public CurateArticleHandlerTests()
    {
        _handler = new CurateArticleHandler(
            _curationService,
            NullLogger<CurateArticleHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ServiceSucceeds_ReturnsCleanedArticle()
    {
        var crawled = new CrawledArticle("<html>content</html>", new Uri("https://example.com/article"));
        var cleaned = new CleanedArticle(
            Title: "Critical Security Vulnerability Found in Popular Library",
            Summary: TestData.ValidSummary,
            Category: CategoryEnum.AiModelsAndApis,
            Url: new Uri("https://example.com/article"),
            RelevanceScore: 85,
            PublishedAt: DateTimeOffset.UtcNow);

        _curationService.CurateAsync(crawled, Arg.Any<CancellationToken>())
            .Returns(ResultResponse<CleanedArticle>.Success(cleaned));

        var result = await _handler.Handle(new CurateArticleCommand(crawled), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(cleaned.Title, result.Data!.Title);
    }

    [Fact]
    public async Task Handle_ServiceFails_ReturnsFailure()
    {
        var crawled = new CrawledArticle("<html>bad</html>", new Uri("https://example.com/bad"));

        _curationService.CurateAsync(crawled, Arg.Any<CancellationToken>())
            .Returns(ResultResponse<CleanedArticle>.Failure("Curation failed"));

        var result = await _handler.Handle(new CurateArticleCommand(crawled), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Curation failed", result.ErrorMessage);
    }
}
