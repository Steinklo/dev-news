using System.Net;
using DevNews.Application.Common.Repositories;
using DevNews.Domain.Common;
using DevNews.Domain.SocialPost;
using DevNews.Infrastructure.Persistence;
using Microsoft.Azure.Cosmos;

namespace DevNews.Infrastructure.Repositories;

public sealed class SocialPostCosmosRepository(CosmosClient client, string databaseId, string containerId)
    : ISocialPostRepository
{
    private readonly Container _container = client.GetContainer(databaseId, containerId);

    public async Task<ResultResponse<SocialPost>> AddAsync(SocialPost socialPost, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = SocialPostDocument.FromDomain(socialPost);

            var response = await _container.CreateItemAsync(document, new PartitionKey(document.Key),
                cancellationToken: cancellationToken);
            return ResultResponse<SocialPost>.Success(response.Resource.ToDomain());
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return ResultResponse<SocialPost>.Failure($"SocialPost {socialPost.Id} already exists");
        }
        catch (Exception ex)
        {
            return ResultResponse<SocialPost>.Failure($"Failed to add SocialPost {socialPost.Id}: {ex.Message}");
        }
    }

    public async Task<ResultResponse<IEnumerable<Guid>>> GetNewsItemIdsWithPostsAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var partitionKey = $"textpost_{date:yyyy-MM}";
            var dayStart = new DateTimeOffset(date, TimeOnly.MinValue, TimeSpan.Zero);
            var dayEnd = dayStart.AddDays(1);

            var query = new QueryDefinition(
                "SELECT c.NewsItemId FROM c WHERE c.Key = @key AND c.CreatedAt >= @start AND c.CreatedAt < @end")
                .WithParameter("@key", partitionKey)
                .WithParameter("@start", dayStart)
                .WithParameter("@end", dayEnd);

            var iterator = _container.GetItemQueryIterator<SocialPostDocument>(query,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) });

            var ids = new List<Guid>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                ids.AddRange(response.Select(doc => Guid.Parse(doc.NewsItemId)));
            }

            return ResultResponse<IEnumerable<Guid>>.Success(ids);
        }
        catch (Exception ex)
        {
            return ResultResponse<IEnumerable<Guid>>.Failure(
                $"Failed to fetch NewsItem IDs with posts for {date}: {ex.Message}");
        }
    }
}
