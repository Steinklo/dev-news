using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

/// <summary>
/// Azure Blob Storage service for video file management.
/// </summary>
public class VideoBlobStorageService : IVideoBlobStorage
{
    private const string ContainerName = "videos";
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<VideoBlobStorageService> _logger;

    public VideoBlobStorageService(
        BlobServiceClient blobServiceClient,
        ILogger<VideoBlobStorageService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<ResultResponse<string>> UploadVideoAsync(
        byte[] videoData,
        string fileName,
        CancellationToken ct = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);

            // Create container if it doesn't exist (with public blob access)
            await containerClient.CreateIfNotExistsAsync(
                PublicAccessType.Blob,
                cancellationToken: ct);

            var blobClient = containerClient.GetBlobClient(fileName);

            using var stream = new MemoryStream(videoData);

            // Upload with metadata
            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = "video/mp4"
            };

            await blobClient.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    HttpHeaders = blobHttpHeaders
                },
                cancellationToken: ct);

            var url = blobClient.Uri.ToString();

            _logger.LogInformation("Successfully uploaded video to blob storage: {FileName} -> {Url}",
                fileName, url);

            return ResultResponse<string>.Success(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload video to blob storage: {FileName}", fileName);
            return ResultResponse<string>.Failure($"Upload failed: {ex.Message}");
        }
    }

    public async Task<ResultResponse<string>> GetPublicUrlAsync(
        string fileName,
        CancellationToken ct = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var blobClient = containerClient.GetBlobClient(fileName);

            // Check if blob exists
            var exists = await blobClient.ExistsAsync(ct);
            if (!exists.Value)
            {
                return ResultResponse<string>.Failure($"Video file not found: {fileName}");
            }

            return ResultResponse<string>.Success(blobClient.Uri.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get public URL for video: {FileName}", fileName);
            return ResultResponse<string>.Failure($"Failed to get URL: {ex.Message}");
        }
    }
}
