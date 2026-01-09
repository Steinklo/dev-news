using DevNews.Application.Common.Services;
using DevNews.Domain.Common;

namespace DevNews.Infrastructure.Services;


public class ArticleCrawlService(HttpClient httpClient) : ICrawlService
{
    public Task<ResultResponse<IEnumerable<CrawledArticle>>> DiscoverArticlesAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
    public async Task<ResultResponse<CrawledArticle>> CrawlArticleAsync(Uri url, CancellationToken ct = default)
    {
        try
        {
            // Fetch the HTML content
            var html = await FetchHtmlAsync(url, ct);
            if (string.IsNullOrWhiteSpace(html))
            {
                return ResultResponse<CrawledArticle>.Failure("Failed to fetch article HTML");
            }

            var crawledArticle = new CrawledArticle(html, url);
            return ResultResponse<CrawledArticle>.Success(crawledArticle);
        }
        catch (Exception ex)
        {
            return ResultResponse<CrawledArticle>.Failure($"Crawl failed: {ex.Message}");
        }
    }

    private async Task<string> FetchHtmlAsync(Uri url, CancellationToken ct)
    {
        try
        {
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            var response = await httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch
        {
            return string.Empty;
        }
    }

}