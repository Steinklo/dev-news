using System.Net;
using DevNews.Application.Common.Repositories;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem;
using Microsoft.Azure.Cosmos;

namespace DevNews.Infrastructure.Repositories;

public sealed class NewsItemCosmosRepository(CosmosClient client, string databaseId, string containerId)
    : INewsItemRepository
{
    private readonly Container _container = client.GetContainer(databaseId, containerId);

    public async Task<ResultResponse<NewsItem?>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<NewsItem>(id.ToString(), new PartitionKey(id.ToString()),
                cancellationToken: cancellationToken);
            return ResultResponse<NewsItem?>.Success(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return ResultResponse<NewsItem?>.Failure($"NewsItem {id} not found");
        }
        catch (Exception ex)
        {
            return ResultResponse<NewsItem?>.Failure($"Failed to fetch NewsItem {id}: {ex.Message}");
        }
    }

    public async Task<ResultResponse<NewsItem?>> GetByUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.Url.Value = @url")
                .WithParameter("@url", url);

            var iterator = _container.GetItemQueryIterator<NewsItem>(query);
            
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var newsItem = response.FirstOrDefault();
                return ResultResponse<NewsItem?>.Success(newsItem);
            }

            return ResultResponse<NewsItem?>.Success(null);
        }
        catch (Exception ex)
        {
            return ResultResponse<NewsItem?>.Failure($"Failed to fetch NewsItem by URL: {ex.Message}");
        }
    }

    public async Task<ResultResponse<IEnumerable<NewsItem>>> GetRecentAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c ORDER BY c.PublishedAt DESC OFFSET 0 LIMIT @limit")
                .WithParameter("@limit", limit);

            var iterator = _container.GetItemQueryIterator<NewsItem>(query);
            var results = new List<NewsItem>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(response);
            }

            return ResultResponse<IEnumerable<NewsItem>>.Success(results);
        }
        catch (Exception ex)
        {
            return ResultResponse<IEnumerable<NewsItem>>.Failure($"Failed to fetch recent NewsItems: {ex.Message}");
        }
    }

    public async Task<ResultResponse<IEnumerable<NewsItem>>> GetByCategoryAndDateRangeAsync(
        string category, 
        DateTimeOffset startDate, 
        DateTimeOffset endDate, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.Category.Value = @category AND c.PublishedAt >= @startDate AND c.PublishedAt < @endDate")
                .WithParameter("@category", category)
                .WithParameter("@startDate", startDate.ToString("o"))
                .WithParameter("@endDate", endDate.ToString("o"));

            var iterator = _container.GetItemQueryIterator<NewsItem>(query);
            var results = new List<NewsItem>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(response);
            }

            return ResultResponse<IEnumerable<NewsItem>>.Success(results);
        }
        catch (Exception ex)
        {
            return ResultResponse<IEnumerable<NewsItem>>.Failure(
                $"Failed to fetch NewsItems by category and date range: {ex.Message}");
        }
    }

    public async Task<ResultResponse<NewsItem>> AddAsync(NewsItem newsItem,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.CreateItemAsync(newsItem, new PartitionKey(newsItem.Id.ToString()),
                cancellationToken: cancellationToken);
            return ResultResponse<NewsItem>.Success(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return ResultResponse<NewsItem>.Failure($"NewsItem {newsItem.Id} already exists");
        }
        catch (Exception ex)
        {
            return ResultResponse<NewsItem>.Failure($"Failed to add NewsItem {newsItem.Id}: {ex.Message}");
        }
    }

    public async Task<ResultResponse<NewsItem>> UpdateAsync(NewsItem newsItem,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.UpsertItemAsync(newsItem, new PartitionKey(newsItem.Id.ToString()),
                cancellationToken: cancellationToken);
            return ResultResponse<NewsItem>.Success(response.Resource);
        }
        catch (Exception ex)
        {
            return ResultResponse<NewsItem>.Failure($"Failed to update NewsItem {newsItem.Id}: {ex.Message}");
        }
    }
}