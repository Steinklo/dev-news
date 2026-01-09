using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

public record CrawledArticle(
    string Html,
    Uri Url);

public interface ICrawlService
{
    Task<ResultResponse<IEnumerable<CrawledArticle>>> DiscoverArticlesAsync(CancellationToken ct = default);
}