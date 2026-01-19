using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

/// <summary>
/// Service for generating video files from scripts.
/// </summary>
public interface IVideoGenerator
{
    Task<ResultResponse<byte[]>> GenerateVideoAsync(
        string script,
        int durationSeconds,
        CancellationToken ct = default);
}
