using DevNews.Application.Common.Models;
using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

public interface IPlatformVideoPublisher
{
    Task<ResultResponse<PlatformPublishResult>> PublishAsync(
        string videoUrl,
        string title,
        string description,
        string[] tags,
        CancellationToken ct = default);
}
