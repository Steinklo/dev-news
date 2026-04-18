using System.ServiceModel.Syndication;
using System.Xml;
using DevNews.Application.Common.Repositories;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.ValueObjects;
using Microsoft.Extensions.Logging;
using SmartReader;

namespace DevNews.Infrastructure.Services;

public static class CrawlServiceOptions
{
    public static int MaxArticleAgeHours => 48;
    public static int MaxArticlesPerFeed => 3;
    public static int MaxCandidatesPerFeed => 10;

    public static IReadOnlyList<string> RssFeedUrls =>
    [
        // AI Model Providers
        "https://openai.com/blog/rss.xml",
        "https://www.anthropic.com/feed",
        "https://blog.google/technology/ai/rss/",
        "https://ai.meta.com/blog/rss/",
        "https://mistral.ai/feed/",

        // AI Research
        "https://deepmind.google/blog/rss.xml",

        // AI Developer Tools & Frameworks
        "https://huggingface.co/blog/feed.xml",
        "https://blog.langchain.dev/rss/",
        "https://blog.llamaindex.ai/feed",

        // AI Infrastructure & Cloud
        "https://developer.nvidia.com/blog/feed/",
        "https://aws.amazon.com/blogs/machine-learning/feed/",
        "https://together.ai/blog/rss",

        // Developer Platforms
        "https://github.blog/feed/",
        "https://devblogs.microsoft.com/ai-machine-learning/feed/",

        // AI News & Analysis
        "https://simonwillison.net/atom/everything/",
        "https://www.latent.space/feed",
        "https://read.deeplearning.ai/the-batch/feed",
        "https://www.infoq.com/ai-ml-data-eng/articles/rss/",

        // Security
        "https://feeds.feedburner.com/TheHackersNews",
        "https://krebsonsecurity.com/feed/",

        // High-Signal Aggregators
        "https://lobste.rs/t/ai.rss",
        "https://news.ycombinator.com/rss"
    ];
}

public class AiCrawlService : ICrawlService
{
    private readonly HttpClient _httpClient;
    private readonly INewsItemRepository _repository;
    private readonly ILogger<AiCrawlService> _logger;

    public AiCrawlService(
        HttpClient httpClient,
        INewsItemRepository repository,
        ILogger<AiCrawlService> logger)
    {
        _httpClient = httpClient;
        _repository = repository;
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

        // Get recent items, try up to 5 candidates until we extract one successfully
        var candidateItems = feed.Items
            .Where(item => item.PublishDate >= cutoffTime || item.LastUpdatedTime >= cutoffTime)
            .Take(CrawlServiceOptions.MaxCandidatesPerFeed)
            .ToList();

        foreach (var item in candidateItems)
        {
            if (articles.Count >= CrawlServiceOptions.MaxArticlesPerFeed)
                break;

            ct.ThrowIfCancellationRequested();

            var articleUrl = GetArticleUrl(item);
            if (articleUrl == null)
            {
                continue;
            }

            // Early duplicate check: canonicalize URL and check if already stored
            var newsUrlResult = NewsUrl.Create(articleUrl.ToString());
            if (!newsUrlResult.IsSuccess)
            {
                _logger.LogDebug("Invalid URL {Url}, skipping", articleUrl);
                continue;
            }

            var existingArticle = await _repository.GetByUrlAsync(newsUrlResult.Data!.Value, ct);
            if (existingArticle.IsSuccess && existingArticle.Data != null)
            {
                _logger.LogDebug("Article already exists: {Url}, skipping", articleUrl);
                continue;
            }

            try
            {
                // Fetch the full article HTML
                var html = await FetchContentAsync(articleUrl, ct);
                if (string.IsNullOrWhiteSpace(html))
                {
                    _logger.LogDebug("Empty HTML for article {Url}, trying next candidate", articleUrl);
                    continue;
                }

                // Extract clean article content using SmartReader
                var reader = new Reader(articleUrl.ToString(), html);
                var article = reader.GetArticle();

                if (article == null || !article.IsReadable || string.IsNullOrWhiteSpace(article.TextContent))
                {
                    _logger.LogDebug("SmartReader couldn't extract content from {Url}, trying next candidate", articleUrl);
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
                _logger.LogDebug(ex, "Failed to fetch article {Url}, trying next candidate", articleUrl);
                // Continue with next candidate
            }
        }

        if (articles.Count == 0 && candidateItems.Count > 0)
        {
            _logger.LogWarning(
                "Failed to extract any articles from {FeedUrl} after trying {Count} candidates",
                feedUrl, candidateItems.Count);
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