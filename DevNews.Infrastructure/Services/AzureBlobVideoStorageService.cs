using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

public class AzureBlobVideoStorageService : IVideoStorageService
{
    private readonly BlobContainerClient _videosContainer;
    private readonly BlobContainerClient _thumbnailsContainer;
    private readonly ILogger<AzureBlobVideoStorageService> _logger;

    public AzureBlobVideoStorageService(
        BlobServiceClient blobServiceClient,
        ILogger<AzureBlobVideoStorageService> logger)
    {
        _videosContainer = blobServiceClient.GetBlobContainerClient("videos");
        _thumbnailsContainer = blobServiceClient.GetBlobContainerClient("thumbnails");
        _logger = logger;
    }

    public async Task<ResultResponse<string>> UploadVideoAsync(
        byte[] videoData,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        try
        {
            var blobClient = _videosContainer.GetBlobClient(fileName);

            await blobClient.UploadAsync(
                new BinaryData(videoData),
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
                },
                ct);

            var url = blobClient.Uri.ToString();
            _logger.LogInformation("Uploaded video to blob: {Url}", url);
            return ResultResponse<string>.Success(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload video: {FileName}", fileName);
            return ResultResponse<string>.Failure($"Failed to upload video: {ex.Message}");
        }
    }

    public async Task<ResultResponse<string>> UploadThumbnailAsync(
        byte[] thumbnailData,
        string fileName,
        CancellationToken ct = default)
    {
        try
        {
            var blobClient = _thumbnailsContainer.GetBlobClient(fileName);

            await blobClient.UploadAsync(
                new BinaryData(thumbnailData),
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" }
                },
                ct);

            var url = blobClient.Uri.ToString();
            _logger.LogInformation("Uploaded thumbnail to blob: {Url}", url);
            return ResultResponse<string>.Success(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload thumbnail: {FileName}", fileName);
            return ResultResponse<string>.Failure($"Failed to upload thumbnail: {ex.Message}");
        }
    }
}
