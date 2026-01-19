using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

/// <summary>
/// Service for generating video scripts from news content using AI.
/// </summary>
public interface IVideoScriptGenerator
{
    Task<ResultResponse<string>> GenerateScriptAsync(
        string title,
        string summary,
        string category,
        int targetDurationSeconds,
        CancellationToken ct = default);
}
