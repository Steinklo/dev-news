namespace DevNews.Application.Common.Models;

/// <summary>
/// Output from the video generation service.
/// </summary>
public record GeneratedVideo(
    byte[] VideoData,
    int DurationSeconds,
    string ContentType);
