using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

public interface IAiService
{
    Task<ResultResponse<string>> GenerateAsync(string prompt, CancellationToken ct = default);
}

