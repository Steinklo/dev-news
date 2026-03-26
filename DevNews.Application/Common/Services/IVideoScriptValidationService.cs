using DevNews.Application.Common.Models;
using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

public interface IVideoScriptValidationService
{
    Task<ResultResponse<ScriptValidationResult>> ValidateScriptAsync(
        string script,
        string originalSummary,
        CancellationToken ct = default);
}
