using DevNews.Domain.Common;

namespace DevNews.Domain.VideoContent.Events;

/// <summary>
/// Domain event raised when video generation is requested for a NewsItem.
/// </summary>
public class VideoGenerationRequestedEvent(Guid newsItemId) : DomainEvent(newsItemId)
{
    public Guid NewsItemId { get; } = newsItemId;
    public DateTimeOffset RequestedAt { get; } = DateTimeOffset.UtcNow;
}
