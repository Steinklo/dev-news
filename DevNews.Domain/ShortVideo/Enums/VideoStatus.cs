namespace DevNews.Domain.ShortVideo.Enums;

/// <summary>
/// Lifecycle status of a short video.
/// </summary>
public enum VideoStatus
{
    Draft = 1,
    ScriptGenerated = 2,
    VideoGenerated = 3,
    Published = 4,
    Failed = 5
}
