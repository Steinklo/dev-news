using DevNews.Domain.Common;

namespace DevNews.Domain.VideoContent.Events;

/// <summary>
/// Domain event raised when a video has been validated by AI.
/// </summary>
public class VideoValidatedEvent(Guid videoContentId, bool isValid, string reason) : DomainEvent(videoContentId)
{
    public Guid VideoContentId { get; } = videoContentId;
    public bool IsValid { get; } = isValid;
    public string Reason { get; } = reason;
    public DateTimeOffset ValidatedAt { get; } = DateTimeOffset.UtcNow;
}
