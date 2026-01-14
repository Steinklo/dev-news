using DevNews.Domain.Common;
using DevNews.Application.Common.Models;

namespace DevNews.Application.Common.Services;

public interface IDuplicationService
{
    Task<ResultResponse<bool>> IsDuplicateAsync(
        CleanedArticle article,
        CancellationToken ct = default);
}

