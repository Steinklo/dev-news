using DevNews.Application.Common.Services;
using DevNews.Application.NewsItem.Commands;
using DevNews.Domain.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Application.NewsItem.Commands;

public class DiscoverArticlesHandlerTests
{
    private readonly ICrawlService _crawlService = Substitute.For<ICrawlService>();
    private readonly DiscoverArticlesHandler _handler;

    public DiscoverArticlesHandlerTests()
    {
        _handler = new DiscoverArticlesHandler(
            _crawlService,
            NullLogger<DiscoverArticlesHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ServiceReturnsArticles_ReturnsSuccessWithArticles()
    {
        var articles = new List<CrawledArticle>
        {
            new("<html>article1</html>", new Uri("https://example.com/article1")),
            new("<html>article2</html>", new Uri("https://example.com/article2"))
        };
        _crawlService.DiscoverArticlesAsync(Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<CrawledArticle>>.Success(articles));

        var result = await _handler.Handle(new DiscoverArticlesCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task Handle_ServiceReturnsFailure_ReturnsFailure()
    {
        _crawlService.DiscoverArticlesAsync(Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<CrawledArticle>>.Failure("Crawl failed"));

        var result = await _handler.Handle(new DiscoverArticlesCommand(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Crawl failed", result.ErrorMessage);
    }
}
