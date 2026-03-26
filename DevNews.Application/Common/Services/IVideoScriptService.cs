using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

public interface IVideoScriptService
{
    Task<ResultResponse<string>> GenerateScriptAsync(
        string title,
        string summary,
        string category,
        CancellationToken ct = default);
}
