using DevNews.Application.Common.Models;
using DevNews.Domain.Common;
using DevNews.Domain.Common.Enums;

namespace DevNews.Application.Common.Services;

public interface ISocialPostPublisher
{
    Task<ResultResponse<PlatformPublishResult>> PublishTextAsync(
        string text,
        Platform platform,
        CancellationToken ct = default);
}
