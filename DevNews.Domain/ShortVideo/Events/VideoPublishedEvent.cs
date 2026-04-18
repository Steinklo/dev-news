using DevNews.Domain.Common;
using DevNews.Domain.Common.Enums;

namespace DevNews.Domain.ShortVideo.Events;

public class VideoPublishedEvent(
    ShortVideo shortVideo,
    Platform platform) : DomainEvent(shortVideo.Id)
{
    public ShortVideo ShortVideo { get; } = shortVideo;
    public Platform Platform { get; } = platform;
}
