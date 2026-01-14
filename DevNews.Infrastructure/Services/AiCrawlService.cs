using System.ServiceModel.Syndication;
using System.Xml;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Microsoft.Extensions.Logging;
using SmartReader;

namespace DevNews.Infrastructure.Services;

public static class CrawlServiceOptions
{
    public static int MaxArticlesPerFeed => 1;
    public static int MaxArticleAgeHours => 48;

    public static IReadOnlyList<string> RssFeedUrls =>
    [
        // Security & Vulnerabilities
        "https://github.com/security-advisories.atom",
        // "https://nvd.nist.gov/feeds/xml/cve/misc/nvd-rss-analyzed.xml",
        // "https://www.cisa.gov/uscert/ncas/alerts.xml",
        // "https://snyk.io/vuln/feed",
        // "https://krebsonsecurity.com/feed/",
        // "https://feeds.feedburner.com/TheHackersNews",
        // "https://www.bleepingcomputer.com/feed/",
        // "https://www.troyhunt.com/rss/",

        // Programming Languages & Runtimes
        "https://nodejs.org/en/feed/blog.xml",
        // "https://devblogs.microsoft.com/typescript/feed/",
        // "https://blog.python.org/feeds/posts/default",
        // "https://go.dev/blog/feed.atom",
        // "https://blog.rust-lang.org/feed.xml",
        // "https://devblogs.microsoft.com/dotnet/feed/",

        // Cloud & Infrastructure
        "https://azure.microsoft.com/en-us/updates/feed/",
        // "https://aws.amazon.com/blogs/aws/feed/",
        // "https://cloud.google.com/blog/rss",
        // "https://kubernetes.io/feed.xml",

        // DevOps, CI/CD & Tools
        "https://github.blog/feed/",
        // "https://www.docker.com/blog/feed/",
        // "https://www.datadoghq.com/blog/feed/",
        // "https://about.gitlab.com/atom.xml",
        // "https://code.visualstudio.com/feed.xml",

        // Frameworks & Libraries
        "https://react.dev/rss.xml",
        // "https://nextjs.org/feed.xml",
        // "https://spring.io/blog.atom",
        // "https://www.djangoproject.com/rss/weblog/",

        // Developer News Aggregators
        "https://hnrss.org/frontpage",
        // "https://lobste.rs/rss",
        // "https://www.infoq.com/feed"
    ];
}

public class AiCrawlService : ICrawlService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiCrawlService> _logger;

    public AiCrawlService(
        HttpClient httpClient,
        ILogger<AiCrawlService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;


        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "DevNews/1.0 (Developer News Aggregator; +https://github.com/devnews)");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<ResultResponse<IEnumerable<CrawledArticle>>> DiscoverArticlesAsync(CancellationToken ct = default)
    {
        var allArticles = new List<CrawledArticle>();
        var cutoffTime = DateTimeOffset.UtcNow.AddHours(-CrawlServiceOptions.MaxArticleAgeHours);

        _logger.LogInformation(
            "Starting article discovery from {FeedCount} RSS feeds, max age: {MaxAge}h",
            CrawlServiceOptions.RssFeedUrls.Count,
            CrawlServiceOptions.MaxArticleAgeHours);

        foreach (var feedUrl in CrawlServiceOptions.RssFeedUrls)
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
            .Take(CrawlServiceOptions.MaxArticlesPerFeed)
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

                // Extract clean article content using SmartReader
                var reader = new Reader(articleUrl.ToString(), html);
                var article = reader.GetArticle();

                if (article == null || !article.IsReadable || string.IsNullOrWhiteSpace(article.TextContent))
                {
                    _logger.LogDebug("SmartReader couldn't extract content from {Url}", articleUrl);
                    continue;
                }

                _logger.LogDebug(
                    "Extracted article: {Length} chars (was {OriginalLength} chars HTML)",
                    article.TextContent.Length,
                    html.Length);

                articles.Add(new CrawledArticle(article.TextContent, articleUrl));
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