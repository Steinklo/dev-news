using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

/// <summary>
/// Service for storing and retrieving video files from Azure Blob Storage.
/// </summary>
public interface IVideoBlobStorage
{
    Task<ResultResponse<string>> UploadVideoAsync(
        byte[] videoData,
        string fileName,
        CancellationToken ct = default);

    Task<ResultResponse<string>> GetPublicUrlAsync(
        string fileName,
        CancellationToken ct = default);
}
