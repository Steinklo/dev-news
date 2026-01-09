using DevNews.Domain.Common;

namespace DevNews.Domain.Services;

public record CrawledArticle(
    string Html,
    Uri Url);

public interface ICrawlService
{
    Task<ResultResponse<IEnumerable<CrawledArticle>>> DiscoverArticlesAsync(CancellationToken ct = default);
}

