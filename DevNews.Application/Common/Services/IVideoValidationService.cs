using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

/// <summary>
/// Service for validating generated videos using AI.
/// </summary>
public interface IVideoValidationService
{
    Task<ResultResponse<(bool IsValid, string Reason)>> ValidateVideoAsync(
        string videoUrl,
        string originalScript,
        CancellationToken ct = default);
}
