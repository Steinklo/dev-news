using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

public interface IVideoStorageService
{
    Task<ResultResponse<string>> UploadVideoAsync(
        byte[] videoData,
        string fileName,
        string contentType,
        CancellationToken ct = default);

    Task<ResultResponse<string>> UploadThumbnailAsync(
        byte[] thumbnailData,
        string fileName,
        CancellationToken ct = default);
}
