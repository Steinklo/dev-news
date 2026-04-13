using DevNews.Application.Common.Models;
using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

public interface IVideoGenerationService
{
    Task<ResultResponse<GeneratedVideo>> GenerateVideoAsync(
        string script,
        string title,
        CancellationToken ct = default);
}
