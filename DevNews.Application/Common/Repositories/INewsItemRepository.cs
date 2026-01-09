using DevNews.Domain.Common;

namespace DevNews.Application.Common.Repositories;

public interface INewsItemRepository
{
    Task<ResultResponse<Domain.NewsItem.NewsItem?>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ResultResponse<Domain.NewsItem.NewsItem?>> GetByUrlAsync(string url, CancellationToken cancellationToken = default);
    Task<ResultResponse<IEnumerable<Domain.NewsItem.NewsItem>>> GetRecentAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<ResultResponse<IEnumerable<Domain.NewsItem.NewsItem>>> GetByCategoryAndDateRangeAsync(
        string category, 
        DateTimeOffset startDate, 
        DateTimeOffset endDate, 
        CancellationToken cancellationToken = default);
    Task<ResultResponse<Domain.NewsItem.NewsItem>> AddAsync(Domain.NewsItem.NewsItem newsItem, CancellationToken cancellationToken = default);
    Task<ResultResponse<Domain.NewsItem.NewsItem>> UpdateAsync(Domain.NewsItem.NewsItem newsItem, CancellationToken cancellationToken = default);
}
