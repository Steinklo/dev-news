using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using DevNews.Domain.ShortVideo.Enums;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

public class PlatformPublishingRouter(
    YouTubePublishingService youTubeService,
    LinkedInPublishingService linkedInService,
    ILogger<PlatformPublishingRouter> logger) : IPlatformPublishingService
{
    public async Task<ResultResponse<PlatformPublishResult>> PublishAsync(
        string videoUrl,
        string title,
        string description,
        string[] tags,
        Platform platform,
        CancellationToken ct = default)
    {
        logger.LogDebug("Routing publish request to {Platform}", platform);

        return platform switch
        {
            Platform.YouTube => await youTubeService.PublishAsync(videoUrl, title, description, tags, ct),
            Platform.LinkedIn => await linkedInService.PublishAsync(videoUrl, title, description, tags, ct),
            _ => ResultResponse<PlatformPublishResult>.Failure($"Unsupported platform: {platform}")
        };
    }
}
