using System.Net;
using DevNews.Application.Common.Repositories;
using DevNews.Domain.Common;
using DevNews.Domain.ShortVideo;
using DevNews.Infrastructure.Persistence;
using Microsoft.Azure.Cosmos;

namespace DevNews.Infrastructure.Repositories;

public sealed class ShortVideoCosmosRepository(CosmosClient client, string databaseId, string containerId)
    : IShortVideoRepository
{
    private readonly Container _container = client.GetContainer(databaseId, containerId);

    public async Task<ResultResponse<ShortVideo>> AddAsync(ShortVideo shortVideo, CancellationToken cancellationToken = default)
    {
        try
        {
            shortVideo.ClearDomainEvents();
            var document = ShortVideoDocument.FromDomain(shortVideo);

            var response = await _container.CreateItemAsync(document, new PartitionKey(document.Key),
                cancellationToken: cancellationToken);
            return ResultResponse<ShortVideo>.Success(response.Resource.ToDomain());
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return ResultResponse<ShortVideo>.Failure($"ShortVideo {shortVideo.Id} already exists");
        }
        catch (Exception ex)
        {
            return ResultResponse<ShortVideo>.Failure($"Failed to add ShortVideo {shortVideo.Id}: {ex.Message}");
        }
    }

    public async Task<ResultResponse<IEnumerable<Guid>>> GetNewsItemIdsWithVideosAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT c.NewsItemId FROM c WHERE c.CreatedAt >= @since")
                .WithParameter("@since", since);

            var iterator = _container.GetItemQueryIterator<ShortVideoDocument>(query);
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
                $"Failed to fetch NewsItem IDs with videos: {ex.Message}");
        }
    }
}
