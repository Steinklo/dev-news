using DevNews.Domain.Common;

namespace DevNews.Domain.ShortVideo.Events;

public class VideoCreatedEvent(
    ShortVideo shortVideo) : DomainEvent(shortVideo.Id)
{
    public ShortVideo ShortVideo { get; } = shortVideo;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
}
