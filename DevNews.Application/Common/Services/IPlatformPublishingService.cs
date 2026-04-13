using DevNews.Application.Common.Models;
using DevNews.Domain.Common;
using DevNews.Domain.ShortVideo.Enums;

namespace DevNews.Application.Common.Services;

public interface IPlatformPublishingService
{
    Task<ResultResponse<PlatformPublishResult>> PublishAsync(
        string videoUrl,
        string title,
        string description,
        string[] tags,
        Platform platform,
        CancellationToken ct = default);
}
