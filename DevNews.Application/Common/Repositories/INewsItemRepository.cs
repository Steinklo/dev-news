using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;

namespace DevNews.Application.Common.Repositories;

public interface INewsItemRepository
{
    Task<ResultResponse<Domain.NewsItem.NewsItem?>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ResultResponse<Domain.NewsItem.NewsItem?>> GetByUrlAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get news items by category and month. Uses partition key for efficient queries.
    /// </summary>
    Task<ResultResponse<IEnumerable<Domain.NewsItem.NewsItem>>> GetByCategoryAndMonthAsync(
        CategoryEnum category,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<ResultResponse<Domain.NewsItem.NewsItem>> AddAsync(
        Domain.NewsItem.NewsItem newsItem,
        CancellationToken cancellationToken = default);

    Task<ResultResponse<Domain.NewsItem.NewsItem>> UpdateAsync(Domain.NewsItem.NewsItem newsItem, CancellationToken cancellationToken = default);
}
