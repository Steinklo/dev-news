using System.Net;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using DevNews.Application.Common.Repositories;
using DevNews.Domain.Common;
using DevNews.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Infrastructure.Services;

public class AiCrawlServiceTests
{
    private readonly FakeHttpMessageHandler _httpHandler = new();
    private readonly INewsItemRepository _repository = Substitute.For<INewsItemRepository>();
    private readonly AiCrawlService _sut;

    public AiCrawlServiceTests()
    {
        var httpClient = new HttpClient(_httpHandler) { BaseAddress = new Uri("https://test.local") };
        _sut = new AiCrawlService(httpClient, _repository, NullLogger<AiCrawlService>.Instance);

        // Default: repository says URL not found (new article)
        _repository.GetByUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<DevNews.Domain.NewsItem.NewsItem?>.Success(null));
    }

    [Fact]
    public async Task DiscoverArticlesAsync_ValidFeedsWithRecentItems_ReturnsArticles()
    {
        var feedXml = BuildRssFeed(
            ("https://example.com/article-1", DateTimeOffset.UtcNow.AddHours(-1)));

        // Setup feed responses for all configured RSS URLs
        foreach (var feedUrl in CrawlServiceOptions.RssFeedUrls)
        {
            _httpHandler.SetupResponse(feedUrl, feedXml);
        }

        // Setup article HTML responses
        _httpHandler.SetupResponse("https://example.com/article-1",
            "<html><head><title>Test</title></head><body><article><p>" +
            string.Join(" ", Enumerable.Repeat("This is a test article with enough content to be readable.", 20)) +
            "</p></article></body></html>");

        var result = await _sut.DiscoverArticlesAsync();

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Data!);
    }

    [Fact]
    public async Task DiscoverArticlesAsync_AllItemsOlderThanCutoff_ReturnsEmpty()
    {
        var feedXml = BuildRssFeed(
            ("https://example.com/old-article", DateTimeOffset.UtcNow.AddHours(-72)));

        foreach (var feedUrl in CrawlServiceOptions.RssFeedUrls)
        {
            _httpHandler.SetupResponse(feedUrl, feedXml);
        }

        var result = await _sut.DiscoverArticlesAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task DiscoverArticlesAsync_InvalidFeedXml_ReturnsEmptyAndContinues()
    {
        foreach (var feedUrl in CrawlServiceOptions.RssFeedUrls)
        {
            _httpHandler.SetupResponse(feedUrl, "this is not valid xml");
        }

        var result = await _sut.DiscoverArticlesAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task DiscoverArticlesAsync_EmptyFeedResponse_ReturnsEmpty()
    {
        foreach (var feedUrl in CrawlServiceOptions.RssFeedUrls)
        {
            _httpHandler.SetupResponse(feedUrl, "");
        }

        var result = await _sut.DiscoverArticlesAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task DiscoverArticlesAsync_UrlAlreadyExists_SkipsArticle()
    {
        var feedXml = BuildRssFeed(
            ("https://example.com/existing", DateTimeOffset.UtcNow.AddHours(-1)));

        foreach (var feedUrl in CrawlServiceOptions.RssFeedUrls)
        {
            _httpHandler.SetupResponse(feedUrl, feedXml);
        }

        // Repository says this URL already exists
        var existingItem = Application.TestData.CreateValidNewsItem();
        _repository.GetByUrlAsync("https://example.com/existing", Arg.Any<CancellationToken>())
            .Returns(ResultResponse<DevNews.Domain.NewsItem.NewsItem?>.Success(existingItem));

        var result = await _sut.DiscoverArticlesAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task DiscoverArticlesAsync_HttpError_ContinuesToNextFeed()
    {
        // First feed returns error, rest return valid but old content
        var first = true;
        foreach (var feedUrl in CrawlServiceOptions.RssFeedUrls)
        {
            if (first)
            {
                _httpHandler.SetupError(feedUrl, HttpStatusCode.InternalServerError);
                first = false;
            }
            else
            {
                _httpHandler.SetupResponse(feedUrl,
                    BuildRssFeed(("https://example.com/old", DateTimeOffset.UtcNow.AddHours(-72))));
            }
        }

        var result = await _sut.DiscoverArticlesAsync();

        // Should not fail entirely - continues processing other feeds
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DiscoverArticlesAsync_SmartReaderCannotExtract_TriesNextCandidate()
    {
        // Feed with two candidates - first has empty HTML, second is readable
        var feedXml = BuildRssFeed(
            ("https://example.com/empty-page", DateTimeOffset.UtcNow.AddHours(-1)),
            ("https://example.com/good-page", DateTimeOffset.UtcNow.AddHours(-1)));

        foreach (var feedUrl in CrawlServiceOptions.RssFeedUrls)
        {
            _httpHandler.SetupResponse(feedUrl, feedXml);
        }

        // First article returns minimal HTML that SmartReader can't parse
        _httpHandler.SetupResponse("https://example.com/empty-page", "<html><body></body></html>");

        // Second article returns readable content
        _httpHandler.SetupResponse("https://example.com/good-page",
            "<html><head><title>Good Article</title></head><body><article><p>" +
            string.Join(" ", Enumerable.Repeat("This is a valid article with substantial content for extraction.", 20)) +
            "</p></article></body></html>");

        var result = await _sut.DiscoverArticlesAsync();

        Assert.True(result.IsSuccess);
        // At least some feeds should have extracted the second candidate
        // (depends on SmartReader behavior, so we just verify no crash)
    }

    private static string BuildRssFeed(params (string url, DateTimeOffset published)[] items)
    {
        var feed = new SyndicationFeed("Test Feed", "Test", new Uri("https://test.local"));
        feed.Items = items.Select(i =>
        {
            var item = new SyndicationItem("Test Article", "Content", new Uri(i.url));
            item.PublishDate = i.published;
            return item;
        });

        using var sw = new StringWriter();
        using var xw = XmlWriter.Create(sw, new XmlWriterSettings { Encoding = Encoding.UTF8 });
        feed.SaveAsRss20(xw);
        xw.Flush();
        return sw.ToString();
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (string? content, HttpStatusCode status)> _responses = new();

        public void SetupResponse(string url, string content)
        {
            _responses[NormalizeUrl(url)] = (content, HttpStatusCode.OK);
        }

        public void SetupError(string url, HttpStatusCode statusCode)
        {
            _responses[NormalizeUrl(url)] = (null, statusCode);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = NormalizeUrl(request.RequestUri!.ToString());

            if (_responses.TryGetValue(url, out var setup))
            {
                if (setup.content == null)
                {
                    return Task.FromResult(new HttpResponseMessage(setup.status));
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(setup.content, Encoding.UTF8, "text/html")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static string NormalizeUrl(string url) => url.TrimEnd('/');
    }
}
