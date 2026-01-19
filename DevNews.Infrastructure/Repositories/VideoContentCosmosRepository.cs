using DevNews.Application.Common.Repositories;
using DevNews.Domain.Common;
using DevNews.Domain.VideoContent.Enums;
using DevNews.Infrastructure.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Repositories;

/// <summary>
/// Cosmos DB repository implementation for VideoContent aggregate.
/// Partition Key: /newsItemId
/// </summary>
public class VideoContentCosmosRepository : IVideoContentRepository
{
    private readonly Container _container;
    private readonly ILogger<VideoContentCosmosRepository> _logger;

    public VideoContentCosmosRepository(
        CosmosClient cosmosClient,
        string databaseId,
        string containerId,
        ILogger<VideoContentCosmosRepository> logger)
    {
        _container = cosmosClient.GetContainer(databaseId, containerId);
        _logger = logger;
    }

    public async Task<Domain.VideoContent.VideoContent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            // First, we need to query to find the document since we don't have the partition key
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", id.ToString());

            var iterator = _container.GetItemQueryIterator<VideoContentDocument>(query);
            var response = await iterator.ReadNextAsync(ct);

            var document = response.FirstOrDefault();
            return document?.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get VideoContent by ID {VideoContentId}", id);
            throw;
        }
    }

    public async Task<Domain.VideoContent.VideoContent?> GetByNewsItemIdAsync(Guid newsItemId, CancellationToken ct = default)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.newsItemId = @newsItemId")
                .WithParameter("@newsItemId", newsItemId.ToString());

            var iterator = _container.GetItemQueryIterator<VideoContentDocument>(query);
            var response = await iterator.ReadNextAsync(ct);

            var document = response.FirstOrDefault();
            return document?.ToDomain();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get VideoContent by NewsItemId {NewsItemId}", newsItemId);
            throw;
        }
    }

    public async Task<IEnumerable<Domain.VideoContent.VideoContent>> GetByStatusAsync(
        VideoStatusEnum status,
        CancellationToken ct = default)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
                .WithParameter("@status", status.ToString());

            var iterator = _container.GetItemQueryIterator<VideoContentDocument>(query);
            var results = new List<Domain.VideoContent.VideoContent>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(ct);
                results.AddRange(response.Select(doc => doc.ToDomain()));
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get VideoContent by status {Status}", status);
            throw;
        }
    }

    public async Task<ResultResponse<Domain.VideoContent.VideoContent>> AddAsync(
        Domain.VideoContent.VideoContent videoContent,
        CancellationToken ct = default)
    {
        try
        {
            var document = VideoContentDocument.FromDomain(videoContent);
            var partitionKey = new PartitionKey(document.NewsItemId);

            await _container.CreateItemAsync(document, partitionKey, cancellationToken: ct);

            _logger.LogInformation("Successfully created VideoContent {VideoContentId} for NewsItem {NewsItemId}",
                videoContent.Id, videoContent.NewsItemId);

            return ResultResponse<Domain.VideoContent.VideoContent>.Success(videoContent);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogWarning("VideoContent {VideoContentId} already exists", videoContent.Id);
            return ResultResponse<Domain.VideoContent.VideoContent>.Failure("VideoContent already exists");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create VideoContent {VideoContentId}", videoContent.Id);
            return ResultResponse<Domain.VideoContent.VideoContent>.Failure($"Failed to create VideoContent: {ex.Message}");
        }
    }

    public async Task<ResultResponse<Domain.VideoContent.VideoContent>> UpdateAsync(
        Domain.VideoContent.VideoContent videoContent,
        CancellationToken ct = default)
    {
        try
        {
            var document = VideoContentDocument.FromDomain(videoContent);
            var partitionKey = new PartitionKey(document.NewsItemId);

            await _container.ReplaceItemAsync(
                document,
                document.Id,
                partitionKey,
                cancellationToken: ct);

            _logger.LogInformation("Successfully updated VideoContent {VideoContentId}", videoContent.Id);

            return ResultResponse<Domain.VideoContent.VideoContent>.Success(videoContent);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("VideoContent {VideoContentId} not found for update", videoContent.Id);
            return ResultResponse<Domain.VideoContent.VideoContent>.Failure("VideoContent not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update VideoContent {VideoContentId}", videoContent.Id);
            return ResultResponse<Domain.VideoContent.VideoContent>.Failure($"Failed to update VideoContent: {ex.Message}");
        }
    }
}
