using System.ServiceModel.Syndication;
using System.Xml;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevNews.Infrastructure.Services;

public class CrawlServiceOptions
{
    public const string SectionName = "CrawlService";

    /// <summary>
    /// RSS feed URLs to crawl for developer news
    /// </summary>
    public List<string> RssFeedUrls { get; set; } =
    [
        "https://news.ycombinator.com/rss",
        "https://lobste.rs/rss",
        "https://dev.to/feed",
        "https://github.blog/feed/",
        "https://blog.golang.org/feed.atom",
        "https://devblogs.microsoft.com/dotnet/feed/",
        "https://kubernetes.io/feed.xml",
        "https://aws.amazon.com/blogs/aws/feed/",
        "https://cloud.google.com/blog/rss"
    ];

    /// <summary>
    /// Maximum number of articles to fetch per feed
    /// </summary>
    public int MaxArticlesPerFeed { get; set; } = 10;

    /// <summary>
    /// Maximum age of articles to consider (in hours)
    /// </summary>
    public int MaxArticleAgeHours { get; set; } = 48;
}

public class ArticleCrawlService : ICrawlService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ArticleCrawlService> _logger;
    private readonly CrawlServiceOptions _options;

    public ArticleCrawlService(
        HttpClient httpClient,
        ILogger<ArticleCrawlService> logger,
        IOptions<CrawlServiceOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "DevNews/1.0 (Developer News Aggregator; +https://github.com/devnews)");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<ResultResponse<IEnumerable<CrawledArticle>>> DiscoverArticlesAsync(CancellationToken ct = default)
    {
        var allArticles = new List<CrawledArticle>();
        var cutoffTime = DateTimeOffset.UtcNow.AddHours(-_options.MaxArticleAgeHours);

        _logger.LogInformation(
            "Starting article discovery from {FeedCount} RSS feeds, max age: {MaxAge}h",
            _options.RssFeedUrls.Count,
            _options.MaxArticleAgeHours);

        foreach (var feedUrl in _options.RssFeedUrls)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var feedArticles = await DiscoverFromFeedAsync(feedUrl, cutoffTime, ct);
                allArticles.AddRange(feedArticles);

                _logger.LogDebug(
                    "Discovered {Count} articles from {Feed}",
                    feedArticles.Count,
                    feedUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process feed {FeedUrl}", feedUrl);
                // Continue with other feeds
            }
        }

        _logger.LogInformation("Total articles discovered: {Count}", allArticles.Count);
        return ResultResponse<IEnumerable<CrawledArticle>>.Success(allArticles);
    }

    private async Task<List<CrawledArticle>> DiscoverFromFeedAsync(
        string feedUrl,
        DateTimeOffset cutoffTime,
        CancellationToken ct)
    {
        var articles = new List<CrawledArticle>();

        // Fetch the RSS feed
        var feedXml = await FetchContentAsync(new Uri(feedUrl), ct);
        if (string.IsNullOrWhiteSpace(feedXml))
        {
            _logger.LogWarning("Empty response from feed {FeedUrl}", feedUrl);
            return articles;
        }

        // Parse the feed
        SyndicationFeed feed;
        try
        {
            using var stringReader = new StringReader(feedXml);
            using var xmlReader = XmlReader.Create(stringReader);
            feed = SyndicationFeed.Load(xmlReader);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse feed {FeedUrl}", feedUrl);
            return articles;
        }

        // Process feed items
        var recentItems = feed.Items
            .Where(item => item.PublishDate >= cutoffTime || item.LastUpdatedTime >= cutoffTime)
            .Take(_options.MaxArticlesPerFeed)
            .ToList();

        foreach (var item in recentItems)
        {
            ct.ThrowIfCancellationRequested();

            var articleUrl = GetArticleUrl(item);
            if (articleUrl == null)
            {
                continue;
            }

            try
            {
                // Fetch the full article HTML
                var html = await FetchContentAsync(articleUrl, ct);
                if (string.IsNullOrWhiteSpace(html))
                {
                    _logger.LogDebug("Empty HTML for article {Url}", articleUrl);
                    continue;
                }

                articles.Add(new CrawledArticle(html, articleUrl));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to fetch article {Url}", articleUrl);
                // Continue with other articles
            }
        }

        return articles;
    }

    private static Uri? GetArticleUrl(SyndicationItem item)
    {
        // Try to get the link from the item
        var link = item.Links.FirstOrDefault(l =>
            l.RelationshipType == "alternate" ||
            string.IsNullOrEmpty(l.RelationshipType));

        if (link?.Uri != null)
        {
            return link.Uri;
        }

        // Fallback to Id if it's a URL
        if (!string.IsNullOrEmpty(item.Id) && Uri.TryCreate(item.Id, UriKind.Absolute, out var idUri))
        {
            return idUri;
        }

        return null;
    }

    private async Task<string> FetchContentAsync(Uri url, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "HTTP error fetching {Url}: {Status}", url, ex.StatusCode);
            return string.Empty;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogDebug("Timeout fetching {Url}", url);
            return string.Empty;
        }
    }
}