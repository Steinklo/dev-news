using DevNews.Application.Common.Models;
using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

public interface ISocialPostPublisher
{
    Task<ResultResponse<PlatformPublishResult>> PublishTextAsync(
        string text,
        CancellationToken ct = default);
}
