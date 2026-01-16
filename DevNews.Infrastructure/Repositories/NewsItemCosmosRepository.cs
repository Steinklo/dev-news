using System.Net;
using DevNews.Application.Common.Repositories;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem;
using DevNews.Domain.NewsItem.Enums;
using DevNews.Infrastructure.Persistence;
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
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", id.ToString());

            var iterator = _container.GetItemQueryIterator<NewsItemDocument>(query);

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var doc = response.FirstOrDefault();
                return ResultResponse<NewsItem?>.Success(doc?.ToDomain());
            }

            return ResultResponse<NewsItem?>.Success(null);
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
            var query = new QueryDefinition("SELECT * FROM c WHERE c.Url = @url")
                .WithParameter("@url", url);

            var iterator = _container.GetItemQueryIterator<NewsItemDocument>(query);

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var doc = response.FirstOrDefault();
                return ResultResponse<NewsItem?>.Success(doc?.ToDomain());
            }

            return ResultResponse<NewsItem?>.Success(null);
        }
        catch (Exception ex)
        {
            return ResultResponse<NewsItem?>.Failure($"Failed to fetch NewsItem by URL: {ex.Message}");
        }
    }

    public async Task<ResultResponse<IEnumerable<NewsItem>>> GetByCategoryAndMonthAsync(
        CategoryEnum category,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use partition key for efficient single-partition query
            var partitionKey = $"{category}_{startDate:yyyy-MM}";

            var query = new QueryDefinition(
                "SELECT * FROM c ORDER BY c.CreatedAt DESC OFFSET 0 LIMIT @limit")
                .WithParameter("@limit", limit);

            var options = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(partitionKey)
            };

            var iterator = _container.GetItemQueryIterator<NewsItemDocument>(query, requestOptions: options);
            var results = new List<NewsItem>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(response.Select(doc => doc.ToDomain()));
            }

            return ResultResponse<IEnumerable<NewsItem>>.Success(results);
        }
        catch (Exception ex)
        {
            return ResultResponse<IEnumerable<NewsItem>>.Failure(
                $"Failed to fetch NewsItems by category and month: {ex.Message}");
        }
    }

    public async Task<ResultResponse<NewsItem>> AddAsync(
        NewsItem newsItem,
        CancellationToken cancellationToken = default)
    {
        try
        {
            newsItem.ClearDomainEvents();
            var document = NewsItemDocument.FromDomain(newsItem);

            var response = await _container.CreateItemAsync(document, new PartitionKey(document.Key),
                cancellationToken: cancellationToken);
            return ResultResponse<NewsItem>.Success(response.Resource.ToDomain());
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
            newsItem.ClearDomainEvents();
            var document = NewsItemDocument.FromDomain(newsItem);

            var response = await _container.UpsertItemAsync(document, new PartitionKey(document.Key),
                cancellationToken: cancellationToken);
            return ResultResponse<NewsItem>.Success(response.Resource.ToDomain());
        }
        catch (Exception ex)
        {
            return ResultResponse<NewsItem>.Failure($"Failed to update NewsItem {newsItem.Id}: {ex.Message}");
        }
    }
}