using DevNews.Domain.Common;

namespace DevNews.Domain.VideoContent.Events;

/// <summary>
/// Domain event raised when a video has been successfully published to platforms.
/// </summary>
public class VideoPublishedEvent(Guid videoContentId, int platformCount) : DomainEvent(videoContentId)
{
    public Guid VideoContentId { get; } = videoContentId;
    public int PlatformCount { get; } = platformCount;
    public DateTimeOffset PublishedAt { get; } = DateTimeOffset.UtcNow;
}
